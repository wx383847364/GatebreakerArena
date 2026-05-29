#!/usr/bin/env python3

import argparse
import os
import shutil
import subprocess
import sys
from pathlib import Path


REQUIRED_HOOKS = (
    "pre-commit",
    "prepare-commit-msg",
    "commit-msg",
    "post-commit",
)
REQUIRED_SUPPORT_FILES = (
    "_common",
)


def run_git(repo_root: Path, *args: str) -> subprocess.CompletedProcess[str]:
    return subprocess.run(
        ["git", "-C", str(repo_root), *args],
        capture_output=True,
        text=True,
        check=False,
    )


def should_check_executable_bit() -> bool:
    return os.name != "nt"


def python_command_available() -> bool:
    candidates = (
        ("python3",),
        ("python",),
        ("py", "-3"),
    )
    for candidate in candidates:
        executable = candidate[0]
        if shutil.which(executable) is None:
            continue
        result = subprocess.run(
            [*candidate, "-c", "import sys; raise SystemExit(sys.version_info < (3, 8))"],
            capture_output=True,
            text=True,
            check=False,
        )
        if result.returncode == 0:
            return True
    return False


def main() -> int:
    parser = argparse.ArgumentParser(description="Check Gatebreaker Arena Git hooks installation")
    parser.add_argument("--warn-only", action="store_true", help="Print warning but exit 0 when hooks are missing")
    parser.add_argument("--quiet", action="store_true", help="Do not print anything when hooks are correctly installed")
    args = parser.parse_args()

    repo_root = Path(__file__).resolve().parents[2]
    configured = run_git(repo_root, "config", "--get", "core.hooksPath")
    hooks_path = configured.stdout.strip()
    problems: list[str] = []
    if configured.returncode != 0 or hooks_path != ".githooks":
        problems.append("core.hooksPath 未设置为 .githooks")

    hooks_dir = repo_root / ".githooks"
    for support_file in REQUIRED_SUPPORT_FILES:
        support_path = hooks_dir / support_file
        if not support_path.exists():
            problems.append(f"缺少 {support_path.relative_to(repo_root)}")

    for hook_name in REQUIRED_HOOKS:
        hook_path = hooks_dir / hook_name
        if not hook_path.exists():
            problems.append(f"缺少 {hook_path.relative_to(repo_root)}")
        elif should_check_executable_bit() and not os.access(hook_path, os.X_OK):
            problems.append(f"{hook_path.relative_to(repo_root)} 不可执行")

    if not python_command_available():
        problems.append("未找到 Python 3.8+ 命令（需要 python3、python 或 py -3）")

    if not problems:
        if not args.quiet:
            print("[hook-check] Git hooks 已安装。")
        return 0

    sys.stderr.write("[hook-check] Git hooks 未安装或配置不完整：\n")
    for problem in problems:
        sys.stderr.write(f"- {problem}\n")
    if os.name == "nt":
        sys.stderr.write("[hook-check] 请执行：tools\\repo_maintenance\\install_git_hooks.cmd\n")
    else:
        sys.stderr.write("[hook-check] 请执行：bash tools/repo_maintenance/install_git_hooks.sh\n")
    return 0 if args.warn_only else 1


if __name__ == "__main__":
    raise SystemExit(main())
