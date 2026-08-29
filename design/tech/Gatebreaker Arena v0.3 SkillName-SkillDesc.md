# Gatebreaker Arena v0.3 SkillName / SkillDesc 数据表

> 每英雄 5 相位（P1–P5），每相位 3 槽位（SlotIndex 0=基准，1/2=进阶）。
> 本表可直接用于 Unity `SkillName` / `SkillDesc` 字段配置。
> `SkillDesc` 采用**增量描述**（如「分形临时球持续时间增加0.6秒」），便于玩家直观理解增减量，不展示 before→after 区间与增强/削弱百分比。

| HeroId | HeroName | Phase | SlotIndex | Kind | Cost | TechId | SkillName | SkillDesc |
|---|---|---|---|---:|---|---|---|---|
| HERO_MIRAGE | 蜃影 | P1 | 0 | Default | 0 | `TECH_MIRAGE_P1_BASE` | 基准 | 分形临时球持续时间增加0.6秒 |
| HERO_MIRAGE | 蜃影 | P1 | 1 | Advanced | 15 | `TECH_MIRAGE_P1_LONEWOLF` | 孤狼 | 裂穿穿透次数增加1次；分形临时球持续时间减少1.8秒 |
| HERO_MIRAGE | 蜃影 | P1 | 2 | Advanced | 15 | `TECH_MIRAGE_P1_SWARM` | 蜂拥 | 分形临时球持续时间增加3秒；缓滞砖潮减速减少8%，持续6秒 |
| HERO_MIRAGE | 蜃影 | P2 | 0 | Default | 0 | `TECH_MIRAGE_P2_BASE` | 基准 | 分形临时球持续时间增加0.6秒 |
| HERO_MIRAGE | 蜃影 | P2 | 1 | Advanced | 15 | `TECH_MIRAGE_P2_FISSION` | 裂变 | 分形临时球持续时间增加2.4秒；疾风球速增幅减少9%，持续5秒 |
| HERO_MIRAGE | 蜃影 | P2 | 2 | Advanced | 15 | `TECH_MIRAGE_P2_FLOOD` | 洪流 | 分形临时球持续时间增加3.6秒；广域挡板增幅减少15%，持续8秒 |
| HERO_MIRAGE | 蜃影 | P3 | 0 | Default | 0 | `TECH_MIRAGE_P3_BASE` | 基准 | 分形临时球持续时间增加0.6秒 |
| HERO_MIRAGE | 蜃影 | P3 | 1 | Advanced | 15 | `TECH_MIRAGE_P3_DEVOUR` | 吞噬 | 缓滞砖潮减速增加6%，持续6秒；分形临时球持续时间减少1.2秒 |
| HERO_MIRAGE | 蜃影 | P3 | 2 | Advanced | 15 | `TECH_MIRAGE_P3_PROLIFERATE` | 增殖 | 分形临时球持续时间增加2.4秒；裂穿穿透次数减少1次 |
| HERO_MIRAGE | 蜃影 | P4 | 0 | Default | 0 | `TECH_MIRAGE_P4_BASE` | 基准 | 分形临时球持续时间增加0.6秒 |
| HERO_MIRAGE | 蜃影 | P4 | 1 | Advanced | 15 | `TECH_MIRAGE_P4_HIVE` | 虫群 | 分形临时球持续时间增加3秒；磁吸自动吸附道具数减少1个 |
| HERO_MIRAGE | 蜃影 | P4 | 2 | Advanced | 15 | `TECH_MIRAGE_P4_HOMECOMING` | 归巢 | 广域挡板增幅增加9%，持续8秒；疾风球速增幅减少6%，持续5秒 |
| HERO_MIRAGE | 蜃影 | P5 | 0 | Default | 0 | `TECH_MIRAGE_P5_BASE` | 基准 | 分形临时球持续时间增加0.6秒 |
| HERO_MIRAGE | 蜃影 | P5 | 1 | Advanced | 15 | `TECH_MIRAGE_P5_FULLNEST` | 满巢 | 分形临时球持续时间增加3.6秒；缓滞砖潮减速减少10%，持续6秒 |
| HERO_MIRAGE | 蜃影 | P5 | 2 | Advanced | 15 | `TECH_MIRAGE_P5_STARBURST` | 星爆 | 裂穿穿透次数增加1次；分形临时球持续时间减少2.4秒 |
| HERO_PULSE | 脉冲 | P1 | 0 | Default | 0 | `TECH_PULSE_P1_BASE` | 基准 | 疾风球速增幅增加3%，持续5秒 |
| HERO_PULSE | 脉冲 | P1 | 1 | Advanced | 15 | `TECH_PULSE_P1_PIERCE` | 穿刺 | 裂穿穿透次数增加1次；疾风球速增幅减少9%，持续5秒 |
| HERO_PULSE | 脉冲 | P1 | 2 | Advanced | 15 | `TECH_PULSE_P1_SPRINT` | 疾走 | 疾风球速增幅增加9%，持续5秒；缓滞砖潮减速减少4%，持续6秒 |
| HERO_PULSE | 脉冲 | P2 | 0 | Default | 0 | `TECH_PULSE_P2_BASE` | 基准 | 疾风球速增幅增加3%，持续5秒 |
| HERO_PULSE | 脉冲 | P2 | 1 | Advanced | 15 | `TECH_PULSE_P2_ARMORBREAK` | 破甲 | 裂穿穿透次数增加1次；分形临时球持续时间减少1.8秒 |
| HERO_PULSE | 脉冲 | P2 | 2 | Advanced | 15 | `TECH_PULSE_P2_OVERCLOCK` | 超频 | 疾风球速增幅增加12%，持续5秒；广域挡板增幅减少9%，持续8秒 |
| HERO_PULSE | 脉冲 | P3 | 0 | Default | 0 | `TECH_PULSE_P3_BASE` | 基准 | 疾风球速增幅增加3%，持续5秒 |
| HERO_PULSE | 脉冲 | P3 | 1 | Advanced | 15 | `TECH_PULSE_P3_REDLINE` | 红线 | 疾风球速增幅增加9%，持续5秒；缓滞砖潮减速减少4%，持续6秒 |
| HERO_PULSE | 脉冲 | P3 | 2 | Advanced | 15 | `TECH_PULSE_P3_STASIS` | 凝滞 | 缓滞砖潮减速增加6%，持续6秒；疾风球速增幅减少6%，持续5秒 |
| HERO_PULSE | 脉冲 | P4 | 0 | Default | 0 | `TECH_PULSE_P4_BASE` | 基准 | 疾风球速增幅增加3%，持续5秒 |
| HERO_PULSE | 脉冲 | P4 | 1 | Advanced | 15 | `TECH_PULSE_P4_BRAKE` | 制动 | 广域挡板增幅增加9%，持续8秒；疾风球速增幅减少6%，持续5秒 |
| HERO_PULSE | 脉冲 | P4 | 2 | Advanced | 15 | `TECH_PULSE_P4_MAXSPEED` | 极速 | 疾风球速增幅增加12%，持续5秒；广域挡板增幅减少9%，持续8秒 |
| HERO_PULSE | 脉冲 | P5 | 0 | Default | 0 | `TECH_PULSE_P5_BASE` | 基准 | 疾风球速增幅增加3%，持续5秒 |
| HERO_PULSE | 脉冲 | P5 | 1 | Advanced | 15 | `TECH_PULSE_P5_CLOUDPIERCE` | 穿云 | 裂穿穿透次数增加1次；疾风球速增幅减少12%，持续5秒 |
| HERO_PULSE | 脉冲 | P5 | 2 | Advanced | 15 | `TECH_PULSE_P5_RAMPAGE` | 暴走 | 疾风球速增幅增加15%，持续5秒；缓滞砖潮减速减少8%，持续6秒 |
| HERO_RIFT | 裂痕 | P1 | 0 | Default | 0 | `TECH_RIFT_P1_BASE` | 基准 | 裂穿穿透次数增加1次 |
| HERO_RIFT | 裂痕 | P1 | 1 | Advanced | 15 | `TECH_RIFT_P1_DEEPDRILL` | 深钻 | 裂穿穿透次数增加1次；分形临时球持续时间减少2.4秒 |
| HERO_RIFT | 裂痕 | P1 | 2 | Advanced | 15 | `TECH_RIFT_P1_STEADY` | 稳盘 | 广域挡板增幅增加9%，持续8秒；磁吸自动吸附道具数减少1个 |
| HERO_RIFT | 裂痕 | P2 | 0 | Default | 0 | `TECH_RIFT_P2_BASE` | 基准 | 裂穿穿透次数增加1次 |
| HERO_RIFT | 裂痕 | P2 | 1 | Advanced | 15 | `TECH_RIFT_P2_BREAKARRAY` | 破阵 | 裂穿穿透次数增加1次；分形临时球持续时间减少1.8秒 |
| HERO_RIFT | 裂痕 | P2 | 2 | Advanced | 15 | `TECH_RIFT_P2_PENETRATE` | 贯穿 | 裂穿穿透次数增加1次；疾风球速增幅减少15%，持续5秒 |
| HERO_RIFT | 裂痕 | P3 | 0 | Default | 0 | `TECH_RIFT_P3_BASE` | 基准 | 裂穿穿透次数增加1次 |
| HERO_RIFT | 裂痕 | P3 | 1 | Advanced | 15 | `TECH_RIFT_P3_STARPIERCE` | 贯星 | 裂穿穿透次数增加1次；广域挡板增幅减少12%，持续8秒 |
| HERO_RIFT | 裂痕 | P3 | 2 | Advanced | 15 | `TECH_RIFT_P3_WALLSTAB` | 凝壁 | 缓滞砖潮减速增加6%，持续6秒；裂穿穿透次数减少1次 |
| HERO_RIFT | 裂痕 | P4 | 0 | Default | 0 | `TECH_RIFT_P4_BASE` | 基准 | 裂穿穿透次数增加1次 |
| HERO_RIFT | 裂痕 | P4 | 1 | Advanced | 15 | `TECH_RIFT_P4_DRILLDOWN` | 钻地 | 裂穿穿透次数增加1次；分形临时球持续时间减少3秒 |
| HERO_RIFT | 裂痕 | P4 | 2 | Advanced | 15 | `TECH_RIFT_P4_WIDEFIELD` | 广域 | 广域挡板增幅增加9%，持续8秒；疾风球速增幅减少6%，持续5秒 |
| HERO_RIFT | 裂痕 | P5 | 0 | Default | 0 | `TECH_RIFT_P5_BASE` | 基准 | 裂穿穿透次数增加1次 |
| HERO_RIFT | 裂痕 | P5 | 1 | Advanced | 15 | `TECH_RIFT_P5_BOUNDLESS` | 无界 | 裂穿穿透次数增加1次；缓滞砖潮减速减少12%，持续6秒 |
| HERO_RIFT | 裂痕 | P5 | 2 | Advanced | 15 | `TECH_RIFT_P5_STARCHATTER` | 碎星 | 裂穿穿透次数增加1次；磁吸自动吸附道具数减少1个 |
| HERO_REFRACT | 折光 | P1 | 0 | Default | 0 | `TECH_REFRACT_P1_BASE` | 基准 | 广域挡板增幅增加3%，持续8秒 |
| HERO_REFRACT | 折光 | P1 | 1 | Advanced | 15 | `TECH_REFRACT_P1_MAGNETSTAR` | 吸星 | 磁吸自动吸附道具数增加1个；分形临时球持续时间减少0.6秒 |
| HERO_REFRACT | 折光 | P1 | 2 | Advanced | 15 | `TECH_REFRACT_P1_STEADYMIRROR` | 稳镜 | 广域挡板增幅增加9%，持续8秒；疾风球速增幅减少6%，持续5秒 |
| HERO_REFRACT | 折光 | P2 | 0 | Default | 0 | `TECH_REFRACT_P2_BASE` | 基准 | 广域挡板增幅增加3%，持续8秒 |
| HERO_REFRACT | 折光 | P2 | 1 | Advanced | 15 | `TECH_REFRACT_P2_CALIBRATE` | 校准 | 磁吸自动吸附道具数增加1个；疾风球速增幅减少6%，持续5秒 |
| HERO_REFRACT | 折光 | P2 | 2 | Advanced | 15 | `TECH_REFRACT_P2_EXPANDFIELD` | 扩域 | 广域挡板增幅增加12%，持续8秒；分形临时球持续时间减少1.8秒 |
| HERO_REFRACT | 折光 | P3 | 0 | Default | 0 | `TECH_REFRACT_P3_BASE` | 基准 | 广域挡板增幅增加3%，持续8秒 |
| HERO_REFRACT | 折光 | P3 | 1 | Advanced | 15 | `TECH_REFRACT_P3_CORRIDOR` | 回廊 | 广域挡板增幅增加9%，持续8秒；裂穿穿透次数减少1次 |
| HERO_REFRACT | 折光 | P3 | 2 | Advanced | 15 | `TECH_REFRACT_P3_SPLITLIGHT` | 分光 | 分形临时球持续时间增加1.8秒；广域挡板增幅减少6%，持续8秒 |
| HERO_REFRACT | 折光 | P4 | 0 | Default | 0 | `TECH_REFRACT_P4_BASE` | 基准 | 广域挡板增幅增加3%，持续8秒 |
| HERO_REFRACT | 折光 | P4 | 1 | Advanced | 15 | `TECH_REFRACT_P4_GRAVITY` | 引力 | 磁吸自动吸附道具数增加1个；分形临时球持续时间减少1.8秒 |
| HERO_REFRACT | 折光 | P4 | 2 | Advanced | 15 | `TECH_REFRACT_P4_INFINITE` | 无限 | 广域挡板增幅增加12%，持续8秒；疾风球速增幅减少9%，持续5秒 |
| HERO_REFRACT | 折光 | P5 | 0 | Default | 0 | `TECH_REFRACT_P5_BASE` | 基准 | 广域挡板增幅增加3%，持续8秒 |
| HERO_REFRACT | 折光 | P5 | 1 | Advanced | 15 | `TECH_REFRACT_P5_FULLDOMAIN` | 全境 | 广域挡板增幅增加15%，持续8秒；缓滞砖潮减速减少8%，持续6秒 |
| HERO_REFRACT | 折光 | P5 | 2 | Advanced | 15 | `TECH_REFRACT_P5_RETURNFLOW` | 归流 | 磁吸自动吸附道具数增加2个；疾风球速增幅减少12%，持续5秒 |
