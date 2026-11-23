# CURSOR_PROMPT — Cursor에 그대로 붙여넣는 구현 지시문

> 사용법
> 1. `https://github.com/max5ex00700-hub/xtower/tree/anime-engine-v2-spec` 를 Cursor로 연다 (또는 `anime-engine-v2-spec/` 폴더를 워크스페이스 루트로).
> 2. `Ctrl+I`(Composer) 또는 `Ctrl+L`(Chat) → **아래 "메인 프롬프트"를 통째로 붙여넣기**.
> 3. Cursor가 파일을 만들기 시작하면 §4의 "단계별로 시킬 때" 로 쪼개서 진행해도 된다.
> 4. 끝나면 터미널에서 `npm test` 와 `npx serve .` 로 확인한다.

---

## 메인 프롬프트 (복붙용)

```text
너는 지금 '애니 캐릭터 조합 엔진 v2' 저장소에서 작업한다. 이 저장소는 이미 완성된 순수 로직 엔진 위에
그림 합성기와 UI를 얹는 프로젝트다. 먼저 다음 파일을 반드시 끝까지 읽어라.

- SPEC.md                (구현 계약서 — 아키텍처·30슬롯·20레이어·규칙·색·API·수용기준)
- engine.js              (이미 완성된 순수 로직. 절대 수정하지 마라)
- manifest.example.json  (파츠팩 매니페스트 예시, 30슬롯 127파츠)
- PARTS_PACK_GUIDE.md    (파츠 이미지 규격. 참고만)
- test/engine.test.mjs   (엔진이 지켜야 하는 계약. 읽고 이해해라)

## 절대 규칙
1. engine.js / manifest.example.json / test/ 절대 수정 금지. 필요한 게 없으면 나에게 물어라.
2. engine.js의 로직(슬롯 선택, 규칙, 색 조화, 레이어 순서)을 다시 구현하지 마라.
   모든 계산은 engine.js의 함수를 호출해서 얻는다. 너는 그리기와 UI만 만든다.
3. gh-pages 브랜치는 절대 건드리지 마라. 커밋/푸시는 하지 말고 파일만 만들어라(내가 확인 후 올린다).
4. 외부 라이브러리를 쓰지 마라. 순수 HTML/CSS/ES module + Canvas2D만 쓴다. (폰트는 Google Fonts Noto Sans KR만 허용)
5. 파츠 이미지가 한 장도 없어도 앱이 완전히 동작해야 한다(자리표시자 렌더).

## 만들어야 할 파일
1. compositor.js      — buildRenderLayers() 결과를 Canvas2D로 합성하는 모듈
2. index.html         — 앱 셸 (다크 프리미엄 톤, 모바일 세로 최적 390~430px, 데스크톱 2단)
3. app.css            — 스타일
4. app.js             — 상태·이벤트·내보내기 (engine.js만 호출)
5. README.md          — 실행 방법, 폴더 구조, 파츠 추가 방법 (한국어)

## 1) compositor.js 요구사항
- export class Compositor { constructor(canvas, manifest); setManifest(m); render(layers, opts); toDataURL(opts); }
- render(layers): buildRenderLayers()가 준 배열을 앞에서부터 그대로 그린다.
  * layer.visible === false → 그리지 않는다 (디버그 모드 opts.debugLayers 면 20% 알파로 그린다)
  * layer.kind === 'text' → layer.text 를 캔버스 우하단에 그린다 (watermark, '제작: 포시즌jo', 14px, alpha .55)
  * layer.src === null → 자리표시자: 반투명 사각형 + 슬롯 키 텍스트 + 레이어명. (색은 layer.tint 또는 회색)
  * transform: pivot 기준 translate → rotate(rot*PI/180) → scale(scaleX, scaleY) 순서. z 정렬은 이미 되어 있으니 재정렬 금지.
  * tint: SPEC.md 4.2 코드대로 오프스크린 캔버스 + multiply + highlight pass(lighter 0.18). tintMode 'screen'/'soft-light'/'none' 분기.
  * transform.bands 가 있으면(몸 레이어) 가로 3밴드 슬라이스 스케일(SPEC 4.4). 경계 8px 선형 블렌드.
  * clip 'right_eye'/'left_eye' → manifest.rig.head_center 기준 오른쪽/왼쪽 절반만 그린다.
- 이미지 캐시: Map<src, HTMLImageElement>. 첫 렌더 전에 모든 src를 preload하고, 로딩 완료마다 onReady 콜백으로 다시 그린다.
  없는 파일은 조용히 src=null 처리(자리표시자)로 폴백하고 콘솔 경고 1회만.
- devicePixelRatio 대응: 내부 해상도 1024×1280 고정, CSS로 표시 크기 조절. 내보내기는 2048×2560(×2).
- 배경 투명 옵션: opts.transparent === true 면 background 레이어를 건너뛴다.

## 2) index.html + app.css 요구사항 (디자인)
- 다크 톤: 배경 #0A0B11, 패널 #12141C, 라인 #22252E, 텍스트 #E8EAF0, 서브 #8A90A2, 액센트 #E4B968(골드) + #7CC6FF(포인트).
- 타이포: Noto Sans KR (400/500/700/900). 제목 900, 라벨 500 12~13px, 숫자 tabular-nums.
- 레이아웃: 모바일(≤600px) 1단 — 상단 브랜드 / 캔버스 스테이지 / 툴바 / 슬롯 패널(아코디언). 데스크톱 2단 — 좌측 스테이지(고정), 우측 360px 패널.
- 캔버스 스테이지: 4:5 비율 유지, 둥근 16px, 얇은 골드 테두리 그라데이션, 좌상단 세계관 배지, 우하단 '제작: 포시즌jo' 배지.
- 헤더: 마크(한자 '奏' 또는 로고) + "애니 캐릭터 조합 엔진" + 부제 "ANIME CHARACTER COMBINATOR · 제작: 포시즌jo".
- 하단 푸터: "제작: 포시즌jo · anime character combinator v2.0".
- 버튼: 상단 그라데이션 하이라이트 + 눌림(translateY 1px + 내부 그림자)으로 클릭감. 금지: 과한 네온/글래스모피즘.
- 접근성: 포커스 링 2px #E4B968, prefers-reduced-motion 존중.

## 3) app.js 요구사항
### 상태
state = { seed, world, locks, pins, zoom, showBg, recipe, customParts (업로드 파츠), grid:[] }
- 초기화: URL 해시가 있으면 decodeHash() 로 복원, 없으면 randomSeed() 로 generate.
- generate({manifest, seed, world, locks, pins, prev}) → recipe → buildRenderLayers → compositor.render → 슬롯/로그 갱신 → encodeHash → history.replaceState.

### 슬롯 패널 (30개 전부)
- GROUPS 순서로 섹션: 골격·체형(8) / 얼굴(9) / 귀·머리(7) / 의상·연출(6).
- 각 행: [라벨] [현재 값(라벨·색 스와치·ON/OFF)] [🔒 잠금 토글] [⟳ 이 슬롯만 리롤] [▸ 값 목록 열기]
- 값 목록: 해당 슬롯 파츠를 그리드 칩으로. 클릭하면 pins 로 그 값 고정 + 즉시 리렌더. (잠금과 구분되는 색)
- 잠긴 행은 좌측 2px 골드 라인 + 배경 살짝 밝게. 숨겨진 슬롯(recipe.hidden[key])은 흐리게 + "가려짐: 이유" 표시.
- 상단에 "잠금 N / 30", "잠금 모두 해제" 버튼.

### 툴바
- 전체 리롤 (locks 유지) / 9종 한 번에 / 시드 입력(+복사) / 새 시드 / 세계관 6칩 / 줌 3단(클로즈업·무릎 위·전신) / 배경 ON·OFF.
- 키보드: Space=전체 리롤, G=9종, S=PNG, L=잠금 패널 포커스, 숫자 1~6=세계관.

### 규칙 로그 패널
- recipe.log 를 그대로 렌더. type 별 색: coerce=파랑, override=보라, budget=주황, harmony/contrast=민트, blocked=빨강, lock/PIN=회색.
- 상단 요약: "고친 것 N · 가린 것 N · 경고 N", 혼잡도 게이지(recipe.clutter.total/6).

### 9종 배치 모달
- batch(seed, world, 9, manifest, {locks, prev: recipe}) → 9개를 3×3 카드로 (prev를 넘겨야 잠근 슬롯이 유지된다). 각 카드에 작은 캔버스(별도 Compositor 인스턴스) + 시드.
- 카드 클릭 → 그 레시피를 메인으로 로드. "다시 9종" 버튼. 중복 0.

### 내보내기
- PNG 저장: 2048×2560 → a.download = `anime_${recipe.seedHex}_${recipe.world}.png`. 배경 투명 체크박스.
- PNG(9종 ZIP 없이): 카드별 개별 저장 버튼만.
- 레시피 JSON: exportJSON(recipe) → `anime_${seedHex}.json`.
- AI 프롬프트: compilePrompt(recipe) + 빈 줄 + NEGATIVE_PROMPT → 클립보드 복사 + 모달 표시.
- 공유 링크: encodeHash(...) → 클립보드.

### 커스텀 파츠 업로더
- 드래그&드롭 또는 파일 선택(PNG/WebP, 알파 필요). 1024×1280으로 리사이즈(비율 유지, 중앙 정렬, 여백 투명).
- 업로드하면 슬롯 선택 UI를 띄우고, 선택한 슬롯의 manifest.slots[key].parts 에 `{id:'custom_<n>', name:'내 파츠 n', file:<objectURL>, tags:[]}` 를 **메모리상** 추가한 뒤 pins 로 고정.
- IndexedDB('anime-engine', store 'parts')에 Blob 저장 → 새로고침 후에도 복원. "내 파츠" 섹션에서 삭제 가능.
- 원본 manifest.example.json 파일은 절대 수정하지 않는다(런타임 복사본만 수정).

## 4) 완료 조건 (스스로 확인하고 보고해라)
- npm test 통과 (엔진 테스트 56개)
- node --check compositor.js && node --check app.js (문법)
- 파츠 이미지 0장 상태에서 캐릭터가 보이고 리롤/잠금/9종/내보내기가 전부 동작
- 같은 시드 새로고침 = 같은 그림
- 링크 복사 → 새 탭 붙여넣기 = 같은 캐릭터
- 콘솔 에러 0

## 작업 순서
1) compositor.js 먼저 (자리표시자만으로 캐릭터가 보일 때까지)
2) index.html + app.css 뼈대 + app.js 최소 루프(생성→렌더)
3) 슬롯 패널(잠금·개별 리롤·값 선택)
4) 9종 배치 → 내보내기 → 업로더
5) README.md

각 단계마다 "무엇을 만들었고 무엇이 아직 안 되는지" 3줄로 보고한 뒤 다음으로 넘어가라.
```

---

## 단계별로 시킬 때 (짧은 프롬프트)

길게 붙여넣기 부담스러우면 아래를 순서대로 하나씩.

### ① 컴포지터만

```text
SPEC.md 4장과 engine.js의 buildRenderLayers()를 읽고 compositor.js만 만들어라.
요구: z 정렬된 레이어 배열을 그대로 Canvas2D에 그린다. visible:false 스킵, src:null 자리표시자,
tint(multiply + highlight pass), transform(pivot 기준 rot/scale/translate), bands 3밴드 스케일,
watermark 텍스트, 이미지 preload 캐시, 2배 해상도 export. 외부 라이브러리 금지. engine.js 수정 금지.
```

### ② 앱 뼈대

```text
index.html/app.css/app.js를 만들어라. 다크 프리미엄 톤(#0A0B11/#12141C/#E4B968), 모바일 1단·데스크톱 2단.
engine.js에서 generate/buildRenderLayers를 호출해 캐릭터를 표시하고, 전체 리롤·시드 입력·세계관 칩·줌을 붙여라.
상태 변경 시 URL 해시를 encodeHash로 갱신하고 새로고침 시 decodeHash로 복원해라. '제작: 포시즌jo' 표기 필수.
```

### ③ 슬롯 패널

```text
SLOTS와 GROUPS를 그대로 순회해 30개 슬롯 행을 만들어라. 행마다 잠금 토글(toggleLock), 이 슬롯만 리롤
(reroll with only:[key]), 값 목록(파츠 칩 → pins 고정)을 붙여라. recipe.hidden 슬롯은 흐리게 + 이유 표시.
상단에 잠금 개수/해제. 규칙 로그는 recipe.log를 type별 색으로 그대로 출력.
```

### ④ 9종 배치 · 내보내기

```text
batch(seed, world, 9, manifest, {locks, prev: recipe})로 9종 모달(3×3)을 만들어라 (prev 필수). 카드 클릭 시 메인 로드.
내보내기: PNG 2048×2560(투명 옵션), exportJSON(recipe), compilePrompt(recipe)+NEGATIVE_PROMPT 복사,
encodeHash 링크 복사. 파일명은 anime_${seedHex}_${world}.png.
```

### ⑤ 업로더

```text
PNG/WebP 드래그&드롭 업로더를 만들어라. 1024×1280 중앙 정렬 리사이즈 → 슬롯 선택 → manifest 런타임 복사본에
custom 파츠 추가 → pins로 고정. IndexedDB('anime-engine'/'parts')에 Blob 저장하고 새로고침 후 복원.
원본 manifest.example.json 파일은 수정 금지.
```

---

## Cursor에게 알려줄 실행 환경

```bash
node --version          # 18+ (ESM)
npm test                # 엔진 테스트 56개 — 항상 통과해야 함
node --check app.js     # 문법 확인
python3 -m http.server 8080   # 로컬 확인 (ESM은 file:// 에서 안 열린다)
```

## 흔한 함정 (Cursor가 자주 틀리는 것)

| 함정 | 대응 지시 |
|---|---|
| 레이어를 z로 다시 정렬 | "이미 정렬되어 있다. 재정렬 금지" |
| tint를 `globalAlpha`로만 처리 | "SPEC 4.2 multiply + highlight pass 구현" |
| 자리표시자 없이 빈 화면 | "src:null이면 슬롯명 텍스트가 있는 반투명 판을 그린다" |
| 슬롯 값 목록을 하드코딩 | "manifest.slots[key].parts 를 읽어라" |
| 잠금을 pins로 착각 | "locks=유지, pins=값 강제. UI에서 색을 다르게" |
| 파일을 fetch로 직접 열기 | "python3 -m http.server 로 서빙, file:// 금지" |
| engine.js에 그리기 코드 추가 | "engine.js는 DOM을 모른다. 수정 금지" |
| `manifest.json` 파일명으로 저장 | "예시는 manifest.example.json 그대로 두고 런타임 복사본 사용" |
