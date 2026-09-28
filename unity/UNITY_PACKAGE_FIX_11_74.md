# 11.74 — Cloud Build #84 패키지 해석 실패 수정

## 확인된 원인

사용자 제공 `14569807222025-xtop-default-android-84.log.txt` 414줄을 확인했다.

- Git revision: `745f4534fef48cd8c33be56980ebdd3e49f18ab4` (11.73).
- 실제 실행 엔진: `6000.3.24f1 (4e7b9b5b6244)`.
- 저장소 checkout: 17초에 완료. Unity 라이선스 갱신도 성공했다.
- 2026-09-29 06:56:40 KST (2026-09-28 21:56:40 UTC), 패키지 해석에서 종료:

```text
An error occurred while resolving packages:
  Project has invalid dependencies:
    com.unity.modules.inputlegacy: Package [com.unity.modules.inputlegacy@1.0.0] cannot be found
```

이후 `Unity player export failed`는 위 오류에 따른 결과다.
앞의 `LogAssemblyErrors (0ms)`는 Domain Reload Profiling의 항목으로,
구체적인 C# 컴파일 오류 메시지가 아니다. 현재 로그에는 프로젝트 C# 컴파일
성공이나 XTapBuildConfig pre-build 실행을 입증하는 기록이 없다.
로그가 없던 #76/#81의 원인까지 이 오류로 소급 확정하지 않는다.

## 수정

- `Packages/manifest.json`에서 존재하지 않는
  `com.unity.modules.inputlegacy: 1.0.0` 의존성 한 항목을 제거한다.
- Legacy Input은 Unity 내장 Input Manager/Input API다. 기존 터치 입력에
  별도의 inputlegacy 패키지를 설치할 필요가 없다.
- `activeInputHandler: 0`, InputManager의 4개 축, StandaloneInputModule,
  UnityEngine.Input 기반 전투/시작 화면/머신 입력 코드는 유지한다.
- uGUI 2.0.0 및 나머지 UI/IMGUI/오디오/이미지/JSON/웹 요청 모듈은 유지한다.
- 앱 버전은 `11.74-unity6-<commit>`, versionCode는 `1174`로 함께 올린다.
- Unity 엔진, Android 서명/패키지 ID, 게임 규칙, 에셋, 저장 키는 변경하지 않는다.

## 검증 범위

- 원격 HEAD와 checkout된 HEAD가 #84 revision과 같은 것을 수정 전에 확인했다.
- manifest JSON 파싱과 수정 전후 의존성 차이를 확인했다.
- 남은 6개 com.unity.modules 항목을 Unity 6.3 공식 내장 패키지 목록과 대조했다.
- 앱/Android 버전의 C# 및 ProjectSettings 일치, 입력 설정 보존,
  런타임 코드·에셋 불변과 `git diff --check`를 확인했다.
- 이 환경의 PATH에서 Unity Editor 실행 파일을 찾지 못했다. UPM 재실행,
  실제 Unity 컴파일, APK 생성/설치/실기기 성공은 아직 검증하지 않았다.
- 변경되지 않은 전투/도감/호감도 adapter 검사를 재실행해 패키지 해결 검증으로
  대신하지 않는다. 11.73에서 통과한 검사도 이 UPM 오류를 검출하지 못했다.

## 다음 Cloud Build

Unity `6000.3.24f1`, 자동 감지 해제, `unity-prototype`,
`unity/XTapUnity` 및 기존 서명을 유지한다. 이미 실행/대기 중인 빌드가 있다면
새 빌드를 중복 시작하지 않는다. 다음 빌드는 이 수정 커밋의 revision을 사용해야 한다.

먼저 패키지 해석을 통과하는지 확인하고, 이후 실제 C# 컴파일과
`X탑 UNITY EDITOR`, `X탑 BUILD FINGERPRINT` 및 최종 결과를 확인한다.
소스 수정 완료와 Cloud Build 성공을 구분한다.

## 공식 근거

- Unity 6.3 Input: https://docs.unity3d.com/6000.3/Documentation/Manual/Input.html
- 내장 패키지 목록: https://docs.unity3d.com/6000.3/Documentation/Manual/pack-build.html
- Android Active Input Handling: https://docs.unity3d.com/6000.3/Documentation/Manual/class-PlayerSettingsAndroid.html
