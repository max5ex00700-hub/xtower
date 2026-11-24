# 애니 캐릭터 조합 엔진 v2

시드 하나로 **같은 캐릭터를 항상 재현**하는 애니 캐릭터 조합기.
30개 슬롯 잠금·리롤, 20단 Canvas2D 레이어 합성, 파츠팩 교체로 수십만 조합.
**제작: 포시즌jo**

---

## 실행 방법

```bash
# 엔진 테스트 (56개)
npm test

# 문법 확인
node --check compositor.js && node --check app.js

# 로컬 서버 (ESM은 file:// 에서 열리지 않음)
npm run serve
# → http://localhost:8080
```

브라우저에서 `index.html`을 열고 전체 리롤·잠금·9종 배치·PNG/JSON 내보내기를 확인하세요.
파츠 이미지가 없어도 자리표시자로 캐릭터가 보입니다.

---

## 폴더 구조

```
anime-engine-v2-spec/
├── index.html              # 앱 셸
├── app.css                 # 다크 프리미엄 UI
├── app.js                  # 상태 · 이벤트 · 내보내기 · 업로더
├── compositor.js           # Canvas2D 레이어 합성
├── engine.js               # 순수 로직 (수정 금지)
├── manifest.example.json   # 파츠팩 예시 (30슬롯 · 127파츠)
├── assets/parts/**         # 파츠 이미지 (없으면 자리표시자)
├── SPEC.md                 # 구현 계약서
├── PARTS_PACK_GUIDE.md     # 파츠 제작 규격
├── CURSOR_PROMPT.md        # Cursor 구현 지시문
└── test/engine.test.mjs    # 엔진 테스트 56개
```

| 파일 | 역할 |
|---|---|
| `engine.js` | 시드 PRNG · 슬롯 생성 · 규칙 · 색 · `buildRenderLayers` · 해시 |
| `compositor.js` | 레이어 배열 → Canvas2D (틴트 · 밴드 · 자리표시자 · PNG) |
| `app.js` | UI 상태, 잠금/핀, 9종, 업로더, 내보내기 |
| `manifest.example.json` | 읽기 전용 예시 — 런타임에서는 복사본만 수정 |

---

## 단축키

| 키 | 동작 |
|---|---|
| `Space` | 전체 리롤 (잠금 유지) |
| `G` | 9종 배치 |
| `S` | PNG 저장 |
| `L` | 슬롯 패널 포커스 |
| `1`~`6` | 세계관 전환 |

---

## 파츠 추가 방법

1. **1024×1280** WebP/PNG(알파), 회색 계조로 그린다 → 규격은 `PARTS_PACK_GUIDE.md`.
2. `assets/parts/{슬롯}/{id}.webp` 에 저장.
3. `manifest.example.json`을 복사해 `manifest.json` 등으로 두고 해당 슬롯 `parts`에 `{id, name, file, tags, prompt}` 추가.
4. `validateManifest()`로 검증 후, 앱의 fetch 경로를 새 매니페스트로 바꾸면 된다.

앱 안에서 PNG/WebP를 드래그&드롭하면 **런타임 복사본**에만 커스텀 파츠가 추가되고 IndexedDB에 저장됩니다. 원본 `manifest.example.json` 파일은 수정되지 않습니다.

---

## 공유 · 재현

- URL 해시: `#v=2&s=0x1A2B3C4D&w=cyber&l=hair_front&p=hair_front-hime&z=knee`
- 같은 시드 + 세계관 + 잠금/고정 = 항상 같은 캐릭터
- PNG: `anime_{seedHex}_{world}.png` (2048×2560, 투명 옵션)
- JSON / AI 프롬프트 복사 지원

---

## 라이선스

비공개 · 개인 프로젝트 (포시즌jo)
