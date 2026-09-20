# X탑 Unity - 폰만으로 APK 만들기

## 이미 준비된 것
- Repository: max5ex00700-hub/xtower
- Branch: unity-prototype
- Unity project subfolder: unity/XTapUnity
- Unity version: 2022.3.62f1
- Android package: com.xtower.game.unity
- Version code: 1025
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
9. Unity version = Auto detect
10. Android signing = Auto-generated debug keystore
11. 저장 후 Build를 누른다.
12. Build history에서 APK를 다운로드한다.

## 다음 빌드부터
같은 Android Configuration에서 Build만 누르면 된다.

## 현재 1-1 프로토타입
- 조훈 HP 100 / ATK 5 / DEF 1
- 1층 보스 HP 100 / ATK 3 / DEF 1
- 공격 / 회피 / 자동 / 재시작
- 타격 플래시, 화면 흔들림, 보스 돌진
- 승리 후 포획
- 감옥 카운트

이 버전의 목적은 먼저 '폰 -> 클라우드 -> 설치 가능한 Unity APK' 파이프라인을 검증하는 것이다.
