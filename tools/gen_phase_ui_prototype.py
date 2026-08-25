# -*- coding: utf-8 -*-
"""Generate v0.3 UI reference prototype HTML from the 5 DT_Phase* source tables.

Data is injected straight from the config tables so the prototype can never
drift from the real tuning values. P4 is a normal 3-option tech slot just like
P1~P5; the old P4 protocol effects were folded into the P4 *advanced* techs as
a `MechanicEffect` string (see tools/merge_p4_protocols.py).
"""
import json
import os

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
CFG = os.path.join(ROOT, "Assets", "Config")


def load(name):
    with open(os.path.join(CFG, name), encoding="utf-8") as f:
        return json.load(f)


heroes_raw = load("DT_PhaseHero.json")
techs_raw = load("DT_PhaseTech.json")
items_raw = load("DT_PhaseItem.json")
curves_raw = load("DT_PhaseCurve.json")
meta_raw = load("DT_PhaseMeta.json")

item_name = {it["ItemId"]: it["ItemName"] for it in items_raw}

# ---- heroes ----
heroes = []
for h in heroes_raw:
    phases = [{"p": pl["PhaseLevel"], "phi": pl["PhiToReach"], "text": pl["EffectText"]}
              for pl in h["PhaseLevels"]]
    heroes.append({
        "id": h["HeroId"],
        "name": h["DisplayName"],
        "dimension": h["Dimension"],
        "coreResource": h["CoreResource"],
        "coreItem": item_name.get(h["CoreItem"], h["CoreItem"]),
        "phases": phases,
    })

# ---- techs (P4 advanced techs carry a folded-in MechanicEffect) ----
techs = []
for t in techs_raw:
    fx = [[e["ItemName"], e["Op"], e["MagnitudePercent"]] for e in t.get("Effects", [])]
    techs.append({
        "id": t["TechId"], "h": t["HeroId"], "s": t["SlotPhase"], "k": t["Kind"],
        "n": t["DisplayName"], "c": t["CostCurrency"], "net": t["NetOffset"], "fx": fx,
        "mech": t.get("MechanicEffect", ""),
    })

items = [{"id": it["ItemId"], "name": it["ItemName"],
          "vw": it["ValueWeight"], "drop": it["BaseDropWeight"]} for it in items_raw]

curve = curves_raw[0]
curves = {"stages": [{"s": st["Stage"], "t": st["TimeStart"],
                     "g": st["GreenWeight"], "y": st["YellowWeight"],
                     "r": st["RedWeight"], "m": st["MysteryWeight"]}
                    for st in curve["Stages"]],
          "breakTh": curve["BreakCounterThreshold"]}

meta = {k: meta_raw[0][k] for k in
        ("CurrencyWin", "CurrencyLoss", "TechUnlockCost", "NetOffsetBudget",
         "DropOffsetCap", "PhiPerSecondCap", "ScissorDiffTargetSeconds")}

# ---- HTML template ----
TPL = r"""<!DOCTYPE html>
<html lang="zh-CN">
<head>
<meta charset="utf-8"/>
<meta name="viewport" content="width=device-width, initial-scale=1"/>
<title>Gatebreaker Arena v0.3 · UI 参考原型</title>
<style>
  :root{--bg:#0e1116;--panel:#171c24;--panel2:#1f2630;--line:#2b3440;--txt:#e6edf3;
        --sub:#8b98a5;--accent:#4cc2ff;--good:#46d18a;--bad:#ff6b6b;--warn:#ffb454;}
  *{box-sizing:border-box}
  body{margin:0;background:var(--bg);color:var(--txt);font:14px/1.5 -apple-system,Segoe UI,Roboto,Helvetica,Arial,"PingFang SC","Microsoft YaHei",sans-serif}
  header{padding:14px 20px;border-bottom:1px solid var(--line);display:flex;align-items:center;gap:18px;flex-wrap:wrap}
  header h1{font-size:16px;margin:0}
  nav{display:flex;gap:8px;margin-left:auto}
  nav button{background:var(--panel2);color:var(--sub);border:1px solid var(--line);padding:6px 12px;border-radius:8px;cursor:pointer}
  nav button.active{color:var(--accent);border-color:var(--accent)}
  main{padding:20px;max-width:1080px;margin:0 auto}
  .screen{display:none}
  .screen.active{display:block}
  h2{font-size:15px;margin:0 0 10px}
  .sub{color:var(--sub);font-weight:400;font-size:13px}
  .tip{color:var(--sub);background:var(--panel);border:1px solid var(--line);padding:8px 12px;border-radius:8px;margin:0 0 14px}
  .grid{display:grid;gap:14px}
  .g2{grid-template-columns:1fr 1fr}
  .g4{grid-template-columns:repeat(4,1fr)}
  @media(max-width:880px){.g2,.g4{grid-template-columns:1fr}}
  .card{background:var(--panel);border:1px solid var(--line);border-radius:12px;padding:14px}
  .hero-card{cursor:pointer;transition:.15s}
  .hero-card:hover{border-color:var(--accent)}
  .hero-card.sel{border-color:var(--accent);box-shadow:0 0 0 1px var(--accent)}
  .hero-card h3{margin:0 0 6px}
  .dim{color:var(--accent);font-size:12px}
  .cr{color:var(--sub);font-size:12px;margin-top:4px}
  .phase-list{margin-top:10px;border-top:1px solid var(--line);padding-top:8px}
  .phase-row{display:flex;gap:8px;font-size:12px;padding:3px 0}
  .pl{color:var(--accent);min-width:28px;font-weight:600}
  .pt{color:var(--sub)}
  .slot{border:1px solid var(--line);border-radius:10px;padding:12px;background:var(--panel2)}
  .slot h4{margin:0 0 8px;display:flex;justify-content:space-between;align-items:center;font-size:13px}
  .pill{background:var(--panel);border:1px solid var(--line);color:var(--sub);font-size:11px;padding:2px 8px;border-radius:20px}
  .tech-opt{display:block;width:100%;text-align:left;background:var(--panel);border:1px solid var(--line);color:var(--txt);
    padding:8px 10px;border-radius:8px;margin:6px 0;cursor:pointer;font-size:13px}
  .tech-opt:hover{border-color:var(--accent)}
  .tech-opt.on{border-color:var(--accent);box-shadow:0 0 0 1px var(--accent)}
  .nm{font-weight:600}
  .badge{background:var(--accent);color:#06202e;font-size:10px;padding:1px 6px;border-radius:10px;margin-left:6px}
  .fx{margin-top:3px;font-size:12px;color:var(--sub)}
  .up{color:var(--good)} .dn{color:var(--bad)}
  .row{display:flex;gap:10px;align-items:center;flex-wrap:wrap}
  .kv{color:var(--sub);font-size:13px}
  .kv b{color:var(--txt)}
  .btn{background:var(--panel2);border:1px solid var(--line);color:var(--txt);padding:6px 12px;border-radius:8px;cursor:pointer}
  .btn.primary{background:var(--accent);color:#06202e;border-color:var(--accent)}
  .btn.ghost{background:transparent}
  table.matrix{width:100%;border-collapse:collapse;margin-top:10px;font-size:13px}
  table.matrix th,table.matrix td{border:1px solid var(--line);padding:6px 10px;text-align:center}
  table.matrix th{color:var(--sub);background:var(--panel2)}
  table.matrix td.it{text-align:left;color:var(--txt);font-weight:600}
  /* HUD */
  .phi-wrap{background:var(--panel);border:1px solid var(--line);border-radius:12px;padding:16px;margin-bottom:14px}
  .phi-track{position:relative;height:22px;background:var(--panel2);border-radius:20px;overflow:hidden;border:1px solid var(--line)}
  .phi-fill{position:absolute;left:0;top:0;bottom:0;width:0;background:linear-gradient(90deg,#2b6cff,#4cc2ff);transition:width .2s}
  .tick{position:absolute;top:-4px;bottom:-4px;width:2px;background:var(--warn)}
  .tick span{position:absolute;top:-18px;left:50%;transform:translateX(-50%);font-size:10px;color:var(--warn);white-space:nowrap}
  .load-strip{display:flex;gap:8px;flex-wrap:wrap;margin:8px 0}
  .chip{background:var(--panel2);border:1px solid var(--line);border-radius:8px;padding:4px 10px;font-size:12px}
  .drop-line{display:flex;gap:8px;flex-wrap:wrap;margin-top:8px}
  .drop-tag{background:var(--panel2);border:1px solid var(--accent);border-radius:8px;padding:4px 10px;font-size:12px;color:var(--accent)}
  .curve-bars{margin-top:8px}
  .cbar{display:flex;align-items:center;gap:8px;font-size:12px;margin:4px 0}
  .cbar .lab{width:42px;color:var(--sub)}
  .cbar .seg{display:flex;height:14px;border-radius:4px;overflow:hidden;flex:1}
  .cbar .seg i{display:block;height:100%}
  .g{background:#46d18a}.y{background:#ffd166}.r{background:#ff6b6b}.m{background:#b07cff}
  .break-box{margin-top:10px;background:var(--panel2);border:1px solid var(--line);border-radius:8px;padding:8px 10px;font-size:12px;color:var(--sub)}
  .break-box b{color:var(--warn)}
  .result-card{font-size:14px;line-height:1.8}
  .hl{color:var(--accent)}
  .note{color:var(--sub);font-size:12px;margin-top:6px}
</style>
</head>
<body>
<header>
  <h1>Gatebreaker Arena v0.3 · UI 参考原型</h1>
  <nav>
    <button data-screen="s1" class="active">① 英雄</button>
    <button data-screen="s2">② 科技配装</button>
    <button data-screen="s3">③ 对局 HUD</button>
    <button data-screen="s4">④ 结算</button>
  </nav>
</header>
<main>

<!-- ============ ① 英雄选择 ============ -->
<section id="s1" class="screen active">
  <h2>相位英雄选择 <span class="sub">（替换 V1：只呈现 4 相位英雄，弃用芯片卡组/路径/签名芯片）</span></h2>
  <p class="tip">点击卡片选择英雄 → 进入「② 科技配装」按该英雄的科技池过滤。每个英雄有 P1~P5 相位成长（Φ 阈值 0/30/50/70/100），每相位槽 3 选 1 科技。<b>P4 已无协议</b>：原协议效果已融并进 P4 进阶科技，作为「机制强化」展示。</p>
  <div id="heroGrid" class="grid g4"></div>
</section>

<!-- ============ ② 科技配装 ============ -->
<section id="s2" class="screen">
  <h2>科技配装 <span class="sub">（战前预设：每相位槽 P1~P5 选 1 项，共 5 项；净偏移每槽守恒 +30）</span></h2>
  <p class="tip" id="loadoutTip">请先在「① 英雄选择」选一个英雄。</p>
  <div class="row" style="margin-bottom:14px;">
    <div class="kv">净偏移预算 / 槽：<b id="budgetInfo">+30</b></div>
    <div class="kv">已选槽：<b id="slotCount">0 / 5</b></div>
    <button class="btn ghost" id="autoFill">按基准自动填满</button>
    <button class="btn ghost" id="clearLoad">清空</button>
  </div>
  <div class="grid g2" id="slotGrid"></div>
  <h2 style="margin-top:20px;">科技装载 → 道具价值矩阵（实时聚合）</h2>
  <p class="sub">所选科技对 6 种道具的「增益 / 削弱」合计；P4 进阶科技额外带「机制强化」文本。这正是局内「科技装载条」展示的内容。</p>
  <table class="matrix" id="valueMatrix"></table>
</section>

<!-- ============ ③ 对局 HUD ============ -->
<section id="s3" class="screen">
  <h2>对局 HUD（局内） <span class="sub">参考布局：Φ 能量条 · 科技装载条 · 相位道具反馈 · 压力指示</span></h2>
  <div class="phi-wrap">
    <div class="kv">Φ 能量：<b id="phiVal">0</b> / 100 ｜ 当前相位 <b id="phiPhase">P1</b></div>
    <div class="phi-track" style="margin-top:10px;">
      <div class="phi-fill" id="phiFill"></div>
      <div class="tick" style="left:0%"><span>P1 · 0</span></div>
      <div class="tick" style="left:30%"><span>P2 · 30</span></div>
      <div class="tick" style="left:50%"><span>P3 · 50</span></div>
      <div class="tick" style="left:70%"><span>P4 · 70</span></div>
      <div class="tick" style="left:100%"><span>P5 · 100</span></div>
    </div>
    <div class="row" style="margin-top:10px;">
      <button class="btn" id="addPhi">模拟操作 +Φ (×2)</button>
      <button class="btn" id="autoPhi">自动累积</button>
      <button class="btn" id="resetPhi">重置</button>
    </div>
  </div>
  <div class="card" style="margin-bottom:14px;">
    <h2 style="margin:0 0 6px;font-size:14px;">科技装载条</h2>
    <div class="load-strip" id="loadStrip"></div>
  </div>
  <div class="card" style="margin-bottom:14px;">
    <h2 style="margin:0 0 6px;font-size:14px;">相位道具掉落反馈</h2>
    <div class="drop-line" id="dropLine"></div>
    <div class="row" style="margin-top:8px;">
      <button class="btn" id="dropBtn">模拟道具掉落</button>
      <span class="note">掉落按 DT_PhaseItem.BaseDropWeight 加权</span>
    </div>
  </div>
  <div class="card">
    <h2 style="margin:0 0 6px;font-size:14px;">压力指示（C0~C5 双层压力）</h2>
    <div class="kv">对局时间：<b id="matchTime">0s</b> ｜ 当前档 <b id="curveStage">C0</b></div>
    <div class="curve-bars" id="curveBars"></div>
    <div class="break-box">破阵计数：<b id="breakCnt">0</b> / __BREAKTH__（达阈值触发反馈）</div>
    <div class="row" style="margin-top:8px;">
      <button class="btn" id="timeBtn">推进 +10s</button>
      <button class="btn" id="breakBtn">破阵 +1</button>
    </div>
  </div>
</section>

<!-- ============ ④ 结算 ============ -->
<section id="s4" class="screen">
  <h2>结算 <span class="sub">货币 / 到达相位 / 成长失败致负归因</span></h2>
  <div class="card result-card">
    <div id="resultBody"></div>
    <div class="row" style="margin-top:14px;">
      <button class="btn primary" id="winBtn">本局胜</button>
      <button class="btn" id="loseBtn">本局负</button>
    </div>
  </div>
</section>

</main>

<script>
const HEROES = __HEROES__;
const TECHS = __TECHS__;
const ITEMS = __ITEMS__;
const CURVES = __CURVES__;
const META = __META__;

const PHI_T = [0,30,50,70,100];

/* ===== 状态 ===== */
let state = { hero:null, loadout:{}, phi:0, phiTimer:null, time:0, breaks:0, drops:[] };
const heroGrid = document.getElementById('heroGrid');

/* ===== 导航 ===== */
document.querySelectorAll('nav button').forEach(b => b.onclick = () => {
  document.querySelectorAll('nav button').forEach(x=>x.classList.remove('active'));
  document.querySelectorAll('.screen').forEach(x=>x.classList.remove('active'));
  b.classList.add('active');
  document.getElementById(b.dataset.screen).classList.add('active');
});

/* ===== ① 英雄选择 ===== */
HEROES.forEach(h => {
  const d = document.createElement('div');
  d.className = 'card hero-card';
  d.innerHTML = `<h3>${h.name}</h3><div class="dim">维度 · ${h.dimension}</div>
    <div class="cr">核心资源：${h.coreResource} ｜ 核心道具：${h.coreItem}</div>
    <div class="phase-list">${h.phases.map(p=>`<div class="phase-row"><span class="pl">${p.p}</span><span class="pt">${p.text}</span></div>`).join('')}</div>`;
  d.onclick = () => {
    document.querySelectorAll('.hero-card').forEach(x=>x.classList.remove('sel'));
    d.classList.add('sel');
    state.hero = h.id; state.loadout = {};
    buildLoadout();
  };
  heroGrid.appendChild(d);
});

/* ===== ② 科技配装 ===== */
const slotGrid = document.getElementById('slotGrid');
const tip = document.getElementById('loadoutTip');
function buildLoadout(){
  if(!state.hero){ slotGrid.innerHTML=''; tip.style.display='block'; return; }
  tip.style.display='none';
  const hero = HEROES.find(h=>h.id===state.hero);
  slotGrid.innerHTML = hero.phases.map(ph => {
    const opts = TECHS.filter(t=>t.h===state.hero && t.s===ph.p);
    const sel = state.loadout[ph.p];
    return `<div class="slot"><h4><span>${ph.p} · Φ${ph.phi}</span><span class="pill">净偏移 +${sel?TECHS.find(t=>t.id===sel).net:30}</span></h4>
      ${opts.map(t=>`<button class="tech-opt ${sel===t.id?'on':''}" data-ph="${ph.p}" data-id="${t.id}">
        <span class="nm">${t.n}${t.k==='Advanced'?' ◆':''}</span>${t.c?` <span style="color:var(--warn)">(${t.c}币)</span>`:''}
        ${t.fx&&t.fx.length?`<div class="fx">${t.fx.map(f=>`<span class="${f[1]==='Enhance'?'up':'dn'}">${f[1]==='Enhance'?'增益':'削弱'} ${f[0]} ${f[2]}%</span>`).join(' ｜ ')}</div>`:''}${t.mech?`<div class="fx" style="color:var(--accent)">机制：${t.mech}</div>`:''}
      </button>`).join('')}</div>`;
  }).join('');
  slotGrid.querySelectorAll('.tech-opt').forEach(btn=>btn.onclick=()=>{
    const ph=btn.dataset.ph, id=btn.dataset.id;
    if(state.loadout[ph]===id) delete state.loadout[ph]; else state.loadout[ph]=id;
    buildLoadout(); renderMatrix();
  });
  renderMatrix();
}
function renderMatrix(){
  const chosen = Object.values(state.loadout).map(id=>TECHS.find(t=>t.id===id)).filter(Boolean);
  document.getElementById('slotCount').textContent = `${chosen.length} / 5`;
  const agg = {};
  ITEMS.forEach(it=>agg[it.id]={up:0,dn:0});
  chosen.forEach(t=>t.fx.forEach(f=>{ if(f[1]==='Enhance') agg[f[0]].up+=f[2]; else agg[f[0]].dn+=f[2]; }));
  document.getElementById('valueMatrix').innerHTML = `<tr><th>道具</th><th>价值权重</th><th>增益合计</th><th>削弱合计</th><th>净</th></tr>` +
    ITEMS.map(it=>{ const a=agg[it.id]; const net=a.up-a.dn;
      return `<tr><td class="it">${it.name}</td><td>${it.vw}</td>
        <td class="up">+${a.up}%</td><td class="dn">-${a.dn}%</td>
        <td style="color:${net>=0?'var(--good)':'var(--bad)'}">${net>=0?'+':''}${net}%</td></tr>`;}).join('');
}
document.getElementById('autoFill').onclick=()=>{
  if(!state.hero) return;
  const hero=HEROES.find(h=>h.id===state.hero);
  state.loadout={};
  hero.phases.forEach(ph=>{
    const def = TECHS.find(t=>t.h===state.hero&&t.s===ph.p&&t.k==='Default')
            || TECHS.find(t=>t.h===state.hero&&t.s===ph.p&&t.def);
    if(def) state.loadout[ph.p]=def.id;
  });
  buildLoadout();
};
document.getElementById('clearLoad').onclick=()=>{ state.loadout={}; buildLoadout(); };

/* ===== ③ HUD ===== */
function phaseOf(phi){ let r='P1'; PHI_T.forEach((t,i)=>{ if(phi>=t) r='P'+(i+1); }); return r; }
function renderPhi(){
  state.phi = Math.min(state.phi, 100);
  document.getElementById('phiVal').textContent = state.phi.toFixed(0);
  document.getElementById('phiFill').style.width = state.phi + '%';
  document.getElementById('phiPhase').textContent = phaseOf(state.phi);
}
function renderLoadStrip(){
  const strip = document.getElementById('loadStrip');
  if(!state.hero){ strip.innerHTML='<span class="note">未选英雄</span>'; return; }
  const hero = HEROES.find(h=>h.id===state.hero);
  strip.innerHTML = hero.phases.map(ph=>{
    const id = state.loadout[ph.p];
    const t = id?TECHS.find(x=>x.id===id):null;
    return `<span class="chip">${ph.p} ${t?t.n:'<span style="color:var(--sub)">未选</span>'}</span>`;
  }).join('');
}
function pickDrop(){
  const tot = ITEMS.reduce((s,it)=>s+it.drop,0);
  let r = Math.random()*tot;
  for(const it of ITEMS){ if((r-=it.drop)<=0) return it; }
  return ITEMS[0];
}
function renderDrops(){
  const dl = document.getElementById('dropLine');
  dl.innerHTML = state.drops.length? state.drops.map(d=>`<span class="drop-tag">${d.name}</span>`).join('') : '<span class="note">暂无掉落</span>';
}
function curveStageAt(t){
  let cur=CURVES.stages[0];
  CURVES.stages.forEach(s=>{ if(t>=s.t) cur=s; });
  return cur;
}
function renderCurve(){
  document.getElementById('matchTime').textContent = state.time + 's';
  const st = curveStageAt(state.time);
  document.getElementById('curveStage').textContent = st.s;
  document.getElementById('curveBars').innerHTML = CURVES.stages.map(s=>{
    const tot=s.g+s.y+s.r+s.m;
    return `<div class="cbar"><span class="lab">${s.s}</span><div class="seg">
      <i class="g" style="width:${s.g/tot*100}%"></i><i class="y" style="width:${s.y/tot*100}%"></i>
      <i class="r" style="width:${s.r/tot*100}%"></i><i class="m" style="width:${s.m/tot*100}%"></i></div></div>`;
  }).join('') + `<div class="note">绿/黄/红/谜 砖占比（数值来自 DT_PhaseCurve，每层谜砖恒定 7%）</div>`;
  document.getElementById('breakCnt').textContent = state.breaks;
}
document.getElementById('addPhi').onclick=()=>{ state.phi+=2; renderPhi(); };
document.getElementById('autoPhi').onclick=()=>{
  if(state.phiTimer){ clearInterval(state.phiTimer); state.phiTimer=null; return; }
  state.phiTimer=setInterval(()=>{ state.phi+=1; renderPhi(); if(state.phi>=100) clearInterval(state.phiTimer); },120);
};
document.getElementById('resetPhi').onclick=()=>{ state.phi=0; renderPhi(); };
document.getElementById('dropBtn').onclick=()=>{ state.drops.push(pickDrop()); renderDrops(); };
document.getElementById('timeBtn').onclick=()=>{ state.time+=10; renderCurve(); };
document.getElementById('breakBtn').onclick=()=>{ state.breaks++; renderCurve(); };

/* ===== ④ 结算 ===== */
function renderResult(win){
  const hero=HEROES.find(h=>h.id===state.hero);
  const reached=phaseOf(state.phi);
  const filled=Object.keys(state.loadout).length;
  let growthFail = (!win && (reached==='P1' || filled<5));
  const cur = win? META.CurrencyWin : META.CurrencyLoss;
  let attr = win? '成长领先，比赛获胜' : (growthFail? '成长失败（未达 P2 或科技未满 5 槽），比赛可输' : '对局操作劣势致负，成长已建立');
  document.getElementById('resultBody').innerHTML =
    `<div>英雄：<span class="hl">${hero?hero.name:'—'}</span></div>
     <div>到达成长相位：<span class="hl">${reached}</span> ｜ 科技槽已选 <span class="hl">${filled}/5</span></div>
     <div>本局结果：<span class="hl">${win?'胜':'负'}</span> ｜ 货币 <span class="hl">+${cur}</span>（胜 ${META.CurrencyWin} / 负 ${META.CurrencyLoss}）</div>
     <div>归因：<span class="hl">${attr}</span></div>
     <div class="note">设计约束：局内成长必须影响比赛结果；成长失败应使比赛可输，无时间兜底。</div>`;
}
document.getElementById('winBtn').onclick=()=>renderResult(true);
document.getElementById('loseBtn').onclick=()=>renderResult(false);

/* ===== 初始化 ===== */
renderPhi(); renderLoadStrip(); renderDrops(); renderCurve();
</script>
</body>
</html>
"""

html = (TPL
        .replace("__HEROES__", json.dumps(heroes, ensure_ascii=False))
        .replace("__TECHS__", json.dumps(techs, ensure_ascii=False))
        .replace("__ITEMS__", json.dumps(items, ensure_ascii=False))
        .replace("__CURVES__", json.dumps(curves, ensure_ascii=False))
        .replace("__META__", json.dumps(meta, ensure_ascii=False))
        .replace("__BREAKTH__", str(curves["breakTh"])))

out_dir = os.path.join(ROOT, "doc", "方案与数据")
os.makedirs(out_dir, exist_ok=True)
out_path = os.path.join(out_dir, "Gatebreaker v0.3 UI 参考原型.html")
with open(out_path, "w", encoding="utf-8") as f:
    f.write(html)

# data sanity
assert len(heroes) == 4, heroes
assert len(techs) == 60, len(techs)  # 60 techs, no protocol pseudo-entries
p4 = [t for t in techs if t["s"] == "P4"]
assert len(p4) == 12, len(p4)  # 3 techs per hero x4
p4_adv = [t for t in p4 if t["k"] == "Advanced"]
assert len(p4_adv) == 8 and all(t["mech"] for t in p4_adv), len(p4_adv)
print("OK generated:", out_path)
print("heroes:", len(heroes), "| techs:", len(techs),
      "| P4 slot options:", len(p4), "| P4 advanced with mech:", len(p4_adv),
      "| items:", len(items))
