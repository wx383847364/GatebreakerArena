#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
从 DT_PhaseTech.json 提取科技名称与参数化描述，生成 SkillName/SkillDesc 数据表。
输出：CSV / JSON / Markdown 三种格式，便于 Unity 配置或策划填表。
"""
import json
import csv
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
CONFIG_DIR = ROOT / "Assets" / "Config"
OUT_DIR = ROOT / "design" / "tech"
OUT_DIR.mkdir(parents=True, exist_ok=True)

TECH_FILE = CONFIG_DIR / "DT_PhaseTech.json"
HERO_FILE = CONFIG_DIR / "DT_PhaseHero.json"

# 英雄 ID -> 中文名 映射（按界面从左到右顺序）
HERO_NAME_MAP = {
    "HERO_MIRAGE": "蜃影",
    "HERO_PULSE": "脉冲",
    "HERO_RIFT": "裂痕",
    "HERO_REFRACT": "折光",
}
HERO_ORDER = {hid: idx for idx, hid in enumerate(HERO_NAME_MAP.keys())}

# 相位 -> 排序权重
PHASE_ORDER = {"P1": 1, "P2": 2, "P3": 3, "P4": 4, "P5": 5}

# 槽位映射：Default=0（基准），Advanced 按出现顺序 1、2
KIND_SLOT = {"Default": 0, "Advanced": 1}


def load_json(path: Path):
    with open(path, encoding="utf-8") as f:
        data = json.load(f)
    return data if isinstance(data, list) else (data.get("rows") or data.get("Techs") or data.get("Heroes") or [])


# 单位中文映射
UNIT_CN = {"s": "秒", "次": "次", "%": "%", "个": "个"}

# ParamLabel 美化（更自然的中文宾语）
PARAM_BEAUTIFY = {
    "临时球持续": "临时球持续时间",
}


def _fmt_num(v) -> str:
    """整数不带小数，小数保留 2 位（消除浮点尾差）。"""
    f = round(float(v), 2)
    return str(int(f)) if f.is_integer() else str(f)


def build_skill_desc(effects: list) -> str:
    """把多个 Effect 转写为增量描述：{道具}{参数}{增加/减少}{差值}{单位}（，持续Ns）。

    用结构化字段（BaseValue / ModifiedValue / Op / Unit）生成，不依赖 ResolvedText 字符串。
    """
    parts = []
    for e in effects:
        if not e.get("ResolvedText"):
            continue
        item = e.get("ItemName", "")
        param = PARAM_BEAUTIFY.get(e.get("ParamLabel", ""), e.get("ParamLabel", ""))
        base = e.get("BaseValue", 0)
        mod = e.get("ModifiedValue", 0)
        op = e.get("Op", "")
        unit = e.get("Unit", "")
        unit_cn = UNIT_CN.get(unit, unit)

        diff = mod - base
        diff_abs = abs(diff)

        # 零差值已被数据模型禁止（整数型走 MagnitudeStep、连续型走 MagnitudePercent，
        # NetOffset 守恒要求每槽必有真实增减）。此分支仅兜底：跳过而非输出「无变化」。
        if diff_abs == 0:
            continue

        direction = "增加" if op == "Enhance" else "减少"
        desc = f"{item}{param}{direction}{_fmt_num(diff_abs)}{unit_cn}"

        # 保留效果持续时长（缓滞/疾风/广域 在「→」之后的「持续 Ns」后缀；
        # 分形参数值里的「持续 6s」是基准值本身，不属于时长，必须排除）
        after = e.get("ResolvedText", "").split("→")[-1]
        dur = re.search(r"持续\s*(\d+(?:\.\d+)?)s", after)
        if dur:
            desc += f"，持续{dur.group(1)}秒"

        parts.append(desc)
    return "；".join(parts)


def build_rows(techs: list):
    rows = []
    # 按英雄、相位分组，给 Advanced 分配 1/2 序号
    group_slots = {}
    for tech in sorted(
        techs,
        key=lambda x: (
            HERO_ORDER.get(x.get("HeroId", ""), 99),
            PHASE_ORDER.get(x.get("SlotPhase", ""), 99),
            0 if x.get("Kind") == "Default" else 1,
            x.get("TechId", ""),
        ),
    ):
        hero_id = tech.get("HeroId", "")
        phase = tech.get("SlotPhase", "")
        kind = tech.get("Kind", "Default")

        # Advanced 的 SlotIndex 按同英雄同相位 Advanced 出现顺序递增
        if kind == "Default":
            slot_index = 0
        else:
            key = (hero_id, phase)
            group_slots[key] = group_slots.get(key, 0) + 1
            slot_index = group_slots[key]

        rows.append({
            "HeroId": hero_id,
            "HeroName": HERO_NAME_MAP.get(hero_id, hero_id),
            "Phase": phase,
            "SlotIndex": slot_index,
            "Kind": kind,
            "CostCurrency": tech.get("CostCurrency", 0),
            "TechId": tech.get("TechId", ""),
            "SkillName": tech.get("DisplayName", ""),
            "SkillDesc": build_skill_desc(tech.get("Effects", [])),
        })
    return rows


def write_csv(rows: list, path: Path):
    fieldnames = [
        "HeroId", "HeroName", "Phase", "SlotIndex", "Kind",
        "CostCurrency", "TechId", "SkillName", "SkillDesc",
    ]
    with open(path, "w", encoding="utf-8-sig", newline="") as f:
        writer = csv.DictWriter(f, fieldnames=fieldnames)
        writer.writeheader()
        writer.writerows(rows)


def write_json(rows: list, path: Path):
    # JSON 按 HeroId -> Phase -> SlotIndex 嵌套，方便程序读取
    nested = {}
    for row in rows:
        hero_id = row["HeroId"]
        phase = row["Phase"]
        nested.setdefault(hero_id, {
            "HeroName": row["HeroName"],
            "Phases": {}
        })
        nested[hero_id]["Phases"].setdefault(phase, [])
        nested[hero_id]["Phases"][phase].append({
            "SlotIndex": row["SlotIndex"],
            "Kind": row["Kind"],
            "CostCurrency": row["CostCurrency"],
            "TechId": row["TechId"],
            "SkillName": row["SkillName"],
            "SkillDesc": row["SkillDesc"],
        })
    # 数组排序
    for hero in nested.values():
        for phase in hero["Phases"]:
            hero["Phases"][phase].sort(key=lambda x: x["SlotIndex"])
    with open(path, "w", encoding="utf-8") as f:
        json.dump(nested, f, ensure_ascii=False, indent=2)


def write_markdown(rows: list, path: Path):
    lines = [
        "# Gatebreaker Arena v0.3 SkillName / SkillDesc 数据表",
        "",
        "> 每英雄 5 相位（P1–P5），每相位 3 槽位（SlotIndex 0=基准，1/2=进阶）。",
        "> 本表可直接用于 Unity `SkillName` / `SkillDesc` 字段配置。",
        "> `SkillDesc` 采用**增量描述**（如「分形临时球持续时间增加0.6秒」），便于玩家直观理解增减量，不展示 before→after 区间与增强/削弱百分比。",
        "",
        "| HeroId | HeroName | Phase | SlotIndex | Kind | Cost | TechId | SkillName | SkillDesc |",
        "|---|---|---|---|---:|---|---|---|---|",
    ]
    for row in rows:
        desc = row["SkillDesc"].replace("|", "\\|")
        lines.append(
            f"| {row['HeroId']} | {row['HeroName']} | {row['Phase']} | {row['SlotIndex']} | {row['Kind']} | "
            f"{row['CostCurrency']} | `{row['TechId']}` | {row['SkillName']} | {desc} |"
        )
    lines.append("")
    path.write_text("\n".join(lines), encoding="utf-8")


def main():
    techs = load_json(TECH_FILE)
    rows = build_rows(techs)

    csv_path = OUT_DIR / "Gatebreaker Arena v0.3 SkillName-SkillDesc.csv"
    json_path = OUT_DIR / "Gatebreaker Arena v0.3 SkillName-SkillDesc.json"
    md_path = OUT_DIR / "Gatebreaker Arena v0.3 SkillName-SkillDesc.md"

    write_csv(rows, csv_path)
    write_json(rows, json_path)
    write_markdown(rows, md_path)

    print(f"生成完成：")
    print(f"  CSV  : {csv_path} ({len(rows)} 行)")
    print(f"  JSON : {json_path}")
    print(f"  MD   : {md_path}")


if __name__ == "__main__":
    main()
