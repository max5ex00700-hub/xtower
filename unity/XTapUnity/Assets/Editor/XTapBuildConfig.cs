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
                throw new BuildFailedException("X탑 11.45 필수 메인 UI 자산 누락: " + required[i]);

            try
            {
                byte[] png = File.ReadAllBytes(path);
                if (png.Length < 128 ||
                    png[0] != 0x89 || png[1] != 0x50 || png[2] != 0x4E || png[3] != 0x47 ||
                    png[4] != 0x0D || png[5] != 0x0A || png[6] != 0x1A || png[7] != 0x0A)
                    throw new Exception("PNG signature invalid");
            }
            catch (Exception e)
            {
                throw new BuildFailedException("X탑 11.45 메인 UI 자산 손상: " + required[i] + " / " + e.Message);
            }
        }

        string forgeWheelPath = Path.Combine(
            Application.dataPath,
            "Resources/XTapBlacksmithUI/forge_success_fail_wheel.bytes"
        );

        if (!File.Exists(forgeWheelPath))
            throw new BuildFailedException("X탑 11.45 대장간 SUCCESS/FAIL 룰렛 에셋 누락.");

        ValidateRuntimeImage(forgeWheelPath, 300, 300, "대장간 SUCCESS/FAIL 룰렛");

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
                throw new BuildFailedException("X탑 11.45 블록 머신 에셋 누락: " + gachaAssets[i]);

            int minWidth = gachaAssets[i].IndexOf("machine", StringComparison.OrdinalIgnoreCase) >= 0 ? 500 : 300;
            int minHeight = gachaAssets[i].IndexOf("machine", StringComparison.OrdinalIgnoreCase) >= 0 ? 900 : 300;
            ValidateRuntimeImage(path, minWidth, minHeight, "블록 머신 " + gachaAssets[i]);
        }

        string sigilBeatAudioPath = Path.Combine(
            Application.dataPath,
            "Resources/XTapSigilBeat/x_sigil_beat_stage1.ogg"
        );
        if (!File.Exists(sigilBeatAudioPath))
            throw new BuildFailedException("X탑 11.45 X SIGIL BEAT 음악 에셋 누락.");

        byte[] sigilBeatAudio = File.ReadAllBytes(sigilBeatAudioPath);
        if (sigilBeatAudio.Length < 100000 ||
            sigilBeatAudio[0] != (byte)'O' ||
            sigilBeatAudio[1] != (byte)'g' ||
            sigilBeatAudio[2] != (byte)'g' ||
            sigilBeatAudio[3] != (byte)'S')
            throw new BuildFailedException("X탑 11.45 X SIGIL BEAT OGG 음악 에셋 손상.");

        Debug.Log("X탑 11.45 UI 검증 완료: 메인 + 머신 + 대장간 + X SIGIL BEAT 음악 정상.");
    }


    static void ValidateRuntimeImage(string path, int minWidth, int minHeight, string label)
    {
        byte[] bytes = File.ReadAllBytes(path);
        if (bytes == null || bytes.Length < 1024)
            throw new BuildFailedException("X탑 11.45 " + label + " 파일 크기 비정상.");

        Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        try
        {
            if (!texture.LoadImage(bytes, false))
                throw new BuildFailedException("X탑 11.45 " + label + " Unity 이미지 디코딩 실패.");

            if (texture.width < minWidth || texture.height < minHeight)
                throw new BuildFailedException(
                    "X탑 11.45 " + label + " 해상도 비정상: " +
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
        PlayerSettings.bundleVersion = "11.45-x-sigil-beat-audio-" + shortCommit;
        PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, "com.xtower.game.unity");

        Debug.Log("X탑 BUILD FINGERPRINT / branch=" + (gitBranch ?? "local") + " / commit=" + (gitCommit ?? "local"));
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
        PlayerSettings.SplashScreen.show = true;
        PlayerSettings.SplashScreen.showUnityLogo = true;
        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel24;
        PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
        PlayerSettings.Android.bundleVersionCode = 1145;

        // 64-bit Android is required for current 64-bit-only devices.
        PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;

        EditorUserBuildSettings.buildAppBundle = false;
        Debug.Log("X탑 Unity Android ARM64 / IL2CPP build settings applied.");
    }
}
#endif
