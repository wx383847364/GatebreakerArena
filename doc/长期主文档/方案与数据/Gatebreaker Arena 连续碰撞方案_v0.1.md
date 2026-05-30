# Gatebreaker Arena 连续碰撞方案 v0.1

## 文档定位

本文记录 Gatebreaker Arena 当前原型的连续碰撞基线。

当前状态：已落地为 HotUpdate runtime 的默认碰撞推进方式。

当前实现位置：

- `Client/Assets/HotUpdateContent/Script/App.HotUpdate/GatebreakerArena/Match/GatebreakerMatchRuntime.cs`
- 入口方法：`SimulateBallsSwept`
- 飞行球推进：`SimulateFlyingBallSwept`
- 事件选择：`TryFindEarliestSweepHit`
- 挡板事件：`TryAddPaddleSweepHit` / `ResolveSweptPaddleHit`
- 球门事件：`TryAddBoundarySweepHit` / `TryAddBoundarySegmentSweepHit` / `ResolveSweptGoalHit`
- 墙体事件：`ResolveSweptWallHit`

本文后续用于实现审查、测试补充和新 UI/联网接入时确认规则边界。玩法物理必须继续留在 `App.HotUpdate.GatebreakerArena`。

## 当前结论

项目采用 `moving swept collision` + 单 tick 内事件迭代上限。

固定子步进不再作为正确性来源。当前实现用同一条 tick 时间轴推进球和挡板，按最早 `time-of-impact / TOI` 处理事件。

核心原则：

- 球和挡板都在同一 tick 时间轴上推进。
- 在同一时间参数下计算 `ball(t)` 与 `paddle(t)`。
- 比较 `Paddle / Goal / Wall` 的最早 TOI。
- 浮点并列时使用固定兜底优先级：`Paddle > Goal > Wall`。
- `GoalRebound`、得分销毁、owner ball count 回收、多球、加时绝杀继续走现有 runtime 规则入口。

## HotUpdate 边界

允许修改：

- `App.HotUpdate.GatebreakerArena.Match`
- `App.HotUpdate.GatebreakerArena.Ball`
- `App.HotUpdate.GatebreakerArena.Paddle`
- `App.HotUpdate.GatebreakerArena.Zone`
- 纯 C# 玩法测试

禁止事项：

- 不把玩法物理放进 `App.AOT`。
- 不向 `App.Shared` 添加玩法实现。
- 不让 `MonoBehaviour`、OnGUI、UGUI 或 prefab binding 承载碰撞、得分、球门、加时或销毁规则。
- 不用 UI 状态覆盖 runtime 判定。
- 不新增 `Resources.Load` 作为碰撞或表现资源入口。

UI/表现层只负责提交输入、绑定调参、读取 runtime 状态和刷新表现。

## 当前算法

每个 tick 的顺序：

```text
采集输入 / AI
计算每个挡板 oldAxis -> targetAxis
对每颗球做 swept 事件迭代
  在任意时间 t 上，用 paddle(t) 判断挡板位置
  在同一时间轴比较 Paddle / Goal / Wall 的 TOI
  处理最早事件
tick 末提交挡板最终位置与 TangentVelocity
刷新危险提示
```

时间函数：

```text
ball(t) = ballStart + ballVelocity * segmentDuration * t
paddleAxis(t) = paddleOldAxis + (paddleTargetAxis - paddleOldAxis) * t
paddlePos(t) = Arena.GetPaddleCenter(paddleNormal, paddleAxis(t))
```

`t` 范围为 `0..1`，表示当前剩余物理段内的归一化时间。挡板 motion 通过 `PaddleMotionState.GetPosition(normalizedTime)` 读取。

## 当前实现参数

当前 runtime 常量：

| 常量 | 当前值 | 说明 |
|---|---:|---|
| `MaxCollisionIterations` | 12 | 单颗球单 tick 内最多处理的连续事件数 |
| `CollisionEpsilon` | 0.0001 | 浮点误差阈值 |
| `CollisionSkin` | 0.02 | 碰撞后推出距离 |

当前行为：

- `deltaTime < 0` 会 clamp 到 0。
- `deltaTime == 0` 走 `ResolveFieldCollisions`，用于静态碰撞检查和 rebound 状态清理。
- 加时绝杀后 runtime 进入 `Result`，当前 tick 剩余物理事件会停止改写结果。
- 当前没有单独暴露 `MaxPhysicsTimePerTick` 配置。

## 挡板 TOI

挡板命中要求球从挡板正面穿过挡板平面：

```text
n0 = Dot(ballStart - paddlePos(segmentStart), paddle.Normal)
n1 = Dot(ballEnd - paddlePos(segmentEnd), paddle.Normal)
```

候选命中时间成立后，必须在命中时刻检查切线范围：

```text
hitPoint = ball(hitT)
paddleAtHit = paddle(globalHitT)
tangentDistance = Dot(hitPoint - paddleAtHit.Position, paddle.Tangent)

abs(tangentDistance) <= paddle.Length * 0.5f + epsilon
```

命中后：

- `hitOffset = tangentDistance / (paddle.Length * 0.5f)`。
- 反弹调用 `PaddleBounceCalculator.CalculateBounce`。
- `normalizedPaddleVelocity` 使用该 tick 内真实挡板切向速度。
- 球位置推出到 `paddleAtHit + tangent * tangentDistance + normal * (thickness + skin)`。
- 调用 `BallSimulationSystem.ClampSpeed`。
- 继续消费剩余物理时间。

当前实现还处理嵌入挡板的起始状态：如果球已经贴近或嵌入挡板且正在向内运动，可生成 `time = 0` 的挡板事件。

## 球门与墙 TOI

当前支持两类边界：

- 旧四边矩形边界。
- `ArenaGeometry.CreateScene3v3` 派生出的自定义边界段。

四边矩形语义：

- bottom：`y < -HalfHeight`
- top：`y > HalfHeight`
- left：`x < -HalfWidth`
- right：`x > HalfWidth`

自定义边界语义：

- `ArenaBoundarySegment` 提供 `Start / End / InwardNormal`。
- 有 `GoalPlayerIndex` 的段可以产生 Goal。
- 无有效 goal 或不在 goal span 内的碰撞按 Wall 处理。
- `Scene3v3` 中未激活玩家对应的网段按墙处理。

Goal 事件继续调用 `ResolveGoalEntry`：

- 敌方球进门：计分、销毁球、回收 owner ball count。
- 己方球进己方守护区：进入 `GoalRebound`，不计分、不销毁。

Wall 事件继续使用 `BallRule.WallBounceFactor`。

## 规则保持

连续碰撞不改变玩法规则：

- `GoalRebound` 仍由 `GoalJudgeSystem` 设置和结束。
- 得分、销毁、owner ball count 回收仍由 `GatebreakerMatchRuntime.ResolveGoalEntry` 和 `RemoveBall` 处理。
- 加时绝杀仍由 `IsOvertimeWinningScore` 和 `EndWithWinner` 处理。
- 多球逐颗处理，已销毁球不再参与后续碰撞。
- 四方向挡板统一使用 `Normal / Tangent / AxisPosition`。
- `Scene3v3` 的边界与可得分区域以 `ArenaGeometry` 为权威。

如果同一 tick 内出现多个事件，唯一权威顺序是最早 TOI。只有浮点并列时才使用 `Paddle > Goal > Wall`。

## 与当前项目后续修改的关系

后续 UI、LAN、资源加载、配置表修改都不应改变本方案的规则边界。

UGUI 接入时保持：

```text
UGUI 按钮 / 摇杆 / 滑条
 -> InputService / Presenter
 -> runtime.ApplyInputFrame(...)
 -> GatebreakerMatchRuntime.Tick(...) 或 StepFrame(...)
 -> HUD snapshot / runtime state
 -> UGUI 刷新显示
```

LAN/Lockstep 接入时保持：

```text
本地输入量化
 -> LockstepSession 收齐帧输入
 -> GatebreakerMatchRuntime.StepFrame(...)
 -> runtime checksum
 -> checksum report / desync handling
```

关键要求：

- 同步层只提交输入帧，不重算碰撞规则。
- UI 层只显示结果，不参与 TOI 判定。
- AOT transport 只传输字节和事件，不理解玩法物理。

## 验收测试计划

以下测试应继续覆盖在 `GatebreakerMatchRuntimeTests` 或相邻测试中：

- 静止挡板 + 大 `deltaTime`：球跨过挡板和门线时，必须先反弹，不得分。
- 移动挡板 + 大 `deltaTime`：球命中挡板中间时刻的真实位置，必须反弹。
- 移动挡板幽灵碰撞：球轨迹只会碰到最终挡板位置，但实际时间上挡板尚未到位，不能反弹。
- 极高速：速度超过 `BallRule.MaxSpeed` 时仍按 swept 处理，不依赖子步上限。
- 四方向挡板：P1/P2/P3/P4 都覆盖，反弹后 `Dot(ball.Velocity, paddle.Normal) > 0`。
- 边缘命中：`halfLength` 内命中，`halfLength + epsilon` 不命中并按球门或墙继续处理。
- 同 tick 多事件：挡板、球门线、墙按最早 TOI 处理；浮点并列时按 `Paddle > Goal > Wall`。
- 己方球进己方守护区：进入 `GoalRebound`，不计分，不销毁。
- 敌方球进门：计分、销毁、owner ball count 正常回收。
- 多球同帧：一颗得分销毁不影响另一颗反弹或继续飞行。
- 加时绝杀：绝杀发生后同 tick 剩余球不再改写结果。
- `deltaTime == 0`：保持静态碰撞检查，并清理挡板速度。
- `Scene3v3`：活动 goal 段能得分，非活动网段和 goal span 外区域按墙处理。

建议验证命令：

```bash
bash tools/validation/run_gatebreaker_validation.sh
```

PlayMode smoke：

```bash
bash tools/validation/run_gatebreaker_playmode_smoke.sh
```

## 后续债务

- 连续碰撞目前集中在 `GatebreakerMatchRuntime`，如果继续增长，可抽出 HotUpdate 纯逻辑服务，但不能移到 AOT 或 UI。
- `MaxCollisionIterations`、`CollisionSkin` 暂为 runtime 常量，是否配表化需要在玩法稳定后决定。
- `GoalRebound` 当前按入射反方向回弹，若后续要加入更强表现或角度控制，需要保持 `GoalJudgeSystem` 作为规则入口。
- `FinalPhaseBallSpeedScale` 是否影响飞行中球速尚需单独设计确认，不能顺手塞进碰撞代码。

## Subagent 分工建议

如果后续继续拆分任务：

- 实现 agent：只改 HotUpdate runtime 或新增 HotUpdate 纯逻辑碰撞服务。
- 测试 agent：补齐 `GatebreakerMatchRuntimeTests` 和 Scene3v3 边界测试。
- 审查验证 agent：只读审查 TOI 顺序、边界、加时、多球和测试覆盖，并运行验证脚本。

并行前必须冻结约束：玩法物理在 HotUpdate，表现层只做输入与刷新，Shared 不承载玩法实现。

## 完成情况

- 当前状态：已落地，继续维护
- 最近更新：2026-05-30
- 说明：moving swept collision 已作为当前 runtime 默认推进方式；后续修改以保持规则边界、补齐测试和降低 runtime 文件复杂度为主。
