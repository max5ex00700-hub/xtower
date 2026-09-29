#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

public sealed class XTapBuildConfig : IPreprocessBuildWithReport
{
    public int callbackOrder { get { return -1000; } }

    [Serializable]
    sealed class SigilVisualPackData
    {
        public string sigil_background;
        public string thumb_left;
        public string thumb_right;
        public string timing_ring;
        public string note_down;
        public string note_right;
        public string note_left;
        public string note_up;
        public string note_tap;
        public string slash_left;
        public string slash_right;
        public string judgement_frame;
        public string title_plate;
    }

    static void ValidateReferenceUiAssets()
    {
        // These atlases are still read by the main panels, inventory and forge
        // selection screen. They must not be omitted from the release gate.
        ValidatePanelAtlas("XTapBlacksmithUI/atlas.bytes", 256, 512, true);
        ValidatePanelAtlas("XTapMainUI/main_atlas_runtime.bytes", 512, 512, true);
        ValidatePanelAtlas("XTapMainUI/main_atlas.png", 512, 512, false);

        foreach (string name in XTapCombatCueSkin.RequiredNames)
        {
            string path = Path.Combine(Application.dataPath, "Resources/XTapCombatUI/" + name + ".bytes");
            if (!File.Exists(path)) throw new BuildFailedException("X탑 전투 표적 에셋 누락: " + name);
            ValidatePngContainer(path, "전투 표적 " + name);
            ValidateRuntimeImage(path, 512, 512, "전투 표적 " + name, true);
        }

        string baseDir = Path.Combine(Application.dataPath, "Resources/XTapMainUI");
        string[] required =
        {
            "ref_fight_runtime.bytes",
            "ref_challenge_runtime.bytes",
            "ref_option_runtime.bytes",
            "ref_codex_runtime.bytes",
            "ref_tab_runtime.bytes",
            "ref_nav_prev_runtime.bytes",
            "ref_nav_bag_runtime.bytes",
            "ref_nav_jail_runtime.bytes",
            "ref_nav_forge_runtime.bytes",
            "ref_nav_next_runtime.bytes"
        };

        for (int i = 0; i < required.Length; i++)
        {
            string path = Path.Combine(baseDir, required[i]);
            if (!File.Exists(path))
                throw new BuildFailedException("X탑 11.78 필수 메인 UI 자산 누락: " + required[i]);

            ValidatePngContainer(path, "메인 UI " + required[i]);
            ValidateRuntimeImage(path, 1, 1, "메인 UI " + required[i]);
        }

        string forgeDir = Path.Combine(Application.dataPath, "Resources/XTapBlacksmithUI");
        string[] forgeAssets = {"forge_duel_background.bytes", "forge_angel_poses.bytes",
            "forge_demon_poses.bytes", "forge_anvil.bytes"};
        for (int i = 0; i < forgeAssets.Length; i++)
        {
            string path = Path.Combine(forgeDir, forgeAssets[i]);
            if (!File.Exists(path))
                throw new BuildFailedException("X탑 11.78 대장간 필수 에셋 누락: " + forgeAssets[i]);
            ValidatePngContainer(path, "대장간 " + forgeAssets[i]);
            bool poseSheet = i == 1 || i == 2;
            ValidateRuntimeImage(path, i == 0 ? 900 : (poseSheet ? 1536 : 600),
                i == 0 ? 1600 : (poseSheet ? 512 : 400),
                "대장간 " + forgeAssets[i], i != 0, poseSheet ? 3 : 1);
        }

        foreach (string sound in new[] { "angel", "demon", "final" })
        {
            string assetPath = "Assets/Resources/XTapBlacksmithUI/forge_hammer_" + sound + ".wav";
            string path = Path.Combine(Application.dataPath, assetPath.Substring(7));
            byte[] wav = File.ReadAllBytes(path);
            if (wav.Length < 44 || System.Text.Encoding.ASCII.GetString(wav, 0, 4) != "RIFF" ||
                System.Text.Encoding.ASCII.GetString(wav, 8, 4) != "WAVE" ||
                BitConverter.ToUInt32(wav, 4) + 8L != wav.Length ||
                System.Text.Encoding.ASCII.GetString(wav, 12, 4) != "fmt " ||
                BitConverter.ToUInt32(wav, 16) != 16 || BitConverter.ToUInt16(wav, 20) != 1 ||
                BitConverter.ToUInt16(wav, 22) != 1 || BitConverter.ToUInt32(wav, 24) != 44100 ||
                BitConverter.ToUInt16(wav, 34) != 16 ||
                System.Text.Encoding.ASCII.GetString(wav, 36, 4) != "data" ||
                BitConverter.ToUInt32(wav, 40) + 44L != wav.Length)
                throw new BuildFailedException("X탑 11.78 대장간 PCM 타격음 손상: " + sound);
            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(assetPath);
            if (clip == null || clip.samples * 2L + 44L != wav.Length || clip.frequency != 44100)
                throw new BuildFailedException("X탑 11.78 대장간 타격음 Unity import 실패: " + sound);
        }

        string gachaUiDir = Path.Combine(Application.dataPath, "Resources/XTapGachaUI");
        string[] gachaAssets =
        {
            "block_gear_machine.bytes",
            "block_gear_wheel.bytes"
        };

        for (int i = 0; i < gachaAssets.Length; i++)
        {
            string path = Path.Combine(gachaUiDir, gachaAssets[i]);
            if (!File.Exists(path))
                throw new BuildFailedException("X탑 11.78 블록 머신 에셋 누락: " + gachaAssets[i]);

            int minWidth = gachaAssets[i].IndexOf("machine", StringComparison.OrdinalIgnoreCase) >= 0 ? 500 : 300;
            int minHeight = gachaAssets[i].IndexOf("machine", StringComparison.OrdinalIgnoreCase) >= 0 ? 900 : 300;
            bool isWheel = gachaAssets[i] == "block_gear_wheel.bytes";
            if (isWheel) ValidatePngContainer(path, "투명 룰렛 장식");
            ValidateRuntimeImage(path, minWidth, minHeight, "블록 머신 " + gachaAssets[i], isWheel);
        }

        string sigilBeatAudioPath = Path.Combine(
            Application.dataPath,
            "Resources/XTapSigilBeat/x_sigil_beat_stage1.ogg"
        );
        if (!File.Exists(sigilBeatAudioPath))
            throw new BuildFailedException("X탑 11.78 X SIGIL BEAT 음악 에셋 누락.");

        byte[] sigilBeatAudio = File.ReadAllBytes(sigilBeatAudioPath);
        if (sigilBeatAudio.Length < 100000 ||
            sigilBeatAudio[0] != (byte)'O' ||
            sigilBeatAudio[1] != (byte)'g' ||
            sigilBeatAudio[2] != (byte)'g' ||
            sigilBeatAudio[3] != (byte)'S')
            throw new BuildFailedException("X탑 11.78 X SIGIL BEAT OGG 음악 에셋 손상.");

        string visualPackPath = Path.Combine(
            Application.dataPath,
            "Resources/XTapSigilBeat/visual_pack.bytes"
        );
        if (!File.Exists(visualPackPath))
            throw new BuildFailedException("X탑 11.78 X SIGIL BEAT visual pack 누락.");

        string visualPack = File.ReadAllText(visualPackPath);
        if (visualPack.Length < 1000000)
            throw new BuildFailedException("X탑 11.78 X SIGIL BEAT visual pack 크기 비정상.");

        SigilVisualPackData pack = JsonUtility.FromJson<SigilVisualPackData>(visualPack);
        if (pack == null)
            throw new BuildFailedException("X탑 11.78 X SIGIL BEAT visual pack JSON 파싱 실패.");

        ValidatePackedImage(pack.sigil_background, 800, 1400, "9:16 배경");
        ValidatePackedImage(pack.thumb_left, 200, 300, "왼엄지");
        ValidatePackedImage(pack.thumb_right, 200, 300, "오른엄지");
        ValidatePackedImage(pack.timing_ring, 150, 150, "타이밍 링");
        ValidatePackedImage(pack.note_tap, 100, 120, "TAP 노트");
        ValidatePackedImage(pack.note_left, 100, 120, "LEFT 노트");
        ValidatePackedImage(pack.note_right, 100, 120, "RIGHT 노트");
        ValidatePackedImage(pack.note_up, 100, 120, "UP 노트");
        ValidatePackedImage(pack.note_down, 100, 120, "DOWN 노트");
        ValidatePackedImage(pack.slash_left, 300, 100, "왼손 슬래시 FX");
        ValidatePackedImage(pack.slash_right, 300, 100, "오른손 슬래시 FX");
        ValidatePackedImage(pack.judgement_frame, 200, 120, "판정 프레임");
        ValidatePackedImage(pack.title_plate, 500, 70, "타이틀 플레이트");

        Debug.Log("X탑 11.78 UI 검증 완료: 메인 + 머신 + 대장간 듀얼 + X SIGIL BEAT 음악 + 13개 시각 에셋 실제 디코딩 정상.");
    }


    static void ValidatePackedImage(string encoded, int minWidth, int minHeight, string label)
    {
        if (string.IsNullOrEmpty(encoded))
            throw new BuildFailedException("X탑 11.78 X SIGIL BEAT " + label + " 데이터 누락.");

        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(encoded);
        }
        catch (Exception e)
        {
            throw new BuildFailedException("X탑 11.78 X SIGIL BEAT " + label + " base64 손상: " + e.Message);
        }

        Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        try
        {
            if (!texture.LoadImage(bytes, false))
                throw new BuildFailedException("X탑 11.78 X SIGIL BEAT " + label + " Unity 이미지 디코딩 실패.");

            if (texture.width < minWidth || texture.height < minHeight)
                throw new BuildFailedException(
                    "X탑 11.78 X SIGIL BEAT " + label + " 해상도 비정상: " +
                    texture.width + "x" + texture.height);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(texture);
        }
    }

    static void ValidatePanelAtlas(string relativePath, int width, int height, bool base64)
    {
        string path = Path.Combine(Application.dataPath, "Resources/" + relativePath);
        if (!File.Exists(path)) throw new BuildFailedException("UI 패널 아틀라스 누락: " + relativePath);
        byte[] bytes;
        try { bytes = base64 ? Convert.FromBase64String(File.ReadAllText(path).Trim()) : File.ReadAllBytes(path); }
        catch (Exception e) { throw new BuildFailedException("UI 패널 데이터 손상: " + relativePath + " / " + e.Message); }
        ValidatePngContainer(bytes, relativePath);
        ValidateRuntimeImage(bytes, width, height, relativePath);
    }

    static void ValidatePngContainer(string path, string label)
    {
        ValidatePngContainer(File.ReadAllBytes(path), label);
    }

    static void ValidatePngContainer(byte[] bytes, string label)
    {
        if (bytes == null || bytes.Length < 33)
            throw new BuildFailedException("X탑 11.78 " + label + " PNG 파일 크기 비정상.");

        if (bytes[0] != 0x89 || bytes[1] != 0x50 || bytes[2] != 0x4E || bytes[3] != 0x47 ||
            bytes[4] != 0x0D || bytes[5] != 0x0A || bytes[6] != 0x1A || bytes[7] != 0x0A)
            throw new BuildFailedException("X탑 11.78 " + label + " PNG 시그니처 손상.");

        bool sawIHDR = false;
        bool sawIDAT = false;
        bool sawIEND = false;
        int pos = 8;

        while (pos + 12 <= bytes.Length)
        {
            uint chunkLength =
                ((uint)bytes[pos] << 24) |
                ((uint)bytes[pos + 1] << 16) |
                ((uint)bytes[pos + 2] << 8) |
                bytes[pos + 3];

            string chunkType = new string(new[]
            {
                (char)bytes[pos + 4],
                (char)bytes[pos + 5],
                (char)bytes[pos + 6],
                (char)bytes[pos + 7]
            });

            long chunkEnd = pos + 12L + chunkLength;
            if (chunkEnd > bytes.Length)
                throw new BuildFailedException(
                    "X탑 11.78 " + label + " PNG 잘림: " +
                    chunkType + " 길이 " + chunkLength +
                    " / 파일 " + bytes.Length + " bytes.");

            int crcOffset = (int)chunkEnd - 4;
            uint storedCrc =
                ((uint)bytes[crcOffset] << 24) |
                ((uint)bytes[crcOffset + 1] << 16) |
                ((uint)bytes[crcOffset + 2] << 8) |
                bytes[crcOffset + 3];
            uint actualCrc = ComputePngCrc(bytes, pos + 4, crcOffset);
            if (storedCrc != actualCrc)
                throw new BuildFailedException(
                    "X탑 11.78 " + label + " PNG CRC 손상: " + chunkType +
                    " offset=" + pos + " stored=" + storedCrc.ToString("X8") +
                    " actual=" + actualCrc.ToString("X8") + ".");

            if (chunkType == "IHDR") sawIHDR = true;
            else if (chunkType == "IDAT") sawIDAT = true;
            else if (chunkType == "IEND")
            {
                sawIEND = true;
                pos = (int)chunkEnd;
                break;
            }

            pos = (int)chunkEnd;
        }

        if (!sawIHDR || !sawIDAT || !sawIEND)
            throw new BuildFailedException(
                "X탑 11.78 " + label + " PNG 구조 손상: " +
                "IHDR=" + sawIHDR + " IDAT=" + sawIDAT + " IEND=" + sawIEND + ".");

        if (pos != bytes.Length)
            throw new BuildFailedException(
                "X탑 11.78 " + label + " PNG 끝 뒤 불필요 데이터: " +
                (bytes.Length - pos) + " bytes.");
    }

    static uint ComputePngCrc(byte[] bytes, int start, int endExclusive)
    {
        uint crc = 0xFFFFFFFFu;
        for (int i = start; i < endExclusive; i++)
        {
            crc ^= bytes[i];
            for (int bit = 0; bit < 8; bit++)
                crc = (crc >> 1) ^ ((crc & 1u) != 0 ? 0xEDB88320u : 0u);
        }
        return crc ^ 0xFFFFFFFFu;
    }

    static void ValidateRuntimeImage(string path, int minWidth, int minHeight, string label, bool requireTransparency = false, int frameCount = 1)
    {
        ValidateRuntimeImage(File.ReadAllBytes(path), minWidth, minHeight, label, requireTransparency, frameCount);
    }

    static void ValidateRuntimeImage(byte[] bytes, int minWidth, int minHeight, string label, bool requireTransparency = false, int frameCount = 1)
    {
        if (bytes == null || bytes.Length < 1024)
            throw new BuildFailedException("X탑 11.78 " + label + " 파일 크기 비정상.");

        Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        try
        {
            if (!texture.LoadImage(bytes, false))
                throw new BuildFailedException("X탑 11.78 " + label + " Unity 이미지 디코딩 실패.");

            if (texture.width < minWidth || texture.height < minHeight)
                throw new BuildFailedException(
                    "X탑 11.78 " + label + " 해상도 비정상: " +
                    texture.width + "x" + texture.height +
                    " / 최소 " + minWidth + "x" + minHeight);

            if (frameCount > 1 && texture.width != texture.height * frameCount)
                throw new BuildFailedException("X탑 11.78 " + label + " 3프레임 정사각 셀 비율 오류.");
            if (requireTransparency)
            {
                Color32[] pixels = texture.GetPixels32();
                int[] transparent = new int[frameCount];
                int frameWidth = texture.width / frameCount;
                for (int i = 0; i < pixels.Length; i++)
                    if (pixels[i].a < 16) transparent[(i % texture.width) / frameWidth]++;
                for (int frame = 0; frame < frameCount; frame++)
                {
                    float fraction = (float)transparent[frame] / (frameWidth * texture.height);
                    if (fraction < .25f || fraction > .90f)
                        throw new BuildFailedException("X탑 11.78 " + label +
                            " 투명 영역 비정상, frame=" + frame + ": " + fraction);
                }
            }
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(texture);
        }
    }

    // Exercises the same outcome object used by the runtime, not a duplicate formula.
    static void ValidateForgeMechanics()
    {
        foreach (int chance in new[] { 0, 1, 10, 30, 60, 100 })
        {
            int angel = 0, slips = 0, doubles = 0, successes = 0;
            for (int baseRoll = 0; baseRoll < 100; baseRoll++)
                for (int twistRoll = 0; twistRoll < 100; twistRoll++)
                {
                    XTapForgeOutcome outcome = XTapForgeOutcome.FromRolls(chance, baseRoll, twistRoll);
                    if (outcome.AngelFinisher) angel++;
                    if (outcome.AngelFinisher && outcome.Reversal) slips++;
                    if (outcome.RewardMultiplier == 2) doubles++;
                    if (outcome.Success) successes++;
                    if (!outcome.Success && outcome.RewardMultiplier != 0)
                        throw new BuildFailedException("대장간 실패 보상 오류");
                }
            if (angel != chance * 100 || slips != chance || doubles != 100 - chance ||
                successes != 98 * chance + 100)
                throw new BuildFailedException("대장간 1% 반전 확률 오류: " + chance);
        }
        XTapForgeOutcome jackpot = XTapForgeOutcome.FromRolls(0, 0, 0);
        if (jackpot.EnhancedLevel(0) != 2 || jackpot.EnhancedLevel(9) != 11 ||
            jackpot.EnhancedLevel(19) != 20)
            throw new BuildFailedException("대장간 2배 강화/상한 오류");
        foreach (string[] lines in new[] { XTapForgeDialogue.AngelFinishers, XTapForgeDialogue.DemonFinishers })
        {
            if (lines.Length != 10) throw new BuildFailedException("대장간 막타 대사 개수 오류");
            for (int i = 0; i < lines.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i])) throw new BuildFailedException("빈 막타 대사");
                for (int j = 0; j < i; j++)
                    if (lines[i] == lines[j]) throw new BuildFailedException("중복 막타 대사");
            }
        }
        Debug.Log("X탑 FORGE_PREFLIGHT / 60000 outcomes and 20 finisher lines passed");
    }

    static void ValidateAudioListenerDependency()
    {
        // Exercise Unity's actual AddComponent/RequireComponent behavior used
        // by XTapBootstrap. An inactive probe avoids running gameplay Awake/Start.
        foreach (bool existingListener in new[] { false, true })
        {
            var probe = new GameObject("XTapAudioPreflight");
            probe.SetActive(false);
            try
            {
                if (existingListener) probe.AddComponent<AudioListener>();
                probe.AddComponent<XTapBattleController>();
                AudioListener[] listeners = probe.GetComponents<AudioListener>();
                if (listeners.Length != 1 || !listeners[0].enabled)
                    throw new BuildFailedException("X탑 오디오 출력 초기화 오류: 게임 루트에 활성 AudioListener 하나가 필요합니다.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(probe);
            }
        }
        Debug.Log("X탑 AUDIO_PREFLIGHT / new and existing listener dependencies passed");
    }

    public void OnPreprocessBuild(BuildReport report)
    {
        Debug.Log("X탑 UNITY EDITOR / actual=" + Application.unityVersion + " / project=6000.3.24f1");
        if (!Application.unityVersion.StartsWith("6000.3.", StringComparison.Ordinal))
            throw new BuildFailedException("X탑 11.78은 Unity 6.3 LTS 프로젝트입니다. Cloud Build Unity version을 6000.3.24f1로 설정하세요. 실제 버전: " + Application.unityVersion);

        int affinityChecks = XTapAffinityChecks.Run(state =>
            JsonUtility.FromJson<XTapAffinityState>(JsonUtility.ToJson(state)));
        Debug.Log("X탑 호감도 규칙/저장 왕복 검증: " + affinityChecks + " checks");
        Debug.Log("X탑 전투 타이밍/콤보/입력 검증: " + XTapCombatChecks.Run() + " checks");
        ValidateAudioListenerDependency();
        ValidateForgeMechanics();
        ValidateReferenceUiAssets();

        string gitBranch = Environment.GetEnvironmentVariable("GIT_BRANCH");
        string gitCommit = Environment.GetEnvironmentVariable("GIT_COMMIT");
        if (string.IsNullOrEmpty(gitCommit))
            gitCommit = Environment.GetEnvironmentVariable("BUILD_REVISION");

        if (!string.IsNullOrEmpty(gitBranch) &&
            gitBranch.IndexOf("unity-prototype", StringComparison.OrdinalIgnoreCase) < 0)
            throw new BuildFailedException(
                "X탑 Unity 빌드 브랜치 오류. unity-prototype이 아니라 " + gitBranch + " 를 빌드하려고 했습니다.");

        string shortCommit = string.IsNullOrEmpty(gitCommit)
            ? "local"
            : gitCommit.Substring(0, Math.Min(8, gitCommit.Length));

        PlayerSettings.productName = "X탑";
        PlayerSettings.companyName = "XTap";
        PlayerSettings.bundleVersion = "11.78-unity6-" + shortCommit;
        PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.xtower.game.unity");

        Debug.Log("X탑 BUILD FINGERPRINT / branch=" + (gitBranch ?? "local") + " / commit=" + (gitCommit ?? "local"));
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
        PlayerSettings.SplashScreen.show = true;
        PlayerSettings.SplashScreen.showUnityLogo = true;
        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel24;
        PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
        PlayerSettings.Android.bundleVersionCode = 1178;

        // 64-bit Android is required for current 64-bit-only devices.
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;

        EditorUserBuildSettings.buildAppBundle = false;
        Debug.Log("X탑 Unity Android ARM64 / IL2CPP build settings applied.");
    }
}
#endif
