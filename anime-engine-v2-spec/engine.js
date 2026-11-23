/* =============================================================================
 * Anime Character Combinator Engine v2 — engine.js
 * 파츠팩(Parts Pack) 아키텍처용 순수 로직 모듈
 * -----------------------------------------------------------------------------
 * - DOM · Canvas · window 에 전혀 의존하지 않는다. (Node 18+ / 브라우저 ES module 공용)
 * - 이 파일이 아는 것: 30개 슬롯 · 20단 레이어 스택 · 시드 PRNG · 규칙(충돌/은폐/혼잡도)
 *   · 60:30:10 색 조화 · 프롬프트 컴파일 · 레시피(JSON) · URL 해시
 * - 이 파일이 모르는 것: 실제 그림 파일 · 캔버스 합성 · UI (compositor.js / app 담당)
 *
 * 제작: 포시즌jo
 * ========================================================================== */

export const VERSION = '2.0.0';
export const SCHEMA = 'anime-parts-pack/2';
export const WATERMARK = '제작: 포시즌jo';

/* =============================================================================
 * 0. 레이어 스택 — Z 순서 20단 (아래 → 위)
 * ========================================================================== */

/** 뒤에서 앞으로 그리는 순서. 이 배열의 index 가 곧 z 순서다. */
export const LAYERS = [
  'background',   // 0  배경 이미지
  'aura_back',    // 1  오라(캐릭터 뒤)
  'hair_back',    // 2  뒤머리
  'body_base',    // 3  몸(피부) 실루엣 + 자세 팔
  'ears_back',    // 4  수인귀 뒤판 · 엘프귀 뒤쪽
  'face_base',    // 5  얼굴 윤곽(피부 + 라인)
  'cheek_fx',     // 6  홍조 · 볼 이펙트
  'eyes',         // 7  흰자 + 홍채 + 무늬 (오드아이 포함)
  'nose',         // 8  코
  'mouth',        // 9  입
  'eyebrow',      // 10 눈썹
  'outfit',       // 11 의상
  'hair_side',    // 12 옆머리
  'ears_front',   // 13 수인귀 앞판
  'hair_front',   // 14 앞머리
  'ahoge',        // 15 삐침
  'glasses',      // 16 안경 · 바이저
  'head_acc',     // 17 머리 악세서리
  'aura_front',   // 18 오라(캐릭터 앞)
  'watermark',    // 19 제작 표기
];

/** 레이어 이름 → 기본 z (100 단위). 같은 레이어 안의 세부 순서는 sub(0~99)로 준다. */
export const LAYER_Z = Object.freeze(
  LAYERS.reduce((acc, name, i) => {
    acc[name] = (i + 1) * 100;
    return acc;
  }, {})
);

/** 같은 레이어 안에서 쓰는 sub 값 (문서·테스트 공용) */
export const SUB = Object.freeze({
  BASE: 0,
  ODD_EYE: 5,
  TWO_TONE: 5,
  ACCENT: 5,
  FX: 10,
  OWNED: 20, // 상위 파츠가 소유한 하위 레이어(예: 표정이 그리는 입)
  EXPRESSION: 30, // 표정 오버레이(홍조·눈물 등) — 얼굴 위, 눈 아래
});

/* =============================================================================
 * 1. 슬롯 정의 — 30개 독립 슬롯 (전부 잠금/개별 리롤 가능)
 * ========================================================================== */

export const GROUPS = Object.freeze([
  { id: 'frame', label: '골격·체형' },
  { id: 'face', label: '얼굴' },
  { id: 'hair', label: '귀·머리' },
  { id: 'style', label: '의상·연출' },
]);

/**
 * kind
 *  - 'part'  : manifest.slots[key].parts 에서 하나 고른다 (id 문자열)
 *  - 'color' : manifest.palette[key] 에서 색을 고른다 (hex 문자열)
 *  - 'flag'  : 확률로 ON/OFF (boolean)
 * layers : 이 슬롯이 그리는 레이어 이름들(문서·검증용)
 */
export const SLOTS = Object.freeze([
  /* ---- 골격·체형 (8) ---- */
  { key: 'face_shape',   label: '얼굴 윤곽',   group: 'frame', kind: 'part',  layers: ['face_base'] },
  { key: 'skin_tone',    label: '피부 톤',     group: 'frame', kind: 'color', layers: ['body_base', 'face_base', 'ears_back', 'ears_front'] },
  { key: 'body_type',    label: '체형 프리셋', group: 'frame', kind: 'part',  layers: ['body_base'] },
  { key: 'height',       label: '키·등신',     group: 'frame', kind: 'part',  layers: [] },
  { key: 'bust',         label: '가슴',        group: 'frame', kind: 'part',  layers: [] },
  { key: 'hip',          label: '골반',        group: 'frame', kind: 'part',  layers: [] },
  { key: 'build',        label: '덩치',        group: 'frame', kind: 'part',  layers: [] },
  { key: 'pose',         label: '자세',        group: 'frame', kind: 'part',  layers: ['body_base'] },

  /* ---- 얼굴 (9) ---- */
  { key: 'eyes_shape',   label: '눈 모양',     group: 'face', kind: 'part',  layers: ['eyes'] },
  { key: 'eye_color',    label: '눈 색',       group: 'face', kind: 'color', layers: ['eyes'] },
  { key: 'iris_fx',      label: '홍채 무늬',   group: 'face', kind: 'part',  layers: ['eyes'] },
  { key: 'eyebrow',      label: '눈썹',        group: 'face', kind: 'part',  layers: ['eyebrow'] },
  { key: 'expression',   label: '표정',        group: 'face', kind: 'part',  layers: ['mouth', 'eyebrow', 'cheek_fx'] },
  { key: 'nose',         label: '코',          group: 'face', kind: 'part',  layers: ['nose'] },
  { key: 'mouth',        label: '입',          group: 'face', kind: 'part',  layers: ['mouth'] },
  { key: 'cheek_fx',     label: '볼 이펙트',   group: 'face', kind: 'part',  layers: ['cheek_fx'] },
  { key: 'heterochromia', label: '오드아이',   group: 'face', kind: 'flag',  layers: ['eyes'] },

  /* ---- 귀·머리 (7) ---- */
  { key: 'ears',         label: '귀',          group: 'hair', kind: 'part',  layers: ['ears_back', 'ears_front'] },
  { key: 'hair_back',    label: '뒤머리',      group: 'hair', kind: 'part',  layers: ['hair_back'] },
  { key: 'hair_side',    label: '옆머리',      group: 'hair', kind: 'part',  layers: ['hair_side'] },
  { key: 'hair_front',   label: '앞머리',      group: 'hair', kind: 'part',  layers: ['hair_front'] },
  { key: 'ahoge',        label: '삐침',        group: 'hair', kind: 'part',  layers: ['ahoge'] },
  { key: 'hair_color',   label: '머리색',      group: 'hair', kind: 'color', layers: ['hair_back', 'hair_side', 'hair_front', 'ahoge'] },
  { key: 'hair_two_tone', label: '투톤 헤어',  group: 'hair', kind: 'flag',  layers: ['hair_back'] },

  /* ---- 의상·연출 (6) ---- */
  { key: 'outfit',       label: '의상',        group: 'style', kind: 'part',  layers: ['outfit'] },
  { key: 'outfit_color', label: '의상색',      group: 'style', kind: 'color', layers: ['outfit'] },
  { key: 'head_acc',     label: '머리 악세',   group: 'style', kind: 'part',  layers: ['head_acc'] },
  { key: 'glasses',      label: '안경',        group: 'style', kind: 'part',  layers: ['glasses'] },
  { key: 'aura',         label: '오라',        group: 'style', kind: 'part',  layers: ['aura_back', 'aura_front'] },
  { key: 'background',   label: '배경',        group: 'style', kind: 'part',  layers: ['background'] },
]);

export const SLOT_KEYS = Object.freeze(SLOTS.map((s) => s.key));
export const SLOT_MAP = Object.freeze(SLOTS.reduce((acc, s) => { acc[s.key] = s; return acc; }, {}));

/** UI 그룹 순서대로 슬롯 묶음 */
export function slotsByGroup() {
  return GROUPS.map((g) => ({ ...g, slots: SLOTS.filter((s) => s.group === g.id) }));
}

/* 생성 기본값 — manifest.fx 로 덮어쓸 수 있다 */
export const DEFAULTS = Object.freeze({
  clutter_budget: 6,     // 혼잡도 상한 (악세·이펙트 점수 합)
  luminance_lock: 24,    // 피부 ↔ 머리 명도차 하한
  hue_gap_min: 20,       // 주조색 ↔ 보조색 색상각 하한
  hue_collision: 14,     // 이보다 가까우면 '동색'으로 보고 밀어낸다
  odd_eye_chance: 0.17,
  two_tone_chance: 0.30,
  tag_boost: 2.8,        // 세계관 태그가 맞는 파츠의 가중치 배수
});

/* =============================================================================
 * 2. PRNG — Mulberry32 + 문자열 해시
 * ========================================================================== */

/** Mulberry32: 32bit 시드 하나로 결정론적 실수열 [0,1) 을 만든다. */
export function mulberry32(a) {
  let t = a >>> 0;
  return function next() {
    t = (t + 0x6d2b79f5) >>> 0;
    let x = t;
    x = Math.imul(x ^ (x >>> 15), 1 | x);
    x = (x + Math.imul(x ^ (x >>> 7), 61 | x)) ^ x;
    return ((x ^ (x >>> 14)) >>> 0) / 4294967296;
  };
}

/** FNV-1a 32bit. 슬롯별 서브스트림과 문자열 시드에 쓴다. */
export function hashString(str) {
  let h = 2166136261 >>> 0;
  const s = String(str);
  for (let i = 0; i < s.length; i++) {
    h ^= s.charCodeAt(i);
    h = Math.imul(h, 16777619);
  }
  return h >>> 0;
}

/**
 * 슬롯별로 독립된 난수 스트림.
 * 같은 시드에서 한 슬롯을 리롤해도 다른 슬롯의 결과가 밀리지 않는다. (v2의 핵심)
 */
export function rngFor(seed, salt) {
  return mulberry32((parseSeed(seed) ^ hashString(String(salt))) >>> 0);
}

/** '0x1A2B3C4D' | '12345' | '내 캐릭터' | number → uint32. 문자열은 해시된다. */
export function parseSeed(value) {
  if (value === null || value === undefined || value === '') return 0;
  if (typeof value === 'number' && Number.isFinite(value)) return Math.trunc(value) >>> 0;
  const s = String(value).trim();
  if (/^0x[0-9a-f]+$/i.test(s)) return parseInt(s.slice(2), 16) >>> 0;
  if (/^\d+$/.test(s)) return parseInt(s, 10) >>> 0;
  return hashString(s);
}

/** uint32 → '0x1A2B3C4D' (8자리 고정) */
export function formatSeed(seed) {
  return '0x' + (parseSeed(seed) >>> 0).toString(16).toUpperCase().padStart(8, '0');
}

/** 다음 시드(리롤 버튼) — 현재 시드에서 결정론적으로 파생한다. */
export function nextSeed(seed) {
  const rng = mulberry32((parseSeed(seed) ^ 0x9e3779b9) >>> 0);
  return Math.floor(rng() * 4294967296) >>> 0;
}

/* =============================================================================
 * 3. 색 유틸 — 60:30:10 · ΔL 잠금에 쓰는 최소 집합
 * ========================================================================== */

const clamp = (v, a, b) => (v < a ? a : v > b ? b : v);

export function hexToRgb(hex) {
  let h = String(hex || '#000').replace('#', '').trim();
  if (h.length === 3) h = h.split('').map((c) => c + c).join('');
  if (!/^[0-9a-f]{6}$/i.test(h)) h = '000000';
  return [parseInt(h.slice(0, 2), 16), parseInt(h.slice(2, 4), 16), parseInt(h.slice(4, 6), 16)];
}

export function rgbToHex(r, g, b) {
  const f = (v) => clamp(Math.round(v), 0, 255).toString(16).padStart(2, '0');
  return '#' + f(r) + f(g) + f(b);
}

export function rgbToHsl(r, g, b) {
  r /= 255; g /= 255; b /= 255;
  const mx = Math.max(r, g, b);
  const mn = Math.min(r, g, b);
  const l = (mx + mn) / 2;
  const d = mx - mn;
  let h = 0;
  let s = 0;
  if (d) {
    s = l > 0.5 ? d / (2 - mx - mn) : d / (mx + mn);
    if (mx === r) h = ((g - b) / d + (g < b ? 6 : 0)) / 6;
    else if (mx === g) h = ((b - r) / d + 2) / 6;
    else h = ((r - g) / d + 4) / 6;
  }
  return [h * 360, s * 100, l * 100];
}

export function hslToHex(h, s, l) {
  h = (((h % 360) + 360) % 360);
  s = clamp(s, 0, 100) / 100;
  l = clamp(l, 0, 100) / 100;
  const c = (1 - Math.abs(2 * l - 1)) * s;
  const x = c * (1 - Math.abs(((h / 60) % 2) - 1));
  const m = l - c / 2;
  let r = 0; let g = 0; let b = 0;
  if (h < 60) [r, g, b] = [c, x, 0];
  else if (h < 120) [r, g, b] = [x, c, 0];
  else if (h < 180) [r, g, b] = [0, c, x];
  else if (h < 240) [r, g, b] = [0, x, c];
  else if (h < 300) [r, g, b] = [x, 0, c];
  else [r, g, b] = [c, 0, x];
  return rgbToHex((r + m) * 255, (g + m) * 255, (b + m) * 255);
}

/** hex → [h, s, l] */
export function hslOf(hex) { return rgbToHsl.apply(null, hexToRgb(hex)); }
export const hueOf = (hex) => hslOf(hex)[0];
export const satOf = (hex) => hslOf(hex)[1];
export const lightOf = (hex) => hslOf(hex)[2];

/** 색상각 두 개의 최단 거리 (0~180) */
export function hueDistance(a, b) {
  const d = Math.abs((((a - b) % 360) + 360) % 360);
  return d > 180 ? 360 - d : d;
}

/** 색상각 두 개의 부호 있는 차이 (-180 ~ +180) */
export function signedHueDelta(a, b) {
  return ((a - b + 540) % 360) - 180;
}

/** 명도·채도·색상 시프트 */
export function shade(hex, dL = 0, dS = 0, dH = 0) {
  const [h, s, l] = hslOf(hex);
  return hslToHex(h + dH, s + dS, l + dL);
}

export function mixHex(a, b, t) {
  const A = hexToRgb(a); const B = hexToRgb(b);
  return rgbToHex(A[0] + (B[0] - A[0]) * t, A[1] + (B[1] - A[1]) * t, A[2] + (B[2] - A[2]) * t);
}

export function rgba(hex, alpha) {
  const [r, g, b] = hexToRgb(hex);
  return `rgba(${r},${g},${b},${alpha})`;
}

/** 프롬프트·라벨에 쓰는 영어 색 이름 */
export function colorName(hex) {
  const [h, s, l] = hslOf(hex);
  if (l > 88 && s < 18) return 'white';
  if (l < 12) return 'jet black';
  if (s < 12) return l < 35 ? 'charcoal' : l < 62 ? 'grey' : 'silver';
  const n =
    h < 12 ? 'crimson' : h < 25 ? 'vermilion' : h < 42 ? 'amber' : h < 58 ? 'golden yellow'
      : h < 75 ? 'lime' : h < 105 ? 'emerald' : h < 135 ? 'jade' : h < 165 ? 'mint'
        : h < 195 ? 'turquoise' : h < 225 ? 'azure' : h < 250 ? 'sapphire'
          : h < 275 ? 'indigo' : h < 300 ? 'violet' : h < 330 ? 'magenta' : 'rose';
  const pre = l > 74 ? 'pale ' : l > 56 ? '' : l > 30 ? 'deep ' : 'dark ';
  return pre + n;
}

/* =============================================================================
 * 4. manifest 접근자
 * ========================================================================== */

export function partsOf(manifest, slotKey) {
  const s = manifest && manifest.slots && manifest.slots[slotKey];
  return (s && Array.isArray(s.parts)) ? s.parts : [];
}

export function partOf(manifest, slotKey, id) {
  const list = partsOf(manifest, slotKey);
  if (!list.length) return null;
  return list.find((p) => p.id === id)
    || list.find((p) => p.id === 'none')
    || list[0];
}

export function paletteOf(manifest, slotKey) {
  const p = manifest && manifest.palette;
  return (p && Array.isArray(p[slotKey])) ? p[slotKey] : [];
}

/** 파츠 → 실제 파일 경로 (pack 루트 prefix 적용). 없으면 null → 컴포지터가 자리표시자를 그린다. */
export function fileOf(manifest, part, layer) {
  if (!part) return null;
  let rel = null;
  if (part.files && typeof part.files[layer] === 'string') rel = part.files[layer];
  else if (part.file && !part.files) rel = part.file;
  else if (part.file && (part.layer === layer || part.layer == null)) rel = part.file;
  else if (part.files && typeof part.files.default === 'string' && !part.files[layer]) {
    // 명시되지 않은 레이어는 그리지 않는다 (예: 오라 앞판이 없는 파츠)
    rel = null;
  }
  if (!rel) return null;
  if (/^https?:\/\//.test(rel) || rel.startsWith('/')) return rel;
  const base = manifest.basePath == null ? '' : manifest.basePath;
  return base + rel;
}

/** 슬롯의 clutter 점수 (혼잡도 예산 계산용) */
export function clutterOf(manifest, slotKey, id) {
  if (id === 'none' || id === false) return 0;
  const part = partOf(manifest, slotKey, id);
  return part && typeof part.clutter === 'number' ? part.clutter : 0;
}

export function weightOf(part, world, tagBoost = DEFAULTS.tag_boost) {
  const base = typeof part.weight === 'number' ? part.weight : 1;
  const tagged = world && world !== 'all' && Array.isArray(part.tags) && part.tags.includes(world);
  return Math.max(0, base) * (tagged ? tagBoost : 1);
}

/* =============================================================================
 * 5. 규칙 매트릭스 (충돌 · 오버라이드 · 혼잡도 · 색)
 * ========================================================================== */

const BIG_ACC = ['headphones', 'beret', 'crown'];
const KEMONOMIMI = ['cat', 'fox', 'rabbit'];
const HEAD_COVER = ['headphones', 'beret', 'crown', 'headband'];

const nameOfPart = (manifest, slotKey, id) => {
  const p = partOf(manifest, slotKey, id);
  return p ? p.name || p.id : String(id);
};

/**
 * 충돌/정합 규칙. 위에서부터 순서대로, 최대 4패스 반복(규칙이 만든 값이
 * 다른 규칙의 조건을 새로 만족할 수 있으므로). 모든 결과는 결정론적이다.
 *
 * type
 *  - 'coerce'   : 값 교정. 해당 슬롯이 잠겨 있으면 건너뛰고 blocked 로그.
 *  - 'override' : 파츠가 선언한 은폐(part.hides / part.owns) 및 상위 슬롯 우선. 잠금과 무관.
 */
export const RULES = [
  /* ---------- 체형 정합 ---------- */
  {
    id: 'CX01', type: 'coerce', label: '슬림 체형 ↔ 큰 가슴',
    when: (p) => p.body_type === 'slim' && (p.bust === 'large' || p.bust === 'glamour'),
    fix: (p) => ({ bust: 'flat' }),
    why: (p, m) => `체형 정합: ${nameOfPart(m, 'body_type', p.body_type)} ↔ ${nameOfPart(m, 'bust', p.bust)} → 가슴 축소`,
  },
  {
    id: 'CX02', type: 'coerce', label: '슬림 체형 ↔ 넓은 골반',
    when: (p) => p.body_type === 'slim' && (p.hip === 'wide' || p.hip === 'full'),
    fix: () => ({ hip: 'slim' }),
    why: (p, m) => `체형 정합: ${nameOfPart(m, 'body_type', p.body_type)} ↔ ${nameOfPart(m, 'hip', p.hip)} → 골반 축소`,
  },
  {
    id: 'CX03', type: 'coerce', label: '슬림 체형 ↔ 넓은 덩치',
    when: (p) => p.body_type === 'slim' && (p.build === 'wide' || p.build === 'athletic'),
    fix: () => ({ build: 'slender' }),
    why: (p, m) => `체형 정합: ${nameOfPart(m, 'body_type', p.body_type)} ↔ ${nameOfPart(m, 'build', p.build)} → 덩치 슬림화`,
  },
  {
    id: 'CX04', type: 'coerce', label: '글래머 체형 ↔ 납작한 가슴',
    when: (p) => p.body_type === 'glamour' && (p.bust === 'flat'),
    fix: () => ({ bust: 'large' }),
    why: (p, m) => `체형 정합: ${nameOfPart(m, 'body_type', p.body_type)} ↔ ${nameOfPart(m, 'bust', p.bust)} → 가슴 볼륨업`,
  },
  {
    id: 'CX05', type: 'coerce', label: '글래머 체형 ↔ 좁은 골반',
    when: (p) => p.body_type === 'glamour' && (p.hip === 'slim'),
    fix: () => ({ hip: 'wide' }),
    why: (p, m) => `체형 정합: ${nameOfPart(m, 'body_type', p.body_type)} ↔ ${nameOfPart(m, 'hip', p.hip)} → 골반 라인 강조`,
  },
  {
    id: 'CX06', type: 'coerce', label: '스포티 체형 ↔ 글래머 가슴',
    when: (p) => p.body_type === 'athletic' && p.bust === 'glamour',
    fix: () => ({ bust: 'medium' }),
    why: (p, m) => `체형 정합: ${nameOfPart(m, 'body_type', p.body_type)} ↔ ${nameOfPart(m, 'bust', p.bust)} → 가슴 정돈`,
  },
  {
    id: 'CX07', type: 'coerce', label: '스포티 체형 ↔ 여린 덩치',
    when: (p) => p.body_type === 'athletic' && p.build === 'slender',
    fix: () => ({ build: 'athletic' }),
    why: (p, m) => `체형 정합: ${nameOfPart(m, 'body_type', p.body_type)} ↔ ${nameOfPart(m, 'build', p.build)} → 탄탄한 골격`,
  },
  {
    id: 'CX08', type: 'coerce', label: '아담 키 ↔ 글래머 체형',
    when: (p) => p.height === 'petite' && p.body_type === 'glamour',
    fix: () => ({ body_type: 'standard' }),
    why: (p, m) => `체형 정합: ${nameOfPart(m, 'height', p.height)} ↔ ${nameOfPart(m, 'body_type', p.body_type)} → 표준 체형`,
  },
  {
    id: 'CX09', type: 'coerce', label: '아담 키 ↔ 넓은 덩치',
    when: (p) => p.height === 'petite' && p.build === 'wide',
    fix: () => ({ build: 'standard' }),
    why: (p, m) => `체형 정합: ${nameOfPart(m, 'height', p.height)} ↔ ${nameOfPart(m, 'build', p.build)} → 덩치 표준화`,
  },

  /* ---------- 파츠 충돌 ---------- */
  {
    id: 'CX10', type: 'coerce', label: '수인귀 ↔ 큰 머리 악세',
    when: (p) => KEMONOMIMI.includes(p.ears) && HEAD_COVER.includes(p.head_acc),
    fix: () => ({ head_acc: 'none' }),
    why: (p, m) => `충돌 해소: 수인귀(${nameOfPart(m, 'ears', p.ears)}) ↔ ${nameOfPart(m, 'head_acc', p.head_acc)} — 귀가 머리 위를 차지해 악세 제거`,
  },
  {
    id: 'CX11', type: 'coerce', label: '수인귀 ↔ 사이버 바이저',
    when: (p) => KEMONOMIMI.includes(p.ears) && p.glasses === 'visor',
    fix: () => ({ glasses: 'round' }),
    why: () => '충돌 해소: 바이저 ↔ 수인귀 → 라운드 안경으로 교체',
  },
  {
    id: 'CX12', type: 'coerce', label: '짧은 머리 ↔ 헤드폰',
    when: (p) => (p.hair_back === 'bob' || p.hair_back === 'short') && p.head_acc === 'headphones',
    fix: () => ({ head_acc: 'none' }),
    why: (p, m) => `충돌 해소: ${nameOfPart(m, 'hair_back', p.hair_back)} ↔ ${nameOfPart(m, 'head_acc', p.head_acc)} — 머리 볼륨이 없어 밴드가 뜸`,
  },
  {
    id: 'CX13', type: 'coerce', label: '반쯤 감긴 눈 ↔ 윙크',
    when: (p) => p.eyes_shape === 'sleepy' && p.expression === 'wink',
    fix: () => ({ expression: 'smile' }),
    why: () => '표정 정합: 반쯤 감긴 눈 + 윙크 불가 → 미소로 대체',
  },
  {
    id: 'CX14', type: 'coerce', label: '활짝 웃음 ↔ 다문 입',
    when: (p) => p.expression === 'grin' && (p.mouth === 'flat' || p.mouth === 'pout'),
    fix: () => ({ mouth: 'open' }),
    why: (p, m) => `표정 정합: ${nameOfPart(m, 'expression', p.expression)} ↔ ${nameOfPart(m, 'mouth', p.mouth)} → 벌린 입으로 교체`,
  },
  {
    id: 'CX15', type: 'coerce', label: '쌍삐침 ↔ 모자',
    when: (p) => p.ahoge === 'double' && BIG_ACC.includes(p.head_acc),
    fix: () => ({ ahoge: 'single' }),
    why: (p, m) => `충돌 해소: ${nameOfPart(m, 'ahoge', p.ahoge)} ↔ ${nameOfPart(m, 'head_acc', p.head_acc)} — 한 가닥만 남김`,
  },
  {
    id: 'CX16', type: 'coerce', label: '가슴 볼륨 ↔ 좁은 골반',
    when: (p) => (p.bust === 'large' || p.bust === 'glamour') && p.hip === 'slim',
    fix: () => ({ hip: 'normal' }),
    why: (p, m) => `실루엣 정합: ${nameOfPart(m, 'bust', p.bust)} ↔ ${nameOfPart(m, 'hip', p.hip)} → 골반 라인 보정`,
  },

  /* ---------- 오버라이드 (파츠 선언형 은폐) ---------- */
  {
    id: 'OV01', type: 'override', label: '상위 파츠가 하위 레이어를 소유',
    // 예: 표정 파츠가 입 그림을 직접 그리면 mouth 슬롯 레이어는 숨긴다.
    hideOwned: true,
    why: (p, m) => '표정 파츠가 입을 직접 그림 → 기본 입 레이어 은폐',
  },
  {
    id: 'OV02', type: 'override', label: '파츠가 선언한 은폐(hides)',
    // 예: 바이저는 눈썹을 가린다, 티아라는 삐침을 가린다.
    hideDeclared: true,
    why: (p, m) => '파츠 선언(hides)에 따라 하위 슬롯 은폐',
  },
];

/** 문서용 규칙 표 (SPEC.md 와 코드가 어긋나지 않게 여기서 뽑아 쓴다) */
export const RULE_TABLE = RULES.map((r) => ({ id: r.id, type: r.type, label: r.label }));

/** 혼잡도 예산: 점수 합 ≤ 6. 제거 우선순위 낮은 것부터. */
export const CLUTTER_ORDER = ['cheek_fx', 'aura', 'glasses', 'head_acc'];

/* =============================================================================
 * 6. 생성 — 시드 → 레시피
 * ========================================================================== */

function pickFrom(rng, list, world, tagBoost) {
  if (!list || !list.length) return null;
  const ws = list.map((it) => weightOf(it, world, tagBoost));
  const total = ws.reduce((a, b) => a + b, 0);
  if (total <= 0) return list[Math.floor(rng() * list.length) % list.length];
  let r = rng() * total;
  for (let i = 0; i < list.length; i++) {
    r -= ws[i];
    if (r <= 0) return list[i];
  }
  return list[list.length - 1];
}

function pickSlotValue(slot, seed, world, manifest) {
  const cfg = (manifest.slots && manifest.slots[slot.key]) || {};
  const fx = { ...DEFAULTS, ...(manifest.fx || {}) };
  const rng = rngFor(seed, slot.key);

  if (slot.kind === 'part') {
    const part = pickFrom(rng, partsOf(manifest, slot.key), world, fx.tag_boost);
    return part ? part.id : null;
  }
  if (slot.kind === 'color') {
    const pool = paletteOf(manifest, slot.key);
    if (!pool.length) return null; // 색 조화 단계에서 파생
    const it = pickFrom(rng, pool, world, fx.tag_boost);
    return it ? it.color : null;
  }
  // flag
  const p = typeof cfg.probability === 'number'
    ? cfg.probability
    : (slot.key === 'heterochromia' ? fx.odd_eye_chance : fx.two_tone_chance);
  return rng() < p;
}

/**
 * 생성.
 * @param {object}  o
 * @param {object}  o.manifest  파츠팩 manifest (필수)
 * @param {number|string} o.seed  시드 (문자열 시드는 해시)
 * @param {string}  [o.world]   세계관 id ('all' 포함)
 * @param {object}  [o.locks]   { 슬롯key: true } 잠근 슬롯
 * @param {object}  [o.prev]    이전 레시피 (잠금 유지용)
 * @param {string[]}[o.only]    이 슬롯들만 리롤
 */
export function generate(opts = {}) {
  const manifest = opts.manifest;
  if (!manifest || !manifest.slots) throw new Error('generate(): manifest.slots 가 필요합니다.');
  const seed = parseSeed(opts.seed);
  const worlds = (manifest.worlds || []).map((w) => w.id);
  const world = opts.world && worlds.includes(opts.world) ? opts.world : (worlds[0] || 'all');
  const locks = opts.locks || {};
  const prev = opts.prev || null;
  const only = Array.isArray(opts.only) && opts.only.length ? new Set(opts.only) : null;
  const pins = opts.pins || {};
  const fx = { ...DEFAULTS, ...(manifest.fx || {}) };
  const log = [];
  const parts = {};
  const locked = { ...locks };

  /* ---- 6.1 슬롯별 값 (서브스트림 → 잠금/부분리롤) ---- */
  for (const slot of SLOTS) {
    const hasPrev = prev && prev.parts && prev.parts[slot.key] !== undefined;
    const keep = hasPrev && (locks[slot.key] || (only && !only.has(slot.key)));
    if (keep) {
      parts[slot.key] = clone(prev.parts[slot.key]);
      if (locks[slot.key]) log.push({ id: 'LOCK', type: 'lock', slot: slot.key, text: `잠금 유지: ${slot.label}` });
      continue;
    }
    parts[slot.key] = pickSlotValue(slot, seed, world, manifest);
  }

  /* ---- 6.1b 고정값(pins) — 공유 URL · 커스텀 업로드 파츠 고정 ---- */
  for (const [key, value] of Object.entries(pins)) {
    if (!SLOT_MAP[key] || value === undefined || value === null) continue;
    parts[key] = value;
    locked[key] = true;
    log.push({ id: 'PIN', type: 'lock', slot: key, text: `고정: ${SLOT_MAP[key].label} = ${value}` });
  }

  /* ---- 6.2 값 교정 규칙 ---- */
  enforceRules(parts, { manifest, locks: locked, log });

  /* ---- 6.3 혼잡도 예산 ---- */
  const clutter = enforceBudget(parts, { manifest, locks: locked, log, fx });

  /* ---- 6.4 색 조화 (60:30:10 · ΔL 24) ---- */
  const colors = resolveColors(parts, { seed, world, manifest, locks: locked, log, fx });

  /* ---- 6.5 파생 값 ---- */
  parts.hair_color = colors.hair;
  parts.outfit_color = colors.outfit;
  parts.eye_color = colors.iris;
  parts.skin_tone = parts.skin_tone || colors.skin;

  /* ---- 6.6 은폐(override) ---- */
  const hidden = resolveHidden(parts, manifest, locked, log);

  /* ---- 6.7 표시 이름 · 프롬프트 조각 ---- */
  const names = {};
  const bits = {};
  for (const slot of SLOTS) {
    const v = parts[slot.key];
    if (slot.kind === 'part') {
      const part = partOf(manifest, slot.key, v);
      names[slot.key] = part ? (part.name || part.id) : String(v);
      bits[slot.key] = part && part.prompt ? part.prompt : '';
    } else if (slot.kind === 'color') {
      names[slot.key] = colorName(v);
      bits[slot.key] = '';
    } else {
      names[slot.key] = v ? 'ON' : 'OFF';
      bits[slot.key] = '';
    }
  }

  const transform = resolveTransform(parts, manifest);

  const recipe = {
    app: 'anime-character-combinator',
    version: VERSION,
    author: '포시즌jo',
    schema: SCHEMA,
    pack: { id: manifest.id || null, version: manifest.version || null },
    seed: seed >>> 0,
    seedHex: formatSeed(seed),
    world,
    parts,
    colors,
    transform,
    hidden,
    clutter,
    names,
    bits,
    locks: { ...locked },
    log,
  };
  recipe.sig = signature(recipe);
  return recipe;
}

/**
 * 기존 레시피에서 리롤. 잠긴 슬롯은 그대로, 나머지는 새 시드로 뽑는다.
 * seed 를 생략하면 nextSeed(recipe.seed) 를 쓴다.
 */
export function reroll(recipe, opts = {}) {
  const manifest = opts.manifest;
  if (!manifest) throw new Error('reroll(): manifest 가 필요합니다.');
  const seed = opts.seed === undefined ? nextSeed(recipe.seed) : parseSeed(opts.seed);
  const locks = opts.locks || recipe.locks || {};
  return generate({
    manifest,
    seed,
    world: opts.world || recipe.world,
    locks,
    pins: opts.pins,
    prev: recipe,
    only: opts.only,
  });
}

function clone(v) {
  return v === null || typeof v !== 'object' ? v : JSON.parse(JSON.stringify(v));
}

/* =============================================================================
 * 7. 규칙 실행
 * ========================================================================== */

/**
 * 값 교정 규칙을 순서대로 적용한다(순수 함수 — 테스트에서 직접 호출 가능).
 * 잠긴 슬롯은 바꾸지 않고 blocked 로그를 남긴다.
 * @returns {object} parts (같은 객체를 변형해 돌려준다)
 */
export function enforceRules(parts, ctx = {}) {
  const { manifest = {}, locks = {}, log = [] } = ctx;
  for (let pass = 0; pass < 4; pass++) {
    let changed = false;
    for (const rule of RULES) {
      if (rule.type !== 'coerce') continue;
      if (!rule.when(parts, manifest)) continue;
      const text = typeof rule.why === 'function' ? rule.why(parts, manifest) : rule.label;
      const fix = rule.fix(parts, manifest) || {};
      const blocked = [];
      const applied = [];
      for (const [key, value] of Object.entries(fix)) {
        if (parts[key] === value) continue;
        if (locks[key]) { blocked.push(key); continue; }
        parts[key] = value;
        applied.push(key);
      }
      if (blocked.length) {
        log.push({
          id: rule.id, type: 'blocked', slot: blocked[0],
          text: `잠금 우선: ${rule.label} — ${blocked.map((k) => SLOT_MAP[k] ? SLOT_MAP[k].label : k).join(', ')} 잠김, 규칙 적용 보류`,
        });
      }
      if (applied.length) {
        changed = true;
        log.push({ id: rule.id, type: 'coerce', slot: applied[0], text });
      }
    }
    if (!changed) break;
  }
  return parts;
}

/** 혼잡도 예산(≤6). 우선순위 낮은 슬롯부터 none 으로. */
export function enforceBudget(parts, ctx = {}) {
  const { manifest = {}, locks = {}, log = [], fx = DEFAULTS } = ctx;
  const budgetMax = fx.clutter_budget;
  const items = CLUTTER_ORDER
    .filter((k) => SLOT_MAP[k])
    .map((k) => ({ key: k, pts: clutterOf(manifest, k, parts[k]), name: nameOfPart(manifest, k, parts[k]) }));

  let total = items.reduce((a, b) => a + b.pts, 0);
  const removed = [];
  for (const it of items) {
    if (total <= budgetMax) break;
    if (!it.pts) continue;
    if (locks[it.key]) {
      log.push({
        id: 'CB01', type: 'blocked', slot: it.key,
        text: `혼잡도: ${it.name}(-${it.pts}) 는 잠겨 있어 제거하지 못함 (합계 ${total}/${budgetMax})`,
      });
      continue;
    }
    parts[it.key] = 'none';
    total -= it.pts;
    removed.push(it);
    log.push({
      id: 'CB01', type: 'budget', slot: it.key,
      text: `혼잡도 예산 초과: ${it.name}(-${it.pts}) 우선순위 낮아 제거 → 합계 ${total}/${budgetMax}`,
    });
  }

  const finalItems = items
    .filter((it) => !removed.includes(it))
    .map((it) => ({ slot: it.key, id: parts[it.key], points: clutterOf(manifest, it.key, parts[it.key]) }));

  return { total, max: budgetMax, over: Math.max(0, total - budgetMax), items: finalItems };
}

/** 은폐된 슬롯 맵 { slotKey: 이유 } */
export function resolveHidden(parts, manifest, locks = {}, log = []) {
  const hidden = {};
  for (const slot of SLOTS) {
    if (slot.kind !== 'part') continue;
    const part = partOf(manifest, slot.key, parts[slot.key]);
    if (!part) continue;
    // OV01: 상위 파츠가 소유한 하위 슬롯
    if (Array.isArray(part.owns)) {
      for (const owned of part.owns) {
        if (!SLOT_MAP[owned]) continue;
        hidden[owned] = `「${part.name || part.id}」 파츠가 직접 그림`;
        log.push({
          id: 'OV01', type: 'override', slot: owned,
          text: `override: ${part.name || part.id} → ${SLOT_MAP[owned].label} 레이어 은폐(상위 파츠가 소유)`,
        });
      }
    }
    // OV02: 파츠가 선언한 은폐
    if (Array.isArray(part.hides)) {
      for (const k of part.hides) {
        if (!SLOT_MAP[k]) continue;
        hidden[k] = `「${part.name || part.id}」 파츠가 가림`;
        log.push({
          id: 'OV02', type: 'override', slot: k,
          text: `override: ${part.name || part.id} → ${SLOT_MAP[k].label} 은폐(파츠 선언 hides)`,
        });
      }
    }
  }
  // 잠긴 슬롯이 은폐되면 사용자가 알 수 있게 남긴다 (은폐는 잠금과 무관하게 적용)
  for (const k of Object.keys(hidden)) {
    if (locks[k]) {
      log.push({ id: 'OV02', type: 'blocked', slot: k, text: `잠긴 ${SLOT_MAP[k].label} 는 값은 유지되지만 화면에서는 가려짐` });
    }
  }
  return hidden;
}

/* =============================================================================
 * 8. 색 — 60:30:10 조화 + 피부↔머리 명도 잠금
 * ========================================================================== */

function resolveColors(parts, ctx) {
  const { seed, world, manifest, locks = {}, log = [], fx = DEFAULTS } = ctx;
  const rng = rngFor(seed, 'color');
  const palette = (k) => paletteOf(manifest, k);

  const skinPool = palette('skin_tone');
  let skin = parts.skin_tone;
  if (!skin || !/^#[0-9a-f]{3,6}$/i.test(skin)) {
    const it = pickFrom(rng, skinPool, world, fx.tag_boost);
    skin = it ? it.color : '#fbdcc8';
  }

  let outfit = parts.outfit_color;
  if (!outfit) {
    const it = pickFrom(rng, palette('outfit_color'), world, fx.tag_boost);
    outfit = it ? it.color : '#2d3b52';
  }
  let hair = parts.hair_color;
  if (!hair) {
    const it = pickFrom(rng, palette('hair_color'), world, fx.tag_boost);
    hair = it ? it.color : '#3b2a22';
  }

  /* ---- CH01 주조색(의상) ↔ 보조색(머리) 색상각 ---- */
  const oHue = hueOf(outfit);
  let hHue = hueOf(hair);
  let hSat = satOf(hair);
  let hLight = lightOf(hair);
  if (locks.hair_color) {
    log.push({ id: 'CH01', type: 'harmony', slot: 'hair_color', text: '색조화 보류: 머리색이 잠겨 있어 색상각 조정 생략' });
  } else {
    const signed = signedHueDelta(hHue, oHue); // -180..180
    const abs = Math.abs(signed);
    if (abs < fx.hue_collision) {
      // 같은 색으로 붙음 → 원래 방향으로 30° 벌린다 (방향을 지켜야 다시 붙지 않는다)
      const target = signed >= 0 ? 30 : -30;
      hHue = oHue + target;
      log.push({ id: 'CH01', type: 'harmony', slot: 'hair_color', text: `색조화: 머리↔의상 색상각 ${abs.toFixed(0)}° → ${hueDistance(oHue, hHue).toFixed(0)}° (동색 붙음 완화)` });
    } else if (abs > 55 && abs < 115) {
      // 60~110° 중간색은 탁해 보인다 → 가장 가까운 경계 밖으로 민다
      hHue = oHue + Math.sign(signed) * 118;
      log.push({ id: 'CH02', type: 'harmony', slot: 'hair_color', text: `색조화: 머리↔의상 색상각 ${abs.toFixed(0)}° → ${hueDistance(oHue, hHue).toFixed(0)}° (중간색 충돌 회피)` });
    }
  }
  let hairHex = hslToHex(hHue, hSat, hLight);

  /* ---- CH03 피부 ↔ 머리 명도 ΔL ≥ 24 ---- */
  const sL = lightOf(skin);
  let dL = Math.abs(sL - lightOf(hairHex));
  if (dL < fx.luminance_lock) {
    if (locks.hair_color) {
      log.push({ id: 'CH03', type: 'blocked', slot: 'hair_color', text: `명도 대비 부족(ΔL ${dL.toFixed(0)}) — 머리색 잠김, 보정 보류` });
    } else {
      hLight = sL > 62 ? Math.max(8, sL - 42) : Math.min(96, sL + 42);
      hairHex = hslToHex(hHue, hSat, hLight);
      dL = Math.abs(sL - lightOf(hairHex));
      log.push({ id: 'CH03', type: 'contrast', slot: 'hair_color', text: `명도 대비 잠금: 피부 L${sL.toFixed(0)} ↔ 머리 L${lightOf(hairHex).toFixed(0)} → ΔL ${dL.toFixed(0)}` });
    }
  } else {
    log.push({ id: 'CH03', type: 'contrast', slot: 'hair_color', text: `명도 대비 통과: 피부 L${sL.toFixed(0)} ↔ 머리 L${lightOf(hairHex).toFixed(0)} (ΔL ${dL.toFixed(0)} ≥ ${fx.luminance_lock})` });
  }

  /* ---- CH04 의상이 너무 어두워 배경에 묻히는 것 방지 ---- */
  if (!locks.outfit_color && lightOf(outfit) < 26) {
    const lifted = shade(outfit, 16, 4);
    log.push({ id: 'CH04', type: 'contrast', slot: 'outfit_color', text: `의상 대비 보정: L${lightOf(outfit).toFixed(0)} → L${lightOf(lifted).toFixed(0)} (배경에 묻힘 방지)` });
    outfit = lifted;
  }

  /* ---- CH05 10% 강조색: 눈동자 ---- */
  let iris = parts.eye_color;
  if (!iris || !/^#[0-9a-f]{3,6}$/i.test(iris)) {
    iris = deriveIris(rng, hairHex, outfit, world, fx);
    log.push({ id: 'CH05', type: 'harmony', slot: 'eye_color', text: `강조색 배정: 눈동자 ${colorName(iris)} (의상 보색 계열, 10% 면적)` });
  } else if (!locks.eye_color) {
    const irisGap = Math.min(hueDistance(hueOf(iris), hueOf(hairHex)), hueDistance(hueOf(iris), hueOf(outfit)));
    if (irisGap < fx.hue_gap_min) {
      iris = deriveIris(rng, hairHex, outfit, world, fx);
      log.push({ id: 'CH05', type: 'harmony', slot: 'eye_color', text: `강조색 보정: 눈동자가 머리·의상과 붙어 ${colorName(iris)} 로 재배정` });
    }
  }

  const irisB = shade(iris, 6, 4, 152);
  const twoTone = !!parts.hair_two_tone;
  const hairInner = twoTone
    ? shade(hairHex, lightOf(hairHex) > 60 ? -20 : 18, 8, 24)
    : shade(hairHex, -12, 2);
  const outfitAccent = hslToHex(hueOf(outfit) + 165 + (rng() * 24 - 12), 62, 58);

  return {
    skin,
    hair: hairHex,
    hair_inner: hairInner,
    iris,
    iris_b: irisB,
    outfit,
    outfit_accent: outfitAccent,
    mode: '60-30-10',
  };
}

function deriveIris(rng, hairHex, outfitHex, world, fx) {
  const oH = hueOf(outfitHex);
  const hH = hueOf(hairHex);
  const rnd = (a, b) => a + (b - a) * rng();
  const candidates = [
    hslToHex(oH + 172 + rnd(-18, 18), rnd(62, 88), rnd(40, 56)),
    hslToHex(hH + rnd(12, 34), rnd(55, 80), rnd(42, 58)),
    hslToHex(oH + rnd(-26, 26), rnd(58, 84), rnd(44, 60)),
  ];
  if (world === 'fantasy') candidates[0] = hslToHex(rnd(0, 360), 70, 52);
  if (world === 'cyber') candidates[0] = hslToHex([190, 300, 330][Math.floor(rng() * 3)], 88, 55);
  if (world === 'gothic') candidates[0] = hslToHex([280, 340, 20][Math.floor(rng() * 3)], 55, 46);
  const ok = (hex) => hueDistance(hueOf(hex), oH) >= fx.hue_gap_min
    && hueDistance(hueOf(hex), hH) >= fx.hue_gap_min;
  return candidates.find(ok) || candidates[0];
}

/* =============================================================================
 * 9. 트랜스폼 — 자세 · 키 · 덩치 · 가슴/골반 밴드
 * ========================================================================== */

const ZERO_ANCHOR = { head: {}, body: {}, bands: {} };

/** parts 의 height / pose / build / bust / hip 파츠가 선언한 anchor 를 합성한다. */
export function resolveTransform(parts, manifest) {
  const head = { dx: 0, dy: 0, rot: 0, scale: 1 };
  const body = { dx: 0, dy: 0, sx: 1, sy: 1 };
  const bands = { bust: 1, hip: 1, waist: 1 };

  const apply = (slotKey) => {
    const id = parts[slotKey];
    if (id === undefined || id === null) return;
    const part = partOf(manifest, slotKey, id);
    const a = (part && part.anchor) || ZERO_ANCHOR;
    const h = a.head || {};
    const b = a.body || {};
    const bd = a.bands || {};
    if (typeof h.dx === 'number') head.dx += h.dx;
    if (typeof h.dy === 'number') head.dy += h.dy;
    if (typeof h.rot === 'number') head.rot += h.rot;
    if (typeof h.scale === 'number') head.scale *= h.scale;
    if (typeof b.dx === 'number') body.dx += b.dx;
    if (typeof b.dy === 'number') body.dy += b.dy;
    if (typeof b.sx === 'number') body.sx *= b.sx;
    if (typeof b.sy === 'number') body.sy *= b.sy;
    if (typeof bd.bust === 'number') bands.bust *= bd.bust;
    if (typeof bd.hip === 'number') bands.hip *= bd.hip;
    if (typeof bd.waist === 'number') bands.waist *= bd.waist;
  };

  ['height', 'pose', 'build', 'bust', 'hip'].forEach(apply);
  return { head, body, bands };
}

/* =============================================================================
 * 10. 렌더 레이어 빌드 — buildRenderLayers(recipe, manifest)
 * ========================================================================== */

/**
 * 레시피를 "그리는 순서대로 정렬된 레이어 배열"로 바꾼다.
 * 컴포지터(Canvas2D)는 이 배열을 위에서부터 그대로 그리기만 하면 된다.
 *
 * 각 항목:
 *  {
 *    layer, z,                       // 레이어 이름 · 정렬 키(작을수록 뒤)
 *    slot, id,                       // 이 레이어를 만든 슬롯/파츠
 *    src,                            // 이미지 경로 (null = 자리표시자)
 *    tint, tintMode, tintStrength,   // 색 입히기 (tintMode: multiply | soft-light | none | screen)
 *    clip,                           // 'left_eye' | 'right_eye' | null
 *    opacity, visible, hiddenReason,
 *    transform: { x, y, scaleX, scaleY, rot, pivot:[x,y], bands }
 *  }
 */
export function buildRenderLayers(recipe, manifest) {
  if (!recipe) throw new Error('buildRenderLayers(): recipe 가 필요합니다.');
  if (!manifest || !manifest.slots) throw new Error('buildRenderLayers(): manifest 가 필요합니다.');

  const rig = manifest.rig || {};
  const headPivot = rig.head_center || [512, 322];
  const hipPivot = rig.hip || [512, 872];
  const tf = recipe.transform || resolveTransform(recipe.parts || {}, manifest);
  const hidden = recipe.hidden || {};
  const p = recipe.parts || {};
  const colors = recipe.colors || {};
  const out = [];

  const headTf = (extra = {}) => ({
    x: tf.head.dx + (extra.dx || 0),
    y: tf.head.dy + (extra.dy || 0),
    scaleX: tf.head.scale * (extra.scale || 1),
    scaleY: tf.head.scale * (extra.scale || 1),
    rot: tf.head.rot,
    pivot: headPivot,
    bands: null,
  });
  const bodyTf = (extra = {}) => ({
    x: tf.body.dx + (extra.dx || 0),
    y: tf.body.dy + (extra.dy || 0),
    scaleX: tf.body.sx * (extra.scale || 1),
    scaleY: tf.body.sy * (extra.scale || 1),
    rot: 0,
    pivot: hipPivot,
    bands: tf.bands || null,
  });

  const slotPart = (key) => partOf(manifest, key, p[key]);

  const push = (entry) => {
    const hid = hidden[entry.slot];
    if (hid && entry.visible !== false) {
      entry.visible = false;
      entry.hiddenReason = hid;
    }
    out.push(entry);
  };

  const make = (opts) => ({
    layer: opts.layer,
    z: LAYER_Z[opts.layer] + (opts.sub || 0),
    slot: opts.slot,
    id: opts.id === undefined ? p[opts.slot] : opts.id,
    src: opts.src || null,
    tint: opts.tint || null,
    tintMode: opts.tintMode || 'multiply',
    tintStrength: opts.tintStrength === undefined ? 1 : opts.tintStrength,
    clip: opts.clip || null,
    opacity: opts.opacity === undefined ? 1 : opts.opacity,
    visible: opts.visible === undefined ? true : opts.visible,
    hiddenReason: null,
    transform: opts.transform,
  });

  const emitPart = (slotKey, layer, sub, extra = {}) => {
    const part = slotPart(slotKey);
    if (!part || part.id === 'none') return;
    const src = extra.src !== undefined ? extra.src : fileOf(manifest, part, layer);
    if (!src && extra.placeholder === false) return;
    push(make({
      layer, sub, slot: slotKey, id: part.id, src,
      tint: extra.tint, tintMode: extra.tintMode, tintStrength: extra.tintStrength,
      clip: extra.clip, transform: extra.transform || headTf(),
      opacity: extra.opacity, visible: extra.visible,
    }));
  };

  /* ---- 1) background ---- */
  {
    const part = slotPart('background');
    if (part && part.id !== 'none') {
      push(make({
        layer: 'background', sub: 0, slot: 'background', id: part.id,
        src: fileOf(manifest, part, 'background'), tint: null, tintMode: 'none',
        transform: { x: 0, y: 0, scaleX: 1, scaleY: 1, rot: 0, pivot: [0, 0], bands: null },
      }));
    }
  }

  /* ---- 2) aura_back ---- */
  emitPart('aura', 'aura_back', 0, { tintMode: 'none' });

  /* ---- 3) hair_back (+ 투톤 안쪽 레이어) ---- */
  emitPart('hair_back', 'hair_back', SUB.BASE, { tint: colors.hair });
  {
    const part = slotPart('hair_back');
    if (p.hair_two_tone && part && part.id !== 'none') {
      const innerSrc = part.files && part.files.inner ? fileOf(manifest, { files: { hair_back: part.files.inner } }, 'hair_back') : null;
      if (innerSrc) {
        push(make({
          layer: 'hair_back', sub: SUB.TWO_TONE, slot: 'hair_two_tone', id: part.id,
          src: innerSrc, tint: colors.hair_inner, tintMode: 'multiply',
          transform: headTf(),
        }));
      }
    }
  }

  /* ---- 4) body_base (체형 파츠 + 자세 팔) ---- */
  emitPart('body_type', 'body_base', SUB.BASE, { tint: colors.skin, tintMode: 'multiply', transform: bodyTf() });
  {
    const pose = slotPart('pose');
    if (pose && pose.id !== 'none') {
      const src = fileOf(manifest, pose, 'body_base');
      if (src) {
        push(make({
          layer: 'body_base', sub: SUB.OWNED, slot: 'pose', id: pose.id,
          src, tint: colors.skin, tintMode: 'multiply', transform: bodyTf(),
        }));
      }
    }
  }

  /* ---- 5) ears_back ---- */
  emitPart('ears', 'ears_back', 0, { tint: colors.skin });

  /* ---- 6) face_base ---- */
  emitPart('face_shape', 'face_base', 0, { tint: colors.skin });
  {
    // 표정 오버레이(홍조·눈물·땀 등) — 얼굴 위, 눈 아래
    const expr = slotPart('expression');
    if (expr && expr.id !== 'none' && expr.file) {
      push(make({
        layer: 'face_base', sub: SUB.EXPRESSION, slot: 'expression', id: expr.id,
        src: fileOf(manifest, expr, 'face_base'), tint: null, tintMode: 'none', transform: headTf(),
      }));
    }
  }

  /* ---- 7) cheek_fx ---- */
  emitPart('cheek_fx', 'cheek_fx', 0, { tint: null, tintMode: 'none' });

  /* ---- 8) eyes (눈 + 오드아이 + 홍채 무늬) ---- */
  emitPart('eyes_shape', 'eyes', SUB.BASE, { tint: colors.iris });
  {
    const eyes = slotPart('eyes_shape');
    if (p.heterochromia && eyes && eyes.files && eyes.files.right) {
      push(make({
        layer: 'eyes', sub: SUB.ODD_EYE, slot: 'heterochromia', id: eyes.id,
        src: fileOf(manifest, { files: { eyes: eyes.files.right } }, 'eyes'),
        tint: colors.iris_b, tintMode: 'multiply', clip: 'right_eye', transform: headTf(),
      }));
    }
  }
  {
    const fx = slotPart('iris_fx');
    if (fx && fx.id !== 'none') {
      const src = fileOf(manifest, fx, 'eyes');
      if (src) {
        push(make({
          layer: 'eyes', sub: SUB.FX, slot: 'iris_fx', id: fx.id, src,
          tint: shade(colors.iris, 18), tintMode: 'screen', opacity: 0.9, transform: headTf(),
        }));
      }
    }
  }

  /* ---- 9) nose ---- */
  emitPart('nose', 'nose', 0, { tint: null, tintMode: 'none' });

  /* ---- 10) mouth ---- */
  emitPart('mouth', 'mouth', 0, { tint: null, tintMode: 'none' });

  /* ---- 11) eyebrow ---- */
  emitPart('eyebrow', 'eyebrow', 0, { tint: null, tintMode: 'none' });

  /* ---- 12) outfit (+ 악센트) ---- */
  emitPart('outfit', 'outfit', SUB.BASE, { tint: colors.outfit, transform: bodyTf() });
  {
    const part = slotPart('outfit');
    if (part && part.id !== 'none' && part.files && part.files.accent) {
      push(make({
        layer: 'outfit', sub: SUB.ACCENT, slot: 'outfit', id: part.id,
        src: fileOf(manifest, { files: { outfit: part.files.accent } }, 'outfit'),
        tint: colors.outfit_accent, tintMode: 'multiply', transform: bodyTf(),
      }));
    }
  }

  /* ---- 13) hair_side ---- */
  emitPart('hair_side', 'hair_side', 0, { tint: colors.hair });

  /* ---- 14) ears_front ---- */
  emitPart('ears', 'ears_front', 0, { tint: colors.skin });

  /* ---- 15) hair_front ---- */
  emitPart('hair_front', 'hair_front', 0, { tint: colors.hair });

  /* ---- 16) ahoge ---- */
  emitPart('ahoge', 'ahoge', 0, { tint: colors.hair });

  /* ---- 17) glasses ---- */
  emitPart('glasses', 'glasses', 0, { tint: null, tintMode: 'none' });

  /* ---- 18) head_acc ---- */
  emitPart('head_acc', 'head_acc', 0, { tint: null, tintMode: 'none' });

  /* ---- 19) aura_front ---- */
  emitPart('aura', 'aura_front', 0, { tintMode: 'none', opacity: 0.95 });

  /* ---- 20) 상위 파츠가 소유한 레이어 (예: 표정이 그리는 입) ---- */
  for (const slot of SLOTS) {
    if (slot.kind !== 'part') continue;
    const part = slotPart(slot.key);
    if (!part || !Array.isArray(part.owns)) continue;
    for (const owned of part.owns) {
      if (!LAYER_Z[owned] || !part.files || !part.files[owned]) continue;
      push(make({
        layer: owned, sub: SUB.OWNED, slot: slot.key, id: part.id,
        src: fileOf(manifest, { files: { [owned]: part.files[owned] } }, owned),
        tint: null, tintMode: 'none', transform: headTf(),
        visible: !hidden[slot.key],
      }));
    }
  }

  /* ---- 21) watermark ---- */
  if (recipe.watermark !== false) {
    out.push({
      layer: 'watermark', z: LAYER_Z.watermark, slot: 'watermark', id: 'mark',
      kind: 'text', text: recipe.watermark || WATERMARK,
      src: null, tint: null, tintMode: 'none', tintStrength: 0, clip: null,
      opacity: 0.55, visible: true, hiddenReason: null,
      transform: { x: 0, y: 0, scaleX: 1, scaleY: 1, rot: 0, pivot: [0, 0], bands: null },
    });
  }

  /* ---- 정렬: z 오름차순, 같은 z 는 삽입 순서 유지 ---- */
  return out
    .map((e, i) => ({ e, i }))
    .sort((a, b) => (a.e.z - b.e.z) || (a.i - b.i))
    .map((x) => x.e);
}

/* =============================================================================
 * 11. 프롬프트 컴파일 · 레시피 내보내기
 * ========================================================================== */

const WORLD_PROMPT = {
  academy: 'school setting, warm afternoon light',
  fantasy: 'fantasy setting, ethereal glow',
  cyber: 'cyberpunk setting, neon lighting',
  resort: 'summer resort setting, bright sunlight',
  gothic: 'dark gothic setting, moody lighting',
  all: 'soft studio backdrop',
};

export const QUALITY_TAGS = Object.freeze([
  '1girl', 'solo', 'upper body portrait', 'anime style illustration',
  'cel shading', 'clean lineart', 'detailed eyes', 'masterpiece', 'best quality', 'high resolution',
]);

export const NEGATIVE_PROMPT = 'lowres, bad anatomy, extra fingers, watermark text, logo, jpeg artifacts, blurry, deformed eyes, mismatched eyes (unless heterochromia)';

/**
 * 레시피 → 이미지 생성용 영어 프롬프트.
 * 보통은 manifest 없이 동작한다(생성 시 recipe.bits 에 파츠 프롬프트를 캐시해 두기 때문).
 * 저장된 JSON을 다시 불러온 경우처럼 bits 가 없으면 manifest 를 넘겨 복원한다.
 */
export function compilePrompt(recipe, manifest) {
  const p = recipe.parts || {};
  let b = recipe.bits;
  if (!b && manifest) {
    b = {};
    for (const slot of SLOTS) {
      if (slot.kind !== 'part') continue;
      const part = partOf(manifest, slot.key, p[slot.key]);
      b[slot.key] = part && part.prompt ? part.prompt : '';
    }
  }
  b = b || {};
  const c = recipe.colors || {};
  const pick = (k) => (b[k] ? String(b[k]).trim() : '');
  const t = [...QUALITY_TAGS];

  t.push(pick('pose'), pick('face_shape'), pick('skin_tone'), pick('body_type'));
  t.push(pick('height'), pick('bust'), pick('hip'), pick('build'));
  t.push(pick('ears'));
  t.push(pick('eyes_shape'), `${colorName(c.iris)} eyes`, 'gradient iris', 'specular highlights');
  if (p.iris_fx && p.iris_fx !== 'none') t.push(pick('iris_fx'));
  if (p.heterochromia) t.push('heterochromia', `one ${colorName(c.iris_b)} eye`);
  t.push(pick('eyebrow'), pick('expression'), pick('nose'), pick('mouth'), pick('cheek_fx'));
  t.push(`${colorName(c.hair)} hair`, pick('hair_back'), pick('hair_side'), pick('hair_front'), pick('ahoge'));
  if (p.hair_two_tone) t.push('two-tone hair', `${colorName(c.hair_inner)} inner layer`);
  t.push(`${colorName(c.outfit)} ${pick('outfit')}`);
  t.push(pick('head_acc'), pick('glasses'), pick('aura'), pick('background'));
  t.push(WORLD_PROMPT[recipe.world] || WORLD_PROMPT.all);
  t.push('depth of field', 'rim light', 'soft shadow on skin');

  const seen = new Set();
  const words = [];
  for (const piece of t) {
    if (!piece) continue;
    const s = String(piece).trim();
    if (!s || seen.has(s.toLowerCase())) continue;
    seen.add(s.toLowerCase());
    words.push(s);
  }
  return words.join(', ') + '.';
}

/** 레시피 → 저장용 객체 (JSON 직렬화 직전 형태) */
export function recipeToJSON(recipe) {
  const { names, bits, ...rest } = recipe;
  return {
    ...rest,
    labels: names || {},
    prompt: compilePrompt(recipe),
    negative_prompt: NEGATIVE_PROMPT,
    exported_at: new Date().toISOString(),
  };
}

/** exportJSON(recipe) → 보기 좋은 JSON 문자열 */
export function exportJSON(recipe, space = 2) {
  return JSON.stringify(recipeToJSON(recipe), null, space);
}

/** 레시피 고유 서명 (중복 판정) */
export function signature(recipe) {
  const p = recipe.parts || {};
  const c = recipe.colors || {};
  const keys = Object.keys(p).sort();
  return keys.map((k) => `${k}=${p[k]}`).join('|') + `#${c.hair}/${c.outfit}/${c.iris}/${recipe.world}`;
}

export const sigOf = signature;

/* =============================================================================
 * 12. 배치 (9-grid) · 잠금 유틸 · 라벨
 * ========================================================================== */

/** 서로 다른 캐릭터 count 개 (기본 9). 시드에서 결정론적으로 파생. */
export function batch(seed, world, count = 9, manifest, opts = {}) {
  if (!manifest) throw new Error('batch(): manifest 가 필요합니다.');
  const out = [];
  const seen = new Set();
  const base = parseSeed(seed);
  const mixer = mulberry32(base);
  const locks = opts.locks || {};
  const prev = opts.prev || null;
  let guard = 0;
  while (out.length < count && guard < 600) {
    const s = Math.floor(mixer() * 4294967296) >>> 0;
    const recipe = generate({ manifest, seed: s, world, locks, prev, only: opts.only });
    if (!seen.has(recipe.sig)) {
      seen.add(recipe.sig);
      out.push(recipe);
    }
    guard++;
  }
  return out;
}

/** 잠금 토글 헬퍼 (UI 상태 불변 갱신) */
export function toggleLock(locks, key) {
  const next = { ...locks };
  if (next[key]) delete next[key];
  else next[key] = true;
  return next;
}

export function countLocks(locks = {}) {
  return Object.keys(locks).filter((k) => locks[k]).length;
}

/** 레시피에서 사람이 읽는 라벨 맵 (없으면 manifest 로 계산) */
export function labels(recipe, manifest) {
  if (recipe.names) return { ...recipe.names };
  if (!manifest) return {};
  const out = {};
  for (const slot of SLOTS) {
    const v = recipe.parts[slot.key];
    if (slot.kind === 'part') {
      const part = partOf(manifest, slot.key, v);
      out[slot.key] = part ? (part.name || part.id) : String(v);
    } else if (slot.kind === 'color') out[slot.key] = colorName(v);
    else out[slot.key] = v ? 'ON' : 'OFF';
  }
  return out;
}

/* =============================================================================
 * 13. URL 해시 — 공유 규약
 *   #v=2&s=0x1A2B3C4D&w=cyber&l=eyes_shape,mouth&z=knee
 * ========================================================================== */

export function encodeHash(state = {}) {
  const params = new URLSearchParams();
  params.set('v', '2');
  params.set('s', formatSeed(state.seed === undefined ? 0 : state.seed));
  params.set('w', state.world || 'all');
  const locks = Object.keys(state.locks || {}).filter((k) => state.locks[k]).sort();
  if (locks.length) params.set('l', locks.join(','));
  const pins = Object.entries(state.pins || {}).filter(([k]) => SLOT_MAP[k]);
  if (pins.length) params.set('p', pins.map(([k, v]) => `${k}-${v}`).join(','));
  if (state.zoom) params.set('z', state.zoom);
  return '#' + params.toString();
}

export function decodeHash(hash) {
  const raw = String(hash || '').replace(/^#/, '');
  const params = new URLSearchParams(raw);
  const out = {
    v: params.get('v') || '2',
    seed: parseSeed(params.get('s') || 0),
    world: params.get('w') || 'all',
    locks: {},
    pins: {},
    zoom: params.get('z') || null,
  };
  const l = params.get('l');
  if (l) {
    for (const key of l.split(',')) {
      const k = key.trim();
      if (k && SLOT_MAP[k]) out.locks[k] = true;
    }
  }
  const p = params.get('p');
  if (p) {
    for (const pair of p.split(',')) {
      const i = pair.indexOf('-');
      if (i < 0) continue;
      const k = pair.slice(0, i).trim();
      const v = decodeURIComponent(pair.slice(i + 1).trim());
      if (SLOT_MAP[k] && v) out.pins[k] = v;
    }
  }
  return out;
}

/* =============================================================================
 * 14. manifest 검증 — 새 파츠팩이 엔진 계약을 지키는지 확인
 * ========================================================================== */

export function validateManifest(manifest) {
  const errors = [];
  const warnings = [];
  if (!manifest || typeof manifest !== 'object') return { ok: false, errors: ['manifest 가 객체가 아닙니다'], warnings };

  if (manifest.schema !== SCHEMA) errors.push(`schema 는 "${SCHEMA}" 여야 합니다 (현재: ${manifest.schema})`);
  const cv = manifest.canvas || {};
  if (cv.width !== 1024 || cv.height !== 1280) warnings.push('canvas 는 1024×1280 을 권장합니다 (리그 좌표가 이 기준)');
  if (!manifest.rig || !Array.isArray(manifest.rig.head_center)) errors.push('rig.head_center([x,y]) 가 필요합니다');

  for (const slot of SLOTS) {
    const cfg = manifest.slots && manifest.slots[slot.key];
    if (slot.kind === 'part') {
      if (!cfg || !Array.isArray(cfg.parts) || !cfg.parts.length) {
        errors.push(`slots.${slot.key}.parts 가 비었습니다`);
        continue;
      }
      const ids = new Set();
      for (const part of cfg.parts) {
        if (!part.id) errors.push(`slots.${slot.key}: id 없는 파츠`);
        else if (ids.has(part.id)) errors.push(`slots.${slot.key}: 중복 id "${part.id}"`);
        ids.add(part.id);
        if (!part.name) warnings.push(`slots.${slot.key}/${part.id}: name 없음`);
        const hasFile = typeof part.file === 'string' || (part.files && typeof part.files === 'object');
        const parametric = !!part.anchor; // height/bust/hip/build 처럼 변형만 하는 파츠
        if (part.id !== 'none' && !hasFile && !parametric) warnings.push(`slots.${slot.key}/${part.id}: 파일 경로 없음(자리표시자로 렌더)`);
        if (part.owns) for (const o of part.owns) if (!SLOT_MAP[o]) errors.push(`slots.${slot.key}/${part.id}: owns 에 알 수 없는 슬롯 "${o}"`);
        if (part.hides) for (const h of part.hides) if (!SLOT_MAP[h]) errors.push(`slots.${slot.key}/${part.id}: hides 에 알 수 없는 슬롯 "${h}"`);
      }
    } else if (slot.kind === 'color') {
      const pool = paletteOf(manifest, slot.key);
      if (!pool.length && slot.key !== 'eye_color') errors.push(`palette.${slot.key} 가 비었습니다`);
      for (const c of pool) if (!/^#[0-9a-f]{3,6}$/i.test(c.color || '')) errors.push(`palette.${slot.key}: 잘못된 색 "${c.color}"`);
    } else if (slot.kind === 'flag') {
      if (!cfg) warnings.push(`slots.${slot.key}: 확률 미지정 → 기본값 사용`);
    }
  }

  // 슬롯이 그리는 레이어가 스택에 존재하는지
  for (const slot of SLOTS) {
    for (const layer of slot.layers) {
      if (!LAYER_Z[layer]) errors.push(`슬롯 ${slot.key} 의 레이어 "${layer}" 가 LAYERS 에 없습니다`);
    }
  }

  return { ok: errors.length === 0, errors, warnings };
}

/* =============================================================================
 * 15. 기본 export
 * ========================================================================== */

export default {
  VERSION,
  SCHEMA,
  WATERMARK,
  LAYERS,
  LAYER_Z,
  SUB,
  SLOTS,
  SLOT_MAP,
  GROUPS,
  RULES,
  RULE_TABLE,
  DEFAULTS,
  mulberry32,
  hashString,
  rngFor,
  parseSeed,
  formatSeed,
  nextSeed,
  generate,
  reroll,
  enforceRules,
  enforceBudget,
  resolveHidden,
  resolveTransform,
  buildRenderLayers,
  compilePrompt,
  recipeToJSON,
  exportJSON,
  signature,
  batch,
  toggleLock,
  countLocks,
  labels,
  encodeHash,
  decodeHash,
  validateManifest,
  colorName,
  shade,
  hueDistance,
  lightOf,
};
