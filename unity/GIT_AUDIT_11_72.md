# 11.72 Git 점검 및 UI 데이터 수정

점검 기준: `6d3ee43c8c988f94e08aa57a4e2785b7f6fc1d07` (11.71).
Unity Cloud Build #76은 사용자 화면에서 2초 실패, checkout 0초,
last commit `-`로 표시됐다. 실제 로그와 사용 Git revision은 확보하지 못했다.
아래 저장소 결함이 #76의 직접 원인이라고 확정하지 않는다.

## 확인하고 수정한 결함

- `XTapMainUI/main_atlas.png`: IDAT 청크가 잘린 손상 PNG.
- `XTapMainUI/main_atlas_runtime.bytes`: 위 PNG를 담은 동일한 손상 사본.
- `XTapBlacksmithUI/atlas.bytes`: 완전한 JPEG 디코딩 실패.
- `XTapBlacksmith` 안의 내장 atlas 복사본도 같은 손상 JPEG였다.
- 세 파일은 현재 main panels, inventory, forge selection에서 실제로 읽는다.
  기존 BuildConfig 필수 검사에는 빠져 있었다.
- `ProjectVersion.txt`의 revision이 `000000000000`이었다.
  공식 2022.3.62f2 changeset `7670c08855a9`로 수정했다.
  출처: https://unity.com/releases/editor/whats-new/2022.3.62f2
- 클라우드 빌드 안내 두 문서에 오래된 Unity/앱 버전 정보가 남아 있어 정정했다.

손실된 원본의 완전한 복구본은 Git 이력에 없었다. UI 패널은 기존 sprite crop과
9-slice border를 보존한 코드 기반 SVG로 교체했다. 남색 배경과 금색 테두리로
구성하며 캐릭터/룰렛/대장간 듀얼 그림은 변경하지 않았다.
`tools/build_panel_atlases.py`로 두 atlas와 내장 사본을 함께 재생성한다.
새 PNG 두 장을 직접 열어 확인했다. 이는 Unity 화면 검증은 아니다.

BuildConfig는 이제 세 패널 파일도 base64 해제 → PNG 구조/CRC → Unity LoadImage
검사 대상으로 포함한다. 독립 이미지 검사도 내장 사본을 확인하고 resource와
동일한지 비교한다. Unity LoadImage 검사는 실제 Unity 빌드 때 실행되며 이번
환경에서는 실행하지 못했다. 버전은 11.72-git-audit-<commit> / 1172.

## 수행한 검증

- 수정 전 필수 32개 이미지: Python 컨테이너/CRC/zlib/실제 디코딩 통과.
- 수정 전 전체 이미지: 37개 중 3개 실패 (내장 사본은 별도 동일 손상 확인).
- 수정 후 필수 이미지+내장 사본 36개: 모두 통과.
- 수정 후 전체 Resources 이미지+내장 사본 38개: 모두 통과.
- 전투: 실제 C# 규칙 570개 + 실제 controller 메서드 adapter 21개 통과.
- 호감도: 실제 C# 규칙 1023개 + 저장/가방/대사 adapter 56개 통과.
- 도감: 실제 C# 목록/완료/기존 저장/보상 메서드 adapter 528개 통과.
  2층 39장 미완료, 40장 완료, +8칸 1회 지급, 전체 +80칸을 확인했다.
- 프로젝트 C# 25개: C# 9 문법 검사 통과. Unity 컴파일과 다르다.
- 시작 씬/Main.unity meta GUID 연결, JSON manifest, Git LFS pointer 및 중복 GUID 확인.
  시작 씬 연결 정상, LFS pointer 및 중복 GUID 없음.
- 원본 APK part1/part2 결합 ZIP 전체 CRC 통과. 필수 도감 이미지 409/409개 실제 디코딩 통과.
- 인코딩된 전투/보이스 WAV의 RIFF 식별자와 파일 길이 확인.

추가 타입 검사도 시도했다. 사용 가능한 UnityEngine 2021.3.33 참조 DLL과
공식 uGUI 2018.4 소스는 목표 환경과 달라, UI VertexHelper/ReflectionMethodsCache,
ColorBlock.selectedColor, 모바일 Handheld의 참조 불일치로 컴파일에 실패했다.
이 결과를 게임의 Unity 2022.3.62f2 컴파일 성공/실패로 해석하지 않는다.
이 임시 참조 패키지들은 프로젝트에 추가하지 않았다.

## 2층 도감 40장으로 변경

`assets/f2_k09.jpg`가 원본 APK 안에 없다. 사용자가 누락 한 장 제외를 승인하여
2층 도감 목록·진행률·페이지·완료 조건을 40장으로 변경했다. 다른 9개 캐릭터는
41장씩이며 총 409장이다. 전투의 2층 k 이미지 선택도 k00~k08로 제한한다.
목록 생성과 전투 선택은 같은 `ActionImageCount` 규칙을 사용한다.

완료 보상은 기존대로 캐릭터별 플레이어 가방 +8칸, 한 번만 지급한다.
이미 40장을 모은 기존 저장은 2층 도감을 열면 완료 보상을 받으며, 기존 수령
기록은 보존한다. APK 검사도 이 409장을 필수 이미지로 검사한다.

## 검증 한계

Unity Editor 2022.3.62f2 실행, 실제 Unity API 컴파일, shader/import,
Cloud Build 성공, APK 설치/실기기 동작은 확인하지 못했다.
#76은 서버 준비/설정/연결 문제인지 실제 로그가 필요하다. 새 Cloud Build는
이 점검 작업에서 직접 실행하지 않았다.
