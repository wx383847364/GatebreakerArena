# skill 与 subagent 任务模板

这页只负责 Gatebreaker Arena 的 skill 选择、常用组合和派工模板。

## 当前核心 skill

- `gatebreaker-hotupdate-boundary`
  - 正式功能开发、测试或审查默认先带。
  - 负责 `App.AOT / App.Shared / App.HotUpdate.GatebreakerArena` 边界、HybridCLR、YooAssets 和玩法/UI 职责隔离。
- `gatebreaker-ui-boundary`
  - 涉及 UGUI、prefab、HUD、View、Presenter、Controller、binding、页面流转、按钮、图片、文本、布局、动画或安全区时叠加。
  - 负责 UI 表现层边界、显式绑定、prefab 视觉参数保护和 UI 审查口径。

## 选 skill 的固定顺序

0. 先自行判断任务分类，不要把分类问题丢回给用户：
   - `纯逻辑`：只改服务、模型、配置、算法、验证脚本，不触碰 UI 文件、prefab、场景绑定或页面流程。
   - `UI 相关`：涉及 UGUI、prefab、HUD、View、Presenter、Controller、binding、页面流转、按钮、图片、文本、布局、动画、安全区、`CanvasGroup`、`Graphic`、`Button`、`Image`、`TMP/Text`，或路径包含 `App.HotUpdate.GatebreakerArena/UI`、`Assets/Res`、`Assets/Scenes` 中的 UI 绑定。
   - `工程/边界`：涉及 `App.AOT / App.Shared / App.HotUpdate` 分层、HybridCLR、YooAssets、跨层 DTO、入口和组合层。
1. 正式功能开发、测试或审查默认先带 `gatebreaker-hotupdate-boundary`。
2. 只要任务属于 `UI 相关`，必须再带 `gatebreaker-ui-boundary`。
3. 测试或审查线默认镜像被测对象的 skill 组合。
4. 未经明确要求，UI 相关任务不得改原 prefab 的颜色、透明度、tint、材质颜色或 `CanvasGroup.alpha`。

## 常用组合

- `工程骨架 / 边界 / 资源 / HotUpdate 入口`
  - `gatebreaker-hotupdate-boundary`
- `Match / Ball / Paddle / Zone / Serve / AI / 配置`
  - `gatebreaker-hotupdate-boundary`
- `HUD / UI / Presenter / prefab / 场景绑定`
  - `gatebreaker-hotupdate-boundary + gatebreaker-ui-boundary`
- `测试 / 审查`
  - 镜像被测对象；审查 UI 时必须包含 `gatebreaker-ui-boundary`

## 通用派工模板

```text
你负责 Gatebreaker Arena 的……实现。
请遵循 $gatebreaker-hotupdate-boundary。
如果本轮涉及 UGUI、prefab、HUD、View、Presenter、Controller、binding、页面流转、按钮、图片、文本、布局、动画或安全区，请额外遵循 $gatebreaker-ui-boundary。

任务分类：
- 纯逻辑 / UI 相关 / 工程/边界

目标：
1. ……
2. ……

允许写入范围：
- ……

禁止写入范围：
- ……
- UI 相关任务未经明确要求，不得改原 prefab 的颜色、透明度、tint、材质颜色或 CanvasGroup.alpha。

交付：
- 修改文件
- 输入、输出和依赖接口
- 验证结果
- 风险和阻塞

验收点：
- 是否符合 `App.AOT / App.Shared / App.HotUpdate` 边界。
- UI 相关任务是否使用显式绑定，并保留原 prefab 颜色、透明度、tint、材质颜色和 CanvasGroup.alpha。
```
