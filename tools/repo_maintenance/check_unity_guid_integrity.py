#!/usr/bin/env python3

from __future__ import annotations

import re
import subprocess
import sys
from pathlib import Path


UNITY_HEX_GUID_RE = re.compile(r"^[0-9a-f]{32}$")
# Tuanjie 2022.3.62t2 can serialize .meta GUIDs as 56-character base64-like
# tokens instead of Unity's common 32-character lowercase hex form.
TUANJIE_BASE64_GUID_RE = re.compile(r"^[A-Za-z0-9+/]{55}=$")
SERIALIZED_GUID_RE = re.compile(r"guid:\s*([^,}\s]+)")
BUILTIN_GUIDS = {
    "00000000000000000000000000000000",
    "0000000000000000e000000000000000",
    "0000000000000000f000000000000000",
}


def is_valid_unity_or_tuanjie_guid(guid: str) -> bool:
    return bool(
        UNITY_HEX_GUID_RE.fullmatch(guid) or
        TUANJIE_BASE64_GUID_RE.fullmatch(guid)
    )


UNITY_TEXT_EXTENSIONS = {
    ".anim",
    ".asmdef",
    ".asset",
    ".controller",
    ".mat",
    ".overrideController",
    ".playable",
    ".prefab",
    ".scene",
}
SKIP_DIRS = {
    ".git",
    "Library",
    "Logs",
    "Obj",
    "Temp",
    "UserSettings",
}


def should_skip(path: Path) -> bool:
    return any(part in SKIP_DIRS for part in path.parts)


def iter_staged_paths(repo_root: Path) -> list[Path]:
    result = subprocess.run(
        [
            "git",
            "-C",
            str(repo_root),
            "diff",
            "--cached",
            "--name-only",
            "-z",
            "--diff-filter=ACMR",
        ],
        check=True,
        stdout=subprocess.PIPE,
    )
    return [
        Path(item.decode("utf-8"))
        for item in result.stdout.split(b"\0")
        if item
    ]


def read_staged_text(repo_root: Path, path: Path) -> str:
    result = subprocess.run(
        ["git", "-C", str(repo_root), "show", f":{path.as_posix()}"],
        check=True,
        stdout=subprocess.PIPE,
    )
    return result.stdout.decode("utf-8", errors="replace")


def iter_meta_files(repo_root: Path, paths: list[Path] | None = None):
    if paths is not None:
        for path in paths:
            if path.name.endswith(".meta"):
                yield path
        return

    for root_name in ("Assets", "Packages"):
        root = repo_root / root_name
        if not root.exists():
            continue
        yield from root.rglob("*.meta")


def iter_unity_text_files(repo_root: Path, paths: list[Path] | None = None):
    if paths is not None:
        for path in paths:
            if path.suffix in UNITY_TEXT_EXTENSIONS:
                yield path
        return

    for root_name in ("Assets", "Packages", "ProjectSettings"):
        root = repo_root / root_name
        if not root.exists():
            continue
        for path in root.rglob("*"):
            if path.is_file() and path.suffix in UNITY_TEXT_EXTENSIONS:
                yield path


def validate_meta_files(repo_root: Path, paths: list[Path] | None = None) -> list[str]:
    errors: list[str] = []
    for path in iter_meta_files(repo_root, paths):
        if should_skip(path):
            continue
        try:
            if paths is None:
                text = path.read_text(encoding="utf-8", errors="replace")
            else:
                text = read_staged_text(repo_root, path)
            lines = text.splitlines()
        except (OSError, subprocess.CalledProcessError) as exc:
            errors.append(f"{path}: failed to read meta file: {exc}")
            continue

        guid_line = next((line for line in lines[:8] if line.startswith("guid: ")), None)
        if guid_line is None:
            errors.append(f"{path}: missing `guid:` line")
            continue

        guid = guid_line[6:].strip()
        if not is_valid_unity_or_tuanjie_guid(guid):
            errors.append(f"{path}: invalid meta GUID `{guid}`")
    return errors


def validate_serialized_guid_tokens(repo_root: Path, paths: list[Path] | None = None) -> list[str]:
    errors: list[str] = []
    for path in iter_unity_text_files(repo_root, paths):
        if should_skip(path):
            continue
        try:
            if paths is None:
                text = path.read_text(encoding="utf-8", errors="replace")
            else:
                text = read_staged_text(repo_root, path)
            lines = text.splitlines()
        except (OSError, subprocess.CalledProcessError) as exc:
            errors.append(f"{path}: failed to read Unity text asset: {exc}")
            continue

        for line_no, line in enumerate(lines, start=1):
            for match in SERIALIZED_GUID_RE.finditer(line):
                guid = match.group(1)
                if guid in BUILTIN_GUIDS:
                    continue
                if not UNITY_HEX_GUID_RE.fullmatch(guid):
                    errors.append(
                        f"{path}:{line_no}: invalid serialized GUID token `{guid}`"
                    )
    return errors


def main() -> int:
    repo_root = Path(__file__).resolve().parents[2]
    staged_only = "--staged" in sys.argv[1:]
    paths = iter_staged_paths(repo_root) if staged_only else None

    errors = []
    errors.extend(validate_meta_files(repo_root, paths))
    errors.extend(validate_serialized_guid_tokens(repo_root, paths))

    if not errors:
        scope = "current commit Unity files" if staged_only else "Unity files"
        print(f"[ok] Unity GUID integrity check passed for {scope}.")
        return 0

    print("[error] Unity GUID integrity check failed:", file=sys.stderr)
    for item in errors:
        print(f"  - {item}", file=sys.stderr)
    print(
        "[hint] `.meta` GUIDs may be 32 lowercase hex characters or Tuanjie 56-character base64-like tokens; serialized asset references must stay 32 lowercase hex characters.",
        file=sys.stderr,
    )
    return 1


if __name__ == "__main__":
    raise SystemExit(main())
