using System;
using System.Collections.Generic;
using UnityEngine;

static class Run
{
    static int checks;
    static void Check(bool ok, string reason) { checks++; if (!ok) throw new Exception(reason); }
    static string Key(int id, string code) { return "xtap_codex_img_" + id + "_" + code; }

    static void Main(string[] args)
    {
        ArtChecks.Run(args[0]);
        var bag = new XTapInventory();
        var codex = new XTapCodex(bag);
        int total = 0;
        for (int id = 1; id <= 10; id++)
        {
            var codes = XTapCodex.Codes(id);
            total += codes.Count;
            Check(codes.Count == (id == 6 ? 10 : id == 2 ? 40 : 41), "per-character requirement");
            Check(new HashSet<string>(codes).Count == codes.Count, "no duplicate entries");
            Check(codes.Contains("k09") == (id != 2 && id != 6), "missing/mixed k09 excluded");
            foreach (string prefix in new[] { "p", "k", "b", "d" })
                Check(XTapCodex.ActionImageCount(id, prefix) == (id == 6 ? 8 : id == 2 && prefix == "k" ? 9 : 10), "combat action range");
            PlayerPrefs.Clear();
            Check(!XTapCodex.IsCharacterComplete(id), "empty collection incomplete");
            foreach (string code in codes) PlayerPrefs.SetInt(Key(id, code), 1);
            Check(XTapCodex.IsCharacterComplete(id), "all required images complete");
            foreach (string code in codes)
            {
                PlayerPrefs.SetInt(Key(id, code), 0);
                Check(!XTapCodex.IsCharacterComplete(id), "every retained image is required: " + id + " " + code);
                PlayerPrefs.SetInt(Key(id, code), 1);
            }
            codex.OpenForTest(id);
            Check(bag.ExpansionBonus == 8 && bag.HasCodexCompletionReward(id), "existing save completion reward");
            codex.OpenForTest(id);
            XTapCodex.MarkImageDiscovered(id, "cap", bag);
            Check(bag.ExpansionBonus == 8, "reopen/rediscovery must not duplicate reward");
        }
        Check(total == 378, "total valid codex images");
        PlayerPrefs.Clear();
        PlayerPrefs.SetInt(Key(6, "p02"), 1); // Old wrong-character discovery survives in storage.
        XTapCodex.MarkImageDiscovered(6, "k00", bag);
        Check(!XTapCodex.IsImageDiscovered(6, "p02") && codex.Count(6) == 0, "legacy mixed art is not displayed/counted");
        Check(PlayerPrefs.GetInt(Key(6, "p02"), 0) == 1 && PlayerPrefs.GetInt(Key(6, "k00"), 0) == 0,
            "keep old saves but reject new invalid discoveries");
        foreach (string code in XTapCodex.Codes(6))
            if (code != "cap") XTapCodex.MarkImageDiscovered(6, code, bag);
        Check(codex.Count(6) == 9 && !XTapCodex.IsCharacterComplete(6) && bag.ExpansionBonus == 0,
            "battle images cannot unlock cap or completion reward");
        XTapCodex.MarkImageDiscovered(6, "cap", bag);
        Check(codex.Count(6) == 10 && bag.ExpansionBonus == 8, "sixth floor completion still requires capture");
        codex.OpenForTest(6);
        Check(bag.ExpansionBonus == 8, "sixth floor reward remains one-time");
        PlayerPrefs.Clear();
        PlayerPrefs.SetInt("xtap_bag_extra_cells", 8);
        PlayerPrefs.SetInt("xtap_codex_complete_reward_6", 1);
        foreach (string code in XTapCodex.Codes(6)) XTapCodex.MarkImageDiscovered(6, code, bag);
        codex.OpenForTest(6);
        Check(bag.ExpansionBonus == 8, "pre-fix receipt prevents duplicate reward");
        PlayerPrefs.Clear();
        PlayerPrefs.SetInt("xtap_bag_extra_cells", 85);
        var floor2 = XTapCodex.Codes(2);
        for (int i = 0; i < floor2.Count - 1; i++) XTapCodex.MarkImageDiscovered(2, floor2[i], bag);
        Check(!XTapCodex.IsCharacterComplete(2) && bag.ExpansionBonus == 85, "39 images no reward");
        XTapCodex.MarkImageDiscovered(2, "k09", bag);
        Check(codex.Count(2) == 39 && bag.ExpansionBonus == 85, "obsolete discovery cannot replace a required image");
        XTapCodex.MarkImageDiscovered(2, floor2[floor2.Count - 1], bag);
        Check(codex.Count(2) == 40 && bag.ExpansionBonus == 93, "40th image adds exactly 8 to existing capacity");
        Check(XTapCodex.IsCharacterComplete(12), "normalized character completion");
        codex.OpenForTest(12);
        Check(bag.ExpansionBonus == 93, "normalized reopen cannot duplicate reward");
        PlayerPrefs.Clear();
        for (int id = 1; id <= 10; id++)
            foreach (string code in XTapCodex.Codes(id)) XTapCodex.MarkImageDiscovered(id, code, bag);
        Check(bag.ExpansionBonus == 80, "all ten codices still grant 80 player bag cells");
        for (int id = 1; id <= 10; id++) codex.OpenForTest(id);
        Check(bag.ExpansionBonus == 80, "all receipt keys remain idempotent");
        Check(!XTapCodex.IsCharacterComplete(0) && !XTapCodex.IsCharacterComplete(-1), "invalid character incomplete");
        Console.WriteLine("Codex: " + checks + " actual C# member adapter checks passed (not a Unity build).");
    }
}

namespace UnityEngine
{
    public static class Mathf { public static int Max(int a, int b) { return Math.Max(a, b); } }
    public static class PlayerPrefs
    {
        static readonly Dictionary<string, int> values = new Dictionary<string, int>();
        public static int GetInt(string key, int fallback) { return values.TryGetValue(key, out int value) ? value : fallback; }
        public static void SetInt(string key, int value) { values[key] = value; }
        public static void Save() { }
        public static void Clear() { values.Clear(); }
    }
}
