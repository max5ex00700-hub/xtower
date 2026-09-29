# X탑 전체 무음 수정 11.75

기준: `99f109ee761e707be46b5bb46b0a84066a609d42`, `unity-prototype`.
2026-09-29 사용자가 “모든 소리가 없어졌다”고 보고했다.
사용자의 실제 설치 APK 버전 및 기기 실행 로그는 이 작업에서 확보하지 못했다.

## 확인한 결함

- 유일한 빌드 씬 `Assets/Main.unity`에는 카메라·AudioListener가 없다.
- `XTapBootstrap`은 빈 `XTapGame`을 만들고 `DontDestroyOnLoad`를 적용한 뒤
  `XTapBattleController`를 추가한다. UI는 ScreenSpaceOverlay이므로 카메라 생성 경로도 없다.
- 수정 전 Unity 소스 전체 검색에서 AudioListener 생성 또는 필수 컴포넌트 선언이 없었다.
- 반면 전투 음성/효과음, 승리음, 대장간, 미니게임의 AudioSource와 재생 코드는 존재한다.
  `com.unity.modules.audio`도 manifest에 포함되어 있다.
- AudioListener 없는 실행 구성은 모든 Unity 오디오 출력이 들리지 않는 원인이 된다.
  Unity 버전 변경 자체가 이 결함을 만들었다거나 사용자 기기에서 유일한 원인이라고 단정하지 않는다.

## 수정

- `XTapBattleController`에 `[RequireComponent(typeof(AudioListener))]`를 선언했다.
  Bootstrap이 컨트롤러를 추가할 때 같은 영속 게임 루트에 리스너가 자동 추가된다.
  화면 전환은 하위 UI를 열고 닫으므로 리스너는 계속 활성 상태로 남는다.
- Awake에서 리스너 활성 여부, 전역 음량, 일시정지 여부, 출력 샘플레이트를 로그로 남긴다.
- BuildConfig에 실제 Unity AddComponent 동작 검사를 추가했다.
  비활성 임시 오브젝트에 컨트롤러를 추가하여, 빈 루트와 기존 리스너가 있는 루트 모두
  활성화된 리스너 컴포넌트가 정확히 하나인지 검사한다. 임시 오브젝트는 항상 제거한다.
  이 검사는 다음 Unity 빌드에서 실행되며 로컬에서 실행했다고 주장하지 않는다.
- 기존 효과음 옵션과 사용자 저장값을 유지한다. 음원, 음색, 음량, 전투 규칙을 바꾸지 않는다.
- 엔진은 `6000.3.24f1`, 앱은 `11.75-unity6-<commit>`, versionCode는 `1175`다.

## 검증

- 전투 음성 17개, 전투 효과음 5개, 모루 타격음 3개, 원본 APK의 승리음 1개:
  Base64/ZIP 추출 후 WAV 컨테이너·PCM 길이를 확인하고 FFmpeg로 끝까지 디코딩했다.
  모두 유한한 비영(非零) 파형을 포함한다.
- 미니게임 OGG 1개도 FFmpeg로 전체 디코딩했고 무음 파일이 아님을 확인했다.
- 프로젝트 C# 25개를 Tree-sitter로 파싱하여 문법 오류가 없음을 확인했다.
  이는 Unity API 컴파일 검증이 아니다.
- 리스너 필수 선언/영속 루트 생성 경로, 오디오 모듈, 11.75/1175 일치 및
  `git diff --check`를 확인했다.
- 기존 Resources/StreamingAssets 및 음원 import 설정은 변경하지 않았다.
- Unity 실행 파일이 로컬 PATH에 없어 실제 Unity 컴파일과 출력 청취는 수행하지 못했다.

## 다음 확인

Cloud 버전·브랜치·프로젝트 하위 폴더·서명 설정을 유지한다.
진행 중인 빌드가 없다면 최신 커밋으로 한 번 빌드한다.

1. 빌드 로그 `X탑 AUDIO_PREFLIGHT / new and existing listener dependencies passed` 확인.
2. 설치 버전 `11.75-unity6-<commit> (1175)` 확인.
3. 효과음 ON에서 전투 타격/음성/방패, 승리음, 대장간, 미니게임 음악 청취.
4. 메인↔전투↔대장간 전환과 앱을 내렸다 복귀한 뒤 재생 확인.
5. 여전히 무음이면 `X탑 AUDIO` 실행 로그 및 실제 설치 버전을 확인한다.

별도 확인 사항: 현재 메인 화면의 배경음악 옵션은 저장/표시만 하며 메인 BGM 재생 코드는 없다.
미니게임 OGG 재생은 존재한다. 이번 수정에서 새 음악을 추가하거나 옵션 의미를 변경하지 않았다.

공식 근거:
- https://docs.unity3d.com/6000.3/Documentation/Manual/class-AudioListener.html
- https://docs.unity3d.com/6000.3/Documentation/ScriptReference/RequireComponent.html
