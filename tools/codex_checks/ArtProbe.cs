using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using UnityEngine;

// Actual controller members are extracted by run.py. Only Unity rendering,
// random selection and asset loading are adapters; this is not a Unity build.
partial class ArtProbe
{
    const int StagesPerFloor = 10;
    int currentStep;
    readonly XTapInventory inventory = new XTapInventory();
    readonly RecordingAssets assets;
    Sprite shown;
    public ArtProbe(HashSet<string> entries) { assets = new RecordingAssets(entries); }
    void SetSprite(Sprite sprite) { shown = sprite; }
    public string StageAt(int floor, int subStage, int hpStage)
    {
        currentStep = (floor - 1) * 10 + subStage - 1;
        shown = null;
        SetStageOrFallback(hpStage);
        return shown == null ? null : shown.Entry;
    }
    public string Action(string prefix, int roll)
    {
        UnityEngine.Random.NextRoll = roll;
        shown = null;
        SetActionSprite(prefix);
        return shown == null ? null : shown.Entry;
    }
    public List<string> Preload()
    {
        assets.Requests.Clear();
        var routine = PreloadCurrentImages();
        while (routine.MoveNext()) { }
        return new List<string>(assets.Requests);
    }
}

sealed class RecordingAssets
{
    readonly HashSet<string> entries;
    public readonly List<string> Requests = new List<string>();
    public RecordingAssets(HashSet<string> entries) { this.entries = entries; }
    public Sprite GetSprite(string entry)
    {
        if (XTapCharacterArt.IsBlockedSourceEntry(entry)) throw new Exception("Controller requested mixed art: " + entry);
        Requests.Add(entry);
        return entries.Contains(entry) ? new Sprite { Entry = entry } : null;
    }
}

static class ArtChecks
{
    static int checks;
    static void Check(bool ok, string message) { checks++; if (!ok) throw new Exception(message); }
    public static void Run(string repo)
    {
        string stream = Path.Combine(repo, "unity/XTapUnity/Assets/StreamingAssets");
        byte[] bytes;
        string full = Path.Combine(stream, "xtop_source.apk");
        using (var data = new MemoryStream())
        {
            if (File.Exists(full)) data.Write(File.ReadAllBytes(full));
            else foreach (int part in new[] { 1, 2 }) data.Write(File.ReadAllBytes(Path.Combine(stream, "xtop_source.part" + part)));
            bytes = data.ToArray();
        }
        using (var data = new MemoryStream(bytes))
        using (var zip = new ZipArchive(data))
        using (var review = JsonDocument.Parse(File.ReadAllText(Path.Combine(repo, "tools/character_art_review.json"))))
        {
            var entries = new HashSet<string>();
            foreach (var entry in zip.Entries) entries.Add(entry.FullName);
            var verified = new HashSet<string>();
            foreach (string section in new[] { "verified_floor_6", "rejected_floor_6" })
                foreach (var pair in review.RootElement.GetProperty(section).EnumerateObject())
                {
                    string entry = "assets/f6_" + pair.Name + ".jpg";
                    using (var source = zip.GetEntry(entry).Open())
                    using (var sha = SHA256.Create())
                        Check(Convert.ToHexString(sha.ComputeHash(source)).ToLowerInvariant() == pair.Value.GetString(), "reviewed bytes changed: " + entry);
                    bool valid = section == "verified_floor_6";
                    if (valid) verified.Add(pair.Name);
                    Check(XTapCharacterArt.IsImageAvailable(6, pair.Name) == valid, "review identity mismatch: " + entry);
                    Check(XTapCharacterArt.IsBlockedSourceEntry(entry) == !valid, "raw source guard: " + entry);
                    Check(XTapCharacterArt.IsBlockedSourceEntry(entry.ToUpperInvariant()) == !valid, "case-insensitive guard: " + entry);
                }
            Check(verified.SetEquals(XTapCharacterArt.AllImageCodes(6)), "floor 6 codex must match reviewed artwork");
            var probe = new ArtProbe(entries);
            // All 10 sub-stages across two character cycles, all HP stages and every possible action roll.
            for (int floor = 1; floor <= 20; floor++)
                for (int sub = 1; sub <= 10; sub++)
                {
                    int id = ((floor - 1) % 10) + 1;
                    var reachable = new HashSet<string>();
                    var expected = new HashSet<string>(XTapCharacterArt.AllImageCodes(id));
                    for (int stage = 0; stage <= 4; stage++)
                    {
                        string code = id == 6 ? new[] { "p00", "d02", "d04", "d06", "d08" }[stage] : "p" + (stage * 2).ToString("00");
                        Check(probe.StageAt(floor, sub, stage) == "assets/f" + id + "_" + code + ".jpg", "floor/stage portrait mismatch");
                        reachable.Add(code);
                    }
                    PlayerPrefs.Clear();
                    foreach (string prefix in new[] { "p", "k", "b", "d" })
                        for (int roll = 0; roll < XTapCharacterArt.ActionImageCount(id, prefix); roll++)
                        {
                            string entry = probe.Action(prefix, roll);
                            string code = XTapCharacterArt.ActionImageCode(id, prefix, roll);
                            Check(entry == "assets/f" + id + "_" + code + ".jpg" && expected.Contains(code), "action belongs to current floor");
                            Check(XTapCodex.IsImageDiscovered(id, code), "codex records actual displayed pose");
                            if (id == 6) Check(verified.Contains(code), "action contains wrong character");
                            reachable.Add(code);
                        }
                    Check(!XTapCodex.IsImageDiscovered(id, "cap"), "combat must not discover capture portrait");
                    expected.Remove("cap");
                    Check(reachable.SetEquals(expected), "all non-capture codex images reachable");
                    var loaded = new HashSet<string>(probe.Preload());
                    Check(loaded.Count == expected.Count + 1, "preload only unique supported art");
                    foreach (string code in XTapCharacterArt.AllImageCodes(id))
                        Check(loaded.Contains("assets/f" + id + "_" + code + ".jpg"), "preload coverage");
                }
            // A missing approved pose may use only this character's p00 and its real discovery code.
            var missing = new HashSet<string>(entries);
            missing.Remove("assets/f6_d04.jpg");
            PlayerPrefs.Clear();
            var fallback = new ArtProbe(missing);
            Check(fallback.StageAt(6, 1, 2) == "assets/f6_p00.jpg", "same-character fallback");
            Check(XTapCodex.IsImageDiscovered(6, "p00") && !XTapCodex.IsImageDiscovered(6, "d04"), "no false fallback discovery");
        }
        Console.WriteLine("Character art: " + checks + " actual C# controller/data adapter checks passed (not a Unity build).");
    }
}

namespace UnityEngine
{
    public sealed class Sprite { public string Entry; }
    public static class Random
    {
        public static int NextRoll;
        public static int Range(int min, int max)
        {
            if (NextRoll < min || NextRoll >= max) throw new Exception("Invalid random action range");
            return NextRoll;
        }
    }
}
