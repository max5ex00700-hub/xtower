using UnityEngine;

public static class XTapBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        if (Object.FindFirstObjectByType<XTapBattleController>() != null) return;
        var root = new GameObject("XTapGame");
        Object.DontDestroyOnLoad(root);
        root.AddComponent<XTapBattleController>();
    }
}
