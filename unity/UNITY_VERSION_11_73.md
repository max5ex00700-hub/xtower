# 11.73 Unity 6.3 LTS 이전

기준 HEAD: `16a10375204dbd690dc081884362c8394de8cb2e` (11.72).
사용자가 Unity 6 이전을 확정했다. 중간 f3 수정은 GitHub에 게시하지 않았으며
이번 Unity 6 변경으로 대체했다.

## 대상

- Unity 6.3 LTS: `6000.3.24f1`, changeset `4e7b9b5b6244`.
- 앱: `11.73-unity6-<commit>` / Android versionCode `1173`.
- 패키지 ID: `com.xtower.game.unity`, ARM64 / IL2CPP / APK.
- 원래 저장 키와 데이터, 11.72의 에셋 수정과 2층 도감 40장 규칙을 유지한다.

공식 릴리스 API와 릴리스 페이지에서 24f1을 확인했다. API에는 2026-09-24
출시된 25f1도 있지만 UBA 반영은 공식 안내상 보통 5영업일까지 걸리므로,
2026-09-10 출시된 24f1로 고정한다. 해당 계정의 버전 드롭다운은 아직 확인하지
못했으며, 클라우드 제공 여부를 직접 확인했다고 주장하지 않는다.
Unity 6.3 LTS 지원 종료는 2027년 12월이다.

## 변경한 호환 항목

- ProjectVersion.txt: 엔진 버전과 revision을 Unity 6.3 값으로 변경.
- com.unity.ugui: 1.0.0 → Unity 6.3 코어 UI 패키지 2.0.0.
- Input Legacy/UI/IMGUI 모듈을 명시. 새 Input System으로 입력 코드를 이중 전환하지 않는다.
- 이전 저장소에 없던 ProjectSettings.asset을 추가해 기존 Input Manager(0),
  Android ARM64/IL2CPP, Activity 진입점, 세로 화면, 패키지 ID를 명시한다.
  Unity 6 공식 프로젝트의 serializedVersion 28 스키마를 참고한 최소 설정이며,
  실제 Editor import로 재직렬화한 결과는 아니다.
- InputManager.asset에 StandaloneInputModule의 Horizontal/Vertical/Submit/Cancel을 정의.
- FindObjectOfType → FindFirstObjectByType (시작 객체 및 EventSystem 중복 방지 유지).
- 기본 폰트 fallback: Arial.ttf → LegacyRuntime.ttf. OS 한글 폰트 선택은 유지.
- PlayerSettings의 BuildTargetGroup API → NamedBuildTarget.Android API.
- 빌드 전 실제 엔진 버전을 로그에 남기고 6.3 계열인지 확인한다.

공식 Unity 6/6.3 업그레이드 가이드의 변경 항목을 프로젝트 의존성과 대조했다.
프로젝트는 Canvas/uGUI, Built-in UI 셰이더, Resources 및 StreamingAssets를 사용한다.
URP/HDRP, DOTS, 네트워크/광고/XR 패키지, UI Toolkit UXML/USS,
사용자 정의 Android Java/Gradle 플러그인은 발견되지 않았다.
기존 XTapJellyTouch.shader는 Built-in 경로를 유지한다. 셰이더 실제 컴파일은 미검증이다.
루트 app/는 별도 Java 앱이므로 Unity 6 이전 대상에서 제외한다.

## Cloud Build에서 반드시 맞출 값

- Unity version: `6000.3.24f1` 또는 해당 ProjectVersion을 읽는 Auto detect.
- Branch: `unity-prototype`.
- Project subfolder: `unity/XTapUnity`.
- 기존 Android 서명 설정을 유지해야 설치된 앱의 업데이트와 저장 데이터 보존을 기대할 수 있다.
- 첫 엔진 변경 빌드는 Clean Build로 실행한다. 진행 중인 빌드와 중복 실행하지 않는다.
- 로그: `X탑 UNITY EDITOR / actual=6000.3.24f1` 및 `X탑 BUILD FINGERPRINT` 확인.

Git 수정만으로 Cloud 대상에 고정한 2022.3.62f2 설정이 바뀌지는 않는다.
과거 빌드 상세 화면에는 당시 엔진 버전이 남는다. 새 설정과 다음 빌드에서 확인한다.
이 환경에서는 Cloud 설정 변경/Unity 6 Editor 실행/실제 컴파일/Android 빌드/
APK 설치/기기 동작을 확인하지 못했다. #76의 직접 실패 원인도 로그 없이는 확정할 수 없다.

## 검증

- C# 25개 문법 검사 통과 (UNITY_EDITOR/UNITY_ANDROID 분기 포함).
- 전투 570+21, 호감도 1023+56, 도감 528: 총 2198개 규칙/adapter 검사 통과.
- Resources/내장 이미지 38개, APK 도감 이미지 409개 디코딩 및 ZIP CRC 통과.
- YAML/JSON 파싱, 입력 backend/4개 UI 축, 앱 ID/ARM64/IL2CPP/버전 일치 확인.
- Git diff 공백 오류 없음.
이는 Unity 6 컴파일과 다르며, 새 PlayerSettings를 Editor에서 import한 검증은 아니다.
실제 Unity 검증에서는 한글 폰트, UI 터치/가방 드래그, 전투 HIT/실드,
대장간 타격/효과음, 머신 보상, 저장 데이터 로드부터 확인해야 한다.

## 공식 근거 (2026-09-28 확인)

- https://unity.com/releases/editor/whats-new/6000.3.24f1
- https://services.api.unity.com/unity/editor/release/v1/releases?limit=10&version=6000.3
- https://unity.com/releases/unity-6/support
- https://docs.unity.com/en-us/build-automation/reference/supported-unity-versions
- https://docs.unity3d.com/6000.0/Documentation/Manual/UpgradeGuideUnity6.html
- https://docs.unity3d.com/6000.3/Documentation/Manual/UpgradeGuideUnity63.html
- https://docs.unity3d.com/6000.3/Documentation/Manual/com.unity.ugui.html
- https://docs.unity3d.com/6000.3/Documentation/ScriptReference/PlayerSettings.SetScriptingBackend.html
- https://docs.unity3d.com/6000.3/Documentation/ScriptReference/PlayerSettings.SetApplicationIdentifier.html
- https://github.com/Unity-Technologies/UnityCsReference/tree/6000.3
- https://github.com/Unity-Technologies/XR-Interaction-Toolkit-Examples/blob/main/ProjectSettings/ProjectSettings.asset

2022.3.62f2 경고의 공식 근거는 아래 폐기 공지다. 11.72에서 고친 f2 revision은
유효한 값이지만 Deprecated 상태를 해제하지 않았다.
https://discussions.unity.com/t/unity-devops-build-automation-2026-dependency-deprecation-cycle/1724029
