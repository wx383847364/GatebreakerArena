#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
UNITY_BIN="${UNITY_BIN:-/Applications/Tuanjie/Hub/Editor/2022.3.62t9/Tuanjie.app/Contents/MacOS/Tuanjie}"
DEVICE_SERIAL="${ANDROID_SERIAL:-}"
LAUNCH=1

usage() {
    cat <<'EOF'
用法：build_and_install_android.sh [--serial 设备序列号] [--no-launch]

构建 Android 测试 APK，通过 ADB 覆盖安装，并默认启动游戏。
只连接一台已授权设备时自动选择；多台设备请用 --serial 指定。
运行前请保存并关闭此项目的团结编辑器，手机开启 USB 调试与 USB 安装。

选项：
  --serial SERIAL  指定设备（也可设置 ANDROID_SERIAL）
  --no-launch      安装后不启动游戏
  -h, --help       显示帮助

环境变量：UNITY_BIN 指定编辑器；ADB_BIN 指定 adb 可执行文件。
APK：Client/Builds/Android/GatebreakerArena-android-smoke.apk
EOF
}

fail() { echo "[错误] $*" >&2; exit 1; }

while [[ $# -gt 0 ]]; do
    case "$1" in
        --serial)
            [[ $# -ge 2 && -n "$2" && "$2" != --* ]] || fail "--serial 缺少设备序列号"
            DEVICE_SERIAL="$2"
            shift 2
            ;;
        --no-launch) LAUNCH=0; shift ;;
        -h|--help) usage; exit 0 ;;
        *) usage >&2; fail "未知参数：$1" ;;
    esac
done

[[ -x "$UNITY_BIN" ]] || fail "找不到编辑器：${UNITY_BIN}；请设置 UNITY_BIN。"
export UNITY_BIN

if [[ -z "${ADB_BIN:-}" ]]; then
    ADB_BIN="$(command -v adb || true)"
    if [[ -z "$ADB_BIN" ]]; then
        EDITOR_DIR="$(cd "$(dirname "$UNITY_BIN")/../../.." && pwd)"
        for candidate in \
            "${ANDROID_SDK_ROOT:-${ANDROID_HOME:-$HOME/Library/Android/sdk}}/platform-tools/adb" \
            "$EDITOR_DIR/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb"; do
            if [[ -x "$candidate" ]]; then ADB_BIN="$candidate"; break; fi
        done
    fi
fi
[[ -n "$ADB_BIN" && -x "$ADB_BIN" ]] || fail "找不到 adb；请设置 ADB_BIN 为 adb 的完整路径。"

# 先检查连接，避免编译数分钟后才发现设备不可用。
DEVICE_LIST="$("$ADB_BIN" devices -l)" || fail "ADB 无法列出设备。"
if [[ -z "$DEVICE_SERIAL" ]]; then
    DEVICE_COUNT="$(printf '%s\n' "$DEVICE_LIST" | awk 'NR > 1 && NF >= 2 {n++} END {print n+0}')"
    [[ "$DEVICE_COUNT" -gt 0 ]] || fail "未检测到手机；请连接 USB 并开启 USB 调试。"
    if [[ "$DEVICE_COUNT" -gt 1 ]]; then
        printf '%s\n' "$DEVICE_LIST" >&2
        if [[ -t 0 ]]; then
            read -r -p "检测到多台设备，请输入上方目标设备的序列号：" DEVICE_SERIAL
            [[ -n "$DEVICE_SERIAL" ]] || fail "未选择设备。"
        else
            fail "检测到多台设备，请用 --serial 指定目标。"
        fi
    else
        DEVICE_SERIAL="$(printf '%s\n' "$DEVICE_LIST" | awk 'NR > 1 && NF >= 2 {print $1; exit}')"
    fi
fi
DEVICE_STATE="$(printf '%s\n' "$DEVICE_LIST" | awk -v serial="$DEVICE_SERIAL" '$1 == serial {print $2; exit}')"
case "$DEVICE_STATE" in
    device) ;;
    unauthorized) fail "手机 $DEVICE_SERIAL 尚未授权，请解锁并允许 USB 调试。" ;;
    offline) fail "手机 $DEVICE_SERIAL 离线，请重新连接 USB。" ;;
    *) fail "设备 $DEVICE_SERIAL 不可用：${DEVICE_STATE:-未连接}。" ;;
esac

cd "$ROOT_DIR"
if command -v lsof >/dev/null 2>&1 && [[ -e Temp/UnityLockfile ]] && \
    lsof -t Temp/UnityLockfile >/dev/null 2>&1; then
    fail "项目正被编辑器占用，请保存并关闭此项目后重试。"
fi
mkdir -p Logs Builds/Android
echo "[1/3] 构建 APK；目标设备：$DEVICE_SERIAL"
echo "构建日志：$ROOT_DIR/Logs/gatebreaker_android_smoke_build.log"
if [[ ! -d HybridCLRData/LocalIl2CppData-OSXEditor/il2cpp/libil2cpp/hybridclr ]]; then
    echo "首次构建：安装 HybridCLR 本地运行时。"
    bash tools/build/install_hybridclr.sh || fail "HybridCLR 安装失败，详见 Logs/gatebreaker_hybridclr_install.log。"
fi

# 每次构建到独立目录，失败时保留上次 APK，且不会误装旧包。
BUILD_DIR="$(mktemp -d "$ROOT_DIR/Builds/Android/.build-install.XXXXXX")"
trap 'rm -rf "$BUILD_DIR"' EXIT
BUILD_APK="${BUILD_DIR#"$ROOT_DIR/"}/GatebreakerArena-android-smoke.apk"
if ! bash tools/build/build_android_smoke.sh "$BUILD_APK"; then
    fail "构建失败，未执行安装。详见 Logs/gatebreaker_android_smoke_build.log。"
fi
[[ -s "$ROOT_DIR/$BUILD_APK" ]] || fail "构建未生成有效 APK，未执行安装。"
APK_PATH="$ROOT_DIR/Builds/Android/GatebreakerArena-android-smoke.apk"
mv "$ROOT_DIR/$BUILD_APK" "$APK_PATH"

echo "[2/3] 覆盖安装到 $DEVICE_SERIAL"
if ! "$ADB_BIN" -s "$DEVICE_SERIAL" install -r "$APK_PATH" 2>&1 | tee Logs/android_install.log; then
    echo "请确认手机已解锁并开启“USB安装”；出现系统确认时请在手机上允许。" >&2
    echo "若提示签名冲突，本脚本不会自动卸载或清除数据。" >&2
    fail "安装失败，APK 已保留：$APK_PATH"
fi

if [[ "$LAUNCH" -eq 1 ]]; then
    echo "[3/3] 启动游戏"
    if ! "$ADB_BIN" -s "$DEVICE_SERIAL" shell am start -W \
        -n com.gatebreakerarena.game/com.unity3d.player.UnityPlayerActivity \
        2>&1 | tee Logs/android_launch.log; then
        fail "安装成功，但启动命令失败。详见 Logs/android_launch.log。"
    fi
    # am start 的部分错误只输出文本，退出码仍为 0。
    if ! grep -q 'Status: ok' Logs/android_launch.log; then
        fail "安装成功，但未确认启动成功。详见 Logs/android_launch.log。"
    fi
else
    echo "[3/3] 已按 --no-launch 跳过启动"
fi
echo "完成：$APK_PATH"
echo "设备：${DEVICE_SERIAL}；安装完成。玩法运行情况请在手机上确认。"
