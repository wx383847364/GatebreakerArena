# Agent 启动与验收规范

这页固定 Gatebreaker Arena 的 agent 分工入口和迭代记录默认状态。

## 迭代文档默认分工状态

- Agent 1：工程骨架与 AOT/Shared/HotUpdate 边界，未启动
- Agent 2：核心对局玩法与 Match/Ball/Paddle/Zone/Serve 模块，未启动
- Agent 3：配置表、数值和 YooAssets 资源接入，未启动
- Agent 4：UI HUD、Presenter 和场景绑定，未启动
- Agent 5：验证、测试、HybridCLR/YooAssets 构建链路，未启动
- Agent 6：审查、边界复核和提交前验收，未启动

## 启动前判断

主线程在进入任何实际执行前，必须自行完成三项判断：

- 任务分类：`纯逻辑` / `UI 相关` / `工程/边界`
- 执行方式：`主线程直做` / `主线程 + helper` / `主线程 + 真实 subagent`
- 必带 skill：按 [skill 与 subagent 任务模板](skill%20与%20subagent%20任务模板.md) 选择

每次启动真实 subagent 时，派工必须写清：

- Agent 名称
- 任务分类
- 必带 skill
- 目标
- 允许写入范围
- 禁止写入范围
- 交付物
- 验收点

## 验收原则

- 改动必须符合 `App.AOT / App.Shared / App.HotUpdate` 边界。
- 工程级改动必须能通过边界检查和文档同步。
- 首轮迁移阶段以可打开、可编译、可提交为优先。
- UI 相关任务必须确认：运行时 UI 不承载玩法规则，节点引用来自显式绑定，且未在未授权情况下改动 prefab 颜色、透明度、tint、材质颜色或 `CanvasGroup.alpha`。
