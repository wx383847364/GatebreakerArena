# Gatebreaker Arena 字体与 TMP 方案 v0.1

## 文档定位

本文固定 Gatebreaker Arena 的 8bit 风格字体、TextMeshPro 资产生成、中文缺字 fallback 和 UI 接入口径。

当前要解决的问题：

- `LiberationSans SDF` 不包含中文字形，例如 `比`。
- TMP 找不到可用 fallback 时会用 `□` 代替中文。
- 这不是缺 Unity 字体模块，而是当前 TMP 字体资产缺少中文字形和 fallback 配置。

## 当前方案

参考 Holmas 项目的字体方案，但按 Gatebreaker 精简：

- 字体文件统一放入热更资源目录。
- 生成项目内受控 TMP Font Asset。
- 建立字体角色配置。
- Fusion Pixel 作为中文主字体和 fallback。
- UI 文本写入时只做字体兜底，不承载玩法规则。

## 字体选择

| 角色 | 字体 | 用途 |
|---|---|---|
| `ArcadeTitle` | Press Start 2P | 英文街机大标题，例如 `BATTLE MODE` |
| `PixelChinese` | Fusion Pixel 12px zh_hans | 中文 HUD、按钮、提示、常规 UI 文本 |
| `PixelBody` | Pixelify Sans | 英文玩家名、状态、数字和正文备选 |
| `PixelAlt` | DotGothic16 | 日式像素风备选标题或风格预览 |
| `Fallback` | Fusion Pixel 12px zh_hans | TMP 中文缺字回退 |

不使用 Zpix，避免商业授权不确定性。

## 资源目录

字体资源落点：

```text
Assets/HotUpdateContent/Res/fonts
├── source
├── licenses
├── tmp
└── preview
```

当前下载脚本：

```bash
bash tools/font/download_gatebreaker_fonts.sh
```

当前源字体：

- `source/FusionPixel12-Proportional-zh_hans.otf`
- `source/PressStart2P-Regular.ttf`
- `source/PixelifySans-wght.ttf`
- `source/DotGothic16-Regular.ttf`

许可证文件保存在：

- `licenses/OFL_FusionPixel.txt`
- `licenses/OFL_PressStart2P.txt`
- `licenses/OFL_PixelifySans.txt`
- `licenses/OFL_DotGothic16.txt`
- Fusion Pixel 附带的上游字体许可证文件

## TMP 资产生成

Editor 菜单：

```text
Gatebreaker/Fonts/Font Role Tool
Gatebreaker/Fonts/Generate TMP Font Assets
```

`Font Role Tool` 参考 Holmas 的字体角色工作台，提供：

- `Load / Create Default`：加载或创建默认字体角色 Profile。
- `Scan Fonts Folder`：扫描字体源目录并给空角色补推荐字体。
- `Generate / Refresh TMP Assets`：按角色生成或刷新 TMP Font Asset。
- `Create / Refresh Runtime Settings`：刷新运行时字体兜底配置。
- `Scan UI Texts`：扫描 `BootstrapScene.scene` 和字体预览 prefab 的文本角色匹配结果。
- `Apply To UI Assets`：按当前角色和手动 override 替换 UI 文本字体引用。

`Apply To UI Assets` 是显式写入操作，点击后会再次确认。它只写 `TMP_Text.font` 或 `UnityEngine.UI.Text.font` 引用，不改文本内容、字号、颜色、对齐、布局、透明度、材质参数或玩法逻辑。

生成资产：

- `tmp/Gatebreaker_PixelChinese_FusionPixel12_TMP.asset`
- `tmp/Gatebreaker_ArcadeTitle_PressStart2P_TMP.asset`
- `tmp/Gatebreaker_PixelBody_PixelifySans_TMP.asset`
- `tmp/Gatebreaker_PixelAlt_DotGothic16_TMP.asset`

生成工具还会：

- 创建或刷新 `GatebreakerFontRoleProfile.asset`
- 创建或刷新 `GatebreakerFontRuntimeSettings.asset`
- 把 Fusion Pixel TMP 字体加入 `TMP Settings.asset` 的 fallback font assets
- 生成 `preview/GatebreakerFontPreview.prefab`

## 运行时接入

运行时字体兜底由 `GatebreakerRuntimeTmpFontResolver` 负责。

当前接入点：

- `GatebreakerArenaSceneBindingService.SetText(...)`

规则：

- 先检查当前 TMP 字体是否支持要显示的文本。
- 再检查 TMP 默认字体。
- 再检查 Gatebreaker 字体运行时配置。
- 再检查 TMP Settings 全局 fallback。
- 必要时使用项目内 Fusion Pixel 源字体创建 dynamic fallback。

UI 约束：

- UI 只做表现和输入转发。
- 不在字体 resolver 中写得分、发球、AI、碰撞、同步或结算规则。
- 替换字体时只写字体引用，不批量改 prefab 或 scene 的颜色、字号、布局、透明度或材质参数。

## 字体替换流程

常规替换推荐顺序：

1. 打开 `Gatebreaker/Fonts/Font Role Tool`。
2. 点击 `Load / Create Default`。
3. 在 `Font Roles` 中把某个角色的 `Source Font` 换成目标字体。
4. 点击 `Generate / Refresh TMP Assets`，生成或刷新对应 TMP Font Asset。
5. 点击 `Create / Refresh Runtime Settings`，同步运行时 fallback。
6. 点击 `Scan UI Texts`，检查每个文本匹配到的角色和 warning。
7. 如某个文本分类不符合预期，在扫描报告里手动改角色，override 会保存到 Profile。
8. 确认报告后点击 `Apply To UI Assets`，把字体引用写入 `BootstrapScene.scene` 和字体预览 prefab。

只想换中文兜底字体时，优先替换 `PixelChinese` 和 `Fallback` 两个角色，并确认新字体包含 `比分阶段弹药比赛结束房间号`。

## 预览与验收

预览资源：

- `Assets/HotUpdateContent/Res/fonts/preview/GatebreakerFontPreview.prefab`
- `Assets/HotUpdateContent/Res/fonts/preview/GatebreakerFontPreview.html`

验收标准：

- `比分`、`阶段`、`弹药`、`比赛结束`、`房间号` 能正常显示。
- Console 不再出现 `LiberationSans SDF` 缺少 `\u6BD4` 的警告。
- Press Start 2P 显示英文标题时，中文能通过 fallback 正常显示。
- 现有 HUD 刷新逻辑不改变玩法规则。

## 验证建议

1. 运行下载脚本。
2. 在 Editor 中打开 `Gatebreaker/Fonts/Font Role Tool`。
3. 执行 `Generate / Refresh TMP Assets`，确认各角色 TMP Font 不为 None。
4. 执行 `Scan UI Texts`，确认 BootstrapScene 和字体预览 prefab 能输出扫描报告。
5. 需要写入字体引用时，执行 `Apply To UI Assets`。
6. 打开字体预览 prefab 目视检查。
7. Play BootstrapScene，确认中文 HUD 不再显示方块。
8. 运行 UI / 字体相关 EditMode 测试。

## 完成情况

- 当前状态：已完成
- 进度说明：字体资源、TMP 资产、fallback、预览、Holmas 风格字体角色工具和验证测试均已落地；工具支持显式 Apply 字体引用。
- 最近更新：2026-05-30，补齐 Holmas 风格 `Font Role Tool`，支持角色管理、TMP 生成、runtime settings 刷新、UI 文本扫描预览和受控字体替换。
