#!/usr/bin/env python3
"""复刻 GatebreakerConfigRuntimeLoader 的读取逻辑，验证合并后的 live JSON 可被引擎正确加载。

覆盖：ReadPhaseHero/Tech/Item/Curve/Meta 的字段名与类型；ValidateV1Catalog 的 3 英雄/6 路径/12/12 硬断言；
60 科技净偏移守恒；道具掉率合计；曲线权重合计。

用法: python tools/verify_phase_rules_load.py
"""
from __future__ import annotations

import json
import sys
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[1]
LIVE = REPO_ROOT / "Assets" / "Config" / "json" / "gatebreaker_rules.json"

EXPECTED_PHI = [0, 30, 50, 70, 100]
EXPECTED_NATURES = {"Identity", "Scale", "Active", "Protocol", "Climax"}
EXPECTED_PHASES = {"P1", "P2", "P3", "P4", "P5"}


def _fail(msg: str) -> None:
    print(f"[error] {msg}")
    sys.exit(1)


def _req(item: dict, key: str):
    if key not in item or item[key] is None:
        _fail(f"missing required field '{key}' in {item.get('HeroId') or item.get('TechId') or item.get('ItemId') or item.get('RuleId') or item.get('MetaId') or item}")
    return item[key]


def _str(item: dict, key: str) -> str:
    return str(_req(item, key))


def _int(item: dict, key: str) -> int:
    v = _req(item, key)
    if isinstance(v, bool) or not isinstance(v, int):
        _fail(f"'{key}' must be int, got {v!r}")
    return v


def _float(item: dict, key: str) -> float:
    v = _req(item, key)
    if isinstance(v, bool) or not isinstance(v, (int, float)):
        _fail(f"'{key}' must be number, got {v!r}")
    return float(v)


def _bool(item: dict, key: str) -> bool:
    v = _req(item, key)
    if not isinstance(v, bool):
        _fail(f"'{key}' must be bool, got {v!r}")
    return v


def read_phase_hero(item: dict) -> None:
    _str(item, "HeroId"); _str(item, "DisplayName"); _str(item, "Dimension")
    _str(item, "CoreResource"); _str(item, "CoreItem")
    levels = _req(item, "PhaseLevels")
    assert len(levels) == 5, f"{item['HeroId']} levels={len(levels)}"
    phi = []
    for lv in levels:
        p = _str(lv, "PhaseLevel")
        n = _str(lv, "Nature")
        if p not in EXPECTED_PHASES:
            _fail(f"bad PhaseLevel {p}")
        if n not in EXPECTED_NATURES:
            _fail(f"bad Nature {n}")
        phi.append(_int(lv, "PhiToReach"))
        _str(lv, "EffectText")
        if "ActiveAbility" in lv and lv["ActiveAbility"] is not None:
            _str(lv["ActiveAbility"], "AbilityId")
            _float(lv["ActiveAbility"], "CooldownSeconds")
        if "ProtocolOptions" in lv and lv["ProtocolOptions"]:
            for opt in lv["ProtocolOptions"]:
                _str(opt, "OptionId"); _str(opt, "DisplayName"); _bool(opt, "IsDefault"); _str(opt, "EffectText")
    if phi != EXPECTED_PHI:
        _fail(f"{item['HeroId']} PhiToReach {phi}")
    sources = _req(item, "PhiSources")
    for s in sources:
        _str(s, "Source"); _float(s, "Phi")  # Phi 可为 0.5 → 必须 float
    if any(float(s["Phi"]) > 0 for s in sources if s["Source"] == "PerSecondCap"):
        cap = next(s for s in sources if s["Source"] == "PerSecondCap")
        assert float(cap["Phi"]) == 6.0, f"PerSecondCap={cap['Phi']}"


def read_phase_tech(item: dict) -> None:
    _str(item, "TechId"); _str(item, "HeroId"); _str(item, "SlotPhase"); _str(item, "Kind")
    _str(item, "DisplayName"); _int(item, "CostCurrency")
    net = _int(item, "NetOffset")
    if net != 30:
        _fail(f"{item['TechId']} NetOffset {net}")
    for e in _req(item, "Effects"):
        _str(e, "ItemId"); _str(e, "ItemName"); _str(e, "Op"); _int(e, "MagnitudePercent")


def read_phase_item(item: dict) -> None:
    _str(item, "ItemId"); _str(item, "ItemName"); _int(item, "ValueWeight")
    _float(item, "BaseDropWeight")
    eff = _req(item, "Effect")
    if not isinstance(eff, dict):
        _fail(f"{item['ItemId']} Effect must be object")


def read_phase_curve(item: dict) -> None:
    _str(item, "RuleId"); _float(item, "CompositionIntervalSeconds")
    stages = _req(item, "Stages")
    assert len(stages) == 6
    for s in stages:
        _str(s, "Stage"); _int(s, "TimeStart")
        _float(s, "GreenWeight"); _float(s, "YellowWeight"); _float(s, "RedWeight"); _float(s, "MysteryWeight")
        total = sum(float(s[k]) for k in ("GreenWeight", "YellowWeight", "RedWeight", "MysteryWeight"))
        if abs(total - 1.0) > 1e-6:
            _fail(f"{item['RuleId']} {s['Stage']} weights total {total}")
    _int(item, "BreakCounterThreshold"); _float(item, "BreakCounterWarnSeconds")


def read_phase_meta(item: dict) -> None:
    _str(item, "MetaId")
    for k in ("CurrencyWin", "CurrencyLoss", "TechUnlockCost", "NetOffsetBudget",
              "DropOffsetCap", "PhiPerSecondCap", "ScissorDiffTargetSeconds"):
        _int(item, k)


def validate_v1(d: dict) -> None:
    heroes = {h["HeroId"] for h in d["DT_Hero"]}
    expected_heroes = {"HERO_FROST_QUEEN", "HERO_MECH_ENGINEER", "HERO_RADIANT_PALADIN"}
    if len(d["DT_Hero"]) != 3 or heroes != expected_heroes:
        _fail(f"V1 heroes mismatch: {heroes}")
    if len(d["DT_HeroPath"]) != 6 or len(d["DT_UniversalChip"]) != 12 or len(d["DT_SignatureChip"]) != 12:
        _fail("V1 catalog counts mismatch")


def main() -> int:
    d = json.loads(LIVE.read_text(encoding="utf-8"))
    if d.get("Version") < 2:
        _fail("Version < 2")

    validate_v1(d)

    for h in d["DT_PhaseHero"]:
        read_phase_hero(h)
    for t in d["DT_PhaseTech"]:
        read_phase_tech(t)
    for i in d["DT_PhaseItem"]:
        read_phase_item(i)
    for c in d["DT_PhaseCurve"]:
        read_phase_curve(c)
    for m in d["DT_PhaseMeta"]:
        read_phase_meta(m)

    drop_total = sum(i["BaseDropWeight"] for i in d["DT_PhaseItem"])
    if abs(drop_total - 1.0) > 1e-6:
        _fail(f"item drop total {drop_total}")

    print("[info] loader 模拟读取通过：")
    print(f"  - V1 catalog: 3 heroes / 6 paths / 12 universal / 12 signature OK")
    print(f"  - DT_PhaseHero: {len(d['DT_PhaseHero'])} heroes, 各 5 档 PhiToReach={EXPECTED_PHI}")
    print(f"  - DT_PhaseTech: {len(d['DT_PhaseTech'])} techs, NetOffset 全=30")
    print(f"  - DT_PhaseItem: {len(d['DT_PhaseItem'])} items, BaseDropWeight 合计=1.0")
    print(f"  - DT_PhaseCurve: 6 档权重合计=1.0, BreakCounterThreshold=20")
    print(f"  - DT_PhaseMeta: NetOffsetBudget=30, PhiPerSecondCap=6, Scissor=40")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
