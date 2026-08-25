# -*- coding: utf-8 -*-
"""Merge P4 protocol options into the P4 tech slot (one-off data migration).

Per the user's decision, DT_PhaseHero's P4 `ProtocolOptions` (the "二选一
协议") is removed, and each hero's two protocol effects are folded into that
hero's two P4 *advanced* techs as a new `MechanicEffect` string. P4 becomes a
plain Scale phase; the tech slot stays 3 options (base + 2 advanced).

The protocol effect text is read straight from DT_PhaseHero (no hand-copy),
mapped in order: protocol[0] -> P4 advanced tech[0], protocol[1] -> tech[1].
"""
import json
import os

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
CFG = os.path.join(ROOT, "Assets", "Config")

HERO_PATH = os.path.join(CFG, "DT_PhaseHero.json")
TECH_PATH = os.path.join(CFG, "DT_PhaseTech.json")

# order matches the ProtocolOptions order in DT_PhaseHero for each hero
P4_TECH_ORDER = {
    "HERO_MIRAGE": ["TECH_MIRAGE_P4_HIVE", "TECH_MIRAGE_P4_HOMECOMING"],
    "HERO_PULSE": ["TECH_PULSE_P4_MAXSPEED", "TECH_PULSE_P4_BRAKE"],
    "HERO_RIFT": ["TECH_RIFT_P4_DRILLDOWN", "TECH_RIFT_P4_WIDEFIELD"],
    "HERO_REFRACT": ["TECH_REFRACT_P4_INFINITE", "TECH_REFRACT_P4_GRAVITY"],
}

P4_TEXT = {
    "HERO_MIRAGE": "球群进阶强化（P4 科技槽）",
    "HERO_PULSE": "节拍进阶强化（P4 科技槽）",
    "HERO_RIFT": "穿透进阶强化（P4 科技槽）",
    "HERO_REFRACT": "弹道进阶强化（P4 科技槽）",
}

with open(HERO_PATH, encoding="utf-8") as f:
    heroes = json.load(f)
with open(TECH_PATH, encoding="utf-8") as f:
    techs = json.load(f)

tech_by_id = {t["TechId"]: t for t in techs}
merge_map = {}

for hero in heroes:
    hid = hero["HeroId"]
    p4 = next(pl for pl in hero["PhaseLevels"] if pl["PhaseLevel"] == "P4")
    opts = p4.get("ProtocolOptions", [])
    tech_ids = P4_TECH_ORDER[hid]
    assert len(opts) == 2, (hid, opts)
    assert len(tech_ids) == 2
    for tech_id, opt in zip(tech_ids, opts):
        assert tech_id in tech_by_id, tech_id
        merge_map[tech_id] = opt["EffectText"]
    # strip protocol data from the hero's P4 phase level
    p4.pop("ProtocolOptions", None)
    p4["Nature"] = "Scale"
    p4["EffectText"] = P4_TEXT[hid]

for tech_id, effect_text in merge_map.items():
    tech_by_id[tech_id]["MechanicEffect"] = effect_text

with open(HERO_PATH, "w", encoding="utf-8") as f:
    json.dump(heroes, f, ensure_ascii=False, indent=2)
    f.write("\n")
with open(TECH_PATH, "w", encoding="utf-8") as f:
    json.dump(techs, f, ensure_ascii=False, indent=2)
    f.write("\n")

# sanity
assert all("ProtocolOptions" not in next(pl for pl in h["PhaseLevels"] if pl["PhaseLevel"] == "P4") for h in heroes)
assert all(next(pl for pl in h["PhaseLevels"] if pl["PhaseLevel"] == "P4")["Nature"] == "Scale" for h in heroes)
assert len(techs) == 60
p4_techs = [t for t in techs if t["SlotPhase"] == "P4" and t["Kind"] == "Advanced"]
assert len(p4_techs) == 8 and all("MechanicEffect" in t for t in p4_techs)

print("Merged", len(merge_map), "protocol effects into P4 advanced techs:")
for tid, txt in merge_map.items():
    print(f"  {tid:<28} -> {txt}")
print("OK: hero P4 now Scale (no ProtocolOptions);", len(techs), "techs;",
      len(p4_techs), "P4 advanced techs carry MechanicEffect.")
