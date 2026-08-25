# -*- coding: utf-8 -*-
"""重组科技清单：每个相位先列「阶段效果」，再列该相位 3 个科技（基准 + 2 进阶）。

数据源：
  - DT_PhaseHero.json : 英雄维度 / 核心道具 / 各相位阶段效果 EffectText
  - DT_PhaseTech.json : 科技参数化 ResolvedText（before→after）
  - DT_PhaseItem.json : ItemId -> ItemName（核心道具中文名）
输出：
  - design/tech/Gatebreaker Arena v0.3 科技清单.md（覆盖）
"""
import json, os

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
# 本脚本位于 tools/，项目根 = 上两级
PROJ = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))


def load(p):
    return json.load(open(os.path.join(PROJ, p), encoding="utf-8"))


PHASE_NATURE = {"P1": "身份", "P2": "蓄势", "P3": "转折", "P4": "共鸣", "P5": "收束"}
HERO_ALIAS = {"HERO_MIRAGE": "蜃核", "HERO_PULSE": "脉核", "HERO_RIFT": "裂核", "HERO_REFRACT": "折核"}


def main():
    items = {it["ItemId"]: it["ItemName"] for it in load("Assets/Config/DT_PhaseItem.json")}
    heroes = load("Assets/Config/DT_PhaseHero.json")
    techs = load("Assets/Config/DT_PhaseTech.json")

    # 阶段效果：(hero, phase) -> EffectText
    phase_effect = {}
    hero_order = []
    for h in heroes:
        hid = h["HeroId"]
        hero_order.append(hid)
        for ph in h["PhaseLevels"]:
            phase_effect[(hid, ph["PhaseLevel"])] = ph["EffectText"]

    # 科技：按 (hero, phase) 分组，保持列表顺序（基准 Default 在前）
    groups = {}
    for t in techs:
        groups.setdefault((t["HeroId"], t["SlotPhase"]), []).append(t)
    for k in groups:
        groups[k].sort(key=lambda x: 0 if x["Kind"] == "Default" else 1)

    # 保留原清单末尾「参数解析规则」段
    src_path = os.path.join(PROJ, "design/tech/Gatebreaker Arena v0.3 科技清单.md")
    tail = ""
    if os.path.exists(src_path):
        raw = open(src_path, encoding="utf-8").read()
        if "\n---\n" in raw:
            tail = raw.split("\n---\n", 1)[1]

    out = []
    out.append("# Gatebreaker Arena v0.3 科技清单（具体参数版）")
    out.append("> 规则：每英雄 5 相位槽（P1–P5）× 每槽 3 选项（1 基准 + 2 进阶）= 15 项/英雄、60 项/全游戏。")
    out.append("> 基准免费且默认填装，进阶需 15 币解锁；每张科技 `NetOffset` 恒 = +30（每槽预算 +30 守恒，无 Σ 上限）。")
    out.append("> 百分数已解析为**具体参数 before→after**（解析规则见文末）。")
    out.append("> **每个相位先列「阶段效果」（来自相位成长），其下为该相位 3 个科技的具体参数**——阶段效果即该相位 3 个科技共享的上下文。")
    out.append("> **P4 阶段不带入机制**：与其它相位一致，仅为基础数值效果偏移，无任何协议/机制文本。")
    out.append("")

    for hid in hero_order:
        h = next(x for x in heroes if x["HeroId"] == hid)
        dim = h["Dimension"]
        core = items.get(h["CoreItem"], h["CoreItem"])
        out.append("## %s（%s·%s）" % (h["DisplayName"], dim, core))
        out.append("")
        for ph in ["P1", "P2", "P3", "P4", "P5"]:
            eff = phase_effect.get((hid, ph), "")
            out.append("### %s %s 〔基准免费·进阶15币·NetOffset+30〕" % (ph, PHASE_NATURE[ph]))
            out.append("> **阶段效果**：%s" % eff)
            out.append("")
            alias = HERO_ALIAS.get(hid, "")
            for t in groups.get((hid, ph), []):
                name = t["DisplayName"]
                if t["Kind"] == "Default" and alias:
                    name = "基准（%s）" % alias
                resolved = [e["ResolvedText"] for e in t["Effects"]]
                line = "- **%s**：%s" % (name, "；".join(resolved))
                out.append(line)
            out.append("")

    if tail:
        out.append("---\n" + tail.rstrip())

    text = "\n".join(out).rstrip() + "\n"
    with open(src_path, "w", encoding="utf-8") as f:
        f.write(text)
    print("written:", src_path)
    print("heroes=%d techs=%d" % (len(hero_order), len(techs)))


if __name__ == "__main__":
    main()
