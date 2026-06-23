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
- Android 构建使用 `GATEBREAKER_YOO_OFFLINE_PLAYMODE`，避免首轮依赖 CDN。
- 微信小游戏构建使用 `GATEBREAKER_YOO_OFFLINE_PLAYMODE` 和 `GATEBREAKER_WECHAT_MINIGAME`，登录、广告、支付保持 mock/fallback。
- 构建脚本会自动创建/维护 YooAssets `DefaultPackage` Collector，并覆盖 `Assets/HotUpdateContent/Res`。

## 已知限制

- 当前脚本不创建正式 keystore，不输出 AAB。
- 当前脚本不上传或发布 YooAssets 远端资源。
- 当前脚本不接入微信正式 AppID、支付、广告位或提审资料。
- 如 HybridCLR 与 WeixinMiniGame 平台存在兼容问题，先保留 Android smoke APK 作为基线，再拆分小游戏兼容性专项。
