# Codex 新会话必读

## 当前项目

- 项目名称：Gatebreaker Arena
- 当前 Git 仓库根、Unity 工程根、工具命令执行根：`Client/`
- 本机路径示例：`<workspace>/GatebreakerArena/Client`
- 当前阶段：Unity + HybridCLR + YooAssets 独立工程骨架

## 必读文档

- [项目总览](../项目总览.md)
- [热更新边界规范 v1](../架构与边界/热更新边界规范_v1.md)
- [Agent 启动与验收规范](Agent%20启动与验收规范.md)
- [skill 与 subagent 任务模板](skill%20与%20subagent%20任务模板.md)
- [Git 提交建议与确认规则](Git%20提交建议与确认规则.md)
- [文档维护与任务收尾流程](任务完成后自动维护文档.md)

## 任务分类与 skill 门禁

每轮实现功能前，主线程必须自行判断任务属于哪类，不要把分类问题丢回给用户：

- `纯逻辑`：只改服务、模型、配置、算法、验证脚本，不触碰 UI 文件、prefab、场景绑定或页面流程。
- `UI 相关`：涉及 UGUI、prefab、HUD、View、Presenter、Controller、binding、页面流转、按钮、图片、文本、布局、动画、安全区，或路径包含 `App.HotUpdate.GatebreakerArena/UI`、`Assets/Res`、`Assets/Scenes` 中的 UI 绑定。
- `工程/边界`：涉及 `App.AOT / App.Shared / App.HotUpdate` 分层、HybridCLR、YooAssets、跨层 DTO、入口和组合层。

执行门禁：

- 正式功能开发、测试或审查默认先读 `$gatebreaker-hotupdate-boundary`。
- 只要任务属于 `UI 相关`，必须再读 `$gatebreaker-ui-boundary`。
- UI 相关任务默认保护原 prefab 视觉参数：没有明确要求时，不改颜色、透明度、tint、材质颜色或 `CanvasGroup.alpha`。

## 固定边界

- `App.AOT` 只做宿主基础设施。
- `App.Shared` 只放稳定跨层契约。
- `App.HotUpdate.GatebreakerArena` 承载玩法。
- 正式资源走 YooAssets。
