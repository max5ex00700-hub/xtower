# 대장간 실제 모루 녹음 교체 11.67

기준 HEAD: `e573a8cce73622153037448ab70512cec68be1d3` (11.66).
수정 시작 시 원격 unity-prototype과 로컬 HEAD가 일치하고 작업 트리가 깨끗함을 확인했다.

## 수정 이유와 내용

사용자가 기존 효과음이 모루를 치는 ‘깡깡’ 소리와 다르다고 지적했다.
기존 `tools/build_forge_sfx.py`는 사인파·잡음을 합성한 소리였다.
이를 BigSoundBank **Anvil #1 / Pablo BERGEL**의 실제 모루 타격 녹음으로 교체했다.
출처 페이지의 CC0 및 상업적 이용·수정·재배포 허용을 확인했다.

- `forge_hammer_angel.wav`: 첫 번째 녹음 타격, 0.58초.
- `forge_hammer_demon.wav`: 두 번째 녹음 타격, 0.62초.
- `forge_hammer_final.wav`: 세 번째 녹음 타격, 1.00초. 마지막 타격은 잔향을 길게 남긴다.
- 원래 금속 음색과 음높이를 유지하고 앞 무음 제거, DC 제거, 샘플레이트 변환,
  음량 조정, 끝 페이드만 적용했다.
- 기존 합성 스크립트도 실제 녹음 편집 스크립트로 교체했다. 재실행해도 합성음으로 되돌아가지 않는다.
- 재현 가능한 원본 무손실 발췌와 출처를 `tools/audio_sources/`에 포함했다.
- 기존 WAV 경로와 .meta GUID를 유지한다. C# 접촉 시점, 효과음 옵션, 확률·보상·화면 코드는 변경하지 않는다.

## 검증 결과와 한계

- 세 WAV 모두 실제 디코딩 후 모노 / 44.1 kHz / 16-bit PCM 확인.
- RIFF, fmt, data 길이와 파일 끝을 BuildConfig가 요구하는 구조와 대조했다.
- 세 파일 모두 타격 시작 약 1ms, 끝 샘플 0, 파일 피크 0.82 / 0.82 / 0.88.
- 원본 발췌의 고주파 타격 분석에서 별도 타격 세 번을 확인했다.
- 기존 C#의 접촉 시각·볼륨으로 7타를 합성한 피크 0.854565: 디지털 클리핑 없음.
- `forge-review-11.67/forge-recorded-anvil-design.mp4`는 실제 게임 PNG/WAV를 사용한
  **설계 미리보기**다. 6회 교대 타격과 마지막 1타를 기존 명목 접촉 시각에 배치했다.
- 실제 Unity 실행, Cloud Build, APK/휴대폰 청취 검증은 하지 않았다.
  음색에 대한 최종 청취 판단 및 실제 기기의 오디오 지연은 별도 확인이 필요하다.

버전: `11.67-forge-recorded-anvil-<commit>`, Android versionCode `1167`.

출처: https://bigsoundbank.com/anvil-1-s3589.html
원본 SHA·라이선스·구간: `tools/audio_sources/README.md`.
