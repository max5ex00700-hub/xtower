/* ============================================================
   app.js — UI · 상태 · 내보내기
   ============================================================ */
(function () {
  const AE = window.AE;
  const $ = (s) => document.querySelector(s);
  const cv = $('#cv');
  const ctx = cv.getContext('2d');

  const state = {
    seed: 0,
    world: 'all',
    ch: null,
    locks: {},
    zoom: 'knee',
    bg: true,
  };

  /* ---------------- 시드 유틸 ---------------- */
  const toHex = (n) => '0x' + (n >>> 0).toString(16).toUpperCase().padStart(8, '0');
  function parseSeed(str) {
    if (!str) return null;
    let s = String(str).trim().replace(/^0x/i, '').replace(/[^0-9a-fA-F]/g, '');
    if (!s) return null;
    let n = parseInt(s, 16);
    if (!isFinite(n)) n = parseInt(String(str).trim(), 10);
    if (!isFinite(n)) return null;
    return n >>> 0;
  }
  function randomSeed() {
    const a = new Uint32Array(1);
    crypto.getRandomValues(a);
    return a[0] >>> 0;
  }

  /* ---------------- 렌더 ---------------- */
  function draw() {
    AE.render(ctx, state.ch, { zoom: state.zoom, bg: state.bg });
  }

  function valueOf(ch, key) {
    if (key === 'iris') return ch.parts.iris;
    if (key === 'hairColor') return ch.parts.hairColor;
    if (key === 'outfitColor') return ch.parts.outfitColor;
    return ch.parts[key];
  }

  /* ---------------- 슬롯 UI ---------------- */
  const GROUPS = ['골격', '얼굴', '귀/머리', '의상', '연출'];
  function renderSlots() {
    const host = $('#slotList');
    host.innerHTML = '';
    let curGroup = null;
    const lockCount = Object.keys(state.locks).filter((k) => state.locks[k]).length;
    for (const slot of AE.SLOTS) {
      if (slot.group !== curGroup) {
        curGroup = slot.group;
        const gh = document.createElement('div');
        gh.className = 'grouphead';
        gh.textContent = curGroup;
        host.appendChild(gh);
      }
      const row = document.createElement('div');
      row.className = 'slot' + (state.locks[slot.key] ? ' locked' : '');
      const val = state.ch.parts[slot.key];
      let inner = '';
      if (slot.color) {
        const lab = slot.key === 'iris' ? AE.colorName(state.ch.parts.iris)
          : slot.key === 'hairColor' ? AE.colorName(state.ch.parts.hairColor)
            : AE.colorName(state.ch.parts.outfitColor);
        inner = `<i class="sw" style="background:${val}"></i><span>${lab}</span><em style="font-style:normal;opacity:.45;font-size:10.5px">${val}</em>`;
      } else if (slot.toggle) {
        inner = `<span>${state.ch.parts[slot.key] ? 'ON' : 'OFF'}</span>`;
      } else {
        inner = `<span>${state.ch.labels[slot.key] || val}</span>`;
      }
      row.innerHTML = `
        <div class="lbl">${slot.label}</div>
        <div class="val">${inner}</div>
        <button class="ic lock ${state.locks[slot.key] ? 'on' : ''}" title="잠금">${state.locks[slot.key] ? '🔒' : '🔓'}</button>
        <button class="ic rr" title="이 슬롯만 다시">↻</button>`;
      row.querySelector('.lock').addEventListener('click', () => {
        state.locks[slot.key] = !state.locks[slot.key];
        renderSlots();
      });
      row.querySelector('.rr').addEventListener('click', () => rerollSlot(slot.key));
      host.appendChild(row);
    }
    $('#lockCount').textContent = `${lockCount} / ${AE.SLOTS.length} 잠금`;
  }

  function renderRules() {
    const host = $('#ruleList');
    host.innerHTML = '';
    const ICON = { override: 'OVR', exclude: 'X', budget: 'CAP', contrast: 'ΔL', harmony: 'HUE' };
    for (const r of state.ch.rules) {
      const li = document.createElement('li');
      li.className = 'k-' + r.k;
      li.innerHTML = `<b>${ICON[r.k] || r.k}</b><span>${r.t}</span>`;
      host.appendChild(li);
    }
  }

  function renderMeta() {
    $('#seedText').textContent = toHex(state.seed);
    $('#seedInput').value = toHex(state.seed);
    const w = AE.WORLDS.find((x) => x.id === state.world);
    $('#metaWorld').textContent = w ? w.name : '전체 랜덤';
    $('#metaHeight').textContent = AE.byId('height', state.ch.parts.height).name.replace(' · ', ' · ');
    const bodyLine = `${AE.byId('bust', state.ch.parts.bust).name} · ${AE.byId('hip', state.ch.parts.hip).name} · ${AE.byId('build', state.ch.parts.build).name}`;
    $('#metaHeight').textContent = AE.byId('height', state.ch.parts.height).name + ' · ' + bodyLine;
    document.querySelectorAll('#worldRow button').forEach((b) => {
      b.classList.toggle('on', b.dataset.world === state.world);
    });
    history.replaceState(null, '', `#s=${toHex(state.seed)}&w=${state.world}&z=${state.zoom}`);
  }

  function refresh() {
    draw();
    renderSlots();
    renderRules();
    renderMeta();
  }

  /* ---------------- 액션 ---------------- */
  function regen(seed, opts) {
    opts = opts || {};
    state.seed = seed >>> 0;
    state.ch = AE.generate({ seed: state.seed, world: state.world, locks: state.locks, prev: opts.keepLocks ? state.ch : null });
    if (!opts.silent) refresh();
  }

  function rerollAll() {
    state.seed = randomSeed();
    state.ch = AE.generate({ seed: state.seed, world: state.world, locks: state.locks, prev: state.ch });
    refresh();
  }

  function rerollSlot(key) {
    const allLocks = {};
    for (const s of AE.SLOTS) allLocks[s.key] = true;
    allLocks[key] = false;
    const cur = valueOf(state.ch, key);
    for (let i = 1; i <= 120; i++) {
      const seed = (state.seed + i * 7919 + i * i * 31) >>> 0;
      const ch = AE.generate({ seed, world: state.world, locks: allLocks, prev: state.ch });
      if (String(valueOf(ch, key)) !== String(cur)) {
        state.seed = seed;
        state.ch = ch;
        refresh();
        return;
      }
    }
  }

  /* ---------------- 배치 9종 ---------------- */
  function openBatch() {
    const modal = $('#gridModal');
    modal.classList.add('show');
    fillGrid();
  }
  function fillGrid() {
    const host = $('#grid9');
    host.innerHTML = '';
    const off = document.createElement('canvas');
    off.width = 1024;
    off.height = 1280;
    const octx = off.getContext('2d');
    const chars = AE.batch(randomSeed(), state.world, 9);
    chars.forEach((ch, i) => {
      const card = document.createElement('div');
      card.className = 'card';
      const c2 = document.createElement('canvas');
      c2.width = 420;
      c2.height = 525;
      AE.render(octx, ch, { zoom: 'knee', bg: true });
      c2.getContext('2d').drawImage(off, 0, 0, 420, 525);
      card.appendChild(c2);
      const tag = document.createElement('div');
      tag.className = 'tag';
      tag.innerHTML = `<span>${ch.labels.hairFront} · ${ch.labels.outfit}</span><em>${toHex(ch.seed)}</em>`;
      card.appendChild(tag);
      card.addEventListener('click', () => {
        state.ch = ch;
        state.seed = ch.seed;
        $('#gridModal').classList.remove('show');
        refresh();
        toast('카드를 불러왔습니다 · ' + toHex(ch.seed));
      });
      host.appendChild(card);
    });
    $('#gridInfo').textContent = `${chars.length}종 · 중복 0 (해시 중복 제거)`;
  }

  /* ---------------- 내보내기 ---------------- */
  function downloadPNG() {
    const off = document.createElement('canvas');
    off.width = 1024;
    off.height = 1280;
    const octx = off.getContext('2d');
    AE.render(octx, state.ch, { zoom: state.zoom, bg: state.bg });
    const url = off.toDataURL('image/png');
    const a = document.createElement('a');
    a.href = url;
    a.download = `anime_${toHex(state.seed).replace('0x', '').toLowerCase()}_${state.ch.parts.outfit}.png`;
    document.body.appendChild(a);
    a.click();
    a.remove();
    toast('PNG 저장 · 1024×1280' + (state.bg ? '' : ' (투명 배경)'));
  }

  function showText(title, text) {
    $('#textTitle').textContent = title;
    $('#textBody').textContent = text;
    $('#textModal').classList.add('show');
    $('#textCopy').onclick = () => copy(text);
  }
  function copy(text) {
    if (navigator.clipboard && navigator.clipboard.writeText) {
      navigator.clipboard.writeText(text).then(() => toast('복사했습니다'), () => fallbackCopy(text));
    } else fallbackCopy(text);
  }
  function fallbackCopy(text) {
    const ta = document.createElement('textarea');
    ta.value = text;
    document.body.appendChild(ta);
    ta.select();
    try { document.execCommand('copy'); toast('복사했습니다'); } catch (e) { toast('복사 실패'); }
    ta.remove();
  }
  let toastTimer = null;
  function toast(msg) {
    const t = $('#toast');
    t.textContent = msg;
    t.classList.add('show');
    clearTimeout(toastTimer);
    toastTimer = setTimeout(() => t.classList.remove('show'), 1900);
  }

  /* ---------------- 바인딩 ---------------- */
  function bind() {
    $('#rerollAll').addEventListener('click', rerollAll);
    $('#seedNew').addEventListener('click', rerollAll);
    $('#seedRun').addEventListener('click', () => {
      const n = parseSeed($('#seedInput').value);
      if (n == null) { toast('시드를 확인해 주세요'); return; }
      regen(n);
      toast('시드 재실행 · ' + toHex(n));
    });
    $('#seedInput').addEventListener('keydown', (e) => {
      if (e.key === 'Enter') $('#seedRun').click();
    });
    $('#seedChip').addEventListener('click', () => copy(toHex(state.seed)));
    $('#batchBtn').addEventListener('click', openBatch);
    $('#gridClose').addEventListener('click', () => $('#gridModal').classList.remove('show'));
    $('#gridAgain').addEventListener('click', fillGrid);
    $('#textClose').addEventListener('click', () => $('#textModal').classList.remove('show'));
    $('#pngBtn').addEventListener('click', downloadPNG);
    $('#promptBtn').addEventListener('click', () => showText('AI 프롬프트 (영문 태그)', AE.compilePrompt(state.ch)));
    $('#jsonBtn').addEventListener('click', () => showText('레시피 JSON', JSON.stringify(AE.recipe(state.ch), null, 2)));
    $('#lockClear').addEventListener('click', () => { state.locks = {}; renderSlots(); });

    document.querySelectorAll('#zoomGroup button').forEach((b) => {
      b.addEventListener('click', () => {
        state.zoom = b.dataset.zoom;
        document.querySelectorAll('#zoomGroup button').forEach((x) => x.classList.toggle('on', x === b));
        draw();
        renderMeta();
      });
    });
    $('#bgToggle').addEventListener('click', () => {
      state.bg = !state.bg;
      $('#bgToggle').classList.toggle('on', state.bg);
      $('#bgToggle').innerHTML = `<span>배경</span> ${state.bg ? 'ON' : 'OFF'}`;
      draw();
    });
    document.querySelectorAll('.modal').forEach((m) => {
      m.addEventListener('click', (e) => { if (e.target === m) m.classList.remove('show'); });
    });
    document.addEventListener('keydown', (e) => {
      if (e.key === 'Escape') document.querySelectorAll('.modal.show').forEach((m) => m.classList.remove('show'));
      if (e.code === 'Space' && !/INPUT|TEXTAREA/.test(document.activeElement.tagName)) {
        e.preventDefault();
        rerollAll();
      }
    });
  }

  /* ---------------- 월드 칩 ---------------- */
  function renderWorlds() {
    const host = $('#worldRow');
    host.innerHTML = '';
    for (const w of AE.WORLDS) {
      const b = document.createElement('button');
      b.dataset.world = w.id;
      b.innerHTML = `<i style="background:${w.color}"></i>${w.name}`;
      b.addEventListener('click', () => {
        state.world = w.id;
        state.seed = randomSeed();
        state.ch = AE.generate({ seed: state.seed, world: state.world, locks: state.locks, prev: state.ch });
        refresh();
        toast('세계관 · ' + w.name);
      });
      host.appendChild(b);
    }
  }

  /* ---------------- 시작 ---------------- */
  function init() {
    const h = location.hash || '';
    const params = new URLSearchParams(h.replace(/^#/, ''));
    let seed = parseSeed(params.get('s'));
    const w = params.get('w');
    if (w && AE.WORLDS.some((x) => x.id === w)) state.world = w;
    if (params.get('z') && ['bust', 'knee', 'full'].indexOf(params.get('z')) >= 0) {
      state.zoom = params.get('z');
    }
    if (seed == null) seed = randomSeed();
    state.seed = seed;
    state.ch = AE.generate({ seed, world: state.world, locks: {}, prev: null });
    renderWorlds();
    bind();
    document.querySelectorAll('#zoomGroup button').forEach((x) => x.classList.toggle('on', x.dataset.zoom === state.zoom));
    refresh();
  }

  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', init);
  else init();
})();
