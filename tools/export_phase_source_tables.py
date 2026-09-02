#!/usr/bin/env python3
"""从 v0.3 草稿 JSON 提取 5 张 DT_Phase* 表，写入 Assets/Config/ 源表文件，并做数值校验。

用法: python tools/export_phase_source_tables.py
产出: Assets/Config/DT_PhaseHero.json / DT_PhaseTech.json / DT_PhaseItem.json /
       DT_PhaseCurve.json / DT_PhaseMeta.json
"""
from __future__ import annotations

import json
import sys
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[1]
DRAFT = REPO_ROOT / "doc" / "方案与数据" / "Gatebreaker Arena 1v1 双向砖潮 规则配置草稿 v0.3.json"
CONFIG_ROOT = REPO_ROOT / "Assets" / "Config"

PHASE_TABLES = [
    "DT_PhaseHero",
    "DT_PhaseTech",
    "DT_PhaseItem",
    "DT_PhaseCurve",
    "DT_PhaseMeta",
]

EXPECTED_PHI_TO_REACH = [0, 30, 50, 70, 100]
EXPECTED_VALUE_WEIGHTS = {"ItemPierce": 3, "ItemSplit": 3, "ItemDamp": 3, "ItemSpeed": 2, "ItemWide": 2, "ItemMagnet": 1}


def _fail(msg: str) -> None:
    print(f"[error] {msg}")
    sys.exit(1)


def main() -> int:
    draft = json.loads(DRAFT.read_text(encoding="utf-8"))

    for table in PHASE_TABLES:
        if table not in draft:
            _fail(f"草稿缺少表 {table}")

    # --- 提取 ---
    tables = {table: draft[table] for table in PHASE_TABLES}

    # --- 校验 ---
    heroes = tables["DT_PhaseHero"]
    techs = tables["DT_PhaseTech"]
    items = tables["DT_PhaseItem"]
    curves = tables["DT_PhaseCurve"]
    metas = tables["DT_PhaseMeta"]

    errors: list[str] = []

    # 英雄
    hero_ids = [h["HeroId"] for h in heroes]
    if len(heroes) != 4:
        errors.append(f"DT_PhaseHero 应为 4 英雄，实际 {len(heroes)}")
    if len(set(hero_ids)) != len(hero_ids):
        errors.append("DT_PhaseHero 存在重复 HeroId")
    for h in heroes:
        levels = h.get("PhaseLevels", [])
        if len(levels) != 5:
            errors.append(f"{h['HeroId']} PhaseLevels 应为 5，实际 {len(levels)}")
            continue
        phi = [lv.get("PhiToReach") for lv in levels]
        if phi != EXPECTED_PHI_TO_REACH:
            errors.append(f"{h['HeroId']} PhiToReach 应为 {EXPECTED_PHI_TO_REACH}，实际 {phi}")
        for lv in levels:
            if lv.get("PhaseLevel") not in {"P1", "P2", "P3", "P4", "P5"}:
                errors.append(f"{h['HeroId']} 存在非法 PhaseLevel {lv.get('PhaseLevel')}")
            if lv.get("Nature") not in {"Identity", "Scale", "Active", "Protocol", "Climax"}:
                errors.append(f"{h['HeroId']} {lv.get('PhaseLevel')} 非法 Nature {lv.get('Nature')}")

    # 科技：净偏移守恒（预算 D=+30）
    if len(techs) != 60:
        errors.append(f"DT_PhaseTech 应为 60 科技，实际 {len(techs)}")
    for t in techs:
        if t.get("NetOffset") != 30:
            errors.append(f"{t['TechId']} NetOffset 应为 30，实际 {t.get('NetOffset')}")
        if t.get("Kind") not in {"Default", "Advanced"}:
            errors.append(f"{t['TechId']} 非法 Kind {t.get('Kind')}")
        for e in t.get("Effects", []):
            if e.get("Op") not in {"Enhance", "Weaken"}:
                errors.append(f"{t['TechId']} 非法 Op {e.get('Op')}")

    # 道具
    if len(items) != 9:
        errors.append(f"DT_PhaseItem 应为 9 道具，实际 {len(items)}")
    for it in items:
        w = it.get("ValueWeight")
        if w != EXPECTED_VALUE_WEIGHTS.get(it.get("ItemId")):
            errors.append(f"{it['ItemId']} ValueWeight 应为 {EXPECTED_VALUE_WEIGHTS.get(it.get('ItemId'))}，实际 {w}")
    drop_total = sum(float(it.get("BaseDropWeight", 0.0)) for it in items)
    if abs(drop_total - 1.0) > 0.0001:
        errors.append(f"DT_PhaseItem BaseDropWeight 合计应=1.0，实际 {drop_total}")

    # 曲线：6 档权重合计=1
    if len(curves) != 1:
        errors.append(f"DT_PhaseCurve 应为 1 条，实际 {len(curves)}")
    for c in curves:
        stages = c.get("Stages", [])
        if len(stages) != 6:
            errors.append(f"{c['RuleId']} Stages 应为 6 档，实际 {len(stages)}")
        for s in stages:
            total = sum(float(s.get(k, 0.0)) for k in ("GreenWeight", "YellowWeight", "RedWeight", "MysteryWeight"))
            if abs(total - 1.0) > 0.0001:
                errors.append(f"{c['RuleId']} {s.get('Stage')} 权重合计应=1.0，实际 {total}")

    # 元信息
    if len(metas) != 1:
        errors.append(f"DT_PhaseMeta 应为 1 条，实际 {len(metas)}")
    meta = metas[0] if metas else {}
    for key, expect in (("NetOffsetBudget", 30), ("DropOffsetCap", 10), ("PhiPerSecondCap", 6), ("ScissorDiffTargetSeconds", 40), ("CurrencyWin", 12), ("CurrencyLoss", 4), ("TechUnlockCost", 15)):
        if meta.get(key) != expect:
            errors.append(f"DT_PhaseMeta {key} 应为 {expect}，实际 {meta.get(key)}")

    if errors:
        for e in errors:
            print(f"[error] {e}")
        return 1

    # --- 写源表 ---
    CONFIG_ROOT.mkdir(parents=True, exist_ok=True)
    for table in PHASE_TABLES:
        out = CONFIG_ROOT / f"{table}.json"
        out.write_text(json.dumps(tables[table], ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
        print(f"[info] wrote {out} ({len(tables[table])} rows)")

    print("[info] phase source tables exported and validated OK.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
