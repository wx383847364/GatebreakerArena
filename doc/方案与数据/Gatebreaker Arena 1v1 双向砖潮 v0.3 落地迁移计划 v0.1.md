# Gatebreaker Arena 1v1 双向砖潮 v0.3 落地迁移计划 v0.1

> **状态**：实施跟踪文档，配套《相位成长与英雄科技设计 v0.3》。
>
> **日期**：2026-08-21
>
> **核心判断**：本计划不是"在现有代码上加功能"，而是**把引擎中已存在的旧英雄/芯片层（霜后/机械工程师/光辉圣骑士 + 共振芯片 + 英雄路径等级）整体替换为 v0.3 的相位成长 Φ + 科技 + 四英雄体系**。砖潮、确定性帧同步、球/挡板模拟等重型基础设施可复用，不重建。

---

## 0. 范围与前提

### 0.1 必须替换 vs 可复用

| 类别 | 判断 | 理由 |
|---|---|---|
| 英雄机制层（Hero/ + Chip/） | **整体替换** | 旧范式与 v0.3 完全不可映射 |
| 砖潮推进 / 碰撞 / 球模拟 | **复用** | 重型基础设施，与设计无关 |
| 确定性帧同步 + checksum | **复用 + 扩展** | Φ 成长须接入，科技不接入 |
| 道具掉落框架（BrickDuelItem*） | **复用 + 重定向** | 掉落链路在，需改道具集与结算 |
| 压比曲线配置 | **复用 + 补段** | 基础时序压力已有，缺配比表与对手反馈腿 |
| 存档（FilePersistenceProvider） | **复用 + 扩展** | 加货币与已解锁科技字段 |

### 0.2 设计基线锁定项

落地前必须先冻结《设计 v0.3》中所有 `[PLACEHOLDER]`（见 §6）。不冻结则无法开始数值实现，只能先做结构骨架。

---

## 1. 现状 vs 目标 系统映射

| 系统 | 引擎现状（旧范式） | v0.3 目标 | 关系 |
|---|---|---|---|
| 英雄定义 | `GatebreakerModeCatalog` 加载 3 旧英雄 | 蜃影 / 脉冲 / 裂痕 / 折光 | **Phase 0 追加 DT_Phase\* 表；Phase 1 替换 DT_Hero/DT_HeroPath/DT_SignatureChip/DT_UniversalChip 并改写 ValidateV1Catalog**（见《配置对接检查 v0.1》） |
| 成长驱动 | 英雄路径等级 V1/M1/M2 | Φ 相位成长 P1~P5（操作驱动） | 替换 |
| 局外养成 | 共振芯片 Strike/Guard/Flow/Chaos + Deck | 科技（5 槽 × 3，道具价值矩阵） | 替换 |
| 道具集 | `BrickDuelItemRules` 现有 8 原型 | 6 道具（裂穿/分形/缓滞/疾风/广域/磁吸） | 替换定义集 |
| 压力 | 基础时序 + 配比 | 时序配比 + 破阵反馈腿 | 复用 + 补腿 |
| 公平锚 | 镜像对称 | 镜像对称（继承） | 保留 |
| 帧同步 | `GatebreakerMatchRuntime` + `Checksum` | Φ 接入，科技不接入 | 扩展 |

---

## 2. 文件级替换清单

> 图例：🔄 替换重写 ｜ 🔀 复用重定向 ｜ 🆕 新建 ｜ ✅ 保留不动

### 2.1 Hero/（整体替换）

| 文件 | 动作 | 说明 |
|---|---|---|
| `Hero/HeroRuntimeSystem.cs` | 🔄 | 旧英雄事件路由（OwnPaddleHit 等）→ 4 新英雄相位机制；Φ 累积与自动升阶在此驱动 |
| `Hero/HeroCombatState.cs` | 🔄 | 删除英雄路径等级状态，改为相位状态（PhaseLevel / PhaseEnergy Φ / 各英雄核心资源：连击/节拍/裂口/折射标记） |

### 2.2 Chip/（替换为科技管线）

| 文件 | 动作 | 说明 |
|---|---|---|
| `Chip/ResonanceAwakener.cs` | 🔄 | 共振觉醒 → 科技开局注入（EquippedTechIds → 道具价值矩阵） |
| `Chip/DeckValidator.cs` | 🔄 | 卡组校验 → 科技解锁校验（已解锁才可装备，净偏移守恒校验） |
| `Chip/V1MatchLoadout.cs` | 🔄 | V1 配装 → 科技配装数据结构 |
| `Chip/ChipRuleInjector.cs` | 🔀 | 注入点保留，注入内容改为科技效果而非芯片规则 |

### 2.3 Mode/（数据层替换）

| 文件 | 动作 | 说明 |
|---|---|---|
| `Mode/GatebreakerModeCatalog.cs` | 🔄 | 英雄/科技/道具 定义加载入口，**Phase 0 追加** 5 个 `DT_Phase*` 表 + 5 个 catalog 字典 + getter（**保留 V1 三英雄/芯片表不动**）；**Phase 1 才替换**旧 HeroDefinition 集 |
| `Mode/RuleDefinitions.cs` | 🔄 | 新增 5 主类 `PhaseHero`/`PhaseTech`/`PhaseItem`/`PhaseCurve`/`PhaseMeta` + 子结构（对应 `DT_PhaseHero`/`DT_PhaseTech`/`DT_PhaseItem`/`DT_PhaseCurve`/`DT_PhaseMeta`） |
| `Mode/GatebreakerConfigRuntimeLoader.cs` | 🔀 | **显式加** 5 个 `ReadPhase*` 方法 + `ParseJson` 接入（非"兼容扩展"，是硬编码加 `ReadArray` 调用，见对接检查 §4.3） |

### 2.4 Match/（接入确定性）

| 文件 | 动作 | 说明 |
|---|---|---|
| `Match/GatebreakerMatchRuntime.cs` | 🔀 | Φ 成长事件纳入帧推进 |
| `Match/GatebreakerMatchChecksum.cs` | 🔀 | Φ / 相位 / 核心资源 纳入校验（科技不纳入） |
| `Match/GatebreakerMatchStartConfig.cs` | 🔄 | 开局注入 EquippedTechIds（替换 DeckChipIds/Loadout） |
| `Match/PlayerRuntimeState.cs` | 🔄 | 玩家状态字段：PhaseLevel / PhaseEnergy / EquippedTechIds；删旧路径等级字段 |
| `Match/GatebreakerFrameInput.cs` | ✅ | 无需改（输入语义不变） |
| `Match/ScoreboardSnapshot.cs` `ScoreSystem.cs` `ArenaGeometry.cs` | ✅ | 保留 |

### 2.5 BrickDuel/（道具 + 破阵反馈）

| 文件 | 动作 | 说明 |
|---|---|---|
| `BrickDuel/BrickDuelItemRules.cs` | 🔄 | 6 新道具定义集；结算时乘科技价值矩阵 + 掉率偏移 |
| `BrickDuel/BrickDuelTypes.cs` | 🔄 | 道具枚举 / 效果类型改写 |
| `BrickDuel/BrickDuelRuntime.cs` | 🔀 | 接入破阵反馈（清砖计数 → 对手注入升级砖） |
| `BrickDuel/BrickDuelAiController.cs` `BrickDuelTacticalAiController.cs` | 🔄 | 旧英雄 AI → 相位 + 科技决策 |
| `BrickDuel/BrickDuelSessionController.cs` | 🔀 | 会话级接入科技初始化 |
| `BrickDuel/BrickDuelCollisionSolver.cs` `BrickDuelCollisionOverlayGeometry.cs` `BrickDuelVisualAssetService.cs` | ✅/🔀 | 碰撞保留；视觉资源需换（见 §5） |

### 2.6 其他目录

| 文件 | 动作 | 说明 |
|---|---|---|
| `AI/GatebreakerAiService.cs` | 🔄 | 英雄 AI 服务改调 4 新英雄 |
| `UI/GatebreakerArenaHudPresenter.cs` | 🔄 | 加相位指示 + 危险道具预警（红/紫负色 + 0.5s 落地预警） |
| `UI/HeroDeckSelectionPresenter.cs` | 🔄 | 英雄/科技配装选择 UI（替换芯片配装 UI） |
| `UI/GatebreakerArenaInputPresenter.cs` 等其余 UI | 🔀 | 按需小幅适配 |
| `Zone/ZoneRuntimeState.cs` `GoalJudgeSystem.cs` | ✅ | 砖潮核心逻辑保留 |
| `Serve/` `Paddle/` `Core/` `Network/` `Ball/` | ✅ | 保留（除 §4 标注的扩展点） |
| `Assets/Config/json/gatebreaker_rules.json` | 🔄 | 重写英雄/科技/道具/压力配置段 |
| `Assets/HotUpdateContent/Res/textures/items/` | 🔄 | 替换/新增 6 道具图标 + 4 英雄视觉 + 预警表现 |

---

## 3. 数据 Schema 设计（落地地基）

基于《设计 v0.3》§8，需新建/改写以下数据结构（落点：`Mode/RuleDefinitions.cs` 或 新建 `Mode/PhaseTechDefinitions.cs`）：

```text
DT_PhaseHero
  HeroId
  CoreItemId            // 核心道具（蜃影=分形 / 脉冲=疾风 / 裂痕=裂穿 / 折光=广域）
  PhaseLevels[5]        // P1~P5：质变/量变 + 效果
  TechSlots[5]          // 每槽 1 默认 + 2~3 进阶科技

DT_PhaseTech
  TechId
  HeroId
  SlotIndex
  Effects[]             // { ItemId, EffectOffset } 增强/削弱 道具效果
  DropOffsets[]         // { ItemId, DropOffset } 可选掉率偏移
  IsDefault

DT_PhaseItem           // 6 道具
  ItemId  ValueWeight  BaseDropRate  BoundaryRules

DT_PhaseCurve         // C0~C5 配比 + 破阵反馈阈值

DT_PhaseMeta          // 单例：货币产出 / 解锁单价 / 净偏移预算 / 掉率偏移封顶 / Φ每秒上限 / 剪刀差目标秒数
```

配置落点：`gatebreaker_rules.json` 新增 `DT_PhaseHero` / `DT_PhaseTech` / `DT_PhaseItem` / `DT_PhaseCurve` / `DT_PhaseMeta` 五段（与引擎 `DT_BrickDuelRule` 风格一致；**Phase 0 追加，保留 V1 三英雄/芯片表不动**，替换旧表推迟到 Phase 1）。

> 说明：更细的 JSON 字段草稿属于"选项 A"交付物（数据 Schema 草稿），本计划不展开，按计划 Phase 0 输出。

---

## 4. 分阶段实施路线与验收

### Phase 0 — 决策冻结与 Schema 草稿
- **目标**：冻结所有 `[PLACEHOLDER]`；产出 5 张 `DT_Phase*` JSON 草稿（英雄/科技/道具/曲线/元信息）。
- **动作**：
  1. tuning 工作坊拍板 §6 全部数值；
  2. 补齐四英雄完整科技清单（当前仅蜃影完稿）；
  3. 产出 `gatebreaker_rules.json` 五段 `DT_Phase*` Schema 草稿，并按《配置对接检查 v0.1》落地：
     - 草稿 JSON **独立不可加载**：缺 `Version` 字段、`DT_Phase*` 表加载器不读 → 须（1）草稿根加 `"Version": 3`；（2）将 5 表**追加**进现有 `gatebreaker_rules.json`（保留 V1 三英雄/芯片表）；（3）代码侧补 5 个 C# 定义类 + 5 个 catalog 字典/getter + 5 个 `ReadPhase*` 方法（见对接检查 §4）。**替换 V1 表推迟到 Phase 1**。
- **验收**：① 四个英雄 × 5 槽 × 3 科技清单齐备；② 所有数值非 `[PLACEHOLDER]`；③ 60 科技净偏移校验通过（同英雄 = D ±5%）。

### Phase 1 — 数据层替换
- **目标**：`gatebreaker_rules.json` + catalog 加载新英雄/科技/道具/压力；存档加货币与解锁字段。
- **验收**：① 启动加载无旧英雄残留；② 四英雄定义可被运行时读取；③ 存档读写货币/解锁字段正常。

### Phase 2 — Φ 相位成长系统
- **目标**：Φ 来源（接道具+2 / 击碎?砖+2 / 红砖+0.5 / 黄砖+1 / 绿砖 0，每秒上限+6），P1~P5 自动升阶，无时限无失谐。
- **关键约束**：Φ / 相位 / 核心资源 **必须进确定性 sim + checksum**。
- **验收**：① 确定性回放双端一致；② Φ 不随时间自动增长；③ 挂机玩家 Φ 慢涨、清砖效率被砖潮压死（剪刀差涌现）；④ checksum 覆盖 Φ 状态。

### Phase 3 — 科技应用
- **目标**：道具结算乘科技价值矩阵（增强/削弱）+ 掉率偏移；Chip/ 管线改为科技解锁校验 + 开局注入。
- **验收**：① 选「蜂拥」玩家分形掉率与效果同步提升、缓滞被削；② 未解锁科技无法装备；③ 科技仅开局注入，不占帧同步带宽。

### Phase 4 — 破阵反馈（对手压力腿）
- **目标**：清砖 15 块 → 向对手下一逻辑行注入 1 块升级砖（提前 1s 预警，不直接伤害）。
- **验收**：① 对手压力曲线出现非单调剪刀差；② 注入可预测、不瞬移、不破坏镜像公平。

### Phase 5 — 新英雄机制 & AI
- **目标**：`HeroRuntimeSystem` 事件路由指向 4 新英雄；AI 服务按相位 + 科技决策。
- **验收**：① 关闭 UI 后四英雄 L4 录像仍可被正确辨认（辨识度）；② AI 会做科技相关的躲/接决策。

### Phase 6 — HUD & 资源
- **目标**：相位指示 + 危险道具预警（红/紫负色 + 0.5s 落地预警）；6 道具图标、4 英雄视觉、预警表现。
- **验收**：① 玩家能一眼识别"该躲的道具"；② 预警提前量足够做出躲避操作。

### Phase 7 — 货币/解锁闭环 & Playtest
- **目标**：战后产出货币、解锁科技；跑首轮验收。
- **验收（核心两条）**：
  1. **剪刀差验收**：操作差 vs 操作好 的死亡时间差显著（目标 >40s）——验证"成长决定胜负"；
  2. **躲/接行为验证**：选不同科技的玩家，对同一随机道具出现可观测的接/躲分化——验证"科技在局内展开"。

---

## 5. 资源清单

| 资源 | 现状 | 需求 |
|---|---|---|
| 道具图标 | `Res/textures/items/` 旧 8 原型 | 新增/替换 6 道具（裂穿/分形/缓滞/疾风/广域/磁吸） |
| 英雄视觉 | 按旧英雄制作 | 4 新英雄视觉（按维度区分：数量/速度/穿透/弹道） |
| 危险道具预警 | 无 | 红/紫负色 + 0.5s 落地预警表现（"躲道具"成立前提） |

---

## 6. 已拍板数值（Tuning Workshop 结论 · 2026-08-21）

> 所有数值以 **Phase 7「剪刀差验收」（操作差 vs 操作好死亡时间差 >40s）** 为唯一仲裁。首轮偏保守，实测偏离按「验证钩子」回调。

| # | 项 | 拍板值 | rationale 摘要 | 验证钩子 |
|---|---|---|---|---|
| 1 | Φ 每阶需求量 | P1→P2 **30** / P2→P3 **50** / P3→P4 **70** / P4→P5 **100**（合计 250） | 操作好者≈125s 达 P5（C4），挂机者 C4 前被碾碎；每秒上限 +6 为硬上限 | 死亡时间差 >40s；若差距过小→下调总量，过大→上调 |
| 2 | 道具价值权重 | 裂穿 3 / 分形 3 / 缓滞 3 / 疾风 2 / 广域 2 / 磁吸 1 | 已驱动 §4 全部 60 科技净偏移 = +30，整数配平自洽 | 若某英雄科技实测超模→优先查权重 |
| 3 | 道具基础掉率 | 裂穿 15 / 分形 15 / 缓滞 15 / 疾风 20 / 广域 20 / 磁吸 15（合计 100） | 强道具略稀，保持经济；磁吸弱但掉率不低（接取价值有限） | 单道具掉率不应 <10% 或 >25% |
| 4 | 压力 C0~C5 配比 | 见下表（绿/黄/红/?） | 绿随阶段收缩、硬砖增长，?砖恒定保证道具经济稳定 | 剪刀差 + C5 是否真正压人 |
| 5 | 科技幅度 | 预算 D = **+30**（四英雄统一）；默认 +10~15% 核心道具；进阶增强 40~75% / 削弱 20~75%，或 增强 + 掉率偏移 +10 点 | 已写入 §4 全 60 科技；D 为最核心平衡常数 | 单科技掉率偏移封顶 +10 点（防效果×掉率双重倾斜超模） |
| 6 | 货币产出量 | 胜 **+12** / 负 **+4**；进阶科技解锁单价 **15**（默认免费） | 全解锁 40 进阶 ≈ 50 胜，长线但非劝退 | 解锁节奏是否符合 retention 目标 |
| 7 | 四英雄完整科技清单 | ✅ **已完稿**（§4.1~4.4，60 科技净偏移均 +30，抽验自洽） | 无阻塞，Phase 0 解除 | — |
| 8 | 破阵反馈阈值 | 清砖 **20** 块 → 对手下一行注入 1 块升级砖（绿→红 / 红→黄），提前 1s 预警 | 约 7 次/局，对齐 30s 节奏，不淹没 | 对手曲线非单调但不压垮 |

**C0~C5 砖块配比（拍板值，单位 %）**：

| 阶段 | 时间 | 绿（易/+0） | 黄（+1） | 红（+0.5） | ?（掉道具/+2） |
|---|---|---:|---:|---:|---:|
| C0 | 0–30s | 80 | 10 | 5 | 5 |
| C1 | 30–60s | 65 | 18 | 10 | 7 |
| C2 | 60–90s | 50 | 25 | 18 | 7 |
| C3 | 90–120s | 35 | 32 | 26 | 7 |
| C4 | 120–150s | 22 | 38 | 33 | 7 |
| C5 | 150s+ | 12 | 42 | 39 | 7 |

> 说明：V0 砖潮推进速度（推进乘区）沿用现有 `PressureLevel/Multiplier`，本表只定义耐久配比；?砖占比 5%~7% 恒定，道具经济不受阶段挤压。

---

## 7. 风险与回滚

| 风险 | 触发 | 缓解 |
|---|---|---|
| 剪刀差不够陡 | 砖潮变硬过慢/过快 | Phase 7 首轮读死亡时间差，回 Phase 1 调配比 |
| 科技超模（效果×掉率双重倾斜） | 某科技实测明显强于默认 | 查 §5.4 双重倾斜，单科技掉率偏移封顶 |
| 旧范式残留 | 删不干净的旧英雄引用 | Phase 1 后全局 grep 旧英雄/芯片 ID，零残留 |
| 确定性破坏 | Φ 误接入非确定性随机 | Phase 2 严守 `GatebreakerDeterministicPrng` |

**回滚基线**：Phase 1 前 `gatebreaker_rules.json` 与 `Hero/`、`Chip/` 为可回退快照点；每阶段结束保留 git 节点。

---

## 8. 版本记录

| 版本 | 日期 | 内容 |
|---|---|---|
| v0.1 | 2026-08-21 | 首次落地迁移计划：系统映射、文件级替换清单、数据 Schema 骨架、Phase 0~7 路线与验收、[PLACEHOLDER] 冻结清单、风险回滚 |
| v0.2 | 2026-08-21 | §6 八项数值 tuning 工作坊拍板：Φ 四阶合计 250、道具权重 3/3/3/2/2/1、掉率 15/15/15/20/20/15、C0~C5 配比表、科技预算 D=+30、货币 胜12/负4 单价15、四英雄科技清单标记已完稿、破阵阈值 20；第 7 项确认已完稿解除阻塞 |
| v0.3 | 2026-08-21 | 据《配置对接检查 v0.1》回填：§1 英雄定义行、§2.3 Mode/ 三行、§3 Schema 表名（phaseHeroes→DT_PhaseHero 等五段 + 补 DT_PhaseMeta）、§4 Phase 0 落地步骤，统一改为"**Phase 0 追加 DT_Phase\* 表（保留 V1 表）/ Phase 1 才替换**"并注明草稿独立不可加载、须补 `Version` + 代码侧 5 类 + 5 Read |
| v0.4 | 2026-08-21 | **Phase 0 实施完成**：草稿补 Version:3；RuleDefinitions/catalog/loader 三处代码落地（5 主类 + 8 子结构、5 字典 + 可选参数构造 + getter、5 个 ReadPhase\* + ReadOptionalArray 接入）；改 exporter.py 纳入 5 张 DT_Phase\* 源表并重新导出 live JSON + `.bytes`（18 键、V1 表完整保留）；新增 `ParseJson_LoadsV03PhaseTables` 测试与 `tools/verify_phase_rules_load.py` 模拟验证；同步修复前几轮遗留的源表/测试脱节（配比 0.2→0.1、BallSpeed 3.0→4.5、ItemDrop 7→8 项）。**Phase 1（替换 V1 表 + 改写 ValidateV1Catalog）尚未开始** |
