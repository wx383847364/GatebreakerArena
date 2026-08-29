# Gatebreaker Arena v0.3 相位效果 SkillName / SkillDesc 数据表

> 每英雄 5 个相位槽（P1–P5），对应 Unity 界面 5 个技能槽。
> `SkillName` = 相位俗称，`SkillDesc` = 阶段效果文本（来自 `DT_PhaseHero.json` 的 `EffectText`）。

| HeroId | HeroName | Phase | Nature | PhiToReach | SkillName | SkillDesc |
|---|---|---|---|---:|---|---|
| HERO_MIRAGE | 蜃影 | P1 | Identity | 0 | 身份 | 主球连击达8次时分裂1颗持续6秒的临时球 |
| HERO_MIRAGE | 蜃影 | P2 | Scale | 30 | 蓄势 | 主球需求连击次数降至6次 |
| HERO_MIRAGE | 蜃影 | P3 | Active | 50 | 转折 | 「幻潮」：复制场上所有球，最多增加2颗临时球，持续6秒。冷却时间30秒 |
| HERO_MIRAGE | 蜃影 | P4 | Scale | 70 | 共鸣 | 分裂生成的临时球增加到2颗 |
| HERO_MIRAGE | 蜃影 | P5 | Climax | 100 | 收束 | 连击达4次时分裂1颗临时球；场上每多1颗物理球挡板长度增加+4%（上限+12%） |
| HERO_PULSE | 脉冲 | P1 | Identity | 0 | 身份 | 每次挡板有效改向增加1点节拍，每3秒无改向减少1点节拍；每节拍球速+3%（上限+12%） |
| HERO_PULSE | 脉冲 | P2 | Scale | 30 | 蓄势 | 节拍上限提高到5 |
| HERO_PULSE | 脉冲 | P3 | Active | 50 | 转折 | 「爆点」：锁定当前最高节拍5秒，期间速度上限提高至6.0。冷却时间30秒 |
| HERO_PULSE | 脉冲 | P4 | Scale | 70 | 共鸣 | 每节拍球速增加至5%，速度上限为20% |
| HERO_PULSE | 脉冲 | P5 | Climax | 100 | 收束 | 节拍大于4次时每第3次命中获得1次穿透 |
| HERO_RIFT | 裂痕 | P1 | Identity | 0 | 身份 | 蓄力满5点后主球获得1次穿透，最多存2次（与道具裂穿共用穿透计数） |
| HERO_RIFT | 裂痕 | P2 | Scale | 30 | 蓄势 | 穿透储存上限提高到3次 |
| HERO_RIFT | 裂痕 | P3 | Active | 50 | 转折 | 「贯裂」：主球立即获得4次穿透并且增加12%速度，效果持续到穿透次数耗尽。冷却时间30秒 |
| HERO_RIFT | 裂痕 | P4 | Scale | 70 | 共鸣 | 蓄力满释放时主球获得穿透增加到2次（储存上限保持3次） |
| HERO_RIFT | 裂痕 | P5 | Climax | 100 | 收束 | 蓄力到达4次后获得穿透效果；穿透命中砖块返还1点蓄力。 |
| HERO_REFRACT | 折光 | P1 | Identity | 0 | 身份 | 主球被挡板回球获得折射标记；带标记命中砖块完成折射循环，完成后下一次挡板回球速度+8%，持续3秒 |
| HERO_REFRACT | 折光 | P2 | Scale | 30 | 蓄势 | 折射循环速度加成提高到+12%；标记持续增加到3秒 |
| HERO_REFRACT | 折光 | P3 | Active | 50 | 转折 | 「镜界」：本方半场生成一面6s镜面，只与本方弹球碰撞。冷却时间30秒 |
| HERO_REFRACT | 折光 | P4 | Scale | 70 | 共鸣 | 折射循环的速度加成改为可叠加：每次循环+6%挡板回球速度，最多叠加3层（合计+18%），折射标记持续增加到4秒 |
| HERO_REFRACT | 折光 | P5 | Climax | 100 | 收束 | 每完成2次折射循环，镜界冷却-3s |
