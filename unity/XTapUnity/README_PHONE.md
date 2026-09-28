# X탑 Unity 클라우드 Android 빌드

- GitHub: max5ex00700-hub/xtower
- 브랜치: unity-prototype
- Project subfolder: unity/XTapUnity
- Unity: Unity 6.3 LTS / 6000.3.24f1 (4e7b9b5b6244)
- 플랫폼: Android ARM64 / IL2CPP / APK
- 패키지: com.xtower.game.unity
- 테스트 서명: Unity Build Automation의 Auto-generated debug keystore

앱 버전과 Android versionCode는 Assets/Editor/XTapBuildConfig.cs가 빌드 전에 설정합니다.
현재 소스: 11.74-unity6-<commit> / 1174. 실제 설치 버전은 APK와 빌드 로그로 확인해야 합니다.

시작 씬은 Assets/Main.unity이며 XTapBootstrap이 런타임 UI를 생성합니다.
캐릭터 데이터는 StreamingAssets/xtop_source.part1, part2를 결합해서 읽습니다.
루트 app/의 Java 앱은 이 Unity 프로젝트와 별개입니다.

2026-09-28 Git 점검 결과와 미해결 항목은 ../GIT_AUDIT_11_72.md에 기록합니다.
Unity Cloud Build #76은 로그와 실제 사용 커밋을 확인하지 못했습니다.

11.73 Unity 버전 변경 근거와 Cloud 설정은 ../UNITY_VERSION_11_73.md를 참고합니다.
Cloud Build가 f2로 고정되어 있으면 Unity version을 6000.3.24f1로 변경해야 합니다.
#84 로그에서 Unity 6000.3.24f1 실행과 11.73 소스 checkout은 확인했습니다.
11.74는 패키지 해석을 중단시킨 잘못된 inputlegacy 의존성을 제거합니다.
수정 근거와 다음 확인 항목은 ../UNITY_PACKAGE_FIX_11_74.md에 기록합니다.
Unity 6 실제 컴파일 및 Cloud Build 성공은 아직 확인하지 못했습니다.
