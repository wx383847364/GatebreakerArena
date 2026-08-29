# -*- coding: utf-8 -*-
"""
Gatebreaker Arena v0.3 — 一次性迁移：整数型道具由百分比(MagnitudePercent)改为离散步进(MagnitudeStep)

原则（用户拍板）：
  - 次数/个数这类整数型数据（裂穿穿透次数、磁吸吸附个数）不做百分比修正，改用离散步进 ±N 次/个。
  - 速度/长度/减速这类连续型数据（分形/缓滞/疾风/广域）保留百分比 MagnitudePercent。

迁移规则：
  1. 裂穿(ItemPierce, base=2) / 磁吸(ItemMagnet, base=1) 的效果：
       - 新增 MagnitudeStep = ±N（Enhance 为正，Weaken 为负）。
       - MagnitudePercent 置 0（仅保留字段名以兼容旧加载器 C# ReadInt，不再参与解析）。
  2. 非「死效果」：step 由当前 ModifiedValue 反推（保当前平衡不变）。
  3. 「死效果」（ModifiedValue == base，即取整吞掉百分比）：按 DEAD_FIX 显式修复为真实离散步进。

DEAD_FIX 语义（10 处）：
  - 裂痕 5 个基准科技：裂穿 增强 +1 次（2→3，基准从空操作变为真实收益）
  - 凝壁 / 回廊：裂穿 削弱 -1 次（2→1，代价从空操作变为真实削弱）
  - 虫群 / 稳盘 / 碎星：磁吸 削弱 -1 个（1→0，代价从空操作变为真实削弱）

用法: python tools/migrate_count_items_to_step.py
"""
import json, os

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DT_PATH = os.path.join(ROOT, "Assets", "Config", "DT_PhaseTech.json")

COUNT_BASE = {"ItemPierce": 2, "ItemMagnet": 1}

# (TechId, ItemId) -> 修复后的离散步进（死效果专属）
DEAD_FIX = {
    ("TECH_RIFT_P1_BASE", "ItemPierce"): 1,
    ("TECH_RIFT_P2_BASE", "ItemPierce"): 1,
    ("TECH_RIFT_P3_BASE", "ItemPierce"): 1,
    ("TECH_RIFT_P4_BASE", "ItemPierce"): 1,
    ("TECH_RIFT_P5_BASE", "ItemPierce"): 1,
    ("TECH_RIFT_P3_WALLSTAB", "ItemPierce"): -1,
    ("TECH_REFRACT_P3_CORRIDOR", "ItemPierce"): -1,
    ("TECH_MIRAGE_P4_HIVE", "ItemMagnet"): -1,
    ("TECH_RIFT_P1_STEADY", "ItemMagnet"): -1,
    ("TECH_RIFT_P5_STARCHATTER", "ItemMagnet"): -1,
}


def main():
    with open(DT_PATH, "r", encoding="utf-8") as f:
        data = json.load(f)

    migrated = 0
    fixed = []
    for tech in data:
        for eff in tech.get("Effects", []):
            iid = eff["ItemId"]
            if iid not in COUNT_BASE:
                continue
            base = COUNT_BASE[iid]
            op = eff["Op"]
            key = (tech["TechId"], iid)
            if key in DEAD_FIX:
                step = DEAD_FIX[key]
                fixed.append((tech["TechId"], eff["ItemName"], op, step))
            else:
                mod = eff.get("ModifiedValue", base)
                delta = int(round(mod - base))
                step = abs(delta) if op == "Enhance" else -abs(delta)
            eff["MagnitudeStep"] = step
            eff["MagnitudePercent"] = 0
            migrated += 1

    with open(DT_PATH, "w", encoding="utf-8") as f:
        json.dump(data, f, ensure_ascii=False, indent=2)
        f.write("\n")

    print(f"OK: {migrated} 个整数型效果已改为 MagnitudeStep；其中 {len(fixed)} 个死效果被修复：")
    for tid, name, op, step in fixed:
        print(f"  - {tid} [{name}] {op} -> step {step:+d}")


if __name__ == "__main__":
    main()
