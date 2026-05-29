#!/usr/bin/env bash

set -euo pipefail

# 把仓库内的 .githooks 设为当前仓库的 hooksPath。
# 这样 hook 脚本可以随仓库一起维护，而不是散落在每个人本地的 .git/hooks 里。

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/../.." && pwd)"
source "${REPO_ROOT}/.githooks/_common"

if ! resolve_python_command; then
    echo "[error] 未找到 Python 3.8+。Git hooks 需要 Python 来检查提交规范和 Unity GUID。" >&2
    echo "[hint] Windows 可安装 Python 后确保 python 或 py 在 PATH 中；macOS/Linux 可安装 python3。" >&2
    exit 1
fi

mkdir -p "${REPO_ROOT}/.githooks"
chmod +x "${REPO_ROOT}/.githooks/_common" 2>/dev/null || true
chmod +x "${REPO_ROOT}/.githooks/pre-commit" 2>/dev/null || true
chmod +x "${REPO_ROOT}/.githooks/prepare-commit-msg" 2>/dev/null || true
chmod +x "${REPO_ROOT}/.githooks/commit-msg" 2>/dev/null || true
chmod +x "${REPO_ROOT}/.githooks/post-commit" 2>/dev/null || true
chmod +x \
    "${REPO_ROOT}/tools/doc_maintenance/finalize_task.sh" \
    "${REPO_ROOT}/tools/doc_maintenance/check_doc_maintenance.py" \
    "${REPO_ROOT}/tools/repo_maintenance/check_unity_guid_integrity.py" \
    "${REPO_ROOT}/tools/repo_maintenance/check_git_hooks_installed.py" \
    "${REPO_ROOT}/tools/repo_maintenance/install_git_hooks.sh"

git -C "${REPO_ROOT}" config core.hooksPath .githooks

echo "[ok] 已将当前仓库的 core.hooksPath 设置为 .githooks"
echo "[ok] 以后提交前会自动检查当前提交内容的 Unity GUID 完整性，并继续执行文档维护检查。"
echo "[ok] Python: ${PYTHON_CMD[*]}"
