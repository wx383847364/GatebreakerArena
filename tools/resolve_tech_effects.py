# -*- coding: utf-8 -*-
"""
Gatebreaker Arena v0.3 — 科技效果参数解析脚本
- 把 DT_PhaseTech.json 中 Effects 解析为具体 before->after 参数
- 整数型道具（裂穿 ItemPierce / 磁吸 ItemMagnet）用 MagnitudeStep 离散步进（±N 次/个），不做百分比缩放
- 连续型道具（分形 / 缓滞 / 疾风 / 广域）用 MagnitudePercent 百分比缩放
- P4 阶段不带入机制：移除 P4 任何档位的 MechanicEffect，仅保留基础数值效果
- 重新生成 design/tech/Gatebreaker Arena v0.3 科技清单.md
向后兼容：只新增字段(Resolved*)，不动既有字段；exporter 透传新字段。
"""
import json, os

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DT_PATH = os.path.join(ROOT, "Assets", "Config", "DT_PhaseTech.json")
MD_PATH = os.path.join(ROOT, "design", "tech", "Gatebreaker Arena v0.3 科技清单.md")

# 每个道具的主参数解析规则
#   kind = "count"     -> 整数型：MagnitudeStep 离散步进（±N 次/个），不缩放
#   kind = "duration"/"pct" -> 连续型：MagnitudePercent 百分比缩放
ITEM_RULES = {
    "ItemPierce": {"label": "穿透次数", "base": 2, "unit": "次", "kind": "count", "duration": None},
    "ItemSplit":  {"label": "临时球持续", "base": 6, "unit": "s", "kind": "duration", "duration": None},
    "ItemDamp":   {"label": "砖潮减速", "base": 20, "unit": "%", "kind": "pct", "duration": 6},
    "ItemSpeed":  {"label": "球速增幅", "base": 20, "unit": "%", "kind": "pct", "duration": 5},
    "ItemWide":   {"label": "挡板增幅", "base": 20, "unit": "%", "kind": "pct", "duration": 8},
    "ItemMagnet": {"label": "自动吸附道具数", "base": 1, "unit": "个", "kind": "count", "duration": None},
}

# P4 阶段不带入机制：仅为基础数值效果偏移，与其它相位一致
HERO_NAMES = {"HERO_MIRAGE": "蜃影", "HERO_PULSE": "脉冲", "HERO_RIFT": "裂痕", "HERO_REFRACT": "折光"}
HERO_CORE = {"HERO_MIRAGE": "蜃核", "HERO_PULSE": "脉核", "HERO_RIFT": "裂核", "HERO_REFRACT": "折核"}

HERO_DIM = {
    "HERO_MIRAGE": "数量·分形", "HERO_PULSE": "速度·疾风",
    "HERO_RIFT": "穿透·裂穿", "HERO_REFRACT": "弹道·广域",
}
PHASE_LABEL = {"P1": "P1 身份", "P2": "P2 蓄势", "P3": "P3 转折", "P4": "P4 共鸣", "P5": "P5 收束"}


def resolve(rule, eff):
    """返回 (modified_value, value_str, delta_str)。"""
    base = rule["base"]
    op = eff["Op"]
    if rule["kind"] == "count":
        # MagnitudeStep 已含符号：Enhance 为正、Weaken 为负
        step = eff.get("MagnitudeStep", 0)
        val = max(0, base + step)
        delta_str = f"{'+' if step > 0 else ''}{step}{rule['unit']}"
        return val, f"{val}{rule['unit']}", delta_str
    pct = eff.get("MagnitudePercent", 0)
    factor = (1 + pct / 100.0) if op == "Enhance" else (1 - pct / 100.0)
    val = round(base * factor, 1)
    return val, f"{val}{rule['unit']}", f"{'增强' if op == 'Enhance' else '削弱'}{pct}%"


def main():
    with open(DT_PATH, "r", encoding="utf-8") as f:
        data = json.load(f)

    for tech in data:
        # P4 阶段不带入机制：任何档位都移除 MechanicEffect，仅保留基础数值效果
        if tech.get("SlotPhase") == "P4":
            tech.pop("MechanicEffect", None)

        new_effects = []
        for eff in tech.get("Effects", []):
            rule = ITEM_RULES.get(eff["ItemId"])
            if not rule:
                new_effects.append(eff)
                continue
            mod_val, mod_str, delta_str = resolve(rule, eff)
            dur = f"，持续 {rule['duration']}s" if rule["duration"] else ""
            eff["ParamLabel"] = rule["label"]
            eff["BaseValue"] = rule["base"]
            eff["ModifiedValue"] = mod_val
            eff["Unit"] = rule["unit"]
            eff["ResolvedText"] = (
                f"{eff['ItemName']} {rule['label']} {rule['base']}{rule['unit']} "
                f"→ {mod_str}（{delta_str}）{dur}"
            )
            new_effects.append(eff)
        tech["Effects"] = new_effects

    with open(DT_PATH, "w", encoding="utf-8") as f:
        json.dump(data, f, ensure_ascii=False, indent=2)
        f.write("\n")

    gen_markdown(data)
    print("OK: resolved effects (count=step / continuous=percent) + P4 base mechanic written; markdown regenerated.")


def gen_markdown(data):
    by_hero = {}
    for t in data:
        by_hero.setdefault(t["HeroId"], []).append(t)

    lines = []
    lines.append("# Gatebreaker Arena v0.3 科技清单（具体参数版）\n")
    lines.append("> 规则：每英雄 5 相位槽（P1–P5）× 每槽 3 选项（1 基准 + 2 进阶）= 15 项/英雄、60 项/全游戏。\n")
    lines.append("> 基准免费且默认填装，进阶需 15 币解锁；每张科技 `NetOffset` 恒 = +30（每槽预算 +30 守恒，无 Σ 上限）。\n")
    lines.append("> 效果已解析为**具体参数 before→after**（解析规则见文末）。\n")
    lines.append("> **P4 阶段不带入机制**：与其它相位一致，仅为基础数值效果偏移，无任何协议/机制文本。\n")

    for hid in ["HERO_MIRAGE", "HERO_PULSE", "HERO_RIFT", "HERO_REFRACT"]:
        name = HERO_NAMES[hid]
        lines.append(f"\n## {name}（{HERO_DIM[hid]}）\n")
        techs = sorted(by_hero[hid], key=lambda t: (t["SlotPhase"], t["Kind"] != "Default"))
        for t in techs:
            core_tag = HERO_CORE[hid] if t["Kind"] == "Default" else ""
            kind_tag = "基准" if t["Kind"] == "Default" else "进阶"
            cost = "免费" if t["CostCurrency"] == 0 else f"{t['CostCurrency']}币"
            title = f"**{t['DisplayName']}**" + (f"（{core_tag}）" if core_tag else "")
            lines.append(f"\n### {PHASE_LABEL[t['SlotPhase']]} · {title} 〔{kind_tag}·{cost}·NetOffset+{t['NetOffset']}〕\n")
            for eff in t["Effects"]:
                lines.append(f"- {eff.get('ResolvedText', eff['ItemName'])}\n")
            if t.get("MechanicEffect"):
                lines.append(f"- 🔧 机制：{t['MechanicEffect']}\n")

    lines.append("\n---\n")
    lines.append("## 参数解析规则\n")
    lines.append("| 道具 | 主参数 | 基准 | 映射 | 固定时长 |\n|---|---|---:|---|---:|\n")
    for iid, r in ITEM_RULES.items():
        dur = f"{r['duration']}s" if r["duration"] else "—"
        mapping = f"±N{r['unit']} 离散步进" if r["kind"] == "count" else "×(1±%)"
        lines.append(f"| {r['label']} | {r['label']} | {r['base']}{r['unit']} | {mapping} | {dur} |\n")
    lines.append("\n> 整数型道具（裂穿穿透次数、磁吸吸附个数）用 `MagnitudeStep` 离散步进（±N 次/个），不做百分比缩放；")
    lines.append("> 连续型道具（分形/缓滞/疾风/广域）用 `MagnitudePercent` 百分比缩放。\n")
    lines.append("> 分形主参数取「临时球持续」而非数量，避免 1.5 颗球的歧义。\n")
    lines.append("> 缓滞为防守道具（减速己方砖潮推进），增强=更强防守，削弱=代价。\n")

    with open(MD_PATH, "w", encoding="utf-8") as f:
        f.write("".join(lines))


if __name__ == "__main__":
    main()
