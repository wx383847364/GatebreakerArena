# Codex 新会话必读

## 当前项目

- 项目名称：Gatebreaker Arena
- 工程目录：`/Users/bruce/work/GatebreakerArena`
- 当前阶段：Unity + HybridCLR + YooAssets 独立工程骨架

## 必读文档

- [项目总览](/Users/bruce/work/GatebreakerArena/doc/长期主文档/项目总览.md)
- [热更新边界规范 v1](/Users/bruce/work/GatebreakerArena/doc/长期主文档/架构与边界/热更新边界规范_v1.md)
- [Git 提交建议与确认规则](/Users/bruce/work/GatebreakerArena/doc/长期主文档/协作与执行/Git%20提交建议与确认规则.md)
- [文档维护与任务收尾流程](/Users/bruce/work/GatebreakerArena/doc/长期主文档/协作与执行/任务完成后自动维护文档.md)

## 固定边界

- `App.AOT` 只做宿主基础设施。
- `App.Shared` 只放稳定跨层契约。
- `App.HotUpdate.GatebreakerArena` 承载玩法。
- 正式资源走 YooAssets。
