# 애니 캐릭터 조합 엔진 v2 — 파츠팩 아키텍처 SPEC

> **이 문서의 목적**: Cursor(Composer/Agent)가 이 문서만 읽고 실제 앱을 구현할 수 있게 하는 것.
> **전제**: `engine.js`(순수 로직)는 **이미 완성되어 테스트까지 통과한 상태**로 제공된다. Cursor가 새로 만드는 것은 **그림을 그리는 컴포지터 · UI · 내보내기**뿐이다.
> **작성/소유**: 제작: 포시즌jo · schema `anime-parts-pack/2` · engine v2.0.0

---

## 0. 이 키트가 푸는 문제

v1은 캐릭터를 **벡터 코드로 직접 그렸다**(`draw-core.js` 등). 그래서
- 눈·머리 모양을 추가할 때마다 드로잉 코드를 새로 짜야 했고,
- 결과물이 "코드로 그린 그림"의 한계(선 굵기·명암·질감)를 벗어나지 못했다.

**v2는 조합과 그림을 분리한다.**
- `engine.js` = 30개 슬롯을 시드로 뽑고, 규칙으로 충돌을 풀고, **"무엇을 어떤 순서로 어떤 색으로 얹을지"** 만 계산한다.
- 실제 그림은 **미리 그려 둔 파츠 이미지(WebP/PNG)** 이고, `compositor.js` 가 **Canvas2D 레이어 합성**만 한다.

> 결과적으로 **그림 품질 = 파츠 품질**이 되고, 엔진은 파츠를 갈아끼우는 것만으로 100종이 아니라 수십만 조합을 만든다.

### 파일 지도

| 파일 | 역할 | 상태 |
|---|---|---|
| `engine.js` | 순수 로직 (상태·PRNG·규칙·색·레이어 계산·프롬프트·JSON) | **완성 · 테스트 56개 통과** |
| `manifest.example.json` | 파츠팩 매니페스트 예시 (30슬롯 · 127파츠) | **완성 · 스키마 검증 통과** |
| `test/engine.test.mjs` | 결정론·잠금·충돌·레이어 출력 검증 | **완성** |
| `SPEC.md` | 이 문서 — 구현 계약 | 완성 |
| `PARTS_PACK_GUIDE.md` | 파츠를 어떻게 그려서 어떤 규격으로 넣는가 | 완성 |
| `CURSOR_PROMPT.md` | Cursor에 그대로 붙여넣는 구현 지시문 | 완성 |
| `compositor.js` | Canvas2D 레이어 합성 | **Cursor가 구현** |
| `index.html` · `app.css` · `app.js` | UI (슬롯 잠금 · 9종 배치 · 업로더 · 내보내기) | **Cursor가 구현** |
| `assets/parts/**` | 실제 파츠 이미지 | 사용자가 채움 (없으면 자리표시자) |

---

## 1. 아키텍처

```
        ┌──────────────────────────┐
        │  parts pack (manifest)   │  ← 파츠 목록·태그·가중치·파일경로·anchor
        │  manifest.json           │
        └────────────┬─────────────┘
                     │ 읽기 전용
   seed ──┐          ▼
   world ─┼──▶ ┌──────────────────┐    recipe (JSON)     ┌────────────────────┐
   locks ─┤    │   engine.js      │ ───────────────────▶ │  compositor.js     │
   pins ──┘    │  (순수 로직, DOM X)│   30 parts + colors  │  Canvas2D 합성      │
               └──────────────────┘   + transform + log   └─────────┬──────────┘
                     │                                             │
                     │ compilePrompt() / exportJSON()               ▼
                     ▼                                     ┌────────────────┐
               AI 프롬프트 · 레시피 파일                     │  UI (app.js)   │
                                                           │ 슬롯 잠금·리롤   │
                                                           │ 9종 배치·업로드  │
                                                           │ PNG/JSON 내보내기│
                                                           └────────────────┘
```

**경계 규칙 (반드시 지킬 것)**

1. `engine.js` 는 **DOM·Canvas·window·localStorage 를 절대 만지지 않는다.** Node에서도 돌아야 한다.
2. `compositor.js` 는 **그림만 그린다.** 슬롯 선택·규칙·색 계산을 다시 구현하면 안 된다 — `buildRenderLayers()` 결과를 그대로 소비한다.
3. `app.js` 는 상태(시드·세계관·잠금)를 들고 **engine 호출 → compositor 호출**만 한다.
4. 파츠를 추가하는 일은 **manifest + 이미지 파일**만으로 끝나야 한다. 코드 수정은 금지(단, 새 슬롯 자체를 만드는 경우 제외).

### 로드 순서

```html
<script type="module">
  import { generate, buildRenderLayers, compilePrompt, exportJSON,
           reroll, batch, toggleLock, encodeHash, decodeHash, SLOTS, GROUPS } from './engine.js';
  import { Compositor } from './compositor.js';
  const manifest = await fetch('./manifest.example.json').then(r => r.json());
  ...
</script>
```

> `package.json` 에 `"type": "module"` 이 들어 있다. engine.js는 ESM 전용이다.

---

## 2. 슬롯 30개 — 전부 독립 잠금 · 독립 리롤

`SLOTS` (engine.js)가 단일 진실. UI는 이 배열을 **그대로** 그린다(하드코딩 금지).

| # | key | 라벨 | 그룹 | 종류 | 그리는 레이어 |
|---|---|---|---|---|---|
| 1 | `face_shape` | 얼굴 윤곽 | frame | part | face_base |
| 2 | `skin_tone` | 피부 톤 | frame | color | body_base, face_base, ears_back, ears_front |
| 3 | `body_type` | 체형 프리셋 | frame | part | body_base |
| 4 | `height` | 키·등신 | frame | part | *(변형만)* |
| 5 | `bust` | 가슴 | frame | part | *(변형만)* |
| 6 | `hip` | 골반 | frame | part | *(변형만)* |
| 7 | `build` | 덩치 | frame | part | *(변형만)* |
| 8 | `pose` | 자세 | frame | part | body_base(팔) |
| 9 | `eyes_shape` | 눈 모양 | face | part | eyes |
| 10 | `eye_color` | 눈 색 | face | color | eyes |
| 11 | `iris_fx` | 홍채 무늬 | face | part | eyes |
| 12 | `eyebrow` | 눈썹 | face | part | eyebrow |
| 13 | `expression` | 표정 | face | part | face_base(+30) 오버레이, mouth·eyebrow·cheek_fx *(소유 시)* |
| 14 | `nose` | 코 | face | part | nose |
| 15 | `mouth` | 입 | face | part | mouth |
| 16 | `cheek_fx` | 볼 이펙트 | face | part | cheek_fx |
| 17 | `heterochromia` | 오드아이 | face | flag | eyes |
| 18 | `ears` | 귀 | hair | part | ears_back, ears_front |
| 19 | `hair_back` | 뒤머리 | hair | part | hair_back |
| 20 | `hair_side` | 옆머리 | hair | part | hair_side |
| 21 | `hair_front` | 앞머리 | hair | part | hair_front |
| 22 | `ahoge` | 삐침 | hair | part | ahoge |
| 23 | `hair_color` | 머리색 | hair | color | hair_back, hair_side, hair_front, ahoge |
| 24 | `hair_two_tone` | 투톤 헤어 | hair | flag | hair_back(안쪽) |
| 25 | `outfit` | 의상 | style | part | outfit |
| 26 | `outfit_color` | 의상색 | style | color | outfit |
| 27 | `head_acc` | 머리 악세 | style | part | head_acc |
| 28 | `glasses` | 안경 | style | part | glasses |
| 29 | `aura` | 오라 | style | part | aura_back, aura_front |
| 30 | `background` | 배경 | style | part | background |

### 값 형식

| kind | `recipe.parts[key]` | UI 표시 | 잠금 |
|---|---|---|---|
| `part` | 파츠 id 문자열 (`"hime"`) 또는 `"none"` | `recipe.names[key]` (한글 이름) | 값 유지 |
| `color` | hex (`"#c2384a"`) | `colorName()` + 스와치 | hex 유지(색 조화도 양보) |
| `flag` | `true` / `false` | ON/OFF | 값 유지 |

### 세계관(world)

`all · academy · fantasy · cyber · resort · gothic`. 파츠/색의 `tags` 에 세계관이 있으면 가중치 ×2.8(`manifest.fx.tag_boost`).
`world` 는 슬롯이 아니라 **가중치 필터**다(잠금 대상 아님).

### 잠금 / 고정 / 리롤 의미

| 개념 | API | 의미 |
|---|---|---|
| 잠금 | `locks: {eyes_shape: true}` | 리롤해도 **그 슬롯 값 유지**. 규칙도 이 슬롯을 바꾸지 못한다(로그 `blocked`). |
| 고정 | `pins: {hair_front: 'hime'}` | 시드와 무관하게 **그 값으로 강제**. 업로드한 커스텀 파츠·공유 URL 재현에 사용. |
| 전체 리롤 | `reroll(recipe, {manifest})` | 새 시드 파생 → 잠금 제외 전부 새로 뽑기 |
| 부분 리롤 | `reroll(recipe, {manifest, only:['hair_front']})` | 지정 슬롯만 새로 뽑기(나머지는 값 유지) |
| 재현 | `generate({seed, world, locks, pins})` | 같은 입력 → 항상 같은 캐릭터 |

**결정론 보장 방식**: 슬롯마다 `rngFor(seed, slotKey)` 로 **독립 난수 스트림**을 쓴다. 그래서 한 슬롯을 리롤해도 다른 슬롯이 밀리지 않는다. (v1의 전역 순차 RNG와 다른 점)

---

## 3. 레이어 스택 — Z 순서 20단

아래에서 위로. `LAYER_Z[layer] = (index+1) * 100`. 같은 레이어 내부 세부 순서는 `sub`(0~99)로 더한다.

| z | 레이어 | 내용 | 소유 슬롯 |
|---|---|---|---|
| 100 | `background` | 배경 일러스트 (불투명) | background |
| 200 | `aura_back` | 오라 뒤판(빛망울·꽃잎 등) | aura |
| 300 | `hair_back` | 뒤머리 (투톤은 +5에 안쪽 레이어) | hair_back, hair_color |
| 400 | `body_base` | 몸 실루엣(피부) · 자세 팔은 +20 | body_type, pose, skin_tone |
| 500 | `ears_back` | 수인귀 뒤판 · 엘프귀 뒤쪽 | ears |
| 600 | `face_base` | 얼굴 윤곽(피부 + 라인) · 표정 오버레이는 +30 | face_shape, expression |
| 700 | `cheek_fx` | 홍조·볼 이펙트 | cheek_fx, expression(소유 시) |
| 800 | `eyes` | 흰자+홍채 (+5 오드아이, +10 무늬) | eyes_shape, eye_color, iris_fx, heterochromia |
| 900 | `nose` | 코 | nose |
| 1000 | `mouth` | 입 (+20 상위 표정이 그리는 입) | mouth, expression |
| 1100 | `eyebrow` | 눈썹 | eyebrow |
| 1200 | `outfit` | 의상 (+5 악센트) | outfit, outfit_color |
| 1300 | `hair_side` | 옆머리 | hair_side |
| 1400 | `ears_front` | 수인귀 앞판 | ears |
| 1500 | `hair_front` | 앞머리 | hair_front |
| 1600 | `ahoge` | 삐침 | ahoge |
| 1700 | `glasses` | 안경·바이저 | glasses |
| 1800 | `head_acc` | 머리 악세서리 | head_acc |
| 1900 | `aura_front` | 오라 앞판(캐릭터를 스치는 빛) | aura |
| 2000 | `watermark` | 제작 표기(텍스트) | engine |

### sub 규약 (`engine.js`의 `SUB`)

| sub | 이름 | 쓰는 곳 |
|---|---|---|
| 0 | `BASE` | 레이어 기본 그림 |
| 5 | `ODD_EYE` / `TWO_TONE` / `ACCENT` | 오드아이 오른눈 · 투톤 안쪽 · 의상 악센트 |
| 10 | `FX` | 홍채 무늬 오버레이 |
| 20 | `OWNED` | 상위 파츠가 소유한 하위 레이어(표정이 그리는 입 등) |
| 30 | `EXPRESSION` | 표정 오버레이(홍조·눈물·땀) — `face_base` 위, 눈 아래 |

> **왜 이 순서인가**: 턱·목이 의상 깃에 가려지고(400 < 1200), 옆머리가 어깨 위로 떨어지고(1200 < 1300), 앞머리가 눈썹 위에 얹히고(1100 < 1500), 안경·악세가 머리카락 위에 온다(1500 < 1700 < 1800). `watermark` 는 항상 마지막.

---

## 4. 렌더 파이프라인

### 4.1 `buildRenderLayers(recipe, manifest)` → 레이어 배열

정렬까지 끝난 배열을 돌려준다. 컴포지터는 **앞에서부터 그리기만** 하면 된다.

```js
[
  {
    layer: 'hair_front',        // 레이어 이름
    z: 1500,                    // 정렬 키 (작을수록 뒤)
    slot: 'hair_front',         // 이 레이어를 만든 슬롯
    id: 'hime',                 // 파츠 id
    src: 'assets/parts/hair_front/hime.webp',  // null 이면 자리표시자
    tint: '#4a2c1d',            // 색 입히기 (null = 원본 색)
    tintMode: 'multiply',       // multiply | soft-light | none | screen
    tintStrength: 1,            // 0~1
    clip: null,                 // 'right_eye' | 'left_eye' | null
    opacity: 1,
    visible: true,
    hiddenReason: null,         // visible:false 일 때 이유(한글)
    transform: {
      x: 0, y: 0,               // 리그 기준 픽셀 이동
      scaleX: 1, scaleY: 1,     // 스케일
      rot: 0,                   // 도(°), 피벗 기준 회전
      pivot: [512, 322],        // 회전·스케일 기준점 (머리=중심, 몸=힙)
      bands: { bust: 1.0, hip: 1.0, waist: 1.0 }  // 몸 레이어만: 밴드별 가로 배율
    }
  },
  ...
]
```

**컴포지터가 지켜야 할 것**

1. `visible === false` 인 항목은 **그리지 않는다**(디버그 모드에서만 반투명 표시).
2. `src === null` 이면 **자리표시자**를 그린다: 레이어 색 반투명 사각형 + `slot` 텍스트. (파츠가 하나도 없어도 앱이 돌아가야 한다)
3. `transform.pivot` 기준으로 `scale → rotate → translate` 순서로 적용한다. (`ctx.translate(px+x, py+y); ctx.rotate(rot); ctx.scale(sx,sy); ctx.translate(-px,-py);`)
4. `bands` 는 **몸통 3구간 슬라이스 스케일**이다: `body_base`·`outfit` 을 세로 밴드로 잘라 `bust`/`waist`/`hip` 구간만 가로로 늘린다. (상세: 4.4)
5. `watermark` 는 `kind:'text'` 이고 `text` 를 그린다. `recipe.watermark === false` 면 항목 자체가 없다.

### 4.2 틴트(색 입히기) — 파츠는 회색으로 그린다

파츠는 **중성 회색(#C8C8C8 계열)으로 그려 두고**, 컴포지터가 색을 곱한다. 선화(#1A1A22)와 하이라이트가 살아남는다.

```js
// tintMode 'multiply' (피부·머리·의상·눈동자 기본)
function drawTinted(ctx, img, layer) {
  const c = layer.tint || '#ffffff';
  // 1) 임시 캔버스에 파츠를 그리고
  tmp.clearRect(0, 0, W, H);
  tmp.globalCompositeOperation = 'source-over';
  tmp.drawImage(img, 0, 0, W, H);
  // 2) 색을 source-in 으로 채워 실루엣만 남기고
  tmp.globalCompositeOperation = 'source-in';
  tmp.fillStyle = c;
  tmp.fillRect(0, 0, W, H);
  // 3) 원본 위에 multiply 로 얹고
  ctx.globalCompositeOperation = 'multiply';
  ctx.globalAlpha = layer.tintStrength;
  ctx.drawImage(tmp, 0, 0, W, H);
  // 4) 원본을 'lighter' 0.18 로 한 번 더 얹어 하이라이트를 되살린다
  ctx.globalCompositeOperation = 'lighter';
  ctx.globalAlpha = 0.18 * layer.tintStrength;
  ctx.drawImage(img, 0, 0, W, H);
  ctx.globalCompositeOperation = 'source-over';
  ctx.globalAlpha = 1;
}
```

| tintMode | 쓰는 곳 | 구현 |
|---|---|---|
| `multiply` | 피부·머리·의상·눈동자 | 위 코드 |
| `screen` | 홍채 무늬(별·하트 등 발광) | `globalCompositeOperation='screen'` + tint |
| `soft-light` | (확장용) 은은한 색 | `soft-light` |
| `none` | 선화 파츠·안경·배경 | 원본 그대로 |

> 파츠 제작 규격(회색 단계, 선 색, 하이라이트)은 **`PARTS_PACK_GUIDE.md` 3장**. 파츠가 규격을 지키면 틴트가 "탁해지지" 않는다.

### 4.3 캔버스

| 항목 | 값 |
|---|---|
| 마스터 해상도 | **1024 × 1280** (세로 4:5). 표시용은 CSS 로 축소, 내보내기는 ×2(2048×2560) |
| alpha | true (배경 투명 PNG 내보내기 가능) |
| 파츠 원본 | 1024×1280 캔버스에 **정렬된 상태로** 그린 WebP(알파). 리사이즈 금지 |
| 리그 앵커 | `manifest.rig` (머리 중심 512,322 · 눈높이 388 · 턱 486 · 힙 512,872) |

### 4.4 키·덩치·가슴·골반 = 밴드 스케일

파츠를 다시 그리지 않고 **몸통만 구간별로 늘린다.**

```
밴드 경계 (manifest.rig.bands)
  bust  560 ~ 700     ← 가슴 구간, bands.bust 배율 (0.94 ~ 1.15)
  waist 700 ~ 780     ← 허리(고정)
  hip   780 ~ 920     ← 골반 구간, bands.hip 배율 (0.95 ~ 1.14)
```

- `body_base`·`outfit` 레이어만 적용. 머리·얼굴 레이어는 `transform.scale`(height 파츠)로만 변한다.
- 구현: 밴드마다 `drawImage(src, 0,y0,W,h, 0,y0, W*hScale,h)` 로 가로만 늘리고, 경계 8px는 선형 블렌드(이음새 방지).
- 세로는 `height` 파츠의 `body.sy`(0.94 ~ 1.055)를 힙 피벗으로 적용한다.

---

## 5. 규칙 엔진 — 충돌 / 오버라이드 / 혼잡도

### 5.1 우선순위

```
  사용자 고정(pins) · 잠금(locks)   ← 항상 이긴다
        ▲
        │  (값이 바뀌지 않고 로그에 blocked)
  값 교정 규칙 (CX, 최대 4패스 반복 → 수렴)
        ▲
  은폐 규칙 (OV — 잠금과 무관하게 화면에서 가림)
        ▼
  혼잡도 예산 (CB — 잠긴 항목은 못 지운다)
```

### 5.2 교정 규칙 매트릭스 (`RULES`, engine.js)

| ID | 조건 | 결과 | 이유 |
|---|---|---|---|
| CX01 | `body_type=slim` ∧ `bust∈{large,glamour}` | bust → flat | 슬림 실루엣에 큰 가슴은 어색 |
| CX02 | `body_type=slim` ∧ `hip∈{wide,full}` | hip → slim | 허리-골반 라인 붕괴 |
| CX03 | `body_type=slim` ∧ `build∈{wide,athletic}` | build → slender | 어깨 폭 불일치 |
| CX04 | `body_type=glamour` ∧ `bust=flat` | bust → large | 볼륨 프리셋 유지 |
| CX05 | `body_type=glamour` ∧ `hip=slim` | hip → wide | 모래시계 유지 |
| CX06 | `body_type=athletic` ∧ `bust=glamour` | bust → medium | 스포티 실루엣 |
| CX07 | `body_type=athletic` ∧ `build=slender` | build → athletic | 어깨-몸통 불일치 |
| CX08 | `height=petite` ∧ `body_type=glamour` | body_type → standard | 6.5등신에 글래머 = 비율 붕괴 |
| CX09 | `height=petite` ∧ `build=wide` | build → standard | 아담+듬직 불일치 |
| CX10 | `ears∈{cat,fox,rabbit}` ∧ `head_acc∈{headphones,beret,crown,headband}` | head_acc → none | 수인귀가 정수리를 차지 |
| CX11 | `ears∈{cat,fox,rabbit}` ∧ `glasses=visor` | glasses → round | 바이저+수인귀 겹침 |
| CX12 | `hair_back∈{bob,short}` ∧ `head_acc=headphones` | head_acc → none | 머리 볼륨이 없어 밴드가 뜸 |
| CX13 | `eyes_shape=sleepy` ∧ `expression=wink` | expression → smile | 반쯤 감긴 눈은 윙크 불가 |
| CX14 | `expression=grin` ∧ `mouth∈{flat,pout}` | mouth → open | 표정-입 불일치 |
| CX15 | `ahoge=double` ∧ `head_acc∈{headphones,beret,crown}` | ahoge → single | 모자와 쌍삐침 충돌 |
| CX16 | `bust∈{large,glamour}` ∧ `hip=slim` | hip → normal | 실루엣 균형 |

### 5.3 오버라이드 (OV) — 그림을 가리는 규칙

| ID | 트리거 | 동작 |
|---|---|---|
| OV01 | 파츠가 `owns:[...]` 를 선언 (예: 표정 `grin` 이 `mouth` 를 소유) | 하위 슬롯 레이어를 `visible:false` 로. 상위 파츠 그림이 그 레이어 `+20` 에 그려진다 |
| OV02 | 파츠가 `hides:[...]` 를 선언 (예: `visor` → `eyebrow`, `crown`/`beret` → `ahoge`) | 해당 슬롯 레이어 `visible:false` |

- **은폐는 잠금과 무관하게 적용**된다. 잠근 슬롯이 가려지면 로그에 `blocked` 로 알린다(값은 유지, 화면에서만 숨김).
- 은폐된 레이어도 **배열에는 남는다**(정렬 유지, `visible:false`). UI가 "이 슬롯은 지금 가려짐"을 보여줄 수 있어야 한다.

### 5.4 혼잡도 예산 (CB) — 악세·이펙트 합계 ≤ 6

| 슬롯 | 점수 | 비고 |
|---|---|---|
| `head_acc` | 파츠별 0~3 | 리본1 · 밴드1 · 핀1 · 헤드폰2 · 베레2 · 티아라3 |
| `glasses` | 0~2 | 라운드1 · 하프1 · 바이저2 |
| `aura` | 0~1 | 파츠별 |
| `cheek_fx` | 0~1 | 파츠별 |

초과하면 **낮은 우선순위부터** `none` 으로 제거: `cheek_fx → aura → glasses → head_acc`.
잠긴 항목은 제거하지 않고 `{over: n}` 으로 초과 상태를 보고한다(UI는 "악세를 줄이세요" 안내).
결과는 `recipe.clutter = {total, max, over, items[]}`.

### 5.5 로그 스키마 (`recipe.log[]`)

```js
{ id: 'CX01', type: 'coerce', slot: 'bust',
  text: '체형 정합: 슬림 ↔ 글래머 볼륨 → 가슴 축소' }
```

| type | 의미 | UI |
|---|---|---|
| `coerce` | 값이 교정됨 | "고친 것" 목록 |
| `override` | 레이어가 가려짐 | "가린 것" 목록 |
| `budget` | 혼잡도로 제거됨 | "줄인 것" |
| `harmony` / `contrast` | 색 조화·명도 보정 | "색 보정" |
| `blocked` | 잠금 때문에 규칙 보류 | **경고 배지** |
| `lock` / `PIN` | 잠금·고정 유지 | 접어서 표시 |

UI는 `log` 를 **그대로** 렌더링한다(문구를 새로 만들지 않는다).

---

## 6. 색 — 60:30:10 · 명도 잠금

피부는 면적이 크지만 중립(고정)이므로 비율 계산에서 제외한다.

| 비율 | 대상 | 슬롯 |
|---|---|---|
| **60% 주조색** | 의상 | `outfit_color` |
| **30% 보조색** | 머리 | `hair_color` |
| **10% 강조색** | 눈동자 + 의상 악센트 | `eye_color`, `colors.outfit_accent` |

| ID | 규칙 | 동작 |
|---|---|---|
| CH01 | 머리↔의상 색상각 < 14° | **원래 방향으로 30°** 벌린다 (방향을 지켜야 다시 붙지 않는다) |
| CH02 | 색상각 55~115°(중간색) | 부호 유지한 채 118° 로 밀어 탁한 중간색 회피 |
| CH03 | 피부↔머리 명도차 **ΔL < 24** | 피부가 밝으면 머리를 −42, 어두우면 +42 (8~96 클램프) |
| CH04 | 의상 L < 26 | +16 밝혀 배경에 묻히지 않게 |
| CH05 | 눈동자가 머리·의상과 20° 이내 | 보색 계열(의상 +172°±18)로 재배정 |

- 잠긴 색은 **건드리지 않고** `blocked` 로그만 남긴다(사용자가 원한 색이 우선).
- 파생 색: `hair_inner`(투톤 안쪽, ±20 L / +24°), `iris_b`(오드아이, +152°), `outfit_accent`(의상 +165°±12).
- **테스트로 보장됨**: 200 시드 전부 ΔL ≥ 24 · 색상각 ≥ 14 · 의상 L ≥ 26.

---

## 7. 시드 & URL 해시

### 7.1 시드

| 형식 | 예 | 해석 |
|---|---|---|
| hex | `0x1A2B3C4D` | 32bit 그대로 (표준 표기, 8자리 대문자) |
| 10진 | `439041101` | 32bit |
| 문자열 | `포시즌jo` | FNV-1a 해시 → 32bit (`parseSeed`) |

- 엔진: `parseSeed`, `formatSeed`, `nextSeed`, `mulberry32`, `rngFor(seed, slotKey)`, `hashString`.
- **같은 시드 + 같은 세계관 + 같은 잠금 = 항상 같은 캐릭터.** (브라우저·Node·기기 무관)
- `nextSeed(seed)` = 리롤 버튼이 쓸 다음 시드(결정론적 파생).

### 7.2 URL 해시 규약

```
#v=2&s=0x1A2B3C4D&w=cyber&l=hair_front,mouth&p=hair_front-hime&z=knee
```

| 키 | 의미 | 필수 |
|---|---|---|
| `v` | 스키마 버전 (항상 `2`) | ✔ |
| `s` | 시드 (`0x` 8자리) | ✔ |
| `w` | 세계관 id | ✔ (`all` 기본) |
| `l` | 잠근 슬롯 key 목록 (쉼표) | – |
| `p` | 고정값 `slot-value` 목록 (쉼표). 값에 `-` 금지 | – |
| `z` | 줌 (`bust` / `knee` / `full`) | – |

```js
encodeHash({ seed, world, locks, pins, zoom })  // → '#v=2&s=...'
decodeHash(location.hash)                        // → {seed, world, locks, pins, zoom}
```

- 앱은 **상태가 바뀔 때마다** `history.replaceState` 로 해시를 갱신한다 → 링크 복사 = 캐릭터 공유.
- 알 수 없는 슬롯 key / 값은 조용히 무시(구버전 링크 안전).

---

## 8. API 레퍼런스 (`engine.js`)

### 생성

```js
generate({ manifest, seed, world?, locks?, pins?, prev?, only? }) -> recipe
reroll(recipe, { manifest, seed?, locks?, pins?, only? }) -> recipe
batch(seed, world, count = 9, manifest, { locks?, prev?, only? }) -> recipe[]
```

`recipe` 구조:

```js
{
  app, version, author: '포시즌jo', schema: 'anime-parts-pack/2',
  pack: { id, version },
  seed, seedHex, world,
  parts:   { face_shape: 'oval', … , hair_color: '#4a2c1d', heterochromia: false },
  colors:  { skin, hair, hair_inner, iris, iris_b, outfit, outfit_accent, mode:'60-30-10' },
  transform: { head:{dx,dy,rot,scale}, body:{dx,dy,sx,sy}, bands:{bust,hip,waist} },
  hidden:  { mouth: '「활짝 웃음」 파츠가 직접 그림' },   // 슬롯 key → 이유
  clutter: { total, max: 6, over, items: [{slot,id,points}] },
  names:   { face_shape: '계란형', … },   // UI 라벨
  bits:    { face_shape: 'soft oval anime face', … },  // 프롬프트 조각
  locks:   { … }, log: [ … ], sig: '…'
}
```

### 렌더

```js
buildRenderLayers(recipe, manifest) -> layer[]     // z 정렬 완료
```

### 내보내기

```js
compilePrompt(recipe[, manifest]) -> string   // 영어 이미지 프롬프트 (중복 제거·마침표)
                                            // 저장 JSON 복원 시에는 manifest를 두 번째 인자로
exportJSON(recipe) -> string                // 저장용 JSON 문자열 (prompt·labels·negative 포함)
recipeToJSON(recipe) -> object
signature(recipe) -> string              // 중복 판정 서명
```

`NEGATIVE_PROMPT` 상수도 함께 export 한다.

### 유틸

```js
SLOTS, SLOT_KEYS, SLOT_MAP, GROUPS, slotsByGroup(), LAYERS, LAYER_Z, SUB, DEFAULTS,
RULES, RULE_TABLE, CLUTTER_ORDER,
mulberry32, hashString, rngFor, parseSeed, formatSeed, nextSeed,
hexToRgb, rgbToHsl, hslToHex, hslOf, hueOf, satOf, lightOf, hueDistance,
signedHueDelta, shade, mixHex, rgba, colorName,
partsOf, partOf, paletteOf, fileOf, clutterOf, weightOf,
enforceRules, enforceBudget, resolveHidden, resolveTransform,
toggleLock, countLocks, labels,
encodeHash, decodeHash, validateManifest,
VERSION, SCHEMA, WATERMARK
```

### 검증

```js
validateManifest(manifest) -> { ok, errors[], warnings[] }
```

새 파츠팩을 넣기 전에 **반드시** 통과시킨다. 실패 예: 슬롯 누락, 중복 id, 잘못된 hex, `owns/hides` 에 없는 슬롯.

---

## 9. 테스트 · 성능

```bash
npm test        # = node test/engine.test.mjs
```

검증 항목(56개, 전부 통과 상태):

| 영역 | 무엇을 보장하나 |
|---|---|
| PRNG | Mulberry32 알려진 수열 일치 · 같은 시드 동일 수열 · 슬롯 서브스트림 독립 |
| 시드 | hex/10진/문자열 파싱 · formatSeed 왕복 · nextSeed 결정론 |
| 정의 | 슬롯 30개 · 레이어 20단 순서 고정 · manifest 스키마 |
| 생성 | 결정론 · 500회 러시아워 전 슬롯 채움 · 잠금 유지 · 부분 리롤 |
| 규칙 | CX01/CX10 발동 · 잠금 우선(blocked) · 캐스케이드 수렴 · 혼잡도 ≤ 6 |
| 은폐 | OV01(입 소유) / OV02(hides) |
| 색 | 200 시드 ΔL ≥ 24 · 색상각 ≥ 14 · 의상 L ≥ 26 · 강조색 분리 |
| 레이어 | z 정렬 · 20종 등장 · 틴트 값 · 피벗 · 자세/키 반영 · 은폐 표시 · 자리표시자 |
| 내보내기 | 프롬프트 중복 없음 · JSON 왕복 · 원본 불변 |
| 해시 | 왕복 · 복원 재현 |
| 성능 | 1,000회 생성+빌드 < 3초 (실측 ~130ms) |

---

## 10. 수용 기준 (Cursor가 만족해야 하는 Definition of Done)

- [ ] `manifest.example.json` 만으로 앱이 뜨고, **파츠 이미지가 하나도 없어도** 자리표시자로 캐릭터가 보인다.
- [ ] 슬롯 30개가 `GROUPS` 순서대로 나오고, 각 행에 **잠금 토글 + 개별 리롤 + 현재 값 라벨**이 있다.
- [ ] 시드를 입력/복사/새로 뽑기 할 수 있고, **같은 시드는 항상 같은 그림**을 낸다(새로고침 포함).
- [ ] 잠근 슬롯은 리롤 후에도 값이 그대로다(화면·JSON 모두).
- [ ] 규칙 로그가 `recipe.log` 그대로 표시되고, `blocked` 는 눈에 띄게 구분된다.
- [ ] 9종 배치가 중복 없이 9개를 뽑고, 카드를 누르면 메인으로 로드된다.
- [ ] PNG 저장(1024×1280, 투명 배경 옵션), 레시피 JSON 저장, 프롬프트 복사가 동작한다.
- [ ] 커스텀 이미지 업로드: 파일을 슬롯에 떨구면 그 슬롯이 `pins` 로 고정되고, 새로고침 후에도 유지된다(IndexedDB).
- [ ] URL 해시가 상태를 따라가고, 링크를 새 탭에서 열면 같은 캐릭터가 나온다.
- [ ] 하단/타이틀에 **제작: 포시즌jo** 표기, 다크 프리미엄 톤 UI.
- [ ] `npm test` 가 계속 통과한다(엔진을 수정했다면 테스트도 함께 갱신).
- [ ] **`gh-pages` 브랜치는 절대 건드리지 않는다.** 작업은 `anime-engine-v2-spec` 브랜치에서만.
