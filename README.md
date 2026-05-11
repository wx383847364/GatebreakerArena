# GatebreakerArena

Gatebreaker Arena 是一个基于 Unity 的多人弹球攻防竞技项目。

项目当前复用 `App.AOT / App.Shared / App.HotUpdate` 三层架构，并以 HybridCLR 和 YooAssets 作为热更新与资源管线基础。

当前重点：

- 保持 Unity 工程骨架干净、可打开、可编译
- 保持清晰的 `App.AOT / App.Shared / App.HotUpdate` 分层边界
- 正式运行时资源统一通过 YooAssets 加载
- 使用项目文档维护流程记录迭代、生成提交建议和维护长期文档
