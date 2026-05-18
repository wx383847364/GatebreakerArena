#!/usr/bin/env python3
import argparse
from datetime import datetime
import hashlib
import os
from pathlib import Path
import json
import re
import subprocess
import sys


LONG_DIR_NAME = "长期主文档"
ITER_DIR_NAME = "迭代记录"
LONG_INDEX_NAME = "主文档索引.md"
ITER_INDEX_NAME = "迭代记录索引.md"
UI_SYSTEM_DIR_NAME = "UI自动生成系统"
UI_SYSTEM_OVERVIEW_NAME = "00_总览.md"
AGENT_RULE_DOC = "协作与执行/Agent 启动与验收规范.md"
AGENT_STATUS_HEADING = "迭代文档默认分工状态"
AGENT_LINE_RE = re.compile(r"^\s*-?\s*Agent\s*([1-6])：(.+?)\s*$")
ALL_AGENT_MAX = 6
DEFAULT_PLACEHOLDERS = {
    "完成项": {"- 暂无"},
    "风险与阻塞": {"- 暂无"},
    "下一步": {"- 待补充"},
}
SESSION_BOOTSTRAP_DOC = "协作与执行/Codex新会话必读.md"
TASK_WRAPUP_DOC = "协作与执行/任务完成后自动维护文档.md"
GIT_COMMIT_RULE_DOC = "协作与执行/Git 提交建议与确认规则.md"
SUITABLE_COMMIT_CONFIRMATION = "提交确认：如需我继续执行，请回复 1（提交并推送） / 2（只提交） / 确认 / 提交 / 直接提交（只提交）。"
UNSUITABLE_COMMIT_CONFIRMATION = "提交确认：当前不建议直接提交；如需强制提交，请明确说明。"
LEGACY_LONG_DOCS = {
    f"{LONG_DIR_NAME}/方案与数据/UI Prefab 自动生成系统 Agent 规划入口.md",
    f"{LONG_DIR_NAME}/方案与数据/UI 自动生成系统隔离化孵化方案 v2.md",
    f"{LONG_DIR_NAME}/方案与数据/UI 自动生成系统隔离化孵化方案 v2 执行派工单与 Skill 规范.md",
}
TOPIC_PATTERNS = [
    ("文档整理", r"文档|主文档|迭代记录|索引|总览|规范|模板|跳转页|规则文档|update_project_docs|finalize_task"),
    ("流程与协作", r"会话|文档维护|交接|协作|启动卡|收尾|流程|自动维护"),
    ("审查与修复", r"Agent\s*6|审查|复审|修复|review|挑刺"),
    ("测试与验证", r"测试|验证|回归|smoke|qa|batchmode"),
    ("UI 与联调", r"UI|界面|prefab|presenter|controller|联调|图标"),
    ("配置表与数值扩展", r"配置表|导表|xlsx|json|bytes|catalog|数值|buildingtable|leveltable"),
    ("任务与长期进度", r"任务|成长|长期|进度|奖励|建筑升级|agency|playerLevel|playerlevel"),
    ("地图与棋盘", r"地图|棋盘|terrain|board|level|扫雷|开图|猫池"),
    ("边界与骨架", r"Shared|DTO|骨架|入口|bootstrap|组合层|boundary"),
]
PROCESS_ONLY_PATTERN = re.compile(
    r"文档|主文档|迭代记录|索引|启动卡|口令|模板|helper|收尾|去重|精简|pre-commit|commit helper|update_project_docs|finalize_task",
    re.IGNORECASE,
)
COMMIT_TITLE_PREFIX = {
    "文档整理": "文档：",
    "流程与协作": "流程：",
    "审查与修复": "修复：",
    "测试与验证": "测试：",
    "UI 与联调": "UI：",
    "配置表与数值扩展": "配置：",
    "任务与长期进度": "玩法：",
    "地图与棋盘": "玩法：",
    "边界与骨架": "架构：",
    "通用实现": "提交：",
    "未识别": "提交：",
}
PENDING_COMMIT_CACHE_RELATIVE = Path("codex") / "pending_commit_suggestion.json"
LAST_FINALIZE_REPORT_RELATIVE = Path("codex") / "last_finalize_report.json"
COMMIT_SEQUENCE_FETCH_CACHE_RELATIVE = Path("codex") / "commit_sequence_fetch_state.json"
CURRENT_COMMIT_SUGGESTION_CACHE_RELATIVE = Path("codex") / "current_commit_suggestion.json"
COMMIT_SEQUENCE_REGISTRY_RELATIVE = Path(LONG_DIR_NAME) / "协作与执行" / "状态登记" / "commit_module_sequences.json"
COMMIT_MODULE_SEQUENCE_START = 1
COMMIT_MODULE_DEFAULT = "610"
NUMBERED_COMMIT_RE = re.compile(r"^\[(\d{8})\]\s+")
DEFAULT_MERGE_COMMIT_MESSAGE_PREFIXES = ("Merge ",)
DEFAULT_COMMIT_SEQUENCE_FETCH_TIMEOUT_SECONDS = 5
DEFAULT_COMMIT_SEQUENCE_FETCH_MAX_AGE_SECONDS = 300
DOC_MODULE_PATTERNS = [
    ("210", r"项目总览|主文档索引|入口页|入口文档|研发入口|当前概览"),
    ("220", r"架构与边界|边界规范|边界文档|热更新边界规范|boundary"),
    ("260", r"迭代记录|启动卡|交接|给下一轮的人"),
    ("270", r"UI 自动生成系统|UiPrefabGenerator|DesignPacket|UiPrefabSpec|PrefabBindingManifest|执行派工单|sample manifest|sample spec"),
    ("250", r"配表|内容表|成长表|表方案|表结构|数据方案"),
    ("240", r"落地方案|长期方案|主线方案|Gatebreaker Arena|GDD|方案"),
    ("230", r"协作与执行|收尾|finalize_task|suggest-handoff|append-iteration|Codex新会话必读|Agent 启动|启动与验收|subagent|skill"),
]
PLAN_PROGRESS_HEADING = "完成情况"
PLAN_STATUS_VALUES = {"未开始", "进行中", "已完成"}
PLAN_PROGRESS_REQUIRED_PREFIXES = {
    "status": "- 当前状态：",
    "note": "- 进度说明：",
    "recent_update": "- 最近更新：",
}
PLAN_LANDING_PATTERN = re.compile(
    r"方案落地|落地方案|按.+方案|完成.+方案|方案阶段|plan progress|plan-doc",
    re.IGNORECASE,
)
PLAN_PROGRESS_FILENAME_EXCLUDE_PATTERN = re.compile(
    r"编号方案|使用与预览说明",
    re.IGNORECASE,
)


def ensure_dirs(doc_root: Path):
    long_dir = doc_root / LONG_DIR_NAME
    iter_dir = doc_root / ITER_DIR_NAME
    long_dir.mkdir(parents=True, exist_ok=True)
    iter_dir.mkdir(parents=True, exist_ok=True)
    return long_dir, iter_dir


def repo_root_for_doc_root(doc_root: Path) -> Path:
    return doc_root.resolve().parent


def run_git(doc_root: Path, args, timeout: int = 20):
    env = os.environ.copy()
    env.setdefault("GIT_TERMINAL_PROMPT", "0")
    return subprocess.run(
        ["git", "-C", str(repo_root_for_doc_root(doc_root)), *args],
        capture_output=True,
        text=True,
        check=False,
        timeout=timeout,
        env=env,
    )


def markdown_title(path: Path) -> str:
    try:
        text = path.read_text(encoding="utf-8")
    except FileNotFoundError:
        return path.stem
    for line in text.splitlines():
        if line.startswith("# "):
            return line[2:].strip()
    return path.stem


def relative_markdown_link(path: Path, root: Path) -> str:
    return path.relative_to(root).as_posix()


def encode_markdown_link_destination(path: str) -> str:
    return path.replace(" ", "%20")


def markdown_link_to(target: Path, from_file: Path) -> str:
    relative = os.path.relpath(target.resolve(), from_file.resolve().parent)
    return encode_markdown_link_destination(Path(relative).as_posix())


def extract_section_bullets(path: Path, heading: str):
    try:
        text = path.read_text(encoding="utf-8")
    except FileNotFoundError:
        return []
    lines = text.splitlines()
    target = f"## {heading}"
    bullets = []
    capture = False
    for line in lines:
        if line.strip() == target:
            capture = True
            continue
        if capture and line.strip().startswith("#"):
            break
        if capture and line.strip().startswith("- "):
            bullets.append(line.strip())
    return bullets


def extract_section_bullets_from_text(text: str, heading: str):
    lines = text.splitlines()
    target = f"## {heading}"
    bullets = []
    capture = False
    for line in lines:
        if line.strip() == target:
            capture = True
            continue
        if capture and line.strip().startswith("#"):
            break
        if capture and line.strip().startswith("- "):
            bullets.append(line.strip())
    return bullets


def extract_section_text(path: Path, heading: str) -> str:
    try:
        text = path.read_text(encoding="utf-8")
    except FileNotFoundError:
        return ""
    lines = text.splitlines()
    target = f"## {heading}"
    body = []
    capture = False
    for line in lines:
        if line.strip() == target:
            capture = True
            continue
        if capture and line.strip().startswith("## "):
            break
        if capture:
            stripped = line.strip()
            if stripped:
                body.append(stripped)
    return "\n".join(body).strip()


def extract_prefixed_value(path: Path, prefix: str) -> str:
    try:
        text = path.read_text(encoding="utf-8")
    except FileNotFoundError:
        return ""
    for line in text.splitlines():
        stripped = line.strip()
        if stripped.startswith(prefix):
            return stripped.replace(prefix, "", 1).strip()
    return ""


def extract_prefixed_value_from_bullets(bullets, prefix: str) -> str:
    for line in bullets:
        stripped = line.strip()
        if stripped.startswith(prefix):
            return stripped.replace(prefix, "", 1).strip()
    return ""


def extract_plan_progress(path: Path):
    bullets = extract_section_bullets(path, PLAN_PROGRESS_HEADING)
    status = extract_prefixed_value_from_bullets(bullets, PLAN_PROGRESS_REQUIRED_PREFIXES["status"])
    note = extract_prefixed_value_from_bullets(bullets, PLAN_PROGRESS_REQUIRED_PREFIXES["note"])
    recent_update = extract_prefixed_value_from_bullets(bullets, PLAN_PROGRESS_REQUIRED_PREFIXES["recent_update"])
    raw_status = status
    if raw_status not in PLAN_STATUS_VALUES:
        status = "未开始"
    return {
        "has_heading": bool(bullets),
        "status": status,
        "raw_status": raw_status,
        "note": note,
        "recent_update": recent_update,
    }


def plan_docs_dir(doc_root: Path) -> Path:
    return doc_root / LONG_DIR_NAME / "方案与数据"


def normalize_plan_doc_path(doc_root: Path, raw_path: str) -> Path:
    if not raw_path or not raw_path.strip():
        raise ValueError("--plan-doc 不能为空")

    value = raw_path.strip()
    repo_root = repo_root_for_doc_root(doc_root)
    candidates = []
    raw = Path(value)
    if raw.is_absolute():
        candidates.append(raw)
    else:
        candidates.extend(
            [
                repo_root / raw,
                doc_root / raw,
                plan_docs_dir(doc_root) / raw,
            ]
        )

    for candidate in candidates:
        resolved = candidate.resolve()
        if resolved.exists():
            try:
                resolved.relative_to(plan_docs_dir(doc_root).resolve())
            except ValueError as exc:
                raise ValueError(f"--plan-doc 必须指向 `方案与数据` 下的 md 文件：{resolved}") from exc
            if resolved.suffix != ".md":
                raise ValueError(f"--plan-doc 必须指向 md 文件：{resolved}")
            return resolved

    raise FileNotFoundError(f"未找到方案文档：{raw_path}")


def plan_progress_warnings(path: Path):
    try:
        text = path.read_text(encoding="utf-8")
    except FileNotFoundError:
        return [f"{path}: 文件不存在"]

    warnings = []
    has_heading = any(line.strip() == f"## {PLAN_PROGRESS_HEADING}" for line in text.splitlines())
    progress = extract_plan_progress(path)
    if not has_heading:
        warnings.append(f"{path}: 缺少 `## {PLAN_PROGRESS_HEADING}`")
        return warnings

    if progress["raw_status"] not in PLAN_STATUS_VALUES:
        warnings.append(f"{path}: `当前状态` 缺失或非法，应为 {' / '.join(sorted(PLAN_STATUS_VALUES))}")
    if not progress["note"]:
        warnings.append(f"{path}: 缺少 `进度说明`")
    if not progress["recent_update"]:
        warnings.append(f"{path}: 缺少 `最近更新`")
    return warnings


def validate_plan_progress(doc_root: Path, plan_doc: str = "", strict: bool = False):
    paths = []
    if plan_doc:
        paths = [normalize_plan_doc_path(doc_root, plan_doc)]
    else:
        root = plan_docs_dir(doc_root)
        if root.exists():
            paths = [
                path
                for path in sorted(root.rglob("*.md"))
                if path.is_file() and is_plan_progress_candidate(doc_root, path)
            ]

    warnings = []
    for path in paths:
        warnings.extend(plan_progress_warnings(path))

    if strict and warnings:
        raise ValueError("\n".join(warnings))
    return warnings


def is_plan_progress_candidate(doc_root: Path, path: Path) -> bool:
    try:
        rel = path.resolve().relative_to(doc_root.resolve()).as_posix()
    except ValueError:
        rel = ""
    if rel in LEGACY_LONG_DOCS:
        return False
    if PLAN_PROGRESS_FILENAME_EXCLUDE_PATTERN.search(path.name):
        return False
    try:
        text = path.read_text(encoding="utf-8")
    except FileNotFoundError:
        text = ""
    if any(line.strip() == f"## {PLAN_PROGRESS_HEADING}" for line in text.splitlines()):
        return True
    return bool(re.search(r"方案|计划", path.name))


def write_plan_progress(
    doc_root: Path,
    plan_doc: str,
    status: str,
    note: str,
    plan_update: str = "",
    date: str = "",
):
    if status not in PLAN_STATUS_VALUES:
        raise ValueError(f"--plan-status 必须是 {' / '.join(sorted(PLAN_STATUS_VALUES))} 之一")
    note = (note or "").strip()
    if not note:
        raise ValueError("--plan-note 不能为空")

    if date and not re.fullmatch(r"\d{4}-\d{2}-\d{2}", date):
        raise ValueError("--date 必须是 YYYY-MM-DD 格式")
    date = date or datetime.now().strftime("%Y-%m-%d")
    recent_text = (plan_update or note).strip()
    recent_update = f"{date}，{recent_text}"

    path = normalize_plan_doc_path(doc_root, plan_doc)
    text = path.read_text(encoding="utf-8")
    bullets = [
        f"- 当前状态：{status}",
        f"- 进度说明：{note}",
        f"- 最近更新：{recent_update}",
    ]
    updated = replace_section_bullets(text, PLAN_PROGRESS_HEADING, bullets)
    if updated != text:
        path.write_text(updated, encoding="utf-8")
    return {
        "path": path,
        "status": status,
        "note": note,
        "recent_update": recent_update,
        "changed": updated != text,
    }


def looks_like_plan_landing(summary: str, done, next_steps) -> bool:
    combined = "\n".join([summary or "", *done, *next_steps])
    return bool(PLAN_LANDING_PATTERN.search(combined))


def format_plan_progress_warnings(warnings, limit: int = 20):
    if not warnings:
        return []
    lines = [f"[warn] 方案完成情况格式待维护：{len(warnings)} 项"]
    for item in warnings[:limit]:
        lines.append(f"[warn] {item}")
    if len(warnings) > limit:
        lines.append(f"[warn] 其余 {len(warnings) - limit} 项已省略。")
    return lines


def normalize_agent_status_line(line: str) -> str:
    stripped = line.strip()
    if stripped.startswith("- "):
        stripped = stripped[2:].strip()
    match = AGENT_LINE_RE.match(stripped)
    if not match:
        raise ValueError(f"非法的 agent 状态行：{line}")
    return f"- Agent {match.group(1)}：{match.group(2).strip()}"


def agent_number_from_line(line: str):
    match = AGENT_LINE_RE.match(line.strip())
    if not match:
        return None
    return int(match.group(1))


def load_default_agent_statuses(doc_root: Path):
    path = doc_root / LONG_DIR_NAME / AGENT_RULE_DOC
    bullets = extract_section_bullets(path, AGENT_STATUS_HEADING)
    if len(bullets) != ALL_AGENT_MAX:
        raise ValueError(f"{path} 的 “{AGENT_STATUS_HEADING}” 必须正好包含 {ALL_AGENT_MAX} 条 agent 状态")
    normalized = [normalize_agent_status_line(line) for line in bullets]
    numbers = [agent_number_from_line(line) for line in normalized]
    if numbers != list(range(1, ALL_AGENT_MAX + 1)):
        raise ValueError(f"{path} 的 “{AGENT_STATUS_HEADING}” 必须按 Agent 1 到 Agent {ALL_AGENT_MAX} 排列")
    return normalized


def recent_files(paths, count=3):
    return sorted(paths, key=lambda p: p.stat().st_mtime, reverse=True)[:count]


def parse_iteration_key(path: Path):
    match = re.match(r"^迭代记录_(\d{8})_(\d{3})\.md$", path.name)
    if not match:
        return None, None
    date_raw, seq = match.groups()
    date_pretty = f"{date_raw[0:4]}-{date_raw[4:6]}-{date_raw[6:8]}"
    return date_pretty, seq


def classify_topic(text: str) -> str:
    compact = " ".join(text.split())
    if not compact:
        return "未识别"
    for label, pattern in TOPIC_PATTERNS:
        if re.search(pattern, compact, re.IGNORECASE):
            return label
    return "通用实现"


def classify_commit_module(text: str, topic: str) -> str:
    compact = " ".join(text.split())
    if not compact:
        return COMMIT_MODULE_DEFAULT

    if re.search(r"Assets/HotUpdateContent/Res|HotUpdate Res|Res 目录|资源 meta|Res/|图标资源 meta", compact, re.IGNORECASE):
        return "320"
    if re.search(r"图标|贴图|头像|icon", compact, re.IGNORECASE):
        return "310"
    if re.search(r"地图资源|关卡资源|terrain 资源|Map/|\.asset\.meta", compact, re.IGNORECASE):
        return "340"
    if re.search(r"场景|scene|预制体|prefab", compact, re.IGNORECASE):
        return "330"
    if re.search(r"DT_ModeRule|DT_BallRule|DT_AIRule|DT_MapRule|\.xlsx|xlsx|配置表", compact, re.IGNORECASE):
        return "410"
    if re.search(r"导表|导出|转换脚本|export report|配置转换", compact, re.IGNORECASE):
        return "420"
    if re.search(r"\.json|\.bytes|catalog|json / bytes|gatebreaker_core_config|gatebreaker_arena_config", compact, re.IGNORECASE):
        return "430"
    if re.search(r"schema|协议|数据结构|字段定义", compact, re.IGNORECASE):
        return "440"
    if re.search(r"UI 自动生成系统|UiPrefabGenerator|DesignPacket|UiPrefabSpec|PrefabBindingManifest|sample manifest|sample spec", compact, re.IGNORECASE):
        return "270" if topic == "文档整理" else "620"

    if topic == "文档整理":
        for code, pattern in DOC_MODULE_PATTERNS:
            if re.search(pattern, compact, re.IGNORECASE):
                return code
        return "230"

    if topic == "流程与协作":
        return "230"

    if topic == "审查与修复":
        return "520"

    if topic == "测试与验证":
        if re.search(r"单元测试|集成测试|EditMode|PlayMode", compact, re.IGNORECASE):
            return "510"
        if re.search(r"check_boundary|QA 脚本|校验脚本", compact, re.IGNORECASE):
            return "530"
        return "520"

    if topic == "UI 与联调":
        return "150"

    if topic == "配置表与数值扩展":
        return "410"

    if topic == "任务与长期进度":
        return "130"

    if topic == "地图与棋盘":
        return "140"

    if topic == "边界与骨架":
        if re.search(r"App\.Shared|Contracts|Shared DTO", compact, re.IGNORECASE):
            return "110"
        if re.search(r"App\.AOT|Bootstrap|Infrastructure|YooRuntimeAssets|HybridCLR", compact, re.IGNORECASE):
            return "120"
        return "110"

    return COMMIT_MODULE_DEFAULT


def first_non_empty(items):
    for item in items:
        if item and item.strip():
            return item.strip()
    return ""


def dedupe_preserve_order(items):
    seen = set()
    ordered = []
    for item in items:
        if not item:
            continue
        normalized = item.strip()
        if not normalized or normalized in seen:
            continue
        seen.add(normalized)
        ordered.append(normalized)
    return ordered


def bullet_text(item: str) -> str:
    stripped = item.strip()
    if stripped.startswith("- "):
        return stripped[2:].strip()
    return stripped


def is_process_only_item(text: str) -> bool:
    compact = bullet_text(text)
    if not compact:
        return False
    return classify_topic(compact) == "流程与协作" or bool(PROCESS_ONLY_PATTERN.search(compact))


def select_mainline_bullets(handoff_bullets, next_steps, overall_judgement: str):
    handoff_candidates = [item for item in handoff_bullets if not is_process_only_item(item)]
    next_candidates = [item for item in next_steps if not is_process_only_item(item)]

    if handoff_candidates:
        return handoff_candidates[:2]
    if overall_judgement and not is_process_only_item(overall_judgement):
        return [overall_judgement]
    if next_candidates:
        return next_candidates[:2]
    if overall_judgement:
        return [overall_judgement]
    return next_steps[:2]


def simplify_commit_title(text: str) -> str:
    value = " ".join(text.split()).strip("。；，, ")
    if not value:
        return "更新当前任务"
    value = re.sub(r"^(完成|继续|开始|正在|统一|补充|补齐|更新|修复|实现|新增|收口)", "", value).strip()
    value = value.strip("：: ")
    return value or "更新当前任务"


def commit_sequence_registry_path(doc_root: Path) -> Path:
    return doc_root / COMMIT_SEQUENCE_REGISTRY_RELATIVE


def commit_sequence_fetch_cache_path(doc_root: Path):
    git_dir = git_dir_for_doc_root(doc_root)
    if git_dir is None:
        return None
    return git_dir / COMMIT_SEQUENCE_FETCH_CACHE_RELATIVE


def parse_numbered_commit_title(title: str):
    match = NUMBERED_COMMIT_RE.match((title or "").strip())
    if not match:
        return None
    full_code = match.group(1)
    return {
        "full_code": full_code,
        "module_code": full_code[:3],
        "sequence": int(full_code[3:]),
    }


def read_commit_sequence_registry(doc_root: Path):
    path = commit_sequence_registry_path(doc_root)
    if not path.exists():
        return {}
    payload = json.loads(path.read_text(encoding="utf-8"))
    modules = payload.get("modules", payload)
    normalized = {}
    for module_code, value in modules.items():
        if isinstance(value, int):
            normalized[module_code] = value
        elif isinstance(value, dict) and isinstance(value.get("latest_sequence"), int):
            normalized[module_code] = value["latest_sequence"]
    return normalized


def write_commit_sequence_registry(doc_root: Path, modules):
    path = commit_sequence_registry_path(doc_root)
    path.parent.mkdir(parents=True, exist_ok=True)
    payload = {
        "version": 1,
        "modules": {module_code: modules[module_code] for module_code in sorted(modules)},
    }
    path.write_text(json.dumps(payload, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    return path


def list_git_remotes(doc_root: Path):
    try:
        result = run_git(doc_root, ["remote"])
    except (OSError, subprocess.TimeoutExpired):
        return []
    if result.returncode != 0:
        return []
    return [line.strip() for line in result.stdout.splitlines() if line.strip()]


def branch_upstream(doc_root: Path) -> str:
    try:
        result = run_git(doc_root, ["rev-parse", "--abbrev-ref", "--symbolic-full-name", "@{upstream}"])
    except (OSError, subprocess.TimeoutExpired):
        return ""
    if result.returncode != 0:
        return ""
    return result.stdout.strip()


def read_commit_sequence_fetch_cache(doc_root: Path):
    path = commit_sequence_fetch_cache_path(doc_root)
    if path is None or not path.exists():
        return None
    try:
        return json.loads(path.read_text(encoding="utf-8"))
    except json.JSONDecodeError:
        return None


def write_commit_sequence_fetch_cache(doc_root: Path, payload):
    path = commit_sequence_fetch_cache_path(doc_root)
    if path is None:
        return
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(payload, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def fetch_commit_sequence_baseline(
    doc_root: Path,
    timeout: int = DEFAULT_COMMIT_SEQUENCE_FETCH_TIMEOUT_SECONDS,
    max_age_seconds: int = DEFAULT_COMMIT_SEQUENCE_FETCH_MAX_AGE_SECONDS,
    allow_cached: bool = True,
):
    upstream = branch_upstream(doc_root)
    remotes = list_git_remotes(doc_root)
    if upstream:
        remote = upstream.split("/", 1)[0]
        branch_name = upstream.split("/", 1)[1]
        fetch_args = [
            "fetch",
            "--quiet",
            "--prune",
            "--no-tags",
            remote,
            f"+refs/heads/{branch_name}:refs/remotes/{upstream}",
        ]
        fetch_target = upstream
    elif len(remotes) == 1:
        fetch_args = ["fetch", "--quiet", "--prune", "--no-tags", remotes[0]]
        fetch_target = remotes[0]
    elif len(remotes) > 1:
        fetch_args = ["fetch", "--quiet", "--prune", "--no-tags", "--all"]
        fetch_target = "__all__"
    else:
        return {
            "attempted": False,
            "ok": True,
            "message": "未配置远端，跳过 fetch。",
        }

    cached = read_commit_sequence_fetch_cache(doc_root) if allow_cached else None
    if cached and cached.get("target") == fetch_target:
        fetched_at = cached.get("fetched_at", "")
        try:
            age_seconds = (datetime.now() - datetime.fromisoformat(fetched_at)).total_seconds()
        except ValueError:
            age_seconds = max_age_seconds + 1
        if age_seconds <= max_age_seconds:
            return {
                "attempted": False,
                "ok": True,
                "message": f"复用 {int(age_seconds)} 秒内的远端基线。",
            }

    try:
        result = run_git(doc_root, fetch_args, timeout=timeout)
    except OSError as exc:
        return {
            "attempted": True,
            "ok": False,
            "message": f"fetch 失败：{exc}",
        }
    except subprocess.TimeoutExpired:
        return {
            "attempted": True,
            "ok": False,
            "message": "fetch 超时，未使用远端最新基线。",
        }

    if result.returncode != 0:
        return {
            "attempted": True,
            "ok": False,
            "message": (result.stderr or result.stdout).strip() or "fetch 失败。",
        }
    write_commit_sequence_fetch_cache(
        doc_root,
        {
            "target": fetch_target,
            "fetched_at": datetime.now().isoformat(timespec="seconds"),
        },
    )
    return {
        "attempted": True,
        "ok": True,
        "message": "已更新远端基线。",
    }


def collect_commit_sequences_from_git_history(doc_root: Path):
    try:
        log_result = run_git(doc_root, ["log", "--format=%s", "--all"], timeout=30)
    except (OSError, subprocess.TimeoutExpired):
        return {}
    if log_result.returncode != 0:
        return {}

    modules = {}
    for line in log_result.stdout.splitlines():
        parsed = parse_numbered_commit_title(line)
        if not parsed:
            continue
        module_code = parsed["module_code"]
        modules[module_code] = max(modules.get(module_code, 0), parsed["sequence"])
    return modules


def latest_module_sequence_from_git_history(doc_root: Path, module_code: str) -> int:
    pattern = rf"^\[{module_code}"
    try:
        log_result = run_git(
            doc_root,
            ["log", "--format=%s", "--all", "--grep", pattern],
            timeout=15,
        )
    except (OSError, subprocess.TimeoutExpired):
        return 0
    if log_result.returncode != 0:
        return 0

    current_max = 0
    for line in log_result.stdout.splitlines():
        parsed = parse_numbered_commit_title(line)
        if not parsed or parsed["module_code"] != module_code:
            continue
        current_max = max(current_max, parsed["sequence"])
    return current_max


def merged_commit_sequence_modules(doc_root: Path, fetch_latest: bool = False):
    fetch_info = {
        "attempted": False,
        "ok": True,
        "message": "未执行 fetch。",
    }
    if fetch_latest:
        fetch_info = fetch_commit_sequence_baseline(doc_root)

    registry_modules = read_commit_sequence_registry(doc_root)
    git_modules = collect_commit_sequences_from_git_history(doc_root)
    merged = dict(registry_modules)
    for module_code, sequence in git_modules.items():
        merged[module_code] = max(merged.get(module_code, 0), sequence)
    return {
        "fetch": fetch_info,
        "registry_modules": registry_modules,
        "git_modules": git_modules,
        "modules": merged,
    }


def sync_commit_sequence_registry(doc_root: Path, fetch_latest: bool = False, stage: bool = False):
    snapshot = merged_commit_sequence_modules(doc_root, fetch_latest=fetch_latest)
    current = snapshot["registry_modules"]
    merged = snapshot["modules"]
    path = commit_sequence_registry_path(doc_root)
    changed = merged != current or not path.exists()
    if changed:
        path = write_commit_sequence_registry(doc_root, merged)
    if stage and path.exists():
        stage_repo_file(doc_root, path)
    return {
        "path": path,
        "changed": changed,
        **snapshot,
    }


def next_commit_sequence(doc_root: Path, module_code: str, fetch_latest: bool = False) -> int:
    if fetch_latest:
        fetch_commit_sequence_baseline(doc_root, allow_cached=False)
    registry_modules = read_commit_sequence_registry(doc_root)
    current = max(
        registry_modules.get(module_code, 0),
        latest_module_sequence_from_git_history(doc_root, module_code),
    )
    return current + 1 if current else COMMIT_MODULE_SEQUENCE_START


def format_commit_sequence(module_code: str, sequence: int) -> str:
    return f"[{module_code}{sequence:05d}]"


def build_commit_title(doc_root: Path, summary: str, done, topic: str) -> str:
    prefix = COMMIT_TITLE_PREFIX.get(topic, "提交：")
    base = first_non_empty([summary, done[0] if done else "", "更新当前任务"])
    simplified = simplify_commit_title(base)
    if not simplified.startswith(prefix):
        simplified = f"{prefix}{simplified}"
    module_code = classify_commit_module(" ".join([summary, *done]), topic)
    sequence = format_commit_sequence(module_code, next_commit_sequence(doc_root, module_code, fetch_latest=True))
    if simplified.startswith(f"{sequence} "):
        return simplified
    return f"{sequence} {simplified}"


def build_commit_content(summary: str, done):
    items = dedupe_preserve_order(done)
    if summary:
        items = dedupe_preserve_order(items + [summary])
    if not items:
        items = ["整理并收口当前任务改动"]
    return items[:4]


def stage_repo_file(doc_root: Path, path: Path):
    repo_root = repo_root_for_doc_root(doc_root)
    relative_path = path.resolve().relative_to(repo_root)
    add_result = run_git(doc_root, ["add", str(relative_path)])
    return add_result.returncode == 0


def git_dir_for_doc_root(doc_root: Path):
    repo_root = repo_root_for_doc_root(doc_root)
    try:
        result = subprocess.run(
            ["git", "-C", str(repo_root), "rev-parse", "--git-dir"],
            capture_output=True,
            text=True,
            check=False,
        )
    except OSError:
        return None

    if result.returncode != 0:
        return None

    git_dir = result.stdout.strip()
    if not git_dir:
        return None

    path = Path(git_dir)
    if not path.is_absolute():
        path = (repo_root / path).resolve()
    return path


def pending_commit_cache_path(doc_root: Path):
    git_dir = git_dir_for_doc_root(doc_root)
    if git_dir is None:
        return None
    return git_dir / PENDING_COMMIT_CACHE_RELATIVE


def current_commit_suggestion_cache_path(doc_root: Path):
    git_dir = git_dir_for_doc_root(doc_root)
    if git_dir is None:
        return None
    return git_dir / CURRENT_COMMIT_SUGGESTION_CACHE_RELATIVE


def last_finalize_report_path(doc_root: Path):
    git_dir = git_dir_for_doc_root(doc_root)
    if git_dir is None:
        return None
    return git_dir / LAST_FINALIZE_REPORT_RELATIVE


def current_head_commit(doc_root: Path) -> str:
    repo_root = repo_root_for_doc_root(doc_root)
    result = subprocess.run(
        ["git", "-C", str(repo_root), "rev-parse", "HEAD"],
        capture_output=True,
        text=True,
        check=False,
    )
    if result.returncode != 0:
        return ""
    return result.stdout.strip()


def current_head_subject(doc_root: Path) -> str:
    try:
        result = run_git(doc_root, ["log", "-1", "--format=%s"])
    except (OSError, subprocess.TimeoutExpired):
        return ""
    if result.returncode != 0:
        return ""
    return result.stdout.strip()


def current_worktree_status_bytes(doc_root: Path):
    repo_root = repo_root_for_doc_root(doc_root)
    try:
        result = subprocess.run(
            ["git", "-C", str(repo_root), "status", "--porcelain", "-z"],
            capture_output=True,
            text=False,
            check=False,
        )
    except OSError:
        return b""
    if result.returncode != 0:
        return b""
    return result.stdout


def current_worktree_snapshot_hash(doc_root: Path) -> str:
    return hashlib.sha256(current_worktree_status_bytes(doc_root)).hexdigest()


def file_content_hash(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def parse_worktree_entries(doc_root: Path):
    entries = []
    parts = current_worktree_status_bytes(doc_root).split(b"\0")
    index = 0
    while index < len(parts):
        raw = parts[index]
        index += 1
        if not raw:
            continue
        status = raw[:2].decode("utf-8", errors="replace")
        path_bytes = raw[3:]
        if status[0] in {"R", "C"} or status[1] in {"R", "C"}:
            if index < len(parts) and parts[index]:
                path_bytes = parts[index]
                index += 1
        path_text = path_bytes.decode("utf-8", errors="replace")
        entries.append(
            {
                "line": f"{status} {path_text}",
                "index_status": status[0],
                "worktree_status": status[1],
                "path": path_text,
            }
        )
    return entries


def commit_scope_for_path(path: str) -> str:
    normalized = path.replace("\\", "/")
    if normalized == "doc/长期主文档/协作与执行/状态登记/commit_module_sequences.json":
        return "registry"
    if (
        normalized.startswith("doc/长期主文档/协作与执行/")
        or normalized.startswith("tools/doc_maintenance/")
        or normalized.startswith(".githooks/")
        or normalized == "tools/repo_maintenance/install_git_hooks.sh"
    ):
        return "230"
    if normalized.startswith("doc/迭代记录/"):
        return "260"
    if normalized.startswith("doc/长期主文档/UI自动生成系统/"):
        return "270"
    if normalized.startswith("doc/长期主文档/方案与数据/"):
        filename = Path(normalized).name
        if re.search(r"配表|内容表|成长表|表方案|表结构|数据方案", filename, re.IGNORECASE):
            return "250"
        return "240"
    return classify_commit_module(normalized, classify_topic(normalized))


def is_bookkeeping_doc_path(path: str) -> bool:
    normalized = path.replace("\\", "/")
    return normalized in {
        "doc/长期主文档/主文档索引.md",
        "doc/迭代记录/迭代记录索引.md",
    } or normalized.startswith("doc/迭代记录/")


def is_auxiliary_test_path(path: str) -> bool:
    normalized = path.replace("\\", "/")
    return normalized.startswith("tools/tests/") or normalized.startswith("tests/")


def dominant_commit_modules(paths):
    normalized = dedupe_preserve_order(path.replace("\\", "/") for path in paths)
    if not normalized:
        return set()

    primary_paths = [path for path in normalized if not is_bookkeeping_doc_path(path) and not is_auxiliary_test_path(path)]
    if primary_paths:
        primary_modules = {commit_scope_for_path(path) for path in primary_paths}
        primary_modules.discard("registry")
        if primary_modules:
            return primary_modules

    modules = {commit_scope_for_path(path) for path in normalized}
    modules.discard("registry")
    return modules


def current_commit_title_and_content(doc_root: Path, paths):
    normalized = dedupe_preserve_order(path.replace("\\", "/") for path in paths)
    if normalized == ["doc/长期主文档/协作与执行/状态登记/commit_module_sequences.json"]:
        module_code = "230"
        title = f"{format_commit_sequence(module_code, next_commit_sequence(doc_root, module_code, fetch_latest=True))} 流程：同步协作模块编号登记"
        content = [
            "更新 commit_module_sequences.json 中协作模块的最新登记值",
            "同步 230 模块提交编号到最新状态",
        ]
        return module_code, title, content

    module_candidates = {commit_scope_for_path(path) for path in normalized}
    module_codes = dominant_commit_modules(normalized)
    if not module_codes and "registry" in module_candidates:
        module_codes = {"230"}
    if len(module_codes) != 1:
        return "", "", []

    module_code = next(iter(module_codes))
    if module_code == "230":
        if any(
            path in {
                "doc/长期主文档/协作与执行/commit_module_sequences.json",
                "doc/长期主文档/协作与执行/状态登记/commit_module_sequences.json",
                "doc/长期主文档/协作与执行/状态登记/README.md",
            }
            for path in normalized
        ):
            summary = "流程：迁移提交编号状态登记"
            content = [
                "将提交编号登记移动到协作与执行/状态登记目录",
                "保持 commit_module_sequences.json 纯数据结构并新增 README 说明用途",
                "同步 hooks、doc_maintenance 脚本、测试和长期文档路径引用",
            ]
            return module_code, f"{format_commit_sequence(module_code, next_commit_sequence(doc_root, module_code, fetch_latest=True))} {summary}", content[:4]
        if any("suggest-current-commit" in path or "update_project_docs.py" in path for path in normalized):
            summary = "流程：修复 Git hook 编号登记时机"
            content = [
                "将模块编号登记从 commit-msg 延后到 post-commit amend 流程",
                "更新 Git 提交建议与任务收尾规则文档的 hook 分工说明",
            ]
            if any(is_auxiliary_test_path(path) for path in normalized):
                content.append("补充 doc_maintenance 真实 hook 提交树回归测试")
            return module_code, f"{format_commit_sequence(module_code, next_commit_sequence(doc_root, module_code, fetch_latest=True))} {summary}", content[:4]
        else:
            summary = "流程：同步协作与执行规则"
    elif module_code == "260":
        summary = "文档：更新迭代记录"
    elif module_code == "270":
        summary = "文档：同步 UI 自动生成系统文档"
    elif module_code == "250":
        summary = "文档：更新配表与数据方案"
    elif module_code == "240":
        summary = "文档：更新长期方案文档"
    else:
        topic = classify_topic(" ".join(normalized))
        prefix = COMMIT_TITLE_PREFIX.get(topic, "提交：")
        summary = f"{prefix}收口当前模块改动"
    title = f"{format_commit_sequence(module_code, next_commit_sequence(doc_root, module_code, fetch_latest=True))} {summary}"
    content = [f"更新 {path}" for path in normalized[:4]]
    return module_code, title, content


def cached_changed_paths(doc_root: Path):
    try:
        result = run_git(doc_root, ["diff", "--cached", "--name-only", "-z"])
    except OSError:
        return []
    if result.returncode != 0:
        return []
    return [chunk for chunk in result.stdout.split("\0") if chunk]


def merge_source_from_subject(subject: str) -> str:
    stripped = (subject or "").strip()
    patterns = [
        r"^Merge (?:remote-tracking )?branch '([^']+)'",
        r"^Merge pull request #\d+ from ([^\s]+)",
        r"^Merge tag '([^']+)'",
    ]
    for pattern in patterns:
        match = re.match(pattern, stripped)
        if match:
            return match.group(1).strip()
    return "上游"


def merge_commit_title_and_content(doc_root: Path, subject: str, fetch_latest: bool = True):
    paths = cached_changed_paths(doc_root)
    module_codes = dominant_commit_modules(paths)
    if len(module_codes) == 1:
        module_code = next(iter(module_codes))
    else:
        module_code = COMMIT_MODULE_DEFAULT
    source = merge_source_from_subject(subject)
    title = f"{format_commit_sequence(module_code, next_commit_sequence(doc_root, module_code, fetch_latest=fetch_latest))} 合并：同步 {source} 分支"
    content = [
        f"合并 {source} 分支并同步当前分支基线",
        "保持合并提交标题和正文符合 Gatebreaker Arena 八位编号规则",
    ]
    if paths:
        content.append(f"纳入 {paths[0]} 等合并结果" if len(paths) > 1 else f"纳入 {paths[0]} 的合并结果")
    return module_code, title, content[:4]


def rewrite_commit_message(message_file: Path, title: str, content):
    original_lines = message_file.read_text(encoding="utf-8").splitlines()
    comment_start = next((index for index, line in enumerate(original_lines) if line.startswith("#")), len(original_lines))
    comment_lines = original_lines[comment_start:]
    body_lines = [title, ""]
    body_lines.extend(f"- {item}" for item in content)
    if comment_lines:
        body_lines.extend(["", *comment_lines])
    message_file.write_text("\n".join(body_lines).rstrip() + "\n", encoding="utf-8")


def suggest_current_commit(doc_root: Path):
    repo_check = run_git(doc_root, ["rev-parse", "--is-inside-work-tree"])
    if repo_check.returncode != 0:
        clear_current_commit_suggestion_cache(doc_root)
        clear_pending_commit_suggestion(doc_root)
        return {
            "suitable": False,
            "reason": "当前目录不在 Git 仓库中。",
            "title": "",
            "content": [],
        }

    cached = cached_current_commit_suggestion(doc_root)
    if cached:
        clear_pending_commit_suggestion(doc_root)
        return cached

    entries = parse_worktree_entries(doc_root)
    if not entries:
        commit = {
            "suitable": False,
            "reason": "当前仓库没有待提交改动。",
            "title": "",
            "content": [],
        }
        write_current_commit_suggestion_cache(doc_root, commit)
        clear_pending_commit_suggestion(doc_root)
        return commit

    staged_paths = dedupe_preserve_order(entry["path"] for entry in entries if entry["index_status"] not in {" ", "?"})
    unstaged_paths = dedupe_preserve_order(entry["path"] for entry in entries if entry["worktree_status"] not in {" ", "?"})
    untracked_paths = dedupe_preserve_order(entry["path"] for entry in entries if "?" in (entry["index_status"], entry["worktree_status"]))

    if staged_paths and unstaged_paths:
        partially_staged = [path for path in staged_paths if path in unstaged_paths]
        if partially_staged:
            commit = {
                "suitable": False,
                "reason": f"当前存在部分已暂存、部分未暂存的同一路径，边界不清晰：{', '.join(partially_staged[:3])}",
                "title": "",
                "content": [],
            }
            write_current_commit_suggestion_cache(doc_root, commit)
            clear_pending_commit_suggestion(doc_root)
            return commit
        extra_unstaged = [path for path in unstaged_paths if path not in staged_paths]
        if extra_unstaged:
            commit = {
                "suitable": False,
                "reason": f"当前同时存在已暂存改动和未暂存改动，边界不清晰：{', '.join(extra_unstaged[:3])}",
                "title": "",
                "content": [],
            }
            write_current_commit_suggestion_cache(doc_root, commit)
            clear_pending_commit_suggestion(doc_root)
            return commit

    all_paths = dedupe_preserve_order(staged_paths + unstaged_paths + untracked_paths)
    module_candidates = {commit_scope_for_path(path) for path in all_paths}
    effective_modules = dominant_commit_modules(all_paths)
    if not effective_modules and "registry" in module_candidates:
        effective_modules = {"230"}
    if len(effective_modules) > 1:
        commit = {
            "suitable": False,
            "reason": f"当前改动跨了多个模块，暂不建议混提：{', '.join(sorted(effective_modules))}",
            "title": "",
            "content": [],
        }
        write_current_commit_suggestion_cache(doc_root, commit)
        clear_pending_commit_suggestion(doc_root)
        return commit

    module_code, title, content = current_commit_title_and_content(doc_root, all_paths)
    if not module_code or not title:
        commit = {
            "suitable": False,
            "reason": "当前改动边界暂时无法稳定归类，建议先补充上下文后再提交。",
            "title": "",
            "content": [],
        }
        write_current_commit_suggestion_cache(doc_root, commit)
        clear_pending_commit_suggestion(doc_root)
        return commit

    commit = {
        "suitable": True,
        "reason": "",
        "title": title,
        "content": content,
    }
    write_current_commit_suggestion_cache(doc_root, commit)
    write_pending_commit_suggestion(doc_root, commit)
    return commit


def write_pending_commit_suggestion(doc_root: Path, commit):
    cache_path = pending_commit_cache_path(doc_root)
    if cache_path is None:
        return
    cache_path.parent.mkdir(parents=True, exist_ok=True)
    payload = {
        "title": commit["title"],
        "content": commit["content"],
        "head_commit": current_head_commit(doc_root),
        "created_at": datetime.now().isoformat(timespec="seconds"),
    }
    parsed = parse_numbered_commit_title(commit["title"])
    if parsed:
        payload["module_code"] = parsed["module_code"]
        payload["sequence"] = parsed["sequence"]
    cache_path.write_text(json.dumps(payload, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def clear_pending_commit_suggestion(doc_root: Path):
    cache_path = pending_commit_cache_path(doc_root)
    if cache_path is None or not cache_path.exists():
        return
    cache_path.unlink()


def read_pending_commit_suggestion(doc_root: Path):
    cache_path = pending_commit_cache_path(doc_root)
    if cache_path is None or not cache_path.exists():
        return None
    return json.loads(cache_path.read_text(encoding="utf-8"))


def write_current_commit_suggestion_cache(doc_root: Path, suggestion):
    if suggestion.get("suitable"):
        clear_current_commit_suggestion_cache(doc_root)
        return
    cache_path = current_commit_suggestion_cache_path(doc_root)
    if cache_path is None:
        return
    cache_path.parent.mkdir(parents=True, exist_ok=True)
    upstream = branch_upstream(doc_root)
    payload = {
        "head_commit": current_head_commit(doc_root),
        "upstream": upstream,
        "worktree_status_hash": current_worktree_snapshot_hash(doc_root),
        "suggestion": suggestion,
        "created_at": datetime.now().isoformat(timespec="seconds"),
    }
    cache_path.write_text(json.dumps(payload, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def clear_current_commit_suggestion_cache(doc_root: Path):
    cache_path = current_commit_suggestion_cache_path(doc_root)
    if cache_path is None or not cache_path.exists():
        return
    cache_path.unlink()


def read_current_commit_suggestion_cache(doc_root: Path):
    cache_path = current_commit_suggestion_cache_path(doc_root)
    if cache_path is None or not cache_path.exists():
        return None
    try:
        return json.loads(cache_path.read_text(encoding="utf-8"))
    except json.JSONDecodeError:
        return None


def cached_current_commit_suggestion(doc_root: Path):
    payload = read_current_commit_suggestion_cache(doc_root)
    if not payload:
        return None
    if payload.get("head_commit") != current_head_commit(doc_root):
        return None
    if payload.get("upstream", "") != branch_upstream(doc_root):
        return None
    if payload.get("worktree_status_hash") != current_worktree_snapshot_hash(doc_root):
        return None
    suggestion = payload.get("suggestion")
    if not isinstance(suggestion, dict):
        return None
    if suggestion.get("suitable"):
        return None
    return suggestion


def validate_commit_message(doc_root: Path, message_file: Path, fetch_latest: bool = True, stage_registry: bool = False):
    fetch_info = {
        "attempted": False,
        "ok": True,
        "message": "未执行 fetch。",
    }
    subject = ""
    for line in message_file.read_text(encoding="utf-8").splitlines():
        stripped = line.strip()
        if stripped and not stripped.startswith("#"):
            subject = stripped
            break

    if not subject:
        return {
            "valid": False,
            "skipped": False,
            "subject": "",
            "expected_title": "[TMMSSSSS] 前缀：摘要",
            "fetch": fetch_info,
            "reason": "提交标题不能为空，且必须以 [TMMSSSSS] 八位编号开头。",
        }

    if subject.startswith(DEFAULT_MERGE_COMMIT_MESSAGE_PREFIXES):
        _, title, content = merge_commit_title_and_content(doc_root, subject, fetch_latest=fetch_latest)
        rewrite_commit_message(message_file, title, content)
        subject = title

    parsed = parse_numbered_commit_title(subject)
    if not parsed:
        return {
            "valid": False,
            "skipped": False,
            "subject": subject,
            "expected_title": "[TMMSSSSS] 前缀：摘要",
            "fetch": fetch_info,
            "reason": "提交标题必须以 [TMMSSSSS] 八位编号开头。",
        }

    if fetch_latest:
        fetch_info = fetch_commit_sequence_baseline(doc_root, allow_cached=False)
    registry_modules = read_commit_sequence_registry(doc_root)
    current_max = max(
        registry_modules.get(parsed["module_code"], 0),
        latest_module_sequence_from_git_history(doc_root, parsed["module_code"]),
    )
    head_parsed = parse_numbered_commit_title(current_head_subject(doc_root))
    is_amending_current_head = bool(head_parsed and head_parsed["full_code"] == parsed["full_code"])
    expected_sequence = head_parsed["sequence"] if is_amending_current_head else current_max + 1 if current_max else COMMIT_MODULE_SEQUENCE_START

    if parsed["sequence"] != expected_sequence:
        return {
            "valid": False,
            "subject": subject,
            "expected_title": format_commit_sequence(parsed["module_code"], expected_sequence),
            "current_max": current_max,
            "fetch": fetch_info,
            "reason": (
                f"模块 {parsed['module_code']} 当前最新编号是 {current_max:05d}，"
                f"下一次应使用 {format_commit_sequence(parsed['module_code'], expected_sequence)}。"
            ),
        }

    registry_path = commit_sequence_registry_path(doc_root)
    updated_modules = dict(registry_modules)
    updated_modules[parsed["module_code"]] = max(updated_modules.get(parsed["module_code"], 0), parsed["sequence"])
    registry_changed = updated_modules != registry_modules or not registry_path.exists()
    if registry_changed and stage_registry:
        registry_path = write_commit_sequence_registry(doc_root, updated_modules)
    if stage_registry and registry_path.exists():
        stage_repo_file(doc_root, registry_path)

    return {
        "valid": True,
        "skipped": False,
        "subject": subject,
        "module_code": parsed["module_code"],
        "sequence": parsed["sequence"],
        "registry_path": str(registry_path),
        "registry_changed": registry_changed,
        "fetch": fetch_info,
    }


def current_worktree_status(doc_root: Path):
    repo_root = doc_root.resolve().parent
    result = subprocess.run(
        ["git", "-C", str(repo_root), "status", "--short"],
        capture_output=True,
        text=True,
        check=False,
    )
    if result.returncode != 0:
        return []
    return [line.rstrip() for line in result.stdout.splitlines() if line.strip()]


def validate_last_finalize_report(doc_root: Path):
    payload = read_last_finalize_report(doc_root)
    if not payload:
        return {
            "valid": False,
            "reasons": ["未找到最近一次完整收尾状态缓存。"],
            "payload": None,
        }

    reasons = []
    current_head = current_head_commit(doc_root)
    current_status = current_worktree_status(doc_root)
    report_text = payload.get("report_text", "")

    if not payload.get("head_commit"):
        reasons.append("收尾状态缺少 head_commit，无法确认它对应的是哪次仓库状态。")
    elif current_head != payload.get("head_commit"):
        reasons.append("当前 HEAD 已变化，最近一次完整收尾状态不再对应当前提交基线。")

    if payload.get("worktree_status", []) != current_status:
        reasons.append("当前工作区状态已变化，最近一次完整收尾状态不再对应当前工作区。")

    for marker in ("文档维护：", "Git 提交建议：", "会话建议："):
        if marker not in report_text:
            reasons.append(f"收尾输出缺少 `{marker}` 段，不能视为完整收尾。")

    if "Git 提交建议：适合提交" in report_text and SUITABLE_COMMIT_CONFIRMATION not in report_text:
        reasons.append("收尾输出缺少适合提交场景的最新 `提交确认` 提示词，不能视为完整收尾。")
    if "Git 提交建议：暂不建议提交" in report_text and UNSUITABLE_COMMIT_CONFIRMATION not in report_text:
        reasons.append("收尾输出缺少暂不建议提交场景的最新 `提交确认` 提示词，不能视为完整收尾。")

    plan_doc = payload.get("plan_doc", "")
    if plan_doc:
        try:
            plan_path = normalize_plan_doc_path(doc_root, plan_doc)
            validate_plan_progress(doc_root, str(plan_path), strict=True)
            expected_plan_hash = payload.get("plan_doc_hash", "")
            if not expected_plan_hash:
                reasons.append("收尾状态缺少绑定方案内容指纹，无法确认方案进度是否仍匹配。")
            elif expected_plan_hash != file_content_hash(plan_path):
                reasons.append("绑定方案文档内容已变化，最近一次完整收尾状态不再对应当前方案进度。")
        except (FileNotFoundError, ValueError) as exc:
            reasons.append(f"绑定方案完成情况校验失败：{exc}")

    return {
        "valid": not reasons,
        "reasons": reasons,
        "payload": payload,
        "current_head": current_head,
        "current_worktree_status": current_status,
    }


def write_last_finalize_report(doc_root: Path, payload):
    report_path = last_finalize_report_path(doc_root)
    if report_path is None:
        return None
    report_path.parent.mkdir(parents=True, exist_ok=True)
    report_path.write_text(json.dumps(payload, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    return report_path


def clear_last_finalize_report(doc_root: Path):
    report_path = last_finalize_report_path(doc_root)
    if report_path is None or not report_path.exists():
        return
    report_path.unlink()


def read_last_finalize_report(doc_root: Path):
    report_path = last_finalize_report_path(doc_root)
    if report_path is None or not report_path.exists():
        return None
    return json.loads(report_path.read_text(encoding="utf-8"))


def suggest_commit_message(doc_root: Path, summary: str, done, risks, agent6_review: str, doc_log_skipped: bool):
    if doc_log_skipped:
        return {
            "suitable": False,
            "reason": "这轮属于事务性协助，没有形成需要写入项目状态的独立里程碑。",
            "title": "",
            "content": [],
        }

    if agent6_review == "failed":
        return {
            "suitable": False,
            "reason": "Agent 6 审查未通过，当前仍在修复链中，暂不适合提交。",
            "title": "",
            "content": [],
        }

    if agent6_review == "pending":
        return {
            "suitable": False,
            "reason": "Agent 6 审查尚未完成，当前还不能确认这轮改动已经收口。",
            "title": "",
            "content": [],
        }

    if agent6_review == "deferred":
        return {
            "suitable": False,
            "reason": "Agent 6 审查当前处于待回补复审，正式审查闭环尚未完成。",
            "title": "",
            "content": [],
        }

    content = build_commit_content(summary, done)
    topics = {
        classify_topic(item)
        for item in [summary, *done]
        if item and item.strip()
    }
    topics.discard("未识别")
    topics.discard("通用实现")
    if len(topics) > 1:
        return {
            "suitable": False,
            "reason": "当前总结里仍混有多条主线，建议先拆分后再分别生成提交标题和内容。",
            "title": "",
            "content": [],
        }

    if risks and not done:
        return {
            "suitable": False,
            "reason": "当前仍以风险说明为主，缺少清晰完成项，暂不建议直接提交。",
            "title": "",
            "content": [],
        }

    topic = next(iter(topics), classify_topic(summary))
    return {
        "suitable": True,
        "reason": "",
        "title": build_commit_title(doc_root, summary, done, topic),
        "content": content,
    }


def extract_first_text_code_block(path: Path) -> str:
    try:
        text = path.read_text(encoding="utf-8")
    except FileNotFoundError:
        return ""
    match = re.search(r"```text\s*(.*?)```", text, re.DOTALL)
    if not match:
        return ""
    return match.group(1).strip()


def suggest_agent_start(bullets):
    patterns = [
        (r"Agent\s*5|测试|质量", "Agent 5：测试与质量保障"),
        (r"Agent\s*4|UI|联调", "Agent 4：UI 与验证"),
        (r"Agent\s*3|任务|长期进度", "Agent 3：任务与长期进度"),
        (r"Agent\s*2|地图|棋盘", "Agent 2：地图与棋盘"),
        (r"Agent\s*1|骨架|Shared|入口冻结", "Agent 1：边界与骨架"),
    ]
    for bullet in bullets:
        if is_process_only_item(bullet):
            continue
        for pattern, label in patterns:
            if re.search(pattern, bullet, re.IGNORECASE):
                return label
    return "暂无明确建议"


def latest_iteration_context(doc_root: Path):
    iteration_docs = scan_iteration_docs(doc_root)
    latest = iteration_docs[-1] if iteration_docs else None
    if not latest:
        return {
            "stage": "暂无",
            "mainline": "暂无",
            "start_agent": "暂无明确建议",
        }
    current_status = extract_section_bullets(latest, "当前状态")
    handoff = extract_section_bullets(latest, "给下一轮的人")
    next_steps = extract_section_bullets(latest, "下一步")
    stage = current_status[0][2:] if current_status else "暂无"
    if stage.startswith("当前阶段："):
        stage = stage.replace("当前阶段：", "", 1).strip()
    overall_judgement = extract_prefixed_value(latest, "- 整体判断：")
    mainline_bullets = select_mainline_bullets(handoff, next_steps, overall_judgement)
    mainline = "；".join(bullet_text(item) for item in mainline_bullets) if mainline_bullets else "暂无"
    start_agent = extract_prefixed_value(latest, "- 建议起始 Agent：")
    if not start_agent or start_agent == "待补充":
        start_agent = suggest_agent_start(handoff + next_steps)
    return {
        "stage": stage,
        "mainline": mainline,
        "start_agent": start_agent,
    }


def classify_long_doc(path: Path):
    if path.name == "项目总览.md":
        return "项目总览"
    parts = set(path.parts)
    if UI_SYSTEM_DIR_NAME in parts:
        return "UI自动生成系统"
    if "方案与数据" in parts:
        return "方案与数据"
    if "架构与边界" in parts:
        return "架构与边界"
    if "协作与执行" in parts:
        return "协作与执行"
    return "协作与执行"


def scan_long_docs(doc_root: Path):
    files = []
    for path in sorted(doc_root.rglob("*.md")):
        rel = path.relative_to(doc_root).as_posix()
        if rel.startswith(f"{ITER_DIR_NAME}/"):
            continue
        if rel == f"{LONG_DIR_NAME}/{LONG_INDEX_NAME}":
            continue
        if rel in LEGACY_LONG_DOCS:
            continue
        files.append(path)
    return files


def scan_iteration_docs(doc_root: Path):
    iter_dir = doc_root / ITER_DIR_NAME
    files = []
    for path in sorted(iter_dir.glob("*.md")):
        if path.name == ITER_INDEX_NAME:
            continue
        files.append(path)
    return files


def replace_section_bullets(text: str, heading: str, bullets) -> str:
    heading_line = f"## {heading}"
    lines = text.splitlines()
    for i, line in enumerate(lines):
        if line.strip() == heading_line:
            body_start = i + 1
            while body_start < len(lines) and lines[body_start].strip() == "":
                body_start += 1
            body_end = body_start
            while body_end < len(lines) and not lines[body_end].startswith("## "):
                body_end += 1

            new_lines = lines[: i + 1] + [""]
            new_lines.extend(bullets)
            if body_end < len(lines):
                new_lines += [""] + lines[body_end:]
            return "\n".join(new_lines).rstrip() + "\n"

    block = "\n".join(bullets)
    return text.rstrip() + f"\n\n{heading_line}\n\n{block}\n"


def clean_placeholder_bullets(text: str, heading: str, placeholders) -> str:
    bullets = extract_section_bullets_from_text(text, heading)
    if not bullets:
        return text
    real_items = [line for line in bullets if line not in placeholders]
    if not real_items:
        return text
    cleaned = [line for line in bullets if line not in placeholders]
    return replace_section_bullets(text, heading, cleaned)


def ensure_default_agent_statuses(text: str, default_statuses) -> str:
    bullets = extract_section_bullets_from_text(text, "分工状态")
    if not bullets:
        return replace_section_bullets(text, "分工状态", default_statuses)

    existing = {}
    replaced = False
    for line in bullets:
        number = agent_number_from_line(line)
        if number is None:
            continue
        if line.strip().endswith("待补充"):
            existing[number] = default_statuses[number - 1]
            replaced = True
        else:
            existing[number] = normalize_agent_status_line(line)

    if not existing:
        return replace_section_bullets(text, "分工状态", default_statuses)

    merged = []
    for index, default_line in enumerate(default_statuses, start=1):
        merged.append(existing.get(index, default_line))
        if index not in existing:
            replaced = True

    if replaced or merged != bullets:
        return replace_section_bullets(text, "分工状态", merged)
    return text


def apply_agent_status_overrides(text: str, overrides):
    if not overrides:
        return text
    bullets = extract_section_bullets_from_text(text, "分工状态")
    if not bullets:
        raise ValueError("分工状态章节不存在，无法覆写 agent 状态")

    merged = {}
    for line in bullets:
        number = agent_number_from_line(line)
        if number is not None:
            merged[number] = normalize_agent_status_line(line)

    for line in overrides:
        normalized = normalize_agent_status_line(line)
        number = agent_number_from_line(normalized)
        merged[number] = normalized

    ordered = [merged[index] for index in range(1, ALL_AGENT_MAX + 1)]
    return replace_section_bullets(text, "分工状态", ordered)


def write_project_overview(doc_root: Path):
    overview_path = doc_root / LONG_DIR_NAME / "项目总览.md"
    if not overview_path.exists():
        return

    context = latest_iteration_context(doc_root)
    bullets = [
        f"- 当前阶段：{context['stage']}",
        f"- 当前主线：{context['mainline']}",
        f"- 当前建议起点：{context['start_agent']}",
    ]

    text = overview_path.read_text(encoding="utf-8")
    text = replace_section_bullets(text, "当前阶段", bullets)
    overview_path.write_text(text, encoding="utf-8")


def write_long_index(doc_root: Path):
    long_dir, _ = ensure_dirs(doc_root)
    index_path = long_dir / LONG_INDEX_NAME
    long_docs = scan_long_docs(doc_root)
    context = latest_iteration_context(doc_root)
    overview_path = next((p for p in long_docs if p.name == "项目总览.md"), None)
    latest_docs = recent_files(long_docs, 3)
    grouped = {
        "项目总览": [],
        "UI自动生成系统": [],
        "方案与数据": [],
        "架构与边界": [],
        "协作与执行": [],
    }
    for path in long_docs:
        grouped[classify_long_doc(path)].append(path)
    ui_system_overview = next(
        (
            path
            for path in grouped["UI自动生成系统"]
            if path.name == UI_SYSTEM_OVERVIEW_NAME
        ),
        grouped["UI自动生成系统"][0] if grouped["UI自动生成系统"] else None,
    )
    lines = [
        "# 主文档索引",
        "",
        "本索引用“研发入口页”的方式组织长期主文档，帮助快速判断当前项目阶段、先看什么，以及下一步从哪里开始。",
        "",
        "## 当前概览",
        "",
        f"- 长期文档总数：{len(long_docs)}",
        f"- 当前阶段：{context['stage']}",
        f"- 当前主线：{context['mainline']}",
        f"- 推荐先读：{markdown_title(overview_path) if overview_path else '暂无'}",
        f"- 当前实现起点：{context['start_agent']}",
        "",
        "## 稳定决策速览",
        "",
        "- `App.AOT` 只放宿主、资源、持久化、HybridCLR 和平台基础设施",
        "- `App.Shared` 只放稳定 DTO / 接口 / 事件",
        "- `App.HotUpdate` 承载正式玩法",
        "- 静态地图或场地模板只作为模板输入，不保存运行时状态",
        "- 正式运行时资源统一走 YooAssets",
        "- UI 只做表现和交互，不承载奖励公式和地图生成逻辑",
        "",
        "## 推荐阅读路径",
        "",
        "### 如果你是第一次接手项目",
        "",
        f"- [ ] 先读 [{markdown_title(overview_path) if overview_path else '项目总览'}]({markdown_link_to(overview_path, index_path) if overview_path else '项目总览.md'})",
        "- [ ] 再读方案与数据目录中的主方案文档",
        "- [ ] 再读协作与执行目录中的配对与任务模板文档",
        "",
        "### 如果你要开始写代码",
        "",
        "- [ ] 先读架构与边界目录中的规范文档",
        "- [ ] 再读协作与执行目录中的任务模板文档",
        "- [ ] 最后打开最新一轮迭代记录索引",
        "",
        "## 文档分区",
        "",
        "### A. 项目总览",
        "",
    ]
    if not grouped["项目总览"]:
        lines.append("- [ ] 暂无")
    else:
        for path in grouped["项目总览"]:
            title = markdown_title(path)
            lines.append(f"- [ ] [{title}]({markdown_link_to(path, index_path)})")
    lines += [
        "",
        "### B. 方案与数据",
        "",
    ]
    if not grouped["方案与数据"]:
        if ui_system_overview is None:
            lines.append("- [ ] 暂无")
    else:
        if ui_system_overview is not None:
            lines.append(f"- [ ] [UI 自动生成系统专区]({markdown_link_to(ui_system_overview, index_path)})")
        for path in grouped["方案与数据"]:
            title = markdown_title(path)
            plan_progress = extract_plan_progress(path)
            label = f"[{plan_progress['status']}] {title}"
            lines.append(f"- [ ] [{label}]({markdown_link_to(path, index_path)})")
    lines += [
        "",
        "### C. 架构与边界",
        "",
    ]
    if not grouped["架构与边界"]:
        lines.append("- [ ] 暂无")
    else:
        for path in grouped["架构与边界"]:
            title = markdown_title(path)
            lines.append(f"- [ ] [{title}]({markdown_link_to(path, index_path)})")
    lines += [
        "",
        "### D. 协作与执行",
        "",
        "- 收尾入口统一看任务收尾与文档维护流程；如果本轮要按确认词继续执行 `git commit / push`，再看 Git 提交建议与确认规则。",
        "",
    ]
    if not grouped["协作与执行"]:
        lines.append("- [ ] 暂无")
    else:
        for path in grouped["协作与执行"]:
            title = markdown_title(path)
            lines.append(f"- [ ] [{title}]({markdown_link_to(path, index_path)})")
    lines += [
        "",
        "## 最近更新",
        "",
    ]
    if not latest_docs:
        lines.append("- 暂无最近更新文档")
    else:
        for path in latest_docs:
            title = markdown_title(path)
            lines.append(f"- [{title}]({markdown_link_to(path, index_path)})")
    lines += [
        "",
        "## 维护建议",
        "",
        "- 这份索引不只是目录，而是长期研发入口页",
        "- 项目阶段变化时，优先更新“当前概览”",
        "- 稳定边界变化时，优先更新“稳定决策速览”",
        "- 新增长期文档时，再补“文档分区”",
    ]
    index_path.write_text("\n".join(lines) + "\n", encoding="utf-8")


def write_iteration_index(doc_root: Path):
    _, iter_dir = ensure_dirs(doc_root)
    index_path = iter_dir / ITER_INDEX_NAME
    iteration_docs = scan_iteration_docs(doc_root)
    latest = iteration_docs[-1] if iteration_docs else None
    context = latest_iteration_context(doc_root)
    latest_done = extract_section_bullets(latest, "完成项")[:3] if latest else []
    latest_risks = extract_section_bullets(latest, "风险与阻塞")[:3] if latest else []
    latest_next = extract_section_bullets(latest, "下一步")[:3] if latest else []
    latest_updates = recent_files(iteration_docs, 3)
    suggested_agent = context["start_agent"]
    lines = [
        "# 迭代记录索引",
        "",
        "本索引用项目清单方式汇总每一轮工作的记录，方便快速找到最近一轮状态、风险和下一步。",
        "",
        "## 当前概览",
        "",
        f"- 迭代记录总数：{len(iteration_docs)}",
        f"- 最新记录：{markdown_title(latest) if latest else '暂无'}",
        "",
        "## 建议动作",
        "",
        "- [ ] 优先阅读最新一轮记录",
        "- [ ] 确认最新风险与阻塞",
        "- [ ] 确认下一步是否已经拆成可执行任务",
        "",
        "## 最近完成",
        "",
    ]
    if not latest_done:
        lines.append("- 暂无记录的完成项")
    else:
        lines.extend(latest_done)
    lines += [
        "",
        "## 最新阻塞",
        "",
    ]
    if not latest_risks:
        lines.append("- 暂无记录的阻塞")
    else:
        lines.extend(latest_risks)
    lines += [
        "",
        "## 下一步入口",
        "",
    ]
    if not latest_next:
        lines.append("- 暂无记录的下一步")
    else:
        lines.extend(latest_next)
    lines += [
        "",
        "## 建议从哪个 Agent 开始继续",
        "",
        f"- {suggested_agent}",
        "",
        "## 最近更新记录",
        "",
    ]
    if not latest_updates:
        lines.append("- 暂无最近更新记录")
    else:
        for path in latest_updates:
            title = markdown_title(path)
            lines.append(f"- [{title}]({markdown_link_to(path, index_path)})")
    lines += [
        "",
        "## 记录清单",
        "",
    ]
    if not iteration_docs:
        lines.append("- [ ] 暂无迭代记录")
    else:
        grouped = {}
        others = []
        for path in reversed(iteration_docs):
            date_pretty, seq = parse_iteration_key(path)
            if date_pretty:
                grouped.setdefault(date_pretty, []).append((seq, path))
            else:
                others.append(path)

        for date_pretty in sorted(grouped.keys(), reverse=True):
            lines.append(f"### {date_pretty}")
            lines.append("")
            for seq, path in grouped[date_pretty]:
                title = markdown_title(path)
                lines.append(f"- [ ] {seq} / [{title}]({markdown_link_to(path, index_path)})")
            lines.append("")

        if others:
            lines.append("### 其他记录")
            lines.append("")
            for path in others:
                title = markdown_title(path)
                lines.append(f"- [ ] [{title}]({markdown_link_to(path, index_path)})")
    index_path.write_text("\n".join(lines) + "\n", encoding="utf-8")


def sync_indexes(doc_root: Path):
    write_project_overview(doc_root)
    write_long_index(doc_root)
    write_iteration_index(doc_root)


def today_sequence(iter_dir: Path) -> int:
    date_str = datetime.now().strftime("%Y%m%d")
    pattern = re.compile(rf"^迭代记录_{date_str}_(\d{{3}})\.md$")
    seq = 0
    for path in iter_dir.glob(f"迭代记录_{date_str}_*.md"):
        match = pattern.match(path.name)
        if match:
            seq = max(seq, int(match.group(1)))
    return seq + 1


def create_iteration(doc_root: Path, title: str, goal: str, status: str):
    _, iter_dir = ensure_dirs(doc_root)
    default_agent_statuses = load_default_agent_statuses(doc_root)
    now = datetime.now()
    date_str = now.strftime("%Y%m%d")
    seq = today_sequence(iter_dir)
    path = iter_dir / f"迭代记录_{date_str}_{seq:03d}.md"
    previous_files = scan_iteration_docs(doc_root)
    previous = previous_files[-1] if previous_files else None
    previous_label = markdown_title(previous) if previous else "无"
    previous_link = (
        f"[{previous_label}]({markdown_link_to(previous, path)})"
        if previous
        else "无"
    )
    lines = [
        f"# 迭代记录 {now.strftime('%Y-%m-%d')} {seq:03d}",
        "",
        "## 迭代标识",
        "",
        f"- 迭代编号：{date_str}-{seq:03d}",
        f"- 记录日期：{now.strftime('%Y-%m-%d')}",
        f"- 当前状态：{status}",
        f"- 承接上一轮：{previous_link}",
        f"- 建议起始 Agent：待补充",
        "",
        "---",
        "",
        "## 本轮主题",
        "",
        title,
        "",
        "## 本轮目标",
        "",
        f"- [ ] {goal}",
        "",
        "## 当前状态",
        "",
        f"- 当前阶段：{status}",
        "- 整体判断：待补充",
        "",
        "## 关键结论",
        "",
        "- 待补充",
        "",
        "## 分工状态",
        "",
        *default_agent_statuses,
        "",
        "## 工作日志",
        "",
        f"### {now.strftime('%Y-%m-%d %H:%M')}",
        "",
        "- 创建本轮迭代记录",
        "",
        "## 完成项",
        "",
        "- 暂无",
        "",
        "## 风险与阻塞",
        "",
        "- 暂无",
        "",
        "## 下一步",
        "",
        "- 待补充",
        "",
        "---",
        "",
        "## 给下一轮的人",
        "",
        "- 待补充",
        "",
    ]
    path.write_text("\n".join(lines), encoding="utf-8")
    sync_indexes(doc_root)
    return path


def latest_iteration_file(doc_root: Path):
    files = scan_iteration_docs(doc_root)
    if not files:
        raise FileNotFoundError("未找到迭代记录，请先使用 new-iteration 创建")
    return files[-1]


def insert_under_heading(text: str, heading: str, block: str) -> str:
    heading_line = f"## {heading}"
    lines = text.splitlines()
    for i, line in enumerate(lines):
        if line.strip() == heading_line:
            insert_at = i + 1
            while insert_at < len(lines) and lines[insert_at].strip() == "":
                insert_at += 1
            block_lines = block.rstrip("\n").splitlines()
            new_lines = lines[: i + 1] + [""] + block_lines
            if insert_at < len(lines):
                new_lines += [""] + lines[insert_at:]
            return "\n".join(new_lines).rstrip() + "\n"
    return text.rstrip() + f"\n\n{heading_line}\n\n{block.rstrip()}\n"


def section_lines(text: str, heading: str) -> list[str]:
    heading_line = f"## {heading}"
    lines = text.splitlines()
    for i, line in enumerate(lines):
        if line.strip() != heading_line:
            continue

        end = len(lines)
        for j in range(i + 1, len(lines)):
            if lines[j].startswith("## "):
                end = j
                break
        return lines[i + 1 : end]

    return []


def heading_has_bullet(text: str, heading: str, item: str) -> bool:
    if not item:
        return False

    expected = "- " + item.strip()
    return any(line.strip() == expected for line in section_lines(text, heading))


def append_iteration(doc_root: Path, target: str, latest: bool, summary: str, done, risks, next_steps, agent_statuses):
    if latest:
        path = latest_iteration_file(doc_root)
    else:
        path = Path(target)
        if not path.is_absolute():
            path = doc_root / target
    text = path.read_text(encoding="utf-8")

    default_agent_statuses = load_default_agent_statuses(doc_root)
    text = ensure_default_agent_statuses(text, default_agent_statuses)
    text = apply_agent_status_overrides(text, agent_statuses)

    timestamp = datetime.now().strftime("%Y-%m-%d %H:%M")
    if summary and not heading_has_bullet(text, "工作日志", summary):
        block = f"### {timestamp}\n\n- {summary}"
        text = insert_under_heading(text, "工作日志", block)

    for item in done:
        if not heading_has_bullet(text, "完成项", item):
            text = insert_under_heading(text, "完成项", f"- {item}")
    for item in risks:
        if not heading_has_bullet(text, "风险与阻塞", item):
            text = insert_under_heading(text, "风险与阻塞", f"- {item}")
    for item in next_steps:
        if not heading_has_bullet(text, "下一步", item):
            text = insert_under_heading(text, "下一步", f"- {item}")

    for heading, placeholders in DEFAULT_PLACEHOLDERS.items():
        text = clean_placeholder_bullets(text, heading, placeholders)

    path.write_text(text, encoding="utf-8")
    sync_indexes(doc_root)
    return path


def latest_iteration_metadata(doc_root: Path):
    files = scan_iteration_docs(doc_root)
    latest = files[-1] if files else None
    if not latest:
        return {
            "path": None,
            "date": "",
            "title": "",
            "theme": "",
            "topic": "未识别",
        }
    date_pretty, _ = parse_iteration_key(latest)
    theme = extract_section_text(latest, "本轮主题")
    title = markdown_title(latest)
    return {
        "path": latest,
        "date": date_pretty or "",
        "title": title,
        "theme": theme,
        "topic": classify_topic(f"{title}\n{theme}"),
    }


def build_session_bootstrap_prompt(doc_root: Path, goal: str, done, risks, next_steps, docs):
    startup_doc = doc_root / LONG_DIR_NAME / SESSION_BOOTSTRAP_DOC
    intro = extract_first_text_code_block(startup_doc)
    if not intro:
        intro = (
            "按长期主文档规则执行。每次完成一个任务后都执行文档维护流程。"
            "每次任务结束时都必须汇报文档维护、Git 提交建议、会话建议和迭代记录建议。"
        )

    lines = [intro]
    if goal:
        lines.append(f"当前目标：{goal}")
    if done:
        lines.append("已完成：")
        for item in done[:3]:
            lines.append(f"- {item}")
    if risks:
        lines.append("当前风险：")
        for item in risks[:3]:
            lines.append(f"- {item}")
    if next_steps:
        lines.append("当前下一步：")
        for item in next_steps[:3]:
            lines.append(f"- {item}")
    if docs:
        lines.append("建议先读：")
        for item in docs:
            lines.append(f"- {item}")
    return "\n".join(lines)


def build_session_description(title: str, goal: str):
    title = (title or "").strip()
    goal = (goal or "").strip()
    if title and goal:
        return f"{title}。目标：{goal}"
    if title:
        return title
    return goal


def suggest_handoff(
    doc_root: Path,
    summary: str,
    done,
    risks,
    next_steps,
    agent6_review: str,
    session_mode: str,
    iteration_mode: str,
    next_session_title: str,
    next_session_goal: str,
    next_session_docs,
    doc_log_skipped: bool,
    context_compressed: bool,
    session_major_task_count: int,
    plan_doc: str = "",
):
    plan_progress = None
    plan_progress_reminder = ""
    if plan_doc:
        plan_path = normalize_plan_doc_path(doc_root, plan_doc)
        validate_plan_progress(doc_root, str(plan_path), strict=True)
        progress = extract_plan_progress(plan_path)
        plan_progress = {
            "path": plan_path.resolve().as_posix(),
            "status": progress["status"],
            "note": progress["note"],
            "recent_update": progress["recent_update"],
        }
    elif looks_like_plan_landing(summary, done, next_steps):
        plan_progress_reminder = "本轮描述看起来涉及方案落地；如确实落地了 `方案与数据` 下的方案，请在收尾入口补传 --plan-doc / --plan-status / --plan-note。"

    commit = suggest_commit_message(doc_root, summary, done, risks, agent6_review, doc_log_skipped)
    if commit["suitable"]:
        write_pending_commit_suggestion(doc_root, commit)
    else:
        clear_pending_commit_suggestion(doc_root)
    latest = latest_iteration_metadata(doc_root)
    today = datetime.now().strftime("%Y-%m-%d")
    next_focus = first_non_empty([next_session_goal, next_steps[0] if next_steps else "", summary])
    next_topic = classify_topic(next_focus)
    review_gate_open = agent6_review in {"passed", "passed-with-suggestions", "not-required"}
    review_failed = agent6_review == "failed"
    review_pending = agent6_review == "pending"
    review_deferred = agent6_review == "deferred"
    session_quality_reasons = []
    if context_compressed:
        session_quality_reasons.append("本会话已出现过自动压缩背景信息，后续继续堆叠上下文的收益会明显下降。")
    if session_major_task_count >= 2:
        session_quality_reasons.append(
            f"当前会话内已连续完成 {session_major_task_count} 个大任务，当前更适合开一个新会话重新装载上下文。"
        )
    session_quality_degraded = bool(session_quality_reasons)

    if review_failed:
        session_advice = "建议新开修复/复审会话"
        session_reason = "Agent 6 未通过，本轮只能先修复并复审，不能直接切到下一阶段。"
        title = next_session_title or "修复 Agent 6 审查问题"
        goal = next_session_goal or first_non_empty(
            [
                next_steps[0] if next_steps else "",
                risks[0] if risks else "",
                "按 Agent 6 审查意见修复当前交付，并准备复审。",
            ]
        )
    elif review_deferred:
        if session_quality_degraded:
            session_advice = "建议新开修复/复审会话"
            session_reason = "Agent 6 审查已转为待回补复审，当前不能宣告下一阶段通过。 " + " ".join(session_quality_reasons)
            title = next_session_title or "继续当前修复/验证链"
            goal = next_session_goal or first_non_empty(
                [
                    next_steps[0] if next_steps else "",
                    risks[0] if risks else "",
                    "继续当前修复与验证，并在审查结果回传后回补复审。",
                ]
            )
        else:
            session_advice = "继续当前修复/验证会话"
            session_reason = "Agent 6 审查已转为待回补复审；当前可继续已知修复与验证，但暂不生成下一个会话可复制提示词。"
            title = ""
            goal = ""
    elif review_pending and not session_quality_degraded:
        session_advice = "继续当前会话"
        session_reason = "Agent 6 审查尚未完成，暂不建议生成下一个会话可复制提示词。"
        title = ""
        goal = ""
    elif review_pending and session_quality_degraded:
        session_advice = "建议新开修复/复审会话"
        session_reason = "Agent 6 审查已发起但结果尚未回传，当前不能宣告下一阶段通过。 " + " ".join(session_quality_reasons)
        title = next_session_title or "继续当前修复/复审链"
        goal = next_session_goal or first_non_empty(
            [
                next_steps[0] if next_steps else "",
                summary,
                "等待 Agent 6 审查结果回传，并继续当前修复链。",
            ]
        )
    else:
        should_new_session = False
        if session_mode == "new":
            should_new_session = True
        elif session_mode == "continue":
            should_new_session = False
        elif session_quality_degraded:
            should_new_session = True
        else:
            should_new_session = bool(next_focus) and (
                latest["topic"] != next_topic
                or len(done) >= 2
                or bool(re.search(r"完成|收口|通过|里程碑", summary))
            )

        if should_new_session and (review_gate_open or session_quality_degraded):
            session_advice = "建议新开下一阶段会话"
            if session_quality_degraded:
                session_reason = " ".join(session_quality_reasons)
            else:
                session_reason = "当前任务已基本收口，下一步可以按独立主题重新装载上下文。"
            title = next_session_title or first_non_empty(
                [
                    next_steps[0] if next_steps else "",
                    summary,
                    "继续下一阶段任务",
                ]
            )
            goal = next_session_goal or first_non_empty(
                [
                    next_steps[0] if next_steps else "",
                    summary,
                    "继续推进下一阶段任务",
                ]
            )
        else:
            session_advice = "继续当前会话"
            session_reason = "下一步仍然依赖当前上下文，暂时不需要切到新的会话。"
            title = ""
            goal = ""

    if iteration_mode == "new":
        iteration_advice = "建议新建迭代记录"
        iteration_reason = "主控显式要求新建迭代记录。"
    elif iteration_mode == "continue":
        iteration_advice = "继续当前迭代记录"
        iteration_reason = "主控显式要求沿用当前迭代记录。"
    else:
        if not latest["path"]:
            iteration_advice = "建议新建迭代记录"
            iteration_reason = "当前还没有迭代记录，下一轮需要先建立记录。"
        elif review_failed or review_pending or review_deferred:
            iteration_advice = "继续当前迭代记录"
            iteration_reason = "当前仍在同一轮修复/复审链路内，不应提前切出新的迭代文件。"
        elif latest["date"] and latest["date"] != today:
            iteration_advice = "建议新建迭代记录"
            iteration_reason = "已跨天，按当前规则默认新建新的迭代记录。"
        elif latest["topic"] != next_topic and next_focus:
            iteration_advice = "建议新建迭代记录"
            iteration_reason = "下一步主题已明显切换，应该把这轮和下一轮分开记录。"
        else:
            iteration_advice = "继续当前迭代记录"
            iteration_reason = "下一步仍属于同一主线，继续沿用当前迭代记录更清晰。"

    doc_paths = []
    for item in next_session_docs:
        candidate = Path(item)
        if candidate.is_absolute():
            doc_paths.append(candidate.as_posix())
        else:
            doc_paths.append((doc_root / item).resolve().as_posix())

    default_docs = [
        (doc_root / LONG_DIR_NAME / SESSION_BOOTSTRAP_DOC).resolve().as_posix(),
        (doc_root / LONG_DIR_NAME / TASK_WRAPUP_DOC).resolve().as_posix(),
        (doc_root / LONG_DIR_NAME / GIT_COMMIT_RULE_DOC).resolve().as_posix(),
    ]
    if latest["path"]:
        default_docs.append(latest["path"].resolve().as_posix())
    docs = dedupe_preserve_order(doc_paths + default_docs)

    startup_prompt = ""
    if title and goal:
        startup_prompt = build_session_bootstrap_prompt(doc_root, goal, done, risks, next_steps, docs)
    session_description = build_session_description(title, goal)

    return {
        "doc_maintenance_status": "未执行。原因是：这轮属于事务性协助，没有写入项目总览或迭代记录。" if doc_log_skipped else "已执行",
        "commit_suggestion": commit,
        "session_advice": session_advice,
        "session_reason": session_reason,
        "iteration_advice": iteration_advice,
        "iteration_reason": iteration_reason,
        "title": title,
        "goal": goal,
        "session_description": session_description,
        "docs": docs,
        "startup_prompt": startup_prompt,
        "doc_log_skipped": doc_log_skipped,
        "plan_progress": plan_progress,
        "plan_progress_reminder": plan_progress_reminder,
    }


def format_handoff_report(report):
    lines = [
        f"文档维护：{report['doc_maintenance_status']}",
    ]
    commit = report["commit_suggestion"]
    if commit["suitable"]:
        lines += [
            "Git 提交建议：适合提交",
            f"标题：{commit['title']}",
            "内容：",
            "```text",
        ]
        for item in commit["content"]:
            lines.append(f"- {item}")
        lines.append("```")
        lines.append(SUITABLE_COMMIT_CONFIRMATION)
    else:
        lines.append(f"Git 提交建议：暂不建议提交。原因是：{commit['reason']}")
        lines.append(UNSUITABLE_COMMIT_CONFIRMATION)

    lines.append(f"会话建议：{report['session_advice']}")
    lines += [
        f"原因是：{report['session_reason']}",
        f"迭代记录建议：{report['iteration_advice']}",
        f"原因是：{report['iteration_reason']}",
    ]
    if report["doc_log_skipped"]:
        lines += [
            "文档落点：本轮被判定为事务性协助，本次只输出建议，不写入迭代记录。",
        ]
    else:
        lines += [
            "文档落点：本次建议默认只在收尾输出中展示，不自动写入迭代记录。",
        ]
    if report.get("plan_progress"):
        progress = report["plan_progress"]
        lines.append(f"方案进度：已校验 {progress['path']}（{progress['status']}）")
    elif report.get("plan_progress_reminder"):
        lines.append(f"方案进度：提醒。{report['plan_progress_reminder']}")
    if report["title"] and report["goal"]:
        lines += [
            f"下一会话标题：{report['title']}",
            f"下一会话目标：{report['goal']}",
            f"下一个会话描述：{report['session_description']}",
            "建议先读：",
        ]
        for item in report["docs"]:
            lines.append(f"- {item}")
        lines += [
            "下一个会话可复制提示词：",
            "```text",
            report["startup_prompt"],
            "```",
        ]
    else:
        lines.append("下一个会话描述：当前不生成，继续当前会话即可。")
        lines.append("下一个会话可复制提示词：当前不生成，继续当前会话即可。")
    lines += [
        "最终回复要求：发送 final 时，必须显式包含上面的 `文档维护 / Git 提交建议 / 会话建议` 三段内容。",
        "最终回复要求：`check-last-finalize` 返回 `[ok]` 只表示允许进入 final，不表示可以省略这三段。",
        "最终回复要求：如果最终回复只总结实现结果、只说“已完成收尾”或只说“脚本已执行”，一律视为未完成收尾。",
    ]
    return "\n".join(lines)


def format_commit_suggestion_report(commit):
    if commit["suitable"]:
        lines = [
            "Git 提交建议：适合提交",
            f"标题：{commit['title']}",
            "内容：",
            "```text",
        ]
        for item in commit["content"]:
            lines.append(f"- {item}")
        lines += [
            "```",
            SUITABLE_COMMIT_CONFIRMATION,
        ]
        return "\n".join(lines)
    return "\n".join(
        [
            f"Git 提交建议：暂不建议提交。原因是：{commit['reason']}",
            UNSUITABLE_COMMIT_CONFIRMATION,
        ]
    )


def backfill_agent_status(doc_root: Path, from_date: str):
    if not re.fullmatch(r"\d{8}", from_date):
        raise ValueError("--from 必须是 YYYYMMDD 格式，例如 20260330")

    default_agent_statuses = load_default_agent_statuses(doc_root)
    updated = []
    for path in scan_iteration_docs(doc_root):
        match = re.match(r"^迭代记录_(\d{8})_(\d{3})\.md$", path.name)
        if not match or match.group(1) < from_date:
            continue

        original = path.read_text(encoding="utf-8")
        text = ensure_default_agent_statuses(original, default_agent_statuses)
        for heading, placeholders in DEFAULT_PLACEHOLDERS.items():
            text = clean_placeholder_bullets(text, heading, placeholders)

        if text != original:
            path.write_text(text, encoding="utf-8")
            updated.append(path)

    sync_indexes(doc_root)
    return updated


PORTABLE_PATH_SKIP_DIRS = {
    ".git",
    "Library",
    "Temp",
    "Logs",
    "obj",
    "bin",
    "Build",
    "Builds",
    "HybridCLRData",
    "HybridCLRGenerate",
    "UserSettings",
}
PORTABLE_PATH_SCAN_ROOTS = (
    "doc",
    "tools",
    "Assets/Tools",
    ".vscode",
)
PORTABLE_PATH_TEXT_SUFFIXES = {
    ".md",
    ".txt",
    ".py",
    ".sh",
    ".command",
    ".bat",
    ".html",
    ".json",
    ".yaml",
    ".yml",
    ".toml",
    ".xml",
    ".asmdef",
    ".csproj",
    ".sln",
}
PORTABLE_PATH_TEXT_NAMES = {
    ".gitignore",
    ".gitattributes",
    "README",
}


def project_private_path_patterns():
    workspace_root = "/" + "Users/bruce/work/" + "GatebreakerArena"
    return (
        workspace_root + "/Client/Client",
        workspace_root + "/Client",
        workspace_root + "/doc",
        workspace_root + "/Assets",
        workspace_root + "/tools",
        workspace_root,
    )


def portable_path_warning_patterns():
    return (
        "/" + "Users/",
        "/" + "home/",
        "C:" + "\\",
    )


def is_portable_path_text_file(path: Path) -> bool:
    return path.suffix in PORTABLE_PATH_TEXT_SUFFIXES or path.name in PORTABLE_PATH_TEXT_NAMES


def iter_portable_path_scan_files(repo_root: Path):
    for relative_root in PORTABLE_PATH_SCAN_ROOTS:
        scan_root = repo_root / relative_root
        if not scan_root.exists():
            continue
        if scan_root.is_file():
            if is_portable_path_text_file(scan_root):
                yield scan_root
            continue
        for path in scan_root.rglob("*"):
            if path.is_dir():
                continue
            relative_parts = path.relative_to(repo_root).parts
            if any(part in PORTABLE_PATH_SKIP_DIRS for part in relative_parts):
                continue
            if is_portable_path_text_file(path):
                yield path


def check_portable_paths(doc_root: Path):
    repo_root = repo_root_for_doc_root(doc_root)
    hard_patterns = project_private_path_patterns()
    warning_patterns = portable_path_warning_patterns()
    hard_failures = []
    warnings = []
    for path in iter_portable_path_scan_files(repo_root):
        try:
            lines = path.read_text(encoding="utf-8").splitlines()
        except UnicodeDecodeError:
            continue
        relative = path.relative_to(repo_root).as_posix()
        for line_number, line in enumerate(lines, start=1):
            if any(pattern in line for pattern in hard_patterns):
                hard_failures.append((relative, line_number, line.strip()))
                continue
            if any(pattern in line for pattern in warning_patterns):
                warnings.append((relative, line_number, line.strip()))
    return {
        "hard_failures": hard_failures,
        "warnings": warnings,
    }


def format_portable_path_report(report) -> str:
    lines = []
    hard_failures = report["hard_failures"]
    warnings = report["warnings"]
    if hard_failures:
        lines.append("[error] found project-private absolute paths:")
        for relative, line_number, text in hard_failures:
            lines.append(f"- {relative}:{line_number}: {text}")
    else:
        lines.append("[ok] no project-private absolute paths found.")

    if warnings:
        lines.append("[warn] found generic local absolute path patterns:")
        for relative, line_number, text in warnings:
            lines.append(f"- {relative}:{line_number}: {text}")
    return "\n".join(lines)


def main():
    parser = argparse.ArgumentParser(description="Maintain long-term docs and iteration logs for this project.")
    parser.add_argument("--doc-root", default="doc", help="Project doc root directory")
    sub = parser.add_subparsers(dest="command", required=True)

    sub.add_parser("sync", help="Regenerate long-term and iteration indexes")

    update_plan = sub.add_parser("update-plan-progress", help="Update a plan document progress block")
    update_plan.add_argument("--plan-doc", required=True, help="Plan doc under 长期主文档/方案与数据")
    update_plan.add_argument("--plan-status", required=True, choices=sorted(PLAN_STATUS_VALUES), help="Plan landing status")
    update_plan.add_argument("--plan-note", required=True, help="Long-lived progress summary")
    update_plan.add_argument("--plan-update", default="", help="One-line latest update; defaults to --plan-note")
    update_plan.add_argument("--date", default="", help="Update date in YYYY-MM-DD; defaults to today")

    new_iteration = sub.add_parser("new-iteration", help="Create a new iteration log")
    new_iteration.add_argument("--title", required=True, help="Iteration title")
    new_iteration.add_argument("--goal", required=True, help="Iteration goal")
    new_iteration.add_argument("--status", default="进行中", help="Initial status")

    append = sub.add_parser("append-iteration", help="Append work notes to an iteration log")
    append_target = append.add_mutually_exclusive_group(required=True)
    append_target.add_argument("--file", help="Target iteration file, relative to doc root or absolute path")
    append_target.add_argument("--latest", action="store_true", help="Append to the latest iteration log")
    append.add_argument("--summary", default="", help="Short work summary")
    append.add_argument("--done", action="append", default=[], help="Completed item, repeatable")
    append.add_argument("--risk", action="append", default=[], help="Risk or blocker, repeatable")
    append.add_argument("--next", dest="next_steps", action="append", default=[], help="Next-step item, repeatable")
    append.add_argument("--agent-status", action="append", default=[], help="Full Agent status line, repeatable")

    handoff = sub.add_parser("suggest-handoff", help="Suggest whether to start a new session and/or iteration")
    handoff.add_argument("--summary", default="", help="Short work summary")
    handoff.add_argument("--done", action="append", default=[], help="Completed item, repeatable")
    handoff.add_argument("--risk", action="append", default=[], help="Risk or blocker, repeatable")
    handoff.add_argument("--next", dest="next_steps", action="append", default=[], help="Next-step item, repeatable")
    handoff.add_argument(
        "--agent6-review",
        default="pending",
        choices=["passed", "passed-with-suggestions", "failed", "pending", "deferred", "not-required"],
        help="Current Agent 6 review status",
    )
    handoff.add_argument(
        "--session-mode",
        default="auto",
        choices=["auto", "new", "continue"],
        help="Override the session suggestion",
    )
    handoff.add_argument(
        "--iteration-mode",
        default="auto",
        choices=["auto", "new", "continue"],
        help="Override the iteration suggestion",
    )
    handoff.add_argument("--next-session-title", default="", help="Explicit next-session title")
    handoff.add_argument("--next-session-goal", default="", help="Explicit next-session goal")
    handoff.add_argument("--next-session-doc", action="append", default=[], help="Recommended doc path, repeatable")
    handoff.add_argument("--doc-log-skipped", action="store_true", help="Mark that this round did not write project docs")
    handoff.add_argument(
        "--context-compressed",
        action="store_true",
        help="Mark that this session has already shown automatic context compression or obvious context degradation",
    )
    handoff.add_argument(
        "--session-major-task-count",
        type=int,
        default=0,
        help="Explicit count of major tasks already completed in the current session",
    )
    handoff.add_argument("--plan-doc", default="", help="Plan doc whose progress must be valid")

    sub.add_parser("suggest-current-commit", help="Inspect current repo state and print a direct Git commit suggestion")
    sub.add_parser("show-pending-commit", help="Show the latest cached suitable commit suggestion")
    sync_commit_sequences = sub.add_parser("sync-commit-sequences", help="Sync the tracked per-module commit sequence registry")
    sync_commit_sequences.add_argument("--fetch", action="store_true", help="Fetch remote refs before recalculating sequence registry")
    sync_commit_sequences.add_argument("--stage", action="store_true", help="Stage the registry file after syncing")

    validate_commit_message_cmd = sub.add_parser("validate-commit-message", help="Validate a commit message against the latest module sequence")
    validate_commit_message_cmd.add_argument("--message-file", required=True, help="Path to the temporary commit message file")
    validate_commit_message_cmd.add_argument("--no-fetch", action="store_true", help="Skip fetching remote refs before validation")
    validate_commit_message_cmd.add_argument("--stage-registry", action="store_true", help="Stage the registry file after successful validation")

    sub.add_parser("show-last-finalize", help="Show the latest cached complete finalize report")
    sub.add_parser("check-last-finalize", help="Validate whether the latest complete finalize report still matches the current repo state")
    sub.add_parser("check-portable-paths", help="Validate docs and tooling do not contain project-private absolute paths")

    record_finalize = sub.add_parser("record-last-finalize", help="Record the latest complete finalize report")
    record_finalize.add_argument("--summary", required=True, help="Round summary used for finalize")
    record_finalize.add_argument("--agent6-review", required=True, help="Agent 6 review state at finalize time")
    record_finalize.add_argument("--doc-log-skipped", action="store_true", help="Mark that this finalize skipped project doc logging")
    record_finalize.add_argument("--plan-doc", default="", help="Plan doc whose progress was updated or verified")

    backfill = sub.add_parser("backfill-agent-status", help="Backfill default agent status into iteration logs")
    backfill.add_argument("--from", dest="from_date", required=True, help="Start date in YYYYMMDD")

    args = parser.parse_args()
    doc_root = Path(args.doc_root).resolve()
    ensure_dirs(doc_root)

    if args.command == "sync":
        previous_status = current_worktree_status(doc_root)
        sync_indexes(doc_root)
        for line in format_plan_progress_warnings(validate_plan_progress(doc_root)):
            print(line, file=sys.stderr)
        if current_worktree_status(doc_root) != previous_status:
            clear_last_finalize_report(doc_root)
        print(f"[ok] synced indexes under {doc_root}")
        return

    if args.command == "update-plan-progress":
        previous_status = current_worktree_status(doc_root)
        result = write_plan_progress(
            doc_root,
            args.plan_doc,
            args.plan_status,
            args.plan_note,
            args.plan_update,
            args.date,
        )
        sync_indexes(doc_root)
        if current_worktree_status(doc_root) != previous_status:
            clear_last_finalize_report(doc_root)
        print(f"[ok] updated plan progress: {result['path']}")
        print(f"status: {result['status']}")
        print(f"recent_update: {result['recent_update']}")
        return

    if args.command == "new-iteration":
        path = create_iteration(doc_root, args.title, args.goal, args.status)
        clear_last_finalize_report(doc_root)
        print(f"[ok] created iteration log: {path}")
        return

    if args.command == "append-iteration":
        path = append_iteration(
            doc_root,
            args.file or "",
            args.latest,
            args.summary,
            args.done,
            args.risk,
            args.next_steps,
            args.agent_status,
        )
        clear_last_finalize_report(doc_root)
        print(f"[ok] updated iteration log: {path}")
        print("[warn] 当前只完成了迭代记录更新，这仍属于半收尾。")
        print("[warn] 如需完成收尾，请继续执行 update_project_docs.py suggest-handoff 或 tools/doc_maintenance/finalize_task.sh。")
        return

    if args.command == "suggest-handoff":
        report = suggest_handoff(
            doc_root,
            args.summary,
            args.done,
            args.risk,
            args.next_steps,
            args.agent6_review,
            args.session_mode,
            args.iteration_mode,
            args.next_session_title,
            args.next_session_goal,
            args.next_session_doc,
            args.doc_log_skipped,
            args.context_compressed,
            args.session_major_task_count,
            args.plan_doc,
        )
        print(format_handoff_report(report))
        return

    if args.command == "suggest-current-commit":
        print(format_commit_suggestion_report(suggest_current_commit(doc_root)))
        return

    if args.command == "show-pending-commit":
        payload = read_pending_commit_suggestion(doc_root)
        if not payload:
            raise SystemExit("未找到最近一次可直接复用的提交建议缓存。")
        print(json.dumps(payload, ensure_ascii=False, indent=2))
        return

    if args.command == "sync-commit-sequences":
        result = sync_commit_sequence_registry(doc_root, fetch_latest=args.fetch, stage=args.stage)
        print(f"[ok] synced commit sequence registry: {result['path']}")
        print(f"changed: {'yes' if result['changed'] else 'no'}")
        print(f"modules: {len(result['modules'])}")
        print(f"fetch: {result['fetch']['message']}")
        return

    if args.command == "validate-commit-message":
        result = validate_commit_message(
            doc_root,
            Path(args.message_file),
            fetch_latest=not args.no_fetch,
            stage_registry=args.stage_registry,
        )
        if not result["valid"]:
            raise SystemExit(
                "提交标题编号校验失败：\n"
                f"- 当前标题：{result.get('subject', '')}\n"
                f"- 原因：{result['reason']}\n"
                f"- 建议标题：{result.get('expected_title', '')}\n"
                f"- fetch：{result['fetch']['message']}"
            )
        if result["skipped"]:
            print(f"[skip] {result['reason']}")
            return
        print(f"[ok] commit title validated: {result['subject']}")
        print(f"module: {result['module_code']}")
        print(f"sequence: {result['sequence']:05d}")
        print(f"registry_changed: {'yes' if result['registry_changed'] else 'no'}")
        print(f"fetch: {result['fetch']['message']}")
        return

    if args.command == "show-last-finalize":
        payload = read_last_finalize_report(doc_root)
        if not payload:
            raise SystemExit("未找到最近一次完整收尾状态缓存。")
        print(json.dumps(payload, ensure_ascii=False, indent=2))
        return

    if args.command == "check-last-finalize":
        result = validate_last_finalize_report(doc_root)
        if not result["valid"]:
            message = "\n".join(f"- {reason}" for reason in result["reasons"])
            raise SystemExit(f"最近一次完整收尾状态校验失败：\n{message}")
        payload = result["payload"] or {}
        print("[ok] 最近一次完整收尾状态仍然有效。")
        print(f"summary: {payload.get('summary', '')}")
        print(f"created_at: {payload.get('created_at', '')}")
        print(f"head_commit: {payload.get('head_commit', '')}")
        return

    if args.command == "check-portable-paths":
        report = check_portable_paths(doc_root)
        print(format_portable_path_report(report))
        if report["hard_failures"]:
            raise SystemExit(1)
        return

    if args.command == "record-last-finalize":
        plan_doc = ""
        plan_doc_hash = ""
        if args.plan_doc:
            plan_path = normalize_plan_doc_path(doc_root, args.plan_doc)
            validate_plan_progress(doc_root, str(plan_path), strict=True)
            plan_doc = plan_path.resolve().as_posix()
            plan_doc_hash = file_content_hash(plan_path)
        report_text = sys.stdin.read().strip()
        payload = {
            "summary": args.summary,
            "agent6_review": args.agent6_review,
            "doc_log_skipped": args.doc_log_skipped,
            "plan_doc": plan_doc,
            "plan_doc_hash": plan_doc_hash,
            "head_commit": current_head_commit(doc_root),
            "worktree_status": current_worktree_status(doc_root),
            "report_text": report_text,
            "created_at": datetime.now().isoformat(timespec="seconds"),
        }
        path = write_last_finalize_report(doc_root, payload)
        if path is None:
            raise SystemExit("当前不在 Git 仓库中，无法写入最近一次完整收尾状态。")
        print(f"[ok] recorded finalize report: {path}")
        return

    if args.command == "backfill-agent-status":
        updated = backfill_agent_status(doc_root, args.from_date)
        print(f"[ok] backfilled {len(updated)} iteration logs under {doc_root}")
        return


if __name__ == "__main__":
    main()
