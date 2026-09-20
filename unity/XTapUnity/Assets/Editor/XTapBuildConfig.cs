#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

public sealed class XTapBuildConfig : IPreprocessBuildWithReport
{
    public int callbackOrder { get { return -1000; } }

    public void OnPreprocessBuild(BuildReport report)
    {
        PlayerSettings.productName = "X탑";
        PlayerSettings.companyName = "XTap";
        PlayerSettings.bundleVersion = "10.25-unity-p1";
        PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, "com.xtower.game.unity");
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel24;
        PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
        PlayerSettings.Android.bundleVersionCode = 1025;
        PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.Mono2x);
        EditorUserBuildSettings.buildAppBundle = false;
        Debug.Log("X탑 Unity Android build settings applied.");
    }
}
#endif
