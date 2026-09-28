# X탑 Unity 클라우드 Android 빌드

- GitHub: max5ex00700-hub/xtower
- 브랜치: unity-prototype
- Project subfolder: unity/XTapUnity
- Unity: 2022.3.62f2 (7670c08855a9)
- 플랫폼: Android ARM64 / IL2CPP / APK
- 패키지: com.xtower.game.unity
- 테스트 서명: Unity Build Automation의 Auto-generated debug keystore

앱 버전과 Android versionCode는 Assets/Editor/XTapBuildConfig.cs가 빌드 전에 설정합니다.
현재 소스: 11.72-git-audit-<commit> / 1172. 실제 설치 버전은 APK와 빌드 로그로 확인해야 합니다.

시작 씬은 Assets/Main.unity이며 XTapBootstrap이 런타임 UI를 생성합니다.
캐릭터 데이터는 StreamingAssets/xtop_source.part1, part2를 결합해서 읽습니다.
루트 app/의 Java 앱은 이 Unity 프로젝트와 별개입니다.

2026-09-28 Git 점검 결과와 미해결 항목은 ../GIT_AUDIT_11_72.md에 기록합니다.
Unity Cloud Build #76은 로그와 실제 사용 커밋을 확인하지 못했습니다.
