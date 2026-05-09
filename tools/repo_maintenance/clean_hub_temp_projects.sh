#!/usr/bin/env bash

set -euo pipefail

DRY_RUN=0
FORCE_WHEN_HUB_RUNNING=0
SKIP_HUB_RECORD_CLEANUP=0

usage() {
    cat <<'EOF'
用法：
  tools/repo_maintenance/clean_hub_temp_projects.sh [--dry-run] [--force-when-hub-running]

说明：
  - 只清理 Gatebreaker Arena 验证过程在 /private/tmp 或 /tmp 下留下的临时工程目录
  - 不会处理 /Users/.../work 下的当前项目或任何非 tmp 正式工程
  - 尝试从 Unity Hub / 团结 Hub 的可编辑 JSON 配置中移除对应的临时项目记录
  - 如果 Hub 正在运行，仍会继续删除 /tmp 下的临时工程目录，但会跳过 Hub 记录清理
  - 建议先用 --dry-run 预览，再正式执行
EOF
}

log() {
    printf '[info] %s\n' "$1"
}

warn() {
    printf '[warn] %s\n' "$1" >&2
}

is_hub_running() {
    pgrep -f 'Unity Hub|UnityHub|Tuanjie Hub|TuanjieHub' >/dev/null 2>&1
}

run_or_echo() {
    if [[ "${DRY_RUN}" -eq 1 ]]; then
        printf '[dry-run] %s\n' "$*"
    else
        "$@"
    fi
}

while [[ $# -gt 0 ]]; do
    case "$1" in
        --dry-run)
            DRY_RUN=1
            shift
            ;;
        --force-when-hub-running)
            FORCE_WHEN_HUB_RUNNING=1
            shift
            ;;
        -h|--help)
            usage
            exit 0
            ;;
        *)
            warn "未知参数：$1"
            usage
            exit 1
            ;;
    esac
done

if is_hub_running && [[ "${FORCE_WHEN_HUB_RUNNING}" -ne 1 ]]; then
    warn "检测到 Unity Hub 或 团结 Hub 仍在运行。将继续删除 /tmp 下的临时工程目录，但跳过 Hub 记录清理。"
    warn "如果你明确要在 Hub 运行时也强制改 Hub 记录，可追加 --force-when-hub-running。"
    SKIP_HUB_RECORD_CLEANUP=1
fi

TMP_ROOT="/private/tmp"
PATTERNS=(
    "gatebreaker_validation_*"
    "GatebreakerArenaValidation"
    "GatebreakerArena_group2_validation"
    "gatebreaker_group1_verify_*"
    "gatebreaker_group2_verify_*"
    "gatebreaker_agent5_verify_*"
    "gatebreaker_compile_check_*"
    "gatebreaker_tests_*"
    "gatebreaker-compile-*"
    "GatebreakerArenaCompile"
)

is_allowed_temp_project() {
    local path="$1"
    local resolved
    resolved="$(python3 - <<'PY' "$path"
import os, sys
print(os.path.realpath(sys.argv[1]))
PY
)"

    case "${resolved}" in
        /private/tmp/*|/tmp/*)
            ;;
        *)
            return 1
            ;;
    esac

    local base
    base="$(basename "${resolved}")"
    case "${base}" in
        gatebreaker_validation_*|GatebreakerArenaValidation|GatebreakerArena_group2_validation|gatebreaker_group1_verify_*|gatebreaker_group2_verify_*|gatebreaker_agent5_verify_*|gatebreaker_compile_check_*|gatebreaker_tests_*|gatebreaker-compile-*|GatebreakerArenaCompile)
            return 0
            ;;
        *)
            return 1
            ;;
    esac
}

log "扫描临时工程目录..."
paths_to_remove=()
for pattern in "${PATTERNS[@]}"; do
    while IFS= read -r path; do
        [[ -z "${path}" ]] && continue
        paths_to_remove+=("${path}")
    done < <(find "${TMP_ROOT}" -maxdepth 1 -type d -name "${pattern}" 2>/dev/null | sort)
done

if [[ ${#paths_to_remove[@]} -eq 0 ]]; then
    log "没有发现待删除的 Gatebreaker Arena 临时工程目录。"
else
    printf '%s\n' "${paths_to_remove[@]}" | awk '!seen[$0]++' | while IFS= read -r path; do
        if is_allowed_temp_project "${path}"; then
            run_or_echo rm -rf "${path}"
        else
            warn "跳过非临时或不受信任路径：${path}"
        fi
    done
fi

if [[ "${SKIP_HUB_RECORD_CLEANUP}" -eq 1 ]]; then
    warn "已跳过 Hub 记录清理；如需清理最近项目记录，请先关闭 Hub 后再执行一次脚本。"
    exit 0
fi

export HUB_CLEANUP_DRY_RUN="${DRY_RUN}"
python3 - <<'PY'
from __future__ import annotations
import json
import os
import shutil
from datetime import datetime
from pathlib import Path

dry_run = os.environ.get("HUB_CLEANUP_DRY_RUN") == "1"
home = Path.home()
targets = [
    home / "Library/Application Support/UnityHub/projects-v1.json",
    home / "Library/Application Support/TuanjieHub/favoriteProjects.json",
    home / "Library/Application Support/TuanjieHub/projectsArchitecture.json",
]

prefixes = ("/tmp/", "/private/tmp/")
name_needles = (
    "gatebreaker_validation",
    "gatebreaker_group",
    "gatebreaker_agent5",
    "gatebreaker_compile",
    "gatebreaker_tests",
    "gatebreakervalidation",
    "gatebreaker_group2_validation",
    "gatebreakercompile",
)

def is_temp_project_path(value: str) -> bool:
    if not isinstance(value, str):
        return False
    lowered = value.lower()
    if not lowered.startswith(prefixes):
        return False
    return any(needle in lowered for needle in name_needles)

def should_remove_object(obj) -> bool:
    if isinstance(obj, dict):
        for key, value in obj.items():
            if isinstance(key, str) and is_temp_project_path(key):
                return True
            if isinstance(value, str) and is_temp_project_path(value):
                return True
    return False

def prune(data):
    if isinstance(data, dict):
        result = {}
        changed = False
        for key, value in data.items():
            if isinstance(key, str) and is_temp_project_path(key):
                changed = True
                continue
            if should_remove_object(value):
                changed = True
                continue
            new_value, value_changed = prune(value)
            changed = changed or value_changed
            result[key] = new_value
        return result, changed

    if isinstance(data, list):
        result = []
        changed = False
        for item in data:
            if isinstance(item, str) and is_temp_project_path(item):
                changed = True
                continue
            if should_remove_object(item):
                changed = True
                continue
            new_item, item_changed = prune(item)
            changed = changed or item_changed
            result.append(new_item)
        return result, changed

    if isinstance(data, str):
        stripped = data.strip()
        if stripped.startswith("[") or stripped.startswith("{"):
            try:
                nested = json.loads(data)
            except Exception:
                return data, False
            new_nested, changed = prune(nested)
            if not changed:
                return data, False
            return json.dumps(new_nested, ensure_ascii=False), True
        return data, False

    return data, False

for path in targets:
    if not path.exists():
        continue

    try:
        original = path.read_text(encoding="utf-8")
        data = json.loads(original)
    except Exception:
        continue

    new_data, changed = prune(data)
    if not changed:
        continue

    print(f"[info] 清理 Hub 记录文件：{path}")
    if dry_run:
        continue

    backup = path.with_suffix(path.suffix + f".bak.{datetime.now().strftime('%Y%m%d_%H%M%S')}")
    shutil.copy2(path, backup)
    path.write_text(json.dumps(new_data, ensure_ascii=False, separators=(",", ":")), encoding="utf-8")
    print(f"[info] 已备份到：{backup}")
PY

log "清理完成。"
warn "如果团结 Hub 里仍有旧的临时项目行，先重启 Hub；若仍保留，再在 Hub 里点对应项目右侧 ... 手动 Remove。"
