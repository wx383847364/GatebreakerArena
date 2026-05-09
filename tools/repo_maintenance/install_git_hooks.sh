#!/usr/bin/env bash

set -euo pipefail

# 把仓库内的 .githooks 设为当前仓库的 hooksPath。
# 这样 hook 脚本可以随仓库一起维护，而不是散落在每个人本地的 .git/hooks 里。

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/../.." && pwd)"

mkdir -p "${REPO_ROOT}/.githooks"
chmod +x "${REPO_ROOT}/.githooks/pre-commit" 2>/dev/null || true
chmod +x "${REPO_ROOT}/.githooks/commit-msg" 2>/dev/null || true
chmod +x "${REPO_ROOT}/.githooks/post-commit" 2>/dev/null || true
chmod +x \
    "${REPO_ROOT}/tools/doc_maintenance/finalize_task.sh" \
    "${REPO_ROOT}/tools/doc_maintenance/check_doc_maintenance.py" \
    "${REPO_ROOT}/tools/repo_maintenance/check_unity_guid_integrity.py" \
    "${REPO_ROOT}/tools/repo_maintenance/install_git_hooks.sh"

git -C "${REPO_ROOT}" config core.hooksPath .githooks

echo "[ok] 已将当前仓库的 core.hooksPath 设置为 .githooks"
echo "[ok] 以后提交前会自动检查当前提交内容的 Unity GUID 完整性，并继续执行文档维护检查。"
