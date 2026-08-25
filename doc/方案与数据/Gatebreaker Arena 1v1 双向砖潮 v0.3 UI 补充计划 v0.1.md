# Gatebreaker Arena 1v1 双向砖潮 v0.3 UI 补充计划 v0.1

> 配套《配置对接检查 v0.1》《落地迁移计划 v0.1》。本文只谈"界面层缺什么、怎么建"，不重复数值。

## 0. 决策记录（本版拍板）

| 项 | 决定 | 影响 |
|---|---|---|
| 英雄选择形态 | **替换 V1**：只呈现 4 相位英雄，弃用芯片卡组/路径/签名芯片 | `HeroDeckSelectionPresenter` 不能复用，须新建或重建 |
| 科技获取时机 | **战前预设**：进对局前配好科技 loadout，局内只展示不抽取 | 取消"局内科技抽取面板"，新增"战前科技配装界面" |

> 设计一致性说明：科技战前预设 ≠ 弱化"局内成长影响结果"。成长张力来自 **Φ 相位能量**（局内操作驱动、P1~P5 阈值 0/30/50/70/100），科技是"build"，Φ 是"局内兑现"。成长失败致负仍成立。

## 1. 现有 UI 库存（V1 范式，全部不可直接复用）

| Presenter | 职责 | v0.3 兼容性 |
|---|---|---|
| `HeroDeckSelectionPresenter` | 战前卡组：英雄→路径→签名芯片→芯片卡组；硬编码 3 V1 英雄，非 V1 直接 `Fail` | ❌ 必须替换 |
| `GatebreakerArenaHudPresenter` | 局内 HUD：比分/发球/球数/英雄 chips·paths·statuses | ⚠️ 需扩展快照，缺 Φ/科技/压力 |
| `GatebreakerArenaInputPresenter` | 输入（挡板控制等） | ✅ 不改 |
| `GatebreakerArenaSceneBindingService` | Presenter↔GameObject 绑定 | ⚠️ 需加新 Presenter 的绑定 |

## 2. 需新增的 UI 界面（修正后优先级）

| 优先级 | 界面 | 时机 | 展示内容 | 数据来源 | 对应 Phase |
|---|---|---|---|---|---|
| P0 | **相位英雄选择** | 战前 | 4 相位英雄（蜃影/脉冲/裂痕/折光）+ 成长定位 | `AllPhaseHeroes` | Phase 1 |
| P0 | **科技配装界面** | 战前 | 从英雄可用池选科技装入槽位；实时净偏移预算余量(D=+30)；益/害配对预览 | `AllPhaseTechs` / `DT_PhaseMeta.NetOffsetBudget` | Phase 1(UI)+Phase 3(结算逻辑) |
| P1 | **Φ 相位能量条** | 局内 | 当前 Φ、P1~P5 阈值、累积速率 | 运行时 Φ 状态 | Phase 2 |
| P2 | **科技装载条** | 局内 | 预设科技列表 + 当前道具价值矩阵修正 | 战前 loadout + 运行时结算 | Phase 3 |
| P3 | **相位道具反馈** | 局内·掉落 | 穿透/分裂/阻尼/加速/加宽/磁吸 增益及其对价值影响 | `AllPhaseItems` | Phase 3 |
| P4 | **压力指示** | 局内 | 当前压力档 C0~C5 + 破阵计数反馈（双层压力） | `AllPhaseCurves` | Phase 4 |
| P5 | **结算强化** | 战后 | 货币(胜12/负4)、到达相位、成长失败致负归因 | 比赛结果 + 运行时 Φ | 贯穿 |

> 已移除：局内"科技抽取面板"（因科技改战前预设）。

## 3. 各界面规格要点

### P0 相位英雄选择（替换 `HeroDeckSelectionPresenter`）
- 读取 `catalog.AllPhaseHeroes`（4 项），不再读 `AllHeroes`（V1）。
- 每项展示：`DisplayName`、成长定位（从 `PhaseLevels` 的 activeAbility 提炼）、P1~P5 概述。
- 选中后联动"科技配装界面"（按英雄过滤可用科技池）。
- 输出：`PhaseMatchLoadout { HeroId, TechIds[] }`（5 槽各 1 科技），替代旧 `V1MatchLoadout`。

### P0 科技配装界面
- 从选中英雄的可用科技池（`AllPhaseTechs` 按 HeroId 过滤）选入槽位。
- 实时净偏移计：`Σ NetOffset` 不得超过 `DT_PhaseMeta.NetOffsetBudget`(=30)，超限禁止确认。
- 每张卡显示益/害配对（Effect 的 paired 益/害），让玩家理解"价值矩阵被改了哪两头"。
- 掉率偏移（`DropOffsetCap`=10）作为可选维度展示，不强制。

### P1 Φ 相位能量条
- 新快照字段：`PhiCurrent`、`PhiToReach[]`(0/30/50/70/100)、`PhiRate`。
- 视觉：单条分段（P1~P5 刻度），随操作填充；`PhiPerSecondCap`(=6) 作软上限提示。
- **命名隔离**：HUD 现有 `Phase` = 比赛阶段(Countdown/Overtime)，此处称 `GrowthPhase`，避免撞车。

### P2 科技装载条 / P3 相位道具反馈
- 装载条：静态展示战前 loadout 的科技及其当前生效的价值修正（局内不再变动，除非 Phase 3 结算逻辑动态调整）。
- 道具反馈：掉落时短暂高亮对应 `PhaseItem` 的 Effect 摘要。

### P4 压力指示
- 读取 `AllPhaseCurves` 当前档（按 `ResolveBrickCompositionWeights` 改读 `DT_PhaseCurve` 后）。
- 双层：时间比（t/ScissorDiffTargetSeconds=40）+ 破阵计数（BreakCounterThreshold=20）反馈。

### P5 结算强化
- 现有 HUD 只有比分。新增：货币结算（胜12/负4）、到达 GrowthPhase、若负且 Φ 未达阈值则标注"成长不足致负"。

## 4. 架构雷区（建之前必知）

1. `HeroDeckSelectionPresenter` 对非 V1 英雄直接 `Fail("not available in V1 catalog")` → P0 必须新建 `PhaseHeroSelectPresenter`，不能在其上打补丁。
2. HUD 的 `HeroHudSnapshot` 是 V1 形（chips/paths/statuses），不含 Φ/科技/压力 → 新增 `PhaseHudSnapshot` 或在 `GatebreakerHudSnapshot` 加 `GrowthPhase`/`Phi`/`ActiveTechs`/`Pressure` 字段（不污染旧字段）。
3. HUD 已有 `Phase`(比赛阶段) 与 v0.3「相位」(Φ 成长) **命名撞车** → 显式区分 `MatchPhase` vs `GrowthPhase`。
4. **运行时耦合警告**：P0 界面产出的 `PhaseMatchLoadout` 当前无运行时消费方（`BrickDuelSessionController` 仍吃 V1 loadout）。界面可先建可测，但"进了对局真正用上"要等 Phase 2~4 运行时改写。建议 P0 只交付 Presenter+Snapshot+测试，绑定留到运行时就绪。

## 5. 建议构建序列

1. **P0（纯 C#，可单测）**：`PhaseHeroSelectPresenter` + `PhaseTechLoadoutPresenter` + `PhaseMatchLoadout` + `PhaseHudSnapshot` 扩展 + 各自测试。不碰 Unity prefab。
2. **P1**：Φ 能量条（需 Phase 2 运行时 Φ 状态就绪后接数据）。
3. **P2/P3**：科技装载条 + 道具反馈（需 Phase 3 结算逻辑）。
4. **P4**：压力指示（需 Phase 4 `ResolveBrickCompositionWeights` 改读 `DT_PhaseCurve`）。
5. **P5**：结算强化。
6. **绑定**：`SceneBindingService` 接新 Presenter + prefab/GameObject（在引擎内完成）。

## 6. 版本

| 版本 | 日期 | 内容 |
|---|---|---|
| v0.1 | 2026-08-21 | 据用户拍板（替换 V1 / 科技战前预设）修正 UI 清单：取消局内抽取面板、加战前科技配装；列出现有 4 Presenter 的 V1 不兼容、各界面规格、架构雷区、构建序列 |
