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
                throw new BuildFailedException("X탑 11.60 필수 메인 UI 자산 누락: " + required[i]);

            ValidatePngContainer(path, "메인 UI " + required[i]);
            ValidateRuntimeImage(path, 1, 1, "메인 UI " + required[i]);
        }

        string forgeDuelBackgroundPath = Path.Combine(
            Application.dataPath,
            "Resources/XTapBlacksmithUI/forge_duel_background.bytes"
        );
        string forgeDuelAtlasPath = Path.Combine(
            Application.dataPath,
            "Resources/XTapBlacksmithUI/forge_duel_atlas.bytes"
        );

        if (!File.Exists(forgeDuelBackgroundPath))
            throw new BuildFailedException("X탑 11.60 대장간 9:16 듀얼 배경 에셋 누락.");
        if (!File.Exists(forgeDuelAtlasPath))
            throw new BuildFailedException("X탑 11.60 대장간 천사/악마/모루 atlas 에셋 누락.");

        // 11.60: complete chunks can still contain damaged compressed bytes.
        // Check each chunk's CRC before Unity performs the actual image decode.
        ValidatePngContainer(forgeDuelBackgroundPath, "대장간 9:16 듀얼 배경");
        ValidatePngContainer(forgeDuelAtlasPath, "대장간 천사/악마/모루 atlas");

        ValidateRuntimeImage(
            forgeDuelBackgroundPath,
            300,
            500,
            "대장간 9:16 듀얼 배경"
        );
        ValidateRuntimeImage(
            forgeDuelAtlasPath,
            400,
            200,
            "대장간 천사/악마/모루 atlas"
        );

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
                throw new BuildFailedException("X탑 11.60 블록 머신 에셋 누락: " + gachaAssets[i]);

            int minWidth = gachaAssets[i].IndexOf("machine", StringComparison.OrdinalIgnoreCase) >= 0 ? 500 : 300;
            int minHeight = gachaAssets[i].IndexOf("machine", StringComparison.OrdinalIgnoreCase) >= 0 ? 900 : 300;
            ValidateRuntimeImage(path, minWidth, minHeight, "블록 머신 " + gachaAssets[i]);
        }

        string sigilBeatAudioPath = Path.Combine(
            Application.dataPath,
            "Resources/XTapSigilBeat/x_sigil_beat_stage1.ogg"
        );
        if (!File.Exists(sigilBeatAudioPath))
            throw new BuildFailedException("X탑 11.60 X SIGIL BEAT 음악 에셋 누락.");

        byte[] sigilBeatAudio = File.ReadAllBytes(sigilBeatAudioPath);
        if (sigilBeatAudio.Length < 100000 ||
            sigilBeatAudio[0] != (byte)'O' ||
            sigilBeatAudio[1] != (byte)'g' ||
            sigilBeatAudio[2] != (byte)'g' ||
            sigilBeatAudio[3] != (byte)'S')
            throw new BuildFailedException("X탑 11.60 X SIGIL BEAT OGG 음악 에셋 손상.");

        string visualPackPath = Path.Combine(
            Application.dataPath,
            "Resources/XTapSigilBeat/visual_pack.bytes"
        );
        if (!File.Exists(visualPackPath))
            throw new BuildFailedException("X탑 11.60 X SIGIL BEAT visual pack 누락.");

        string visualPack = File.ReadAllText(visualPackPath);
        if (visualPack.Length < 1000000)
            throw new BuildFailedException("X탑 11.60 X SIGIL BEAT visual pack 크기 비정상.");

        SigilVisualPackData pack = JsonUtility.FromJson<SigilVisualPackData>(visualPack);
        if (pack == null)
            throw new BuildFailedException("X탑 11.60 X SIGIL BEAT visual pack JSON 파싱 실패.");

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

        Debug.Log("X탑 11.60 UI 검증 완료: 메인 + 머신 + 대장간 듀얼 + X SIGIL BEAT 음악 + 13개 시각 에셋 실제 디코딩 정상.");
    }


    static void ValidatePackedImage(string encoded, int minWidth, int minHeight, string label)
    {
        if (string.IsNullOrEmpty(encoded))
            throw new BuildFailedException("X탑 11.60 X SIGIL BEAT " + label + " 데이터 누락.");

        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(encoded);
        }
        catch (Exception e)
        {
            throw new BuildFailedException("X탑 11.60 X SIGIL BEAT " + label + " base64 손상: " + e.Message);
        }

        Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        try
        {
            if (!texture.LoadImage(bytes, false))
                throw new BuildFailedException("X탑 11.60 X SIGIL BEAT " + label + " Unity 이미지 디코딩 실패.");

            if (texture.width < minWidth || texture.height < minHeight)
                throw new BuildFailedException(
                    "X탑 11.60 X SIGIL BEAT " + label + " 해상도 비정상: " +
                    texture.width + "x" + texture.height);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(texture);
        }
    }

    static void ValidatePngContainer(string path, string label)
    {
        byte[] bytes = File.ReadAllBytes(path);
        if (bytes == null || bytes.Length < 33)
            throw new BuildFailedException("X탑 11.60 " + label + " PNG 파일 크기 비정상.");

        if (bytes[0] != 0x89 || bytes[1] != 0x50 || bytes[2] != 0x4E || bytes[3] != 0x47 ||
            bytes[4] != 0x0D || bytes[5] != 0x0A || bytes[6] != 0x1A || bytes[7] != 0x0A)
            throw new BuildFailedException("X탑 11.60 " + label + " PNG 시그니처 손상.");

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
                    "X탑 11.60 " + label + " PNG 잘림: " +
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
                    "X탑 11.60 " + label + " PNG CRC 손상: " + chunkType +
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
                "X탑 11.60 " + label + " PNG 구조 손상: " +
                "IHDR=" + sawIHDR + " IDAT=" + sawIDAT + " IEND=" + sawIEND + ".");

        if (pos != bytes.Length)
            throw new BuildFailedException(
                "X탑 11.60 " + label + " PNG 끝 뒤 불필요 데이터: " +
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

    static void ValidateRuntimeImage(string path, int minWidth, int minHeight, string label)
    {
        byte[] bytes = File.ReadAllBytes(path);
        if (bytes == null || bytes.Length < 1024)
            throw new BuildFailedException("X탑 11.60 " + label + " 파일 크기 비정상.");

        Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        try
        {
            if (!texture.LoadImage(bytes, false))
                throw new BuildFailedException("X탑 11.60 " + label + " Unity 이미지 디코딩 실패.");

            if (texture.width < minWidth || texture.height < minHeight)
                throw new BuildFailedException(
                    "X탑 11.60 " + label + " 해상도 비정상: " +
                    texture.width + "x" + texture.height +
                    " / 최소 " + minWidth + "x" + minHeight);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(texture);
        }
    }

    public void OnPreprocessBuild(BuildReport report)
    {
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
        PlayerSettings.bundleVersion = "11.60-png-integrity-" + shortCommit;
        PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, "com.xtower.game.unity");

        Debug.Log("X탑 BUILD FINGERPRINT / branch=" + (gitBranch ?? "local") + " / commit=" + (gitCommit ?? "local"));
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
        PlayerSettings.SplashScreen.show = true;
        PlayerSettings.SplashScreen.showUnityLogo = true;
        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel24;
        PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
        PlayerSettings.Android.bundleVersionCode = 1160;

        // 64-bit Android is required for current 64-bit-only devices.
        PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;

        EditorUserBuildSettings.buildAppBundle = false;
        Debug.Log("X탑 Unity Android ARM64 / IL2CPP build settings applied.");
    }
}
#endif
