# 파츠팩 제작 가이드 — 애니 캐릭터 조합 엔진 v2

> 이 문서는 **그리는 사람(또는 이미지를 만드는 AI)** 을 위한 규격서다.
> 규격을 지키면 `engine.js` + `compositor.js` 가 **색을 곱해도 선화가 죽지 않고**, 파츠를 갈아끼워도 얼굴이 어긋나지 않는다.
> 코드는 `PARTS_PACK_GUIDE` 를 읽지 않는다. 지켜야 하는 계약은 여기와 `manifest.json` 뿐이다.

---

## 1. 한 줄 원칙

> **파츠는 "인형의 옷"이다.**
> 얼굴·몸은 하나의 마스터 캔버스 위에 **같은 좌표계**로 그리고, 옷·머리·악세는 **그 위에 정확히 얹히는 판**으로 그린다.
> 색은 그리지 않는다 — **회색으로 그려 두고 엔진이 색을 곱한다.**

---

## 2. 마스터 캔버스 & 리그 앵커

### 2.1 캔버스

| 항목 | 값 | 비고 |
|---|---|---|
| 크기 | **1024 × 1280 px** | 세로 4:5. 모든 파츠가 이 크기(풀캔버스) |
| 포맷 | **WebP (알파)** — 품질 90 | PNG 허용. JPG 금지(알파 없음) |
| 배경 | 완전 투명 (`alpha = 0`) | 흰 배경으로 저장하면 틴트가 뭉갠다 |
| 프레이밍 | **머리 ~ 무릎 위** (bust-to-knee) | 가로 512 중심축, 세로는 아래 표 |
| 안전 여백 | 상하좌우 **24 px** | 악세·오라가 잘리지 않게 |
| 블리드 | 파츠 경계 **8 px** 겹침 | 밴드 스케일·회전 시 이음새 방지 |

### 2.2 리그 앵커 (모든 파츠가 공유하는 기준점)

`manifest.rig` 와 동일. **파츠는 이 좌표에 몸을 맞춰 그린다.**

| 앵커 | 좌표 (x, y) | 쓰임 |
|---|---|---|
| `head_center` | **512, 320** | 머리 회전·스케일 피벗 (모든 머리 레이어) |
| `head_radius` | **160** (반지름) | 두상 크기 기준 |
| 머리 위 | 160 | 정수리 |
| `eye_line` | y **370** | 두 눈 중심을 잇는 선 |
| 코 끝 | y ≈ 424 | |
| 입 중심 | y ≈ 452 | |
| `chin` | y **480** | 턱 끝 |
| `neck` | **512, 516** | 목 중심 (어깨선 위) |
| `shoulder_l` / `shoulder_r` | **372, 588** / **652, 588** | 어깨 관절 |
| `waist` | **512, 792** | 허리 최소폭 |
| `hip` | **512, 872** | 몸 스케일·회전 피벗 |
| `knee` | **512, 1120** | 화면 아래 기준 |

```
 y=160  ┌──────────── 정수리
          │   ◯  머리 (head_center 512,320 / r=160)
 y=370  │  ─────  eye_line  ← 눈 중심
 y=452  │    ‿    입
 y=480  └────  chin
 y=516     ▏     neck
 y=588  ───┴───  shoulder  (372)        (652)
 y=700  │      │  ← bust 밴드 끝
 y=792  │      │  ← waist
 y=872  ─┴────┴─  hip  ← 몸 피벗
 y=1120          (knee) — 화면 아래
```

### 2.3 밴드 구간 (가슴·골반 변형)

`bust`·`hip` 슬롯은 그림을 다시 그리지 않고 이 구간만 **가로로** 늘린다.

| 밴드 | y 범위 | 배율 범위 |
|---|---|---|
| `bust` | 560 ~ 700 | 0.94 ~ 1.15 |
| `waist` | 700 ~ 780 | 고정(1.0) |
| `hip` | 780 ~ 920 | 0.95 ~ 1.14 |

**그릴 때 주의**: 이 경계를 가로지르는 **얼굴형 무늬·로고·가는 줄무늬**는 피한다(늘어나면 티가 난다). 세로선·주름은 괜찮다.

---

## 3. 틴팅 대응 — 회색으로 그리기

엔진은 파츠에 `multiply` 로 색을 곱한다. 파츠가 **무채색 계조**로 그려져 있어야 색이 예쁘게 들어가고 선화가 살아남는다.

### 3.1 색 규격

| 요소 | 규격 | 이유 |
|---|---|---|
| 베이스(중간톤) | **#C8C8C8** (L 78%) | 곱하면 지정색의 78% 밝기로 떨어진다 → 자연스러운 중간톤 |
| 그림자 | L **35 ~ 62%** 회색 | 곱해도 색이 죽지 않는 범위 |
| 하이라이트 | L **92 ~ 98%** 회색 (**순백 #FFF 금지**) | 순백은 곱해도 흰색으로 남아 이질감 |
| 선화 | **#1A1A22** (거의 검정, 살짝 푸른 기) | 곱해도 검정 유지, 톤 통일 |
| 채도 | **0** (완전 무채색) | 색은 엔진이 준다. 회색에 색이 섞이면 곱했을 때 탁해진다 |
| 투명도 | 그림자 60~85%, 하이라이트 70~90% | 알파 그라데이션 허용 |

> **예외 — 색을 직접 칠하는 파츠**: `iris_fx`(별·하트 무늬, `screen` 틴트), `background`(풀컬러), `aura`(자체 발광색). 이들은 매니페스트에서 `tintMode` 를 다르게 준다.

### 3.2 선 굵기

| 대상 | 굵기 (@1024) |
|---|---|
| 얼굴·몸 외곽선 | 4 ~ 5 px |
| 머리카락 외곽 | 4 px |
| 눈꺼풀·눈썹 | 3 ~ 4 px |
| 코·입 | 2 ~ 3 px |
| 의상 주름 | 3 px |

같은 파츠 안에서는 **일정한 굵기**를 유지한다(파츠끼리 섞였을 때 선이 튀지 않게).

### 3.3 그림자 방향

- 광원은 **좌상단** 고정.
- 파츠에 **색 그림자 넣지 않는다**. 얼굴 위로 떨어지는 머리 그림자는 `hair_back`·`hair_front` 파츠에 **회색 곱셈 레이어**(L 55%, 알파 35%)로 넣는다.
- 파츠 외곽에 **드롭섀도 효과 금지**(합성 시 이중 그림자).

### 3.4 가장자리

- 안티앨리어싱 유지. **1px 검정 테두리(외곽선 굽기) 금지** — 레이어 순서가 바뀌면 선이 겹쳐 보인다.
- 파츠 경계는 **8px 블리드**(다른 파츠 아래로 살짝 들어가게) 그린다.

---

## 4. 폴더 · 파일 명명

```
assets/parts/
  face_shape/     oval.webp  vline.webp  baby.webp  cool.webp
  body_type/      slim.webp  standard.webp  glamour.webp  athletic.webp
  pose/           standing.arms.webp  tilt.arms.webp  ...
  eyes_shape/     round.webp  round.right.webp  sharp.webp  sharp.right.webp  ...
  iris_fx/        star.webp  heart.webp  ...
  eyebrow/        straight.webp  ...
  expression/     grin.webp  grin.mouth.webp  shy.cheek.webp  ...
  nose/           dot.webp  ...
  mouth/          soft.webp  ...
  cheek_fx/       sweat.webp  ...
  ears/           cat.back.webp  cat.front.webp  ...
  hair_back/      long_straight.webp  long_straight.inner.webp  ...
  hair_side/      long.webp  ...
  hair_front/     hime.webp  ...
  ahoge/          single.webp  ...
  outfit/         sailor.webp  sailor.accent.webp  ...
  head_acc/       crown.webp  ...
  glasses/        round.webp  ...
  aura/           sakura.back.webp  sakura.front.webp  ...
  background/     classroom.webp  ...
```

### 명명 규칙

| 규칙 | 예 |
|---|---|
| 소문자 + `snake_case` | `side_swept.webp` (O) · `SideSwept.webp` (X) |
| 폴더명 = 슬롯 key | `hair_front/hime.webp` |
| 파일명 = 파츠 `id` | `manifest` 의 `id` 와 **반드시 일치** |
| 변형 접미사 | `.back` 뒤판 · `.front` 앞판 · `.inner` 안쪽(투톤) · `.right` 오른쪽 눈 · `.accent` 악센트 · `.mouth`/`.cheek` 소유 레이어 · `.arms` 팔 |

> 접미사는 매니페스트의 `files` 객체 키(`aura_back`, `inner`, `right`, `accent`, `mouth`, `ears_front`, …)와 연결된다. 파일명 규칙을 바꾸려면 `manifest.json` 만 고치면 된다.

### 크기·용량

| 항목 | 권장 |
|---|---|
| 해상도 | 1024 × 1280 (풀캔버스, **정렬된 상태**) |
| 파일 크기 | 1개 **≤ 400 KB** (WebP 90). 배경은 ≤ 900 KB |
| 파츠팩 전체 | 127파츠 기준 **≤ 25 MB** |

> **왜 풀캔버스인가**: 좌표 계산이 0이 되고, 어떤 파츠도 어긋나지 않는다. 크롭 최적화(bbox + `offset`)는 v3 과제.

---

## 5. 레이어별 "어디까지 그리는가"

같은 부위를 두 파츠가 그리면 안 된다. 아래 경계를 지킨다.

| 레이어 | 파츠 | 그리는 범위 | **그리지 않는 것** |
|---|---|---|---|
| `hair_back` | `hair_back` | 얼굴 **뒤** 전체 실루엣 (정수리 뒤·어깨 아래까지) | 얼굴 앞으로 나오는 부분 |
| `body_base` | `body_type` | 목·어깨·팔·몸통·허벅지 (피부, 회색) | 얼굴·손가락 끝 디테일 |
| `body_base(+20)` | `pose` | **팔·손만** (피부, 회색) | 몸통·얼굴 |
| `ears_back` | `ears` | 귀의 **뒤쪽 절반** (머리 뒤로 넘어간 부분) | 귀 앞면 |
| `face_base` | `face_shape` | 얼굴 윤곽 + 피부 + 턱·볼 음영 | 눈·코·입·눈썹·홍조(다른 파츠가 그림) |
| `face_base(+30)` | `expression` | 표정 **오버레이** (홍조·눈물·땀·이마 그림자) | 눈·코·입·눈썹 |
| `cheek_fx` | `cheek_fx` | 홍조·점·반창고 등 **얼굴 위 장식** | 눈·입 |
| `eyes` | `eyes_shape` | 흰자 + 눈꺼풀 선 + 속눈썹 + **회색 홍채 바탕** | 홍채 무늬(→`iris_fx`), 눈썹 |
| `eyes(+5)` | `eyes_shape.right` | 오른쪽 눈만 (오드아이용) | 왼쪽 눈 |
| `eyes(+10)` | `iris_fx` | 홍채 **위에 얹는 무늬·발광** (알파) | 흰자·눈꺼풀 |
| `nose` | `nose` | 코 끝 점·선·음영 | 볼 |
| `mouth` | `mouth` | 입술·입 안 (표정 중립 버전) | 볼 홍조 |
| `mouth(+20)` | `expression` | 그 표정 전용 입 | 나머지 얼굴 |
| `eyebrow` | `eyebrow` | 눈썹만 | 이마 음영 |
| `outfit` | `outfit` | 옷 전체 (목·어깨·가슴·팔) | 피부·머리 |
| `outfit(+5)` | `outfit.accent` | 리본·단추·라인 등 **악센트 색 부분** | 옷 본체 |
| `hair_side` | `hair_side` | 귀 앞·볼 옆으로 떨어지는 **얇은 가닥** | 뒤통수·앞머리 |
| `ears_front` | `ears` | 귀의 **앞쪽 절반** (머리카락 앞) | 뒤쪽 |
| `hair_front` | `hair_front` | 이마를 덮는 **앞머리** | 정수리 뒤 |
| `ahoge` | `ahoge` | 정수리에서 솟은 가닥 | 나머지 머리 |
| `glasses` | `glasses` | 렌즈·테 (알파) | 눈 자체 |
| `head_acc` | `head_acc` | 리본·모자·헤드폰 등 | 머리카락 |
| `aura_back/front` | `aura` | 캐릭터 뒤/앞의 빛·꽃잎·마법진 | 캐릭터 몸 |
| `background` | `background` | 풀컬러 배경 (불투명) | 캐릭터 |

---

## 6. `manifest.json` 스키마

### 6.1 최상위

```jsonc
{
  "schema": "anime-parts-pack/2",     // 고정
  "id": "core-female-v1",
  "name": "코어 여성 파츠팩 v1",
  "version": "1.0.0",
  "author": "포시즌jo",
  "basePath": "assets/parts/",        // 파일 경로 앞에 붙는 prefix
  "canvas": { "width": 1024, "height": 1280, "format": "webp", "alpha": true, "bleed": 8 },
  "rig": { "head_center": [512,320], "eye_line": 370, "chin": 480, "neck": [512,516],
           "shoulder_l": [372,588], "shoulder_r": [652,588], "waist": [512,792],
           "hip": [512,872], "knee": [512,1120], "head_radius": 160,
           "bands": { "bust": [560,700], "waist": [700,780], "hip": [780,920] } },
  "worlds": [ { "id": "academy", "name": "학원물", "short": "SCHOOL", "color": "#7cc6ff" }, ... ],
  "fx": { "clutter_budget": 6, "luminance_lock": 24, "hue_gap_min": 20,
          "odd_eye_chance": 0.17, "two_tone_chance": 0.30, "tag_boost": 2.8 },
  "palette": { "hair_color": [ ... ], "outfit_color": [ ... ], "skin_tone": [ ... ], "eye_color": [ ... ] },
  "slots":   { "face_shape": { "pick": "part", "parts": [ ... ] }, ... }
}
```

### 6.2 파츠 항목

| 필드 | 필수 | 설명 |
|---|---|---|
| `id` | ✔ | 파일명과 동일. 슬롯 안에서 유일 |
| `name` | ✔ | UI 표시용 한글 이름 |
| `file` | △ | 단일 파일 경로 (`basePath` 기준 상대경로) |
| `files` | △ | 다중 레이어 파일: `{ "ears_back": "...", "ears_front": "...", "inner": "...", "right": "...", "accent": "...", "mouth": "..." }` |
| `tags` | ✔ | 세계관 태그 배열 (`academy`/`fantasy`/`cyber`/`resort`/`gothic`) — 비우면 모든 세계관 |
| `prompt` | ✔ | AI 프롬프트에 들어갈 **영어** 묘사 (없으면 빈 문자열) |
| `weight` | – | 뽑힐 가중치(기본 1). `human` 귀처럼 흔해야 하는 파츠는 2.4 |
| `clutter` | – | 혼잡도 점수 0~3 (악세·이펙트만) |
| `anchor` | – | 변형/자세: `{ head:{dx,dy,rot,scale}, body:{dx,dy,sx,sy}, bands:{bust,hip,waist} }` |
| `owns` | – | 이 파츠가 **직접 그리는** 하위 슬롯 (`["mouth"]`) → 그 슬롯 레이어는 은폐되고 `files` 의 그림이 `+20` 에 그려진다 |
| `hides` | – | 이 파츠가 **가리는** 슬롯 (`["eyebrow"]`, `["ahoge"]`) |
| `layer` | – | `file` 하나를 여러 레이어에 쓸 때 대상 레이어 지정 |

**`file` 과 `files` 규칙**

```jsonc
// 단일 파일(그 레이어에만 그린다)
{ "id": "hime", "file": "hair_front/hime.webp" }

// 여러 레이어(각 레이어마다 다른 파일)
{ "id": "cat", "files": {
    "ears_back":  "ears/cat.back.webp",
    "ears_front": "ears/cat.front.webp" } }

// 소유 레이어(표정이 입을 직접 그림)
{ "id": "grin", "file": "expression/grin.webp",
  "files": { "mouth": "expression/grin.mouth.webp" },
  "owns": ["mouth"] }
```

### 6.3 변형 파츠 (그림 없음)

`height`·`bust`·`hip`·`build` 는 **그림을 그리지 않는다.** `anchor` 만 준다.

```jsonc
{ "id": "tall", "name": "장신 · 8등신", "tags": [], "prompt": "tall, 8 heads tall",
  "anchor": { "head": { "dy": -22, "scale": 0.985 }, "body": { "sy": 1.055 } } }

{ "id": "glamour", "name": "글래머", "tags": ["resort"], "prompt": "very large bust",
  "anchor": { "bands": { "bust": 1.15 } } }
```

| 필드 | 효과 |
|---|---|
| `anchor.head.dx/dy` | 머리 레이어 전체 이동 (px) |
| `anchor.head.rot` | 머리 회전(도). 갸웃 자세 −4 등 |
| `anchor.head.scale` | 머리 확대/축소 (등신 조절) |
| `anchor.body.sx/sy` | 몸 가로/세로 배율 |
| `anchor.bands.bust/hip` | 몸통 밴드 가로 배율 |

### 6.4 팔레트 항목

```jsonc
{ "id": "wine", "name": "와인 레드", "color": "#8d3f3f",
  "tags": ["gothic"], "prompt": "wine red hair" }
```

`color` 는 **반드시 `#rrggbb`**. `prompt` 는 색 이름 대신 AI에 넣을 묘사(선택).

### 6.5 슬롯 설정

```jsonc
"slots": {
  "face_shape": { "pick": "part", "parts": [ ... ] },
  "skin_tone":  { "pick": "color" },                       // palette.skin_tone 사용
  "heterochromia": { "pick": "flag", "probability": 0.17 }   // 확률 ON
}
```

---

## 7. 파츠 추가 절차 (5단계)

1. **그린다** — §3 규격(회색·선화·알파·1024×1280)으로. 기존 캐릭터 PNG 위에 반투명 레이어로 대고 그리면 정렬이 쉽다.
2. **저장한다** — `assets/parts/{slot}/{id}.webp` (§4 명명). WebP 90, 알파 유지.
3. **매니페스트에 등록한다** — 해당 슬롯 `parts` 배열에 `{id, name, file, tags, prompt}` 추가.
4. **검증한다** — `node -e "import('./engine.js').then(E=>console.log(E.validateManifest(JSON.parse(require('fs').readFileSync('manifest.json')))))"` → `ok: true`.
5. **눈으로 검수한다** — §8 QA 3종(틴트·실루엣·앵커).

---

## 8. 검수 QA — 3종

### QA-1. 앵커 정렬

`PARTS_PACK_GUIDE` 의 앵커 가이드 PNG(십자선: 눈높이 370, 턱 480, 어깨 588)를 파츠 위에 50% 투명도로 얹어 확인.

- [ ] 눈 중심이 `eye_line`(y 370)에 온다
- [ ] 턱이 `chin`(y 480)에 온다
- [ ] 어깨 관절이 (372 / 652, 588)에 온다
- [ ] 좌우가 512 중심으로 대칭이다(자세 파츠 제외)

### QA-2. 틴트

파츠를 **#E8C48A(허니), #2B1A16(흑발), #7FD8C8(민트), #C2384A(루비)** 4색으로 곱해 본다.

- [ ] 어떤 색에서도 **선화(#1A1A22)가 사라지지 않는다**
- [ ] 하이라이트가 흰색으로 뜨지 않는다
- [ ] 그림자가 탁해지지 않는다(회색 계조 범위를 지켰는지)

### QA-3. 실루엣

파츠를 **100% 검정 실루엣**으로 칠해 본다.

- [ ] 다른 파츠와 겹치는 부위가 자연스럽다(블리드 8px)
- [ ] 밴드 경계(700·780)에서 잘린 티가 없다
- [ ] 머리카락이 얼굴을 덮는 경계가 의도대로다

---

## 9. AI 이미지로 파츠 만들 때

그림을 직접 그리지 않아도 된다. 순서만 지키면 된다.

1. **기준 캐릭터 1장**을 만든다(정면·무표정·회색조·1024×1280·투명 배경).
2. 파츠별로 **inpaint/mask 편집**으로 해당 부위만 바꾼다(같은 시드·같은 프롬프트 유지 → 정렬이 흔들리지 않는다).
3. **누끼**(배경 제거) → **무채색화**(채도 −100, 명도 35~98 유지) → **선화 보정**(#1A1A22).
4. 1024×1280 캔버스에 **원본 위치 그대로** 배치해 저장.
5. §7 절차로 등록.

> 생성 도구의 결과물은 대개 **채도가 있는 회색**이다. `PARTS_PACK_GUIDE` §3.1 의 채도 0 규격을 반드시 맞춘다(아니면 틴트 시 탁해진다).

---

## 10. 자주 하는 실수

| 증상 | 원인 | 해결 |
|---|---|---|
| 색을 곱하면 파츠가 새까매진다 | 베이스가 너무 어둡다 / 채도가 있다 | 베이스 #C8C8C8, 채도 0 |
| 하이라이트가 하얗게 뜬다 | 순백(#FFF) 사용 | L 92~98% 회색 |
| 파츠 사이에 흰 테두리 | JPG 저장 / 알파 번짐 | WebP 알파, 블리드 8px |
| 옆머리가 얼굴을 덮는다 | `hair_side` 에 앞머리까지 그림 | §5 범위 준수 |
| 가슴을 키우면 옷 무늬가 늘어난다 | 밴드 경계를 가로지르는 무늬 | 무늬를 밴드 밖으로 |
| 표정을 바꿔도 입이 안 바뀐다 | `owns`/`files.mouth` 누락 | §6.2 참고 |
| 새 파츠가 안 나온다 | `tags` 에 현재 세계관만 있고 가중치 0 / `id` 불일치 | `tags` 확인, 파일명 = id |
