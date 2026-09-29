using System;
using System.Collections.Generic;

// File names in the bundled APK are not a reliable character identity: 31 f6
// entries contain floor 5 artwork. Use only visually reviewed floor 6 poses.
public static class XTapCharacterArt
{
    static readonly string[] FloorSixActions =
        { "d02", "d03", "d04", "d05", "d06", "d07", "d08", "d09" };
    static readonly string[] FloorSixStages = { "p00", "d02", "d04", "d06", "d08" };

    static int Normalize(int characterId)
    {
        return characterId <= 0 ? 0 : ((characterId - 1) % 10) + 1;
    }

    static bool IsActionPrefix(string prefix)
    {
        return prefix == "p" || prefix == "k" || prefix == "b" || prefix == "d";
    }

    public static int ActionImageCount(int characterId, string prefix)
    {
        int id = Normalize(characterId);
        if (id == 0 || !IsActionPrefix(prefix)) return 0;
        if (id == 6) return FloorSixActions.Length;
        return id == 2 && prefix == "k" ? 9 : 10;
    }

    public static string ActionImageCode(int characterId, string prefix, int index)
    {
        int count = ActionImageCount(characterId, prefix);
        if (index < 0 || index >= count) throw new ArgumentOutOfRangeException("index");
        return Normalize(characterId) == 6 ? FloorSixActions[index] : prefix + index.ToString("00");
    }

    public static string StageImageCode(int characterId, int stage)
    {
        int index = Math.Max(0, Math.Min(4, stage));
        return Normalize(characterId) == 6 ? FloorSixStages[index] : "p" + (index * 2).ToString("00");
    }

    public static bool IsImageAvailable(int characterId, string code)
    {
        int id = Normalize(characterId);
        if (id == 0 || string.IsNullOrEmpty(code)) return false;
        if (code == "p00" || code == "cap") return true;
        if (id == 6) return Array.IndexOf(FloorSixActions, code) >= 0;
        if (code.Length != 3 || !IsActionPrefix(code.Substring(0, 1)) ||
            code[1] != '0' || code[2] < '0' || code[2] > '9') return false;
        return !(id == 2 && code == "k09");
    }

    public static List<string> AllImageCodes(int characterId)
    {
        var codes = new List<string>(41);
        if (Normalize(characterId) == 0) return codes;
        codes.Add("p00");
        foreach (string prefix in new[] { "p", "k", "b", "d" })
            for (int i = 0; i < ActionImageCount(characterId, prefix); i++)
            {
                string code = ActionImageCode(characterId, prefix, i);
                if (!codes.Contains(code)) codes.Add(code);
            }
        codes.Add("cap");
        return codes;
    }

    // Also guard direct callers (including future UI) at the asset boundary.
    // Do not silently relabel a fallback pose as a different codex discovery.
    public static bool IsBlockedSourceEntry(string entry)
    {
        const string prefix = "assets/f6_";
        const string suffix = ".jpg";
        if (string.IsNullOrEmpty(entry) ||
            !entry.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ||
            !entry.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)) return false;
        string code = entry.Substring(prefix.Length, entry.Length - prefix.Length - suffix.Length);
        return !IsImageAvailable(6, code.ToLowerInvariant());
    }
}
