#!/usr/bin/env bash

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/../.." && pwd)"
SOURCE_ROOT="${REPO_ROOT}/doc/长期主文档/协作与执行/skills"
DEST_ROOT="${CODEX_HOME:-${HOME}/.codex}/skills"

usage() {
    cat <<'EOF'
用法：
  tools/repo_maintenance/sync_codex_skills.sh
  tools/repo_maintenance/sync_codex_skills.sh unity-hotupdate-boundary

说明：
  - 默认会把仓库 doc/长期主文档/协作与执行/skills 下的所有 skill 同步到 ~/.codex/skills
  - 传 skill 名称时，只同步指定 skill
EOF
}

sync_one() {
    local skill_name="$1"
    local source_dir="${SOURCE_ROOT}/${skill_name}"
    local dest_dir="${DEST_ROOT}/${skill_name}"

    if [[ ! -f "${source_dir}/SKILL.md" ]]; then
        echo "[error] 未找到 skill 真源：${source_dir}" >&2
        return 1
    fi

    rm -rf "${dest_dir}"
    mkdir -p "${DEST_ROOT}"
    cp -R "${source_dir}" "${dest_dir}"

    if [[ -d "${dest_dir}/scripts" ]]; then
        find "${dest_dir}/scripts" -type f -exec chmod +x {} +
    fi

    echo "[ok] synced ${skill_name} -> ${dest_dir}"
}

if [[ "${1:-}" == "-h" || "${1:-}" == "--help" ]]; then
    usage
    exit 0
fi

if [[ ! -d "${SOURCE_ROOT}" ]]; then
    echo "[error] 未找到 skill 真源目录：${SOURCE_ROOT}" >&2
    exit 1
fi

if [[ "$#" -gt 0 ]]; then
    for skill_name in "$@"; do
        sync_one "${skill_name}"
    done
    exit 0
fi

shopt -s nullglob
SKILL_DIRS=("${SOURCE_ROOT}"/*)
if [[ "${#SKILL_DIRS[@]}" -eq 0 ]]; then
    echo "[warn] ${SOURCE_ROOT} 下没有可同步的 skill"
    exit 0
fi

for path in "${SKILL_DIRS[@]}"; do
    if [[ -d "${path}" && -f "${path}/SKILL.md" ]]; then
        sync_one "$(basename "${path}")"
    fi
done
