/* =============================================================================
 * headless-checks.mjs — 파츠 테스트 회귀 4건을 수치로 확인 (Node, 브라우저 불필요)
 * 실행: node test/headless-checks.mjs
 * ========================================================================== */

import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import * as E from '../engine.js';
import { protectScleraPixels } from '../compositor.js';

const HERE = path.dirname(fileURLToPath(import.meta.url));
const ROOT = path.join(HERE, '..');
const manifest = JSON.parse(fs.readFileSync(path.join(ROOT, 'manifest.example.json'), 'utf8'));

const results = [];

function check(name, fn) {
  try {
    const detail = fn() || 'ok';
    results.push({ name, ok: true, detail });
    console.log(`  ✓ ${name}`);
    console.log(`      ${detail}`);
  } catch (err) {
    results.push({ name, ok: false, detail: String(err.message) });
    console.log(`  ✗ ${name}`);
    console.log(`      ${err.message}`);
  }
}

console.log('\n헤드리스 회귀 체크 (흰자 · 배색 · z-order · 워터마크)\n');

check('1. 흰자(sclera) 순백 유지 — L>225 복원', () => {
  const W = 4;
  const src = new Uint8ClampedArray(W * 4);
  const dest = new Uint8ClampedArray(W * 4);
  // pixel0 sclera 248, pixel1 mid 200, pixel2 iris 80, pixel3 highlight 255
  const levels = [248, 200, 80, 255];
  for (let i = 0; i < W; i++) {
    const L = levels[i];
    src[i * 4] = L; src[i * 4 + 1] = L; src[i * 4 + 2] = L; src[i * 4 + 3] = 255;
    dest[i * 4] = 60; dest[i * 4 + 1] = 120; dest[i * 4 + 2] = 200; dest[i * 4 + 3] = 255;
  }
  protectScleraPixels(dest, src, { threshold: 225, fullAt: 245 });
  const scleraL = 0.2126 * dest[0] + 0.7152 * dest[1] + 0.0722 * dest[2];
  const irisL = 0.2126 * dest[8] + 0.7152 * dest[9] + 0.0722 * dest[10];
  assert.ok(scleraL >= 240, `sclera luma ${scleraL.toFixed(1)}`);
  assert.deepEqual([dest[8], dest[9], dest[10]], [60, 120, 200], 'iris untouched');
  assert.ok(dest[4] === 60, 'L=200 below threshold stays tinted');
  const hiL = 0.2126 * dest[12] + 0.7152 * dest[13] + 0.0722 * dest[14];
  assert.ok(hiL >= 250, `pure white restore ${hiL.toFixed(1)}`);
  return `scleraL=${scleraL.toFixed(1)} irisRGB=(60,120,200) whiteL=${hiL.toFixed(1)}`;
});

check('2a. outfit_accent — 네이비 → 크림/로즈 (머드 보색 아님)', () => {
  const navy = '#1e2a4a';
  const samples = [];
  for (let i = 0; i < 24; i++) {
    const a = E.deriveOutfitAccent(navy, E.mulberry32((i + 1) * 9973));
    const [h, s, l] = E.hslOf(a);
    const muddy = E.hueDistance(h, (E.hueOf(navy) + 165) % 360) < 28
      && s >= 35 && s <= 72 && l >= 42 && l <= 62;
    assert.equal(muddy, false, a);
    samples.push(`${a}(h${h.toFixed(0)}s${s.toFixed(0)}l${l.toFixed(0)})`);
  }
  return samples.slice(0, 4).join(', ') + ` … (${samples.length} samples)`;
});

check('2b. hair_two_tone — 밤색 → 쿨 애시 (노랑 아님)', () => {
  const brown = '#3b2a22';
  const baseH = E.hueOf(brown);
  const samples = [];
  for (let i = 0; i < 24; i++) {
    const inner = E.deriveHairInner(brown, E.mulberry32((i + 1) * 7919));
    const [h, s, l] = E.hslOf(inner);
    assert.ok(h < baseH + 8, `${inner} hue ${h}`);
    assert.ok(h > 0 || h === 0 || h < 360);
    assert.ok(l > E.lightOf(brown) + 6);
    samples.push(`${inner}(h${h.toFixed(0)}s${s.toFixed(0)}l${l.toFixed(0)})`);
  }
  return samples.slice(0, 4).join(', ') + ` … baseH=${baseH.toFixed(1)}`;
});

check('3. z-order — ears_front(1300) < hair_side(1400)', () => {
  assert.equal(E.LAYER_Z.ears_front, 1300);
  assert.equal(E.LAYER_Z.hair_side, 1400);
  const r = E.generate({ manifest, seed: 99, pins: { ears: 'cat', hair_side: 'long' } });
  const L = E.buildRenderLayers(r, manifest);
  const ear = L.find((l) => l.layer === 'ears_front');
  const side = L.find((l) => l.layer === 'hair_side');
  assert.ok(side.z > ear.z);
  assert.ok(L.indexOf(side) > L.indexOf(ear));
  return `ears_front.z=${ear.z} hair_side.z=${side.z} drawOrder ok`;
});

check('4. 워터마크 중복 제거 — DOM 배지 없음, 캔버스 text만', () => {
  const html = fs.readFileSync(path.join(ROOT, 'index.html'), 'utf8');
  const css = fs.readFileSync(path.join(ROOT, 'app.css'), 'utf8');
  assert.equal(/credit-badge/.test(html), false);
  assert.equal(/\.credit-badge\s*\{/.test(css), false);
  const L = E.buildRenderLayers(E.generate({ manifest, seed: 1 }), manifest);
  const wms = L.filter((l) => l.layer === 'watermark');
  assert.equal(wms.length, 1);
  assert.equal(wms[0].text, E.WATERMARK);
  return `DOM badge=0 canvas watermark=1 text="${wms[0].text}"`;
});

const failed = results.filter((r) => !r.ok);
console.log('\n' + '─'.repeat(58));
if (failed.length) {
  console.log(`실패 ${failed.length} / ${results.length}`);
  process.exit(1);
}
console.log(`전체 통과: ${results.length}개 헤드리스 체크`);
process.exit(0);
