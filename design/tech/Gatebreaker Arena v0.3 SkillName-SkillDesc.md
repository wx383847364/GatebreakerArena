# Gatebreaker Arena v0.3 SkillName / SkillDesc 数据表

> 每英雄 5 相位（P1–P5），每相位 3 槽位（SlotIndex 0=基准，1/2=进阶）。
> 本表可直接用于 Unity `SkillName` / `SkillDesc` 字段配置。

| HeroId | HeroName | Phase | SlotIndex | Kind | Cost | TechId | SkillName | SkillDesc |
|---|---|---|---|---:|---|---|---|---|
| HERO_MIRAGE | 蜃影 | P1 | 0 | Default | 0 | `TECH_MIRAGE_P1_BASE` | 基准 | 分形 临时球持续 6s → 6.6s（增强10%） |
| HERO_MIRAGE | 蜃影 | P1 | 1 | Advanced | 15 | `TECH_MIRAGE_P1_LONEWOLF` | 孤狼 | 裂穿 穿透次数 2次 → 3次（增强40%）；分形 临时球持续 6s → 4.2s（削弱30%） |
| HERO_MIRAGE | 蜃影 | P1 | 2 | Advanced | 15 | `TECH_MIRAGE_P1_SWARM` | 蜂拥 | 分形 临时球持续 6s → 9.0s（增强50%）；缓滞 砖潮减速 20% → 12.0%（削弱40%），持续 6s |
| HERO_MIRAGE | 蜃影 | P2 | 0 | Default | 0 | `TECH_MIRAGE_P2_BASE` | 基准 | 分形 临时球持续 6s → 6.6s（增强10%） |
| HERO_MIRAGE | 蜃影 | P2 | 1 | Advanced | 15 | `TECH_MIRAGE_P2_FISSION` | 裂变 | 分形 临时球持续 6s → 8.4s（增强40%）；疾风 球速增幅 20% → 11.0%（削弱45%），持续 5s |
| HERO_MIRAGE | 蜃影 | P2 | 2 | Advanced | 15 | `TECH_MIRAGE_P2_FLOOD` | 洪流 | 分形 临时球持续 6s → 9.6s（增强60%）；广域 挡板增幅 20% → 5.0%（削弱75%），持续 8s |
| HERO_MIRAGE | 蜃影 | P3 | 0 | Default | 0 | `TECH_MIRAGE_P3_BASE` | 基准 | 分形 临时球持续 6s → 6.6s（增强10%） |
| HERO_MIRAGE | 蜃影 | P3 | 1 | Advanced | 15 | `TECH_MIRAGE_P3_DEVOUR` | 吞噬 | 缓滞 砖潮减速 20% → 26.0%（增强30%），持续 6s；分形 临时球持续 6s → 4.8s（削弱20%） |
| HERO_MIRAGE | 蜃影 | P3 | 2 | Advanced | 15 | `TECH_MIRAGE_P3_PROLIFERATE` | 增殖 | 分形 临时球持续 6s → 8.4s（增强40%）；裂穿 穿透次数 2次 → 1次（削弱30%） |
| HERO_MIRAGE | 蜃影 | P4 | 0 | Default | 0 | `TECH_MIRAGE_P4_BASE` | 基准 | 分形 临时球持续 6s → 6.6s（增强10%） |
| HERO_MIRAGE | 蜃影 | P4 | 1 | Advanced | 15 | `TECH_MIRAGE_P4_HIVE` | 虫群 | 分形 临时球持续 6s → 9.0s（增强50%）；磁吸 自动吸附道具数 1个 → 1个（削弱120%） |
| HERO_MIRAGE | 蜃影 | P4 | 2 | Advanced | 15 | `TECH_MIRAGE_P4_HOMECOMING` | 归巢 | 广域 挡板增幅 20% → 29.0%（增强45%），持续 8s；疾风 球速增幅 20% → 14.0%（削弱30%），持续 5s |
| HERO_MIRAGE | 蜃影 | P5 | 0 | Default | 0 | `TECH_MIRAGE_P5_BASE` | 基准 | 分形 临时球持续 6s → 6.6s（增强10%） |
| HERO_MIRAGE | 蜃影 | P5 | 1 | Advanced | 15 | `TECH_MIRAGE_P5_FULLNEST` | 满巢 | 分形 临时球持续 6s → 9.6s（增强60%）；缓滞 砖潮减速 20% → 10.0%（削弱50%），持续 6s |
| HERO_MIRAGE | 蜃影 | P5 | 2 | Advanced | 15 | `TECH_MIRAGE_P5_STARBURST` | 星爆 | 裂穿 穿透次数 2次 → 3次（增强50%）；分形 临时球持续 6s → 3.6s（削弱40%） |
| HERO_PULSE | 脉冲 | P1 | 0 | Default | 0 | `TECH_PULSE_P1_BASE` | 基准 | 疾风 球速增幅 20% → 23.0%（增强15%），持续 5s |
| HERO_PULSE | 脉冲 | P1 | 1 | Advanced | 15 | `TECH_PULSE_P1_PIERCE` | 穿刺 | 裂穿 穿透次数 2次 → 3次（增强40%）；疾风 球速增幅 20% → 11.0%（削弱45%），持续 5s |
| HERO_PULSE | 脉冲 | P1 | 2 | Advanced | 15 | `TECH_PULSE_P1_SPRINT` | 疾走 | 疾风 球速增幅 20% → 29.0%（增强45%），持续 5s；缓滞 砖潮减速 20% → 16.0%（削弱20%），持续 6s |
| HERO_PULSE | 脉冲 | P2 | 0 | Default | 0 | `TECH_PULSE_P2_BASE` | 基准 | 疾风 球速增幅 20% → 23.0%（增强15%），持续 5s |
| HERO_PULSE | 脉冲 | P2 | 1 | Advanced | 15 | `TECH_PULSE_P2_ARMORBREAK` | 破甲 | 裂穿 穿透次数 2次 → 3次（增强40%）；分形 临时球持续 6s → 4.2s（削弱30%） |
| HERO_PULSE | 脉冲 | P2 | 2 | Advanced | 15 | `TECH_PULSE_P2_OVERCLOCK` | 超频 | 疾风 球速增幅 20% → 32.0%（增强60%），持续 5s；广域 挡板增幅 20% → 11.0%（削弱45%），持续 8s |
| HERO_PULSE | 脉冲 | P3 | 0 | Default | 0 | `TECH_PULSE_P3_BASE` | 基准 | 疾风 球速增幅 20% → 23.0%（增强15%），持续 5s |
| HERO_PULSE | 脉冲 | P3 | 1 | Advanced | 15 | `TECH_PULSE_P3_REDLINE` | 红线 | 疾风 球速增幅 20% → 29.0%（增强45%），持续 5s；缓滞 砖潮减速 20% → 16.0%（削弱20%），持续 6s |
| HERO_PULSE | 脉冲 | P3 | 2 | Advanced | 15 | `TECH_PULSE_P3_STASIS` | 凝滞 | 缓滞 砖潮减速 20% → 26.0%（增强30%），持续 6s；疾风 球速增幅 20% → 14.0%（削弱30%），持续 5s |
| HERO_PULSE | 脉冲 | P4 | 0 | Default | 0 | `TECH_PULSE_P4_BASE` | 基准 | 疾风 球速增幅 20% → 23.0%（增强15%），持续 5s |
| HERO_PULSE | 脉冲 | P4 | 1 | Advanced | 15 | `TECH_PULSE_P4_BRAKE` | 制动 | 广域 挡板增幅 20% → 29.0%（增强45%），持续 8s；疾风 球速增幅 20% → 14.0%（削弱30%），持续 5s |
| HERO_PULSE | 脉冲 | P4 | 2 | Advanced | 15 | `TECH_PULSE_P4_MAXSPEED` | 极速 | 疾风 球速增幅 20% → 32.0%（增强60%），持续 5s；广域 挡板增幅 20% → 11.0%（削弱45%），持续 8s |
| HERO_PULSE | 脉冲 | P5 | 0 | Default | 0 | `TECH_PULSE_P5_BASE` | 基准 | 疾风 球速增幅 20% → 23.0%（增强15%），持续 5s |
| HERO_PULSE | 脉冲 | P5 | 1 | Advanced | 15 | `TECH_PULSE_P5_CLOUDPIERCE` | 穿云 | 裂穿 穿透次数 2次 → 3次（增强50%）；疾风 球速增幅 20% → 8.0%（削弱60%），持续 5s |
| HERO_PULSE | 脉冲 | P5 | 2 | Advanced | 15 | `TECH_PULSE_P5_RAMPAGE` | 暴走 | 疾风 球速增幅 20% → 35.0%（增强75%），持续 5s；缓滞 砖潮减速 20% → 12.0%（削弱40%），持续 6s |
| HERO_RIFT | 裂痕 | P1 | 0 | Default | 0 | `TECH_RIFT_P1_BASE` | 基准 | 裂穿 穿透次数 2次 → 2次（增强10%） |
| HERO_RIFT | 裂痕 | P1 | 1 | Advanced | 15 | `TECH_RIFT_P1_DEEPDRILL` | 深钻 | 裂穿 穿透次数 2次 → 3次（增强50%）；分形 临时球持续 6s → 3.6s（削弱40%） |
| HERO_RIFT | 裂痕 | P1 | 2 | Advanced | 15 | `TECH_RIFT_P1_STEADY` | 稳盘 | 广域 挡板增幅 20% → 29.0%（增强45%），持续 8s；磁吸 自动吸附道具数 1个 → 1个（削弱60%） |
| HERO_RIFT | 裂痕 | P2 | 0 | Default | 0 | `TECH_RIFT_P2_BASE` | 基准 | 裂穿 穿透次数 2次 → 2次（增强10%） |
| HERO_RIFT | 裂痕 | P2 | 1 | Advanced | 15 | `TECH_RIFT_P2_BREAKARRAY` | 破阵 | 裂穿 穿透次数 2次 → 3次（增强40%）；分形 临时球持续 6s → 4.2s（削弱30%） |
| HERO_RIFT | 裂痕 | P2 | 2 | Advanced | 15 | `TECH_RIFT_P2_PENETRATE` | 贯穿 | 裂穿 穿透次数 2次 → 3次（增强60%）；疾风 球速增幅 20% → 5.0%（削弱75%），持续 5s |
| HERO_RIFT | 裂痕 | P3 | 0 | Default | 0 | `TECH_RIFT_P3_BASE` | 基准 | 裂穿 穿透次数 2次 → 2次（增强10%） |
| HERO_RIFT | 裂痕 | P3 | 1 | Advanced | 15 | `TECH_RIFT_P3_STARPIERCE` | 贯星 | 裂穿 穿透次数 2次 → 3次（增强50%）；广域 挡板增幅 20% → 8.0%（削弱60%），持续 8s |
| HERO_RIFT | 裂痕 | P3 | 2 | Advanced | 15 | `TECH_RIFT_P3_WALLSTAB` | 凝壁 | 缓滞 砖潮减速 20% → 26.0%（增强30%），持续 6s；裂穿 穿透次数 2次 → 2次（削弱20%） |
| HERO_RIFT | 裂痕 | P4 | 0 | Default | 0 | `TECH_RIFT_P4_BASE` | 基准 | 裂穿 穿透次数 2次 → 2次（增强10%） |
| HERO_RIFT | 裂痕 | P4 | 1 | Advanced | 15 | `TECH_RIFT_P4_DRILLDOWN` | 钻地 | 裂穿 穿透次数 2次 → 3次（增强60%）；分形 临时球持续 6s → 3.0s（削弱50%） |
| HERO_RIFT | 裂痕 | P4 | 2 | Advanced | 15 | `TECH_RIFT_P4_WIDEFIELD` | 广域 | 广域 挡板增幅 20% → 29.0%（增强45%），持续 8s；疾风 球速增幅 20% → 14.0%（削弱30%），持续 5s |
| HERO_RIFT | 裂痕 | P5 | 0 | Default | 0 | `TECH_RIFT_P5_BASE` | 基准 | 裂穿 穿透次数 2次 → 2次（增强10%） |
| HERO_RIFT | 裂痕 | P5 | 1 | Advanced | 15 | `TECH_RIFT_P5_BOUNDLESS` | 无界 | 裂穿 穿透次数 2次 → 3次（增强70%）；缓滞 砖潮减速 20% → 8.0%（削弱60%），持续 6s |
| HERO_RIFT | 裂痕 | P5 | 2 | Advanced | 15 | `TECH_RIFT_P5_STARCHATTER` | 碎星 | 裂穿 穿透次数 2次 → 3次（增强50%）；磁吸 自动吸附道具数 1个 → 1个（削弱120%） |
| HERO_REFRACT | 折光 | P1 | 0 | Default | 0 | `TECH_REFRACT_P1_BASE` | 基准 | 广域 挡板增幅 20% → 23.0%（增强15%），持续 8s |
| HERO_REFRACT | 折光 | P1 | 1 | Advanced | 15 | `TECH_REFRACT_P1_MAGNETSTAR` | 吸星 | 磁吸 自动吸附道具数 1个 → 2个（增强60%）；分形 临时球持续 6s → 5.4s（削弱10%） |
| HERO_REFRACT | 折光 | P1 | 2 | Advanced | 15 | `TECH_REFRACT_P1_STEADYMIRROR` | 稳镜 | 广域 挡板增幅 20% → 29.0%（增强45%），持续 8s；疾风 球速增幅 20% → 14.0%（削弱30%），持续 5s |
| HERO_REFRACT | 折光 | P2 | 0 | Default | 0 | `TECH_REFRACT_P2_BASE` | 基准 | 广域 挡板增幅 20% → 23.0%（增强15%），持续 8s |
| HERO_REFRACT | 折光 | P2 | 1 | Advanced | 15 | `TECH_REFRACT_P2_CALIBRATE` | 校准 | 磁吸 自动吸附道具数 1个 → 2个（增强90%）；疾风 球速增幅 20% → 14.0%（削弱30%），持续 5s |
| HERO_REFRACT | 折光 | P2 | 2 | Advanced | 15 | `TECH_REFRACT_P2_EXPANDFIELD` | 扩域 | 广域 挡板增幅 20% → 32.0%（增强60%），持续 8s；分形 临时球持续 6s → 4.2s（削弱30%） |
| HERO_REFRACT | 折光 | P3 | 0 | Default | 0 | `TECH_REFRACT_P3_BASE` | 基准 | 广域 挡板增幅 20% → 23.0%（增强15%），持续 8s |
| HERO_REFRACT | 折光 | P3 | 1 | Advanced | 15 | `TECH_REFRACT_P3_CORRIDOR` | 回廊 | 广域 挡板增幅 20% → 29.0%（增强45%），持续 8s；裂穿 穿透次数 2次 → 2次（削弱20%） |
| HERO_REFRACT | 折光 | P3 | 2 | Advanced | 15 | `TECH_REFRACT_P3_SPLITLIGHT` | 分光 | 分形 临时球持续 6s → 7.8s（增强30%）；广域 挡板增幅 20% → 14.0%（削弱30%），持续 8s |
| HERO_REFRACT | 折光 | P4 | 0 | Default | 0 | `TECH_REFRACT_P4_BASE` | 基准 | 广域 挡板增幅 20% → 23.0%（增强15%），持续 8s |
| HERO_REFRACT | 折光 | P4 | 1 | Advanced | 15 | `TECH_REFRACT_P4_GRAVITY` | 引力 | 磁吸 自动吸附道具数 1个 → 2个（增强120%）；分形 临时球持续 6s → 4.2s（削弱30%） |
| HERO_REFRACT | 折光 | P4 | 2 | Advanced | 15 | `TECH_REFRACT_P4_INFINITE` | 无限 | 广域 挡板增幅 20% → 32.0%（增强60%），持续 8s；疾风 球速增幅 20% → 11.0%（削弱45%），持续 5s |
| HERO_REFRACT | 折光 | P5 | 0 | Default | 0 | `TECH_REFRACT_P5_BASE` | 基准 | 广域 挡板增幅 20% → 23.0%（增强15%），持续 8s |
| HERO_REFRACT | 折光 | P5 | 1 | Advanced | 15 | `TECH_REFRACT_P5_FULLDOMAIN` | 全境 | 广域 挡板增幅 20% → 35.0%（增强75%），持续 8s；缓滞 砖潮减速 20% → 12.0%（削弱40%），持续 6s |
| HERO_REFRACT | 折光 | P5 | 2 | Advanced | 15 | `TECH_REFRACT_P5_RETURNFLOW` | 归流 | 磁吸 自动吸附道具数 1个 → 3个（增强150%）；疾风 球速增幅 20% → 8.0%（削弱60%），持续 5s |
