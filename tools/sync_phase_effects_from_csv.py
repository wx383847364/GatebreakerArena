# -*- coding: utf-8 -*-
"""以「相位效果 SkillName-SkillDesc.csv」为基准，反向同步 DT_PhaseHero.json 的 EffectText。

匹配键：(HeroId, Phase)。CSV 的 SkillDesc 直接覆盖对应相位 EffectText。
仅改 EffectText 字段，不触碰其他字段 / ResourceRule。
"""
import csv
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
CSV_PATH = ROOT / "design/tech/Gatebreaker Arena v0.3 相位效果 SkillName-SkillDesc.csv"
JSON_PATH = ROOT / "Assets/Config/DT_PhaseHero.json"

# 1) 读 CSV 基准
baseline = {}
with open(CSV_PATH, encoding="utf-8-sig", newline="") as f:
    for row in csv.DictReader(f):
        key = (row["HeroId"].strip(), row["Phase"].strip())
        baseline[key] = row["SkillDesc"].strip()

print(f"CSV baseline entries: {len(baseline)}")

# 2) 加载并改写 JSON
data = json.loads(JSON_PATH.read_text(encoding="utf-8"))
heroes = data if isinstance(data, list) else data.get("rows") or data.get("Heroes") or []

applied = 0
missing = []
for h in heroes:
    hid = h.get("HeroId")
    for p in h.get("PhaseLevels", []):
        key = (hid, p.get("PhaseLevel"))
        if key in baseline:
            new_txt = baseline[key]
            if p.get("EffectText") != new_txt:
                p["EffectText"] = new_txt
                applied += 1
        else:
            missing.append(key)

if missing:
    print("WARN missing in CSV:", missing)

JSON_PATH.write_text(json.dumps(data, ensure_ascii=False, indent=2), encoding="utf-8")
print(f"Applied EffectText updates: {applied}")
print("DT_PhaseHero.json synced to CSV baseline.")
