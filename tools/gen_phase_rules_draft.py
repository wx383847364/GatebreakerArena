# -*- coding: utf-8 -*-
"""Generate the v0.3 rule-config draft (four segments + meta) for Gatebreaker Arena.

Source of truth: doc/方案与数据/Gatebreaker Arena 1v1 双向砖潮相位成长与英雄科技设计 v0.3.md
All numbers are the tuning-workshop locked values (迁移计划 §6). This script is the
regenerable source for the JSON draft; do NOT hand-edit the JSON directly.

Output: doc/方案与数据/Gatebreaker Arena 1v1 双向砖潮 规则配置草稿 v0.3.json
"""
import json
import os

OUT = os.path.join(
    os.path.dirname(__file__),
    "..", "doc", "方案与数据",
    "Gatebreaker Arena 1v1 双向砖潮 规则配置草稿 v0.3.json",
)

# ---- value weights (§5.1 / §6 #2) ----
WEIGHT = {
    "ItemPierce": 3,   # 裂穿
    "ItemSplit": 3,    # 分形
    "ItemDamp": 3,     # 缓滞
    "ItemSpeed": 2,    # 疾风
    "ItemWide": 2,     # 广域
    "ItemMagnet": 1,   # 磁吸
}
ITEM_CN = {
    "ItemPierce": "裂穿", "ItemSplit": "分形", "ItemDamp": "缓滞",
    "ItemSpeed": "疾风", "ItemWide": "广域", "ItemMagnet": "磁吸",
}

# ---- per-phase Phi cost (§2.3 / §6 #1, total 250) ----
PHI_COST = {"P1": 0, "P2": 30, "P3": 50, "P4": 70, "P5": 100}

# shared Phi sources (§2.2)
PHI_SOURCES = [
    {"Source": "PickupItem", "Phi": 2, "Note": "接取道具，纯操作"},
    {"Source": "BreakMysteryBrick", "Phi": 2, "Note": "击碎道具砖？，稀缺高价值"},
    {"Source": "BreakRedBrick", "Phi": 0.5, "Note": "稳定低权重"},
    {"Source": "BreakYellowBrick", "Phi": 1, "Note": "稳定低权重，奖励处理难题"},
    {"Source": "BreakGreenBrick", "Phi": 0, "Note": "挂机白嫖重灾区，不给"},
    {"Source": "PerSecondCap", "Phi": 6, "Note": "所有来源每秒上限，硬约束，防滚雪球"},
]

# ================= DT_PhaseHero =================
HEROES = [
    {
        "HeroId": "HERO_MIRAGE", "DisplayName": "蜃影", "Dimension": "数量",
        "CoreResource": "Combo", "CoreItem": "ItemSplit",
        "PhaseLevels": [
            {"PhaseLevel": "P1", "Nature": "Identity", "PhiToReach": 0,
             "EffectText": "1 颗主球；连击达 8 时分裂 1 颗持续 4s 的临时球"},
            {"PhaseLevel": "P2", "Nature": "Scale", "PhiToReach": 30,
             "EffectText": "连击阈值降至 6"},
            {"PhaseLevel": "P3", "Nature": "Active", "PhiToReach": 50,
             "EffectText": "主动「幻潮」：复制场上所有球，最多 +2 颗临时球，持续 6s",
             "ActiveAbility": {"AbilityId": "ABILITY_MIRAGE_MIRAGE_TIDE", "CooldownSeconds": 12}},
            {"PhaseLevel": "P4", "Nature": "Protocol", "PhiToReach": 70,
             "EffectText": "协议二选一（局外配装，P4 自动激活）",
             "ProtocolOptions": [
                 {"OptionId": "MIRAGE_PROTOCOL_MIRROR", "DisplayName": "镜影协议", "IsDefault": True,
                  "EffectText": "生成 1 颗永久副球，速度为主球 90%"},
                 {"OptionId": "MIRAGE_PROTOCOL_TIDE", "DisplayName": "潮汐协议", "IsDefault": False,
                  "EffectText": "临时球持续时间 +2s；断连时连击清零惩罚翻倍"},
             ]},
            {"PhaseLevel": "P5", "Nature": "Climax", "PhiToReach": 100,
             "EffectText": "连击达 4 时分裂 1 颗临时球；场上每多 1 颗物理球挡板 +4%，上限 +12%"},
        ],
    },
    {
        "HeroId": "HERO_PULSE", "DisplayName": "脉冲", "Dimension": "速度",
        "CoreResource": "Tempo", "CoreItem": "ItemSpeed",
        "PhaseLevels": [
            {"PhaseLevel": "P1", "Nature": "Identity", "PhiToReach": 0,
             "EffectText": "节拍系统上线：每次挡板有效改向 +1 节拍，每 3s 无改向 -1；每节拍球速 +3%，上限 +12%"},
            {"PhaseLevel": "P2", "Nature": "Scale", "PhiToReach": 30,
             "EffectText": "节拍上限提高到 5"},
            {"PhaseLevel": "P3", "Nature": "Active", "PhiToReach": 50,
             "EffectText": "主动「爆点」：锁定当前最高节拍 5s，期间速度上限提高至 6.0",
             "ActiveAbility": {"AbilityId": "ABILITY_PULSE_BURST", "CooldownSeconds": 18}},
            {"PhaseLevel": "P4", "Nature": "Protocol", "PhiToReach": 70,
             "EffectText": "协议二选一（局外配装，P4 自动激活）",
             "ProtocolOptions": [
                 {"OptionId": "PULSE_PROTOCOL_SPRINT", "DisplayName": "疾走协议", "IsDefault": True,
                  "EffectText": "节拍上限提高到 7；节拍清零时速度瞬间 -15%，持续 0.5s"},
                 {"OptionId": "PULSE_PROTOCOL_BRAKE", "DisplayName": "制动协议", "IsDefault": False,
                  "EffectText": "节拍上限保持 5；挡板边缘击球附带 0.3s 减速瞄准，随后加速"},
             ]},
            {"PhaseLevel": "P5", "Nature": "Climax", "PhiToReach": 100,
             "EffectText": "高节拍（≥4）时每第 3 次命中获得 1 次穿透"},
        ],
    },
    {
        "HeroId": "HERO_RIFT", "DisplayName": "裂痕", "Dimension": "穿透",
        "CoreResource": "Rift", "CoreItem": "ItemPierce",
        "PhaseLevels": [
            {"PhaseLevel": "P1", "Nature": "Identity", "PhiToReach": 0,
             "EffectText": "蓄力满 5 点后主球获得 1 次穿透，最多存 2 次"},
            {"PhaseLevel": "P2", "Nature": "Scale", "PhiToReach": 30,
             "EffectText": "穿透储存上限提高到 3"},
            {"PhaseLevel": "P3", "Nature": "Active", "PhiToReach": 50,
             "EffectText": "主动「贯裂」：主球立即获得 4 次穿透 + 12% 速度，持续到耗尽或 6s",
             "ActiveAbility": {"AbilityId": "ABILITY_RIFT_PIERCE", "CooldownSeconds": 20}},
            {"PhaseLevel": "P4", "Nature": "Protocol", "PhiToReach": 70,
             "EffectText": "协议二选一（局外配装，P4 自动激活）",
             "ProtocolOptions": [
                 {"OptionId": "RIFT_PROTOCOL_DEEPDRILL", "DisplayName": "深钻协议", "IsDefault": True,
                  "EffectText": "穿透同一列连穿 2 块返还 1 次穿透，每次技能最多返还 2 次"},
                 {"OptionId": "RIFT_PROTOCOL_SHATTER", "DisplayName": "震裂协议", "IsDefault": False,
                  "EffectText": "穿透结束时对左右相邻砖各造成 1 点伤害"},
             ]},
            {"PhaseLevel": "P5", "Nature": "Climax", "PhiToReach": 100,
             "EffectText": "蓄力阈值降至 4；穿透命中砖块返还 1 点蓄力（每次飞行最多返还 2 点）"},
        ],
    },
    {
        "HeroId": "HERO_REFRACT", "DisplayName": "折光", "Dimension": "弹道",
        "CoreResource": "RefractMark", "CoreItem": "ItemWide",
        "PhaseLevels": [
            {"PhaseLevel": "P1", "Nature": "Identity", "PhiToReach": 0,
             "EffectText": "完成折射循环后，下一次侧墙反弹速度 +8%，持续 3s"},
            {"PhaseLevel": "P2", "Nature": "Scale", "PhiToReach": 30,
             "EffectText": "折射循环速度加成提高到 +12%；标记持续 2.5s 延长到 3s"},
            {"PhaseLevel": "P3", "Nature": "Active", "PhiToReach": 50,
             "EffectText": "主动「镜界」：本方半场生成一面 6s 镜面，只与本方弹球碰撞",
             "ActiveAbility": {"AbilityId": "ABILITY_REFRACT_MIRROR", "CooldownSeconds": 16}},
            {"PhaseLevel": "P4", "Nature": "Protocol", "PhiToReach": 70,
             "EffectText": "协议二选一（局外配装，P4 自动激活）",
             "ProtocolOptions": [
                 {"OptionId": "REFRACT_PROTOCOL_CORRIDOR", "DisplayName": "回廊协议", "IsDefault": True,
                  "EffectText": "镜面期间主球与镜面碰撞 3 次，每次速度 +5%，总计 ≤15%"},
                 {"OptionId": "REFRACT_PROTOCOL_SPLIT", "DisplayName": "分光协议", "IsDefault": False,
                  "EffectText": "主球首次碰镜面生成 1 颗持续 5s 的临时球"},
             ]},
            {"PhaseLevel": "P5", "Nature": "Climax", "PhiToReach": 100,
             "EffectText": "每完成 2 次折射循环，镜界冷却 -3s；所有球经过镜面后获得“墙面命中不降速”"},
        ],
    },
]
for h in HEROES:
    h["PhiSources"] = PHI_SOURCES

# ================= DT_PhaseTech (60) =================
# tuple: (hero, phase, kind, en, cn, cost, [(item, op, pct, drop_offset_or_None), ...])
TECH_RAW = [
    # 蜃影
    ("HERO_MIRAGE", "P1", "Default", "BASE", "基准", 0, [("ItemSplit", "Enhance", 10, None)]),
    ("HERO_MIRAGE", "P1", "Advanced", "SWARM", "蜂拥", 15, [("ItemSplit", "Enhance", 50, None), ("ItemDamp", "Weaken", 40, None)]),
    ("HERO_MIRAGE", "P1", "Advanced", "LONEWOLF", "孤狼", 15, [("ItemPierce", "Enhance", 40, None), ("ItemSplit", "Weaken", 30, None)]),
    ("HERO_MIRAGE", "P2", "Default", "BASE", "基准", 0, [("ItemSplit", "Enhance", 10, None)]),
    ("HERO_MIRAGE", "P2", "Advanced", "FISSION", "裂变", 15, [("ItemSplit", "Enhance", 40, None), ("ItemSpeed", "Weaken", 45, None)]),
    ("HERO_MIRAGE", "P2", "Advanced", "FLOOD", "洪流", 15, [("ItemSplit", "Enhance", 60, None), ("ItemWide", "Weaken", 75, None)]),
    ("HERO_MIRAGE", "P3", "Default", "BASE", "基准", 0, [("ItemSplit", "Enhance", 10, None)]),
    ("HERO_MIRAGE", "P3", "Advanced", "PROLIFERATE", "增殖", 15, [("ItemSplit", "Enhance", 40, None), ("ItemPierce", "Weaken", 30, None)]),
    ("HERO_MIRAGE", "P3", "Advanced", "DEVOUR", "吞噬", 15, [("ItemDamp", "Enhance", 30, None), ("ItemSplit", "Weaken", 20, None)]),
    ("HERO_MIRAGE", "P4", "Default", "BASE", "基准", 0, [("ItemSplit", "Enhance", 10, None)]),
    ("HERO_MIRAGE", "P4", "Advanced", "HIVE", "虫群", 15, [("ItemSplit", "Enhance", 50, None), ("ItemMagnet", "Weaken", 120, None)]),
    ("HERO_MIRAGE", "P4", "Advanced", "HOMECOMING", "归巢", 15, [("ItemWide", "Enhance", 45, None), ("ItemSpeed", "Weaken", 30, None)]),
    ("HERO_MIRAGE", "P5", "Default", "BASE", "基准", 0, [("ItemSplit", "Enhance", 10, None)]),
    ("HERO_MIRAGE", "P5", "Advanced", "FULLNEST", "满巢", 15, [("ItemSplit", "Enhance", 60, None), ("ItemDamp", "Weaken", 50, None)]),
    ("HERO_MIRAGE", "P5", "Advanced", "STARBURST", "星爆", 15, [("ItemPierce", "Enhance", 50, None), ("ItemSplit", "Weaken", 40, None)]),
    # 脉冲
    ("HERO_PULSE", "P1", "Default", "BASE", "基准", 0, [("ItemSpeed", "Enhance", 15, None)]),
    ("HERO_PULSE", "P1", "Advanced", "SPRINT", "疾走", 15, [("ItemSpeed", "Enhance", 45, None), ("ItemDamp", "Weaken", 20, None)]),
    ("HERO_PULSE", "P1", "Advanced", "PIERCE", "穿刺", 15, [("ItemPierce", "Enhance", 40, None), ("ItemSpeed", "Weaken", 45, None)]),
    ("HERO_PULSE", "P2", "Default", "BASE", "基准", 0, [("ItemSpeed", "Enhance", 15, None)]),
    ("HERO_PULSE", "P2", "Advanced", "OVERCLOCK", "超频", 15, [("ItemSpeed", "Enhance", 60, None), ("ItemWide", "Weaken", 45, None)]),
    ("HERO_PULSE", "P2", "Advanced", "ARMORBREAK", "破甲", 15, [("ItemPierce", "Enhance", 40, None), ("ItemSplit", "Weaken", 30, None)]),
    ("HERO_PULSE", "P3", "Default", "BASE", "基准", 0, [("ItemSpeed", "Enhance", 15, None)]),
    ("HERO_PULSE", "P3", "Advanced", "REDLINE", "红线", 15, [("ItemSpeed", "Enhance", 45, None), ("ItemDamp", "Weaken", 20, None)]),
    ("HERO_PULSE", "P3", "Advanced", "STASIS", "凝滞", 15, [("ItemDamp", "Enhance", 30, None), ("ItemSpeed", "Weaken", 30, None)]),
    ("HERO_PULSE", "P4", "Default", "BASE", "基准", 0, [("ItemSpeed", "Enhance", 15, None)]),
    ("HERO_PULSE", "P4", "Advanced", "MAXSPEED", "极速", 15, [("ItemSpeed", "Enhance", 60, None), ("ItemWide", "Weaken", 45, None)]),
    ("HERO_PULSE", "P4", "Advanced", "BRAKE", "制动", 15, [("ItemWide", "Enhance", 45, None), ("ItemSpeed", "Weaken", 30, None)]),
    ("HERO_PULSE", "P5", "Default", "BASE", "基准", 0, [("ItemSpeed", "Enhance", 15, None)]),
    ("HERO_PULSE", "P5", "Advanced", "RAMPAGE", "暴走", 15, [("ItemSpeed", "Enhance", 75, None), ("ItemDamp", "Weaken", 40, None)]),
    ("HERO_PULSE", "P5", "Advanced", "CLOUDPIERCE", "穿云", 15, [("ItemPierce", "Enhance", 50, None), ("ItemSpeed", "Weaken", 60, None)]),
    # 裂痕
    ("HERO_RIFT", "P1", "Default", "BASE", "基准", 0, [("ItemPierce", "Enhance", 10, None)]),
    ("HERO_RIFT", "P1", "Advanced", "DEEPDRILL", "深钻", 15, [("ItemPierce", "Enhance", 50, None), ("ItemSplit", "Weaken", 40, None)]),
    ("HERO_RIFT", "P1", "Advanced", "STEADY", "稳盘", 15, [("ItemWide", "Enhance", 45, None), ("ItemMagnet", "Weaken", 60, None)]),
    ("HERO_RIFT", "P2", "Default", "BASE", "基准", 0, [("ItemPierce", "Enhance", 10, None)]),
    ("HERO_RIFT", "P2", "Advanced", "PENETRATE", "贯穿", 15, [("ItemPierce", "Enhance", 60, None), ("ItemSpeed", "Weaken", 75, None)]),
    ("HERO_RIFT", "P2", "Advanced", "BREAKARRAY", "破阵", 15, [("ItemPierce", "Enhance", 40, None), ("ItemSplit", "Weaken", 30, None)]),
    ("HERO_RIFT", "P3", "Default", "BASE", "基准", 0, [("ItemPierce", "Enhance", 10, None)]),
    ("HERO_RIFT", "P3", "Advanced", "STARPIERCE", "贯星", 15, [("ItemPierce", "Enhance", 50, None), ("ItemWide", "Weaken", 60, None)]),
    ("HERO_RIFT", "P3", "Advanced", "WALLSTAB", "凝壁", 15, [("ItemDamp", "Enhance", 30, None), ("ItemPierce", "Weaken", 20, None)]),
    ("HERO_RIFT", "P4", "Default", "BASE", "基准", 0, [("ItemPierce", "Enhance", 10, None)]),
    ("HERO_RIFT", "P4", "Advanced", "DRILLDOWN", "钻地", 15, [("ItemPierce", "Enhance", 60, None), ("ItemSplit", "Weaken", 50, None)]),
    ("HERO_RIFT", "P4", "Advanced", "WIDEFIELD", "广域", 15, [("ItemWide", "Enhance", 45, None), ("ItemSpeed", "Weaken", 30, None)]),
    ("HERO_RIFT", "P5", "Default", "BASE", "基准", 0, [("ItemPierce", "Enhance", 10, None)]),
    ("HERO_RIFT", "P5", "Advanced", "BOUNDLESS", "无界", 15, [("ItemPierce", "Enhance", 70, None), ("ItemDamp", "Weaken", 60, None)]),
    ("HERO_RIFT", "P5", "Advanced", "STARCHATTER", "碎星", 15, [("ItemPierce", "Enhance", 50, None), ("ItemMagnet", "Weaken", 120, None)]),
    # 折光
    ("HERO_REFRACT", "P1", "Default", "BASE", "基准", 0, [("ItemWide", "Enhance", 15, None)]),
    ("HERO_REFRACT", "P1", "Advanced", "STEADYMIRROR", "稳镜", 15, [("ItemWide", "Enhance", 45, None), ("ItemSpeed", "Weaken", 30, None)]),
    ("HERO_REFRACT", "P1", "Advanced", "MAGNETSTAR", "吸星", 15, [("ItemMagnet", "Enhance", 60, None), ("ItemSplit", "Weaken", 10, None)]),
    ("HERO_REFRACT", "P2", "Default", "BASE", "基准", 0, [("ItemWide", "Enhance", 15, None)]),
    ("HERO_REFRACT", "P2", "Advanced", "EXPANDFIELD", "扩域", 15, [("ItemWide", "Enhance", 60, None), ("ItemSplit", "Weaken", 30, None)]),
    ("HERO_REFRACT", "P2", "Advanced", "CALIBRATE", "校准", 15, [("ItemMagnet", "Enhance", 90, None), ("ItemSpeed", "Weaken", 30, None)]),
    ("HERO_REFRACT", "P3", "Default", "BASE", "基准", 0, [("ItemWide", "Enhance", 15, None)]),
    ("HERO_REFRACT", "P3", "Advanced", "CORRIDOR", "回廊", 15, [("ItemWide", "Enhance", 45, None), ("ItemPierce", "Weaken", 20, None)]),
    ("HERO_REFRACT", "P3", "Advanced", "SPLITLIGHT", "分光", 15, [("ItemSplit", "Enhance", 30, None), ("ItemWide", "Weaken", 30, None)]),
    ("HERO_REFRACT", "P4", "Default", "BASE", "基准", 0, [("ItemWide", "Enhance", 15, None)]),
    ("HERO_REFRACT", "P4", "Advanced", "INFINITE", "无限", 15, [("ItemWide", "Enhance", 60, None), ("ItemSpeed", "Weaken", 45, None)]),
    ("HERO_REFRACT", "P4", "Advanced", "GRAVITY", "引力", 15, [("ItemMagnet", "Enhance", 120, None), ("ItemSplit", "Weaken", 30, None)]),
    ("HERO_REFRACT", "P5", "Default", "BASE", "基准", 0, [("ItemWide", "Enhance", 15, None)]),
    ("HERO_REFRACT", "P5", "Advanced", "FULLDOMAIN", "全境", 15, [("ItemWide", "Enhance", 75, None), ("ItemDamp", "Weaken", 40, None)]),
    ("HERO_REFRACT", "P5", "Advanced", "RETURNFLOW", "归流", 15, [("ItemMagnet", "Enhance", 150, None), ("ItemSpeed", "Weaken", 60, None)]),
]

PHASE_TECH = []
for hero, phase, kind, en, cn, cost, effects in TECH_RAW:
    effs = []
    net = 0
    for item, op, pct, drop in effects:
        w = WEIGHT[item]
        signed = pct if op == "Enhance" else -pct
        net += signed * w
        e = {"ItemId": item, "ItemName": ITEM_CN[item], "Op": op, "MagnitudePercent": pct}
        if drop is not None:
            e["DropOffset"] = drop
        effs.append(e)
    assert abs(net - 30) <= 5, f"tech {hero} {phase} {en} net={net} not ~30"
    PHASE_TECH.append({
        "TechId": f"TECH_{hero.split('_')[1]}_{phase}_{en}",
        "HeroId": hero,
        "SlotPhase": phase,
        "Kind": kind,
        "DisplayName": cn,
        "CostCurrency": cost,
        "NetOffset": net,
        "Effects": effs,
    })

# ================= DT_PhaseItem (6) =================
PHASE_ITEM = [
    {"ItemId": "ItemPierce", "ItemName": "裂穿", "ValueWeight": 3, "BaseDropWeight": 0.15,
     "Effect": {"PierceCharges": 2, "DurationSeconds": 0}, "Note": "主球获得 2 次穿透；与英雄蓄力穿透共用计数池"},
    {"ItemId": "ItemSplit", "ItemName": "分形", "ValueWeight": 3, "BaseDropWeight": 0.15,
     "Effect": {"TempBallCount": 1, "TempBallSeconds": 6}, "Note": "生成 1 颗持续 6s 临时球；与英雄分裂球同为临时球实体"},
    {"ItemId": "ItemDamp", "ItemName": "缓滞", "ValueWeight": 3, "BaseDropWeight": 0.15,
     "Effect": {"TideSpeedMultiplier": 0.8, "DurationSeconds": 6}, "Note": "本方砖潮推进速度 -20%；只作用本方"},
    {"ItemId": "ItemSpeed", "ItemName": "疾风", "ValueWeight": 2, "BaseDropWeight": 0.20,
     "Effect": {"BallSpeedMultiplier": 1.2, "DurationSeconds": 5}, "Note": "球速临时 +20%"},
    {"ItemId": "ItemWide", "ItemName": "广域", "ValueWeight": 2, "BaseDropWeight": 0.20,
     "Effect": {"PaddleLengthMultiplier": 1.2, "DurationSeconds": 8}, "Note": "挡板 +20%"},
    {"ItemId": "ItemMagnet", "ItemName": "磁吸", "ValueWeight": 1, "BaseDropWeight": 0.15,
     "Effect": {"PullNextItem": True}, "Note": "下一个道具自动落向挡板"},
]

# ================= DT_PhaseCurve (pressure) =================
# v0.3 C0~C5 % table -> fractions matching DT_BrickDuelRule.BrickCompositionStages format
CURVE_STAGES = [
    {"Stage": "C0", "TimeStart": 0, "GreenWeight": 0.80, "YellowWeight": 0.10, "RedWeight": 0.05, "MysteryWeight": 0.05},
    {"Stage": "C1", "TimeStart": 30, "GreenWeight": 0.65, "YellowWeight": 0.18, "RedWeight": 0.10, "MysteryWeight": 0.07},
    {"Stage": "C2", "TimeStart": 60, "GreenWeight": 0.50, "YellowWeight": 0.25, "RedWeight": 0.18, "MysteryWeight": 0.07},
    {"Stage": "C3", "TimeStart": 90, "GreenWeight": 0.35, "YellowWeight": 0.32, "RedWeight": 0.26, "MysteryWeight": 0.07},
    {"Stage": "C4", "TimeStart": 120, "GreenWeight": 0.22, "YellowWeight": 0.38, "RedWeight": 0.33, "MysteryWeight": 0.07},
    {"Stage": "C5", "TimeStart": 150, "GreenWeight": 0.12, "YellowWeight": 0.42, "RedWeight": 0.39, "MysteryWeight": 0.07},
]
PHASE_CURVE = [{
    "RuleId": "BRICK_DUEL_PHASE_V0",
    "CompositionIntervalSeconds": 30.0,
    "Stages": CURVE_STAGES,
    "BreakCounterThreshold": 20,          # §3.2 / §6 #8 清砖 20 块 -> 对手注入 1 升级砖
    "BreakCounterWarnSeconds": 1.0,       # 提前 1s 预警
    "Note": "替换现有 DT_BrickDuelRule.BrickCompositionStages；?砖占比 5%~7% 恒定，道具经济不被压缩",
}]

# ================= DT_PhaseMeta (economy + global tuning) =================
PHASE_META = [{
    "MetaId": "PHASE_META_V0",
    "CurrencyWin": 12, "CurrencyLoss": 4,      # §5.3 / §6 #6
    "TechUnlockCost": 15,                       # 进阶科技单价（默认免费）
    "NetOffsetBudget": 30,                      # §5.4 / §6 #5 四英雄统一预算 D
    "DropOffsetCap": 10,                        # §5.4 单科技掉率偏移封顶 +10 点
    "PhiPerSecondCap": 6,                       # §2.2 硬上限
    "ScissorDiffTargetSeconds": 40,             # §6.2 / §7 死亡时间差验收线
    "Note": "经济与全局平衡常数；全部为 §6 tuning 拍板值",
}]

# ================= assemble =================
draft = {
    "DT_PhaseHero": HEROES,
    "DT_PhaseTech": PHASE_TECH,
    "DT_PhaseItem": PHASE_ITEM,
    "DT_PhaseCurve": PHASE_CURVE,
    "DT_PhaseMeta": PHASE_META,
    "_DraftMeta": {
        "Source": "Gatebreaker Arena 1v1 双向砖潮相位成长与英雄科技设计 v0.3.md",
        "GeneratedBy": "tools/gen_phase_rules_draft.py",
        "Status": "数值草稿，待 Phase 0 落地；引擎现有 DT_Hero/DT_HeroPath/DT_SignatureChip/DT_UniversalChip 为旧范式需替换",
        "TechCount": len(PHASE_TECH),
        "HeroCount": len(HEROES),
        "ItemCount": len(PHASE_ITEM),
    },
}

out_path = os.path.abspath(OUT)
os.makedirs(os.path.dirname(out_path), exist_ok=True)
with open(out_path, "w", encoding="utf-8") as f:
    json.dump(draft, f, ensure_ascii=False, indent=2)
print(f"written: {out_path}")
print(f"techs={len(PHASE_TECH)} items={len(PHASE_ITEM)} heroes={len(HEROES)}")
# sanity: re-check all net offsets
bad = [t["TechId"] for t in PHASE_TECH if abs(t["NetOffset"] - 30) > 5]
print("net-offset violations:", bad)
