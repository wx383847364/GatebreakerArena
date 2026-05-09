# Agent 启动与验收规范

这页固定 Gatebreaker Arena 的 agent 分工入口和迭代记录默认状态。

## 迭代文档默认分工状态

- Agent 1：工程骨架与 AOT/Shared/HotUpdate 边界，未启动
- Agent 2：核心对局玩法与 Match/Ball/Paddle/Zone/Serve 模块，未启动
- Agent 3：配置表、数值和 YooAssets 资源接入，未启动
- Agent 4：UI HUD、Presenter 和场景绑定，未启动
- Agent 5：验证、测试、HybridCLR/YooAssets 构建链路，未启动
- Agent 6：审查、边界复核和提交前验收，未启动

## 验收原则

- 改动必须符合 `App.AOT / App.Shared / App.HotUpdate` 边界。
- 工程级改动必须能通过边界检查和文档同步。
- 首轮迁移阶段以可打开、可编译、可提交为优先。
