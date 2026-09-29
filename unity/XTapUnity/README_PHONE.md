# X탑 Unity 클라우드 Android 빌드

- GitHub: max5ex00700-hub/xtower
- 브랜치: unity-prototype
- Project subfolder: unity/XTapUnity
- Unity: Unity 6.3 LTS / 6000.3.24f1 (4e7b9b5b6244)
- 플랫폼: Android ARM64 / IL2CPP / APK
- 패키지: com.xtower.game.unity
- 테스트 서명: Unity Build Automation의 Auto-generated debug keystore

앱 버전과 Android versionCode는 Assets/Editor/XTapBuildConfig.cs가 빌드 전에 설정합니다.
현재 소스: 11.78-unity6-<commit> / 1178. 실제 설치 버전은 APK와 빌드 로그로 확인해야 합니다.

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

11.75는 모든 오디오 재생 경로에 필요한 AudioListener 누락을 수정합니다.
게임 루트 생성 시 리스너를 자동으로 붙이고, 실제 Unity 컴포넌트 의존성 검사를 빌드 전 실행합니다.
원인·로컬 검증·기기 확인 항목은 ../AUDIO_OUTPUT_FIX_11_75.md를 참고합니다.
11.75 실제 Unity 컴파일, Cloud Build 및 기기 청취 결과는 아직 확인하지 못했습니다.

11.76은 블럭 머신의 0 선택 후 포획/캐릭터 전용 블럭 획득에 같은 1% 판정을 적용합니다.
기존 포획은 1%였고 전용 블럭의 확정 지급을 1%로 변경했습니다.
../GACHA_ZERO_CHANCE_11_76.md를 참고합니다. 11.76 Cloud Build 및 기기 검증은 미완료입니다.

11.77은 감옥 UI를 대형 캐릭터 배경·초상화 목록·금속 프레임·교감 정보 패널로 재구성합니다.
../JAIL_UI_11_77.md 및 ../jail-review-11.77/jail-layout-design.jpg를 참고합니다.
11.75 소리와 11.76 블럭 머신 확률 수정도 포함하며, 11.77 실기기 동작은 아직 확인하지 못했습니다.

11.78은 6층 전투 이미지에 섞인 5층 캐릭터 31장을 차단합니다.
확인된 6층 이미지 10장으로 전투·도감 연결을 통일합니다.
../CHARACTER_ART_FIX_11_78.md를 참고합니다. 실제 Unity 컴파일 및 기기 검증은 미완료입니다.
