# Gatebreaker Arena 核心玩法与规则设计说明 v0.3

## 一、文档定位

- 项目名称：`Gatebreaker Arena`
- 中文暂名：`弹门对决`
- 文档类型：长期玩法、数据与当前实现说明
- 当前基线日期：2026-05-30
- 当前工程基线：Unity + HybridCLR + YooAssets，正式业务分层为 `App.AOT / App.Shared / App.HotUpdate.GatebreakerArena`

本文不再作为早期头脑风暴稿使用，而是描述当前项目已经落地的玩法事实、配置事实和后续修改方向。若本文与代码冲突，以当前代码为准，并优先同步本文。

## 二、当前项目状态

当前项目已经从“规则设计”进入“本地可玩原型 + 配表接入 + LAN/Lockstep 骨架 + 场景表现接入”的阶段。

已落地内容：

- HotUpdate 玩法根：`Client/Assets/HotUpdateContent/Script/App.HotUpdate/GatebreakerArena`
- 本地原型入口：`GatebreakerPrototypeRunner`
- 对局运行时：`GatebreakerMatchRuntime`
- 发球资源：`ServeResourceSystem`
- 挡板反弹：`PaddleBounceCalculator` + `PaddleBounceTuning`
- 球门判定：`GoalJudgeSystem`
- 连续碰撞：`GatebreakerMatchRuntime` 内置 swept collision
- 配置加载：`GatebreakerConfigRuntimeLoader` 从 `Assets/HotUpdateContent/Config/gatebreaker_rules.bytes` 读取
- 场景资源：`Scene3v3.prefab`、`Baffle.prefab`、`Ball01.prefab`
- HUD 数据：`GatebreakerArenaHudPresenter`
- LAN 房间与帧同步骨架：`LanRoomService`、`LockstepSession`
- 测试覆盖：EditMode / PlayMode smoke / 配表 / LAN / HUD / 碰撞相关测试

仍属于后续推进内容：

- 正式 UGUI HUD 完整替代原型 UI 和 OnGUI 临时面板
- LAN 真机/多端稳定性验证
- HybridCLR Player DLL / metadata 构建链路完整验证
- YooAssets 远端补丁和 CDN 流程验证
- 规则数值的实机调优
- 组队模式、更多地图和特殊球种的正式产品化

## 三、产品目标

### 3.1 产品定位

Gatebreaker Arena 是一款以多人守护区域弹球攻防为核心的短局竞技游戏。玩家控制己方挡板守住自己的守护区域，同时通过发球、反弹和角度控制，把己方弹球送入其他玩家守护区域得分。

### 3.2 当前验证目标

- 30 秒内进入有效对抗。
- 玩家能理解“发球弹药、冷却、己方场上球上限、全场球上限”的关系。
- 多球时仍能分辨威胁来源和球归属。
- 低帧率、大 `deltaTime`、高速球下不出现明显穿板进门。
- 3 人 `Scene3v3` 场景能稳定完成本地原型对局。

## 四、分层边界

玩法规则必须留在 HotUpdate 层。

`App.AOT`：

- 负责宿主、DI、日志、Tick、持久化、平台桥接、YooAssets、HybridCLR、LAN socket 底座。
- 不写得分、发球、AI、碰撞、加时、结算规则。

`App.Shared`：

- 只放跨层接口、DTO 和稳定事件。
- 不放玩法 service 实现、Unity 场景逻辑、`ScriptableObject` 逻辑。

`App.HotUpdate.GatebreakerArena`：

- 承载 Match / Ball / Paddle / Zone / Serve / Mode / AI / UI Presenter / Network orchestration。
- 玩法逻辑优先写成纯 C#，方便 EditMode 测试。

UI 和 MonoBehaviour：

- 只做输入、表现绑定、状态刷新和资源实例化。
- 不承载得分、发球许可、AI 决策、碰撞或加时规则。

## 五、当前核心玩法规则

### 5.1 玩家对象

每名有效玩家拥有：

- `PlayerRuntimeState`
- `PaddleRuntimeState`
- `ZoneRuntimeState`
- `ServeResourceState`

当前运行时最多支持 4 个 active slot。默认地图 `MAP_ARENA_01` 使用 `Scene3v3` 资源，默认激活 3 名玩家：

| PlayerId | 默认颜色 | ScenePosition | BoundarySegmentIndex |
|---:|---|---|---:|
| 1 | Red | `Position01` | 5 |
| 2 | Blue | `Position03` | 1 |
| 3 | Green | `Position05` | 3 |

第 4 名玩家保留在配色和运行时能力中，但当前默认 `Scene3v3` 配置不作为默认激活玩家。

### 5.2 球归属

每颗球必须有明确归属：

- `OwnerPlayerId`
- `OwnerTeamId`
- `SpawnSourceType`
- `BallState`

当前规则：

- 初始球和主动发球都会归属于某个玩家。
- 普通碰撞不改变归属。
- 当前 MVP 不实现归属转移。

### 5.3 得分与守护区域

球进入守护区域后：

- `Ball.OwnerPlayerId != Zone.OwnerPlayerId`：进攻方得分，球销毁，归属玩家的 `OwnedBallsInField` 回收。
- `Ball.OwnerPlayerId == Zone.OwnerPlayerId`：进入 `GoalRebound`，不计分，不销毁，沿入射反方向回弹，回到场内后恢复 `Flying`。

得分不触发全场重开，其余球继续运行。

### 5.4 挡板反弹

挡板反弹由 `PaddleBounceCalculator` 统一计算：

- 命中点相对挡板中心的偏移影响出射角。
- 挡板切向速度会影响出射方向。
- `PaddleBounceTuning` 提供命中偏移、挡板速度、最小外向分量等调参值。
- 球速由 `BallRuleDefinition.MaxSpeed` clamp。

### 5.5 连续碰撞

当前对局运行时已经使用 moving swept collision：

- 球和挡板在同一 tick 时间轴上推进。
- 按最早 TOI 处理 `Paddle / Goal / Wall`。
- 浮点并列时优先级为 `Paddle > Goal > Wall`。
- `MaxCollisionIterations = 12`。
- `deltaTime <= 0` 时走静态碰撞和状态清理。

详细方案见同目录：

- [Gatebreaker Arena 连续碰撞方案 v0.1](Gatebreaker%20Arena%20连续碰撞方案_v0.1.md)

## 六、发球资源系统

### 6.1 当前字段

每名玩家维护：

- `CurrentServeAmmo`
- `MaxServeAmmo`
- `OwnedBallsInField`
- `MaxOwnedBallsInField`
- `ServeCooldownRemaining`
- `BaseServeCooldown`
- `LastBlockReason`

### 6.2 发球许可

当前 `ServeResourceSystem.EvaluateCanServe` 判定顺序为：

1. 玩家是否 disabled。
2. `CurrentServeAmmo > 0`。
3. `OwnedBallsInField < MaxOwnedBallsInField`。
4. `ballsInMatch < MaxBallsInMatch`。

注意：当前实现没有把 `ServeCooldownRemaining > 0` 作为直接阻塞条件。冷却系统的作用是补充 `CurrentServeAmmo`，玩家只要仍有弹药且未被球数限制，就可以继续发球。后续如要改成“每次发球都受冷却门控”，需要同步修改 `ServeResourceSystem`、HUD 文案和测试。

### 6.3 冷却恢复

当前恢复规则：

- 未满弹药时，`ServeCooldownRemaining` 递减。
- 倒计时结束后 `CurrentServeAmmo += 1`。
- 若仍未满，继续下一轮恢复。
- 若已满，冷却清零。

### 6.4 发球方向

当前规则：

- `AllowAimServe = true` 时，输入帧的 `AimDirection` 有效则按瞄准方向发球。
- 否则从玩家挡板法线方向发球。
- 本地原型会根据移动轴生成简化瞄准方向。

## 七、当前配置基线

正式配置资产：

- `Client/Assets/HotUpdateContent/Config/gatebreaker_rules.bytes`

加载入口：

- `GatebreakerConfigRuntimeLoader.RulesAssetLocation`
- 通过 `IAssetsRuntime` 加载，正式运行时不得绕开 YooAssets。

当前配置表：

- `DT_ModeRule`
- `DT_BallRule`
- `DT_AIRule`
- `DT_MapRule`
- `DT_PlayerColorRule`

### 7.1 DT_ModeRule 当前默认值

| ModeId | Time | InitialBalls | MaxBalls | BaseCooldown | InitialAmmo | MaxAmmo | MaxOwned | Overtime | AimServe | FinalPhase |
|---|---:|---:|---:|---:|---:|---:|---:|---|---|---:|
| `PVE_STANDARD` | 60 | 1 | 4 | 6.0 | 1 | 2 | 1 | SuddenDeath | true | 30 |
| `PVP_FFA` | 60 | 1 | 4 | 6.0 | 1 | 2 | 1 | SuddenDeath | true | 30 |
| `PVP_TEAM` | 60 | 1 | 4 | 6.5 | 1 | 2 | 1 | SuddenDeath | true | 30 |

说明：

- 当前工程默认局长是 60 秒，不是早期文档中的 150 到 180 秒。
- `PVP_TEAM` 已有 `ScoreRuleType.TeamScore` 配置，但完整组队产品化仍需继续推进。
- `FinalPhaseBallSpeedScale` 字段已在配置中存在；当前运行时代码已使用 `FinalPhaseCooldownScale` 影响发球恢复节奏，球速倍率是否正式进入 runtime 仍需单独任务确认。

### 7.2 DT_BallRule 当前默认值

| BallTypeId | InitialSpeed | MaxSpeed | SpeedGainOnPaddleHit | MinVerticalVelocity | DangerPromptThreshold | PrefabLocation |
|---|---:|---:|---:|---:|---:|---|
| `BALL_NORMAL` | 5.25 | 9.8 | 0.15 | 2.0 | 1.2 | `Assets/HotUpdateContent/Res/prefabs/Ball01.prefab` |

当前只正式启用 `BALL_NORMAL`。`Ball02/03/04.prefab` 可作为表现扩展资源，但不等于已经有正式特殊球规则。

### 7.3 DT_AIRule 当前默认值

| AILevelId | ReactionDelay | PredictError | ServeDecisionInterval | Aggression | Defense | MultiBall | AimAccuracy |
|---|---:|---:|---:|---:|---:|---:|---:|
| `AI_NORMAL` | 0.18 | 0.25 | 0.6 | 0.55 | 0.70 | 0.65 | 0.60 |

当前只正式启用 `AI_NORMAL`。

### 7.4 DT_MapRule 当前默认值

| MapId | MapName | DefaultPlayerCount | SupportedPlayerCount | ScenePrefab | MaxBallsModifier | MaxServeAmmo | MaxOwnedBalls | ServeRecharge |
|---|---|---:|---|---|---:|---:|---:|---:|
| `MAP_ARENA_01` | 标准四边场 | 3 | 2,3,4 | `Scene3v3.prefab` | 16 | 5 | 5 | 5.0 |

说明：

- `EffectiveMatchRule.MaxBallsInMatch = Mode.MaxBallsInMatch + Map.MaxBallsModifier`，因此当前默认有效全场球上限为 `4 + 16 = 20`。
- `EffectiveMatchRule.MaxServeAmmo` 和 `MaxOwnedBallsInField` 会优先使用地图覆盖值，因此当前默认有效值均为 5。
- `ServeRechargeSeconds = 5.0` 覆盖模式基础冷却。

## 八、对局状态

当前枚举以 `GatebreakerEnums.cs` 为准。

对局阶段：

- `Waiting`
- `Countdown`
- `Playing`
- `GoalPause`
- `Overtime`
- `Result`

当前运行时主要使用：

- `Waiting`：runtime 初始状态。
- `Playing`：`StartMatch` 后进入。
- `Overtime`：常规时间结束且最高分并列时进入。
- `Result`：产生胜者后进入。

球状态：

- `Flying`
- `GoalRebound`
- `ScoredOut`
- `Destroyed`

发球阻塞原因：

- `None`
- `PlayerDisabled`
- `NoAmmo`
- `OwnedBallLimit`
- `MatchBallLimit`

## 九、模式说明

### 9.1 PVE_STANDARD

当前本地原型默认模式之一。默认玩家 1 为本地玩家，其余有效 slot 可作为 AI。

当前重点：

- 验证核心手感。
- 验证 AI 挡板移动和发球策略。
- 验证 HUD 数据和危险提示。

### 9.2 PVP_FFA

当前已有模式配置和计分规则，适合作为 LAN/Lockstep 的主要验证目标。

当前重点：

- 输入帧一致性。
- checksum 检测。
- 多端对局启动和中止。

### 9.3 PVP_TEAM

当前已有 `TeamScore` 配置，但完整队伍匹配、队伍 UI、队友状态提示仍是后续内容。

## 十、UI/HUD 当前状态

当前已有 `GatebreakerArenaHudPresenter.BuildSnapshot`，可提供：

- `Phase`
- `RemainingTime`
- `PlayerScores`
- `LocalPlayerId`
- `HasWinner`
- `WinnerPlayerId`
- `CurrentServeAmmo`
- `MaxServeAmmo`
- `OwnedBallsInField`
- `MaxOwnedBallsInField`
- `ServeCooldownRemaining`
- `ServeBlockReason`
- `IsOvertime`
- `HasDanger`

后续 UGUI 的规则：

- 只读取 snapshot 或 presenter 输出。
- 只转发输入和显示状态。
- 不直接调用或重写发球、得分、碰撞、AI、加时规则。

P0 HUD：

- 比分。
- 剩余时间。
- 本地玩家发球弹药。
- 冷却剩余。
- 己方场上球数。
- 发球阻塞原因。
- 危险提示。
- 加时和胜者提示。

当前原型 UI 和部分 OnGUI 面板属于临时调试表现，后续应由正式 UGUI 方案替代。

## 十一、资源与表现

正式资源路径由配置表提供：

- 场景：`Assets/HotUpdateContent/Res/prefabs/Scene3v3.prefab`
- 挡板：`Assets/HotUpdateContent/Res/prefabs/Baffle.prefab`
- 默认球：`Assets/HotUpdateContent/Res/prefabs/Ball01.prefab`
- BGM：`Assets/HotUpdateContent/Res/sounds/gameBGM.mp3`
- UI/场景纹理：`Assets/HotUpdateContent/Res/textures`

正式运行时资源加载要求：

- 通过 `IAssetsRuntime` / YooAssets。
- 不新增 `Resources.Load` 或 `Resources.LoadAsync`。
- Editor 本地分支可以由 `YooAssetsRuntime` 使用 `AssetDatabase` 支持调试。

## 十二、联网与同步

当前联网方向是 LAN 房间 + Lockstep。

多端创建、发现、加入、Ready、Start、Playing、退出/断线和日志取证的验收步骤，以 [Gatebreaker Arena LAN 联机测试方案 v0.1](Gatebreaker%20Arena%20LAN联机测试方案_v0.1.md) 为准。

已落地模块：

- `LanRoomService`：房间、发现、加入、准备、loading ack、playing 状态。
- `LockstepSession`：30 FPS、3 帧 input delay、输入包、帧包、checksum、等待和 abort。
- `GatebreakerLockstepInputConverter`：输入量化。
- `GatebreakerEnvelopeCodec` / `GatebreakerNetworkDtos`：网络消息 DTO 和编码。
- AOT 层 `LanTransport`：socket transport，只处理字节帧和事件，不理解玩法规则。

后续重点：

- 多端实机联调。
- 弱网和丢包策略验证。
- checksum 差异定位工具。
- 房间 UI 正式化。
- WeChat MiniGame 平台网络能力确认。

## 十三、测试与验证

常用验证入口：

```bash
bash tools/validation/run_gatebreaker_validation.sh
```

PlayMode smoke：

```bash
bash tools/validation/run_gatebreaker_playmode_smoke.sh
```

当前重点测试域：

- `GatebreakerMatchRuntimeTests`：对局、球门、连续碰撞、场景边界。
- `ServeResourceSystemTests`：发球弹药和回收。
- `GoalJudgeSystemTests`：己方门回弹与敌方门得分。
- `GatebreakerModeCatalogTests` / `GatebreakerConfigRuntimeLoaderTests`：配置和有效规则。
- `GatebreakerLockstepRuntimeTests` / `GatebreakerLanApiReadinessTests`：同步和 LAN 准备度。
- `GatebreakerHudPresenterTests` / `GatebreakerSceneBindingServiceTests`：HUD snapshot 与场景 UI 绑定。

LAN 多端验收、同机限制、真机测试步骤和 `game.log` 取证要求，统一维护在 [Gatebreaker Arena LAN 联机测试方案 v0.1](Gatebreaker%20Arena%20LAN联机测试方案_v0.1.md)。

## 十四、后续修改方向

### P0：让当前原型稳定可测

- 正式 UGUI HUD 接入 `GatebreakerArenaHudPresenter`。
- 统一本地原型和 LAN 对局的启动/重开/退出流程。
- 继续补齐 Scene3v3 表现和碰撞边界一致性测试。
- LAN 多端跑通后补充 checklist。

### P1：规则和数值调优

- 明确发球是否需要“冷却门控”。当前实现是“弹药恢复型”，不是“每次发球强制等冷却”。
- 明确默认有效球上限是否继续使用 `20`，还是回收至更接近早期 MVP 的 `4~5`。
- 验证 `FinalPhaseBallSpeedScale` 是否进入 runtime。
- 评估默认局长是否从 60 秒延长到 120 或 150 秒。

### P2：产品化扩展

- 完整组队模式。
- 更多地图和特殊球种。
- 归属转移规则。
- 结算高光和回放。
- 正式音频、特效和移动端 HUD。

## 十五、当前与旧文档差异摘要

旧文档中以下内容已调整为当前项目描述：

- 默认局长从 `150~180s` 更新为当前配置的 `60s`。
- 默认玩家规模从 `3~6` 收敛为当前运行时最多 4 slot，默认 `Scene3v3` 为 3 人。
- 发球从“固定朝场地中央”更新为当前 `AllowAimServe = true` 的瞄准发球。
- 配表从“建议字段”更新为当前实际 `gatebreaker_rules.bytes` 字段。
- 增加 `DT_PlayerColorRule`、资源 prefab 路径、`DefaultPlayerCount`、`PlayerSideBindings` 等已落地字段。
- 球速从早期 `7.5 / 14.0` 更新为当前 `5.25 / 9.8`。
- 当前有效全场球上限、弹药上限、己方场上球上限受地图覆盖影响，默认有效值已不同于早期 MVP 建议。
- 连续碰撞已经落地，不再是待实现方案。
- 联网描述从“建议服务端或主机权威”更新为当前 LAN + Lockstep 骨架。

## 十六、维护规则

- 改玩法规则时，同步更新本文。
- 改 `RuleDefinitions.cs`、`GatebreakerConfigRuntimeLoader.cs` 或 `gatebreaker_rules.bytes` 时，同步更新第七章。
- 改 UI snapshot 时，同步更新第十章。
- 改 LAN / Lockstep 时，同步更新第十二章。
- 改连续碰撞或 TOI 顺序时，同步更新连续碰撞方案文档。
