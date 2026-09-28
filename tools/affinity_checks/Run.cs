// Standalone checks compile the actual affinity runtime files. PlayerPrefs and
// JsonUtility below are in-memory adapters, not substitutes for Unity validation.
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

static class Run
{
    static int count;
    static void Check(bool ok, string reason) { count++; if (!ok) throw new Exception(reason); }
    static void Reload() { typeof(XTapJailAffinity).GetMethod("ClearCache", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null); }
    static string Key(int id) { return "xtap_jail_affinity_v1_" + id; }
    static void Main(string[] paths)
    {
        int rules = XTapAffinityChecks.Run(s => UnityEngine.JsonUtility.FromJson<XTapAffinityState>(UnityEngine.JsonUtility.ToJson(s)));
        bool grew, failed;
        UnityEngine.PlayerPrefs.SetInt("xtap_captured_char_1", 1);
        UnityEngine.PlayerPrefs.SetInt("xtap_captured_char_2", 1);
        UnityEngine.PlayerPrefs.SetInt("xtap_bag_extra_cells", 85);
        Check(!XTapJailAffinity.TryTalk(3, out grew, out failed), "uncaptured reward");
        Check(!XTapJailAffinity.TryTalk(0, out grew, out failed), "player reward");
        Check(!XTapJailAffinity.TryTalk(11, out grew, out failed), "invalid character reward");
        Check(XTapJailAffinity.TryTalk(1, out grew, out failed) && !grew && !failed, "first conversation");
        Check(XTapJailAffinity.Points(1) == 1, "first score");
        Reload();
        Check(!XTapJailAffinity.TryTalk(1, out grew, out failed), "reload duplicate");
        Check(XTapJailAffinity.TryTalk(2, out grew, out failed), "independent daily reward");
        Check(XTapJailAffinity.Points(1) == 1 && XTapJailAffinity.Points(2) == 1, "owner isolation");
        UnityEngine.PlayerPrefs.SetString(Key(1), UnityEngine.JsonUtility.ToJson(new XTapAffinityState { points = 99, lastTalkDay = XTapJailAffinity.Today - 1 }));
        Reload();
        Check(XTapJailAffinity.TryTalk(1, out grew, out failed) && grew, "99 to 100 reward");
        Check(XTapJailAffinity.ExtraCells(1) == 1 && XTapJailAffinity.ExtraCells(2) == 0, "only selected bag expands");
        Reload();
        Check(XTapJailAffinity.Points(1) == 100 && XTapJailAffinity.ExtraCells(1) == 1, "expansion saved");
        Check(!XTapJailAffinity.TryTalk(1, out grew, out failed) && !grew, "no second cell");
        Check(UnityEngine.PlayerPrefs.GetInt("xtap_bag_extra_cells", 0) == 85, "player bag unchanged");
        var bag1 = new InventoryCapacityProbe(1);
        var bag2 = new InventoryCapacityProbe(2);
        Check(bag1.GetBagCapacity(1) == 25 && bag2.GetBagCapacity(2) == 24, "actual character bag capacities");
        Check(bag1.Accepts(0, 3) && !bag1.Accepts(1, 3), "only 25th cell unlocks");
        Check(!bag2.Accepts(0, 3), "other character 25th cell locked");
        Check(bag1.GetBagCapacity(0) == 109, "actual player capacity unchanged");
        Check(!bag1.Accepts(-1, 0) && !bag1.Accepts(8, 0) && !bag1.Accepts(0, -1), "grid bounds");
        // Save failure must not consume the day's conversation or show a reward.
        UnityEngine.PlayerPrefs.SetInt("xtap_captured_char_4", 1);
        UnityEngine.PlayerPrefs.FailSave = true;
        Check(!XTapJailAffinity.TryTalk(4, out grew, out failed) && failed && !grew, "save failure result");
        Check(XTapJailAffinity.Points(4) == 0 && XTapJailAffinity.CanTalk(4), "save rollback");
        Check(!UnityEngine.PlayerPrefs.HasKey(Key(4)), "failed first save key removed");
        UnityEngine.PlayerPrefs.FailSave = false;
        Check(XTapJailAffinity.TryTalk(4, out grew, out failed), "retry after save failure");
        // Corrupt records are preserved, not reset and rewarded again.
        UnityEngine.PlayerPrefs.SetInt("xtap_captured_char_5", 1);
        UnityEngine.PlayerPrefs.SetString(Key(5), "broken-json"); Reload();
        Check(XTapJailAffinity.HasSaveError(5) && !XTapJailAffinity.CanTalk(5), "corrupt record gate");
        Check(!XTapJailAffinity.TryTalk(5, out grew, out failed) && failed, "corrupt write prevented");
        Check(UnityEngine.PlayerPrefs.GetString(Key(5), "") == "broken-json", "corrupt source preserved");
        UnityEngine.PlayerPrefs.SetString(Key(2), UnityEngine.JsonUtility.ToJson(new XTapAffinityState { points = 10, lastTalkDay = XTapJailAffinity.Today + 1 })); Reload();
        Check(XTapJailAffinity.ClockBehind(2) && !XTapJailAffinity.CanTalk(2), "clock rollback gate");
        for (int id = 1; id <= 10; id++) for (int i = 0; i < 3; i++)
            Check(!string.IsNullOrWhiteSpace(XTapJailDialogue.Line(id, i)), "missing character conversation");
        int parsed = 0;
        foreach (string path in paths)
        {
            var tree = CSharpSyntaxTree.ParseText(File.ReadAllText(path), new CSharpParseOptions(LanguageVersion.CSharp9, preprocessorSymbols: new[] { "UNITY_EDITOR" }));
            foreach (var diagnostic in tree.GetDiagnostics())
                if (diagnostic.Severity == DiagnosticSeverity.Error) throw new Exception(path + ": " + diagnostic);
            parsed++;
        }
        Console.WriteLine("PASS: " + rules + " actual rule checks; " + count + " storage/bag/dialogue adapter checks; " + parsed + " C# files syntax parsed.");
        Console.WriteLine("Not a Unity compilation, Unity JsonUtility test, Cloud Build, or device test.");
    }
}
namespace UnityEngine
{
    public static class Mathf { public static int Max(int a, int b) { return Math.Max(a,b); } }
    public enum RuntimeInitializeLoadType { SubsystemRegistration }
    public sealed class RuntimeInitializeOnLoadMethodAttribute : Attribute { public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType type) {} }
    public static class Debug { public static void LogError(string message) {} }
    public static class JsonUtility
    {
        static readonly JsonSerializerOptions Options = new JsonSerializerOptions { IncludeFields = true };
        public static string ToJson<T>(T state) { return JsonSerializer.Serialize(state, Options); }
        public static T FromJson<T>(string json) { return JsonSerializer.Deserialize<T>(json, Options); }
    }
    public static class PlayerPrefs
    {
        static readonly Dictionary<string, string> Texts = new Dictionary<string, string>();
        static readonly Dictionary<string, int> Ints = new Dictionary<string, int>();
        public static bool FailSave;
        public static bool HasKey(string key) { return Texts.ContainsKey(key) || Ints.ContainsKey(key); }
        public static string GetString(string key, string fallback) { return Texts.TryGetValue(key, out string value) ? value : fallback; }
        public static void SetString(string key, string value) { Texts[key] = value; }
        public static int GetInt(string key, int fallback) { return Ints.TryGetValue(key, out int value) ? value : fallback; }
        public static void SetInt(string key, int value) { Ints[key] = value; }
        public static void DeleteKey(string key) { Texts.Remove(key); Ints.Remove(key); }
        public static void Save() { if (FailSave) throw new IOException("simulated disk failure"); }
    }
}
