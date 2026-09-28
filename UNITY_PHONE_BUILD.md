# X탑 Unity - 폰만으로 APK 만들기

## 이미 준비된 것
- Repository: max5ex00700-hub/xtower
- Branch: unity-prototype
- Unity project subfolder: unity/XTapUnity
- Unity version: Unity 6.3 LTS / 6000.3.24f1
- Android package: com.xtower.game.unity
- Version code: source-controlled in `unity/XTapUnity/Assets/Editor/XTapBuildConfig.cs` (현재 1173)
- Test APK signing: Unity Build Automation의 Auto-generated debug keystore 사용

## 폰에서 최초 1회 설정
1. Unity Dashboard에 로그인한다.
2. 프로젝트를 하나 만든다.
3. DevOps > Build Automation으로 이동한다.
4. Source control을 Git으로 설정한다.
5. 이 공개 저장소를 연결한다:
   https://github.com/max5ex00700-hub/xtower.git
6. Android Build Configuration을 만든다.
7. Branch = unity-prototype
8. Project subfolder path = unity/XTapUnity
9. Unity version = 6000.3.24f1 (또는 이 프로젝트의 ProjectVersion.txt를 읽는 Auto detect)
10. Android signing = Auto-generated debug keystore
11. 저장 후 Build를 누른다.
12. Build history에서 APK를 다운로드한다.

## 기존 빌드 대상의 Unity 버전 변경
기존 설정이 2022.3.62f2로 고정되어 있다면 Build configuration의 Unity version을
6000.3.24f1로 바꾸고 저장한다. Git 파일 수정만으로 고정 설정이 바뀌지는 않는다.
과거 빌드 상세 화면에는 당시 Unity 2022 정보가 남으므로 새 설정과 다음 빌드에서 확인한다.
엔진 변경 후 첫 빌드는 Clean Build로 실행한다. 이미 진행 중인 빌드가 있으면 중복 실행하지 않는다.

## 다음 빌드부터
위 설정 저장 후 같은 Android Configuration에서 빌드한다.

## 현재 1-1 프로토타입
- 조훈 HP 100 / ATK 5 / DEF 1
- 1층 보스 HP 100 / ATK 3 / DEF 1
- 공격 / 회피 / 자동 / 재시작
- 타격 플래시, 화면 흔들림, 보스 돌진
- 승리 후 포획
- 감옥 카운트

이 버전의 목적은 먼저 '폰 -> 클라우드 -> 설치 가능한 Unity APK' 파이프라인을 검증하는 것이다.


## 중요: GitHub Actions APK와 Unity APK를 혼동하지 말 것
- `.github/workflows/build-apk.yml`은 루트 `app/`의 예전 Android/Java 앱만 빌드한다.
- X탑 Unity 최신 APK는 반드시 Unity Build Automation에서 `unity/XTapUnity`를 빌드해야 한다.
- GitHub Actions의 legacy Android artifact는 Unity APK가 아니다.

## 최신 커밋 검증
- 11.11부터 Unity Build Automation의 `GIT_COMMIT`을 Android versionName에 자동 삽입한다.
- 옵션 > 버전 정보에서 `11.73-unity6-<커밋 앞 8자리> (1173)` 형식으로 확인한다.
- Unity Build Automation 빌드 로그에서도 `X탑 BUILD FINGERPRINT`를 검색하면 branch와 commit이 표시된다.
- 11.11은 기준 이미지에서 추출한 메인 버튼 9개를 pre-build 단계에서 검증한다. 누락/손상 시 APK를 만들지 않고 빌드를 실패시킨다.

## 2026-09-28 Git 점검
- Unity 6000.3.24f1 공식 리비전: `4e7b9b5b6244`.
- 점검 결과/실제 검증 한계: `unity/GIT_AUDIT_11_72.md`.

## 11.73 Unity 6.3 LTS 이전
- 변경 근거/Cloud 설정 확인 사항: `unity/UNITY_VERSION_11_73.md`.
- Unity 6 실제 컴파일/Cloud Build/기기 검증은 아직 수행하지 않았다.
