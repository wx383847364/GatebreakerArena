#!/bin/bash
# Finder 双击入口；执行结束后保留终端，便于查看结果。
SCRIPT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
/bin/bash "$SCRIPT_ROOT/tools/build/build_and_install_android.sh" "$@"
result=$?
echo
if [[ "$result" -eq 0 ]]; then
    echo "运行结束。按回车关闭。"
else
    echo "操作失败，请查看上方提示。按回车关闭。"
fi
if [[ -t 0 ]]; then read -r _; fi
exit "$result"
