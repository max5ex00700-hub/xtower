# 애니 캐릭터 조합 엔진 v2 — 파츠팩 아키텍처 키트

> 시드 하나로 **같은 캐릭터를 항상 재현**하는 애니 캐릭터 조합 엔진.
> 30개 슬롯을 잠그고/리롤하고, 20단 레이어로 합성하며, 파츠를 갈아끼우는 것만으로 수십만 조합을 만든다.
> **제작: 포시즌jo**

---

## 무엇이 들어 있나

| 파일 | 설명 |
|---|---|
| `SPEC.md` | 구현 계약서 — 아키텍처 · 30슬롯 · 20레이어 · 규칙 매트릭스 · 색 조화 · API · 수용 기준 |
| `engine.js` | **완성된 순수 로직** (DOM 의존 0). 시드 PRNG · 슬롯 생성 · 충돌/은폐/혼잡도 · 60:30:10 색 · `buildRenderLayers` · 프롬프트 · JSON · URL 해시 |
| `manifest.example.json` | 파츠팩 매니페스트 예시 (30슬롯 · 127파츠 · 6세계관) |
| `PARTS_PACK_GUIDE.md` | 파츠 이미지 제작 규격 (마스터 캔버스 · 리그 앵커 · 회색 틴팅 · 폴더/명명 · QA) |
| `CURSOR_PROMPT.md` | Cursor에 붙여넣는 구현 지시문 (컴포지터 · UI · 9종 배치 · 업로더 · PNG 내보내기) |
| `test/engine.test.mjs` | 엔진 검증 56개 (결정론 · 잠금 · 충돌 · 레이어 출력 · 색 · 성능) |

> `compositor.js`(Canvas2D 합성) · `app.js`(UI)는 **아직 없다.** `CURSOR_PROMPT.md`로 Cursor가 만든다.

---

## 빠른 시작

```bash
npm test                  # 엔진 테스트 56개 (node 18+, ESM)
node -e "import('./engine.js').then(m=>console.log(m.SLOTS.length+' slots / '+m.LAYERS.length+' layers'))"
python3 -m http.server 8080   # ESM은 file:// 에서 열리지 않는다
```

### 30초 사용 예

```js
import { generate, buildRenderLayers, compilePrompt, exportJSON } from './engine.js';
import fs from 'node:fs';

const manifest = JSON.parse(fs.readFileSync('./manifest.example.json', 'utf8'));

const recipe = generate({ manifest, seed: '0x1A2B3C4D', world: 'cyber' });
console.log(recipe.names);              // { face_shape: '둥근 베이비페이스', … }
console.log(recipe.log);                // [{id:'CH03', type:'contrast', text:'명도 대비 통과 …'}, …]

const layers = buildRenderLayers(recipe, manifest);   // z 정렬 완료 — 그리기만 하면 된다
console.log(layers.map(l => `${l.z} ${l.layer} ${l.src ?? '(placeholder)'}`).join('\n'));

console.log(compilePrompt(recipe));     // AI 이미지 프롬프트
fs.writeFileSync('recipe.json', exportJSON(recipe));
```

### 잠그고 부분 리롤

```js
const locks = { eyes_shape: true, hair_front: true };
const next = reroll(recipe, { manifest, locks });      // 잠근 2개는 그대로
const only = reroll(recipe, { manifest, only: ['background'] });  // 배경만 새로
```

---

## 설계 요점

- **결정론**: 슬롯마다 독립 난수 스트림 `rngFor(seed, slotKey)`. 한 슬롯을 리롤해도 다른 슬롯이 밀리지 않는다.
- **그림과 로직의 분리**: 엔진은 "무엇을 어떤 순서로 어떤 색으로" 만 정하고, 그림은 파츠 이미지가 담당한다.
- **규칙은 데이터**: 충돌(CX) · 은폐(OV, `owns`/`hides`) · 혼잡도(CB ≤ 6) · 색(CH) 모두 테이블과 로그로 남는다.
- **색**: 60:30:10, 피부↔머리 명도 ΔL ≥ 24, 눈동자는 강조색(보색 계열).
- **레이어**: 20단 고정 Z 스택, 같은 레이어 내부는 `sub`(0/5/10/20)로 세분.

---

## 라이선스

비공개 · 개인 프로젝트 (포시즌jo)
