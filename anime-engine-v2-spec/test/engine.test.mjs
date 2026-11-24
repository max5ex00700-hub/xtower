/* =============================================================================
 * engine.test.mjs — engine.js 검증 (node 18+)
 * 실행: node test/engine.test.mjs   (또는 npm test)
 * 검증 항목: Mulberry32 결정론 · 잠금/부분 리롤 · 충돌 해결 · 렌더 레이어 출력
 * ========================================================================== */

import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import * as E from '../engine.js';

const HERE = path.dirname(fileURLToPath(import.meta.url));
const MANIFEST_PATH = path.join(HERE, '..', 'manifest.example.json');
const manifest = JSON.parse(fs.readFileSync(MANIFEST_PATH, 'utf8'));

let passed = 0;
const failed = [];

function test(name, fn) {
  try {
    fn();
    passed++;
    console.log(`  \x1b[32m✓\x1b[0m ${name}`);
  } catch (err) {
    failed.push({ name, err });
    console.log(`  \x1b[31m✗\x1b[0m ${name}`);
    console.log(`      ${String(err.message).split('\n').join('\n      ')}`);
  }
}

function section(title) {
  console.log(`\n\x1b[1m${title}\x1b[0m`);
}

/* ------------------------------------------------------------------ */
section('1. PRNG — Mulberry32 결정론');

test('mulberry32(1) 첫 5개 값이 알려진 수열과 일치한다', () => {
  const rng = E.mulberry32(1);
  const got = [rng(), rng(), rng(), rng(), rng()];
  const want = [0.6270739405881613, 0.002735721180215478, 0.5274470399599522, 0.9810509674716741, 0.9683778982143849];
  assert.deepEqual(got, want);
});

test('같은 시드 → 완전히 같은 수열, 출력은 [0,1) 범위', () => {
  const a = E.mulberry32(0xdeadbeef);
  const b = E.mulberry32(0xdeadbeef);
  for (let i = 0; i < 1000; i++) {
    const v = a();
    assert.equal(v, b());
    assert.ok(v >= 0 && v < 1);
  }
});

test('슬롯 서브스트림(rngFor)은 같은 시드·같은 슬롯에서 안정적이고 슬롯마다 다르다', () => {
  const s = 0x1a2b3c4d;
  assert.equal(E.rngFor(s, 'hair_color')(), E.rngFor(s, 'hair_color')());
  assert.notEqual(E.rngFor(s, 'hair_color')(), E.rngFor(s, 'hair_front')());
  assert.equal(E.rngFor('내 캐릭터', 'mouth')(), E.rngFor('내 캐릭터', 'mouth')());
});

test('parseSeed / formatSeed 왕복', () => {
  assert.equal(E.parseSeed('0x1A2B3C4D'), 439041101);
  assert.equal(E.parseSeed('439041101'), 439041101);
  assert.equal(E.parseSeed(439041101), 439041101);
  assert.equal(E.formatSeed(439041101), '0x1A2B3C4D');
  assert.equal(E.parseSeed('포시즌jo'), E.hashString('포시즌jo'));
  assert.equal(E.formatSeed(0), '0x00000000');
});

test('nextSeed 는 결정론적이고 매번 다른 값을 낸다', () => {
  const a = E.nextSeed(123);
  assert.equal(a, E.nextSeed(123));
  assert.notEqual(a, 123);
  assert.notEqual(a, E.nextSeed(a));
});

/* ------------------------------------------------------------------ */
section('2. 슬롯·레이어 정의 — 30 슬롯 · 20 레이어');

test('슬롯은 정확히 30개, key 중복 없음', () => {
  assert.equal(E.SLOTS.length, 30);
  assert.equal(new Set(E.SLOT_KEYS).size, 30);
});

test('슬롯 그룹 구성: 골격 8 · 얼굴 9 · 귀머리 7 · 의상연출 6', () => {
  const count = (g) => E.SLOTS.filter((s) => s.group === g).length;
  assert.deepEqual(
    { frame: count('frame'), face: count('face'), hair: count('hair'), style: count('style') },
    { frame: 8, face: 9, hair: 7, style: 6 }
  );
});

test('레이어 스택은 브리프의 Z 순서 20단과 정확히 일치한다', () => {
  assert.deepEqual(E.LAYERS, [
    'background', 'aura_back', 'hair_back', 'body_base', 'ears_back', 'face_base',
    'cheek_fx', 'eyes', 'nose', 'mouth', 'eyebrow', 'outfit', 'ears_front',
    'hair_side', 'hair_front', 'ahoge', 'glasses', 'head_acc', 'aura_front', 'watermark',
  ]);
  for (let i = 1; i < E.LAYERS.length; i++) {
    assert.ok(E.LAYER_Z[E.LAYERS[i]] > E.LAYER_Z[E.LAYERS[i - 1]]);
  }
});

test('옆머리(hair_side)가 귀 앞판(ears_front)보다 위에 그려진다', () => {
  assert.ok(E.LAYER_Z.hair_side > E.LAYER_Z.ears_front);
  assert.equal(E.LAYER_Z.ears_front, 1300);
  assert.equal(E.LAYER_Z.hair_side, 1400);
});

test('모든 슬롯이 선언한 레이어가 스택에 존재한다', () => {
  for (const slot of E.SLOTS) {
    for (const layer of slot.layers) assert.ok(E.LAYER_Z[layer], `${slot.key} → ${layer}`);
  }
});

test('manifest.example.json 은 스키마 검증을 통과한다', () => {
  const v = E.validateManifest(manifest);
  assert.deepEqual(v.errors, []);
  assert.equal(v.ok, true);
  assert.ok(v.warnings.length === 0, `경고: ${v.warnings.slice(0, 3).join(' / ')}`);
});

test('manifest 가 슬롯 30개를 모두 채운다', () => {
  for (const slot of E.SLOTS) assert.ok(manifest.slots[slot.key], `missing ${slot.key}`);
  const parts = Object.values(manifest.slots).reduce((n, s) => n + (s.parts ? s.parts.length : 0), 0);
  assert.ok(parts >= 120, `파츠 ${parts}개`);
});

/* ------------------------------------------------------------------ */
section('3. 생성 — 결정론 · 잠금 · 부분 리롤');

const SEED = '0x1A2B3C4D';
const NEUTRAL_PINS = {
  expression: 'calm', glasses: 'round', head_acc: 'ribbon', aura: 'bokeh',
  ahoge: 'single', iris_fx: 'star', heterochromia: false, hair_two_tone: true, cheek_fx: 'mole',
};

test('같은 시드·세계관 → 같은 레시피 (부품·색·서명 전부 동일)', () => {
  const a = E.generate({ manifest, seed: SEED, world: 'cyber' });
  const b = E.generate({ manifest, seed: SEED, world: 'cyber' });
  assert.deepEqual(a.parts, b.parts);
  assert.deepEqual(a.colors, b.colors);
  assert.equal(a.sig, b.sig);
  assert.equal(a.seedHex, '0x1A2B3C4D');
});

test('다른 시드 → 다른 캐릭터', () => {
  const a = E.generate({ manifest, seed: SEED });
  const b = E.generate({ manifest, seed: '0x1A2B3C4E' });
  assert.notEqual(a.sig, b.sig);
  const diff = Object.keys(a.parts).filter((k) => a.parts[k] !== b.parts[k]).length;
  assert.ok(diff >= 5, `달라진 슬롯 ${diff}개`);
});

test('잠금 슬롯은 새 시드 리롤에서도 유지된다', () => {
  const base = E.generate({ manifest, seed: '0x11111111' });
  const locks = { eyes_shape: true, hair_front: true, outfit: true };
  const next = E.generate({ manifest, seed: '0x99999999', locks, prev: base });
  for (const k of Object.keys(locks)) assert.equal(next.parts[k], base.parts[k], k);
  const changed = Object.keys(base.parts).filter((k) => base.parts[k] !== next.parts[k]).length;
  assert.ok(changed >= 4, `잠그지 않은 슬롯이 바뀌어야 함 (바뀐 수 ${changed})`);
  assert.ok(next.log.some((l) => l.type === 'lock'));
});

test('잠근 색상은 색 조화 단계에서도 그대로다', () => {
  const base = E.generate({ manifest, seed: '0x22222222' });
  const next = E.generate({
    manifest, seed: '0x33333333', prev: base,
    locks: { hair_color: true, outfit_color: true, eye_color: true },
  });
  assert.equal(next.parts.hair_color, base.parts.hair_color);
  assert.equal(next.parts.outfit_color, base.parts.outfit_color);
  assert.equal(next.parts.eye_color, base.parts.eye_color);
  assert.ok(next.log.some((l) => l.id === 'CH01' && l.type === 'harmony'));
});

test('only 옵션: 지정한 슬롯만 리롤된다', () => {
  const base = E.generate({ manifest, seed: '0x44444444' });
  const next = E.generate({ manifest, seed: '0x55555555', prev: base, only: ['background'] });
  const changed = Object.keys(base.parts).filter((k) => base.parts[k] !== next.parts[k]);
  assert.deepEqual(changed, ['background']);
});

test('reroll() 은 같은 시드면 완전히 같은 결과를 낸다', () => {
  const base = E.generate({ manifest, seed: '0x66666666' });
  const a = E.reroll(base, { manifest, seed: '0x77777777' });
  const b = E.reroll(base, { manifest, seed: '0x77777777' });
  assert.equal(a.sig, b.sig);
  assert.equal(a.seed, 0x77777777);
});

test('reroll() 은 시드를 생략하면 새 시드를 파생한다', () => {
  const base = E.generate({ manifest, seed: '0x88888888' });
  const next = E.reroll(base, { manifest });
  assert.equal(next.seed, E.nextSeed(base.seed));
});

test('pins 는 시드와 무관하게 지정값을 고정한다', () => {
  const a = E.generate({ manifest, seed: '0xAAAA0001', pins: { hair_front: 'hime', eye_color: '#c2384a' } });
  const b = E.generate({ manifest, seed: '0xBBBB0002', pins: { hair_front: 'hime', eye_color: '#c2384a' } });
  assert.equal(a.parts.hair_front, 'hime');
  assert.equal(b.parts.hair_front, 'hime');
  assert.equal(a.parts.eye_color, '#c2384a');
  assert.equal(b.parts.eye_color, '#c2384a');
});

test('러시아워 500회 생성 — 예외 없이 30슬롯이 모두 채워진다', () => {
  for (let i = 0; i < 500; i++) {
    const r = E.generate({ manifest, seed: (i * 2654435761) >>> 0, world: ['all', 'academy', 'cyber', 'fantasy', 'resort', 'gothic'][i % 6] });
    for (const key of E.SLOT_KEYS) assert.ok(r.parts[key] !== undefined && r.parts[key] !== null, `${key} @ ${i}`);
    assert.equal(r.log.length >= 0, true);
  }
});

/* ------------------------------------------------------------------ */
section('4. 규칙 — 충돌 해결 · 잠금 우선 · 혼잡도 · 은폐');

test('충돌 해결: 슬림 체형 + 큰 가슴 → 가슴 축소 (CX01)', () => {
  const parts = { ...E.generate({ manifest, seed: 1 }).parts, body_type: 'slim', bust: 'glamour' };
  const log = [];
  E.enforceRules(parts, { manifest, locks: {}, log });
  assert.equal(parts.bust, 'flat');
  assert.ok(log.some((l) => l.id === 'CX01' && l.type === 'coerce'));
});

test('충돌 해결: 수인귀 + 모자 → 악세 제거 (CX10)', () => {
  const parts = { ...E.generate({ manifest, seed: 2 }).parts, ears: 'cat', head_acc: 'crown', ahoge: 'single' };
  const log = [];
  E.enforceRules(parts, { manifest, locks: {}, log });
  assert.equal(parts.head_acc, 'none');
  assert.ok(log.some((l) => l.id === 'CX10'));
});

test('잠금 우선: 잠근 슬롯은 규칙이 바꾸지 못하고 blocked 로그를 남긴다', () => {
  const parts = { ...E.generate({ manifest, seed: 3 }).parts, body_type: 'slim', bust: 'glamour' };
  const log = [];
  E.enforceRules(parts, { manifest, locks: { bust: true }, log });
  assert.equal(parts.bust, 'glamour', '잠긴 가슴은 그대로');
  assert.ok(log.some((l) => l.id === 'CX01' && l.type === 'blocked'));
});

test('규칙은 캐스케이드된다: 슬림 체형 + 넓은 덩치 → slender, 반복 패스에서 수렴', () => {
  const parts = { ...E.generate({ manifest, seed: 4 }).parts, body_type: 'slim', build: 'wide', bust: 'large', hip: 'full' };
  const log = [];
  E.enforceRules(parts, { manifest, locks: {}, log });
  assert.equal(parts.build, 'slender');
  assert.equal(parts.bust, 'flat');
  assert.equal(parts.hip, 'slim');
});

test('혼잡도 예산 ≤ 6: 초과분은 우선순위 낮은 슬롯부터 제거된다', () => {
  const parts = { ...E.generate({ manifest, seed: 5 }).parts, head_acc: 'crown', glasses: 'visor', aura: 'sakura', cheek_fx: 'sweat' };
  const log = [];
  const c = E.enforceBudget(parts, { manifest, locks: {}, log });
  assert.ok(c.total <= 6, `합계 ${c.total}`);
  assert.equal(c.over, 0);
  assert.ok(log.some((l) => l.id === 'CB01' && l.type === 'budget'));
  assert.equal(parts.cheek_fx, 'none', '가장 낮은 우선순위(볼)부터 제거');
});

test('혼잡도: 잠긴 항목은 제거되지 않고 초과 상태를 보고한다', () => {
  const parts = { ...E.generate({ manifest, seed: 6 }).parts, head_acc: 'crown', glasses: 'visor', aura: 'sakura', cheek_fx: 'sweat' };
  const log = [];
  const c = E.enforceBudget(parts, { manifest, locks: { head_acc: true, glasses: true, aura: true, cheek_fx: true }, log });
  assert.ok(c.over > 0);
  assert.ok(log.some((l) => l.type === 'blocked'));
});

test('은폐(override): 표정이 입을 소유하면 mouth 슬롯 레이어가 숨는다 (OV01)', () => {
  const r = E.generate({ manifest, seed: '0x12121212', pins: { expression: 'grin', mouth: 'soft' } });
  assert.ok(r.hidden.mouth, 'mouth 은폐');
  assert.ok(r.log.some((l) => l.id === 'OV01'));
});

test('은폐(override): 파츠 선언(hides) — 바이저는 눈썹, 티아라는 삐침을 가린다 (OV02)', () => {
  const r = E.generate({ manifest, seed: '0x13131313', pins: { glasses: 'visor', head_acc: 'crown', ahoge: 'single' } });
  assert.ok(r.hidden.eyebrow);
  assert.ok(r.hidden.ahoge);
  assert.ok(r.log.filter((l) => l.id === 'OV02').length >= 2);
});

test('세계관 가중치: cyber 세계관에서 cyber 태그 파츠가 더 자주 뽑힌다', () => {
  let tagged = 0;
  let total = 0;
  for (let i = 0; i < 300; i++) {
    const r = E.generate({ manifest, seed: 1000 + i, world: 'cyber' });
    const part = E.partOf(manifest, 'outfit', r.parts.outfit);
    total++;
    if (part.tags.includes('cyber')) tagged++;
  }
  const ratio = tagged / total;
  assert.ok(ratio > 0.4, `cyber 의상 비율 ${(ratio * 100).toFixed(0)}%`);
});

/* ------------------------------------------------------------------ */
section('5. 색 — 60:30:10 · 명도 잠금');

test('200 시드 전부: 피부↔머리 ΔL ≥ 24, 머리↔의상 색상각 ≥ 14, 유효한 hex', () => {
  for (let i = 0; i < 200; i++) {
    const r = E.generate({ manifest, seed: i * 7919 + 13 });
    const dL = Math.abs(E.lightOf(r.colors.skin) - E.lightOf(r.colors.hair));
    assert.ok(dL >= 23.5, `seed ${i}: ΔL ${dL.toFixed(1)}`);
    const gap = E.hueDistance(E.hueOf(r.colors.hair), E.hueOf(r.colors.outfit));
    assert.ok(gap >= 13.5, `seed ${i}: 색상각 ${gap.toFixed(1)}°`);
    for (const v of Object.values(r.colors)) {
      if (typeof v === 'string' && v.startsWith('#')) assert.match(v, /^#[0-9a-f]{6}$/i);
    }
  }
});

test('눈동자(강조색)는 머리·의상과 색상각이 벌어진다', () => {
  for (let i = 0; i < 120; i++) {
    const r = E.generate({ manifest, seed: i * 104729 });
    const gapH = E.hueDistance(E.hueOf(r.colors.iris), E.hueOf(r.colors.hair));
    const gapO = E.hueDistance(E.hueOf(r.colors.iris), E.hueOf(r.colors.outfit));
    assert.ok(Math.max(gapH, gapO) >= 19.5, `seed ${i}: ${gapH.toFixed(1)}/${gapO.toFixed(1)}`);
  }
});

test('어두운 의상은 배경에 묻히지 않게 보정된다 (L ≥ 26)', () => {
  for (let i = 0; i < 200; i++) {
    const r = E.generate({ manifest, seed: i + 777 });
    assert.ok(E.lightOf(r.colors.outfit) >= 25.5, `seed ${i}: L ${E.lightOf(r.colors.outfit).toFixed(1)}`);
  }
});

test('네이비 의상 outfit_accent 는 탁한 보색(탄/머드골드)이 아니다', () => {
  const navy = '#1e2a4a';
  const oH = E.hueOf(navy);
  for (let i = 0; i < 40; i++) {
    const rng = E.mulberry32((i * 7919 + 3) >>> 0);
    const accent = E.deriveOutfitAccent(navy, rng);
    const [ah, as, al] = E.hslOf(accent);
    const towardComp = E.hueDistance(ah, (oH + 165) % 360) < 28;
    const muddy = towardComp && as >= 35 && as <= 72 && al >= 42 && al <= 62;
    assert.equal(muddy, false, `accent ${accent} h${ah.toFixed(0)} s${as.toFixed(0)} l${al.toFixed(0)}`);
    // 크림·로즈·소프트포인트: 명도 높거나 채도가 애니풍
    assert.ok(al >= 40 || as >= 35, `too dull ${accent}`);
  }
});

test('밤색 머리 투톤(hair_inner)은 노란색(+24° 근처)으로 튀지 않는다', () => {
  const brown = '#3b2a22';
  const baseH = E.hueOf(brown);
  for (let i = 0; i < 40; i++) {
    const rng = E.mulberry32((i * 104729 + 7) >>> 0);
    const inner = E.deriveHairInner(brown, rng);
    const [ih, is, il] = E.hslOf(inner);
    // 구 로직 +24° → ~43° 노랑대. 새 로직은 쿨 애시(베이스보다 작거나 같음)
    assert.ok(ih < baseH + 8, `yellow-ish hue ${ih.toFixed(1)} from brown ${baseH.toFixed(1)} → ${inner}`);
    assert.ok(ih > baseH - 25, `too far cool ${ih.toFixed(1)}`);
    assert.ok(il > E.lightOf(brown) + 6, `should lighten for dark hair: L ${il.toFixed(1)}`);
    assert.ok(is < 45, `oversaturated ${is.toFixed(1)}`);
  }
});

/* ------------------------------------------------------------------ */
section('6. buildRenderLayers — Z 정렬 · 틴트 · 트랜스폼');

const neutral = E.generate({ manifest, seed: '0x1A2B3C4D', world: 'cyber', pins: NEUTRAL_PINS });
const layers = E.buildRenderLayers(neutral, manifest);

test('레이어는 z 오름차순으로 정렬된다', () => {
  for (let i = 1; i < layers.length; i++) assert.ok(layers[i].z >= layers[i - 1].z, `index ${i}`);
});

test('20개 레이어가 모두 한 번 이상 등장한다', () => {
  const used = new Set(layers.map((l) => l.layer));
  for (const name of E.LAYERS) assert.ok(used.has(name), `없는 레이어: ${name}`);
  assert.equal(used.size, 20);
});

test('정렬이 곧 그리기 순서: background → … → watermark', () => {
  assert.equal(layers[0].layer, 'background');
  assert.equal(layers[layers.length - 1].layer, 'watermark');
  assert.equal(layers[layers.length - 1].text, '제작: 포시즌jo');
});

test('틴트: 머리 레이어는 hair 색, 몸/얼굴은 skin 색, 의상은 outfit 색', () => {
  const byLayer = (n) => layers.filter((l) => l.layer === n);
  const base = (n) => byLayer(n).find((l) => l.z === E.LAYER_Z[n]);
  for (const n of ['hair_back', 'hair_side', 'hair_front', 'ahoge']) {
    assert.equal(base(n).tint, neutral.colors.hair, n);
  }
  // 투톤 ON 이면 hair_back 안쪽 레이어가 hair_inner 로 한 번 더 올라간다
  const inner = byLayer('hair_back').find((l) => l.z === E.LAYER_Z.hair_back + E.SUB.TWO_TONE);
  assert.ok(inner, '투톤 안쪽 레이어');
  assert.equal(inner.tint, neutral.colors.hair_inner);
  assert.equal(base('body_base').tint, neutral.colors.skin);
  assert.equal(base('face_base').tint, neutral.colors.skin);
  assert.equal(base('eyes').tint, neutral.colors.iris);
  assert.equal(base('outfit').tint, neutral.colors.outfit);
});

test('src 는 pack basePath 로 해석된다', () => {
  const hair = layers.find((l) => l.layer === 'hair_front');
  assert.equal(hair.src, 'assets/parts/hair_front/' + neutral.parts.hair_front + '.webp');
  const bg = layers.find((l) => l.layer === 'background');
  assert.ok(bg.src.startsWith('assets/parts/background/'));
});

test('트랜스폼: 머리 레이어는 head_center, 몸 레이어는 hip 을 피벗으로 쓴다', () => {
  const face = layers.find((l) => l.layer === 'face_base');
  const body = layers.find((l) => l.layer === 'body_base');
  assert.deepEqual(face.transform.pivot, manifest.rig.head_center);
  assert.deepEqual(body.transform.pivot, manifest.rig.hip);
  assert.ok(body.transform.bands && typeof body.transform.bands.bust === 'number');
});

test('자세 파츠의 머리 앵커가 머리 레이어에 반영된다', () => {
  const posed = E.generate({ manifest, seed: '0x1A2B3C4D', pins: { ...NEUTRAL_PINS, pose: 'tilt' } });
  const L = E.buildRenderLayers(posed, manifest);
  const face = L.find((l) => l.layer === 'face_base');
  assert.equal(face.transform.rot, -4);
  assert.equal(face.transform.x, -10);
});

test('키 파츠가 몸 레이어 세로 스케일을 바꾼다', () => {
  const tall = E.generate({ manifest, seed: '0x1A2B3C4D', pins: { ...NEUTRAL_PINS, height: 'tall' } });
  const short = E.generate({ manifest, seed: '0x1A2B3C4D', pins: { ...NEUTRAL_PINS, height: 'petite' } });
  const tallBody = E.buildRenderLayers(tall, manifest).find((l) => l.layer === 'body_base');
  const shortBody = E.buildRenderLayers(short, manifest).find((l) => l.layer === 'body_base');
  assert.ok(tallBody.transform.scaleY > shortBody.transform.scaleY);
});

test('표정 오버레이(홍조·눈물)는 face_base +30 에 깔린다', () => {
  const ov = layers.find((l) => l.layer === 'face_base' && l.z === E.LAYER_Z.face_base + E.SUB.EXPRESSION);
  assert.ok(ov, '표정 오버레이 레이어');
  assert.equal(ov.slot, 'expression');
});

test('은폐된 슬롯 레이어는 visible:false + 이유를 갖고 정렬에는 남는다', () => {
  const r = E.generate({ manifest, seed: '0x1A2B3C4D', pins: { ...NEUTRAL_PINS, expression: 'grin', mouth: 'soft' } });
  const L = E.buildRenderLayers(r, manifest);
  const mouth = L.find((l) => l.layer === 'mouth' && l.z === E.LAYER_Z.mouth);
  assert.equal(mouth.visible, false);
  assert.ok(mouth.hiddenReason && mouth.hiddenReason.length > 0);
  const owned = L.find((l) => l.layer === 'mouth' && l.z === E.LAYER_Z.mouth + E.SUB.OWNED);
  assert.ok(owned, '표정이 그리는 입 레이어');
  assert.equal(owned.visible, true);
});

test('없는 파일은 src:null 로 남아 컴포지터가 자리표시자를 그린다', () => {
  const m2 = JSON.parse(JSON.stringify(manifest));
  delete m2.slots.hair_side.parts[0].file;
  const r = E.generate({ manifest: m2, seed: 9, pins: { hair_side: 'short' } });
  const L = E.buildRenderLayers(r, m2);
  const side = L.find((l) => l.layer === 'hair_side');
  assert.equal(side.src, null);
});

test('레이어 항목 스키마: 필수 필드가 모두 있다', () => {
  const need = ['layer', 'z', 'slot', 'id', 'src', 'tint', 'tintMode', 'opacity', 'visible', 'hiddenReason', 'transform'];
  for (const l of layers) for (const k of need) assert.ok(k in l, `${l.layer} 에 ${k} 없음`);
});

test('manifest 없이 호출하면 명확한 오류', () => {
  assert.throws(() => E.buildRenderLayers(neutral, null), /manifest/);
  assert.throws(() => E.generate({ seed: 1 }), /manifest/);
});

/* ------------------------------------------------------------------ */
section('7. 프롬프트 · JSON 내보내기');

test('compilePrompt: 품질 태그로 시작, 문장부호로 끝, 중복 토큰 없음, 색 이름 포함', () => {
  const p = E.compilePrompt(neutral);
  assert.ok(p.startsWith('1girl, solo'));
  assert.ok(p.endsWith('.'));
  const tokens = p.replace(/\.$/, '').split(',').map((s) => s.trim().toLowerCase());
  assert.equal(new Set(tokens).size, tokens.length, '중복 토큰');
  assert.ok(p.includes(E.colorName(neutral.colors.hair) + ' hair'));
  assert.ok(p.includes(E.colorName(neutral.colors.iris) + ' eyes'));
});

test('compilePrompt: 오드아이/투톤이 켜지면 해당 토큰이 붙는다', () => {
  const r = E.generate({ manifest, seed: 12, pins: { heterochromia: true, hair_two_tone: true } });
  const p = E.compilePrompt(r);
  assert.ok(p.includes('heterochromia'));
  assert.ok(p.includes('two-tone hair'));
});

test('exportJSON: 파싱 가능하고 prompt·parts·seedHex 를 담는다', () => {
  const json = E.exportJSON(neutral);
  const o = JSON.parse(json);
  assert.equal(o.seedHex, '0x1A2B3C4D');
  assert.equal(o.prompt, E.compilePrompt(neutral));
  assert.deepEqual(o.parts, neutral.parts);
  assert.ok(o.labels.hair_front);
  assert.ok(o.negative_prompt.includes('lowres'));
  assert.equal(o.author, '포시즌jo');
});

test('exportJSON 은 원본 레시피를 변형하지 않는다', () => {
  const before = JSON.stringify(neutral.parts);
  E.exportJSON(neutral);
  assert.equal(JSON.stringify(neutral.parts), before);
});

test('내보낸 JSON을 되살려도 manifest로 프롬프트를 다시 만들 수 있다', () => {
  const o = JSON.parse(E.exportJSON(neutral));
  const rebuilt = { ...o };
  delete rebuilt.bits;
  assert.equal(E.compilePrompt(rebuilt, manifest), E.compilePrompt(neutral));
});

/* ------------------------------------------------------------------ */
section('8. 배치 · 잠금 유틸 · URL 해시');

test('9종 배치는 서로 다른 9개를 낸다', () => {
  const grid = E.batch('0x0BADF00D', 'all', 9, manifest);
  assert.equal(grid.length, 9);
  assert.equal(new Set(grid.map((r) => r.sig)).size, 9);
});

test('배치도 결정론적이다 (같은 시드 → 같은 9종)', () => {
  const a = E.batch('0x0BADF00D', 'all', 9, manifest).map((r) => r.sig);
  const b = E.batch('0x0BADF00D', 'all', 9, manifest).map((r) => r.sig);
  assert.deepEqual(a, b);
});

test('toggleLock / countLocks', () => {
  let locks = {};
  locks = E.toggleLock(locks, 'mouth');
  assert.equal(E.countLocks(locks), 1);
  locks = E.toggleLock(locks, 'mouth');
  assert.equal(E.countLocks(locks), 0);
});

test('URL 해시 왕복: 시드·세계관·잠금·고정값', () => {
  const state = { seed: 439041101, world: 'cyber', locks: { mouth: true, hair_front: true }, pins: { hair_front: 'hime' }, zoom: 'knee' };
  const hash = E.encodeHash(state);
  assert.equal(hash, '#v=2&s=0x1A2B3C4D&w=cyber&l=hair_front%2Cmouth&p=hair_front-hime&z=knee');
  const back = E.decodeHash(hash);
  assert.equal(back.seed, 439041101);
  assert.equal(back.world, 'cyber');
  assert.deepEqual(back.locks, { hair_front: true, mouth: true });
  assert.deepEqual(back.pins, { hair_front: 'hime' });
  assert.equal(back.zoom, 'knee');
});

test('해시에서 복원한 상태로 같은 캐릭터가 재현된다', () => {
  const back = E.decodeHash(E.encodeHash({ seed: SEED, world: 'cyber' }));
  const a = E.generate({ manifest, seed: back.seed, world: back.world });
  const plain = E.generate({ manifest, seed: SEED, world: 'cyber' });
  assert.equal(a.sig, plain.sig);
});

/* ------------------------------------------------------------------ */
section('9. 성능 — 1,000회 생성 + 레이어 빌드');

test('1,000회 생성+빌드가 3초 이내', () => {
  const t0 = Date.now();
  for (let i = 0; i < 1000; i++) {
    const r = E.generate({ manifest, seed: i * 2654435761 >>> 0 });
    E.buildRenderLayers(r, manifest);
  }
  const ms = Date.now() - t0;
  assert.ok(ms < 3000, `${ms}ms`);
  console.log(`      (${ms}ms / 1,000회)`);
});

/* ------------------------------------------------------------------ */
section('10. UI·합성 회귀 — 흰자 · z-order · 워터마크');

test('렌더 레이어에서 hair_side.z > ears_front.z', () => {
  const r = E.generate({ manifest, seed: 42, pins: { ears: 'cat', hair_side: 'long' } });
  const L = E.buildRenderLayers(r, manifest);
  const side = L.find((l) => l.layer === 'hair_side');
  const ear = L.find((l) => l.layer === 'ears_front');
  assert.ok(side && ear);
  assert.ok(side.z > ear.z, `hair_side ${side.z} vs ears_front ${ear.z}`);
  const iSide = L.indexOf(side);
  const iEar = L.indexOf(ear);
  assert.ok(iSide > iEar, '그리기 순서: ears_front 다음 hair_side');
});

test('캔버스 watermark 는 유지되고 DOM credit-badge 는 없다', () => {
  const html = fs.readFileSync(path.join(HERE, '..', 'index.html'), 'utf8');
  assert.equal(html.includes('credit-badge'), false);
  assert.ok(html.includes('mainCanvas'));
  const r = E.generate({ manifest, seed: 1 });
  const L = E.buildRenderLayers(r, manifest);
  const wm = L.find((l) => l.layer === 'watermark');
  assert.ok(wm);
  assert.equal(wm.kind, 'text');
  assert.equal(wm.text, '제작: 포시즌jo');
});

test('protectScleraPixels: 고명도 흰자는 틴트 복원, 홍채는 유지', async () => {
  const { protectScleraPixels } = await import('../compositor.js');
  // 2픽셀: 흰자(248) + 홍채(90), 틴트는 파란 multiply 결과물로 가정
  const src = new Uint8ClampedArray([
    248, 248, 248, 255,
    90, 90, 90, 255,
  ]);
  const dest = new Uint8ClampedArray([
    80, 140, 200, 255,   // 흰자가 파랗게 물든 상태
    40, 70, 120, 255,    // 홍채 틴트
  ]);
  protectScleraPixels(dest, src, { threshold: 225, fullAt: 245 });
  // 흰자: L≈248 → full restore toward source
  assert.ok(dest[0] >= 230 && dest[1] >= 230 && dest[2] >= 230, `sclera ${[dest[0], dest[1], dest[2]]}`);
  // 홍채: L=90 < 225 → unchanged
  assert.deepEqual([dest[4], dest[5], dest[6]], [40, 70, 120]);
});

test('protectScleraPixels: L=230 은 부분 복원', async () => {
  const { protectScleraPixels } = await import('../compositor.js');
  const src = new Uint8ClampedArray([230, 230, 230, 255]);
  const dest = new Uint8ClampedArray([50, 100, 180, 255]);
  protectScleraPixels(dest, src, { threshold: 225, fullAt: 245 });
  // t = (230-225)/(245-225) = 0.25
  assert.equal(dest[0], Math.round(50 + (230 - 50) * 0.25));
  assert.equal(dest[1], Math.round(100 + (230 - 100) * 0.25));
  assert.equal(dest[2], Math.round(180 + (230 - 180) * 0.25));
  assert.ok(dest[0] > 50 && dest[0] < 230);
});

/* ------------------------------------------------------------------ */
console.log('\n' + '─'.repeat(58));
if (failed.length) {
  console.log(`\x1b[31m실패 ${failed.length} · 통과 ${passed}\x1b[0m`);
  for (const f of failed) console.log(`  ✗ ${f.name}\n    ${f.err.stack.split('\n')[0]}`);
  process.exit(1);
} else {
  console.log(`\x1b[32m전체 통과: ${passed}개 테스트\x1b[0m`);
  process.exit(0);
}
