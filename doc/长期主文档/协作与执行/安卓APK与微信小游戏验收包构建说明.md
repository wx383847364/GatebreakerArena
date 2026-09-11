# 安卓 APK 与微信小游戏验收包构建说明

## 目标

本说明记录 Gatebreaker Arena 首轮验收测试包构建链路。目标是产出可安装、可启动、可 smoke 验收的 Android APK 和微信小游戏导出工程，不包含正式上架签名、隐私合规、广告、支付和 CDN 灰度发布。

## 环境

- Unity/团结版本：`2022.3.62t9 / Tuanjie 1.9.1`
- Android：需要 Android Build Support、SDK、NDK、JDK
- 微信小游戏：通过 UPM 锁定官方插件 `com.qq.weixin.minigame`
- 微信插件来源：`https://github.com/wechat-miniprogram/minigame-tuanjie-transform-sdk.git#ed4ad28f433c6b52b5fd3f22a6fa155a0c98c228`

如编辑器路径不同，运行脚本前设置：

```bash
export UNITY_BIN=/path/to/Tuanjie.app/Contents/MacOS/Tuanjie
```

首次在一台机器上出 Player 包前，需要安装 HybridCLR 本地运行时：

```bash
tools/build/install_hybridclr.sh
```

## 构建命令

### 双击运行（Mac / Windows）

在 `Client` 工程根目录双击对应入口：

- **Mac：`BuildAndroid.command`**。
- **Windows：`BuildAndroid.bat`**（内部使用系统 Windows PowerShell 5.1，调用 `tools/build/build_and_install_android.ps1`）。

两者均执行“构建新 APK → ADB 覆盖安装 → 启动游戏”，结束后保留窗口显示结果。运行前保存并关闭该工程的编辑器，连接手机，开启 USB 调试和 USB 安装；设备授权、手机账号验证等仍在手机上完成。多台设备连接时，交互窗口会要求输入目标序列号。

Windows 会从 Program Files 下的 Unity/团结 Hub 默认安装目录查找项目版本对应的编辑器，找不到时弹出文件选择框，请选择该版本的 `Tuanjie.exe` / `Unity.exe`（不要选择 Hub）。仍需安装 Android Build Support、SDK、NDK、JDK；首次安装 HybridCLR 还需 Git 和网络。Mac 使用现有默认编辑器路径。

两端都支持 `UNITY_BIN`、`ADB_BIN`、`ANDROID_SERIAL`。Windows 命令行参数为 `-Serial 设备序列号` 和 `-NoLaunch`，Mac 参数为 `--serial 设备序列号` 和 `--no-launch`。Windows 子进程输出保存在 `Logs/*.stdout.log` / `Logs/*.stderr.log`，Unity 构建详细日志路径与 Mac 相同。

Mac 启动器已在本机检查；Windows 入口按 PowerShell 5.1 编写，当前 Mac 环境未进行 Windows 实机出包验证。

### 命令行运行

一键构建、ADB 覆盖安装并启动（运行前保存并关闭此项目的编辑器）：

```bash
tools/build/build_and_install_android.sh
```

只连接一台设备时自动选择。多台设备或模拟器连接时，显式指定目标：

```bash
tools/build/build_and_install_android.sh --serial 9125274112FF
```

- 加 `--no-launch` 可仅打包和安装；`--help` 查看帮助。
- 支持 `UNITY_BIN`、`ADB_BIN`（完整路径）和 `ANDROID_SERIAL` 环境变量；通过脚本绝对路径可从任意工作目录运行。
- 构建前检查设备连接和授权，缺少 HybridCLR 本地运行时时调用现有安装脚本。
- 每次在独立临时目录生成新 APK，成功后替换默认输出；构建失败不安装旧包，安装失败保留新 APK，不自动卸载应用或清除数据。
- 构建、安装、启动日志分别位于 `Logs/gatebreaker_android_smoke_build.log`、`Logs/android_install.log` 和 `Logs/android_launch.log`。启动成功仅确认 Android Activity 启动，玩法状态仍需真机检查。
- 一键脚本已通过 Bash 语法检查及 11 个隔离模拟检查，覆盖成功、设备缺失/未授权/离线/多设备、指定设备、构建失败、缺失新 APK、安装失败、启动文本错误、跳过启动；同时验证含空格路径和跨目录调用。此脚本新增后未重复执行完整真机构建。

Android smoke APK：

```bash
tools/build/build_android_smoke.sh
```

默认输出：

```text
Builds/Android/GatebreakerArena-android-smoke.apk
```

微信小游戏 smoke 工程：

```bash
WECHAT_APP_ID=wx-test-gatebreaker tools/build/build_weixin_smoke.sh
```

默认输出：

```text
Builds/WeixinMiniGame/GatebreakerArena-minigame-smoke
```

## 构建入口

Unity 菜单：

- `Gatebreaker/Build/Android Smoke APK`
- `Gatebreaker/Build/Weixin MiniGame Smoke`
- `Gatebreaker/Build/Validate Smoke Build Inputs`
- `Gatebreaker/Build/Install HybridCLR Local Runtime`

Batchmode 方法：

- `GatebreakerSmokeBuildPipeline.BuildAndroidSmokeApkFromCommandLine`
- `GatebreakerSmokeBuildPipeline.BuildWeixinMiniGameSmokeFromCommandLine`
- `GatebreakerSmokeBuildPipeline.InstallHybridClrFromCommandLine`

## 验收点

- `BootstrapScene` 是唯一启用场景。
- 本机已执行 HybridCLR Installer；否则脚本会在 Player 构建前中止并提示运行 `tools/build/install_hybridclr.sh`。
- 构建前生成并复制 HybridCLR 热更 DLL 和 AOT metadata 到 `Assets/HotUpdateContent/Res/HotUpdate`。
- HybridCLR 预构建先检查本地运行时，再生成 `Il2CppDef`（Unity/团结版本宏）、link.xml、裁剪 AOT DLL 和桥接代码，避免首次原生编译因版本宏为空失败。
- Android 构建使用 `GATEBREAKER_YOO_OFFLINE_PLAYMODE`，避免首轮依赖 CDN。
- 微信小游戏构建使用 `GATEBREAKER_YOO_OFFLINE_PLAYMODE` 和 `GATEBREAKER_WECHAT_MINIGAME`，登录、广告、支付保持 mock/fallback。
- 构建脚本会自动创建/维护 YooAssets `DefaultPackage` Collector，并覆盖 `Assets/HotUpdateContent/Res` 与 `Assets/HotUpdateContent/Config`，确保正式玩法配置随包分发。

## 已知限制

- 当前脚本不创建正式 keystore，不输出 AAB。
- 当前脚本不上传或发布 YooAssets 远端资源。
- 当前脚本不接入微信正式 AppID、支付、广告位或提审资料。
- 如 HybridCLR 与 WeixinMiniGame 平台存在兼容问题，先保留 Android smoke APK 作为基线，再拆分小游戏兼容性专项。

## USB 真机安装与诊断

构建成功后，使用 `adb devices -l` 确认目标设备，再执行（将 `<serial>` 替换为设备序列号）：

```bash
adb -s <serial> install -r Builds/Android/GatebreakerArena-android-smoke.apk
adb -s <serial> shell am start -W -n com.gatebreakerarena.game/com.unity3d.player.UnityPlayerActivity
adb -s <serial> pull /sdcard/Android/data/com.gatebreakerarena.game/files/logs/game.log Logs/android-device-startup.log
adb -s <serial> exec-out screencap -p > Logs/android-device-startup.png
```

- 红魔 NX809J / RedMagicOS 11.5 需额外打开开发者选项中的“USB安装”。仅打开“USB调试”时，安装可能报 `Caller has no access to session -1`；开关要求的账号验证由机主在手机上完成。
- 系统可能屏蔽 Unity 的 logcat 输出；优先拉取上述应用文件日志，不能把空 logcat 当作无错误。
- 验收应确认玩法配置加载、热更新入口成功和主界面显示，不能只看安装返回 `Success`。

## 完成情况

- 当前状态：Android 首屏真机验收已完成；微信小游戏本轮未验收。
- 最近更新：2026-09-08，团结 1.9.1 构建 ARM64 开发 APK，覆盖安装至 NX809J（Android API 36 / RedMagicOS 11.5.14MR4），主菜单正常显示。
- 本轮修复：补齐 HybridCLR `GenerateIl2CppDef`；YooAssets 收集增加 `Assets/HotUpdateContent/Config`，修复缺失 `gatebreaker_rules.bytes` 导致启动停留背景的问题。
- 构建产物：`Builds/Android/GatebreakerArena-android-smoke.apk`，56 MB 左右，版本 1.0.0（1），包名 `com.gatebreakerarena.game`。
- APK SHA-256：`5f182f13cc9ef4a424e75b83aa6d8c6322fc3dad4cc984d6b00e060d51a9c27e`。
- 本地证据：`Logs/gatebreaker_android_smoke_build.log`、`Logs/android-device-startup.log`、`Logs/android-device-startup.png`。文件日志存在缓冲，运行中拉取可能只有已刷盘前缀；主菜单显示另有截图确认。
- 验收边界：构建、安装、启动及主菜单显示通过；完整对局、联网和长时间稳定性由后续真机测试覆盖。
