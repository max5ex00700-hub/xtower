/* ============================================================
   애니 캐릭터 조합 엔진 — engine.js  (v2)
   결정론적 시드 PRNG · 세계관 가중치 · 60:30:10 팔레트 ·
   충돌/혼잡도 규칙 엔진 · 23개 독립 슬롯
   ============================================================ */
(function () {
  const AE = (window.AE = window.AE || {});

  /* ---------------- PRNG (Mulberry32) ---------------- */
  AE.mulberry32 = function (a) {
    let t = a >>> 0;
    return function () {
      t = (t + 0x6d2b79f5) >>> 0;
      let x = t;
      x = Math.imul(x ^ (x >>> 15), 1 | x);
      x = (x + Math.imul(x ^ (x >>> 7), 61 | x)) ^ x;
      return ((x ^ (x >>> 14)) >>> 0) / 4294967296;
    };
  };
  AE.hashStr = function (s) {
    let h = 2166136261 >>> 0;
    for (let i = 0; i < s.length; i++) {
      h ^= s.charCodeAt(i);
      h = Math.imul(h, 16777619);
    }
    return h >>> 0;
  };
  AE.subRng = function (seed, salt) {
    return AE.mulberry32((seed ^ AE.hashStr(String(salt))) >>> 0);
  };
  AE.rnd = (rng, a, b) => a + (b - a) * rng();
  AE.rint = (rng, a, b) => Math.floor(a + (b - a + 1) * rng());

  /* ---------------- 색 유틸 ---------------- */
  const clamp = (v, a, b) => (v < a ? a : v > b ? b : v);
  function hex2rgb(h) {
    h = h.replace('#', '');
    if (h.length === 3) h = h.split('').map((c) => c + c).join('');
    return [parseInt(h.slice(0, 2), 16), parseInt(h.slice(2, 4), 16), parseInt(h.slice(4, 6), 16)];
  }
  function rgb2hex(r, g, b) {
    const f = (v) => clamp(Math.round(v), 0, 255).toString(16).padStart(2, '0');
    return '#' + f(r) + f(g) + f(b);
  }
  function rgb2hsl(r, g, b) {
    r /= 255; g /= 255; b /= 255;
    const mx = Math.max(r, g, b), mn = Math.min(r, g, b);
    let h = 0, s = 0;
    const l = (mx + mn) / 2, d = mx - mn;
    if (d) {
      s = l > 0.5 ? d / (2 - mx - mn) : d / (mx + mn);
      if (mx === r) h = ((g - b) / d + (g < b ? 6 : 0)) / 6;
      else if (mx === g) h = ((b - r) / d + 2) / 6;
      else h = ((r - g) / d + 4) / 6;
    }
    return [h * 360, s * 100, l * 100];
  }
  function hsl2hex(h, s, l) {
    h = ((h % 360) + 360) % 360; s = clamp(s, 0, 100) / 100; l = clamp(l, 0, 100) / 100;
    const c = (1 - Math.abs(2 * l - 1)) * s, x = c * (1 - Math.abs(((h / 60) % 2) - 1)), m = l - c / 2;
    let r = 0, g = 0, b = 0;
    if (h < 60) [r, g, b] = [c, x, 0];
    else if (h < 120) [r, g, b] = [x, c, 0];
    else if (h < 180) [r, g, b] = [0, c, x];
    else if (h < 240) [r, g, b] = [0, x, c];
    else if (h < 300) [r, g, b] = [x, 0, c];
    else [r, g, b] = [c, 0, x];
    return rgb2hex((r + m) * 255, (g + m) * 255, (b + m) * 255);
  }
  AE.hex2rgb = hex2rgb;
  AE.rgb2hsl = rgb2hsl;
  AE.hsl2hex = hsl2hex;
  AE.lum = (hex) => rgb2hsl.apply(null, hex2rgb(hex))[2];
  AE.hue = (hex) => rgb2hsl.apply(null, hex2rgb(hex))[0];
  AE.shade = function (hex, dl, ds, dh) {
    const [h, s, l] = rgb2hsl.apply(null, hex2rgb(hex));
    return hsl2hex(h + (dh || 0), s + (ds || 0), l + dl);
  };
  AE.mix = function (a, b, t) {
    const A = hex2rgb(a), B = hex2rgb(b);
    return rgb2hex(A[0] + (B[0] - A[0]) * t, A[1] + (B[1] - A[1]) * t, A[2] + (B[2] - A[2]) * t);
  };
  AE.alpha = function (hex, a) {
    const [r, g, b] = hex2rgb(hex);
    return `rgba(${r},${g},${b},${a})`;
  };
  AE.hueDist = (a, b) => {
    const d = Math.abs((((a - b) % 360) + 360) % 360);
    return d > 180 ? 360 - d : d;
  };
  AE.colorName = function (hex) {
    const [h, s, l] = rgb2hsl.apply(null, hex2rgb(hex));
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
  };

  /* ---------------- 세계관 ---------------- */
  AE.WORLDS = [
    { id: 'all', name: '전체 랜덤', short: 'ALL', color: '#e4b968' },
    { id: 'academy', name: '학원물', short: 'SCHOOL', color: '#7cc6ff' },
    { id: 'fantasy', name: '판타지', short: 'FANTASY', color: '#8ee6b0' },
    { id: 'cyber', name: '사이버·네온', short: 'CYBER', color: '#ff7bd5' },
    { id: 'resort', name: '리조트', short: 'RESORT', color: '#ffc46b' },
    { id: 'gothic', name: '다크고딕', short: 'GOTHIC', color: '#c39bff' },
  ];

  /* ---------------- 부품 카탈로그 ---------------- */
  const C = {};
  AE.CAT = C;

  C.faceShape = [
    { id: 'oval', name: '계란형', tags: ['academy', 'resort'], prompt: 'soft oval anime face' },
    { id: 'vline', name: '갸름 V라인', tags: ['gothic', 'cyber'], prompt: 'slim V-line jaw' },
    { id: 'baby', name: '둥근 베이비페이스', tags: ['academy', 'fantasy'], prompt: 'round baby face, youthful cheeks' },
    { id: 'cool', name: '날렵 쿨형', tags: ['cyber', 'gothic'], prompt: 'sharp cool face, defined cheekbones' },
  ];

  C.skinTone = [
    { id: 'pale', name: '설백 페일', color: '#fdeae3', tags: ['gothic', 'fantasy'], prompt: 'pale porcelain skin' },
    { id: 'peach', name: '복숭아 피치', color: '#fbdcc8', tags: ['academy', 'resort'], prompt: 'peach tone skin' },
    { id: 'ivory', name: '웜 아이보리', color: '#f6dfc4', tags: ['academy', 'fantasy'], prompt: 'warm ivory skin' },
    { id: 'honey', name: '허니 베이지', color: '#efc9a4', tags: ['resort', 'cyber'], prompt: 'honey beige skin' },
    { id: 'tan', name: '탠 브론즈', color: '#d9a47c', tags: ['resort'], prompt: 'sun-tanned bronze skin' },
  ];

  C.bodyType = [
    { id: 'slim', name: '슬림', tags: ['gothic', 'cyber'], prompt: 'slender slim figure, narrow shoulders' },
    { id: 'standard', name: '표준', tags: ['academy'], prompt: 'average build' },
    { id: 'glamour', name: '글래머 볼륨', tags: ['resort', 'gothic'], prompt: 'curvy figure, hourglass waist' },
    { id: 'athletic', name: '스포티', tags: ['cyber', 'resort'], prompt: 'athletic toned build, broad shoulders' },
  ];

  C.height = [
    { id: 'petite', name: '아담 · 6.5등신', tags: ['academy', 'fantasy'], prompt: 'petite slender height, 6.5 head proportion' },
    { id: 'standard', name: '표준 · 7.2등신', tags: [], prompt: 'average height, 7.2 head proportion' },
    { id: 'tall', name: '장신 · 8등신', tags: ['cyber', 'gothic', 'resort'], prompt: 'tall model height, 8 head proportion, long legs' },
  ];

  C.bust = [
    { id: 'flat', name: '슬림', tags: ['cyber', 'academy'], prompt: 'flat chest' },
    { id: 'medium', name: '보통', tags: [], prompt: 'medium bust' },
    { id: 'large', name: '볼륨', tags: ['resort', 'gothic'], prompt: 'large bust' },
    { id: 'glamour', name: '글래머', tags: ['resort'], prompt: 'very large bust, glamorous' },
  ];

  C.hip = [
    { id: 'slim', name: '슬림 힙', tags: ['cyber', 'academy'], prompt: 'slim hips' },
    { id: 'normal', name: '보통 힙', tags: [], prompt: 'average hips' },
    { id: 'wide', name: '골반 강조', tags: ['resort', 'gothic'], prompt: 'wide hips, pronounced waist curve' },
    { id: 'full', name: '풍만 힙', tags: ['resort'], prompt: 'full curvy hips' },
  ];

  C.build = [
    { id: 'slender', name: '여리여리', tags: ['gothic', 'fantasy'], prompt: 'slender delicate frame, thin arms' },
    { id: 'standard', name: '표준', tags: [], prompt: 'normal build' },
    { id: 'athletic', name: '탄탄 스포티', tags: ['cyber', 'resort'], prompt: 'athletic toned frame, firm shoulders' },
    { id: 'wide', name: '듬직 와이드', tags: ['cyber'], prompt: 'broad sturdy frame, wide shoulders' },
  ];

  C.pose = [
    { id: 'standing', name: '정면 스탠딩', tags: ['academy', 'gothic'], prompt: 'facing viewer, neutral standing pose' },
    { id: 'tilt', name: '고개 갸웃', tags: ['academy', 'resort'], prompt: 'head tilted, cute pose' },
    { id: 'threequarter', name: '3/4 측면', tags: ['cyber', 'fantasy'], prompt: 'three-quarter view, looking back' },
    { id: 'v_sign', name: '손 브이(V)', tags: ['academy', 'cyber'], prompt: 'peace sign near cheek' },
    { id: 'wave', name: '손 인사', tags: ['resort', 'academy'], prompt: 'waving hand hello' },
    { id: 'crossed', name: '팔짱', tags: ['gothic', 'cyber'], prompt: 'arms crossed' },
    { id: 'hip', name: '허리 손', tags: ['resort', 'cyber'], prompt: 'hand on hip' },
    { id: 'chin', name: '턱 괴기', tags: ['gothic', 'fantasy'], prompt: 'hand resting on chin, thoughtful' },
  ];

  C.eyeShape = [
    { id: 'round', name: '둥근 순정눈', tags: ['academy', 'resort'], prompt: 'large round eyes' },
    { id: 'sharp', name: '샤프한 눈', tags: ['cyber', 'gothic'], prompt: 'sharp narrow eyes' },
    { id: 'droopy', name: '처진 다정눈', tags: ['academy', 'fantasy'], prompt: 'gentle droopy eyes' },
    { id: 'cat', name: '고양이눈', tags: ['cyber', 'fantasy'], prompt: 'upturned cat eyes' },
    { id: 'sleepy', name: '반쯤 감긴 눈', tags: ['gothic', 'cyber'], prompt: 'half-lidded eyes' },
    { id: 'wide', name: '또렷한 큰눈', tags: ['fantasy', 'academy'], prompt: 'wide sparkling eyes' },
  ];

  C.irisFx = [
    { id: 'star', name: '별 무늬', tags: ['fantasy', 'academy'], prompt: 'star-shaped iris highlight' },
    { id: 'heart', name: '하트 무늬', tags: ['resort', 'academy'], prompt: 'heart-shaped iris highlight' },
    { id: 'ring', name: '동심원', tags: ['cyber', 'gothic'], prompt: 'concentric ring iris' },
    { id: 'gradient', name: '그라데이션', tags: ['resort', 'fantasy'], prompt: 'vertical gradient iris' },
    { id: 'sparkle', name: '반짝 다중 하이라이트', tags: ['academy', 'fantasy'], prompt: 'multiple sparkle highlights' },
    { id: 'none', name: '없음', tags: ['gothic'], prompt: 'plain flat iris' },
  ];

  C.eyebrow = [
    { id: 'straight', name: '일자 눈썹', tags: ['academy', 'cyber'], prompt: 'straight eyebrows' },
    { id: 'arch', name: '아치 눈썹', tags: ['gothic', 'resort'], prompt: 'arched eyebrows' },
    { id: 'cool', name: '올라간 쿨 눈썹', tags: ['cyber', 'gothic'], prompt: 'angled cool eyebrows' },
    { id: 'gentle', name: '처진 순둥 눈썹', tags: ['academy', 'fantasy'], prompt: 'gentle downward eyebrows' },
    { id: 'thick', name: '굵은 눈썹', tags: ['resort', 'academy'], prompt: 'thick bold eyebrows' },
  ];

  C.expression = [
    { id: 'calm', name: '무표정', tags: ['gothic', 'cyber'], prompt: 'calm neutral expression' },
    { id: 'smile', name: '미소', tags: ['academy', 'resort'], prompt: 'soft smile' },
    { id: 'grin', name: '활짝 웃음', tags: ['academy', 'resort'], prompt: 'happy open smile' },
    { id: 'wink', name: '윙크', tags: ['academy', 'cyber'], prompt: 'one eye closed wink' },
    { id: 'shy', name: '수줍음', tags: ['academy', 'fantasy'], prompt: 'shy blushing expression' },
    { id: 'cool', name: '쿨한 시선', tags: ['cyber', 'gothic'], prompt: 'cool confident gaze' },
    { id: 'surprise', name: '놀람', tags: ['resort', 'fantasy'], prompt: 'surprised expression' },
  ];

  C.nose = [
    { id: 'dot', name: '점코', tags: ['academy', 'resort'], prompt: 'tiny dot nose' },
    { id: 'line', name: '선코', tags: ['cyber', 'gothic'], prompt: 'thin line nose' },
    { id: 'shade', name: '음영코', tags: ['fantasy', 'gothic'], prompt: 'shaded soft nose bridge' },
    { id: 'none', name: '생략', tags: [], prompt: 'minimal nose' },
  ];

  C.mouth = [
    { id: 'soft', name: '옅은 미소', tags: ['academy', 'fantasy'], prompt: 'soft closed smile' },
    { id: 'open', name: '벌린 입', tags: ['resort', 'academy'], prompt: 'slightly open mouth' },
    { id: 'cat', name: '고양이 입', tags: ['academy', 'fantasy'], prompt: 'cat-like :3 mouth' },
    { id: 'smirk', name: '쌔끈한 미소', tags: ['cyber', 'gothic'], prompt: 'confident smirk' },
    { id: 'pout', name: '발그레 삐침', tags: ['academy', 'resort'], prompt: 'small pout' },
    { id: 'flat', name: '일자 입', tags: ['gothic', 'cyber'], prompt: 'flat lips' },
  ];

  C.ears = [
    { id: 'human', name: '인간귀', tags: ['academy', 'resort', 'cyber', 'gothic'], prompt: 'normal human ears', w: 2.4 },
    { id: 'elf', name: '엘프 뾰족귀', tags: ['fantasy'], prompt: 'long pointed elf ears' },
    { id: 'cat', name: '수인 고양이귀', tags: ['fantasy', 'cyber', 'academy'], prompt: 'fluffy cat ears on head' },
    { id: 'fox', name: '수인 여우귀', tags: ['fantasy', 'resort'], prompt: 'fox ears on head' },
    { id: 'rabbit', name: '수인 토끼귀', tags: ['fantasy', 'resort', 'academy'], prompt: 'long rabbit ears on head' },
  ];

  C.hairBack = [
    { id: 'long_straight', name: '긴 생머리', tags: ['academy', 'gothic'], prompt: 'long straight hair' },
    { id: 'twin_tails', name: '트윈테일', tags: ['academy', 'cyber'], prompt: 'long twin tails' },
    { id: 'ponytail', name: '포니테일', tags: ['resort', 'academy'], prompt: 'high ponytail' },
    { id: 'wavy', name: '웨이브 롱', tags: ['resort', 'fantasy'], prompt: 'long wavy hair' },
    { id: 'bob', name: '단발', tags: ['cyber', 'academy'], prompt: 'short bob cut' },
    { id: 'braid', name: '땋은 머리', tags: ['fantasy', 'academy'], prompt: 'braided hair' },
    { id: 'short', name: '숏컷', tags: ['cyber', 'gothic'], prompt: 'short pixie hair' },
  ];

  C.hairSide = [
    { id: 'short', name: '짧은 옆머리', tags: ['cyber', 'academy'], prompt: 'short face-framing strands' },
    { id: 'long', name: '긴 옆머리', tags: ['gothic', 'fantasy'], prompt: 'long face-framing locks' },
    { id: 'curl', name: '휘어진 옆머리', tags: ['resort', 'fantasy'], prompt: 'curled side locks' },
    { id: 'braid', name: '옆 땋기', tags: ['fantasy', 'academy'], prompt: 'small side braid' },
    { id: 'spiky', name: '삐죽 옆머리', tags: ['cyber', 'gothic'], prompt: 'spiky side strands' },
  ];

  C.hairFront = [
    { id: 'blunt', name: '일자 앞머리', tags: ['academy', 'gothic'], prompt: 'blunt straight bangs' },
    { id: 'parted', name: '6:4 가르마', tags: ['academy', 'cyber'], prompt: 'side-parted bangs' },
    { id: 'side_swept', name: '쓸어넘긴 앞머리', tags: ['resort', 'fantasy'], prompt: 'swept side bangs' },
    { id: 'hime', name: '히메컷', tags: ['gothic', 'fantasy'], prompt: 'hime-cut straight bangs' },
    { id: 'choppy', name: '층진 앞머리', tags: ['cyber', 'fantasy'], prompt: 'choppy layered bangs' },
    { id: 'curtain', name: '커튼 뱅', tags: ['resort', 'academy'], prompt: 'curtain bangs' },
    { id: 'see_through', name: '시스루 뱅', tags: ['academy', 'fantasy'], prompt: 'see-through wispy bangs' },
  ];

  C.ahoge = [
    { id: 'none', name: '없음', tags: [], prompt: '' },
    { id: 'single', name: '삐침 한 가닥', tags: ['academy', 'fantasy'], prompt: 'ahoge' },
    { id: 'double', name: '쌍삐침', tags: ['cyber', 'academy'], prompt: 'double ahoge' },
    { id: 'curl', name: '말린 삐침', tags: ['resort', 'fantasy'], prompt: 'curled ahoge' },
  ];

  C.outfit = [
    { id: 'sailor', name: '세일러복', tags: ['academy'], prompt: 'sailor school uniform with neckerchief' },
    { id: 'hoodie', name: '후드티', tags: ['cyber', 'academy'], prompt: 'oversized hoodie' },
    { id: 'blazer', name: '블레이저 교복', tags: ['academy', 'gothic'], prompt: 'blazer school uniform with necktie' },
    { id: 'cyber', name: '사이버 재킷', tags: ['cyber'], prompt: 'techwear cyberpunk jacket' },
    { id: 'gothic', name: '고딕 드레스', tags: ['gothic', 'fantasy'], prompt: 'gothic lolita dress' },
    { id: 'resort', name: '리조트 웨어', tags: ['resort'], prompt: 'summer resort off-shoulder top' },
  ];

  C.headAcc = [
    { id: 'none', name: '없음', tags: [], prompt: '', clutter: 0 },
    { id: 'ribbon', name: '리본', tags: ['academy', 'gothic'], prompt: 'hair ribbon', clutter: 1 },
    { id: 'headband', name: '헤어밴드', tags: ['academy', 'resort'], prompt: 'hair band', clutter: 1 },
    { id: 'headphones', name: '헤드폰', tags: ['cyber'], prompt: 'headphones', clutter: 2 },
    { id: 'beret', name: '베레모', tags: ['academy', 'resort'], prompt: 'beret', clutter: 2 },
    { id: 'hairpin', name: '별 핀', tags: ['academy', 'fantasy'], prompt: 'star hairpin', clutter: 1 },
    { id: 'crown', name: '티아라', tags: ['gothic', 'fantasy'], prompt: 'tiara crown', clutter: 3 },
  ];

  C.glasses = [
    { id: 'none', name: '없음', tags: [], prompt: '', clutter: 0 },
    { id: 'round', name: '라운드 안경', tags: ['academy', 'gothic'], prompt: 'round glasses', clutter: 1 },
    { id: 'half', name: '하프림', tags: ['academy', 'cyber'], prompt: 'half-rim glasses', clutter: 1 },
    { id: 'visor', name: '사이버 바이저', tags: ['cyber'], prompt: 'translucent cyber visor', clutter: 2 },
  ];

  C.cheekFx = [
    { id: 'none', name: '없음', tags: [], prompt: '', clutter: 0 },
    { id: 'sweat', name: '땀방울', tags: ['academy', 'resort'], prompt: 'sweat drop', clutter: 1 },
    { id: 'mole', name: '눈밑 점', tags: ['gothic', 'cyber'], prompt: 'beauty mole under eye', clutter: 0 },
    { id: 'band', name: '볼 반창고', tags: ['academy', 'cyber'], prompt: 'bandage on cheek', clutter: 1 },
    { id: 'hatch', name: '홍조 빗금', tags: ['academy', 'fantasy'], prompt: 'hatched blush lines', clutter: 0 },
  ];

  C.aura = [
    { id: 'none', name: '없음', tags: [], prompt: '', clutter: 0 },
    { id: 'bokeh', name: '보케 빛망울', tags: ['academy', 'resort'], prompt: 'bokeh light particles', clutter: 1 },
    { id: 'sakura', name: '벚꽃잎', tags: ['academy', 'fantasy'], prompt: 'floating cherry blossom petals', clutter: 1 },
    { id: 'neon', name: '네온 그리드', tags: ['cyber'], prompt: 'neon grid light', clutter: 1 },
    { id: 'magic', name: '마법진', tags: ['fantasy', 'gothic'], prompt: 'glowing magic circle', clutter: 1 },
    { id: 'speed', name: '스피드 라인', tags: ['cyber', 'gothic'], prompt: 'manga speed lines', clutter: 1 },
  ];

  C.bg = [
    { id: 'classroom', name: '교실 창가', tags: ['academy'], prompt: 'classroom with sunlit windows' },
    { id: 'forest', name: '요정의 숲', tags: ['fantasy'], prompt: 'enchanted forest bokeh' },
    { id: 'neon_city', name: '네온 시티', tags: ['cyber'], prompt: 'neon cyberpunk city at night' },
    { id: 'sunset_sea', name: '노을 바다', tags: ['resort'], prompt: 'tropical sunset ocean' },
    { id: 'gothic_hall', name: '고딕 성당', tags: ['gothic'], prompt: 'dark gothic cathedral' },
    { id: 'bokeh_soft', name: '소프트 보케', tags: ['academy', 'resort', 'fantasy'], prompt: 'soft gradient bokeh backdrop' },
  ];

  /* ---------------- 팔레트 후보 ---------------- */
  const HAIR_COLORS = {
    all: ['#2b1a16', '#4a2c1d', '#7b4a2a', '#a9703c', '#d9a55f', '#f0d9a8', '#8d3f3f', '#c05a72', '#5b6ea8', '#7b5ea7', '#3e3f4a', '#c9c3bd'],
    academy: ['#2b1a16', '#43301f', '#6b4226', '#a06a34', '#d5a75f', '#efd6a4', '#7e3346', '#c76b83', '#3f4a6b'],
    fantasy: ['#e8e2f5', '#cfd9ef', '#9fb4d8', '#b48fd8', '#7fd8c8', '#f0c3d8', '#d8c07f', '#5f7fa8', '#40355e'],
    cyber: ['#f0f2f7', '#c9d4e8', '#ff6fc0', '#63e6ff', '#a98bff', '#22252e', '#5a2f7a', '#8ef5c8'],
    resort: ['#e6c27a', '#c98f4e', '#8a5a33', '#f2dcae', '#e08a72', '#6d4a7a', '#3d5a6b'],
    gothic: ['#14121a', '#221a28', '#3a2230', '#5b2a3c', '#7d3a4a', '#b9b2c4', '#8f8aa0', '#2a3350'],
  };
  const OUTFIT_COLORS = {
    all: ['#1f2b45', '#2d3b52', '#5a3a4a', '#8a4a5c', '#efe6d8', '#d8dde8', '#3d5a4a', '#6b4a2a', '#2a2a33'],
    academy: ['#1f2b45', '#233a5c', '#3b3f4a', '#efe6d8', '#c94a63', '#2d5a4a'],
    fantasy: ['#3a2f5e', '#2f4a5e', '#6a4a8a', '#e8e2f5', '#2f5e4a', '#8a6a3a'],
    cyber: ['#1a1c26', '#2a2440', '#3d2a5e', '#0f2233', '#e8eef7', '#5a2a4a'],
    resort: ['#f2e6d0', '#e8a86b', '#6bb0c9', '#f0f0f0', '#d96b6b', '#7fa86b'],
    gothic: ['#1a1420', '#2a1a26', '#3d1f2a', '#0f0d14', '#4a2a4a', '#5a4a6a'],
  };

  function wpick(rng, list, world) {
    const ws = list.map((it) => (it.w || 1) * (world && world !== 'all' && it.tags && it.tags.indexOf(world) >= 0 ? 2.8 : 1));
    let tot = 0;
    for (const w of ws) tot += w;
    let r = rng() * tot;
    for (let i = 0; i < list.length; i++) {
      r -= ws[i];
      if (r <= 0) return list[i];
    }
    return list[list.length - 1];
  }
  const byId = (cat, id) => C[cat].find((x) => x.id === id) || C[cat][0];
  AE.byId = byId;

  function pickColor(rng, table, world) {
    const pool = table[world && table[world] ? world : 'all'];
    return pool[Math.floor(rng() * pool.length) % pool.length];
  }

  /* ---------------- 슬롯 정의 (UI/잠금/레시피 공통) ---------------- */
  AE.SLOTS = [
    { key: 'faceShape', label: '얼굴 윤곽', cat: 'faceShape', group: '골격' },
    { key: 'skinTone', label: '피부', cat: 'skinTone', group: '골격' },
    { key: 'bodyType', label: '체형 프리셋', cat: 'bodyType', group: '골격' },
    { key: 'height', label: '키', cat: 'height', group: '골격' },
    { key: 'bust', label: '가슴', cat: 'bust', group: '골격' },
    { key: 'hip', label: '엉덩이', cat: 'hip', group: '골격' },
    { key: 'build', label: '덩치', cat: 'build', group: '골격' },
    { key: 'pose', label: '자세', cat: 'pose', group: '골격' },

    { key: 'eyeShape', label: '눈 모양', cat: 'eyeShape', group: '얼굴' },
    { key: 'iris', label: '눈 색', color: true, group: '얼굴' },
    { key: 'irisFx', label: '홍채 무늬', cat: 'irisFx', group: '얼굴' },
    { key: 'eyebrow', label: '눈썹', cat: 'eyebrow', group: '얼굴' },
    { key: 'expression', label: '표정', cat: 'expression', group: '얼굴' },
    { key: 'nose', label: '코', cat: 'nose', group: '얼굴' },
    { key: 'mouth', label: '입', cat: 'mouth', group: '얼굴' },
    { key: 'cheekFx', label: '볼 FX', cat: 'cheekFx', group: '얼굴' },

    { key: 'ears', label: '귀', cat: 'ears', group: '귀/머리' },
    { key: 'hairBack', label: '뒤머리', cat: 'hairBack', group: '귀/머리' },
    { key: 'hairSide', label: '옆머리', cat: 'hairSide', group: '귀/머리' },
    { key: 'hairFront', label: '앞머리', cat: 'hairFront', group: '귀/머리' },
    { key: 'ahoge', label: '삐침', cat: 'ahoge', group: '귀/머리' },
    { key: 'hairColor', label: '머리색', color: true, group: '귀/머리' },
    { key: 'twoTone', label: '투톤 헤어', toggle: true, group: '귀/머리' },

    { key: 'outfit', label: '의상', cat: 'outfit', group: '의상' },
    { key: 'outfitColor', label: '의상색', color: true, group: '의상' },
    { key: 'headAcc', label: '헤어악세', cat: 'headAcc', group: '의상' },
    { key: 'glasses', label: '안경', cat: 'glasses', group: '의상' },

    { key: 'aura', label: '오라', cat: 'aura', group: '연출' },
    { key: 'bg', label: '배경', cat: 'bg', group: '연출' },
    { key: 'oddEye', label: '오드아이', toggle: true, group: '연출' },
  ];

  /* ---------------- 생성 ---------------- */
  AE.generate = function (opts) {
    const seed = opts.seed >>> 0;
    const world = opts.world || 'all';
    const locks = opts.locks || {};
    const prev = opts.prev || null;
    const rng = AE.mulberry32(seed);
    const rules = [];
    const p = {};
    const L = (k, t) => rules.push({ k, t });

    const take = (key, cat) => {
      const it = wpick(rng, C[cat], world);
      p[key] = locks[key] && prev && prev.parts[key] !== undefined ? prev.parts[key] : it.id;
      return byId(cat, p[key]);
    };
    const takeColorHue = (key, hex) => {
      if (locks[key] && prev) return prev.parts[key];
      return hex;
    };

    /* 골격 */
    take('faceShape', 'faceShape');
    take('skinTone', 'skinTone');
    take('bodyType', 'bodyType');
    take('height', 'height');
    take('bust', 'bust');
    take('hip', 'hip');
    take('build', 'build');
    take('pose', 'pose');

    /* 체형 프리셋 → 키/가슴/힙/덩치 정합 (잠금 슬롯은 보존) */
    const coerce = (key, val, why) => {
      if (locks[key] && prev) return;
      if (p[key] !== val) {
        L('harmony', `체형 정합: ${byId('bodyType', p.bodyType).name} → ${why} (${byId(key, p[key]).name} → ${byId(key, val).name})`);
        p[key] = val;
      }
    };
    if (p.bodyType === 'slim') {
      coerce('build', 'slender', '덩치 슬림화');
      if (['large', 'glamour'].indexOf(p.bust) >= 0) coerce('bust', 'flat', '가슴 축소');
      if (['wide', 'full'].indexOf(p.hip) >= 0) coerce('hip', 'slim', '힙 축소');
    } else if (p.bodyType === 'glamour') {
      if (['flat', 'medium'].indexOf(p.bust) >= 0) coerce('bust', rng() < 0.5 ? 'large' : 'glamour', '가슴 볼륨업');
      if (['slim', 'normal'].indexOf(p.hip) >= 0) coerce('hip', rng() < 0.5 ? 'wide' : 'full', '힙 라인 강조');
      coerce('build', 'standard', '덩치 표준화');
    } else if (p.bodyType === 'athletic') {
      coerce('build', 'athletic', '탄탄한 골격');
      coerce('height', 'standard', '표준 신장');
      if (p.bust === 'glamour') coerce('bust', 'medium', '가슴 정돈');
    } else {
      coerce('build', 'standard', '표준 골격');
    }

    /* 얼굴 파츠 */
    take('eyeShape', 'eyeShape');
    take('irisFx', 'irisFx');
    take('eyebrow', 'eyebrow');
    take('expression', 'expression');
    take('nose', 'nose');
    take('mouth', 'mouth');
    take('cheekFx', 'cheekFx');

    /* 귀 · 헤어 구조 */
    take('ears', 'ears');
    take('hairBack', 'hairBack');
    take('hairSide', 'hairSide');
    take('hairFront', 'hairFront');
    take('ahoge', 'ahoge');

    /* 의상 */
    const outfitIt = take('outfit', 'outfit');
    let hoodUp = false;
    if (outfitIt.id === 'hoodie') {
      hoodUp = locks.hoodUp !== undefined && prev ? !!prev.parts.hoodUp : rng() < 0.42;
    }
    p.hoodUp = hoodUp;

    /* 팔레트 60:30:10 */
    let outfitColor = takeColorHue('outfitColor', pickColor(rng, OUTFIT_COLORS, world));
    let hairColor = takeColorHue('hairColor', pickColor(rng, HAIR_COLORS, world));

    const oHue = AE.hue(outfitColor), hHue0 = AE.hue(hairColor);
    let hHue = hHue0;
    const dist = AE.hueDist(oHue, hHue0);
    if (dist < 14) {
      hHue = hHue0 + 26;
      L('harmony', `색조화: 머리↔의상 색상각 ${dist.toFixed(0)}° → ${AE.hueDist(oHue, hHue).toFixed(0)}° (동색 붙음 완화)`);
    } else if (dist > 55 && dist < 115) {
      hHue = hHue0 + (oHue - hHue0) * 0.34;
      L('harmony', `색조화: 머리↔의상 색상각 ${dist.toFixed(0)}° → ${AE.hueDist(oHue, hHue).toFixed(0)}° (중간색 충돌 회피)`);
    }
    const hslH = rgb2hsl.apply(null, hex2rgb(hairColor));
    let hLight = hslH[2];
    hairColor = hsl2hex(hHue, hslH[1], hLight);

    const skinBase = byId('skinTone', p.skinTone).color;
    const sL = AE.lum(skinBase);
    let dL = Math.abs(sL - AE.lum(hairColor));
    if (dL < 24) {
      hLight = sL > 62 ? Math.max(8, sL - 42) : Math.min(96, sL + 42);
      hairColor = hsl2hex(hHue, hslH[1], hLight);
      dL = Math.abs(sL - AE.lum(hairColor));
      L('contrast', `명도 대비 잠금: 피부 L${sL.toFixed(0)} ↔ 머리 L${AE.lum(hairColor).toFixed(0)} → ΔL ${dL.toFixed(0)}`);
    } else {
      L('contrast', `명도 대비 통과: 피부 L${sL.toFixed(0)} ↔ 머리 L${AE.lum(hairColor).toFixed(0)} (ΔL ${dL.toFixed(0)} ≥ 24)`);
    }

    const irisPool = irisPalette(hairColor, outfitColor, world, rng);
    const iris = takeColorHue('iris', irisPool[0]);
    const oddEye = locks.oddEye !== undefined && prev ? !!prev.parts.oddEye : rng() < 0.17;
    const irisB = oddEye ? AE.shade(iris, 6, 4, 152) : iris;
    const twoTone = locks.twoTone !== undefined && prev ? !!prev.parts.twoTone : rng() < 0.3;
    const hairInner = twoTone ? AE.shade(hairColor, AE.lum(hairColor) > 60 ? -20 : 18, 8, 24) : AE.shade(hairColor, -12, 2);

    /* 의상 대비 보정 — 배경에 묻히지 않게 */
    if (AE.lum(outfitColor) < 26) {
      const lifted = AE.shade(outfitColor, 16, 4);
      L('contrast', `의상 대비 보정: L${AE.lum(outfitColor).toFixed(0)} → L${AE.lum(lifted).toFixed(0)} (배경에 묻힘 방지)`);
      outfitColor = lifted;
    }
    p.outfitColor = outfitColor;
    p.outfitAccent = accentOf(outfitColor, world, rng);
    p.hairColor = hairColor;
    p.hairInner = hairInner;
    p.iris = iris;
    p.irisB = irisB;
    p.twoTone = twoTone;
    p.oddEye = oddEye;

    /* 머리 악세 — 후보 2개 + 충돌 해소 */
    const accA = wpick(rng, C.headAcc, world);
    const accB = wpick(rng, C.headAcc, world);
    const animal = ['cat', 'fox', 'rabbit'].indexOf(p.ears) >= 0;
    let acc = locks.headAcc && prev ? prev.parts.headAcc : accA.id;
    if (!(locks.headAcc && prev)) {
      const big = (x) => ['headphones', 'beret', 'crown'].indexOf(x) >= 0;
      if (big(accA.id) && big(accB.id)) {
        L('exclude', `충돌 해소: ${byId('headAcc', accA.id).name} ↔ ${byId('headAcc', accB.id).name} 동시 후보 → ${byId('headAcc', accA.id).name} 유지`);
      } else {
        L('exclude', `악세 충돌 검사 통과: 후보 ${byId('headAcc', accA.id).name} / ${byId('headAcc', accB.id).name}`);
      }
    }
    if (animal && ['headphones', 'beret', 'crown', 'headband'].indexOf(acc) >= 0) {
      L('exclude', `충돌 해소: 수인귀(${byId('ears', p.ears).name}) ↔ ${byId('headAcc', acc).name} — 귀가 머리 위를 차지해 악세 제거`);
      acc = 'none';
    }
    p.headAcc = acc;

    const gl = wpick(rng, C.glasses, world);
    p.glasses = locks.glasses && prev ? prev.parts.glasses : gl.id;
    if (p.glasses === 'visor' && animal) {
      p.glasses = 'round';
      L('exclude', '충돌 해소: 바이저 ↔ 수인귀 → 바이저를 라운드 안경으로 교체');
    }

    p.aura = locks.aura && prev ? prev.parts.aura : wpick(rng, C.aura, world).id;
    p.bg = locks.bg && prev ? prev.parts.bg : wpick(rng, C.bg, world).id;

    /* 오버라이드: 후드 업 → 뒤머리 은폐 */
    p.hairVisible = true;
    if (p.outfit === 'hoodie' && p.hoodUp) {
      p.hairVisible = false;
      L('override', `override: 후드 업 → 뒤머리(${byId('hairBack', p.hairBack).name}) 은폐`);
    }

    /* 혼잡도 예산 ≤ 6 */
    const items = [
      { key: 'headAcc', pts: byId('headAcc', p.headAcc).clutter, name: byId('headAcc', p.headAcc).name, pr: 3 },
      { key: 'glasses', pts: byId('glasses', p.glasses).clutter, name: byId('glasses', p.glasses).name, pr: 2 },
      { key: 'aura', pts: byId('aura', p.aura).clutter, name: byId('aura', p.aura).name, pr: 1 },
      { key: 'cheekFx', pts: byId('cheekFx', p.cheekFx).clutter, name: byId('cheekFx', p.cheekFx).name, pr: 0 },
    ];
    let budget = items.reduce((a, b) => a + b.pts, 0);
    if (budget > 6) {
      for (const it of items.slice().sort((a, b) => a.pr - b.pr)) {
        if (budget <= 6) break;
        if (!it.pts || it.key === 'cheekFx') continue;
        p[it.key] = it.key === 'glasses' ? 'none' : 'none';
        budget -= it.pts;
        L('budget', `혼잡도 예산 초과: ${it.name}(-${it.pts}) 우선순위 낮아 제거 → 합계 ${budget}/6`);
      }
    }
    p.complexity = items.reduce((a, b) => a + (p[b.key] && p[b.key] !== 'none' ? b.pts : 0), 0);
    L('budget', `혼잡도 ${p.complexity}/6 — 악세서리·이펙트 합계 상한 이내`);

    /* 표정 ↔ 입/눈 정합 보정 */
    if (p.expression === 'grin' && ['flat', 'pout'].indexOf(p.mouth) >= 0) {
      p.mouth = 'open';
      L('exclude', `표정 정합: 활짝 웃음 ↔ ${byId('mouth', 'flat').name} → 벌린 입으로 교체`);
    }
    if (p.expression === 'wink' && p.eyeShape === 'sleepy') {
      p.expression = 'smile';
      L('exclude', '표정 정합: 반쯤 감긴 눈 + 윙크 불가 → 미소로 대체');
    }

    return {
      seed,
      world,
      parts: p,
      rules,
      colors: { skin: skinBase, hair: hairColor, hairInner, iris, irisB, outfit: outfitColor, accent: p.outfitAccent },
      labels: labelsOf(p),
      sig: sigOf(p),
    };
  };

  function irisPalette(hairColor, outfitColor, world, rng) {
    const hH = AE.hue(hairColor), oH = AE.hue(outfitColor);
    const base = [
      hsl2hex(oH + 172 + AE.rnd(rng, -18, 18), AE.rnd(rng, 62, 88), AE.rnd(rng, 40, 56)),
      hsl2hex(hH + AE.rnd(rng, 12, 34), AE.rnd(rng, 55, 80), AE.rnd(rng, 42, 58)),
      hsl2hex(oH + AE.rnd(rng, -26, 26), AE.rnd(rng, 58, 84), AE.rnd(rng, 44, 60)),
    ];
    if (world === 'fantasy') base[0] = hsl2hex(AE.rnd(rng, 0, 360), 70, 52);
    if (world === 'cyber') base[0] = hsl2hex([190, 300, 330][Math.floor(rng() * 3)], 88, 55);
    if (world === 'gothic') base[0] = hsl2hex([280, 340, 20][Math.floor(rng() * 3)], 55, 46);
    return base;
  }
  function accentOf(outfitColor, world, rng) {
    return hsl2hex(AE.hue(outfitColor) + 165 + AE.rnd(rng, -12, 12), 62, 58);
  }

  function labelsOf(p) {
    return {
      faceShape: byId('faceShape', p.faceShape).name,
      skinTone: byId('skinTone', p.skinTone).name,
      bodyType: byId('bodyType', p.bodyType).name,
      height: byId('height', p.height).name,
      bust: byId('bust', p.bust).name,
      hip: byId('hip', p.hip).name,
      build: byId('build', p.build).name,
      pose: byId('pose', p.pose).name,
      eyeShape: byId('eyeShape', p.eyeShape).name,
      iris: AE.colorName(p.iris),
      irisFx: byId('irisFx', p.irisFx).name,
      eyebrow: byId('eyebrow', p.eyebrow).name,
      expression: byId('expression', p.expression).name,
      nose: byId('nose', p.nose).name,
      mouth: byId('mouth', p.mouth).name,
      cheekFx: byId('cheekFx', p.cheekFx).name,
      ears: byId('ears', p.ears).name,
      hairBack: (p.hoodUp ? '후드 업 · ' : '') + byId('hairBack', p.hairBack).name,
      hairSide: byId('hairSide', p.hairSide).name,
      hairFront: byId('hairFront', p.hairFront).name,
      ahoge: byId('ahoge', p.ahoge).name,
      hairColor: AE.colorName(p.hairColor),
      twoTone: p.twoTone ? '투톤 ON' : '단색',
      outfit: byId('outfit', p.outfit).name + (p.hoodUp ? ' (후드 업)' : ''),
      outfitColor: AE.colorName(p.outfitColor),
      headAcc: byId('headAcc', p.headAcc).name,
      glasses: byId('glasses', p.glasses).name,
      aura: byId('aura', p.aura).name,
      bg: byId('bg', p.bg).name,
      oddEye: p.oddEye ? '오드아이 ON' : '오드아이 OFF',
    };
  }

  function sigOf(p) {
    return Object.keys(p).sort().map((k) => k + '=' + p[k]).join('|');
  }
  AE.sig = sigOf;

  /* ---------------- 프롬프트 컴파일 ---------------- */
  const WORLD_PROMPT = {
    academy: 'school setting, warm afternoon light',
    fantasy: 'fantasy setting, ethereal glow',
    cyber: 'cyberpunk setting, neon lighting',
    resort: 'summer resort setting, bright sunlight',
    gothic: 'dark gothic setting, moody lighting',
    all: 'soft studio backdrop',
  };

  AE.compilePrompt = function (ch) {
    const p = ch.parts;
    const t = [];
    t.push('1girl', 'solo', 'upper body portrait');
    t.push('anime style illustration', 'cel shading', 'clean lineart', 'detailed eyes', 'masterpiece', 'best quality', 'high resolution');
    t.push(byId('pose', p.pose).prompt);
    t.push(byId('faceShape', p.faceShape).prompt);
    t.push(byId('skinTone', p.skinTone).prompt);
    const BODY_PROMPT = {
      petite: 'petite, short stature, 6.5 heads tall, compact figure',
      standard: 'average height, 7.2 heads tall',
      tall: 'tall, long legs, 8 heads tall, model proportions',
    };
    const BUST_PROMPT = { flat: 'flat chest', medium: 'medium bust', large: 'large bust', glamour: 'very large bust, glamorous figure' };
    const HIP_PROMPT = { slim: 'slim hips', normal: 'average hips', wide: 'wide hips, hourglass waist', full: 'full curvy hips' };
    const BUILD_PROMPT = { slender: 'slender frame, thin limbs', standard: 'average build', athletic: 'athletic toned build', wide: 'broad sturdy frame' };
    t.push(byId('bodyType', p.bodyType).prompt);
    t.push(BODY_PROMPT[p.height], BUST_PROMPT[p.bust], HIP_PROMPT[p.hip], BUILD_PROMPT[p.build]);
    t.push(byId('ears', p.ears).prompt);
    t.push(byId('eyeShape', p.eyeShape).prompt);
    t.push(`${AE.colorName(p.iris)} eyes`, 'gradient iris', 'specular highlights');
    if (p.irisFx !== 'none') t.push(byId('irisFx', p.irisFx).prompt);
    t.push(byId('eyebrow', p.eyebrow).prompt);
    t.push(byId('expression', p.expression).prompt);
    t.push(byId('nose', p.nose).prompt);
    t.push(byId('mouth', p.mouth).prompt);
    if (byId('cheekFx', p.cheekFx).prompt) t.push(byId('cheekFx', p.cheekFx).prompt);
    t.push(`${AE.colorName(p.hairColor)} hair`);
    t.push(byId('hairBack', p.hairBack).prompt);
    t.push(byId('hairSide', p.hairSide).prompt);
    t.push(byId('hairFront', p.hairFront).prompt);
    if (byId('ahoge', p.ahoge).prompt) t.push(byId('ahoge', p.ahoge).prompt);
    if (p.twoTone) t.push('two-tone hair', `${AE.colorName(p.hairInner)} inner layer`);
    if (p.oddEye) t.push('heterochromia', `one ${AE.colorName(p.irisB)} eye`);
    t.push(`${AE.colorName(p.outfitColor)} ${byId('outfit', p.outfit).prompt}`);
    if (byId('headAcc', p.headAcc).prompt) t.push(byId('headAcc', p.headAcc).prompt);
    if (byId('glasses', p.glasses).prompt) t.push(byId('glasses', p.glasses).prompt);
    if (byId('aura', p.aura).prompt) t.push(byId('aura', p.aura).prompt);
    t.push(byId('bg', p.bg).prompt);
    t.push(WORLD_PROMPT[ch.world] || WORLD_PROMPT.all);
    t.push('depth of field', 'rim light', 'soft shadow on skin');
    return t.filter(Boolean).join(', ') + '.';
  };

  /* ---------------- 배치 9종 ---------------- */
  AE.batch = function (seed, world, count) {
    const out = [], seen = new Set();
    const mixer = AE.mulberry32(seed >>> 0);
    const seeds = [];
    for (let i = 0; i < 400; i++) seeds.push(Math.floor(mixer() * 4294967296) >>> 0);
    let guard = 0;
    while (out.length < count && guard < 400) {
      const ch = AE.generate({ seed: seeds[guard % seeds.length], world, locks: {}, prev: null });
      if (!seen.has(ch.sig)) {
        seen.add(ch.sig);
        out.push(ch);
      }
      guard++;
    }
    return out;
  };

  /* ---------------- 레시피 ---------------- */
  AE.recipe = function (ch) {
    return {
      app: 'anime-character-combinator',
      author: '포시즌jo',
      version: '2.0.0',
      seed: '0x' + (ch.seed >>> 0).toString(16).toUpperCase().padStart(8, '0'),
      seed_dec: ch.seed >>> 0,
      world: ch.world,
      parts: ch.parts,
      colors: ch.colors,
      rules: ch.rules.map((r) => r.t),
      prompt: AE.compilePrompt(ch),
    };
  };
})();
