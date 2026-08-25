#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""生成《英雄相位阶段与科技总表》。
数据源：Assets/Config/DT_PhaseHero.json（相位阶段效果）+ DT_PhaseTech.json（科技参数化效果）。
输出：design/tech/Gatebreaker Arena v0.3 英雄相位与科技总表.md
纯原型层文档整理，不改动任何游戏配置。
"""
import json
import os

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
HERO_JSON = os.path.join(ROOT, "Assets", "Config", "DT_PhaseHero.json")
TECH_JSON = os.path.join(ROOT, "Assets", "Config", "DT_PhaseTech.json")
OUT = os.path.join(ROOT, "design", "tech", "Gatebreaker Arena v0.3 英雄相位与科技总表.md")

# 英雄顺序（与设计文档一致）
HERO_ORDER = ["HERO_MIRAGE", "HERO_PULSE", "HERO_RIFT", "HERO_REFRACT"]
# 基准科技别名（用于图标生成，见科技清单/图标对照表）
BASE_ALIAS = {"HERO_MIRAGE": "蜃核", "HERO_PULSE": "脉核", "HERO_RIFT": "裂核", "HERO_REFRACT": "折核"}
# 相位中文俗称（与科技清单.md 统一：P4 俗称“共鸣”，其 Nature 仍为 Scale）
PHASE_CN = {"P1": "身份", "P2": "蓄势", "P3": "转折", "P4": "共鸣", "P5": "收束"}


def load_json(path):
    with open(path, "r", encoding="utf-8") as f:
        return json.load(f)


def main():
    heroes = load_json(HERO_JSON)
    techs = load_json(TECH_JSON)

    hero_map = {h["HeroId"]: h for h in heroes}
    # 科技按 英雄+相位 分组，组内保持 JSON 顺序（基准在前、进阶在后）
    tech_by_hp = {}
    for t in techs:
        tech_by_hp.setdefault((t["HeroId"], t["SlotPhase"]), []).append(t)

    lines = []
    lines.append("# Gatebreaker Arena v0.3 英雄相位阶段与科技总表")
    lines.append("")
    lines.append("> 数据来源：`DT_PhaseHero.json`（相位阶段效果）+ `DT_PhaseTech.json`（科技参数化效果，经 `resolve_tech_effects.py` 解析）。")
    lines.append("> 规则：每英雄 5 相位槽（P1–P5）× 每槽 3 选项（1 基准 + 2 进阶）= 15 项/英雄、60 项/全游戏；基准免费默认填装，进阶 15 币解锁；每张科技 `NetOffset` 恒 = +30（每槽预算守恒，无 Σ 上限）。")
    lines.append("> **P4 阶段不带入机制**：与其它相位一致，仅为基础数值效果偏移，无协议/主动/被动机制。")
    lines.append("")
    lines.append("## 目录")
    for hid in HERO_ORDER:
        h = hero_map[hid]
        lines.append(f"- [{h['DisplayName']}（{h['Dimension']}·{h['CoreItem']}）](#{h['HeroId'].lower()})")
    lines.append("")

    for hid in HERO_ORDER:
        h = hero_map[hid]
        alias = BASE_ALIAS[hid]
        lines.append(f"<a id=\"{hid.lower()}\"></a>")
        lines.append(f"## {h['DisplayName']}（{hid}）")
        lines.append("")
        lines.append(f"- **维度**：{h['Dimension']}　**核心资源**：{h['CoreResource']}　**核心道具**：{h['CoreItem']}")
        lines.append("")
        # —— 相位阶段效果表 ——
        lines.append("### 相位阶段效果（P1–P5）")
        lines.append("")
        lines.append("| 相位 | 俗称 | 性质 | 达到 Φ | 阶段效果 |")
        lines.append("|---|---|---|---:|---|")
        for lv in h["PhaseLevels"]:
            p = lv["PhaseLevel"]
            nature = lv.get("Nature", "")
            phi = lv.get("PhiToReach", "")
            eff = lv.get("EffectText", "")
            lines.append(f"| {p} | {PHASE_CN.get(p, p)} | {nature} | {phi} | {eff} |")
        lines.append("")

        # —— 科技（按相位分组）——
        lines.append("### 科技（15 项，按相位分组）")
        lines.append("")
        for p in ["P1", "P2", "P3", "P4", "P5"]:
            grp = tech_by_hp.get((hid, p), [])
            lines.append(f"#### {p} {PHASE_CN.get(p, p)}")
            lines.append("")
            if not grp:
                lines.append("_（无科技数据）_")
                lines.append("")
                continue
            for t in grp:
                name = t["DisplayName"]
                kind = t["Kind"]
                cost = t["CostCurrency"]
                net = t["NetOffset"]
                if kind == "Default":
                    label = f"基准（{alias}）"
                    tag = f"基准·免费·NetOffset+{net}"
                else:
                    label = name
                    tag = f"进阶·{cost}币·NetOffset+{net}"
                effects = "；".join(e.get("ResolvedText", "") for e in t.get("Effects", []))
                lines.append(f"- **{label}** 〔{tag}〕：{effects}")
            lines.append("")

    with open(OUT, "w", encoding="utf-8") as f:
        f.write("\n".join(lines) + "\n")
    print(f"written: {OUT}")
    print(f"heroes={len(hero_map)} techs={len(techs)}")


if __name__ == "__main__":
    main()
