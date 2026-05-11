# Gatebreaker Arena 连续碰撞方案 v0.1

## 文档定位

本文记录 Gatebreaker Arena 当前原型从固定子步进升级到 `moving swept collision` 的落地方案。

目标是解决低帧率、卡顿或极高速场景下：

- 球穿过挡板后直接进门。
- 同一 tick 内本应先碰板，却因当前位置先越界而得分。
- 挡板先按整帧 `deltaTime` 瞬移到最终位置，导致漏碰旧位置或中间位置。
- 挡板提前到最终位置参与所有球子步碰撞，导致幽灵碰撞。

本文作为后续实现、测试和 subagent 拆分的权威入口。实现前仍需阅读热更新边界规范，玩法物理逻辑必须留在 `App.HotUpdate.GatebreakerArena`。

## 最终决策

采用 `moving swept collision` + 单 tick 内事件迭代上限。

不再把固定子步进作为正确性来源。固定子步进只能降低穿透概率，不能保证事件发生顺序；如果挡板仍先按整帧移动到最终位置，再对球做子步碰撞，会继续产生漏碰和幽灵碰撞。

核心原则：

- 球和挡板都在同一条 tick 时间轴上推进。
- 同一时间参数下计算 `ball(t)` 与 `paddle(t)`。
- 按最早 `time-of-impact / TOI` 处理 `Paddle / Goal / Wall`。
- 浮点并列时使用固定兜底优先级：`Paddle > Goal > Wall`。
- `GoalRebound`、得分销毁、多球和加时绝杀继续走现有 runtime 规则入口，不重写业务规则。

## HotUpdate 边界

本方案只允许修改 HotUpdate 玩法层：

- 主要落点：`App.HotUpdate.GatebreakerArena.Match`
- 可新增纯逻辑碰撞服务，例如 `BallSweepCollisionSystem` 或 `SweptBallSimulationSystem`
- 继续复用 `PaddleBounceCalculator`
- 继续复用 `GoalJudgeSystem`

禁止事项：

- 不把玩法物理放进 `App.AOT`
- 不向 `App.Shared` 添加非必要玩法实现
- 不让 `MonoBehaviour`、OnGUI 或未来 UGUI 承载碰撞、得分、加时或球门规则
- 不新增配置表字段作为本轮方案前置条件

UI/表现层只负责提交 `PlayerInputFrame`、绑定调参值、读取 runtime 状态并刷新表现。

## 核心算法

每个 tick 的物理推进改为：

```text
采集输入/AI
计算每个挡板 oldAxis -> targetAxis
对每颗球做 swept 事件迭代
  在任意时间 t 上，用 paddle(t) 判断挡板位置
  在同一时间轴上比较 Paddle / Goal / Wall 的 TOI
  处理最早事件
tick 末提交挡板最终位置与 TangentVelocity
刷新危险提示
```

同一 tick 内的时间函数：

```text
ball(t) = ballStart + ballVelocity * segmentDuration * t
paddleAxis(t) = paddleOldAxis + (paddleTargetAxis - paddleOldAxis) * t
paddlePos(t) = Arena.GetPaddleCenter(paddleNormal, paddleAxis(t))
```

`t` 范围为 `0..1`，表示当前剩余物理段内的归一化时间。

### 挡板 TOI

对每个挡板，先判断球是否从挡板正面穿过挡板平面：

```text
n0 = Dot(ballStart - paddlePos(0), paddle.Normal)
n1 = Dot(ballEnd - paddlePos(1), paddle.Normal)
```

候选命中时间 `hitT` 成立后，必须用该时刻的挡板位置判断切线范围：

```text
hitPoint = ball(hitT)
paddleAtHit = paddle(hitT)
tangentDistance = Dot(hitPoint - paddleAtHit.Position, paddle.Tangent)

abs(tangentDistance) <= paddle.Length * 0.5f
```

命中后：

- `hitOffset = tangentDistance / (paddle.Length * 0.5f)`
- 反弹仍调用 `PaddleBounceCalculator.CalculateBounce`
- `normalizedPaddleVelocity` 使用该物理段真实挡板位移 / 时间
- 球位置推出到 `paddleAtHit.Position + tangent * tangentDistance + normal * (thickness + skin)`
- 调用现有速度 clamp
- 继续消费剩余物理时间

### 球门与墙 TOI

球门线和墙都按 `start -> end` 线段做 sweep。

四条边界语义保持当前 arena 定义：

- bottom：`y < -HalfHeight`
- top：`y > HalfHeight`
- right：`x > HalfWidth`
- left：`x < -HalfWidth`

有 zone 的边界生成 `Goal` 事件；无 zone 的边界生成 `Wall` 事件。

`Goal` 事件继续调用现有 `ResolveGoalEntry`：

- 敌方球进门：计分并销毁球
- 己方球进己方守护区：进入 `GoalRebound`，不计分，不销毁

`Wall` 事件继续使用现有墙反弹规则和 `WallBounceFactor`。

### deltaTime 与迭代默认值

默认实现策略：

- `deltaTime < 0` clamp 到 `0`
- `deltaTime == 0` 只做静态碰撞检查，并清理挡板 `TangentVelocity = 0`
- `MaxCollisionIterations` 默认 8 或 12，由实现时作为 HotUpdate runtime 常量落地
- 加时绝杀后立即停止当前 tick 剩余物理事件，避免同帧其他球继续改写结果
- 极端卡顿可增加 `MaxPhysicsTimePerTick`，例如 `0.25f` 或 `0.33f`，但不作为首轮必须新增配置项

## 规则保持

本方案不改变 Gatebreaker Arena 的玩法规则：

- `GoalRebound` 仍由 `GoalJudgeSystem` 设置
- 得分、销毁、owner ball count 回收仍走现有 runtime 路径
- 加时绝杀仍由现有 overtime 逻辑决定
- 多球逐颗处理，已销毁球不再参与后续碰撞
- 四方向挡板统一使用 `Normal / Tangent / AxisPosition`，不写上下左右专用分支

如果同一 tick 内出现多个事件，唯一权威顺序是最早 TOI。只有在浮点并列时才使用 `Paddle > Goal > Wall` 兜底优先级。

## UGUI 兼容说明

未来 UI 迁移到 UGUI 时，本方案不需要调整。

推荐数据流保持：

```text
UGUI 按钮 / 摇杆 / 滑条
 -> InputService / Presenter
 -> runtime.ApplyInputFrame(...)
 -> GatebreakerMatchRuntime.Tick(...)
 -> HUD snapshot / runtime state
 -> UGUI 刷新显示
```

UGUI 只绑定输入、调参和显示，不参与碰撞、得分、球门、加时或销毁规则。

当前 OnGUI 发射按钮和反弹调参面板的小窗口问题作为原型 UI 临时债处理，后续随 UGUI 迁移单独解决。

## 验收测试计划

以下测试应直接映射到 `GatebreakerMatchRuntimeTests`：

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

建议验证命令：

```bash
bash tools/validation/run_gatebreaker_validation.sh
```

Unity/Tuanjie Editor 内还需跑 EditMode `GatebreakerMatchRuntimeTests`。如要跑 PlayMode smoke，需先关闭已打开的同项目 Editor 实例，再执行：

```bash
bash tools/validation/run_gatebreaker_playmode_smoke.sh
```

## Subagent 分工

如果启动 subagent，建议拆为 3 个：

- 实现 agent：负责 moving swept collision 实现，只改 HotUpdate runtime 或新增 HotUpdate 纯逻辑物理类。禁止改 `App.AOT`、`App.Shared`、UI 和配置表。
- 测试 agent：负责补齐 `GatebreakerMatchRuntimeTests`。禁止改 runtime 实现，除非先提出测试辅助需求。
- 审查验证 agent：只读审查 TOI 顺序、边界、加时、多球和测试覆盖，运行验证脚本。默认不直接改代码。

并行前必须冻结约束：玩法物理在 HotUpdate，表现层只做输入与刷新，Shared 不承载玩法实现。

## 完成情况

- 当前状态：进行中
- 进度说明：已完成方案文档落地、HotUpdate runtime 初版实现、回归测试补充和原型布局修复；待 Editor 内复验。
- 最近更新：2026-05-11，已完成方案文档落地、HotUpdate runtime 初版实现、回归测试补充和原型布局修复；待 Editor 内复验。
