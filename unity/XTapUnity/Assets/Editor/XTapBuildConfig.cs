#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.Build;
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
                throw new BuildFailedException("X탑 11.11 필수 메인 UI 자산 누락: " + required[i]);

            try
            {
                string encoded = File.ReadAllText(path).Trim();
                byte[] png = Convert.FromBase64String(encoded);
                if (png.Length < 128 ||
                    png[0] != 0x89 || png[1] != 0x50 || png[2] != 0x4E || png[3] != 0x47)
                    throw new Exception("PNG signature invalid");
            }
            catch (Exception e)
            {
                throw new BuildFailedException("X탑 11.11 메인 UI 자산 손상: " + required[i] + " / " + e.Message);
            }
        }

        Debug.Log("X탑 11.11 메인 UI 검증 완료: 기준 이미지 버튼 9개 정상.");
    }

    public void OnPreprocessBuild(BuildReport report)
    {
        ValidateReferenceUiAssets();

        string gitBranch = Environment.GetEnvironmentVariable("GIT_BRANCH");
        string gitCommit = Environment.GetEnvironmentVariable("GIT_COMMIT");
        string shortCommit = string.IsNullOrEmpty(gitCommit)
            ? "local"
            : gitCommit.Substring(0, Math.Min(8, gitCommit.Length));

        PlayerSettings.productName = "X탑";
        PlayerSettings.companyName = "XTap";
        PlayerSettings.bundleVersion = "11.11-main-ref-" + shortCommit;
        PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, "com.xtower.game.unity");

        Debug.Log("X탑 BUILD FINGERPRINT / branch=" + (gitBranch ?? "local") + " / commit=" + (gitCommit ?? "local"));
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
        PlayerSettings.SplashScreen.show = true;
        PlayerSettings.SplashScreen.showUnityLogo = true;
        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel24;
        PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
        PlayerSettings.Android.bundleVersionCode = 1111;

        // 64-bit Android is required for current 64-bit-only devices.
        PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;

        EditorUserBuildSettings.buildAppBundle = false;
        Debug.Log("X탑 Unity Android ARM64 / IL2CPP build settings applied.");
    }
}
#endif
