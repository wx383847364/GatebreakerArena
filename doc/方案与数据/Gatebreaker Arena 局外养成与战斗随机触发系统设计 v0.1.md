# Gatebreaker Arena 局外养成与战斗随机触发系统设计 v0.2

> **版本**: v0.2 | **日期**: 2026-07-11 | **作者**: GameDesigner
> **状态**: 方案阶段
> **依赖**: 核心玩法规则 v0.3, 局内变数系统设计 v0.1, 热更新边界规范 v1, **世界观与命名规范 v1.0**
> **命名遵循**: [世界观与命名规范 v1.0](Gatebreaker%20Arena%20世界观与命名规范%20v1.0.md)

> **一期覆盖说明（2026-07-12）**：本文档保留为旧版完整养成与随机触发方案。当前一期以[局外养成与战斗随机触发系统设计 v0.3](Gatebreaker%20Arena%20局外养成与战斗随机触发系统设计%20v0.3.md)为准：方案为 **1 路线专属 + 5 通用芯片**，按 **0/15/30/45s** 确定性激活；不采用本文档中的赛前随机抽取、能量波动追加激活与过载随机事件。 

---

## 0. 文档定位

本文档定义 Gatebreaker Arena 的**局外养成系统**与**战斗内随机性触发系统**，以及两者如何咬合形成"越玩越不同"的体验循环。

与 [局内变数系统设计 v0.1](../方案与数据/Gatebreaker%20Arena%20局内变数系统设计%20v0.1.md) 的关系：

| 文档 | 覆盖范围 | 本文档增量 |
|---|---|---|
| 变数系统 v0.1 | 地图变量、局内道具、Loadout、改装机 | 纯局内/赛前的即时变数 |
| **本文档** | **局外养成 × 战斗内随机触发** | **跨对局的成长积累 + 养成成果以随机方式注入战斗** |

变数系统文档的四象限是"每局不同的即时原因"，本文档是"每局不同的长期原因"——玩家的成长决定了变数的**素材池**，随机系统决定**本局实际拿到什么**。

---

## 1. 设计哲学

### 1.1 核心问题

Gatebreaker Arena 是 60 秒短局竞技。纯对称竞技的问题上一份文档已经说了——坍缩到最优策略。但引入养成会带来另一个问题：

> **养成会破坏竞技公平性吗？**

如果老玩家数值碾压新玩家，短局竞技就死了。所以这套系统必须满足：

| 约束 | 含义 |
|---|---|
| **养成提供广度，不提供纯强度** | 解锁更多选项，而不是单纯的面板数值堆叠 |
| **随机性是均衡器** | 即使你养成深度高，每局也只能随机激活一部分 |
| **新手有兜底** | 免费基础模块保证新玩家不裸奔 |
| **PvP 可选公平模式** | 竞技排位使用"镜像模式"，双方共享同一组随机激活 |

### 1.2 体验目标

玩家在连续 5 局中应该能说出：

> "上一局我激活了'分裂弹'，球满天飞；这局激活了'铁壁'，变成了防守拉锯；下一局不知道会抽到什么。"

养成让玩家有**期待感**——"我再多打两局就能解锁那个模块了"。随机让玩家有**新鲜感**——"这次会是什么组合？"

---

## 2. 系统总览

```
┌─────────────────────────────────────────────────────────┐
│                    局外 (Meta Layer)                     │
│                                                         │
│  对局结算 → 获得工程点 → 模块商店/升级 → 模块收藏库     │
│                                                         │
│  玩家从收藏库中组建「改装方案」(最多 8 张)                 │
│                                                         │
└────────────────────────┬────────────────────────────────┘
                         │ 改装方案带入对局
                         ▼
┌─────────────────────────────────────────────────────────┐
│                   赛前 (Pre-Match)                       │
│                                                         │
│  从改装方案中伪随机抽取 3 张 → 「激活模块」(本局生效)         │
│  剩余模块进入「待激活池」                                 │
│                                                         │
└────────────────────────┬────────────────────────────────┘
                         │ 激活模块 + 待激活池
                         ▼
┌─────────────────────────────────────────────────────────┐
│                   战斗内 (In-Match)                      │
│                                                         │
│  ┌─────────────┐  ┌──────────────┐  ┌────────────────┐ │
│  │ 被动模块效果 │  │ 能量波动(t=20s│  │ 过载事件随机触发│ │
│  │ 全局持续生效 │  │  /40s)再激活  │  │ 基于活跃模块    │ │
│  └─────────────┘  └──────────────┘  └────────────────┘ │
│                                                         │
│  对局结束 → 结算奖励 → 回到局外                           │
└─────────────────────────────────────────────────────────┘
```

三层结构：

| 层 | 时机 | 玩家做什么 | 系统做什么 |
|---|---|---|---|
| **局外养成** | 对局之间 | 收集模块、升级模块、组建改装方案 | 持久化玩家数据 |
| **赛前抽取** | 对局开始前 | 无（纯系统行为） | 从改装方案随机抽取激活模块 |
| **战斗内随机** | 对局进行中 | 适应激活模块的效果 | 定时再激活 + 过载事件触发 |

---

## 3. 局外养成 —— 改装模块系统

### 3.1 货币与奖励

#### 工程点 (Resonance Shards)

| 来源 | 数量 [PLACEHOLDER] | 备注 |
|---|---|---|
| 对局胜利 | 50 | PVP 胜利 |
| 对局失败 | 20 | 安慰奖，保证养成不停滞 |
| 对局平局 | 30 | 加时未分胜负 |
| 首胜奖励（每日） | +100 | 每日首胜额外奖励 |
| 成就解锁 | 100~300 | 一次性 |
| 段位晋级 | 200~500 | 一次性 |

**设计意图**: 失败也给工程点，避免"连败 → 养成停滞 → 更多败"的负反馈循环。工程点获取量让玩家平均 2-3 局可以解锁一个新模块或升级一次。

#### 工程点用途

```
工程点 → 购买新模块（永久解锁，加入收藏库）
工程点 → 升级已有模块（Lv1 → Lv2 → Lv3，效果增强）
```

### 3.2 模块设计

#### 模块属性结构

每个模块是一个**可叠加的规则修饰器**，在激活后持续影响整局：

```csharp
// 伪代码: 模块定义
public class ChipDefinition
{
    public string ChipId;                    // 唯一标识
    public string DisplayName;               // 显示名
    public ChipCategory Category;            // 分类
    public ChipRarity Rarity;                // 品级
    public int MaxLevel;                     // 最大等级 (默认 3)
    
    // 效果：对 EffectiveMatchRule 的修饰
    public ChipModifier[] Modifiers;         // 每级一个修饰器
    
    // 过载事件关联
    public QuantumEventId LinkedEvent;       // 激活此模块时可触发的过载事件
    public float EventTriggerWeight;         // 事件触发权重加成
}

public struct ChipModifier
{
    public string TargetField;    // 修饰的目标字段 (如 "ServeSpeed", "PaddleLength", "BallMaxSpeed")
    public ModifyOp Op;           // 操作类型: Add / Multiply / Override
    public float Value;           // 修饰值
    public string Condition;      // 条件 (如 "OnScored", "OnWallBounce", "Always")
}
```

#### 模块分类

模块按效果领域分为四类，对应四种游玩风格：

| 类别 | 图标色 | 核心幻想 | 代表效果方向 |
|---|---|---|---|
| **攻 (Strike)** | 红 | "我要用火力碾压你" | 球速、发球、分裂、穿透 |
| **守 (Guard)** | 蓝 | "你攻不破我" | 挡板、门宽、反弹、护盾 |
| **速 (Flow)** | 绿 | "我比你快" | 移动速度、弹药回复、冷却 |
| **乱 (Chaos)** | 紫 | "你猜不到会发生什么" | 随机事件、地图干扰、球路偏移 |

#### 完整模块清单

##### 攻击类 (Strike)

| 模块ID | 名称 | 品级 | 效果 (Lv1/2/3) | 过载事件 |
|---|---|---|---|---|
| `STRIKE_POWER` | 蓄能击 | 标准 | 挡板反弹球速增益 +20%/35%/50% | 火力全开 |
| `STRIKE_SERVE` | 重发球 | 标准 | 发球初速 +15%/25%/35% | — |
| `STRIKE_MULTI` | 弹幕 | 强化 | 发球时 15%/25%/35% 概率同时发出 2 颗球（消耗 1 弹药） | 齐射风暴 |
| `STRIKE_PIERCE` | 穿透弹 | 强化 | 己方球命中墙体时 10%/15%/20% 概率穿透一次 | 维度裂隙 |
| `STRIKE_HOMING` | 追踪弹 | 精英 | 己方球每秒向最近的敌方球门微调 2°/3°/4° | — |
| `STRIKE_OVERCHARGE` | 过载 | 原型 | 己方球速上限 +10%/20%/30%，但己方挡板长度 -10% | 超频爆发 |

##### 防守类 (Guard)

| 模块ID | 名称 | 品级 | 效果 (Lv1/2/3) | 过载事件 |
|---|---|---|---|---|
| `GUARD_LENGTH` | 长板 | 标准 | 挡板长度 +15%/25%/35% | — |
| `GUARD_GOAL` | 收缩门 | 标准 | 己方球门 HalfLength -10%/15%/20% | — |
| `GUARD_BOUNCE` | 弹性墙 | 强化 | 敌方球碰己方墙时减速 10%/15%/20% | — |
| `GUARD_SHIELD` | 能量护盾 | 强化 | 己方门被进球后，10%/15%/20% 概率球被弹回不计分 | 力场展开 |
| `GUARD_REFLECT` | 反击板 | 精英 | 挡板命中球后，球获得 +30%/45%/60% 速度增益（比标准高） | — |
| `GUARD_FORTRESS` | 铁壁 | 原型 | 己方门 HalfLength -30%，但挡板长度 +30%/40%/50% | 绝对防御 |

##### 速度类 (Flow)

| 模块ID | 名称 | 品级 | 效果 (Lv1/2/3) | 过载事件 |
|---|---|---|---|---|
| `FLOW_SPEED` | 疾驰 | 标准 | 挡板移动速度 +15%/25%/35% | — |
| `FLOW_AMMO` | 快装填 | 标准 | 弹药冷却回复速度 +20%/35%/50% | — |
| `FLOW_CAPACITY` | 弹药库 | 强化 | 最大弹药 +1/2/3 | — |
| `FLOW_QUICK_SERVE` | 速射 | 强化 | 发球后 2s/3s/4s 内下次发球不消耗弹药 | — |
| `FLOW_TIME_WARP` | 阻尼器 | 精英 | 敌方球速 -10%/15%/20%（仅对飞向己方的球） | 紧急制动 |
| `FLOW_NEXUS` | 枢纽 | 原型 | 弹药冷却回复速度 +50%，最大弹药 +2，但发球初速 -15% | — |

##### 混乱类 (Chaos)

| 模块ID | 名称 | 品级 | 效果 (Lv1/2/3) | 过载事件 |
|---|---|---|---|---|
| `CHAOS_SPIN` | 旋球 | 标准 | 己方球碰墙后获得 ±3°/5°/8° 随机角度偏移 | — |
| `CHAOS_SPLIT` | 分裂 | 强化 | 己方球碰墙时 8%/12%/16% 概率分裂为 2 颗（速度 ×0.8） | 分裂风暴 |
| `CHAOS_GRAVITY` | 引力 | 强化 | 场地中心出现引力井，所有球受 0.3/0.5/0.7 加速度向中心偏移 | 引力脉冲 |
| `CHAOS_SHUFFLE` | 洗牌 | 精英 | 每 20s 随机交换两名玩家的挡板位置（3s/4s/5s 警告期） | 维度错位 |
| `CHAOS_MIRROR` | 镜像 | 精英 | 球的视觉位置与实际碰撞位置偏移 0.2/0.4/0.6 单位（纯视觉欺骗） | — |
| `CHAOS_QUANTUM` | 信号叠加 | 原型 | 每颗己方球同时存在于两个位置（碰撞取较近的），持续整局 | 信号坍缩 |

#### 模块品级与获取成本

| 品级 | 购买工程点 | 升级工程点 (Lv2→3) | 设计意图 |
|---|---|---|---|
| 标准 | 100 | 150 / 250 | 基础选项，1-2 局解锁 |
| 强化 | 250 | 350 / 500 | 中阶选项，需要定向攒 |
| 精英 | 500 | 700 / 1000 | 高阶选项，约一周解锁 |
| 原型 | 1000 | 1500 / 2000 | 终极选项，长期目标 |

所有数值标注为 `[PLACEHOLDER]`，需要经济模拟后调整。

#### 基础免费模块

新玩家开局自动拥有以下模块，保证不裸奔：

| 模块 | 等级 | 理由 |
|---|---|---|
| `STRIKE_POWER` Lv1 | 蓄能击 | 让新手发球有基本威胁 |
| `GUARD_LENGTH` Lv1 | 长板 | 让新手防守有基本保障 |
| `FLOW_SPEED` Lv1 | 疾驰 | 让新手挡板跟得上球 |

### 3.3 改装方案 (Resonance Deck)

#### 改装方案规则

| 规则 | 值 | 说明 |
|---|---|---|
| 改装方案上限 | 8 张 | 玩家从收藏库中选择 8 张模块组成改装方案 |
| 同名模块 | 不可重复 | 一张模块只能放一张 |
| 类别限制 | 每类最多 3 张 | 防止单一类别堆叠 |
| 原型 限制 | 最多 2 张 | 防止终极速通 |

**改装方案策略**: 玩家的核心决策是"我想让什么风格的可能性出现"。全放攻击模块 → 每局都可能激活强攻，但防守模块永远不会出现。混搭 → 稳定但不极致。

#### 改装方案预设

为降低新手门槛，提供 3 个预设改装方案：

| 预设 | 组成 | 风格 |
|---|---|---|
| 均衡型 | 每类 2 张 标准 | 安全起步 |
| 猛攻型 | 攻 3 + 速 3 + 乱 2 | 激进 |
| 铁桶型 | 守 3 + 速 3 + 乱 2 | 防守反击 |

### 3.4 持久化数据结构

```csharp
// 局外玩家数据 (App.AOT 持久化层)
public class PlayerMetaProfile
{
    public int ResonanceShards;                    // 当前工程点余额
    public Dictionary<string, int> OwnedChips;     // ChipId → Level (0=未拥有)
    public string[] DeckChipIds;                   // 当前改装方案 (最多8)
    public int TotalMatchesPlayed;
    public int TotalWins;
    public int DailyFirstWinRemaining;             // 今日首胜是否可用
    public long LastPlayTimestamp;                 // 用于每日重置
}
```

**存储位置**: 由 `App.AOT` 持久化层负责（SharedPreferences / 本地文件 / 云存档），不放在 HotUpdate 层。HotUpdate 层通过接口读取。

**与配表的关系**: 模块定义在 `DT_ChipRule` 配置表中（通过 YooAssets 加载），玩家数据只存 `ChipId → Level` 映射。这样调整模块数值只需热更配表，不需要改存档。

---

## 4. 赛前抽取 —— 模块激活

### 4.1 激活机制

对局开始时（`Countdown` 阶段），系统从玩家改装方案中**伪随机抽取 3 张模块激活**，激活的模块在整局持续生效。

```
改装方案 (8张) 
  → 伪随机抽取 3 张 → 激活模块 (Active Chips)
  → 剩余 5 张 → 待激活池 (Dormant Pool)
```

#### 伪随机规则（Lockstep 兼容）

```
1. 每名玩家的激活种子 = MatchSeed XOR PlayerId XOR 0xA7C3
2. 使用确定性 PRNG (xorshift32) 从改装方案中无放回抽取 3 张
3. 所有玩家的种子都从 MatchSeed 派生 → 多端结果一致
4. MatchSeed 由房间主机生成并广播（LAN）或从配表读取（PVE）
```

#### 类别倾向加权

抽取不是完全均匀——玩家可以通过"倾向设置"调整各类别的抽取概率：

| 倾向设置 | 攻 | 守 | 速 | 乱 | 效果 |
|---|---|---|---|---|---|
| 均衡（默认） | 25% | 25% | 25% | 25% | 无偏好 |
| 进攻倾向 | 40% | 15% | 25% | 20% | 更可能抽到攻击模块 |
| 防守倾向 | 15% | 40% | 25% | 20% | 更可能抽到防守模块 |
| 混乱倾向 | 20% | 20% | 15% | 45% | 更可能抽到混乱模块 |

加权方式：先按类别权重选类别，再在该类别的改装方案模块中均匀选一张。

**设计意图**: 倾向设置让玩家有"这局我想打什么风格"的控制感，但不保证一定能抽到——你设了进攻倾向也不一定三张全是攻击。这保留了随机性的新鲜感。

### 4.2 激活展示

```
Countdown 阶段 (3s):
  → 屏幕中央依次翻牌展示 3 张激活模块
  → 每张展示 0.8s（名称 + 图标 + 效果简述）
  → 全部展示完毕后进入 Playing
```

**设计原则**: 玩家必须在球开始飞之前知道本局自己有什么能力。不可隐藏激活模块效果。

### 4.3 PvP 公平模式 —— 镜像激活

在排位/竞技模式中，使用**镜像激活**保证公平：

```
镜像模式:
  1. 双方玩家各自提交改装方案
  2. 系统合并双方改装方案为统一池
  3. 从统一池中抽取 3 张 → 双方激活相同的 3 张
  4. 后续能量波动也双方同步
```

| 模式 | 激活方式 | 适用场景 |
|---|---|---|
| PVE_STANDARD | 各自激活 | 娱乐、养成刷工程点 |
| PVP_FFA (休闲) | 各自激活 | 自由对战 |
| PVP_FFA (排位) | **镜像激活** | 竞技公平 |
| PVP_TEAM | 队内各自激活，队间镜像 | 组队竞技 |

**镜像模式下的养成意义**: 养成深度决定你的改装方案有多少好模块可选。改装方案越大越精，镜像池质量越高，双方都受益。新手改装方案小但镜像时也能用到对手的高级模块——养成差距被镜像机制抹平。

---

## 5. 战斗内随机触发系统

### 5.1 能量波动 (Resonance Surge)

激活不是一锤子买卖——对局进行中会有定时波动，从待激活池中再抽取模块激活。

#### 波动时间表

| 时间点 | 事件 | 效果 |
|---|---|---|
| t=0s (Countdown) | 初始激活 | 抽取 3 张 |
| t=20s | 第一次能量波动 | 再抽取 1 张（共 4 张活跃） |
| t=40s | 第二次能量波动 | 再抽取 1 张（共 5 张活跃） |
| t=55s (FinalPhase) | 终局过载 | 从剩余待激活池中抽取 1 张，**直接升至 Lv3 超载**（共 6 张活跃） |

```
活跃模块上限: 6 张 (3 初始 + 3 波动)
```

**设计意图**:
- t=20s 波动给中盘一个转折点——"哦我还有这个能力"
- t=40s 波动配合 FinalPhase(30s) 倒计时加速——更多变量涌入终盘
- t=55s 超载模块是"最后 5 秒翻盘"的戏剧性设计

#### 波动展示

```
能量波动触发时:
  → 全场 0.5s 慢动作 + 紫色光波扫过场地
  → 屏幕上方翻牌展示新激活模块
  → 对应玩家的挡板/球闪烁新效果颜色
  → 音效: 低频嗡鸣 + 翻牌音效
```

#### 波动的 Lockstep 实现

```csharp
// 在 GatebreakerMatchRuntime.Tick() 中
if (phase == Playing)
{
    float elapsed = matchDuration - remainingTime;
    
    if (!surge20Triggered && elapsed >= 20f)
    {
        TriggerResonanceSurge(playerId: allActivePlayers, count: 1);
        surge20Triggered = true;
    }
    if (!surge40Triggered && elapsed >= 40f)
    {
        TriggerResonanceSurge(playerId: allActivePlayers, count: 1);
        surge40Triggered = true;
    }
    if (!surgeFinalTriggered && remainingTime <= 5f)
    {
        TriggerResonanceSurge(playerId: allActivePlayers, count: 1, overload: true);
        surgeFinalTriggered = true;
    }
}
```

`surge20Triggered` 等标记是对局运行时状态，会被纳入 checksum 验证。

### 5.2 过载事件 (Quantum Events)

过载事件是**条件触发的瞬间效果**，不是持续修饰器。它们与激活模块关联——只有当对应模块激活时，才有可能触发过载事件。

#### 事件触发机制

```
每 tick (30 FPS):
  遍历所有活跃模块的 LinkedEvent
  如果 LinkedEvent != null:
    检查事件触发条件
    满足条件 → 按 TriggerWeight 做伪随机判定
    通过 → 触发过载事件
```

#### 触发条件类型

| 条件类型 | 描述 | 示例 |
|---|---|---|
| `OnScored` | 任意玩家得分时 | 火力全开: 得分后己方球速 ×1.5 持续 5s |
| `OnWallBounce` | 任意球碰墙时 | 维度裂隙: 球穿透墙体一次 |
| `OnPaddleHit` | 任意球碰挡板时 | 超频爆发: 球速瞬间拉到 MaxSpeed |
| `OnServe` | 任意玩家发球时 | 齐射风暴: 额外向两侧各发一颗球 |
| `OnGoalRebound` | 球碰己方门回弹时 | 力场展开: 回弹球获得 +50% 速度 |
| `OnSurge` | 能量波动触发时 | 信号坍缩: 所有球瞬移到场地对称位置 |
| `Timed` | 定时触发 | 紧急制动: 每 30s 敌方球速 ×0.3 持续 3s |

#### 完整过载事件清单

| 事件ID | 名称 | 关联模块 | 触发条件 | 效果 | 冷却 |
|---|---|---|---|---|---|
| `QE_FIREPOWER` | 火力全开 | STRIKE_POWER | OnScored (己方得分) | 己方所有球速 ×1.5，持续 5s | 15s |
| `QE_VOLLEY` | 齐射风暴 | STRIKE_MULTI | OnServe (己方发球) | 额外向左右各 30° 发出一颗球（不消耗弹药） | 20s |
| `QE_RIFT` | 维度裂隙 | STRIKE_PIERCE | OnWallBounce (己方球碰墙) | 该球穿透所有墙体 3s | 12s |
| `QE_OVERCLOCK` | 超频爆发 | STRIKE_OVERCHARGE | OnPaddleHit (己方挡板命中) | 该球速度直接设为 MaxSpeed | 10s |
| `QE_SHIELD` | 力场展开 | GUARD_SHIELD | OnGoalRebound (己方门回弹) | 回弹球速度 +80%，向最近敌方门偏转 | 15s |
| `QE_FORTRESS` | 绝对防御 | GUARD_FORTRESS | OnScored (己方被进球) | 己方门缩小 50% 持续 5s | 25s |
| `QE_FREEZE` | 紧急制动 | FLOW_TIME_WARP | Timed (每 30s) | 敌方球速 ×0.3，持续 3s | 30s |
| `QE_BLACKHOLE` | 引力脉冲 | CHAOS_GRAVITY | OnSurge (能量波动时) | 场地中心生成强力引力井 5s，吸所有球 | — |
| `QE_SPLIT_STORM` | 分裂风暴 | CHAOS_SPLIT | OnWallBounce (己方球碰墙) | 该球立即分裂为 3 颗 | 15s |
| `QE_DISPLACE` | 维度错位 | CHAOS_SHUFFLE | OnSurge (能量波动时) | 随机交换两名非己方玩家的挡板位置 | — |
| `QE_COLLAPSE` | 信号坍缩 | CHAOS_QUANTUM | OnSurge (终局过载时) | 所有球瞬移到当前速度方向的对称位置 | — |

**冷却机制**: 过载事件有独立冷却，防止同一事件连续触发。冷却计时器纳入对局运行时状态，参与 checksum。

#### 事件触发概率

基础概率 + 模块等级加成：

```
实际概率 = BaseChance + (ChipLevel - 1) * LevelBonus

BaseChance = 0.08  [PLACEHOLDER]
LevelBonus = 0.04  [PLACEHOLDER]

示例: STRIKE_POWER Lv3 触发火力全开
  概率 = 0.08 + (3-1) * 0.04 = 0.16 = 16% (每次己方得分时)
```

**设计意图**: 概率不高（8-16%），不会每局都触发。触发时是"惊喜时刻"，不触发时也不影响核心玩法。Lv3 模块比 Lv1 更容易触发事件，给升级以意义。

#### 事件视觉表现

| 阶段 | 表现 |
|---|---|
| 触发瞬间 | 全场 0.3s 慢动作 + 屏幕边缘紫色闪光 |
| 效果持续 | 受影响球/挡板有对应颜色的粒子拖尾 |
| HUD | 顶部弹出事件名称条 2s (如 "⚡ 火力全开!") |
| 音效 | 每个事件有独立音效 (低频冲击 + 对应元素音) |

### 5.3 激活模块效果注入点

激活模块的效果在**对局初始化时注入到 `EffectiveMatchRule`**：

```csharp
// 伪代码: 模块效果注入
public EffectiveMatchRule BuildEffectiveRuleWithChips(
    ModeRuleDefinition mode, 
    MapRuleDefinition map, 
    ChipDefinition[] activeChips)
{
    var baseRule = BuildEffectiveRule(mode, map);
    
    foreach (var chip in activeChips)
    {
        var modifier = chip.Modifiers[chip.CurrentLevel - 1];
        ApplyModifier(baseRule, modifier);
    }
    
    // 钳制红线
    ClampToBalanceRedlines(baseRule);
    
    return baseRule;
}

void ApplyModifier(EffectiveMatchRule rule, ChipModifier mod)
{
    switch (mod.TargetField)
    {
        case "BallMaxSpeed":
            rule.BallRule.MaxSpeed = ApplyOp(rule.BallRule.MaxSpeed, mod);
            break;
        case "PaddleLength":
            rule.PaddleHalfLength = ApplyOp(rule.PaddleHalfLength, mod);
            break;
        case "ServeSpeed":
            rule.BallRule.InitialSpeed = ApplyOp(rule.BallRule.InitialSpeed, mod);
            break;
        case "ServeCooldown":
            rule.BaseServeCooldown = ApplyOp(rule.BaseServeCooldown, mod);
            break;
        case "MaxServeAmmo":
            rule.MaxServeAmmo = ApplyOp(rule.MaxServeAmmo, mod);
            break;
        case "GoalHalfLength":
            rule.GoalHalfLength = ApplyOp(rule.GoalHalfLength, mod);
            break;
        // ... 其他字段
    }
}
```

**关键约束**: 模块效果在 `Playing` 阶段开始前一次性注入。能量波动激活的模块在波动瞬间注入——这意味着 `EffectiveMatchRule` 在对局中是**可变的**。需要确保所有运行时系统读取的是当前最新的 `EffectiveMatchRule` 快照，而不是对局开始时的缓存。

---

## 6. 养成 × 随机 × 竞技的平衡

### 6.1 平衡红线（继承自变数系统 v0.1 并扩展）

| 红线 | 值 | 理由 |
|---|---|---|
| 全场球数上限 | ≤ 25 | 碰撞迭代成本 |
| 单次速度倍率上限 | ×3.0 | 球速过快丢失策略性 |
| 活跃模块上限 | 6 | 信息过载阈值 |
| 单类模块活跃上限 | 3 | 防止单一风格碾压 |
| 过载事件同屏上限 | 1 | 同时只允许 1 个过载事件效果在场 |
| 过载事件最长持续 | 5s | 超过 5s 变成常态 buff 而非事件 |
| 面板数值单项加成上限 | +50% | 单项不超过 50%，避免极端 build |
| 面板数值总加成上限 | +80% | 所有模块叠加后单项不超过 80% |
| 负面效果叠加后下限 | -40% | 任何属性不会被模块压到原值的 60% 以下 |

### 6.2 模块效果钳制流程

```
模块效果注入顺序:
  1. 基础 EffectiveMatchRule (Mode + Map)
  2. 逐个应用激活模块的 Modifier (按模块ID排序，保证确定性)
  3. ClampToBalanceRedlines():
     - BallRule.MaxSpeed ≤ 9.8 * 1.8 = 17.64 (×3.0 含过载事件预留空间)
     - BallRule.InitialSpeed ≤ 5.25 * 1.5 = 7.875
     - PaddleHalfLength ≤ base * 1.8
     - GoalHalfLength ≥ base * 0.4
     - MaxServeAmmo ≤ base + 5
     - BaseServeCooldown ≥ base * 0.4
  4. 输出最终 EffectiveMatchRule
```

### 6.3 竞技公平性分析

| 场景 | 新手 (3 张 标准 Lv1) | 老手 (8 张含 原型 Lv3) | 公平性 |
|---|---|---|---|
| PVE 休闲 | 各自激活 | 各自激活 | ✅ 无需公平，各玩各的 |
| PVP 休闲 | 各自激活 | 各自激活 | ⚠️ 老手有优势，但随机性是均衡器 |
| PVP 排位 | 镜像激活 | 镜像激活 | ✅ 双方相同模块，完全公平 |
| PVP 排位 (改装方案质量) | 改装方案小，镜像池小 | 改装方案大，镜像池大 | ✅ 老手改装方案质量更高，双方都受益 |

**排位模式的核心公平性保证**: 镜像激活让双方拿到相同的模块。老手的优势在于"改装方案中有更好的模块可供镜像抽取"，但这同时惠及新手。真正的竞技差距来自**操作和策略**，不是数值。

### 6.4 经济模型（纸面模拟）

```
假设: 新玩家每日打 5 局 PVE

每日工程点收入:
  5 局 × (50胜 or 20败) = 100~250 工程点
  首胜 +100
  总计: 200~350 工程点/天

解锁速度:
  1 个 标准 模块 (100工程点) = 0.3~0.5 天
  1 个 强化 模块 (250工程点) = 0.7~1.25 天
  1 个 精英 模块 (500工程点) = 1.5~2.5 天
  1 个 原型 模块 (1000工程点) = 3~5 天

首周体验:
  Day 1-2: 解锁 2-3 个 标准，改装方案从 3 张扩到 5-6 张
  Day 3-5: 解锁 1-2 个 强化，体验中阶模块
  Day 6-7: 开始攒 精英，改装方案成型

长期目标 (1 个月):
  全 标准 Lv3 + 2-3 个 强化 + 1 个 精英
  改装方案 8 张满，开始精修升级
```

所有数值为 `[PLACEHOLDER]`，需要上线后根据实际数据调整。

---

## 7. 与现有系统的集成

### 7.1 架构接入点

```
App.AOT (持久化层)
  ├── PlayerMetaProfile 存储/读取
  └── 每日重置逻辑

App.Shared (跨层契约)
  ├── IPlayerMetaProfileProvider (接口)
  └── ChipDefinition DTO

App.HotUpdate.GatebreakerArena
  ├── Chip/                    [新模块] 模块系统核心
  │   ├── ChipCatalog.cs           模块定义目录 (从 DT_ChipRule 加载)
  │   ├── ChipEffectInjector.cs    模块效果注入 EffectiveMatchRule
  │   ├── ResonanceAwakener.cs     赛前激活抽取
  │   ├── ResonanceSurgeSystem.cs  战斗内能量波动
  │   └── QuantumEventSystem.cs    过载事件触发引擎
  ├── Match/
  │   └── GatebreakerMatchRuntime.cs  [修改] 在 Tick 中加入波动检查
  ├── Mode/
  │   └── GatebreakerModeCatalog.cs  [修改] BuildEffectiveRule 加入模块参数
  ├── UI/
  │   ├── ChipCollectionPresenter.cs [新] 模块收藏/升级 UI
  │   ├── DeckBuilderPresenter.cs    [新] 改装方案编辑 UI
  │   └── AwakeningDisplayPresenter.cs [新] 激活翻牌展示
  └── Config/
      └── DT_ChipRule (新配表)
```

### 7.2 配表扩展

新增配表 `DT_ChipRule`：

| 字段 | 类型 | 说明 |
|---|---|---|
| ChipId | string | 唯一标识 |
| DisplayName | string | 显示名 |
| Category | enum | Strike/Guard/Flow/Chaos |
| Rarity | enum | 标准/强化/精英/原型 |
| MaxLevel | int | 最大等级 |
| ModifiersLv1 | string (JSON) | Lv1 修饰器数组 |
| ModifiersLv2 | string (JSON) | Lv2 修饰器数组 |
| ModifiersLv3 | string (JSON) | Lv3 修饰器数组 |
| LinkedEventId | string | 关联过载事件 ID (可空) |
| EventTriggerWeight | float | 事件触发权重 |
| IconPath | string | 图标资源路径 |
| Description | string | 效果描述文本 |

新增配表 `DT_QuantumEventRule`：

| 字段 | 类型 | 说明 |
|---|---|---|
| EventId | string | 唯一标识 |
| DisplayName | string | 显示名 |
| TriggerCondition | enum | 触发条件类型 |
| BaseChance | float | 基础触发概率 |
| EffectDuration | float | 效果持续时间 |
| Cooldown | float | 冷却时间 |
| EffectScript | string | 效果脚本标识 (由 QuantumEventSystem 解析执行) |

### 7.3 EffectiveMatchRule 可变性

当前 `EffectiveMatchRule` 在对局开始时一次性构建。引入模块系统后，需要支持**对局中途变更**：

```csharp
// GatebreakerMatchRuntime 新增
private EffectiveMatchRule _currentEffectiveRule;
public EffectiveMatchRule CurrentEffectiveRule => _currentEffectiveRule;

void OnResonanceSurge(ChipDefinition newChip)
{
    // 注入新模块效果
    _currentEffectiveRule = ChipEffectInjector.Inject(
        _currentEffectiveRule, newChip);
    
    // 通知所有系统刷新
    _ballSystem.OnRuleChanged(_currentEffectiveRule);
    _paddleSystem.OnRuleChanged(_currentEffectiveRule);
    _serveSystem.OnRuleChanged(_currentEffectiveRule);
}
```

**风险**: 运行时规则变更可能引入确定性 bug。缓解措施：
- 规则变更只在能量波动时发生（固定时间点），不是每 tick
- 变更后立即生成 checksum 验证
- 所有系统必须从 `CurrentEffectiveRule` 读取参数，不缓存

### 7.4 与局内变数系统的交互

模块系统与变数系统 v0.1 的四象限可以叠加：

| 变数象限 | 模块系统交互 | 规则 |
|---|---|---|
| 地图变量 (象限 III) | 模块效果 × 地图变量 = 乘法叠加 | 模块改属性，地图改结构，互不冲突 |
| 局内道具 (象限 IV) | 道具效果 × 模块效果 = 乘法叠加 | 道具是临时 buff，模块是持续修饰 |
| 赛前 Loadout (象限 II) | Loadout 是固定选择，模块是随机激活 | 可共存：Loadout 提供基础能力，模块提供随机变数 |
| 改装机 (象限 I) | 改装机定义基础属性模版，模块在此基础上修饰 | 远期共存 |

**推荐落地顺序**: 变数系统 Phase 1-3 (地图+道具) → 模块系统 Phase 1-2 (基础养成+激活) → 变数系统 Phase 4 (Loadout) → 模块系统 Phase 3 (过载事件)

---

## 8. 分阶段落地路线图

```
Phase 0（当前）: 核心碰撞 + 计分 + 发球资源 ✅
│
├─ Chip Phase 1（2-3 周）: 养成基础 + 赛前激活
│   ├── PlayerMetaProfile 持久化
│   ├── DT_ChipRule 配表 + ChipCatalog
│   ├── 12 个模块 (每类 3 个 标准+强化)
│   ├── 改装方案编辑 UI
│   ├── 赛前激活抽取 (3 张) + 翻牌展示
│   ├── ChipEffectInjector 注入 EffectiveMatchRule
│   ├── 对局结算工程点奖励
│   └── PVE 模式验证
│
├─ Chip Phase 2（2 周）: 能量波动 + 扩展模块
│   ├── ResonanceSurgeSystem (t=20s/40s/55s)
│   ├── 激活模块中途注入 (EffectiveMatchRule 可变)
│   ├── 剩余 12 个模块 (精英 + 原型)
│   ├── 模块升级 UI
│   ├── 倾向设置
│   └── PvP 休闲模式 (各自激活)
│
├─ Chip Phase 3（2-3 周）: 过载事件 + 竞技模式
│   ├── QuantumEventSystem 事件引擎
│   ├── 11 个过载事件实现
│   ├── 事件视觉特效 + HUD
│   ├── PvP 排位模式 (镜像激活)
│   ├── 经济平衡调优
│   └── 全量 Playtest
│
└─ Chip Phase 4（远期）: 深化
    ├── 模块合成/分解
    ├── 赛季模块轮换
    ├── 模块故事/皮肤
    └── 排行榜与赛季奖励
```

---

## 9. Playtest 验证计划

### Chip Phase 1 验证清单

- [ ] 改装方案编辑是否在 30s 内完成？
- [ ] 激活翻牌展示是否清晰？玩家能否复述本局 3 个激活模块？
- [ ] 模块效果注入后 EffectiveMatchRule 是否正确？数值是否在红线内？
- [ ] PVE 对局工程点奖励是否到账？重复对局是否正常累计？
- [ ] 存档关闭重开后 PlayerMetaProfile 是否完整恢复？

### Chip Phase 2 验证清单

- [ ] 能量波动在 t=20/40/55s 是否准确触发？
- [ ] 中途注入模块后所有系统是否正确读取新规则？
- [ ] Checksum 在波动前后是否一致（多端）？
- [ ] 6 张活跃模块时信息是否过载？玩家能否管理？
- [ ] 倾向设置是否有效改变抽取分布？

### Chip Phase 3 验证清单

- [ ] 过载事件触发概率是否合理？（不能太频繁也不能完全看不到）
- [ ] 事件冷却是否生效？是否有一局内同一事件多次触发？
- [ ] 镜像激活下双方模块是否完全一致？多端 checksum 是否同步？
- [ ] 新手 vs 老手在排位模式中胜率是否接近 50%？
- [ ] 过载事件视觉是否清晰？玩家能否区分不同事件？

---

## 10. 系统交互矩阵

| 系统A × 系统B | 交互规则 |
|---|---|
| 模块球速加成 × 地图加速面 | 乘法叠加，最终由 ClampSpeed 钳制 |
| 模块挡板长度 × 道具长板 | 加法叠加 (base × (1 + chip + item))，上限 ×2.0 |
| 模块分裂 × 道具分裂球 | 触发时各自独立判定概率，不叠加概率 |
| 过载事件火力全开 × 模块过载 | 火力全开设球速 = CurrentMax × 1.5，过载已提高 CurrentMax，乘法叠加 |
| 能量波动激活 × 道具拾取 | 波动瞬间不清除已有道具效果，新模块效果独立生效 |
| 镜像激活 × 各自倾向设置 | 镜像模式下忽略倾向，使用统一均匀抽取 |
| 过载事件 × FinalPhase 加速 | 事件效果 × FinalPhase 球速倍率，乘法叠加 |
| 模块穿透 × 墙体碰撞 | 穿透期间球跳过墙体 TOI 计算，但仍检查挡板和球门 |

---

## 11. 边界与风险

### 11.1 确定性风险

| 风险 | 严重度 | 缓解 |
|---|---|---|
| EffectiveMatchRule 中途变更导致多端不同步 | 高 | 变更只在固定时间点，变更后立即 checksum |
| 过载事件伪随机多端不一致 | 高 | 所有随机使用 MatchSeed 派生的确定性 PRNG |
| 模块效果叠加顺序影响结果 | 中 | 按模块 ID 排序注入，保证多端顺序一致 |
| 能量波动标记未纳入 checksum | 中 | surgeTriggered 标记纳入运行时状态快照 |

### 11.2 设计风险

| 风险 | 严重度 | 缓解 |
|---|---|---|
| 6 张活跃模块信息过载 | 中 | 模块效果通过视觉颜色/图标直观表达，不要求玩家记数字 |
| 过载事件打断核心玩法节奏 | 中 | 事件最长 5s，有冷却，最多 1 个同屏 |
| 养成差距在休闲 PvP 中过大 | 中 | 失败也给工程点 + 随机激活是均衡器 + 排位用镜像 |
| 原型 模块效果过于强力 | 高 | 所有 原型 有"代价"（如过载减挡板长度），且受红线钳制 |

### 11.3 工程风险

| 风险 | 严重度 | 缓解 |
|---|---|---|
| PlayerMetaProfile 存档损坏 | 高 | 版本号 + 迁移逻辑 + 校验和 |
| 配表热更后存档引用了不存在的 ChipId | 中 | 加载时校验，无效 ID 降级为基础模块 |
| 模块效果注入链路太深难以调试 | 中 | 每次注入生成日志（模块ID → 目标字段 → 旧值 → 新值） |
| 过载事件效果脚本化引入安全风险 | 低 | 事件效果用枚举+参数化实现，不用动态脚本 |

---

## 12. 变更日志

| 版本 | 日期 | 变更 |
|---|---|---|
| v0.1 | 2026-06-28 | 初稿：改装模块系统（局外养成）、赛前激活抽取、能量波动（战斗内定时再激活）、过载事件（战斗内条件随机触发）、镜像激活（PvP公平）、经济模型、落地路线图 |
| v0.2 | 2026-07-11 | **全量术语更新**：共鸣碎片→工程点、共鸣模块→改装模块、共鸣方案→改装方案、觉醒→激活、共鸣波动→能量波动、终焉波动→终局过载、量子事件→过载事件、镜像觉醒→镜像激活、品级 Common/Rare/Epic/Legendary→标准/强化/精英/原型。详见[世界观与命名规范 v1.0](Gatebreaker%20Arena%20世界观与命名规范%20v1.0.md)。机制数值不变 |
