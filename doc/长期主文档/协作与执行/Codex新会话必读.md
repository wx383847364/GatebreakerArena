# Codex 新会话必读

## 当前项目

- 项目名称：Gatebreaker Arena
- 当前工作区根：`<workspace>/GatebreakerArena`
- Git 仓库根、Unity 工程根、默认工具命令执行根：`Client/`
- 本机 Unity 工程路径示例：`<workspace>/GatebreakerArena/Client`
- Unity Editor 版本：`2022.3.62t2 (24637c95313f)`
- 团结 Editor 版本：`1.8.0`
- 本机 Editor 路径：`/Applications/Tuanjie/Hub/Editor/2022.3.62t2/Tuanjie.app/Contents/MacOS/Tuanjie`
- 本文中未特别说明的工程路径，均以 `Client/` 为根。
- 当前阶段：Unity + HybridCLR + YooAssets 独立工程骨架

## 必读文档

以下链接均以本文所在目录 `Client/doc/长期主文档/协作与执行/` 为基准：

- [项目总览](../项目总览.md)
- [热更新边界规范 v1](../架构与边界/热更新边界规范_v1.md)
- [Agent 启动与验收规范](Agent%20启动与验收规范.md)
- [skill 与 subagent 任务模板](skill%20与%20subagent%20任务模板.md)
- [Git 提交建议与确认规则](Git%20提交建议与确认规则.md)
- [文档维护与任务收尾流程](任务完成后自动维护文档.md)

## 任务分类与 skill 门禁

每轮实现功能前，主线程必须自行判断任务属于哪类，不要把分类问题丢回给用户：

- `纯逻辑`：只改服务、模型、配置、算法、验证脚本，不触碰 UI 文件、prefab、场景绑定或页面流程。
- `UI 相关`：涉及 UGUI、prefab、HUD、View、Presenter、Controller、binding、页面流转、按钮、图片、文本、布局、动画、安全区，或路径包含 `Assets/HotUpdateContent/Script/App.HotUpdate/GatebreakerArena/UI`、`Assets/HotUpdateContent/Res`、`Assets/Scenes` 中的 UI 绑定。
- `工程/边界`：涉及 `Assets/Scripts/App.AOT`、`Assets/Scripts/App.Shared`、`Assets/HotUpdateContent/Script/App.HotUpdate` 分层，或 HybridCLR、YooAssets、跨层 DTO、入口和组合层。

执行门禁：

- 正式功能开发、测试或审查默认先读 `$gatebreaker-hotupdate-boundary`。
- 只要任务属于 `UI 相关`，必须再读 `$gatebreaker-ui-boundary`。
- UI 相关任务默认保护原 prefab 视觉参数：没有明确要求时，不改颜色、透明度、tint、材质颜色或 `CanvasGroup.alpha`。

验证规则：

- 除非必须验证当前工程编译、场景、资源或真实绑定，优先在 `/private/tmp` 或 `/tmp` 创建临时 Unity/Tuanjie 工程验证。
- 如果当前 `Client/` 已被打开的 Unity/Tuanjie Editor 占用，batchmode 不要停在“项目被占用”；应复制 `Client/` 到 `/tmp` 或 `/private/tmp` 的临时工程，用本文记录的 Editor 路径在临时副本跑 EditMode/PlayMode。
- 临时工程验证通过后，除非有必要保留现场，否则必须删除临时工程；可用 `tools/repo_maintenance/clean_hub_temp_projects.sh` 清理。
- 删除后再检查 `/private/tmp` 和 `/tmp` 中仍残留的 Gatebreaker/Unity/Tuanjie 临时工程；只有发现残留时才在回复里列出。

## 固定边界

- `App.AOT`：宿主基础设施，代码根为 `Assets/Scripts/App.AOT`。
- `App.Shared`：稳定跨层契约，代码根为 `Assets/Scripts/App.Shared`。
- `App.HotUpdate.GatebreakerArena`：正式玩法，代码根为 `Assets/HotUpdateContent/Script/App.HotUpdate/GatebreakerArena`。
- 正式资源走 YooAssets。
