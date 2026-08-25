# -*- coding: utf-8 -*-
"""
生成 Gatebreaker v0.3 三界面可交互原型（主菜单 / 英雄舱 / 排行榜）。
重构点（按用户 2026-08-22 要求 + 2026-08-23 参数化要求）：
- 主菜单直接加「英雄」入口；1v1 砖潮挑战不再进入英雄界面（改为直接开局占位）。
- 英雄舱：顶部大头像 + 左右切换；下方展示当前英雄 5 槽科技；点科技弹窗激活/切换。
- 科技配装不再单列，合并进英雄舱。
- 科技数据直接读 DT_PhaseTech.json（含 ResolvedText 具体参数 before->after），HEROES 文案保留自参考原型。
- 槽位与弹窗显示「具体参数变化」而非裸百分数。
"""
import os, json, re

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SRC = os.path.join(ROOT, "doc", "方案与数据", "Gatebreaker v0.3 UI 参考原型.html")
OUT = os.path.join(ROOT, "doc", "方案与数据", "Gatebreaker v0.3 三界面原型.html")
TECH_JSON = os.path.join(ROOT, "Assets", "Config", "DT_PhaseTech.json")

# ---- 读取参考原型的 HEROES 文案（含相位 P1-P5 文本） ----
html = open(SRC, encoding="utf-8").read()
hero_start = html.index("const HEROES")
tech_start = html.index("const TECHS")
heroes_js = html[hero_start:tech_start].rstrip()  # const HEROES = [...];

# ---- 从 DT_PhaseTech.json 构建 TECHS（带具体参数） ----
techs_raw = json.load(open(TECH_JSON, encoding="utf-8"))
TECHS = []
for t in techs_raw:
    fx, detail = [], []
    for e in t.get("Effects", []):
        fx.append([e["ItemName"], e["Op"], e["MagnitudePercent"]])
        detail.append(e.get("ResolvedText", ""))
    TECHS.append({
        "id": t["TechId"], "h": t["HeroId"], "s": t["SlotPhase"], "ph": t["SlotPhase"],
        "n": t["DisplayName"], "k": t["Kind"], "c": t["CostCurrency"],
        "fx": fx, "detail": detail, "mech": t.get("MechanicEffect"),
    })
techs_js = "const TECHS = " + json.dumps(TECHS, ensure_ascii=False) + ";\n"

HERO_TINT = """
const HERO_TINT = {
  "HERO_MIRAGE":"#9B8CFF","HERO_PULSE":"#6E8BFF","HERO_RIFT":"#FF5FA2","HERO_REFRACT":"#2EE6C0"
};"""
LEADERBOARD = """
const LEADERBOARD = [
  {rank:1, name:"NeonGhost",  hero:"HERO_MIRAGE",  rating:1820, w:68, l:42},
  {rank:2, name:"VoltRunner", hero:"HERO_PULSE",   rating:1790, w:65, l:48},
  {rank:3, name:"RiftKing",   hero:"HERO_RIFT",    rating:1755, w:63, l:52},
  {rank:4, name:"PrismAce",   hero:"HERO_REFRACT", rating:1702, w:60, l:55},
  {rank:5, name:"ShadowPaw",  hero:"HERO_MIRAGE",  rating:1658, w:58, l:57},
  {rank:6, name:"TempoFox",   hero:"HERO_PULSE",   rating:1610, w:55, l:60},
  {rank:7, name:"BreachX",    hero:"HERO_RIFT",    rating:1577, w:53, l:61},
  {rank:8, name:"LensWeaver", hero:"HERO_REFRACT", rating:1549, w:52, l:63},
  {rank:9, name:"ComboCat",   hero:"HERO_MIRAGE",  rating:1501, w:50, l:64},
  {rank:10,name:"BeatHop",    hero:"HERO_PULSE",   rating:1466, w:49, l:66},
  {rank:11,name:"DrillBit",   hero:"HERO_RIFT",    rating:1433, w:47, l:67},
  {rank:12,name:"RayFold",    hero:"HERO_REFRACT", rating:1402, w:46, l:69}
];
const ME = {rank:37, name:"You", hero:"HERO_PULSE", rating:1210, w:54, l:67};"""
DATA = heroes_js + "\n" + techs_js + HERO_TINT + "\n" + LEADERBOARD + "\n"

# 改两处渲染：槽位与弹窗显示具体参数
HTML = r"""<!DOCTYPE html>
<html lang="zh-CN">
<head>
<meta charset="utf-8"/>
<meta name="viewport" content="width=device-width, initial-scale=1"/>
<title>Gatebreaker Arena v0.3 · 英雄舱 / 排行榜 原型</title>
<style>
  :root{--bg:#0e1116;--bg2:#141a22;--panel:#171c24;--panel2:#1f2630;--panel3:#232c38;
        --line:#2b3440;--line-strong:#38465a;--txt:#e6edf3;--sub:#8b98a5;--dim:#5b6675;
        --accent:#4cc2ff;--accent2:#2ee6c0;--accent-soft:rgba(76,194,255,.16);
        --good:#46d18a;--bad:#ff6b6b;--warn:#ffb454;
        --gold:#ffd166;--silver:#c7d0da;--bronze:#cd7f47;}
  *{box-sizing:border-box}
  body{margin:0;background:radial-gradient(120% 80% at 50% 0%, #10161f 0%, var(--bg) 60%),var(--bg);
      color:var(--txt);min-height:100vh;
      font:14px/1.5 -apple-system,Segoe UI,Roboto,Helvetica,Arial,"PingFang SC","Microsoft YaHei",sans-serif;}
  .hexbg{position:fixed;inset:0;z-index:0;opacity:.05;pointer-events:none;
      background-image:radial-gradient(circle at 24px 14px, #4cc2ff 2px, transparent 3px);background-size:48px 28px;}
  .app{position:relative;z-index:1;max-width:1000px;margin:0 auto;padding:0 24px 60px;}
  .topbar{display:flex;align-items:center;gap:16px;padding:18px 0 14px;border-bottom:1px solid var(--line);}
  .topbar h1{font-size:20px;margin:0}
  .topbar .en{font:15px/1 "Press Start 2P",monospace;color:var(--accent);letter-spacing:1px;opacity:.8}
  .back{background:transparent;border:1px solid var(--line-strong);color:var(--sub);width:44px;height:44px;border-radius:10px;cursor:pointer;font-size:20px}
  .back:hover{border-color:var(--accent);color:var(--accent)}
  .status{margin-left:auto;color:var(--sub);font-size:13px}
  .bottombar{position:sticky;bottom:0;display:flex;gap:14px;padding:16px 0;margin-top:18px;border-top:1px solid var(--line);
      background:linear-gradient(0deg,var(--bg) 60%,transparent)}
  .btn{padding:14px 22px;border-radius:12px;cursor:pointer;font-size:15px;border:1px solid var(--line);background:var(--panel2);color:var(--txt);transition:.15s;font-weight:600}
  .btn:hover{border-color:var(--accent)}
  .btn.primary{background:linear-gradient(135deg,var(--accent),var(--accent2));color:#06202e;border:none;box-shadow:0 0 12px rgba(76,194,255,.45)}
  .btn.ghost{background:transparent;border:1px solid var(--line-strong);color:var(--sub);font-weight:400}
  .btn:disabled{opacity:.45;cursor:not-allowed;box-shadow:none;border-color:var(--line);color:var(--dim)}
  .spacer{flex:1}
  .screen{display:none}
  .screen.active{display:block;animation:fade .22s ease-out}
  @keyframes fade{from{opacity:0;transform:translateY(6px)}to{opacity:1;transform:none}}
  .sub{color:var(--sub);font-weight:400;font-size:13px}
  .tip{color:var(--sub);background:var(--panel);border:1px solid var(--line);padding:10px 14px;border-radius:10px;margin:14px 0}
  .menu-card{background:var(--panel);border:1px solid var(--line);border-radius:16px;padding:28px;box-shadow:0 0 18px rgba(46,230,192,.12);max-width:520px;margin:30px auto}
  .menu-btn{display:block;width:100%;margin:12px 0;padding:18px;text-align:left;font-size:16px}
  .hangar{display:flex;flex-direction:column;align-items:center;gap:18px;margin-top:8px}
  .avatar-row{display:flex;align-items:center;gap:22px;width:100%;justify-content:center}
  .nav-arrow{width:48px;height:48px;border-radius:50%;background:var(--panel2);border:1px solid var(--line);color:var(--txt);font-size:22px;cursor:pointer}
  .nav-arrow:hover{border-color:var(--accent);color:var(--accent)}
  .avatar{width:150px;height:170px;position:relative;display:flex;flex-direction:column;align-items:center;justify-content:flex-end;
      background:var(--panel);border:2px solid var(--tint);border-radius:18px;box-shadow:0 0 22px var(--glow);padding-bottom:14px}
  .avatar .glyph{position:absolute;top:18px;left:0;right:0;text-align:center;font-size:64px;font-weight:700;color:var(--tint);text-shadow:0 0 18px var(--glow)}
  .avatar .ring{position:absolute;top:14px;left:50%;transform:translateX(-50%);width:84px;height:84px;border-radius:50%;
      border:2px solid var(--tint);opacity:.5}
  .avatar .hname{font-size:22px;font-weight:700}
  .avatar .hen{font:12px/1 "Press Start 2P",monospace;color:var(--sub);letter-spacing:1px;margin-top:4px}
  .hero-meta{text-align:center}
  .hero-meta .hdim{color:var(--accent);font-size:14px;margin-bottom:4px}
  .hero-meta .hcr{color:var(--sub);font-size:13px}
  .phase-strip{display:flex;gap:10px;flex-wrap:wrap;justify-content:center;max-width:760px}
  .phase-chip{flex:1 1 130px;background:var(--panel2);border:1px solid var(--line);border-radius:10px;padding:8px 10px;font-size:12px}
  .phase-chip b{color:var(--accent);font-size:13px}
  .phase-chip span{color:var(--sub)}
  .techs{width:100%;max-width:820px;margin-top:6px}
  .techs h3{font-size:14px;color:var(--sub);margin:14px 0 8px}
  .tech-slot{display:flex;align-items:center;gap:12px;background:var(--panel);border:1px solid var(--line);border-radius:12px;padding:12px 14px;margin:8px 0;cursor:pointer;transition:.12s}
  .tech-slot:hover{border-color:var(--accent)}
  .tech-slot .ph{flex:0 0 64px;color:var(--accent);font-weight:700}
  .tech-slot .ph small{display:block;color:var(--sub);font-weight:400;font-size:11px}
  .tech-slot .cur{flex:1}
  .tech-slot .cur .nm{font-weight:600;font-size:15px}
  .tech-slot .cur .desc{color:var(--sub);font-size:12px;margin-top:2px}
  .tech-slot .pill{background:var(--panel2);border:1px solid var(--line);color:var(--sub);font-size:11px;padding:2px 8px;border-radius:20px}
  .tech-slot .go{color:var(--accent);font-size:18px}
  .overlay{position:fixed;inset:0;z-index:5;background:rgba(8,11,17,.72);display:none;align-items:center;justify-content:center;padding:20px}
  .overlay.show{display:flex}
  .modal{width:min(580px,96vw);max-height:86vh;overflow:auto;background:var(--panel);border:1px solid var(--accent);border-radius:16px;box-shadow:0 0 24px rgba(76,194,255,.3);padding:20px}
  .modal h3{margin:0 0 4px;font-size:16px}
  .modal .msub{color:var(--sub);font-size:12px;margin-bottom:14px}
  .opt{background:var(--panel2);border:1px solid var(--line);border-radius:12px;padding:12px 14px;margin:10px 0;cursor:pointer;transition:.12s}
  .opt:hover{border-color:var(--accent)}
  .opt.on{border:2px solid var(--accent);box-shadow:0 0 10px rgba(76,194,255,.35)}
  .opt .onm{font-weight:600;font-size:15px}
  .opt .badge{background:var(--accent);color:#06202e;font-size:10px;padding:1px 6px;border-radius:10px;margin-left:6px}
  .opt .cost{color:var(--warn);font-size:12px;margin-left:6px}
  .opt .fx{margin-top:4px;font-size:12px}
  .opt .mech{color:var(--accent);font-size:12px;margin-top:4px;background:rgba(76,194,255,.08);padding:6px 8px;border-radius:8px}
  .up{color:var(--good)} .dn{color:var(--bad)}
  .modal-actions{display:flex;gap:12px;margin-top:14px}
  .lb-tools{display:flex;gap:10px;flex-wrap:wrap;margin:14px 0}
  .lb-tools select{background:var(--panel2);color:var(--txt);border:1px solid var(--line);border-radius:8px;padding:8px 10px}
  table.lb{width:100%;border-collapse:collapse;font-size:14px}
  table.lb th,table.lb td{border-bottom:1px solid var(--line);padding:10px 12px;text-align:center}
  table.lb th{color:var(--sub);font-size:12px}
  table.lb td.name{text-align:left;font-weight:600}
  .medal{display:inline-block;min-width:22px;height:22px;line-height:22px;border-radius:50%;font-size:12px;font-weight:700;color:#06202e}
  .m1{background:var(--gold)} .m2{background:var(--silver)} .m3{background:var(--bronze)}
  .mrow.me{background:var(--accent-soft)}
  .mrow.me td.name{box-shadow:inset 4px 0 0 var(--accent)}
  .hero-chip{font-size:11px;padding:2px 8px;border-radius:10px;border:1px solid var(--line-strong)}
  .wr{height:6px;background:var(--panel2);border-radius:4px;overflow:hidden;width:60px;display:inline-block;vertical-align:middle}
  .wr i{display:block;height:100%;background:var(--accent2)}
</style>
</head>
<body>
<div class="hexbg"></div>
<div class="app">
  <div class="topbar">
    <button class="back" id="backBtn" style="display:none" title="返回">‹</button>
    <div><h1 id="screenTitle">主菜单</h1><div class="en" id="screenEn">MAIN MENU</div></div>
    <div class="status" id="statusBar"></div>
  </div>

  <section id="sc-menu" class="screen active">
    <div class="menu-card">
      <p class="tip" style="margin-top:0">「英雄」从主菜单直达英雄舱（含科技配装）；不再由挑战入口进入。</p>
      <button class="btn primary menu-btn" id="toHero">🦸 英雄</button>
      <button class="btn menu-btn" id="toLb">🏆 排行榜</button>
      <button class="btn menu-btn" id="toMatch">⚔ 1v1 砖潮挑战（直接开局）</button>
    </div>
  </section>

  <section id="sc-hero" class="screen">
    <div class="hangar">
      <div class="avatar-row">
        <button class="nav-arrow" id="prevHero">‹</button>
        <div class="avatar" id="avatar"><div class="ring"></div><div class="glyph" id="avGlyph">蜃</div>
          <div class="hname" id="avName">蜃影</div><div class="hen" id="avEn">MIRAGE</div></div>
        <button class="nav-arrow" id="nextHero">›</button>
      </div>
      <div class="hero-meta">
        <div class="hdim" id="heroDim">维度 · 数量</div>
        <div class="hcr" id="heroCr">核心资源：Combo ｜ 核心道具：分形</div>
      </div>
      <div class="phase-strip" id="phaseStrip"></div>
      <div class="techs">
        <h3>科技配装（点击槽位激活 / 切换 · 每槽 +30 守恒）</h3>
        <div id="slotList"></div>
      </div>
    </div>
  </section>

  <section id="sc-lb" class="screen">
    <h2>排行榜 <span class="sub">LEADERBOARD（本地占位数据，后端就绪替换）</span></h2>
    <div class="lb-tools">
      <label class="sub">模式 <select id="lbMode"><option>1v1 砖潮</option><option>联机</option></select></label>
      <label class="sub">范围 <select id="lbScope"><option>全球</option><option>本区</option><option>好友</option></select></label>
      <label class="sub">排序 <select id="lbSort"><option value="rating">积分 ↓</option><option value="win">胜率 ↓</option></select></label>
    </div>
    <table class="lb" id="lbTable"></table>
  </section>

  <div class="bottombar" id="bottombar">
    <button class="btn ghost" id="exitBtn">返回</button>
    <div class="spacer"></div>
    <button class="btn primary" id="enterBtn">进入</button>
  </div>
</div>

<div class="overlay" id="overlay">
  <div class="modal">
    <h3 id="modalTitle">科技槽</h3>
    <div class="msub" id="modalSub"></div>
    <div id="optList"></div>
    <div class="modal-actions">
      <button class="btn ghost" id="modalClose">关闭</button>
    </div>
  </div>
</div>

<script>
__DATA__

/* ===== 导航 ===== */
const SCREENS = {
  menu:{title:"主菜单",en:"MAIN MENU",back:null,exit:null,enter:null},
  hero:{title:"英雄舱",en:"HERO HANGAR",back:"menu",exit:"menu",enter:"match",enterLabel:"进入对局",exitLabel:"返回主菜单"},
  lb:{title:"排行榜",en:"LEADERBOARD",back:"menu",exit:"menu",enter:"menu",enterLabel:"返回主菜单",exitLabel:"返回主菜单"}
};
let cur="menu";
let heroIdx=0;
let state={hero:null, loadout:{}};
const backBtn=document.getElementById('backBtn'), exitBtn=document.getElementById('exitBtn'),
      enterBtn=document.getElementById('enterBtn'), statusBar=document.getElementById('statusBar');

function show(name){
  cur=name;
  document.querySelectorAll('.screen').forEach(s=>s.classList.remove('active'));
  document.getElementById('sc-'+name).classList.add('active');
  const cfg=SCREENS[name];
  document.getElementById('screenTitle').textContent=cfg.title;
  document.getElementById('screenEn').textContent=cfg.en;
  backBtn.style.display=cfg.back?'block':'none';
  backBtn.onclick=()=>cfg.back&&show(cfg.back);
  if(cfg.exit){exitBtn.style.display='inline-block';exitBtn.textContent=cfg.exitLabel;exitBtn.onclick=()=>show(cfg.exit);}else exitBtn.style.display='none';
  if(cfg.enter){enterBtn.style.display='inline-block';enterBtn.textContent=cfg.enterLabel;enterBtn.onclick=()=>{
      if(cur==='hero') alert('（占位）进入对局：Hero='+state.hero+'，TechIds='+JSON.stringify(Object.values(state.loadout)));
      else show('menu');
  };}else enterBtn.style.display='none';
  renderStatus();
  if(name==='hero'){ if(!state.hero) selectHero(0,false); else renderHero(); }
  if(name==='lb') renderLeaderboard();
}
function renderStatus(){
  if(cur==='hero') statusBar.textContent=HEROES[heroIdx].name+" · 已配 "+Object.keys(state.loadout).length+"/5";
  else if(cur==='lb') statusBar.textContent="我的排名 #"+ME.rank;
  else statusBar.textContent="";
}

document.getElementById('toHero').onclick=()=>{ show('hero'); };
document.getElementById('toLb').onclick=()=>show('lb');
document.getElementById('toMatch').onclick=()=>alert('（占位）直接开局：使用上次/默认配装');

/* ===== 英雄舱 ===== */
function selectHero(i, fill){
  heroIdx=(i+HEROES.length)%HEROES.length;
  state.hero=HEROES[heroIdx].id;
  if(fill!==false) autoFill();
  renderHero();
}
function autoFill(){
  const hero=HEROES[heroIdx];
  state.loadout={};
  hero.phases.forEach(ph=>{ const d=TECHS.find(t=>t.h===hero.id&&t.s===ph.p&&t.k==='Default'); if(d) state.loadout[ph.p]=d.id; });
}
function heroTechs(ph){ return TECHS.filter(t=>t.h===HEROES[heroIdx].id&&t.s===ph.p); }
function detailHtml(t){
  let s = t.detail.map(d=>`<span class="sub">${d}</span>`).join('<br>');
  if(t.mech) s += `<br><span class="up">🔧 ${t.mech}</span>`;
  return s;
}
function renderHero(){
  const h=HEROES[heroIdx], tint=HERO_TINT[h.id];
  const av=document.getElementById('avatar'); av.style.setProperty('--tint',tint); av.style.setProperty('--glow',tint+'55');
  document.getElementById('avGlyph').textContent=h.name[0];
  document.getElementById('avName').textContent=h.name;
  document.getElementById('avEn').textContent=h.id.replace('HERO_','');
  document.getElementById('heroDim').textContent='维度 · '+h.dimension;
  document.getElementById('heroCr').textContent='核心资源：'+h.coreResource+' ｜ 核心道具：'+h.coreItem;
  document.getElementById('phaseStrip').innerHTML=h.phases.map(p=>
    `<div class="phase-chip"><b>${p.p} · Φ${p.phi}</b><br><span>${p.text}</span></div>`).join('');
  renderSlots(); renderStatus();
}
function renderSlots(){
  const h=HEROES[heroIdx];
  document.getElementById('slotList').innerHTML=h.phases.map(ph=>{
    const sel=state.loadout[ph.p];
    const t=sel?TECHS.find(x=>x.id===sel):null;
    const desc=t? detailHtml(t) : '未选（点击激活）';
    return `<div class="tech-slot" data-ph="${ph.p}">
      <div class="ph">${ph.p}<small>Φ${ph.phi}</small></div>
      <div class="cur"><div class="nm">${t?t.n:'— 未选 —'}</div><div class="desc">${desc}</div></div>
      <div class="pill">+30</div><div class="go">›</div></div>`;
  }).join('');
  document.querySelectorAll('.tech-slot').forEach(s=>s.onclick=()=>openModal(s.dataset.ph));
}
document.getElementById('prevHero').onclick=()=>selectHero(heroIdx-1,true);
document.getElementById('nextHero').onclick=()=>selectHero(heroIdx+1,true);

/* ===== 科技激活/切换弹窗 ===== */
const overlay=document.getElementById('overlay');
let modalPh=null;
function openModal(ph){
  modalPh=ph;
  const h=HEROES[heroIdx];
  const sel=state.loadout[ph];
  const opts=heroTechs(ph);
  document.getElementById('modalTitle').textContent=ph+' 科技 · '+h.name;
  document.getElementById('modalSub').textContent='选择一个科技激活（基准免费，进阶 15 币）';
  document.getElementById('optList').innerHTML=opts.map(t=>{
    const on=sel===t.id;
    return `<div class="opt ${on?'on':''}" data-id="${t.id}">
      <div class="onm">${t.n}${t.k==='Advanced'?' <span class="badge">◆</span>':''}${t.c?`<span class="cost">(${t.c}币)</span>`:''}${on?' <span style="color:var(--accent)">· 已激活</span>':''}</div>
      <div class="fx">${detailHtml(t)}</div>
    </div>`;
  }).join('');
  document.querySelectorAll('.opt').forEach(o=>o.onclick=()=>{
    state.loadout[modalPh]=o.dataset.id;
    closeModal(); renderSlots(); renderStatus();
  });
  overlay.classList.add('show');
}
function closeModal(){ overlay.classList.remove('show'); }
document.getElementById('modalClose').onclick=closeModal;
overlay.onclick=(e)=>{ if(e.target===overlay) closeModal(); };

/* ===== 排行榜 ===== */
function renderLeaderboard(){
  const sort=document.getElementById('lbSort').value;
  const rows=LEADERBOARD.slice(); rows.push(ME);
  rows.sort((a,b)=> sort==='win'? (b.w/(b.w+b.l))-(a.w/(a.w+a.l)) : b.rating-a.rating);
  document.getElementById('lbTable').innerHTML=`<tr><th>名次</th><th>玩家</th><th>主英雄</th><th>积分</th><th>胜率</th><th>战绩</th></tr>`+
    rows.map(r=>{ const wr=Math.round(r.w/(r.w+r.l)*100), tint=HERO_TINT[r.hero];
      const medal=r.rank<=3?`<span class="medal m${r.rank}">${r.rank}</span>`:('#'+r.rank);
      return `<tr class="mrow ${r===ME?'me':''}"><td>${medal}</td><td class="name">${r===ME?'★ ':''}${r.name}</td>
        <td><span class="hero-chip" style="color:${tint};border-color:${tint}">${HEROES.find(h=>h.id===r.hero).name}</span></td>
        <td>${r.rating}</td><td><span class="wr"><i style="width:${wr}%"></i></span> ${wr}%</td><td class="sub">${r.w}-${r.l}</td></tr>`;
    }).join('');
}
['lbMode','lbScope','lbSort'].forEach(id=>document.getElementById(id).onchange=renderLeaderboard);

show('menu');
</script>
</body>
</html>
"""

open(OUT,"w",encoding="utf-8").write(HTML.replace("__DATA__",DATA))
print("written:",OUT,len(HTML),"bytes")
