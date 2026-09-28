# X탑 11.60 이미지 무결성 수정

기준 커밋: `9429a3e8bb581959ff21f95930edb137848c6a1a` (11.59).

11.59의 PNG 컨테이너 검사는 청크 길이와 IEND만 확인했다. 아래 네 파일은
청크가 끝까지 있어도 IDAT CRC가 틀렸고, zlib 스트림도 손상돼 있었다.
대장간 배경, 전투 버튼, 도감 버튼은 Pillow의 실제 이미지 로딩도 실패했다.
정보 탭은 Pillow 로딩만 통과했지만 CRC와 zlib 스트림 완결성 검사는 실패했다.

## 수정

기존 CRC와 zlib Adler-32를 수정하지 않고, 그 값에 일치하는 데이터 바이트를
복구했다. 후보는 CRC, zlib 압축 해제, Pillow verify/load를 모두 통과해야
채택했다. 이미지 크기, 팔레트, 투명도, PNG 청크 및 파일 길이는 그대로다.
복구된 네 이미지를 직접 열어 전투/도감/정보 버튼과 대장간 배경도 확인했다.

바이트 위치는 파일 시작 기준 0부터이며 값은 16진수다.

| 파일 | 복구 바이트 | 크기 |
| --- | --- | --- |
| XTapBlacksmithUI/forge_duel_background.bytes | 2619: 58→5A, 2620: B8→58 | 7,073 bytes, 300×533 |
| XTapMainUI/ref_fight_runtime.bytes | 11199: 92→C2 | 14,486 bytes, 368×240 |
| XTapMainUI/ref_codex_runtime.bytes | 1005: 56→55, 1007: 1D→1E, 1008: DF→5C | 2,622 bytes, 93×93 |
| XTapMainUI/ref_tab_runtime.bytes | 2257: CC→6C | 2,284 bytes, 50×101 |

XTapBuildConfig에 PNG 청크 CRC 검사를 추가했다. 실제 Unity LoadImage 검사는
유지한다. bundleVersion은 `11.60-png-integrity-<commit>`, versionCode는 1160이다.

이 검사로 저장된 바이트의 손상은 확인했다. 최초 전송/저장 과정 중 정확히
어느 단계에서 손상이 생겼는지는 기록이 없어 확정하지 않았다.

## 재현과 검증

Python 3과 Pillow가 필요하다. 저장소 루트에서 실행한다.

```bash
python3 tools/validate_unity_images.py --build-inputs --ref 9429a3e8bb581959ff21f95930edb137848c6a1a
python3 tools/validate_unity_images.py --build-inputs
python3 tools/validate_unity_images.py
```

- 11.59 필수 빌드 입력: 27개 중 위 4개 실패, 종료 코드 1.
- 수정 후 필수 빌드 입력: 27개 통과, 종료 코드 0.
- 전체 Resources 이미지 검사: 32개 중 아래 기존 파일 3개 실패.
- 이번 변경의 에셋 수정 범위는 위 네 파일의 총 7바이트다.

## 별도로 남아 있는 기존 아틀라스 손상

| 파일 | 확인된 문제 |
| --- | --- |
| XTapMainUI/main_atlas.png | IDAT는 27,091 bytes를 선언하나 파일 전체가 13,638 bytes로 잘림 |
| XTapMainUI/main_atlas_runtime.bytes | base64 디코딩 결과가 위 손상 PNG와 동일 |
| XTapBlacksmithUI/atlas.bytes | 256×512 JPEG 디코딩 실패; XTapBlacksmith 내장 복사본도 동일 |

이 세 파일은 11.59에서 시작된 문제가 아니다. 전체 Git 이력을 확인했으나
각 파일의 최초 추가 이후 정상 교체본은 없었다. 손실된 원본 그림을 임의로
재생성하거나 검사에서 성공으로 처리하지 않았다. 전체 검사 명령은 계속
실패를 보고한다. 이 파일들은 현재 XTapBuildConfig의 필수 27개 검사 대상에
포함돼 있지 않으며, 앱 화면에서의 영향은 Unity/실기기 확인이 필요하다.

## 빌드 상태

확인한 Cloud Build 로그는 `14569807222025-xtop-default-android-67.log.txt`,
총 1,436줄이다. Git revision은 `02f72d752f8f9432e9c1071df145035bd1894f88`
(11.58)이며 첫 빌드 중단 원인은 대장간 배경 LoadImage 실패다.
뒤의 `export directory is empty`는 빌드 중단 이후 발생한 결과다.

11.59/11.60 Cloud Build 성공 로그는 확인하지 않았다. 이 환경에는 Unity
실행 파일이 없어 Unity 컴파일, Texture2D.LoadImage 실행, APK 빌드 및
실기기 동작을 검증하지 않았다. Python 디코딩 통과를 Unity 빌드 성공으로
해석하면 안 된다.
