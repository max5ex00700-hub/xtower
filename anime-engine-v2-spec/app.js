/* =============================================================================
 * app.js — UI 상태 · 이벤트 · 내보내기
 * engine.js 호출 → compositor 렌더. 로직을 다시 구현하지 않는다.
 * 제작: 포시즌jo
 * ========================================================================== */

import {
  generate,
  reroll,
  batch,
  buildRenderLayers,
  compilePrompt,
  exportJSON,
  NEGATIVE_PROMPT,
  toggleLock,
  countLocks,
  encodeHash,
  decodeHash,
  parseSeed,
  formatSeed,
  SLOTS,
  slotsByGroup,
  colorName,
} from './engine.js';
import { Compositor } from './compositor.js';

function randomSeed() {
  return (Math.floor(Math.random() * 0xffffffff) >>> 0);
}

const IDB_NAME = 'anime-engine';
const IDB_STORE = 'parts';

const state = {
  seed: 0,
  world: 'all',
  locks: {},
  pins: {},
  zoom: 'knee',
  showBg: true,
  recipe: null,
  customParts: [],
  grid: [],
  manifest: null,
  manifestBase: null,
};

let compositor = null;
let pendingUpload = null;
let openPartsKey = null;

const $ = (sel, root = document) => root.querySelector(sel);
const $$ = (sel, root = document) => [...root.querySelectorAll(sel)];

function toast(msg) {
  const el = $('#toast');
  el.textContent = msg;
  el.hidden = false;
  clearTimeout(toast._t);
  toast._t = setTimeout(() => { el.hidden = true; }, 1800);
}

async function loadManifest() {
  const res = await fetch('./manifest.example.json');
  const raw = await res.json();
  state.manifestBase = raw;
  state.manifest = structuredClone(raw);
}

function syncHash() {
  const hash = encodeHash({
    seed: state.seed,
    world: state.world,
    locks: state.locks,
    pins: state.pins,
    zoom: state.zoom,
  });
  history.replaceState(null, '', hash);
}

function applyZoomClass() {
  const stage = $('#stage');
  stage.classList.remove('is-zoom-bust', 'is-zoom-knee', 'is-zoom-full');
  stage.classList.add(`is-zoom-${state.zoom || 'knee'}`);
  $$('#zoomChips .chip').forEach((c) => {
    c.classList.toggle('is-active', c.dataset.zoom === state.zoom);
  });
}

function updateWorldUI() {
  const worlds = state.manifest.worlds || [];
  const cur = worlds.find((w) => w.id === state.world) || worlds[0];
  const badge = $('#worldBadge');
  badge.textContent = cur ? (cur.short || cur.id.toUpperCase()) : 'ALL';
  if (cur && cur.color) badge.style.color = cur.color;

  $$('#worldChips .chip').forEach((c) => {
    c.classList.toggle('is-active', c.dataset.world === state.world);
  });
  $('#btnBg').textContent = state.showBg ? '배경 ON' : '배경 OFF';
  $('#btnBg').setAttribute('aria-pressed', state.showBg ? 'true' : 'false');
}

/** engine.fileOf 가 blob URL 앞에 basePath 를 붙이므로, 커스텀 파츠 src 를 바로잡는다. */
function remapCustomLayers(layers) {
  for (const layer of layers) {
    if (!layer || !layer.id || !String(layer.id).startsWith('custom_')) continue;
    const custom = state.customParts.find((c) => c.id === layer.id);
    if (!custom) continue;
    const broken = layer.src;
    layer.src = custom.objectURL;
    if (broken && broken !== custom.objectURL) {
      const existing = compositor.imageCache.get(custom.objectURL);
      if (existing && existing !== null) {
        compositor.imageCache.set(broken, existing);
      } else {
        const img = new Image();
        img.onload = () => {
          compositor.imageCache.set(custom.objectURL, img);
          compositor.imageCache.set(broken, img);
          compositor.refresh();
        };
        img.onerror = () => {
          compositor.imageCache.set(custom.objectURL, null);
          compositor.imageCache.set(broken, null);
        };
        compositor.imageCache.set(custom.objectURL, img);
        compositor.imageCache.set(broken, img);
        img.src = custom.objectURL;
      }
    }
  }
  return layers;
}

function regenerate(opts = {}) {
  const recipe = generate({
    manifest: state.manifest,
    seed: state.seed,
    world: state.world,
    locks: state.locks,
    pins: state.pins,
    prev: opts.prev !== undefined ? opts.prev : state.recipe,
    only: opts.only,
  });
  state.recipe = recipe;
  state.seed = recipe.seed;
  $('#seedInput').value = recipe.seedHex;
  renderAll();
  syncHash();
}

function renderAll() {
  const layers = remapCustomLayers(buildRenderLayers(state.recipe, state.manifest));
  compositor.setManifest(state.manifest);
  compositor.render(layers, { transparent: !state.showBg });
  updateWorldUI();
  applyZoomClass();
  renderSlots();
  renderLog();
  renderCustomParts();
  $('#lockCount').textContent = `잠금 ${countLocks(state.locks)} / 30`;
}

function renderWorldChips() {
  const box = $('#worldChips');
  box.innerHTML = '';
  const order = ['all', 'academy', 'fantasy', 'cyber', 'resort', 'gothic'];
  for (const w of state.manifest.worlds || []) {
    const btn = document.createElement('button');
    btn.type = 'button';
    btn.className = 'chip' + (w.id === state.world ? ' is-active' : '');
    btn.dataset.world = w.id;
    btn.textContent = w.name || w.short || w.id;
    const num = order.indexOf(w.id) + 1;
    btn.title = num > 0 ? `세계관: ${w.name} (키 ${num})` : `세계관: ${w.name}`;
    btn.addEventListener('click', () => {
      state.world = w.id;
      regenerate({ prev: null });
    });
    box.appendChild(btn);
  }
}

function slotValueLabel(key) {
  const slot = SLOTS.find((s) => s.key === key);
  const recipe = state.recipe;
  if (!slot || !recipe) return '';
  if (slot.kind === 'color') {
    return colorName(recipe.parts[key]) || recipe.names[key] || recipe.parts[key];
  }
  if (slot.kind === 'flag') return recipe.parts[key] ? 'ON' : 'OFF';
  return recipe.names[key] || String(recipe.parts[key]);
}

function renderSlots() {
  const root = $('#slotsPanel');
  const groups = slotsByGroup();
  const openGroups = new Set(
    $$('.slot-group.is-open', root).map((g) => g.dataset.group)
  );
  if (openGroups.size === 0) openGroups.add('frame');

  root.innerHTML = '';
  for (const g of groups) {
    const section = document.createElement('div');
    section.className = 'slot-group' + (openGroups.has(g.id) ? ' is-open' : '');
    section.dataset.group = g.id;

    const head = document.createElement('button');
    head.type = 'button';
    head.className = 'slot-group-head';
    head.innerHTML = `<span>${g.label} (${g.slots.length})</span><span class="chev">▸</span>`;
    head.addEventListener('click', () => {
      section.classList.toggle('is-open');
    });
    section.appendChild(head);

    const body = document.createElement('div');
    body.className = 'slot-group-body';

    for (const slot of g.slots) {
      const key = slot.key;
      const locked = !!state.locks[key];
      const pinned = state.pins[key] !== undefined;
      const hidden = state.recipe.hidden && state.recipe.hidden[key];
      const row = document.createElement('div');
      row.className = 'slot-row'
        + (locked ? ' is-locked' : '')
        + (hidden ? ' is-hidden' : '')
        + (pinned ? ' is-pinned' : '');
      row.dataset.slot = key;

      const main = document.createElement('div');
      main.className = 'slot-main';
      main.innerHTML = `<div class="slot-label">${slot.label}</div>`;

      const value = document.createElement('div');
      value.className = 'slot-value';
      if (slot.kind === 'color') {
        const sw = document.createElement('span');
        sw.className = 'swatch';
        sw.style.background = state.recipe.parts[key];
        value.appendChild(sw);
      }
      const lab = document.createElement('span');
      lab.textContent = slotValueLabel(key);
      value.appendChild(lab);
      main.appendChild(value);

      if (hidden) {
        const hr = document.createElement('div');
        hr.className = 'hidden-reason';
        hr.textContent = `가려짐: ${hidden}`;
        main.appendChild(hr);
      }

      const actions = document.createElement('div');
      actions.className = 'slot-actions';

      const lockBtn = document.createElement('button');
      lockBtn.type = 'button';
      lockBtn.className = 'btn btn-sm btn-lock' + (locked ? ' is-on' : '');
      lockBtn.title = '잠금 토글';
      lockBtn.textContent = locked ? '🔒' : '🔓';
      lockBtn.addEventListener('click', () => {
        state.locks = toggleLock(state.locks, key);
        syncHash();
        renderSlots();
        $('#lockCount').textContent = `잠금 ${countLocks(state.locks)} / 30`;
      });

      const rerollBtn = document.createElement('button');
      rerollBtn.type = 'button';
      rerollBtn.className = 'btn btn-sm';
      rerollBtn.title = '이 슬롯만 리롤';
      rerollBtn.textContent = '⟳';
      rerollBtn.addEventListener('click', () => {
        if (state.pins[key] !== undefined) {
          const next = { ...state.pins };
          delete next[key];
          state.pins = next;
        }
        const nextRecipe = reroll(state.recipe, {
          manifest: state.manifest,
          locks: state.locks,
          pins: state.pins,
          only: [key],
        });
        state.recipe = nextRecipe;
        state.seed = nextRecipe.seed;
        $('#seedInput').value = nextRecipe.seedHex;
        renderAll();
        syncHash();
      });

      const expandBtn = document.createElement('button');
      expandBtn.type = 'button';
      expandBtn.className = 'btn btn-sm';
      expandBtn.title = '값 목록 열기';
      expandBtn.textContent = openPartsKey === key ? '▾' : '▸';
      expandBtn.addEventListener('click', () => {
        openPartsKey = openPartsKey === key ? null : key;
        renderSlots();
      });

      actions.append(lockBtn, rerollBtn, expandBtn);
      row.append(main, actions);

      const wrap = document.createElement('div');
      wrap.style.gridColumn = '1 / -1';
      const grid = document.createElement('div');
      grid.className = 'parts-grid' + (openPartsKey === key ? ' is-open' : '');
      if (openPartsKey === key) fillPartsGrid(grid, slot);
      wrap.appendChild(grid);
      row.appendChild(wrap);

      body.appendChild(row);
    }

    section.appendChild(body);
    root.appendChild(section);
  }
}

function fillPartsGrid(grid, slot) {
  const key = slot.key;
  if (slot.kind === 'part') {
    const parts = (state.manifest.slots[key] && state.manifest.slots[key].parts) || [];
    for (const p of parts) {
      const btn = document.createElement('button');
      btn.type = 'button';
      btn.className = 'part-chip'
        + (state.recipe.parts[key] === p.id ? ' is-current' : '')
        + (state.pins[key] === p.id ? ' is-pinned' : '');
      btn.textContent = p.name || p.id;
      btn.addEventListener('click', () => {
        state.pins = { ...state.pins, [key]: p.id };
        regenerate({ prev: state.recipe });
      });
      grid.appendChild(btn);
    }
  } else if (slot.kind === 'color') {
    const palette = (state.manifest.palette && state.manifest.palette[key]) || [];
    for (const c of palette) {
      const btn = document.createElement('button');
      btn.type = 'button';
      const cur = String(state.recipe.parts[key] || '').toLowerCase();
      btn.className = 'part-chip'
        + (cur === String(c.color).toLowerCase() ? ' is-current' : '')
        + (state.pins[key] === c.color ? ' is-pinned' : '');
      btn.innerHTML = `<span class="swatch" style="background:${c.color};display:inline-block;vertical-align:middle;margin-right:4px"></span>${c.name || c.id}`;
      btn.addEventListener('click', () => {
        state.pins = { ...state.pins, [key]: c.color };
        regenerate({ prev: state.recipe });
      });
      grid.appendChild(btn);
    }
  } else if (slot.kind === 'flag') {
    for (const v of [true, false]) {
      const btn = document.createElement('button');
      btn.type = 'button';
      btn.className = 'part-chip'
        + (state.recipe.parts[key] === v ? ' is-current' : '')
        + (state.pins[key] === v ? ' is-pinned' : '');
      btn.textContent = v ? 'ON' : 'OFF';
      btn.addEventListener('click', () => {
        state.pins = { ...state.pins, [key]: v };
        regenerate({ prev: state.recipe });
      });
      grid.appendChild(btn);
    }
  }
}

function renderLog() {
  const log = state.recipe.log || [];
  const coerce = log.filter((l) => l.type === 'coerce').length;
  const override = log.filter((l) => l.type === 'override').length;
  const blocked = log.filter((l) => l.type === 'blocked').length;
  $('#logSummary').textContent = `고친 것 ${coerce} · 가린 것 ${override} · 경고 ${blocked}`;

  const c = state.recipe.clutter || { total: 0, max: 6 };
  const gauge = $('#clutterGauge');
  const pct = Math.min(100, (c.total / (c.max || 6)) * 100);
  gauge.innerHTML = `<span style="width:${pct}%"></span>`;
  gauge.title = `혼잡도 ${c.total}/${c.max}` + (c.over ? ` (초과 ${c.over})` : '');

  const list = $('#logList');
  list.innerHTML = '';
  for (const entry of log) {
    const li = document.createElement('li');
    li.dataset.type = entry.type || 'lock';
    li.textContent = `[${entry.id || '-'}] ${entry.text || ''}`;
    list.appendChild(li);
  }
}

function doFullReroll() {
  const next = reroll(state.recipe, {
    manifest: state.manifest,
    locks: state.locks,
    pins: state.pins,
  });
  state.recipe = next;
  state.seed = next.seed;
  $('#seedInput').value = next.seedHex;
  renderAll();
  syncHash();
}

function doNewSeed() {
  state.seed = randomSeed();
  regenerate({ prev: null });
}

function applySeedInput() {
  const raw = $('#seedInput').value.trim();
  if (!raw) return;
  state.seed = parseSeed(raw);
  regenerate({ prev: null });
}

function copySeed() {
  const v = state.recipe ? state.recipe.seedHex : formatSeed(state.seed);
  navigator.clipboard.writeText(v).then(() => toast('시드 복사됨')).catch(() => toast(v));
}

function setZoom(z) {
  state.zoom = z;
  applyZoomClass();
  syncHash();
}

function toggleBg() {
  state.showBg = !state.showBg;
  renderAll();
}

function openBatch() {
  const recipes = batch(state.seed, state.world, 9, state.manifest, {
    locks: state.locks,
    prev: state.recipe,
  });
  state.grid = recipes;
  const grid = $('#batchGrid');
  grid.innerHTML = '';

  recipes.forEach((r, i) => {
    const card = document.createElement('button');
    card.type = 'button';
    card.className = 'batch-card';
    const canvas = document.createElement('canvas');
    canvas.width = 1024;
    canvas.height = 1280;
    const meta = document.createElement('div');
    meta.className = 'meta';
    meta.innerHTML = `<span>${r.seedHex}</span><button type="button" class="btn btn-sm btn-save-one" data-i="${i}">PNG</button>`;
    card.append(canvas, meta);
    card.addEventListener('click', (e) => {
      if (e.target.closest('.btn-save-one')) return;
      loadRecipe(r);
      $('#batchModal').close();
    });
    grid.appendChild(card);

    const comp = new Compositor(canvas, state.manifest);
    const layers = remapCustomLayers(buildRenderLayers(r, state.manifest));
    comp.onReady = () => comp.refresh();
    comp.render(layers, { transparent: !state.showBg });
  });

  grid.querySelectorAll('.btn-save-one').forEach((btn) => {
    btn.addEventListener('click', (e) => {
      e.stopPropagation();
      const r = state.grid[Number(btn.dataset.i)];
      downloadRecipePng(r);
    });
  });

  $('#batchModal').showModal();
}

function loadRecipe(r) {
  state.recipe = r;
  state.seed = r.seed;
  state.world = r.world;
  $('#seedInput').value = r.seedHex;
  renderAll();
  syncHash();
}

function downloadRecipePng(recipe) {
  const layers = remapCustomLayers(buildRenderLayers(recipe, state.manifest));
  const off = document.createElement('canvas');
  const comp = new Compositor(off, state.manifest);
  // share image cache for custom parts
  for (const [k, v] of compositor.imageCache) comp.imageCache.set(k, v);
  const transparent = $('#chkTransparent').checked;
  const url = comp.toDataURL({ layers, transparent, scale: 2 });
  const a = document.createElement('a');
  a.href = url;
  a.download = `anime_${recipe.seedHex}_${recipe.world}.png`;
  a.click();
}

function savePng() {
  downloadRecipePng(state.recipe);
  toast('PNG 저장');
}

function saveJson() {
  const json = exportJSON(state.recipe);
  const blob = new Blob([json], { type: 'application/json' });
  const a = document.createElement('a');
  a.href = URL.createObjectURL(blob);
  a.download = `anime_${state.recipe.seedHex}.json`;
  a.click();
  toast('JSON 저장');
}

function copyPrompt() {
  const prompt = compilePrompt(state.recipe) + '\n\n' + NEGATIVE_PROMPT;
  navigator.clipboard.writeText(prompt).then(() => toast('프롬프트 복사됨')).catch(() => {});
  $('#promptBody').textContent = prompt;
  $('#promptModal').showModal();
}

function copyShareLink() {
  syncHash();
  const url = location.href;
  navigator.clipboard.writeText(url).then(() => toast('링크 복사됨')).catch(() => toast(url));
}

function openIdb() {
  return new Promise((resolve, reject) => {
    const req = indexedDB.open(IDB_NAME, 1);
    req.onupgradeneeded = () => {
      const db = req.result;
      if (!db.objectStoreNames.contains(IDB_STORE)) {
        db.createObjectStore(IDB_STORE, { keyPath: 'id' });
      }
    };
    req.onsuccess = () => resolve(req.result);
    req.onerror = () => reject(req.error);
  });
}

async function idbPut(record) {
  const db = await openIdb();
  return new Promise((resolve, reject) => {
    const tx = db.transaction(IDB_STORE, 'readwrite');
    tx.objectStore(IDB_STORE).put(record);
    tx.oncomplete = () => resolve();
    tx.onerror = () => reject(tx.error);
  });
}

async function idbDelete(id) {
  const db = await openIdb();
  return new Promise((resolve, reject) => {
    const tx = db.transaction(IDB_STORE, 'readwrite');
    tx.objectStore(IDB_STORE).delete(id);
    tx.oncomplete = () => resolve();
    tx.onerror = () => reject(tx.error);
  });
}

async function idbGetAll() {
  const db = await openIdb();
  return new Promise((resolve, reject) => {
    const tx = db.transaction(IDB_STORE, 'readonly');
    const req = tx.objectStore(IDB_STORE).getAll();
    req.onsuccess = () => resolve(req.result || []);
    req.onerror = () => reject(req.error);
  });
}

function resizeToCanvas(bitmapOrImg) {
  const c = document.createElement('canvas');
  c.width = 1024;
  c.height = 1280;
  const ctx = c.getContext('2d');
  const iw = bitmapOrImg.width;
  const ih = bitmapOrImg.height;
  const scale = Math.min(1024 / iw, 1280 / ih);
  const dw = iw * scale;
  const dh = ih * scale;
  const dx = (1024 - dw) / 2;
  const dy = (1280 - dh) / 2;
  ctx.clearRect(0, 0, 1024, 1280);
  ctx.drawImage(bitmapOrImg, dx, dy, dw, dh);
  return new Promise((resolve) => {
    c.toBlob((blob) => resolve(blob), 'image/png');
  });
}

async function handleUploadFile(file) {
  if (!file || !/image\/(png|webp)/i.test(file.type)) {
    toast('PNG 또는 WebP만 지원합니다');
    return;
  }
  const bmp = await createImageBitmap(file);
  const blob = await resizeToCanvas(bmp);
  bmp.close();
  const objectURL = URL.createObjectURL(blob);
  pendingUpload = { blob, objectURL };
  openUploadSlotModal();
}

function openUploadSlotModal() {
  const box = $('#uploadSlots');
  box.innerHTML = '';
  for (const slot of SLOTS.filter((s) => s.kind === 'part')) {
    const btn = document.createElement('button');
    btn.type = 'button';
    btn.textContent = slot.label;
    btn.addEventListener('click', () => {
      $('#uploadModal').close();
      commitCustomPart(slot.key);
    });
    box.appendChild(btn);
  }
  $('#uploadModal').showModal();
}

async function commitCustomPart(slotKey) {
  if (!pendingUpload) return;
  const n = state.customParts.length + 1;
  const id = `custom_${Date.now().toString(36)}_${n}`;
  const name = `내 파츠 ${n}`;
  const { blob, objectURL } = pendingUpload;
  pendingUpload = null;

  const slotCfg = state.manifest.slots[slotKey];
  if (!slotCfg.parts) slotCfg.parts = [];
  slotCfg.parts.push({
    id,
    name,
    file: objectURL,
    tags: [],
    prompt: 'custom uploaded part',
  });

  const record = { id, slot: slotKey, name, blob, createdAt: Date.now() };
  state.customParts.push({ id, slot: slotKey, name, blob, objectURL });
  await idbPut(record);

  state.pins = { ...state.pins, [slotKey]: id };
  regenerate({ prev: state.recipe });
  const label = (SLOTS.find((s) => s.key === slotKey) || {}).label || slotKey;
  toast(`${name} → ${label}`);
}

function renderCustomParts() {
  const box = $('#customParts');
  box.innerHTML = '';
  if (!state.customParts.length) return;
  const title = document.createElement('div');
  title.className = 'panel-title';
  title.style.marginTop = '8px';
  title.textContent = '내 파츠';
  box.appendChild(title);
  for (const c of state.customParts) {
    const row = document.createElement('div');
    row.className = 'custom-item';
    const slotLabel = (SLOTS.find((s) => s.key === c.slot) || {}).label || c.slot;
    row.innerHTML = `<span>${c.name} · ${slotLabel}</span>`;
    const del = document.createElement('button');
    del.type = 'button';
    del.className = 'btn btn-sm';
    del.textContent = '삭제';
    del.addEventListener('click', () => { removeCustomPart(c.id); });
    row.appendChild(del);
    box.appendChild(row);
  }
}

async function removeCustomPart(id) {
  const c = state.customParts.find((x) => x.id === id);
  if (!c) return;
  const parts = state.manifest.slots[c.slot].parts;
  const idx = parts.findIndex((p) => p.id === id);
  if (idx >= 0) parts.splice(idx, 1);
  if (state.pins[c.slot] === id) {
    const next = { ...state.pins };
    delete next[c.slot];
    state.pins = next;
  }
  URL.revokeObjectURL(c.objectURL);
  state.customParts = state.customParts.filter((x) => x.id !== id);
  await idbDelete(id);
  regenerate({ prev: state.recipe });
  toast('파츠 삭제됨');
}

async function restoreCustomParts() {
  try {
    const rows = await idbGetAll();
    for (const row of rows) {
      const objectURL = URL.createObjectURL(row.blob);
      state.customParts.push({
        id: row.id,
        slot: row.slot,
        name: row.name,
        blob: row.blob,
        objectURL,
      });
      const slotCfg = state.manifest.slots[row.slot];
      if (!slotCfg) continue;
      if (!slotCfg.parts) slotCfg.parts = [];
      if (!slotCfg.parts.some((p) => p.id === row.id)) {
        slotCfg.parts.push({
          id: row.id,
          name: row.name,
          file: objectURL,
          tags: [],
          prompt: 'custom uploaded part',
        });
      }
    }
  } catch (err) {
    console.warn('IndexedDB 복원 실패', err);
  }
}

function bindUploader() {
  const zone = $('#uploader');
  const input = $('#fileInput');
  $('#btnPickFile').addEventListener('click', () => input.click());
  input.addEventListener('change', () => {
    const f = input.files && input.files[0];
    if (f) handleUploadFile(f);
    input.value = '';
  });
  zone.addEventListener('dragover', (e) => {
    e.preventDefault();
    zone.classList.add('is-drag');
  });
  zone.addEventListener('dragleave', () => zone.classList.remove('is-drag'));
  zone.addEventListener('drop', (e) => {
    e.preventDefault();
    zone.classList.remove('is-drag');
    const f = e.dataTransfer.files && e.dataTransfer.files[0];
    if (f) handleUploadFile(f);
  });
}

function bindKeys() {
  window.addEventListener('keydown', (e) => {
    const tag = (e.target && e.target.tagName) || '';
    if (tag === 'INPUT' || tag === 'TEXTAREA') return;
    if (e.code === 'Space') {
      e.preventDefault();
      doFullReroll();
    } else if (e.key === 'g' || e.key === 'G') {
      openBatch();
    } else if (e.key === 's' || e.key === 'S') {
      e.preventDefault();
      savePng();
    } else if (e.key === 'l' || e.key === 'L') {
      $('#slotsPanel').focus();
    } else if (e.key >= '1' && e.key <= '6') {
      const worlds = state.manifest.worlds || [];
      const w = worlds[Number(e.key) - 1];
      if (w) {
        state.world = w.id;
        regenerate({ prev: null });
      }
    }
  });
}

function bindUI() {
  $('#btnReroll').addEventListener('click', doFullReroll);
  $('#btnBatch').addEventListener('click', openBatch);
  $('#btnBatchAgain').addEventListener('click', openBatch);
  $('#btnNewSeed').addEventListener('click', doNewSeed);
  $('#btnApplySeed').addEventListener('click', applySeedInput);
  $('#seedInput').addEventListener('keydown', (e) => {
    if (e.key === 'Enter') applySeedInput();
  });
  $('#btnCopySeed').addEventListener('click', copySeed);
  $('#btnBg').addEventListener('click', toggleBg);
  $('#btnPng').addEventListener('click', savePng);
  $('#btnJson').addEventListener('click', saveJson);
  $('#btnPrompt').addEventListener('click', copyPrompt);
  $('#btnShare').addEventListener('click', copyShareLink);
  $('#btnUnlockAll').addEventListener('click', () => {
    state.locks = {};
    syncHash();
    renderSlots();
    $('#lockCount').textContent = '잠금 0 / 30';
    toast('잠금 해제');
  });
  $$('#zoomChips .chip').forEach((c) => {
    c.addEventListener('click', () => setZoom(c.dataset.zoom));
  });
  bindUploader();
  bindKeys();
}

async function boot() {
  await loadManifest();
  await restoreCustomParts();

  const canvas = $('#mainCanvas');
  compositor = new Compositor(canvas, state.manifest);
  compositor.onReady = () => compositor.refresh();

  // 표시 크기는 CSS(width/height:100%)가 담당. 내부 해상도만 1024×1280.

  const decoded = decodeHash(location.hash);
  if (location.hash && location.hash.length > 2) {
    state.seed = decoded.seed;
    state.world = decoded.world || 'all';
    state.locks = decoded.locks || {};
    state.pins = { ...(decoded.pins || {}) };
    for (const [k, v] of Object.entries(state.pins)) {
      const slot = SLOTS.find((s) => s.key === k);
      if (slot && slot.kind === 'flag') {
        state.pins[k] = v === true || v === 'true';
      }
    }
    if (decoded.zoom) state.zoom = decoded.zoom;
  } else {
    state.seed = randomSeed();
  }

  renderWorldChips();
  bindUI();
  regenerate({ prev: null });
}

boot().catch((err) => {
  console.error(err);
  toast('초기화 실패 — 콘솔을 확인하세요');
});
