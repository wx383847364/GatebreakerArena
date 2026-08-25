#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
从 DT_PhaseHero.json 提取相位阶段效果，生成与科技表同构的
SkillName / SkillDesc 数据表（用于 Unity P1–P5 技能槽填表）。
输出：CSV / JSON / Markdown 三种格式。
"""
import json
import csv
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
CONFIG_DIR = ROOT / "Assets" / "Config"
OUT_DIR = ROOT / "design" / "tech"
OUT_DIR.mkdir(parents=True, exist_ok=True)

HERO_FILE = CONFIG_DIR / "DT_PhaseHero.json"

HERO_NAME_MAP = {
    "HERO_MIRAGE": "蜃影",
    "HERO_PULSE": "脉冲",
    "HERO_RIFT": "裂痕",
    "HERO_REFRACT": "折光",
}
HERO_ORDER = {hid: idx for idx, hid in enumerate(HERO_NAME_MAP.keys())}

# 相位 -> 俗称映射（按固定相位序号，与 Nature 英文相互独立）
PHASE_NICKNAME = {
    "P1": "身份",
    "P2": "蓄势",
    "P3": "转折",
    "P4": "共鸣",
    "P5": "收束",
}
PHASE_ORDER = {"P1": 1, "P2": 2, "P3": 3, "P4": 4, "P5": 5}


def load_json(path: Path):
    with open(path, encoding="utf-8") as f:
        data = json.load(f)
    return data if isinstance(data, list) else (data.get("rows") or data.get("Heroes") or [])


def build_rows(heroes: list):
    rows = []
    for hero in sorted(heroes, key=lambda x: HERO_ORDER.get(x.get("HeroId", ""), 99)):
        hero_id = hero.get("HeroId", "")
        for p in sorted(hero.get("PhaseLevels", []),
                        key=lambda x: PHASE_ORDER.get(x.get("PhaseLevel", ""), 99)):
            phase = p.get("PhaseLevel", "")
            rows.append({
                "HeroId": hero_id,
                "HeroName": HERO_NAME_MAP.get(hero_id, hero_id),
                "Phase": phase,
                "Nature": p.get("Nature", ""),
                "PhiToReach": p.get("PhiToReach", 0),
                "SkillName": PHASE_NICKNAME.get(phase, phase),
                "SkillDesc": p.get("EffectText", ""),
            })
    return rows


def write_csv(rows, path):
    fieldnames = ["HeroId", "HeroName", "Phase", "Nature", "PhiToReach", "SkillName", "SkillDesc"]
    with open(path, "w", encoding="utf-8-sig", newline="") as f:
        writer = csv.DictWriter(f, fieldnames=fieldnames)
        writer.writeheader()
        writer.writerows(rows)


def write_json(rows, path):
    nested = {}
    for row in rows:
        hid = row["HeroId"]
        nested.setdefault(hid, {
            "HeroName": row["HeroName"],
            "Phases": {}
        })
        nested[hid]["Phases"][row["Phase"]] = {
            "Nature": row["Nature"],
            "PhiToReach": row["PhiToReach"],
            "SkillName": row["SkillName"],
            "SkillDesc": row["SkillDesc"],
        }
    with open(path, "w", encoding="utf-8") as f:
        json.dump(nested, f, ensure_ascii=False, indent=2)


def write_markdown(rows, path):
    lines = [
        "# Gatebreaker Arena v0.3 相位效果 SkillName / SkillDesc 数据表",
        "",
        "> 每英雄 5 个相位槽（P1–P5），对应 Unity 界面 5 个技能槽。",
        "> `SkillName` = 相位俗称，`SkillDesc` = 阶段效果文本（来自 `DT_PhaseHero.json` 的 `EffectText`）。",
        "",
        "| HeroId | HeroName | Phase | Nature | PhiToReach | SkillName | SkillDesc |",
        "|---|---|---|---|---:|---|---|",
    ]
    for row in rows:
        desc = row["SkillDesc"].replace("|", "\\|")
        lines.append(
            f"| {row['HeroId']} | {row['HeroName']} | {row['Phase']} | {row['Nature']} | "
            f"{row['PhiToReach']} | {row['SkillName']} | {desc} |"
        )
    lines.append("")
    path.write_text("\n".join(lines), encoding="utf-8")


def main():
    heroes = load_json(HERO_FILE)
    rows = build_rows(heroes)

    csv_path = OUT_DIR / "Gatebreaker Arena v0.3 相位效果 SkillName-SkillDesc.csv"
    json_path = OUT_DIR / "Gatebreaker Arena v0.3 相位效果 SkillName-SkillDesc.json"
    md_path = OUT_DIR / "Gatebreaker Arena v0.3 相位效果 SkillName-SkillDesc.md"

    write_csv(rows, csv_path)
    write_json(rows, json_path)
    write_markdown(rows, md_path)

    print(f"生成完成：")
    print(f"  CSV  : {csv_path} ({len(rows)} 行)")
    print(f"  JSON : {json_path}")
    print(f"  MD   : {md_path}")


if __name__ == "__main__":
    main()
