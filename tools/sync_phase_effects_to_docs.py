# -*- coding: utf-8 -*-
"""以相位效果 CSV 为基准，同步设计文档 v0.3 的 §4.1~§4.4 相位表、参考原型 HEROES 相位文本。

new 值 = CSV SkillDesc（与已同步的 DT_PhaseHero.json 一致）。
old 值取各文件当前精确文本；P3 两文件冷却旧串不同，分别列出。
"""
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
CSV_PATH = ROOT / "design/tech/Gatebreaker Arena v0.3 相位效果 SkillName-SkillDesc.csv"
DESIGN = ROOT / "doc/方案与数据/Gatebreaker Arena 1v1 双向砖潮相位成长与英雄科技设计 v0.3.md"
REF = ROOT / "doc/方案与数据/Gatebreaker v0.3 UI 参考原型.html"

# 读 CSV baseline： (HeroId, Phase) -> SkillDesc
baseline = {}
import csv
with open(CSV_PATH, encoding="utf-8-sig", newline="") as f:
    for row in csv.DictReader(f):
        baseline[(row["HeroId"].strip(), row["Phase"].strip())] = row["SkillDesc"].strip()

# 各文件 old 文本（与 new 一一对应）。new 取自 baseline，按 (HeroId,Phase) 索引。
# 键： (HeroId, Phase) ；值：该文件当前 old 文本列表（P3 两文件各一）
OLD = {
    ("HERO_MIRAGE","P1"): ["1 颗主球；连击达 8 时分裂 1 颗持续 6s 的临时球"],
    ("HERO_MIRAGE","P2"): ["连击阈值降至 6"],
    ("HERO_MIRAGE","P3"): ["主动「幻潮」：复制场上所有球，最多 +2 颗临时球，持续 6s，冷却 12s",
                          "主动「幻潮」：复制场上所有球，最多 +2 颗临时球，持续 6s"],
    ("HERO_MIRAGE","P4"): ["分裂生成的临时球：数量 1 → 2 颗（仅增加数量，不延长存在时间）"],
    ("HERO_MIRAGE","P5"): ["连击达 4 时分裂 1 颗临时球；场上每多 1 颗物理球挡板 +4%，上限 +12%"],
    ("HERO_PULSE","P1"): ["节拍系统上线：每次挡板有效改向 +1 节拍，每 3 秒无改向 -1；每节拍球速 +3%，上限 +12%",
                          "节拍系统上线：每次挡板有效改向 +1 节拍，每 3s 无改向 -1；每节拍球速 +3%，上限 +12%"],
    ("HERO_PULSE","P2"): ["节拍上限提高到 5"],
    ("HERO_PULSE","P3"): ["主动「爆点」：锁定当前最高节拍 5s，期间速度上限提高至 6.0，冷却 18s",
                          "主动「爆点」：锁定当前最高节拍 5s，期间速度上限提高至 6.0"],
    ("HERO_PULSE","P4"): ["节拍系统基础 Scaling 提升：每节拍球速 +3% → +5%，速度上限 +12% → +16%"],
    ("HERO_PULSE","P5"): ["高节拍（≥4）时每第 3 次命中获得 1 次穿透"],
    ("HERO_RIFT","P1"): ["蓄力满 5 点后主球获得 1 次穿透，最多存 2 次（与道具裂穿共用穿透计数池）"],
    ("HERO_RIFT","P2"): ["穿透储存上限提高到 3"],
    ("HERO_RIFT","P3"): ["主动「贯裂」：主球立即获得 4 次穿透 + 12% 速度，持续到耗尽或 6s，冷却 20s",
                        "主动「贯裂」：主球立即获得 4 次穿透 + 12% 速度，持续到耗尽或 6s"],
    ("HERO_RIFT","P4"): ["蓄力满释放时主球获得穿透 1 → 2 次（储存上限保持 3 次），蓄力阈值保持 5"],
    ("HERO_RIFT","P5"): ["蓄力阈值降至 4；穿透命中砖块返还 1 点蓄力（每次飞行最多返还 2 点）"],
    ("HERO_REFRACT","P1"): ["主球被挡板回球获得折射标记；带标记命中砖块完成折射循环，完成后下一次挡板回球速度 +8%，持续 3s"],
    ("HERO_REFRACT","P2"): ["折射循环速度加成提高到 +12%；标记持续 2.5s 延长到 3s"],
    ("HERO_REFRACT","P3"): ["主动「镜界」：本方半场生成一面 6s 镜面，只与本方弹球碰撞，冷却 16s",
                            "主动「镜界」：本方半场生成一面 6s 镜面，只与本方弹球碰撞"],
    ("HERO_REFRACT","P4"): ["折射循环的速度加成改为可叠加：每次循环 +6% 挡板回球速度，最多叠加 3 层（合计 +18%），折射标记持续 3s → 4s"],
    ("HERO_REFRACT","P5"): ['每完成 2 次折射循环，镜界冷却 -3s；所有球经过镜面后获得"墙面命中不降速"',
                            '每完成 2 次折射循环，镜界冷却 -3s；所有球经过镜面后获得“墙面命中不降速”'],
}

def apply_to_file(path: Path):
    txt = path.read_text(encoding="utf-8")
    total = 0
    for (hid, ph), olds in OLD.items():
        new = baseline[(hid, ph)]
        for old in olds:
            cnt = txt.count(old)
            if cnt == 0:
                print(f"  [WARN] not found in {path.name}: [{hid} {ph}] {old[:30]}")
            elif cnt > 1:
                print(f"  [WARN] multiple ({cnt}) in {path.name}: [{hid} {ph}] {old[:30]}")
            txt = txt.replace(old, new)
            total += cnt
    path.write_text(txt, encoding="utf-8")
    print(f"{path.name}: replaced {total} occurrences")

apply_to_file(DESIGN)
apply_to_file(REF)
print("Docs synced to CSV baseline.")
