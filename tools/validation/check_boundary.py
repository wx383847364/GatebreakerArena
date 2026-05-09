#!/usr/bin/env python3
from __future__ import annotations

import argparse
import json
import re
import sys
from bisect import bisect_right
from dataclasses import dataclass
from pathlib import Path, PurePath
from typing import Dict, Iterable, List, Optional, Sequence, Tuple


SEVERITY_FAILURE = "failure"
SEVERITY_WARNING = "warning"
SEVERITY_INFO = "info"

SCOPE_DEFAULT = "default"
SCOPE_APP_ONLY = "app-only"
SCOPE_ALL_ASSETS = "all-assets"

REPO_ROOT = Path(__file__).resolve().parents[2]

EXCLUDED_PATH_PREFIXES = (
    "Assets/Editor/",
    "Assets/Tests/",
    "Assets/Editor/Tests/",
)

SCAN_SCOPE_SUMMARY = {
    SCOPE_DEFAULT: [
        "Assets/Scripts/App.AOT/**/*.cs",
        "Assets/Scripts/App.Shared/**/*.cs",
        "Assets/HotUpdateContent/Script/App.HotUpdate/**/*.cs",
    ],
    SCOPE_APP_ONLY: [
        "Assets/Scripts/App.AOT/**/*.cs",
        "Assets/Scripts/App.Shared/**/*.cs",
        "Assets/HotUpdateContent/Script/App.HotUpdate/**/*.cs",
    ],
    SCOPE_ALL_ASSETS: [
        "Assets/**/*.cs（排除 Editor / Tests 默认目录）",
    ],
}

WHITELIST_SUMMARY = [
    {
        "path": "Assets/Scripts/App.AOT/YooRuntimeAssets/YooAssetsRuntime.cs",
        "note": "允许 UnityEditor.AssetDatabase，但必须位于 #if UNITY_EDITOR 块内。",
    },
    {
        "path": "Assets/Scripts/App.AOT/HotUpdate/HybridClrLoader.cs",
        "note": "允许字符串字面量中的 App.HotUpdate；不允许 using App.HotUpdate 或真实类型引用。",
    },
]

RULES: Dict[str, Dict[str, str]] = {
    "BD001": {
        "severity": SEVERITY_FAILURE,
        "title": "AOT 越层实现引用",
        "description": "禁止 App.AOT 直接 using 或访问 App.HotUpdate 实现。",
    },
    "BD002": {
        "severity": SEVERITY_FAILURE,
        "title": "HotUpdate 反向依赖 AOT",
        "description": "禁止 App.HotUpdate 直接 using 或访问 App.AOT 实现。",
    },
    "BD003": {
        "severity": SEVERITY_FAILURE,
        "title": "Shared 混入运行时层依赖",
        "description": "禁止 App.Shared 依赖 App.AOT 或 App.HotUpdate。",
    },
    "BD004": {
        "severity": SEVERITY_FAILURE,
        "title": "Shared 混入 Unity 行为类",
        "description": "禁止 App.Shared 出现 UnityEditor、MonoBehaviour、ScriptableObject、AssetDatabase、Resources.Load。",
    },
    "BD005": {
        "severity": SEVERITY_FAILURE,
        "title": "HotUpdate/Shared 混入 Editor API",
        "description": "禁止 App.HotUpdate 和 App.Shared 混入 UnityEditor、AssetDatabase、EditorUtility、EditorSceneManager。",
    },
    "BD006": {
        "severity": SEVERITY_FAILURE,
        "title": "正式资源链路绕开 YooAssets",
        "description": "禁止在正式运行时代码中新增 Resources.Load / Resources.LoadAsync。",
    },
    "BD007": {
        "severity": SEVERITY_WARNING,
        "title": "模板资产运行时写入",
        "description": "警告非 Editor / 非 Tests 代码写入 ArenaTemplateData 可变方法。",
    },
    "BD008": {
        "severity": SEVERITY_WARNING,
        "title": "Shared 可疑业务实现扩张",
        "description": "警告 App.Shared 中可疑的 Service/Manager/Runtime/Bootstrap/Coordinator/Loader 实现类。",
    },
}

EDITOR_API_PATTERNS = (
    ("using UnityEditor", re.compile(r"\busing\s+UnityEditor(?:\.[\w.]+)?\s*;")),
    ("UnityEditor.", re.compile(r"(?<![\w.])UnityEditor\.")),
    ("AssetDatabase", re.compile(r"\bAssetDatabase\b")),
    ("EditorUtility", re.compile(r"\bEditorUtility\b")),
    ("EditorSceneManager", re.compile(r"\bEditorSceneManager\b")),
)

RESOURCES_LOAD_PATTERNS = (
    ("Resources.LoadAsync", re.compile(r"\bResources\s*\.\s*LoadAsync\s*\(")),
    ("Resources.Load", re.compile(r"\bResources\s*\.\s*Load\s*\(")),
)

SHARED_UNITY_BEHAVIOR_PATTERNS = (
    ("using UnityEditor", re.compile(r"\busing\s+UnityEditor(?:\.[\w.]+)?\s*;")),
    ("UnityEditor.", re.compile(r"(?<![\w.])UnityEditor\.")),
    ("MonoBehaviour", re.compile(r"\bMonoBehaviour\b")),
    ("ScriptableObject", re.compile(r"\bScriptableObject\b")),
    ("AssetDatabase", re.compile(r"\bAssetDatabase\b")),
    ("Resources.Load", re.compile(r"\bResources\s*\.\s*Load(?:Async)?\s*\(")),
)

TYPE_REFERENCE_PATTERNS = {
    "App.HotUpdate": re.compile(r"(?<![\w.])App\.HotUpdate\."),
    "App.AOT": re.compile(r"(?<![\w.])App\.AOT\."),
}

USING_REFERENCE_PATTERNS = {
    "App.HotUpdate": re.compile(r"\busing\s+App\.HotUpdate(?:\.[\w.]+)?\s*;"),
    "App.AOT": re.compile(r"\busing\s+App\.AOT(?:\.[\w.]+)?\s*;"),
}

MUTATING_TEMPLATE_METHODS = ("Resize", "SetValid", "SetColor", "SetCell")
SUSPICIOUS_SHARED_SUFFIXES = ("Service", "Manager", "Runtime", "Bootstrap", "Coordinator", "Loader")


@dataclass
class Finding:
    rule_id: str
    severity: str
    file: str
    line: int
    message: str

    def to_dict(self) -> Dict[str, object]:
        return {
            "rule_id": self.rule_id,
            "severity": self.severity,
            "file": self.file,
            "line": self.line,
            "message": self.message,
        }


@dataclass
class RuleSummary:
    rule_id: str
    severity: str
    title: str
    description: str
    count: int

    def to_dict(self) -> Dict[str, object]:
        return {
            "rule_id": self.rule_id,
            "severity": self.severity,
            "title": self.title,
            "description": self.description,
            "count": self.count,
        }


def normalize_path(value: Path | PurePath | str) -> str:
    return str(value).replace("\\", "/")


def is_excluded(relative_path: str) -> bool:
    return any(relative_path.startswith(prefix) for prefix in EXCLUDED_PATH_PREFIXES)


def collect_scope_files(repo_root: Path, scope: str) -> List[Path]:
    assets_root = repo_root / "Assets"
    files: Dict[str, Path] = {}

    def add_files(base: Path, include_editor: bool = False) -> None:
        if not base.exists():
            return
        for path in sorted(base.rglob("*.cs")):
            relative_path = normalize_path(path.relative_to(repo_root))
            if not include_editor and is_excluded(relative_path):
                continue
            files[relative_path] = path

    if scope == SCOPE_DEFAULT:
        add_files(repo_root / "Assets/Scripts/App.AOT")
        add_files(repo_root / "Assets/Scripts/App.Shared")
        add_files(repo_root / "Assets/HotUpdateContent/Script/App.HotUpdate")
    elif scope == SCOPE_APP_ONLY:
        add_files(repo_root / "Assets/Scripts/App.AOT")
        add_files(repo_root / "Assets/Scripts/App.Shared")
        add_files(repo_root / "Assets/HotUpdateContent/Script/App.HotUpdate")
    elif scope == SCOPE_ALL_ASSETS:
        add_files(assets_root)
    else:
        raise ValueError(f"Unsupported scope: {scope}")

    return [files[key] for key in sorted(files)]


def classify_file(relative_path: str) -> str:
    if relative_path.startswith("Assets/Scripts/App.AOT/"):
        return "aot"
    if relative_path.startswith("Assets/Scripts/App.Shared/"):
        return "shared"
    if relative_path.startswith("Assets/HotUpdateContent/Script/App.HotUpdate/"):
        return "hotupdate"
    return "other"


def compute_line_starts(text: str) -> List[int]:
    starts = [0]
    for index, char in enumerate(text):
        if char == "\n":
            starts.append(index + 1)
    return starts


def line_number_for_offset(line_starts: Sequence[int], offset: int) -> int:
    return bisect_right(line_starts, offset)


def strip_comments_and_strings(text: str) -> str:
    chars = list(text)
    length = len(chars)
    output: List[str] = []
    i = 0

    def preserve(char: str) -> str:
        return "\n" if char == "\n" else " "

    while i < length:
        char = chars[i]
        nxt = chars[i + 1] if i + 1 < length else ""

        if char == "/" and nxt == "/":
            output.append(" ")
            output.append(" ")
            i += 2
            while i < length and chars[i] != "\n":
                output.append(" ")
                i += 1
            continue

        if char == "/" and nxt == "*":
            output.append(" ")
            output.append(" ")
            i += 2
            while i < length:
                current = chars[i]
                next_char = chars[i + 1] if i + 1 < length else ""
                output.append(preserve(current))
                if current == "*" and next_char == "/":
                    output.append(" ")
                    i += 2
                    break
                i += 1
            continue

        if char == "@" and nxt == '"':
            output.append(" ")
            output.append(" ")
            i += 2
            while i < length:
                current = chars[i]
                next_char = chars[i + 1] if i + 1 < length else ""
                output.append(preserve(current))
                if current == '"' and next_char == '"':
                    output.append(" ")
                    i += 2
                    continue
                if current == '"':
                    i += 1
                    break
                i += 1
            continue

        if char == "$" and nxt == '"':
            output.append(" ")
            output.append(" ")
            i += 2
            while i < length:
                current = chars[i]
                if current == "\\" and i + 1 < length:
                    output.append(" ")
                    output.append(preserve(chars[i + 1]))
                    i += 2
                    continue
                output.append(preserve(current))
                if current == '"':
                    i += 1
                    break
                i += 1
            continue

        if char == '"':
            output.append(" ")
            i += 1
            while i < length:
                current = chars[i]
                if current == "\\" and i + 1 < length:
                    output.append(" ")
                    output.append(preserve(chars[i + 1]))
                    i += 2
                    continue
                output.append(preserve(current))
                if current == '"':
                    i += 1
                    break
                i += 1
            continue

        if char == "'":
            output.append(" ")
            i += 1
            while i < length:
                current = chars[i]
                if current == "\\" and i + 1 < length:
                    output.append(" ")
                    output.append(preserve(chars[i + 1]))
                    i += 2
                    continue
                output.append(preserve(current))
                if current == "'":
                    i += 1
                    break
                i += 1
            continue

        output.append(char)
        i += 1

    return "".join(output)


def compute_editor_active_lines(text: str) -> List[bool]:
    lines = text.splitlines()
    active_flags = [False] * (len(lines) + 1)
    stack: List[Tuple[bool, bool]] = []
    current_active = False

    for line_number, line in enumerate(lines, start=1):
        stripped = line.strip()
        if stripped.startswith("#if "):
            condition = stripped[4:].strip()
            is_editor_branch = condition == "UNITY_EDITOR"
            stack.append((current_active, is_editor_branch))
            current_active = is_editor_branch
            active_flags[line_number] = current_active
            continue

        if stripped.startswith("#elif "):
            if stack:
                parent_active, _ = stack[-1]
                condition = stripped[6:].strip()
                is_editor_branch = condition == "UNITY_EDITOR"
                stack[-1] = (parent_active, is_editor_branch)
                current_active = is_editor_branch and parent_active is False
            active_flags[line_number] = current_active
            continue

        if stripped.startswith("#else"):
            if stack:
                parent_active, branch_active = stack[-1]
                current_active = (not branch_active) and parent_active is False
                stack[-1] = (parent_active, current_active)
            active_flags[line_number] = current_active
            continue

        if stripped.startswith("#endif"):
            active_flags[line_number] = current_active
            if stack:
                parent_active, _ = stack.pop()
                current_active = parent_active
            else:
                current_active = False
            continue

        active_flags[line_number] = current_active

    return active_flags


def build_finding(rule_id: str, file_path: str, line: int, message: str) -> Finding:
    return Finding(
        rule_id=rule_id,
        severity=RULES[rule_id]["severity"],
        file=file_path,
        line=line,
        message=message,
    )


def match_to_line(text: str, line_starts: Sequence[int], match: re.Match[str]) -> int:
    return line_number_for_offset(line_starts, match.start())


def is_editor_whitelisted(relative_path: str, line_number: int, editor_active_lines: Sequence[bool]) -> bool:
    if relative_path != "Assets/Scripts/App.AOT/YooRuntimeAssets/YooAssetsRuntime.cs":
        return False
    if line_number < 1 or line_number >= len(editor_active_lines):
        return False
    return editor_active_lines[line_number]


def find_pattern_matches(
    rule_id: str,
    relative_path: str,
    sanitized_text: str,
    line_starts: Sequence[int],
    pattern: re.Pattern[str],
    message_template: str,
    editor_active_lines: Optional[Sequence[bool]] = None,
    allow_editor_whitelist: bool = False,
) -> List[Finding]:
    findings: List[Finding] = []
    for match in pattern.finditer(sanitized_text):
        line = match_to_line(sanitized_text, line_starts, match)
        if allow_editor_whitelist and editor_active_lines is not None and is_editor_whitelisted(relative_path, line, editor_active_lines):
            continue
        findings.append(build_finding(rule_id, relative_path, line, message_template))
    return findings


def parse_shared_suspicious_classes(sanitized_text: str) -> List[Tuple[str, int, int]]:
    class_pattern = re.compile(
        r"(?P<prefix>(?:\bpublic\b|\binternal\b|\bprivate\b|\bprotected\b|\babstract\b|\bsealed\b|\bstatic\b|\bpartial\b|\s)+)"
        r"\bclass\s+(?P<name>[A-Za-z_]\w*)\b",
        re.MULTILINE,
    )
    results: List[Tuple[str, int, int]] = []
    for match in class_pattern.finditer(sanitized_text):
        name = match.group("name")
        if not name.endswith(SUSPICIOUS_SHARED_SUFFIXES):
            continue
        brace_index = sanitized_text.find("{", match.end())
        if brace_index < 0:
            continue
        body_end = find_matching_brace(sanitized_text, brace_index)
        if body_end < 0:
            continue
        results.append((name, brace_index, body_end))
    return results


def find_matching_brace(text: str, open_index: int) -> int:
    depth = 0
    for index in range(open_index, len(text)):
        char = text[index]
        if char == "{":
            depth += 1
        elif char == "}":
            depth -= 1
            if depth == 0:
                return index
    return -1


def class_has_method_body(class_name: str, class_body: str) -> bool:
    method_pattern = re.compile(
        r"(?P<signature>"
        r"(?:\bpublic\b|\bprivate\b|\bprotected\b|\binternal\b|\bstatic\b|\bvirtual\b|\boverride\b|\basync\b|\bsealed\b|\bpartial\b|\s)+"
        r"(?:[\w<>\[\],?.]+\s+)+"
        r"(?P<method>[A-Za-z_]\w*)\s*\([^;{}]*\)\s*)"
        r"(?P<body>\{|=>)",
        re.MULTILINE,
    )
    for match in method_pattern.finditer(class_body):
        method_name = match.group("method")
        if method_name == class_name:
            continue
        return True
    return False


def analyze_template_writes(relative_path: str, sanitized_text: str, line_starts: Sequence[int]) -> List[Finding]:
    findings: List[Finding] = []
    if "ArenaTemplateData" not in sanitized_text:
        return findings

    variable_pattern = re.compile(r"\bArenaTemplateData\s+([A-Za-z_]\w*)\b")
    variable_names = {match.group(1) for match in variable_pattern.finditer(sanitized_text)}
    if not variable_names:
        return findings

    for variable_name in sorted(variable_names):
        for method_name in MUTATING_TEMPLATE_METHODS:
            pattern = re.compile(rf"\b{re.escape(variable_name)}\s*\.\s*{method_name}\s*\(")
            for match in pattern.finditer(sanitized_text):
                line = match_to_line(sanitized_text, line_starts, match)
                findings.append(
                    build_finding(
                        "BD007",
                        relative_path,
                        line,
                        f"检测到对 ArenaTemplateData 实例 `{variable_name}` 的可写调用 `{method_name}`，请人工确认未把模板资产当成运行时状态写回。",
                    )
                )
    return findings


def analyze_file(path: Path, repo_root: Path) -> List[Finding]:
    relative_path = normalize_path(path.relative_to(repo_root))
    file_kind = classify_file(relative_path)
    raw_text = path.read_text(encoding="utf-8")
    sanitized_text = strip_comments_and_strings(raw_text)
    line_starts = compute_line_starts(sanitized_text)
    editor_active_lines = compute_editor_active_lines(raw_text)

    findings: List[Finding] = []

    if file_kind == "aot":
        findings.extend(
            find_pattern_matches(
                "BD001",
                relative_path,
                sanitized_text,
                line_starts,
                USING_REFERENCE_PATTERNS["App.HotUpdate"],
                "App.AOT 中不应 using App.HotUpdate。",
            )
        )
        findings.extend(
            find_pattern_matches(
                "BD001",
                relative_path,
                sanitized_text,
                line_starts,
                TYPE_REFERENCE_PATTERNS["App.HotUpdate"],
                "App.AOT 中不应直接访问 App.HotUpdate 实现类型。",
            )
        )
        for label, pattern in RESOURCES_LOAD_PATTERNS:
            findings.extend(
                find_pattern_matches(
                    "BD006",
                    relative_path,
                    sanitized_text,
                    line_starts,
                    pattern,
                    f"App.AOT 正式资源链路中不应新增 {label}，应走 IAssetsRuntime / YooAssetsRuntime。",
                    editor_active_lines=editor_active_lines,
                    allow_editor_whitelist=False,
                )
            )

    if file_kind == "hotupdate":
        findings.extend(
            find_pattern_matches(
                "BD002",
                relative_path,
                sanitized_text,
                line_starts,
                USING_REFERENCE_PATTERNS["App.AOT"],
                "App.HotUpdate 中不应 using App.AOT。",
            )
        )
        findings.extend(
            find_pattern_matches(
                "BD002",
                relative_path,
                sanitized_text,
                line_starts,
                TYPE_REFERENCE_PATTERNS["App.AOT"],
                "App.HotUpdate 中不应直接访问 App.AOT 实现类型。",
            )
        )
        for label, pattern in EDITOR_API_PATTERNS:
            findings.extend(
                find_pattern_matches(
                    "BD005",
                    relative_path,
                    sanitized_text,
                    line_starts,
                    pattern,
                    f"App.HotUpdate 中不应出现 Editor API：{label}。",
                )
            )
        for label, pattern in RESOURCES_LOAD_PATTERNS:
            findings.extend(
                find_pattern_matches(
                    "BD006",
                    relative_path,
                    sanitized_text,
                    line_starts,
                    pattern,
                    f"App.HotUpdate 正式资源链路中不应新增 {label}，应走 IAssetsRuntime / YooAssetsRuntime。",
                )
            )

    if file_kind == "shared":
        findings.extend(
            find_pattern_matches(
                "BD003",
                relative_path,
                sanitized_text,
                line_starts,
                USING_REFERENCE_PATTERNS["App.AOT"],
                "App.Shared 中不应 using App.AOT。",
            )
        )
        findings.extend(
            find_pattern_matches(
                "BD003",
                relative_path,
                sanitized_text,
                line_starts,
                USING_REFERENCE_PATTERNS["App.HotUpdate"],
                "App.Shared 中不应 using App.HotUpdate。",
            )
        )
        findings.extend(
            find_pattern_matches(
                "BD003",
                relative_path,
                sanitized_text,
                line_starts,
                TYPE_REFERENCE_PATTERNS["App.AOT"],
                "App.Shared 中不应直接访问 App.AOT 实现类型。",
            )
        )
        findings.extend(
            find_pattern_matches(
                "BD003",
                relative_path,
                sanitized_text,
                line_starts,
                TYPE_REFERENCE_PATTERNS["App.HotUpdate"],
                "App.Shared 中不应直接访问 App.HotUpdate 实现类型。",
            )
        )
        for label, pattern in SHARED_UNITY_BEHAVIOR_PATTERNS:
            findings.extend(
                find_pattern_matches(
                    "BD004",
                    relative_path,
                    sanitized_text,
                    line_starts,
                    pattern,
                    f"App.Shared 中不应出现 Unity 行为或编辑器能力：{label}。",
                )
            )
        for label, pattern in EDITOR_API_PATTERNS:
            findings.extend(
                find_pattern_matches(
                    "BD005",
                    relative_path,
                    sanitized_text,
                    line_starts,
                    pattern,
                    f"App.Shared 中不应出现 Editor API：{label}。",
                )
            )
        suspicious_classes = parse_shared_suspicious_classes(sanitized_text)
        for class_name, body_start, body_end in suspicious_classes:
            class_body = sanitized_text[body_start : body_end + 1]
            if not class_has_method_body(class_name, class_body):
                continue
            line = line_number_for_offset(line_starts, body_start)
            findings.append(
                build_finding(
                    "BD008",
                    relative_path,
                    line,
                    f"App.Shared 中的 `{class_name}` 命中可疑业务实现命名，且包含方法体，请确认它仍属于稳定跨层类型。",
                )
            )

    findings.extend(analyze_template_writes(relative_path, sanitized_text, line_starts))
    return findings


def analyze_repo(repo_root: Path, scope: str = SCOPE_DEFAULT) -> Dict[str, object]:
    repo_root = repo_root.resolve()
    files = collect_scope_files(repo_root, scope)
    findings: List[Finding] = []
    for path in files:
        findings.extend(analyze_file(path, repo_root))

    findings.sort(key=lambda item: (item.severity, item.file, item.line, item.rule_id, item.message))

    counts = {rule_id: 0 for rule_id in RULES}
    for finding in findings:
        counts[finding.rule_id] += 1

    failures = [item for item in findings if item.severity == SEVERITY_FAILURE]
    warnings = [item for item in findings if item.severity == SEVERITY_WARNING]
    info = [item for item in findings if item.severity == SEVERITY_INFO]

    rule_summaries = [
        RuleSummary(
            rule_id=rule_id,
            severity=meta["severity"],
            title=meta["title"],
            description=meta["description"],
            count=counts[rule_id],
        ).to_dict()
        for rule_id, meta in RULES.items()
    ]

    return {
        "summary": {
            "repo_root": normalize_path(repo_root),
            "scope": scope,
            "scanned_files": len(files),
            "failures": len(failures),
            "warnings": len(warnings),
            "info": len(info),
            "exit_code": determine_exit_code(len(failures), len(warnings), fail_on_warning=False),
            "scope_patterns": SCAN_SCOPE_SUMMARY[scope],
            "whitelists": WHITELIST_SUMMARY,
            "excluded_prefixes": list(EXCLUDED_PATH_PREFIXES),
        },
        "rules": rule_summaries,
        "findings": [item.to_dict() for item in findings],
    }


def determine_exit_code(failure_count: int, warning_count: int, fail_on_warning: bool) -> int:
    if failure_count > 0:
        return 2
    if fail_on_warning and warning_count > 0:
        return 2
    return 0


def render_text_report(report: Dict[str, object], fail_on_warning: bool) -> str:
    summary = report["summary"]
    findings = report["findings"]
    lines = [
        "Gatebreaker Arena Boundary Check",
        f"Repo Root: {summary['repo_root']}",
        f"Scope: {summary['scope']}",
        f"Scanned Files: {summary['scanned_files']}",
        "",
        "Scope Patterns:",
    ]
    for item in summary["scope_patterns"]:
        lines.append(f"- {item}")

    lines += [
        "",
        "Excluded Prefixes:",
    ]
    for item in summary["excluded_prefixes"]:
        lines.append(f"- {item}")

    lines += [
        "",
        "Whitelists:",
    ]
    for item in summary["whitelists"]:
        lines.append(f"- {item['path']}: {item['note']}")

    findings_by_severity = {
        SEVERITY_FAILURE: [],
        SEVERITY_WARNING: [],
        SEVERITY_INFO: [],
    }
    for finding in findings:
        findings_by_severity[finding["severity"]].append(finding)

    for severity in (SEVERITY_FAILURE, SEVERITY_WARNING, SEVERITY_INFO):
        title = severity.capitalize()
        entries = findings_by_severity[severity]
        lines += ["", f"{title} ({len(entries)}):"]
        if not entries:
            lines.append("- none")
            continue
        for item in entries:
            lines.append(f"- [{item['rule_id']}] {item['file']}:{item['line']} {item['message']}")

    exit_code = determine_exit_code(summary["failures"], summary["warnings"], fail_on_warning)
    lines += [
        "",
        "Summary:",
        f"- Failures: {summary['failures']}",
        f"- Warnings: {summary['warnings']}",
        f"- Info: {summary['info']}",
        f"- Exit Code: {exit_code}",
        "- Exit Code Meaning: 0 = no failure, 2 = failure or fail-on-warning hit, 1 = script error",
    ]
    return "\n".join(lines)


def parse_args(argv: Optional[Sequence[str]] = None) -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Check Gatebreaker Arena runtime boundary rules.")
    parser.add_argument("--json", action="store_true", dest="json_output", help="Output machine-readable JSON.")
    parser.add_argument(
        "--scope",
        choices=(SCOPE_DEFAULT, SCOPE_APP_ONLY, SCOPE_ALL_ASSETS),
        default=SCOPE_DEFAULT,
        help="Select scan scope.",
    )
    parser.add_argument(
        "--fail-on-warning",
        action="store_true",
        help="Return exit code 2 when warnings are present.",
    )
    return parser.parse_args(argv)


def main(argv: Optional[Sequence[str]] = None) -> int:
    args = parse_args(argv)
    report = analyze_repo(REPO_ROOT, scope=args.scope)
    report["summary"]["exit_code"] = determine_exit_code(
        report["summary"]["failures"],
        report["summary"]["warnings"],
        args.fail_on_warning,
    )

    if args.json_output:
        print(json.dumps(report, ensure_ascii=False, indent=2))
    else:
        print(render_text_report(report, args.fail_on_warning))

    return report["summary"]["exit_code"]


if __name__ == "__main__":
    try:
        sys.exit(main())
    except KeyboardInterrupt:
        print("Boundary check interrupted.", file=sys.stderr)
        sys.exit(1)
    except Exception as exc:  # pragma: no cover - CLI safeguard
        print(f"Boundary check failed: {exc}", file=sys.stderr)
        sys.exit(1)
