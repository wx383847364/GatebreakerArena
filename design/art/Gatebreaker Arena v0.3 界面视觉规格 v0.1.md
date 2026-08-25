# Gatebreaker Arena v0.3 界面视觉规格 v0.1

> **版本**：v0.1 ｜ **日期**：2026-08-22 ｜ **作者**：ui-visual-designer
> **状态**：视觉系统定义稿（供 UI 实现与 UX 流程对齐）
> **适用范围**：v0.3 新增的 3 个局外界面 —— ① 英雄选择 ② 科技配装 ③ 排行榜
> **依据文档**：
> - `Gatebreaker Arena 1v1 双向砖潮 v0.3 UI 补充计划 v0.1.md`（界面清单 / 架构雷区）
> - `Gatebreaker Arena 游戏界面设计 v0.1.md`（V1 视觉基线）
> - `Gatebreaker Arena 字体与TMP方案_v0.1.md`（字体角色）
> - `Gatebreaker Arena 10比16 固定视口适配方案_v0.1.md`（1000×1600 / Screen Space-Camera）
> - `Gatebreaker Arena 世界观与命名规范 v1.0.md`（机械命名 / 禁用词）
> - `Gatebreaker v0.3 UI 参考原型.html`（功能原型与色板/数据结构）
> - 既有截图：主菜单（六边形竞技场边框 + 蓝绿发光面板）、对局 HUD（竖屏、砖墙居中、红/蓝球、X5 倍率）

---

## 0. 文档定位与原则

本文只定义**视觉系统**：颜色、字体、形状、发光、间距、组件外观、状态、图标、动效、对比度、切图清单。不写任何游戏逻辑、不生成最终美术资源、不改动现有代码。

视觉一致性高于数量：所有界面必须对齐本规格；任何偏离需在评审中显式标注。

设计基调沿用既有截图确立的**「深空霓虹 + 六边形竞技场」**语言：

- 近黑深空底色，叠加微弱蓝绿空间渐变与六边形网格。
- 六边形（六重对称）为第一母题：边框、徽章、底纹、铆钉皆由此派生。
- 蓝 / 青色霓虹发光描边，面板以「蓝绿发光」为主。
- 内容居中、对称、聚焦，竖屏 10:16 优先。
- 像素 / 科技字体（Press Start 2P + Fusion Pixel），呼应街机弹球血统。

> ⚠️ **待用户拍板项**（见 §10）：英雄主题色提供 A/B 两套方向；请最终确认一套，本规格默认采用 A。

---

## 1. 视觉身份摘要（Visual Identity）

### 1.1 色彩系统（Color Tokens）

所有颜色以 CSS 变量形式定义，Unity 侧映射为 `Color` + 材质发光参数。数值取自截图观感与 `v0.3 UI 参考原型.html` 既有色板（`--bg #0e1116` / `--panel #171c24` / `--accent #4cc2ff` / `--good #46d18a` / `--bad #ff6b6b` / `--warn #ffb454`），并补全深空梯度。

#### 1.1.1 表面 / 背景（Surface）

| Token | Hex | 用途 |
|---|---|---|
| `--bg-space-0` | `#080B11` | 最底层深空（Letterbox 黑边、视口外） |
| `--bg-space-1` | `#0E1116` | 应用主底（原型 `--bg`） |
| `--bg-space-2` | `#141A22` | 抬升表面、卡片外底 |
| `--panel` | `#171C24` | 面板基色（原型 `--panel`） |
| `--panel-2` | `#1F2630` | 嵌套面板 / 选项块（原型 `--panel2`） |
| `--panel-3` | `#232C38` | 悬停 / 激活态面板 |
| `--line` | `#2B3440` | 发丝描边（原型 `--line`） |
| `--line-strong` | `#38465A` | 强调描边 |

#### 1.1.2 文本（Text）

| Token | Hex | 对比度(于 `--panel`) | 用途 |
|---|---|---|---|
| `--txt` | `#E6EDF3` | ≈13:1 | 主文本 / 标题 / 按钮字（原型 `--txt`） |
| `--txt-sub` | `#8B98A5` | ≈4.6:1 | 次级说明（仅 ≥16px，原型 `--sub`） |
| `--txt-dim` | `#5B6675` | — | 禁用 / 占位（不用于正文） |

#### 1.1.3 霓虹强调 / 状态（Neon Accent & Semantic）

| Token | Hex | 用途 |
|---|---|---|
| `--accent` | `#4CC2FF` | **主霓虹**：青蓝发光描边、主 CTA、选中环 |
| `--accent-2` | `#2EE6C0` | **蓝绿霓虹**：面板辉光、次级强调、折光主题 |
| `--accent-soft` | `rgba(76,194,255,.16)` | 发光填充（选中底、当前玩家行） |
| `--good` | `#46D18A` | 增益 / 解锁 / 成功（原型 `--good`） |
| `--bad` | `#FF6B6B` | 错误 / 非法 / 锁定失败 / 危险（原型 `--bad`） |
| `--warn` | `#FFB454` | 警示 / 预算临界 / 科技花费（原型 `--warn`） |

> **语义色使用纪律**：红(`--bad`)/绿(`--good`)/琥珀(`--warn`)只承载「状态语义」，不承载「英雄身份」。英雄身份色见 §1.4，**仅作卡片头部装饰**，绝不与状态色混用。

#### 1.1.4 英雄相位主题色（Hero Phase Tints）—— 待拍板

4 个相位英雄（`HERO_MIRAGE/PULSE/RIFT/REFRACT`）在命名规范中无预设主题色，需新增。为避免与 §1.1.3 语义色撞车，全部避开纯红/纯绿/纯琥珀，仅作卡片头部与徽章装饰。

**方向 A（推荐 · 光谱区分，4 色易于一眼区分）**

| 英雄 | ID | 维度 | 主题色 | Hex |
|---|---|---|---|---|
| 蜃影 | `HERO_MIRAGE` | 数量 | 幻彩紫 | `#9B8CFF` |
| 脉冲 | `HERO_PULSE` | 速度 | 电光靛 | `#6E8BFF` |
| 裂痕 | `HERO_RIFT` | 穿透 | 品红 | `#FF5FA2` |
| 折光 | `HERO_REFRACT` | 弹道 | 蓝绿 | `#2EE6C0` |

**方向 B（备选 · 单色青蓝梯度，更克制统一）**

`#6FB7FF` → `#4CC2FF` → `#38E0D8` → `#7CF0C8`（按 蜃影/脉冲/裂痕/折光 分配）

> 注：方向 B 的 4 色彼此接近，弱光下区分度低于 A；若选 B 需在卡片上辅以维度图标（§6）保证辨识。本规格正文默认按 **方向 A** 描述。

### 1.2 字体系统（Typography）

直接沿用 `字体与TMP方案_v0.1` 的 4 角色 + Fallback。所有中文均走 Fusion Pixel fallback，禁止出现 `□`。

| 角色 | 字体 | TMP 资产 | 用途 |
|---|---|---|---|
| `ArcadeTitle` | Press Start 2P | `Gatebreaker_ArcadeTitle_PressStart2P_TMP` | 英文屏标题（如 `HERO SELECT`）、版本水印 |
| `PixelChinese` | Fusion Pixel 12px zh_hans | `Gatebreaker_PixelChinese_FusionPixel12_TMP` | 全部中文 UI：标题、按钮、卡片、HUD |
| `PixelBody` | Pixelify Sans | `Gatebreaker_PixelBody_PixelifySans_TMP` | 英文玩家名、数字、状态、积分 |
| `PixelAlt` | DotGothic16 | `Gatebreaker_PixelAlt_DotGothic16_TMP` | 风格预览 / 备选标题 |
| `Fallback` | Fusion Pixel 12px zh_hans | — | TMP 缺字回退（强制） |

**字号（以 1000×1600 参考分辨率，1 单位 = 1px；随 Canvas Scaler 等比缩放）**

| 层级 | 字号 | 字体角色 | 示例 |
|---|---:|---|---|
| 屏标题（中文） | 40–48 | PixelChinese | 英雄选择 / 科技配装 |
| 屏标题（英文副标） | 16–18 | ArcadeTitle | `HERO SELECT` |
| 区块标题 | 28–32 | PixelChinese | P1 · Φ30 |
| 卡片标题 | 24–28 | PixelChinese | 蜃影 / 脉冲 |
| 正文 | 20–22 | PixelChinese | 核心资源：Combo |
| 说明 / 标签 | 16–18 | PixelChinese | 净偏移 +30 |
| 微标 / 角标 | 14 | PixelBody | X5 / +12% |

**字距 / 行高**：中文行高 1.5；英文街机标题字距 +2px；霓虹标题可加 `TextMeshPro` 描边（2px `--accent`）+ 外发光材质。

### 1.3 形状语言（Shapes）

- **六边形母题（Hex Motif）**：正 6 边形。用于——面板外轮廓（六角切角）、徽章（相位 P1–P5、名次、维度）、背景网格平铺、四角铆钉。
- **六角切角面板（Hex-Cut Panel）**：矩形四角以 45° 斜切（chamfer 16–24px）模拟六边形轮廓；内圆角 12–16px。居中、对称。
- **霓虹描边（Neon Border）**：2px 实色 `--accent` + 外发光 `box-shadow 0 0 12px rgba(76,194,255,.55)` + 内辉光 `inset 0 0 8px rgba(76,194,255,.18)`。--accent-2 用于蓝绿面板变体。
- **胶囊 / 药丸（Pill）**：小标签（净偏移、维度、状态）用圆角胶囊，1px `--line-strong` 描边。

### 1.4 发光与边框处理（Glow & Border）

| 处理 | 参数 | 应用 |
|---|---|---|
| Neon 描边 | 2px `--accent` + 外发光 12px(.55) + 内辉光 8px(.18) | 面板、主按钮、选中环 |
| Blue-Green 面板辉光 | 2px `--accent-2` + 外发光 16px(.45) | 主菜单式居中面板、英雄卡 |
| 暗描边 | 1px `--line` | 选项块、次级分隔 |
| 强调描边 | 1px `--line-strong` | 聚焦、输入框 |
| 空间底纹 | 径向暗渐变 + 六边形网格(透明度 4%) + 轻扫描线 | 全局背景 |
| 选中脉冲环 | 3px `--accent` 环 + 1.2s 呼吸发光 | 选中卡 / 选中行 |

### 1.5 间距与栅格（Spacing & Grid）

- **设计基准**：1000 × 1600（10:16），`Screen Space - Camera`，`Match=0.5`，详见视口方案。
- **安全区（Safe Area）**：逻辑内边距 上 64 / 下 64 / 左 40 / 右 40；刘海机型取 `Screen.safeArea` 后再嵌最大 10:16（优先「安全区内嵌」策略）。
- **页边距**：内容区左右 48px → 内容宽 904px。
- **栅格**：竖屏以 8 列（每列 110px，间距 8）为主；英雄卡用 2×2（每卡 432×360）。
- **节奏**：组件间距 12 / 16 / 24 / 32；区块间距 32。

---

## 2. 各界面布局栅格（Layout Grids）

> 统一骨架（所有界面共用）：

```text
┌──────────────── 1000 ────────────────┐
│ [‹ 返回]      屏幕标题(居中)     [状态] │ ← Top Bar   高 112 (安全内 64)
├──────────────────────────────────────┤
│                                        │
│           中央内容区 (可滚动)           │ ← Content   flex:1
│                                        │
├──────────────────────────────────────┤
│ [ 退出/返回 ]          [ 进入 / 确认 ]  │ ← Bottom Bar 高 136 (安全内 64)
└──────────────── 1600 ────────────────┘
  安全内边距: 上64 下64 左40 右40
```

### 2.1 ① 英雄选择界面（Hero Select）

```text
┌──────────────── 1000 ────────────────┐
│ ‹返回    相位英雄选择            EP 120 │  Top 112
│        HERO SELECT                      │
├──────────────────────────────────────┤
│  ┌──────────┐  ┌──────────┐           │
│  │ 蜃影 MIR │  │ 脉冲 PUL │  2×2 英雄卡 │
│  │ 数量·Combo│  │ 速度·Tempo│  (432×360) │
│  │ P1..P5 简述│  │ P1..P5 简述│           │
│  └──────────┘  └──────────┘           │
│  ┌──────────┐  ┌──────────┐           │
│  │ 裂痕 RIFT│  │ 折光 REFR│           │
│  │ 穿透·Rift │  │ 弹道·Refra│           │
│  └──────────┘  └──────────┘           │
│  （选中卡：青蓝环 + 呼吸脉冲）          │
├──────────────────────────────────────┤
│  [ 返回对战模式 ]        [ 进入科技配装 ]│  Bottom 136
│     Exit/Back                 Enter     │
└──────────────────────────────────────┘
```
- **Enter**：`进入科技配装`（主 CTA，未选英雄时禁用置灰）
- **Exit/Back**：`返回对战模式`
- 进入下一步前选中态即确定英雄（`PhaseMatchLoadout.HeroId`）。

### 2.2 ② 科技配装界面（Tech Loadout）

```text
┌──────────────── 1000 ────────────────┐
│ ‹返回    科技配装        净偏移 +30/槽 │  Top 112
│        TECH LOADOUT                     │
├──────────────────────────────────────┤
│  [P1·Φ0  +30]  ◉基准  ◆蜂拥(15币) ◆孤狼 │  槽行 ×5 (P1..P5)
│  [P2·Φ30 +30]  ◉基准  ◆裂变(15币) ◆洪流 │  每槽 3 选项(默认+2进阶)
│  [P3·Φ50 +30]  ...                      │
│  [P4·Φ70 +30]  ... ◆虫群(机制强化)       │
│  [P5·Φ100+30]  ...                      │
│  ┌─ 科技装载→道具价值矩阵(实时) ──────┐ │
│  │ 道具 │权重│增益│削弱│净 │ 裂穿/分形…│ │  6 行 (裂穿/分形/缓滞/疾风/广域/磁吸)
│  └──────────────────────────────────┘ │
├──────────────────────────────────────┤
│ [返回英雄] [锁定配装]    [ 进入对局 ]   │  Bottom 136
│  Exit/Back  Confirm        Enter        │
└──────────────────────────────────────┘
```
- **Enter**：`进入对局`（主 CTA；未满 5 槽或净偏移超预算时禁用）
- **Exit/Back**：`返回英雄选择`
- **Confirm**：`锁定配装`（显式锁定当前 5 槽，防止误触；锁定后槽位只读，需先解锁再改）
- 净偏移实时计：`Σ NetOffset` ≤ `DT_PhaseMeta.NetOffsetBudget(=30)`；进阶科技花费 `c=15` 币（来自 `META.TechUnlockCost` 同量级）。

### 2.3 ③ 排行榜界面（Leaderboard）

```text
┌──────────────── 1000 ────────────────┐
│ ‹返回     排行榜              我的排名#42│  Top 112
│        LEADERBOARD                       │
├──────────────────────────────────────┤
│ 名次│接入者      │英雄  │积分│胜率│      │  表头 (sticky)
│  🥇 │NeonGhost   │蜃影  │1820│68% │      │
│  🥈 │VoltRunner  │脉冲  │1790│65% │      │
│  🥉 │RiftKing    │裂痕  │1755│63% │      │
│  #4 │...         │折光  │... │... │      │
│  #42│★You(高亮)  │脉冲  │1210│54% │  ← 当前玩家(accent-soft 底)│
│  ... (纵向滚动)                       │
├──────────────────────────────────────┤
│ [ 返回主菜单 ]          [ 返回竞技大厅 ] │  Bottom 136
│    Exit/Back                 Enter      │
└──────────────────────────────────────┘
```
- **Enter**：`返回竞技大厅`（主 CTA，离开榜单回到可开局状态）
- **Exit/Back**：`返回主菜单`（关闭榜单）
- **Confirm**：`查看玩家详情`（选中某行后弹出该玩家卡片：主用英雄、段位、胜率、近期战绩）
- 当前玩家行恒以 `--accent-soft` 底 + 左侧青蓝条标记；前三名用金/银/铜 tint 徽章（见 §6）。
- 积分口径：胜负货币 胜+12 / 负+4（`META.CurrencyWin/Loss`），榜单按积分排。

---

## 3. 组件规格（Component Specs）

### 3.1 按钮（Buttons）

通用：最小触控区 **200×72**（图标钮 64×64）；六角切角；发光仅在外缘。

| 类型 | 视觉 | 文案示例 | 状态 |
|---|---|---|---|
| **Enter（进入/确认进入）** | 主霓虹填充渐变 `--accent`→`--accent-2`，白字，强发光 | 进入科技配装 / 进入对局 / 返回竞技大厅 | normal / hover(辉光+1.02) / pressed(.97) / disabled(灰底无光,`--txt-dim`) |
| **Exit / Back（返回/退出）** | 幽灵描边：1px `--line-strong`，`--txt-sub` 字，弱发光 | 返回对战模式 / 返回英雄选择 / 返回主菜单 | 同 Enter |
| **Confirm（确认/锁定）** | 次 solid：`--panel-3` 底 + `--accent` 描边 + `--accent` 字 | 锁定配装 / 查看玩家详情 | 同 Enter |
| 图标钮（返回箭头 / 关闭 X） | 透明圆角六边，64×64，`--txt-sub` 描边图标，hover 转 `--accent` | ‹ / ✕ | hover/pressed |

按钮角标：`Enter` 可附 EN 副标（`PROCEED`）；`disabled` 必须配原因提示（如「请先选择英雄」），不只置灰。

### 3.2 卡片（Cards）

#### 3.2.1 英雄卡（Hero Card）
- 尺寸 432×360，六角切角面板，`--panel` 底；头部 64px 用英雄主题色（`--hero-tint`）作顶条 + 维度 hex 徽章。
- 内容：中文名 28（PixelChinese）+ EN 16（ArcadeTitle）；维度·核心资源·核心道具 说明 16–20；P1–P5 简述列表（每行：相位 hex 徽章 + 截断文案 16）。
- 关联数据：`HEROES[i].{name, dimension, coreResource, coreItem, phases[P1..P5].text}`。

#### 3.2.2 科技槽 / 科技选项卡（Tech Slot & Option）
- **槽行**（P1–P5）：整行 904×auto，头部 hex 徽章（`P1`）+ `Φ{n}` + 净偏移 pill（`+30`）；体内部 3 个**选项钮**（默认 `基准` + 2 进阶 `◆`）。
- **选项钮**：`--panel-2` 底，1px `--line`；hover 转 `--accent` 描边；`on` 态加 `--accent` 环 + `--accent-soft` 底。进阶显示 `◆` + 花费（`15币` `--warn`）。fx 行：`增益 <item> +n%`(`--good`) / `削弱 <item> -n%`(`--bad`)；P4 进阶附 `机制：…`(`--accent`) 文本。
- 关联数据：`TECHS[]` 按 `h==heroId && s==phase` 过滤；`k∈{Default,Advanced}`，`c` 花费，`net`，`fx[]`，`mech`。

#### 3.2.3 排行榜行（Leaderboard Row）
- 整行 904×96，左名次 hex 徽章（前三金/银/铜 tint，其余 `--line-strong`）；接入者名（PixelBody 22）；英雄 chip（`--hero-tint` 描边小胶囊）；积分（PixelBody 24，突出）；胜率微型进度条。
- 选中：整行 `--accent` 环；当前玩家：`--accent-soft` 底 + 左 4px `--accent` 条 + `★` 前缀。
- 关联数据：名次 / `RunnerName` / `HeroId` / `Rating` / `WinRate`。

### 3.3 面板（Panels）
- **居中六角切角面板**：`--panel` 底，2px `--accent` 或 `--accent-2` Neon 描边；用于价值矩阵、玩家详情弹层、确认弹窗。
- **弹层（Popup）**：覆盖 10:16 视口，外 60% 黑遮罩（`--bg-space-0` α.6）；内部面板 scale-in。

### 3.4 滚动区（Scrollers）
- 竖向 `ScrollRect`，自定义细滚动条（`--accent-2` 发光 thumb，8px）；顶/底边缘渐隐遮罩；惯性滚动。
- 英雄 2×2 可整屏显示，无需滚动；科技（5 槽 + 矩阵）与排行榜（长列表）必滚动。
- 榜单行支持键盘/手柄上下聚焦移动（见 §7）。

---

## 4. 状态与发光规格（State & Glow Specs）

> 下列状态适用于按钮 / 卡片 / 行 / 选项；颜色均引 §1.1。

| 状态 | 描边 | 填充 | 文本 | 发光 | 备注 |
|---|---|---|---|---|---|
| **Normal** | 1px `--line` | `--panel`/`--panel-2` | `--txt` | 无 | 默认 |
| **Hover** | 1px `--accent` | `--panel-3` | `--txt` | 外 10px(.4) | 指针/手柄聚焦 |
| **Selected** | 2px `--accent` 环 | `--accent-soft` | `--txt` | 外 14px(.55) + 1.2s 呼吸 | 选中卡/行/选项；持续脉冲 |
| **Disabled** | 1px `--line` | `--bg-space-2` | `--txt-dim` | 无 | 配原因提示 |
| **Error** | 2px `--bad` | `--bad` α.08 | `--bad` | 外 12px(.5) 红 | 非法/超预算；配抖动 |
| **Locked** | 1px `--line` | `--bg-space-2` | `--txt-dim` | 无 | 锁图标 + 条件说明 |
| **Unlocked** | 1px `--good` | `--good` α.08 | `--txt` | 外 10px(.4) 绿 | 已解锁/已拥有 |

**约束**：
- 状态色优先级 `Error > Selected > Hover > Normal`。
- 同一时刻仅一个「选中」主对象；多选列表（科技槽）每个槽独立选中。
- 英雄主题色（§1.4）不进入状态系统；即便选中英雄卡，状态环仍用 `--accent`，主题色只作头部装饰。

---

## 5. 图标与装饰母题（Iconography & Motifs）

### 5.1 核心母题
- **六边形**：面板轮廓、徽章、背景网格（4% 透明六边形平铺）、四角铆钉。
- **相位刻度（Phase Scale）**：P1–P5 用 5 级 hex 徽章，颜色随相位由 `--accent` 渐变到 `--accent-2`（P1 青蓝 → P5 蓝绿）。

### 5.2 功能图标（线性霓虹，64×64，描边 3px）
| 图标 | 语义 | 样式 |
|---|---|---|
| `arrow_back` | 返回 / Exit-Back | 左向箭头 |
| `close` | 关闭弹层 | ✕ |
| `lock` / `unlock` | 锁定 / 解锁 | 六边形锁 |
| `check` | 确认 / 已选 | ✓ |
| `warn` | 警示 / 预算临界 | ⚠ 三角 |
| `balance` | 净偏移 / 价值矩阵 | 天平 ± |
| `star` | 当前玩家 | ★ |

### 5.3 英雄维度图标（96×96，描边霓虹）
| 维度 | 图标 | 隐喻 |
|---|---|---|
| 数量（蜃影） | 三重复制 | 三重球/克隆 |
| 速度（脉冲） | 节拍波 / 节拍器 | 波形 |
| 穿透（裂痕） | 穿墙箭头 | 箭头破墙 |
| 弹道（折光） | 折射棱镜 | 光线偏折 |

### 5.4 名次徽章（Medal Tints）
- `#1` 金 `#FFD166` / `#2` 银 `#C7D0DA` / `#3` 铜 `#CD7F47`；其余用 `--line-strong` 中性。徽章内数字用 `--bg-space-0` 深字保证对比。

### 5.5 装饰
- 背景：径向暗渐变 + 六边形网格(4%) + 轻扫描线（shader）。
- 面板四角：六边形铆钉（小 hex dot）。
- 主菜单式「蓝绿发光面板」用于居中主面板（呼应既有截图）。

---

## 6. 动效与运动（Animation & Motion）

| 动效 | 参数 | 触发 |
|---|---|---|
| 屏幕进入 | 六角虹膜擦除（hex iris）或面板 scale .96→1 + fade 220ms ease-out；子元素 stagger 40ms | 进入界面 |
| 屏幕退出 | 反向擦除 180ms | Exit/Back |
| Enter 转场 | 六角虹膜闭合→下一界面开启 | 点 Enter |
| 按钮 Hover | 辉光 0→1，scale 1→1.02，140ms | 指针/手柄聚焦 |
| 选中脉冲 | 3px `--accent` 环 1.2s 呼吸（expand+fade loop） | 持续选中 |
| 错误反馈 | 边框闪 `--bad` + 水平抖动 ±6px ×2，300ms | 非法操作/超预算 |
| 锁定反馈 | 锁图标轻微抖动 1 次 | 尝试进入未解锁 |
| 滚动 | 惯性 + 顶/底渐隐 | 列表滚动 |
| 数据刷新 | 价值矩阵数字滚动 + 净偏移条伸缩 | 选/改科技 |

**无障碍**：检测到 `prefers-reduced-motion` 或设置「减少动态」时，关闭脉冲/抖动/虹膜，仅保留 ≤200ms 淡入淡出。

---

## 7. 无障碍与对比度（Accessibility）

- **文本对比**：`--txt #E6EDF3` 于 `--panel #171C24` ≈ 13:1（远超 AA 4.5 / AAA 7）；`--txt-sub` 仅用于 ≥16px 说明（≈4.6:1，达 AA）；正文一律 `--txt`。
- **不依赖颜色 alone**：选中=环+`✓`；锁定=锁图标+说明；错误=`⚠`+红边+抖动；倍率/胜负=文字标签+颜色（呼应 HUD 红/蓝 X5）。
- **色盲友好**：英雄主题色避开红/绿语义；状态红(`--bad`)/绿(`--good`)必配图标；HUD 红蓝球倍率沿用截图「标签+形状」双编码。
- **触控**：最小触控 72px（图标钮 64 加 8 内边距）；组件间距 ≥12px，避免误触。
- **焦点可见**：键盘/手柄聚焦显示 3px `--accent` 偏移环；榜单/科技槽支持方向键导航。
- **文本缩放**：TMP fallback 保证缺字；字号随 Canvas Scaler 等比，禁止固定死尺寸。
- **双语**：中文 PixelChinese 为主，英文 ArcadeTitle/PixelBody 作副标，均不承载玩法规则。

---

## 8. 切图与资产清单（Asset Slice Recommendations）

> 供 UI 实现导出。格式：PNG（9-slice 标 9）或 SVG（图标）；霓虹发光优先用**材质/Shader** 实现而非烘焙，便于状态切换。优先级 P0 = 本版 3 界面必需。

| 资产 | 类型 | 尺寸 | 格式 | 状态变体 | 说明 |
|---|---|---|---|---|---|
| `ui_hex_frame` | 9-slice | 256 | PNG | thin / strong | 面板六角切角外框 |
| `ui_glow_border` | Shader/Mat | — | — | normal/hover/sel/err | 霓虹描边发光（参数化） |
| `btn_enter` | 9-slice | 320×96 | PNG | normal/hover/pressed/disabled | 主 CTA 渐变填充 |
| `btn_ghost` | 9-slice | 240×80 | PNG | normal/hover/pressed/disabled | 返回/退出幽灵钮 |
| `btn_confirm` | 9-slice | 280×88 | PNG | normal/hover/sel | 确认/锁定 |
| `icon_arrow_back` / `icon_close` | SVG | 64 | SVG/PNG | normal/hover | 线性霓虹 |
| `icon_lock` / `icon_unlock` | SVG | 64 | SVG/PNG | — | 六边形锁 |
| `icon_check` / `icon_warn` / `icon_balance` / `icon_star` | SVG | 64 | SVG/PNG | — | 状态/功能 |
| `hero_dim_{count,speed,pierce,refract}` | SVG | 96 | SVG/PNG | — | 维度图标 |
| `phase_badge_P1..P5` | SVG/shader | 72 | — | — | 相位 hex 徽章（色阶） |
| `medal_{gold,silver,bronze}` | SVG | 64 | SVG/PNG | — | 名次 tint |
| `card_hero_frame` | 9-slice | 432×360 | PNG | normal/sel | 英雄卡框 |
| `card_tech_slot` / `card_tech_opt` | 9-slice | — | PNG | normal/hover/on | 科技槽/选项 |
| `card_leader_row` | 9-slice | 904×96 | PNG | normal/sel/me | 榜单行 |
| `panel_center` / `popup_bg` | 9-slice | — | PNG | — | 居中面板/弹层 |
| `scrollbar_thumb` / `scrollbar_track` | SVG | 8×64 | PNG | — | 发光滚动条 |
| `bg_hex_grid` | tile | 256 | PNG | — | 平铺六边形网格(4%) |
| `bg_space_gradient` | 贴图 | 1000×1600 | PNG | — | 径向深空渐变 |
| `deco_hex_rivet` | SVG | 24 | PNG | — | 面板铆钉 |
| `fx_select_pulse` / `fx_glow_spark` | 粒子 | — | — | — | 选中脉冲/火花 |
| TMP 字体资产 ×4 + Fallback | Asset | — | .asset | — | 见 §1.2 |

**导出约定**：
- 9-slice 标注边距；发光层与底色分层（便于改色）。
- 图标统一 3px 霓虹描边、圆角端点、视框 64。
- 所有色值引用 §1.1 Token，禁止硬编码截图取样色。

---

## 9. 数据模型挂接（供 UX / 工程对齐）

视觉组件到 `v0.3 UI 参考原型.html` 数据的映射：

| 界面 | 数据源 | 关键字段 |
|---|---|---|
| 英雄选择 | `HEROES[]` | `id, name, dimension, coreResource, coreItem, phases[P1..P5].{phi,text}` |
| 科技配装 | `TECHS[]`（按 `h==heroId && s==phase` 过滤） | `k(Default/Advanced), n, c(花费), net(+30), fx[.{item,dir(Enhance/Weaken),val}], mech` |
| 价值矩阵 | `ITEMS[]` + 选中 `fx` 聚合 | `name, vw(权重)`；聚合 增益/削弱/净 |
| 排行榜 | 排名服务 | `Rank, RunnerName, HeroId, Rating, WinRate`；当前玩家高亮 |
| 全局 | `META` | `CurrencyWin=12, CurrencyLoss=4, NetOffsetBudget=30, DropOffsetCap=10` |

> 注：P0 界面产出的 `PhaseMatchLoadout{HeroId, TechIds[5]}` 当前无运行时消费方（见 UI 补充计划 §4 雷区 4）；视觉层先按可测交付，绑定留待运行时就绪。

---

## 10. 待用户拍板 / 开放问题

1. **英雄主题色方向 A vs B**（§1.4）：默认 A（光谱区分）。请确认或指定。
2. **排行榜 Enter 语义**：本规格定 `返回竞技大厅`（主 CTA 离开榜单）；若产品希望 Enter=刷新榜单 / 定位我的名次，请告知。
3. **蓝绿面板辉光强度**：主菜单截图偏「蓝绿」，本规格以 `--accent-2 #2EE6C0` 表达；强度可按实机微调。
4. **科技花费来源**：进阶科技 `c=15` 币与 `META.TechUnlockCost=15` 同量级，建议复用；解锁货币是否即 EP（工程点）请 UX 确认。
5. **是否纳入「减少动态」开关**：建议设置项提供（§6 已预留）。

---

## 11. 验收要点（视觉侧）

- 三界面均含显式 **Enter** 与 **Exit/Back** 按钮（科技配装另含 **Confirm**）。
- 颜色 100% 取自 §1.1 Token；无截图硬编码色。
- 字体仅用 §1.2 四角色 + Fallback；中文无 `□`。
- 布局锁定 10:16 / 1000×1600，安全区内嵌；关键按钮不越出视口。
- 所有状态（normal/hover/selected/disabled/error/locked/unlocked）有明确视觉与图标双编码。
- 对比度达 AA；触控 ≥72px；支持减少动态。
