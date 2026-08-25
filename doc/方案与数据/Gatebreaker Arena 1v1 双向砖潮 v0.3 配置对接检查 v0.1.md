# Gatebreaker Arena 1v1 双向砖潮 v0.3 配置对接检查 v0.1

> **状态**：引擎加载契约核查报告，配套《规则配置草稿 v0.3.json》与《落地迁移计划 v0.1》。
>
> **日期**：2026-08-21
>
> **检查方法**：通读 `GatebreakerConfigRuntimeLoader.cs`、`RuleDefinitions.cs`、`GatebreakerModeCatalog.cs`、`GatebreakerConfigRuntimeLoaderTests.cs` 及现有 `gatebreaker_rules.json`，逐表逐字段对照草稿 JSON。

---

## 0. 一句话结论

草稿 JSON **作为独立文件无法直接被引擎加载**（缺 `Version` 字段、且其 `DT_Phase*` 表加载器根本不读），但作为**追加源**并入现有 `gatebreaker_rules.json`（保留 V1 三英雄/芯片表）后，配合**新增的 C# 定义类 + catalog 字典 + loader 读取方法**，可干净落地，不破坏现有任何模式。

并在本次检查中**修正了迁移计划 §1/§2 的一处错误假设**：原写"替换 `DT_Hero` 等旧表"，实际 `GatebreakerConfigRuntimeLoader.ValidateV1Catalog` 对旧表做了硬校验，直接替换会让**整个配置加载失败并回退默认 catalog**。Phase 0 必须"追加"而非"替换"。

---

## 1. 引擎加载契约（三个硬事实）

| # | 事实 | 来源 | 对草稿的影响 |
|---|---|---|---|
| 1 | 加载器是**硬编码解析器**：只 `ReadArray(root, "DT_Xxx", …)` 显式列出的表；**未知 `DT_` 键被静默忽略**，不报错也不加载 | `GatebreakerConfigRuntimeLoader.ParseJson` L84–116 | 草稿的 `DT_PhaseHero` 等不会被读取 → 必须加 `Read` 方法才能进 catalog |
| 2 | `Version` 字段**必填**，`< 2` 直接抛 `FormatException`（"require schema Version >= 2"） | L79–83 | 草稿**缺 `Version`** → 独立加载必崩；并入 live 文件（已有 `Version:3`）则无碍 |
| 3 | `ValidateV1Catalog` 硬断言：`DT_Hero` 恰好 3 个 V1 英雄、`DT_HeroPath` 6、`DT_UniversalChip` 12、`DT_SignatureChip` 12，且每英雄恰 2 路径、各 2 个 Stable/Style +3 共振变体 | L135–169 | 替换旧表 → 整表加载失败 → 触发 `CanUseDefaultCatalogFallback`，**所有规则清零** |

> 结论：草稿的 5 张 `DT_Phase*` 表对加载器是"透明"的——加进去不会炸，但也读不出来。要让它们生效，必须补齐代码侧（见 §4）。

---

## 2. 字段级对接结果

> 约定：✅ = 现有 loader 已能读同名字段；🆕 = 需新增定义类与 Read 方法；⚠️ = 需注意的异构/命名点。

### 2.1 DT_PhaseHero（4 行）

| 草稿字段 | 类型 | 对接 | 备注 |
|---|---|---|---|
| `HeroId` / `DisplayName` | string | 🆕 `ReadString` | `HERO_MIRAGE` 等 4 个新 ID，不与旧 3 英雄冲突 |
| `Dimension` / `CoreResource` / `CoreItem` | string | 🆕 字符串直读 | 维度/核心资源/核心道具（如 `ItemSplit`），仅作展示与逻辑引用 |
| `PhaseLevels[]` | obj[] | 🆕 `ReadPhaseLevel` | `PhaseLevel`(P1~P5 字符串) / `Nature`(Identity/Scale/Active/Protocol/Climax 字符串) / `PhiToReach`(int **0/30/50/70/100**，已拍板) / `EffectText`(string) / `ActiveAbility{AbilityId,CooldownSeconds}` / `ProtocolOptions[{OptionId,DisplayName,IsDefault(bool),EffectText}]` |
| `PhiSources[]` | obj[] | 🆕 `ReadPhaseSource` | `Source`(string) / `Phi`(float，含 0.5) / `Note`(string)；`PerSecondCap` 的 `Phi:6` 是硬约束 |

**判定**：结构清晰，无类型冲突。`PhiToReach` 数值与 §6 拍板值一致。

### 2.2 DT_PhaseTech（60 行）

| 草稿字段 | 类型 | 对接 | 备注 |
|---|---|---|---|
| `TechId` / `HeroId` / `SlotPhase` | string | 🆕 | `SlotPhase` 取值 P1~P5 |
| `Kind` | string | 🆕 字符串或枚举 | `Default` / `Advanced` |
| `DisplayName` | string | 🆕 | |
| `CostCurrency` | int | 🆕 | 0（默认）或 15（进阶） |
| `NetOffset` | int | 🆕 | 全部 = 30（预算 D），已校验自洽 |
| `Effects[]` | obj[] | 🆕 `ReadPhaseTechEffect` | `{ItemId, ItemName, Op, MagnitudePercent}`；`Op` 为 `Enhance`/`Weaken`，可定义 `PhaseTechOp` 枚举（枚举解析 `Enum.TryParse` 大小写不敏感，兼容）或保留字符串 |

**判定**：60 行净偏移均 = 30，无逻辑冲突。

### 2.3 DT_PhaseItem（6 行）⚠️

| 草稿字段 | 类型 | 对接 | 备注 |
|---|---|---|---|
| `ItemId` / `ItemName` | string | 🆕 | `ItemPierce/ItemSplit/ItemDamp/ItemSpeed/ItemWide/ItemMagnet` |
| `ValueWeight` | int | 🆕 | 3/3/3/2/2/1（§6 拍板） |
| `BaseDropWeight` | float | 🆕 | 0.15~0.20（合计 1.0）；**注意**：现有 `DT_BrickDuelItemDrop` 用 `DropWeight` 命名，本表用 `BaseDropWeight`，二者不冲突，是新字段 |
| `Effect{}` | **异构对象** | 🆕 `ReadPhaseItemEffect` | ⚠️ 每个道具子字段不同：`PierceCharges` / `TempBallCount`+`TempBallSeconds` / `TideSpeedMultiplier` / `BallSpeedMultiplier` / `PaddleLengthMultiplier` / `PullNextItem(bool)`。**建议读成 `IReadOnlyDictionary<string,object>` 灵活包**，由运行时按 `ItemId` 解释，避免为 6 种效果各写强类型 |
| `Note` | string | 🆕 | |

**判定**：唯一需设计决策的表——`Effect` 异构，不要强类型化，用灵活字典包。

### 2.4 DT_PhaseCurve（6 档）⚠️

| 草稿字段 | 类型 | 对接 | 备注 |
|---|---|---|---|
| `RuleId` | string | 🆕 | `BRICK_DUEL_PHASE_V0` |
| `CompositionIntervalSeconds` | float | 🆕 | 30.0 |
| `Stages[]` | obj[] | 🆕 `ReadPhaseStage` | `{Stage(string), TimeStart(int), GreenWeight, YellowWeight, RedWeight, MysteryWeight(float)}`——**字段名与现有 `BrickDuelCompositionStageDefinition` 完全一致**（Green/Red/Yellow/Mystery Weight），可复用同一结构 |
| `BreakCounterThreshold` | int | 🆕 | 20（破阵反馈阈值，§6 拍板） |
| `BreakCounterWarnSeconds` | float | 🆕 | 1.0（落地预警提前量） |

⚠️ **与现有 `DT_BrickDuelRule.BrickCompositionStages` 重叠**：两者都定义 C0~C5 配比。现有玩法读 `BrickDuelRuleDefinition.ResolveBrickCompositionWeights` → 读的是旧 `BrickCompositionStages`（绿 0.9→0.1）。草稿的新配比（0.8→0.12）**不会自动生效**，直到 Phase 4 把 `ResolveBrickCompositionWeights` 改为优先读 `DT_PhaseCurve`。

✅ **权重校验已核对**：6 档 `Green+Yellow+Red+Mystery` 均 = 1.000（C0 0.8+0.1+0.05+0.05 … C5 0.12+0.42+0.39+0.07），满足 loader `ValidateBrickCompositionWeights` 的"合计=1"约定（若将来折叠进 `BrickCompositionStages` 则必需）。

### 2.5 DT_PhaseMeta（1 行）

| 草稿字段 | 类型 | 对接 | 备注 |
|---|---|---|---|
| `MetaId` | string | 🆕 | `PHASE_META_V0` |
| `CurrencyWin` / `CurrencyLoss` | int | 🆕 | 12 / 4 |
| `TechUnlockCost` | int | 🆕 | 15 |
| `NetOffsetBudget` | int | 🆕 | 30 |
| `DropOffsetCap` | int | 🆕 | 10 |
| `PhiPerSecondCap` | int | 🆕 | 6 |
| `ScissorDiffTargetSeconds` | int | 🆕 | 40 |

**判定**：全部标量，Read 最简单，无冲突。

---

## 3. 四个阻断点（BLOCKER）

| # | 阻断点 | 现状 | 修复 |
|---|---|---|---|
| B1 | 草稿缺 `Version` 字段 | 独立 `ParseJson` 抛 "require schema Version >= 2" | 草稿根加 `"Version": 3`（并保留 `_DraftMeta`） |
| B2 | `ValidateV1Catalog` 绑定旧范式 | 替换 `DT_Hero` 等 → 整表加载失败 → 回退默认 catalog（所有规则清零） | **Phase 0 只追加 `DT_Phase*` 表，保留 V1 表不动**；替换旧表推迟到 Phase 1，并同步改写 `ValidateV1Catalog`（去掉 3 英雄/12 芯片硬断言） |
| B3 | 加载器硬编码表名 | 草稿 5 表不会被读 | 必须新增 5 个 `ReadPhase*` 方法 + 在 `ParseJson` 显式调用 + 5 个 catalog 字典与 getter（见 §4） |
| B4 | 测试断言绑旧配比 | `ParseJson_VersionThreeLoadsBrickDuelRule` 断言 `BrickCompositionStages[0].GreenWeight == 0.90f`、`[5]==0.20f` | Phase 0 不折叠配比 → 无碍；若 Phase 4 把新配比写回 `BrickCompositionStages`，须同步改这两条断言 |

---

## 4. 必须新增 / 修改的代码（精确到方法）

### 4.1 `Mode/RuleDefinitions.cs`（🆕 新增 5 主类 + 子结构）

```csharp
public sealed class PhaseHeroDefinition
{
    public string HeroId;
    public string DisplayName;
    public string Dimension;
    public string CoreResource;
    public string CoreItem;
    public IReadOnlyList<PhaseHeroLevelDefinition> PhaseLevels;
    public IReadOnlyList<PhaseHeroPhiSourceDefinition> PhiSources;
}
// + PhaseHeroLevelDefinition / PhaseHeroProtocolOptionDefinition / PhaseHeroActiveAbilityDefinition
// + PhaseHeroPhiSourceDefinition

public sealed class PhaseTechDefinition
{
    public string TechId;
    public string HeroId;
    public string SlotPhase;
    public string Kind;          // "Default" | "Advanced"
    public string DisplayName;
    public int CostCurrency;
    public int NetOffset;
    public IReadOnlyList<PhaseTechEffectDefinition> Effects;
}
// + PhaseTechEffectDefinition { ItemId; ItemName; Op; MagnitudePercent }

public sealed class PhaseItemDefinition
{
    public string ItemId;
    public string ItemName;
    public int ValueWeight;
    public float BaseDropWeight;
    public IReadOnlyDictionary<string, object> Effect;   // 异构灵活包
    public string Note;
}

public sealed class PhaseCurveDefinition
{
    public string RuleId;
    public float CompositionIntervalSeconds;
    public IReadOnlyList<PhaseCurveStageDefinition> Stages;   // 字段同 BrickDuelCompositionStageDefinition
    public int BreakCounterThreshold;
    public float BreakCounterWarnSeconds;
}
// + PhaseCurveStageDefinition { Stage; TimeStart; GreenWeight; YellowWeight; RedWeight; MysteryWeight }

public sealed class PhaseMetaDefinition
{
    public string MetaId;
    public int CurrencyWin;
    public int CurrencyLoss;
    public int TechUnlockCost;
    public int NetOffsetBudget;
    public int DropOffsetCap;
    public int PhiPerSecondCap;
    public int ScissorDiffTargetSeconds;
}
```

### 4.2 `Mode/GatebreakerModeCatalog.cs`（🆕 字典 + 构造重载 + getter）

- 新增 5 个 `Dictionary<string, …Definition>`（`_phaseHeroes` 等，按 `HeroId`/`TechId`/`ItemId`/`RuleId`/`MetaId` 索引）
- 新增构造重载接收这 5 个 `IEnumerable<T>`，并在 `IndexBy` 中聚合
- 新增 `GetPhaseHero` / `GetPhaseTech` / `GetPhaseItem` / `GetPhaseCurve` / `GetPhaseMeta` 及 `AllPhaseHeroes` 等只读字典暴露

### 4.3 `Mode/GatebreakerConfigRuntimeLoader.cs`（🆕 5 个 Read + ParseJson 接入）

- 新增 `ReadPhaseHero` / `ReadPhaseTech` / `ReadPhaseItem` / `ReadPhaseCurve` / `ReadPhaseMeta`（字段对照 §2）
- 在 `ParseJson` 的 catalog 构造处，追加 5 个 `ReadArray(root, "DT_PhaseXxx", ReadPhaseXxx)` 调用
- **可选校验**（Phase 0 先不做，Phase 7 前补）：`NetOffset` 同英雄 = `NetOffsetBudget ±5%`；`BaseDropWeight` 合计 ≈ 1.0；`Stages` 权重合计 = 1.0

### 4.4 `Assets/Tests/`（🆕 1 个测试）

- `ParseJson_LoadsV03PhaseTables`：读取合并后的 `gatebreaker_rules.json`，断言 `result.Catalog.GetPhaseHero("HERO_MIRAGE")` 非空、`PhaseLevels.Count == 5`、`GetPhaseTech` 共 60、`GetPhaseItem("ItemPierce").ValueWeight == 3`、`GetPhaseCurve(...).Stages.Count == 6`、`GetPhaseMeta(...).PhiPerSecondCap == 6`。

---

## 5. 推荐 Phase 0 落地步骤（修正后）

1. 草稿根补 `"Version": 3`（修 B1）；
2. 写 `RuleDefinitions.cs` 5 主类 + 子结构（§4.1）；
3. `GatebreakerModeCatalog.cs` 加字典 + 构造重载 + getter（§4.2）；
4. `GatebreakerConfigRuntimeLoader.cs` 加 5 个 Read + ParseJson 接入（§4.3）；
5. 将草稿 5 表**追加**进 `gatebreaker_rules.json` 根（**不改 V1 表**，修 B2/B3），重建 `.bytes`；
6. 加 `ParseJson_LoadsV03PhaseTables` 测试并跑绿；
7. （Phase 1 起）再考虑替换 V1 表 + 改写 `ValidateV1Catalog`。

---

## 6. 对《落地迁移计划 v0.1》的修正

| 计划位置 | 原表述 | 修正后 |
|---|---|---|
| §1 系统映射「英雄定义」行 | "替换" | "**Phase 0 追加 DT_Phase\* 表；Phase 1 替换 DT_Hero/DT_HeroPath/DT_SignatureChip/DT_UniversalChip 并改写 ValidateV1Catalog**" |
| §2.3 Mode/ 三行 | "替换旧 HeroDefinition 集" / "新增 DT_PhaseTech…" / "加载逻辑扩展兼容新 JSON 段" | 改为"**Phase 0 追加** 5 个 DT_Phase\* 表与 5 个 catalog 字典 + 5 个 Read 方法；**Phase 1 才替换**旧 HeroDefinition 集"；加载器不是"兼容扩展"而是"显式加 Read 调用" |
| §3 Schema 落点 | "`gatebreaker_rules.json` 新增 `phaseHeroes`/`phaseTechs`… 四段" | 实际草稿表名为 **`DT_PhaseHero`/`DT_PhaseTech`/`DT_PhaseItem`/`DT_PhaseCurve`/`DT_PhaseMeta`**（大写 `DT_Phase` 前缀，与引擎 `DT_BrickDuelRule` 风格一致） |
| §4 Phase 0 | "产出四段 Schema 草稿" | 追加"草稿独立不可加载，须加 `Version` 且并入 live 文件；代码侧补 5 类 + 5 Read" |

---

## 7. 版本记录

| 版本 | 日期 | 内容 |
|---|---|---|
| v0.1 | 2026-08-21 | 首次对接检查：核对加载器硬编码解析 + ValidateV1Catalog 硬绑定；定位 4 阻断点；字段级对接结果；修正迁移计划 §1/§2/§3/§4 的"替换"错误假设为"Phase 0 追加 / Phase 1 替换" |
| v0.2 | 2026-08-21 | **Phase 0 代码落地完成**：修 B1（草稿加 Version:3）；RuleDefinitions.cs 加 5 主类 + 8 子结构；catalog 加 5 字典 + 可选参数构造 + getter；loader 加 5 个 ReadPhase\* + ReadObjectMap + ParseJson 以 ReadOptionalArray 接入；改 exporter.py 纳入 5 张 DT_Phase\* 源表并重新导出 live JSON + .bytes；新增 `ParseJson_LoadsV03PhaseTables` 测试与 `tools/verify_phase_rules_load.py` 模拟验证。发现并同步修复前几轮会话遗留的"源表 vs 测试"脱节（BrickDuelRule 配比 0.2→0.1、BallSpeed 3.0→4.5、ItemDrop 7→8 项） |
