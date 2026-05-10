# 《弹门对决 / Gate Breaker》开发实施稿 GDD v0.3

## 一、文档信息

- 项目代号：`Gate Breaker`
- 文档类型：`开发实施稿 GDD`
- 版本：`v0.3`
- 目标：用于指导程序、美术、UI、关卡、测试进入原型开发与首轮联调。

## 二、产品目标

### 2.1 产品定位

一款以`多人守护区域弹球攻防`为核心的短局竞技游戏。每名玩家拥有自己的守护区域，以及一块只能在指定范围内移动的挡板。玩家通过主动发射弹球、挡板反弹和墙体折射，把己方弹球送入其他玩家守护区域得分。

### 2.2 核心目标

- 规则必须一眼能懂
- 30 秒内进入有效对抗
- 2 到 3 分钟内出现至少一次明显节奏升级
- 多球混战时仍能识别威胁来源
- 主动发球要有决策价值，而非纯冷却按钮

### 2.3 原型验证重点

- 守护区域防守与反弹是否足够爽
- 发球资源系统是否清晰
- 己方球进入己方守护区域后的反弹是否真的提升策略深度
- 多人乱斗是否兼顾混乱与可读性

## 三、核心玩法总览

### 3.1 玩家控制

每位玩家控制：

- `1 个挡板`
- `1 个守护区域`
- `1 套发球资源`

### 3.2 核心行为

- 左右移动挡板
- 接球并改变反弹角度
- 在满足条件时主动发球
- 处理己方球进入己方守护区域后的反弹
- 压制对手并保护己方守护区域

### 3.3 基础胜负

- 己方球进入其他玩家守护区域：己方得分，该球消失
- 己方球进入己方守护区域：反弹回场，不得分，不消失
- 常规时间结束：得分最高的玩家或队伍获胜
- 若最高分相同：进入加时赛

## 四、局内规则实施定义

### 4.1 球归属

球必须有明确归属字段，用于判定得分、消失和守护区域反弹逻辑。

建议字段：

- `OwnerPlayerId`
- `OwnerTeamId`
- `SpawnSourceType`
- `BallState`

归属规则建议：

- 玩家主动发球生成的球，归属于发球玩家
- 球在普通碰撞后默认不改变归属
- 若后续要设计“归属转移”，作为扩展规则单独实现，不进入 MVP

### 4.2 守护区域判定

弹球进入守护区域时，按球归属和守护区域归属判定：

- `Ball.OwnerPlayerId != Zone.OwnerPlayerId`：有效得分，弹球离场消失
- `Ball.OwnerPlayerId == Zone.OwnerPlayerId`：守护区域反弹，弹球不消失

守护区域反弹效果：

- 保持球存在
- 重新赋予出射方向
- 播放己方守护区域回弹反馈

### 4.3 得分后的球处理

MVP 建议采用：

- 该颗得分球体`离场销毁`
- 其余场上球继续存在
- 被销毁球体不参与后续碰撞
- 由发球系统继续补足场面压力，不强制全场重开

这样节奏会更连贯，不会像传统球类游戏那样频繁中断。

### 4.4 挡板反弹

挡板反弹建议使用分区算法：

- 中心区域：小角度、稳定回弹
- 中间区域：中角度
- 边缘区域：大角度

实现建议：

- 按击中点相对于挡板中心的归一化偏移计算水平分量
- 垂直分量保持最小阈值，避免低角度无限横飞
- 可叠加球当前速度做微调，不建议完全真实物理

### 4.5 加时赛规则

常规时间结束时，若最高分存在并列，则进入加时赛。

MVP 推荐采用`先得分胜利`规则：

- 只有常规时间结束时并列最高分的玩家或队伍具备加时获胜资格
- 加时赛开始后，具备资格的玩家或队伍率先获得 1 分即立刻获胜
- 若存在非资格玩家仍留在场内，可继续参与干扰和防守，但不能直接赢得比赛
- 若加时赛设置时间上限且仍未分出胜负，则按加时阶段得分、常规时间得分、最近一次得分时间依次判定

## 五、发球系统实施方案

### 5.0 开局可发射弹球

游戏开始时，每名玩家拥有 `N` 个可发射弹球。该数值由 `InitialServeAmmo` 配置，表示玩家开局可主动发射的弹球资源数量。

该资源与 `InitialBallsInMatch` 区分：

- `InitialServeAmmo`：每名玩家开局手中可发射的弹球数量
- `InitialBallsInMatch`：对局开始时系统已经投放在场内的初始弹球数量

### 5.1 发球前置条件

玩家主动发球前必须满足：

- 发球冷却完成
- 当前可发球数量 > 0
- 场内己方弹球数量 < 场内己方弹球数量上限
- 全场总球数 < 全场总球数上限
- 当前玩家未处于禁止发球状态

### 5.2 发球流程

1. 玩家按下发球键
2. 系统检查发球条件
3. 条件成立则生成球体
4. 扣除 `当前可发球数量`
5. 增加 `场内己方弹球数量`
6. 启动发球冷却
7. 播放发球表现

### 5.3 发球冷却恢复

建议规则：

- 每轮补充计时结束时，若 `当前可发球数量 < 可发球数量上限`，则 `+1`
- 若已满，则本次恢复不累计
- 补充计时可在资源未满时持续循环，也可仅在消耗后重新开始，二选一

MVP 推荐：

- `消耗后才开始下一轮补充计时`

理由：

- 玩家更容易理解
- 节奏更可控
- 不容易堆满资源后瞬间爆发过强

### 5.4 发球方向

MVP 可采用两种方案之一：

1. `固定扇区发球`
2. `挡板朝向映射发球`

推荐 MVP：

- 固定朝场地中央
- 附带左右小幅偏移

这样更简单，便于先验证核心乐趣。

## 六、资源系统实施定义

### 6.1 资源字段

每个玩家需要至少维护：

- `CurrentServeAmmo`
- `MaxServeAmmo`
- `OwnedBallsInField`
- `MaxOwnedBallsInField`
- `ServeCooldownRemaining`
- `BaseServeCooldown`

### 6.2 资源更新事件

这些事件会引发资源变化：

- 对局开始
- 玩家主动发球
- 发球资源补充完成
- 球体离场
- 模式 Buff / Debuff 生效
- 道具生效
- 特殊关卡事件触发

### 6.3 资源显示要求

UI 必须能直接显示：

- 当前可发球数量
- 发球冷却剩余时间
- 场内己方球数 / 上限

## 七、球数量与场面控制实施方案

### 7.1 场面控制目标

- 避免场上无球导致空窗
- 避免球量过多导致纯随机
- 支持不同模式与地图的节奏差异

### 7.2 关键球量参数

- `InitialBallsInMatch`
- `MaxBallsInMatch`
- `InitialServeAmmo`
- `MaxServeAmmo`
- `MaxOwnedBallsInField`

其中 `InitialServeAmmo` 控制每名玩家开局可发射弹球数量，`InitialBallsInMatch` 控制场上初始弹球数量，两者需要独立配置。

### 7.3 默认建议值

原型推荐：

- `InitialBallsInMatch = 1`
- `MaxBallsInMatch = 4`
- `InitialServeAmmo = 1`
- `MaxServeAmmo = 2`
- `MaxOwnedBallsInField = 1`

### 7.4 球量控制优先级

当多个限制同时存在时，按以下优先级判断是否允许发球：

1. 玩家状态限制
2. 发球冷却
3. 当前可发球数量
4. 场内己方弹球数量上限
5. 全场总球数上限

## 八、模式实施配置

### 8.1 PVE

默认参数建议：

- 玩家数：`1 + 2~5 AI`
- 局长：`120~180 秒`
- 球量上限：`3~4`
- AI 冷却可按难度修正

### 8.2 PVP 乱斗

默认参数建议：

- 玩家数：`3~6`
- 局长：`150 秒`
- 球量上限：`4~5`
- 计分方式：个人得分制

### 8.3 PVP 组队乱斗

默认参数建议：

- 队伍：`2v2 / 3v3`
- 局长：`150~180 秒`
- 球量上限：`4`
- 计分方式：队伍共享得分

## 九、核心数值表

下面这组值用于首轮原型，不代表最终平衡。

| 参数 | 建议值 | 说明 |
|---|---:|---|
| `MatchDuration` | 150s | 标准局时长 |
| `BaseServeCooldown` | 6.0s | 基础发球冷却 |
| `InitialServeAmmo` | 1 | 初始可发球数量 |
| `MaxServeAmmo` | 2 | 可发球数量上限 |
| `MaxOwnedBallsInField` | 1 | 场内己方球数量上限 |
| `InitialBallsInMatch` | 1 | 开局球数 |
| `MaxBallsInMatch` | 4 | 全场总球数上限 |
| `BallInitialSpeed` | 7.5 | 球初速度 |
| `BallSpeedCap` | 14.0 | 球最大速度 |
| `BallSpeedGainOnPaddleHit` | 0.15 | 每次挡板击中速度增量 |
| `GoalPauseTime` | 0.4s | 得分反馈停顿 |
| `DangerPromptThreshold` | 1.2s | 球接近守护区域的危险提示阈值 |
| `EnableOvertime` | true | 是否启用加时赛 |
| `OvertimeRuleType` | SuddenDeath | 加时赛规则，首版推荐先得分胜利 |
| `OvertimeDuration` | 60s | 加时赛时间上限 |
| `OvertimeWinScore` | 1 | 加时阶段获胜所需得分 |

## 十、数据表设计

### 10.1 模式配置表 `DT_ModeRule`

字段建议：

- `ModeId`
- `ModeName`
- `MatchDuration`
- `InitialBallsInMatch`
- `MaxBallsInMatch`
- `BaseServeCooldown`
- `InitialServeAmmo`
- `MaxServeAmmo`
- `MaxOwnedBallsInField`
- `ScoreRuleType`
- `EnableOvertime`
- `OvertimeRuleType`
- `OvertimeDuration`
- `OvertimeEligibleOnly`
- `OvertimeWinScore`
- `AllowAimServe`
- `GoalPauseTime`

### 10.2 球配置表 `DT_BallRule`

字段建议：

- `BallTypeId`
- `BallTypeName`
- `InitialSpeed`
- `MaxSpeed`
- `PaddleBounceFactor`
- `WallBounceFactor`
- `GoalReboundFactor`
- `TrailStyle`
- `ColorTag`

### 10.3 AI 配置表 `DT_AIRule`

字段建议：

- `AILevelId`
- `ReactionDelay`
- `PredictError`
- `ServeDecisionInterval`
- `AggressionWeight`
- `DefenseWeight`
- `MultiBallPriority`

### 10.4 地图配置表 `DT_MapRule`

字段建议：

- `MapId`
- `SupportedPlayerCount`
- `SpawnLayoutType`
- `HasObstacle`
- `InitialBallsModifier`
- `MaxBallsModifier`
- `ServeCooldownModifier`

## 十一、系统模块拆分

### 11.1 核心程序模块

- `MatchFlowSystem`
- `PlayerInputSystem`
- `PaddleController`
- `BallSimulationSystem`
- `GoalJudgeSystem`
- `ServeResourceSystem`
- `ScoreSystem`
- `ModeRuleSystem`
- `BuffAndModifierSystem`
- `ReplayEventLogSystem` 可选

### 11.2 模块职责

`MatchFlowSystem`

- 管理开局、进行中、结束、结算

`BallSimulationSystem`

- 管理球移动、碰撞、速度变化、离场

`GoalJudgeSystem`

- 处理守护区域判定、己方守护区域回弹逻辑

`ServeResourceSystem`

- 管理冷却、弹药、球量上限

`ScoreSystem`

- 更新个人/队伍分数
- 推送比分变化事件

## 十二、局内状态机

### 12.1 对局状态

- `Waiting`
- `Countdown`
- `Playing`
- `GoalPause`
- `Overtime`
- `Result`

### 12.2 球状态

- `Spawned`
- `Flying`
- `GoalRebound`
- `ScoredOut`
- `Destroyed`

### 12.3 玩家发球状态

- `Ready`
- `CoolingDown`
- `BlockedByAmmo`
- `BlockedByOwnedBallLimit`
- `BlockedByMatchBallLimit`

## 十三、流程图说明

### 13.1 发球流程

1. 玩家输入发球
2. 检查冷却
3. 检查当前可发球数量
4. 检查己方场内球上限
5. 检查全场球量上限
6. 生成球
7. 更新资源
8. 进入冷却

### 13.2 得分判定流程

1. 球进入守护区域
2. 比较球归属和守护区域归属
3. 若归属不同：记分，销毁球，进入短暂停顿
4. 若归属相同：执行守护区域反弹，球返回场内

### 13.3 常规时间结束流程

1. 常规时间归零
2. 统计当前最高分
3. 若最高分唯一：进入结算
4. 若最高分并列且 `EnableOvertime = true`：进入加时赛
5. 若最高分并列且未启用加时：按模式配置的平局规则结算

### 13.4 加时赛流程

1. 记录常规时间结束时的并列最高分玩家或队伍
2. 进入 `Overtime` 状态
3. 继续对局，直到具备资格的玩家或队伍得分
4. 若 `OvertimeRuleType = SuddenDeath`，该玩家或队伍立刻获胜
5. 若加时达到时间上限仍未分出胜负，则按模式配置的加时兜底规则结算

## 十四、UI 实施清单

### 14.1 HUD 必备元素

- 比分区
- 剩余时间
- 发球冷却条
- 当前可发球数量
- 场内己方球数量
- 危险提示
- 个人颜色标识

### 14.2 UI 优先级

P0：

- 分数
- 时间
- 冷却
- 发球数量
- 危险提示
- 加时状态提示

P1：

- 己方球计数
- 队伍状态
- 模式提示

P2：

- 连续得分反馈
- MVP 风格播报
- 结算高光回放入口

## 十五、美术资产清单

### 15.1 场景资产

- 基础竞技场 1 套
- 守护区域标识
- 边界墙体
- 背景氛围层

### 15.2 角色/功能资产

- 挡板 1 套基础款
- 球体 1 套基础款
- 己方球/敌方球区分样式
- 守护区域命中效果
- 发球特效
- 危险预警特效

### 15.3 UI 资产

- 冷却条
- 资源图标
- 比分组件
- 倒计时组件
- 结算面板

## 十六、音频实施清单

P0：

- 挡板击球音
- 墙反弹音
- 得分音
- 发球音
- 冷却恢复提示音
- 倒计时音

P1：

- 连得分音效
- 决胜阶段音乐增强
- 己方守护区域回弹专属音效

## 十七、联网与同步要求

### 17.1 MVP 联网目标

- 支持实时多人 PVP
- 保证得分判定一致
- 球体轨迹尽量一致
- 玩家挡板操作反馈及时

### 17.2 同步重点

- 玩家输入
- 球体位置/速度
- 球归属
- 分数变化
- 发球资源变化
- 对局状态切换

### 17.3 建议

- 核心判定以服务端或主机权威为主
- 客户端做平滑显示和预测
- 得分、发球、球销毁必须是强同步事件

## 十八、测试验收标准

### 18.1 功能验收

- 能正常开局、计时、结算
- 玩家可正常移动挡板
- 发球条件校验正确
- 己方球进入己方守护区域时一定反弹
- 敌方球进入己方守护区域时一定记分
- 球销毁后资源统计正确恢复

### 18.2 手感验收

- 挡板击球方向可预判
- 球速提升不会突然失控
- 多球时仍能识别主要威胁
- 发球冷却与资源反馈清楚

### 18.3 平衡验收

- 开局 20 秒内必然进入有效交锋
- 单人无法长期靠堆球碾压全场
- 2 到 3 分钟内通常能拉开分差但仍保留逆转空间

## 十九、开发里程碑建议

### 阶段 1：核心原型

- 单球移动
- 挡板反弹
- 守护区域判定
- 基础得分
- 倒计时结束

### 阶段 2：资源系统

- 发球冷却
- 当前可发球数量
- 己方球数量限制
- 全场球量限制
- HUD 接入

### 阶段 3：模式与 AI

- PVE
- 乱斗
- 组队
- AI 行为

### 阶段 4：联机与调优

- 实时同步
- 表现优化
- 平衡调参
- 首轮测试

## 二十、待定问题

- 发球是否支持精确瞄准
- 挡板是否允许轻微加速/蓄力击球
- 归属是否永不转移
- 是否加入特殊球种
- 是否在最后 20 秒加入全局加速规则

## 二十一、下一步建议

最适合立刻推进的是这三份配套文档：

1. `数值配置表 v0.1`
2. `UI 原型需求稿`
3. `程序任务拆分清单`

建议先做 `UI 原型需求稿`，因为这套玩法对冷却、弹药、球归属和危险提示的可读性要求很高，UI 先定，很多系统实现会更顺。

---

# 附录 A：数据表字段与数值样例表 v0.1

## A.1 文档目的

这份文档用于把 GDD 中的核心玩法参数落成可执行配置，方便程序接表、策划调数值、测试验证规则。

目标是先建立`一套最小可用的数据表结构`，支持：

- 模式差异配置
- 球体行为配置
- AI 难度配置
- 地图节奏配置
- 后续活动规则扩展

## A.2 数据表总览

首版建议至少建立 4 张核心表：

1. `DT_ModeRule`
2. `DT_BallRule`
3. `DT_AIRule`
4. `DT_MapRule`

如果后续加入 Buff、道具、特殊事件，再补：

- `DT_BuffRule`
- `DT_ItemRule`
- `DT_EventRule`

## A.3 DT_ModeRule

用途：
定义不同模式下的基础规则和节奏参数。

建议字段如下：

| 字段名 | 类型 | 示例 | 说明 |
|---|---|---:|---|
| `ModeId` | String | `PVP_FFA` | 模式唯一 ID |
| `ModeName` | String | `PVP乱斗` | 模式名 |
| `MatchDuration` | Int | `150` | 单局时长，单位秒 |
| `InitialBallsInMatch` | Int | `1` | 开局全场初始球数量 |
| `MaxBallsInMatch` | Int | `4` | 全场总球数上限 |
| `BaseServeCooldown` | Float | `6.0` | 基础发球冷却 |
| `InitialServeAmmo` | Int | `1` | 初始可发球数量 |
| `MaxServeAmmo` | Int | `2` | 可发球数量上限 |
| `MaxOwnedBallsInField` | Int | `1` | 场内己方弹球数量上限 |
| `GoalPauseTime` | Float | `0.4` | 得分后的短暂停顿时间 |
| `ScoreRuleType` | Enum | `AddScore` | 计分方式 |
| `EnableOvertime` | Bool | `true` | 是否启用加时赛 |
| `OvertimeRuleType` | Enum | `SuddenDeath` | 加时规则，首版推荐先得分胜利 |
| `OvertimeDuration` | Int | `60` | 加时赛时间上限，单位秒 |
| `OvertimeEligibleOnly` | Bool | `true` | 是否只有常规时间并列最高分玩家或队伍具备获胜资格 |
| `OvertimeWinScore` | Int | `1` | 加时阶段获胜所需得分 |
| `AllowAimServe` | Bool | `false` | 是否允许手动瞄准发球 |
| `FinalPhaseStartTime` | Int | `30` | 最后多少秒进入决胜阶段 |
| `FinalPhaseBallSpeedScale` | Float | `1.1` | 决胜阶段球速倍率 |
| `FinalPhaseCooldownScale` | Float | `0.9` | 决胜阶段冷却倍率 |
| `EnableAITeams` | Bool | `false` | 是否启用队伍 AI 逻辑 |

## A.4 DT_ModeRule 数值样例

| ModeId | ModeName | MatchDuration | InitialBallsInMatch | MaxBallsInMatch | BaseServeCooldown | InitialServeAmmo | MaxServeAmmo | MaxOwnedBallsInField | GoalPauseTime | ScoreRuleType | EnableOvertime | OvertimeRuleType | OvertimeDuration | OvertimeEligibleOnly | OvertimeWinScore | AllowAimServe | FinalPhaseStartTime | FinalPhaseBallSpeedScale | FinalPhaseCooldownScale |
|---|---|---:|---:|---:|---:|---:|---:|---:|---:|---|---|---|---:|---|---:|---|---:|---:|---:|
| `PVE_STANDARD` | `PVE标准` | 150 | 1 | 4 | 6.0 | 1 | 2 | 1 | 0.4 | `AddScore` | true | `SuddenDeath` | 60 | true | 1 | false | 30 | 1.05 | 0.95 |
| `PVP_FFA` | `PVP乱斗` | 150 | 1 | 4 | 6.0 | 1 | 2 | 1 | 0.4 | `AddScore` | true | `SuddenDeath` | 60 | true | 1 | false | 30 | 1.10 | 0.90 |
| `PVP_TEAM` | `PVP组队乱斗` | 180 | 1 | 4 | 6.5 | 1 | 2 | 1 | 0.4 | `TeamScore` | true | `SuddenDeath` | 60 | true | 1 | false | 30 | 1.10 | 0.92 |

## A.5 DT_BallRule

用途：
定义球的基础行为和表现参数。

建议字段如下：

| 字段名 | 类型 | 示例 | 说明 |
|---|---|---:|---|
| `BallTypeId` | String | `BALL_NORMAL` | 球类型 ID |
| `BallTypeName` | String | `普通球` | 球类型名 |
| `InitialSpeed` | Float | `7.5` | 初速度 |
| `MaxSpeed` | Float | `14.0` | 最大速度 |
| `PaddleBounceFactor` | Float | `1.0` | 挡板反弹速度系数 |
| `WallBounceFactor` | Float | `1.0` | 墙体反弹速度系数 |
| `GoalReboundFactor` | Float | `1.0` | 己方守护区域反弹速度系数 |
| `SpeedGainOnPaddleHit` | Float | `0.15` | 每次击中挡板的速度增益 |
| `MinVerticalVelocity` | Float | `2.0` | 最小纵向速度，避免横向死飞 |
| `DangerPromptThreshold` | Float | `1.2` | 接近守护区域的危险提示阈值 |
| `TrailStyle` | String | `DefaultBlue` | 拖尾样式 |
| `ColorTag` | String | `Neutral` | 表现颜色标签 |

## A.6 DT_BallRule 数值样例

| BallTypeId | BallTypeName | InitialSpeed | MaxSpeed | PaddleBounceFactor | WallBounceFactor | GoalReboundFactor | SpeedGainOnPaddleHit | MinVerticalVelocity | DangerPromptThreshold | TrailStyle | ColorTag |
|---|---|---:|---:|---:|---:|---:|---:|---:|---:|---|---|
| `BALL_NORMAL` | `普通球` | 7.5 | 14.0 | 1.0 | 1.0 | 1.0 | 0.15 | 2.0 | 1.2 | `Default` | `Neutral` |
| `BALL_FAST` | `高速球` | 9.0 | 16.0 | 1.05 | 1.0 | 1.0 | 0.20 | 2.2 | 1.0 | `FastTrail` | `Red` |

MVP 建议只启用 `BALL_NORMAL`。

## A.7 DT_AIRule

用途：
控制 AI 难度和行为偏好。

建议字段如下：

| 字段名 | 类型 | 示例 | 说明 |
|---|---|---:|---|
| `AILevelId` | String | `AI_NORMAL` | AI 难度 ID |
| `AILevelName` | String | `普通` | 难度名 |
| `ReactionDelay` | Float | `0.18` | 反应延迟 |
| `PredictError` | Float | `0.25` | 预测误差 |
| `ServeDecisionInterval` | Float | `0.6` | 发球决策轮询间隔 |
| `AggressionWeight` | Float | `0.55` | 进攻倾向 |
| `DefenseWeight` | Float | `0.70` | 防守倾向 |
| `MultiBallPriority` | Float | `0.65` | 多球处理优先级 |
| `AimAccuracy` | Float | `0.60` | 控角精度 |
| `TargetSwitchFrequency` | Float | `0.50` | 目标切换频率 |

## A.8 DT_AIRule 数值样例

| AILevelId | AILevelName | ReactionDelay | PredictError | ServeDecisionInterval | AggressionWeight | DefenseWeight | MultiBallPriority | AimAccuracy | TargetSwitchFrequency |
|---|---|---:|---:|---:|---:|---:|---:|---:|---:|
| `AI_EASY` | `简单` | 0.28 | 0.40 | 0.90 | 0.35 | 0.65 | 0.45 | 0.40 | 0.30 |
| `AI_NORMAL` | `普通` | 0.18 | 0.25 | 0.60 | 0.55 | 0.70 | 0.65 | 0.60 | 0.50 |
| `AI_HARD` | `困难` | 0.10 | 0.12 | 0.40 | 0.70 | 0.80 | 0.80 | 0.78 | 0.65 |

## A.9 DT_MapRule

用途：
控制地图对节奏和玩法的修正。

建议字段如下：

| 字段名 | 类型 | 示例 | 说明 |
|---|---|---:|---|
| `MapId` | String | `MAP_ARENA_01` | 地图 ID |
| `MapName` | String | `标准四边场` | 地图名 |
| `SupportedPlayerCount` | String | `2,3,4` | 支持人数 |
| `SpawnLayoutType` | Enum | `FourSide` | 出生布局 |
| `HasObstacle` | Bool | `false` | 是否有障碍物 |
| `InitialBallsModifier` | Int | `0` | 对初始球数修正 |
| `MaxBallsModifier` | Int | `0` | 对全场总球上限修正 |
| `ServeCooldownModifier` | Float | `0.0` | 对发球冷却修正 |
| `BallSpeedModifier` | Float | `0.0` | 对球速修正 |
| `GoalSizeModifier` | Float | `0.0` | 对门大小修正 |

## A.10 DT_MapRule 数值样例

| MapId | MapName | SupportedPlayerCount | SpawnLayoutType | HasObstacle | InitialBallsModifier | MaxBallsModifier | ServeCooldownModifier | BallSpeedModifier | GoalSizeModifier |
|---|---|---|---|---|---:|---:|---:|---:|---:|
| `MAP_ARENA_01` | `标准四边场` | `2,3,4` | `FourSide` | false | 0 | 0 | 0.0 | 0.0 | 0.0 |
| `MAP_RING_01` | `环形乱斗场` | `4,5,6` | `Ring` | false | 0 | 1 | 0.0 | 0.2 | 0.0 |
| `MAP_TEAM_01` | `双阵地对抗场` | `4,6` | `DualFront` | false | 0 | 0 | 0.2 | 0.0 | 0.1 |

## A.11 推荐枚举定义

建议先统一几组基础枚举。

`ScoreRuleType`

- `AddScore`
- `TeamScore`
- `LoseLife`
- `Mixed`

`OvertimeRuleType`

- `SuddenDeath`
- `TimedScore`
- `Disabled`

`SpawnLayoutType`

- `FourSide`
- `Ring`
- `DualFront`

`BallState`

- `Flying`
- `GoalRebound`
- `ScoredOut`
- `Destroyed`

## A.12 运行时公式建议

为了方便程序接入，首版规则可以先明确成下面这几条。

### 12.1 发球许可判定

```text
CanServe =
ServeCooldownRemaining <= 0
AND CurrentServeAmmo > 0
AND OwnedBallsInField < MaxOwnedBallsInField
AND BallsInMatch < MaxBallsInMatch
AND PlayerState != Disabled
```

### 12.2 冷却恢复

```text
OnServeCooldownFinished:
if CurrentServeAmmo < MaxServeAmmo:
    CurrentServeAmmo += 1
```

### 12.3 发球消耗

```text
OnServe:
CurrentServeAmmo -= 1
OwnedBallsInField += 1
ServeCooldownRemaining = BaseServeCooldown * CooldownScale
```

### 12.4 球离场

```text
OnOwnedBallRemoved:
OwnedBallsInField -= 1
```

要点：
`OwnedBallsInField` 必须只统计“当前还在场上的、归属于该玩家的球”。

## A.13 参数联动建议

为了避免后期调数值时互相打架，建议按下面的优先关系理解参数：

- `BaseServeCooldown` 控制发球频率
- `InitialServeAmmo` 控制开局进攻权
- `MaxServeAmmo` 控制资源储备能力
- `MaxOwnedBallsInField` 控制单人铺场能力
- `MaxBallsInMatch` 控制整体混乱度
- `BallInitialSpeed` 与 `BallSpeedCap` 控制操作压力

一个简单判断：

- 想让对局更快：优先调 `BaseServeCooldown`
- 想让对局更热闹：优先调 `MaxBallsInMatch`
- 想限制单人滚雪球：优先调 `MaxOwnedBallsInField`
- 想增强开局冲突：优先调 `InitialServeAmmo` 或 `InitialBallsInMatch`

## A.14 首轮调参建议

建议先跑 3 套模板做对比测试。

### 模板 A：稳态竞技

- `BaseServeCooldown = 6.5`
- `MaxBallsInMatch = 3`
- `MaxOwnedBallsInField = 1`

特点：

- 更清楚
- 更偏策略
- 更适合验证核心手感

### 模板 B：标准原型

- `BaseServeCooldown = 6.0`
- `MaxBallsInMatch = 4`
- `MaxOwnedBallsInField = 1`

特点：

- 平衡
- 易评估
- 适合作为默认首测版本

### 模板 C：高压乱斗

- `BaseServeCooldown = 5.0`
- `MaxBallsInMatch = 5`
- `MaxOwnedBallsInField = 2`

特点：

- 更热闹
- 更混乱
- 适合压力测试可读性上限

## A.15 程序接入要求

程序实现时建议注意：

- 所有模式参数优先读 `DT_ModeRule`
- 地图修正通过 `DT_MapRule` 二次叠加
- 球本体参数只从 `DT_BallRule` 读取
- AI 决策只读 `DT_AIRule`
- 不要把这些数值写死在逻辑里

推荐最终生效值计算方式：

```text
FinalInitialBallsInMatch = Mode.InitialBallsInMatch + Map.InitialBallsModifier
FinalMaxBallsInMatch = Mode.MaxBallsInMatch + Map.MaxBallsModifier
FinalServeCooldown = Mode.BaseServeCooldown + Map.ServeCooldownModifier
```

## A.16 测试用例建议

策划和 QA 首轮至少验证这些点：

- `InitialServeAmmo = 0/1/2` 时开局发球行为是否正确
- `MaxServeAmmo = 1/2/3` 时冷却恢复是否溢出正确
- `MaxOwnedBallsInField = 1` 时是否会阻止连续发球
- `MaxBallsInMatch = 3/4/5` 时全场球量是否正确封顶
- 己方球进入己方守护区域后是否始终反弹，不会误记分
- 敌方球进入己方守护区域后是否始终计分并离场
- 得分后 `OwnedBallsInField` 是否正确回收
- 常规时间最高分并列时是否正确进入加时赛
- 加时赛中具备资格的玩家或队伍得分后是否立即结算胜负

---

# 附录 B：UI 原型需求稿 v0.1

## B.1 文档目的

这份文档用于明确首版原型的 UI/HUD 需求，确保玩家在高速对局中能快速读懂以下关键信息：

- 我当前的得分情况
- 我还能不能发球
- 我手里还有几次发球资源
- 我场内还有几颗己方球
- 哪些球正在威胁己方守护区域
- 对局还剩多少时间

本稿以`开发可落地`为目标，优先保证信息清晰、反馈及时、状态统一。

## B.2 UI 设计目标

### 2.1 核心目标

- 规则信息必须一眼能懂
- 高速多球场景下不能丢失关键状态
- 资源状态显示必须和玩法规则强绑定
- HUD 尽量轻量，不遮挡场地与球路
- 不同人数模式下都要保持可读性

### 2.2 UI 优先级原则

P0：

- 不显示就没法玩

P1：

- 不显示也能玩，但体验明显下降

P2：

- 增强表现和观感，不影响基础可玩性

## B.3 HUD 总体结构

首版 HUD 建议分为 4 个区域：

1. `顶部中央信息区`
2. `玩家本地资源区`
3. `场内即时危险提示区`
4. `局后结算区`

### 3.1 顶部中央信息区

用途：

- 显示对局剩余时间
- 显示当前比分
- 显示模式名或阶段提示

建议内容：

- 倒计时
- 各玩家或各队分数
- 决胜阶段提示

### 3.2 玩家本地资源区

用途：

- 显示本地玩家的发球与资源状态

建议内容：

- 发球冷却条
- 当前可发球数量
- 可发球数量上限
- 场内己方球数量 / 上限

### 3.3 场内即时危险提示区

用途：

- 在守护区域危险来临时提供快速预警

建议内容：

- 己方守护区域危险闪烁
- 敌方危险球高亮
- 逼近守护区域的轨迹提示或预警箭头

### 3.4 局后结算区

用途：

- 展示胜负结果、个人数据和再次开始入口

建议内容：

- 胜负结果
- 分数排行
- 关键数据
- 再来一局按钮

## B.4 P0 HUD 清单

以下为 MVP 必做 UI。

### 4.1 剩余时间

位置建议：

- 屏幕顶部中央

显示格式：

- `02:30`
- 倒计时低于 30 秒时变色
- 倒计时低于 10 秒时加入跳动或闪烁

作用：

- 明确局内节奏
- 让玩家判断何时保守、何时强攻

### 4.2 比分显示

位置建议：

- 顶部中央时间两侧
- 或顶部横排显示所有玩家/队伍得分

显示要求：

- 每个玩家或队伍有固定颜色
- 分数变化时要有短促反馈
- 组队模式下显示队伍总分，不强调个人分数

PVP 乱斗示例：

- `蓝 3 | 红 2 | 黄 1 | 绿 4`

组队示例：

- `蓝队 5 : 红队 4`

### 4.3 发球冷却显示

位置建议：

- 本地玩家挡板附近
- 或屏幕底部中央偏下的本地资源区

显示形式建议：

- 进度条
- 环形充能
- 数字倒计时可选

状态要求：

- 冷却中：显示剩余进度
- 可发球：高亮提示
- 被其他条件阻止发球时：仍显示冷却完成，但提示无法发球原因

### 4.4 当前可发球数量

位置建议：

- 冷却条旁边
- 与发球按钮或发球图标绑定

显示形式建议：

- 数字：`1`
- 或球形图标数量：`●○`
- 建议同时显示上限，例如 `1/2`

作用：

- 告诉玩家“现在手里还有没有发球资源”

### 4.5 场内己方球数量

位置建议：

- 资源区内，与发球资源并列

显示形式建议：

- `场内己方球：1/1`
- 或简化为 `在场：1/1`

作用：

- 让玩家理解“为什么现在不能继续发球”

### 4.6 守护区域危险提示

位置建议：

- 直接作用于己方守护区域
- 不建议只放在屏幕边角

表现形式建议：

- 守护区域描边闪红
- 接近守护区域的危险球高亮
- 短时警示箭头
- 危险音效同步触发

触发条件建议：

- 敌方球进入守护区域危险范围
- 预计在阈值时间内撞向守护区域

## B.5 P1 HUD 清单

### 5.1 己方球与敌方球识别

目标：
让玩家快速识别“这颗球是不是自己的”。

建议方式：

- 己方球使用本方颜色尾迹
- 敌方球使用对方颜色描边
- 中立初始球用白色或灰色

要求：

- 归属变化若未来实现，表现必须同步更新
- 多人模式下仍需易区分

### 5.2 发球被阻止原因提示

当玩家按下发球键却无法发球时，建议显示简短原因：

可选文案：

- `冷却中`
- `无可发球资源`
- `己方场内球已满`
- `全场球数已满`

位置建议：

- 本地资源区上方短暂浮字
- 或发球按钮附近提示

### 5.3 决胜阶段提示

触发时机：

- 进入最后 30 秒
- 或进入特殊加速阶段

表现建议：

- 顶部出现 `决胜阶段`
- HUD 边框轻微变色
- 倒计时强化闪烁
- 背景音乐进入高压版本

## B.6 P2 HUD 清单

### 6.1 连续得分提示

示例：

- `连进 2 球`
- `连续压制`

### 6.2 焦点目标提示

适用于乱斗模式，可选：

- 对当前领先玩家加“领先”标签
- 对最后得分者做短暂高亮

### 6.3 回放入口

结算页可显示：

- `精彩回放`
- `本局关键得分`

MVP 可以不做。

## B.7 多人模式 UI 适配

### 7.1 PVE

重点信息：

- 玩家分数
- 剩余时间
- AI 数量或难度提示
- 本地资源状态

PVE 中可以弱化其他 AI 的复杂 HUD，仅保留必要分数信息。

### 7.2 PVP 乱斗

重点信息：

- 多人分数排行
- 本地资源
- 守护区域危险提示
- 球归属区分

乱斗模式要避免顶部信息过密。
建议：

- 4 人以内直接横排
- 5 到 6 人使用紧凑排行条

### 7.3 PVP 组队乱斗

重点信息：

- 队伍比分
- 队友颜色统一
- 本地资源
- 队友守护区域情况可弱提示

建议：

- 顶部只显示队伍比分
- 屏幕边缘可轻量显示队友受压状态

## B.8 UI 布局建议

### 8.1 PC 版布局

顶部：

- 中间倒计时
- 两侧比分

底部或本地角色附近：

- 发球冷却
- 当前可发球数量
- 场内己方球数量

场内：

- 守护区域危险提示
- 球归属颜色/尾迹

### 8.2 移动版布局

顶部：

- 倒计时 + 比分

底部：

- 左右滑动操作区
- 发球按钮
- 冷却显示覆盖在发球按钮上
- 当前可发球数量显示在按钮旁

移动端注意：

- 按钮和资源显示不能遮挡守护区域危险范围
- 文案尽量短
- 危险提示优先使用图形而不是长文本

## B.9 UI 状态定义

为便于程序和 UI 联调，建议统一状态机。

### 9.1 发球按钮状态

- `Ready`
- `CoolingDown`
- `NoAmmo`
- `OwnedBallLimitReached`
- `MatchBallLimitReached`
- `Disabled`

### 9.2 守护区域危险状态

- `Safe`
- `Warning`
- `Critical`

触发建议：

- `Warning`：有敌方球接近
- `Critical`：敌方球极近且高概率命中守护区域

### 9.3 比分变化状态

- `Idle`
- `Scored`
- `LeadChanged`
- `FinalPhase`

## B.10 HUD 文案建议

首版建议统一使用短文案，避免读屏负担。

可用文案：

- `可发球`
- `冷却中`
- `球数已满`
- `资源不足`
- `危险`
- `决胜阶段`
- `加时压制` 可选
- `你得分了`
- `对手得分`

如果要更偏竞技感，可进一步压缩成图标+颜色，不依赖完整文字。

## B.11 美术表现要求

### 11.1 色彩原则

- 每个玩家必须有固定主色
- 己方信息优先高亮
- 危险提示统一红/橙区间
- 中立信息使用白/灰

### 11.2 动效原则

- 发球可用短促放射动画
- 冷却完成要有轻反馈，不要过吵
- 得分时比分跳动要明显
- 决胜阶段整体 UI 节奏略加强，但不能遮挡球路

### 11.3 可读性原则

- 不使用过细字体
- 多人对战中优先图标和颜色，不堆大段文字
- 任何重要状态变化应在 0.2 到 0.5 秒内被玩家感知

## B.12 程序对接需求

UI 需要程序提供以下实时数据：

- `CurrentScore`
- `TeamScore`
- `MatchRemainingTime`
- `ServeCooldownRemaining`
- `ServeCooldownNormalized`
- `CurrentServeAmmo`
- `MaxServeAmmo`
- `OwnedBallsInField`
- `MaxOwnedBallsInField`
- `CanServe`
- `ServeBlockReason`
- `GoalDangerLevel`
- `BallOwnerTag`
- `IsFinalPhase`

建议通过统一 HUD ViewModel 或数据绑定层提供，避免 UI 直接拼底层逻辑。

## B.13 验收标准

UI 首轮验收至少满足：

- 玩家能在 3 秒内理解比分与剩余时间位置
- 玩家能在 1 局内理解发球冷却与发球数量的关系
- 玩家按不出球时，能立即知道原因
- 危险提示能有效帮助玩家做出回防反应
- 多球场景下，玩家能大致分辨己方球和敌方球
- 最后 30 秒节奏强化能被明显感知

## B.14 低保真原型建议

如果先做线框稿，建议先画这 4 张：

1. `PVP 乱斗对局 HUD`
2. `PVP 组队对局 HUD`
3. `移动端 HUD`
4. `结算界面`

优先顺序：

- 先定 PVP 乱斗 HUD
- 再推组队 HUD
- 再适配移动端

## 完成情况

- 当前状态：进行中
- 进度说明：已完成本地可玩原型的 HotUpdate 规则骨架、配置导出链路和首批验证入口；Unity 场景表现层与手工 HUD 接入仍待推进
- 最近更新：2026-05-10，已完成本地可玩原型的 HotUpdate 规则骨架、配置导出链路和首批验证入口；Unity 场景表现层与手工 HUD 接入仍待推进
