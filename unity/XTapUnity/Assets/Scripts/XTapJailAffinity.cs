using System;
using System.Collections.Generic;
using UnityEngine;

public static class XTapJailAffinity
{
    const string KeyPrefix = "xtap_jail_affinity_v1_";
    static readonly Dictionary<int, XTapAffinityState> states = new Dictionary<int, XTapAffinityState>();
    static readonly HashSet<int> unreadable = new HashSet<int>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ClearCache() { states.Clear(); unreadable.Clear(); }

    public static int Today { get { return XTapAffinityState.KoreanDay(DateTime.UtcNow); } }
    public static bool IsCaptured(int id)
    {
        return id >= 1 && id <= 10 && PlayerPrefs.GetInt("xtap_captured_char_" + id, 0) == 1;
    }

    static XTapAffinityState Get(int id)
    {
        if (id < 1 || id > 10) return new XTapAffinityState();
        XTapAffinityState state;
        if (states.TryGetValue(id, out state)) return state;
        string json = PlayerPrefs.GetString(KeyPrefix + id, "");
        try
        {
            state = string.IsNullOrEmpty(json) ? new XTapAffinityState() : JsonUtility.FromJson<XTapAffinityState>(json);
            if (state == null || state.points < 0 || state.lastTalkDay < 0)
                throw new FormatException("Invalid affinity record");
        }
        catch (Exception e)
        {
            // Preserve the original save for recovery instead of silently
            // resetting it and awarding another conversation.
            unreadable.Add(id);
            state = new XTapAffinityState();
            Debug.LogError("X탑 호감도 저장 읽기 실패 (캐릭터 " + id + "): " + e.Message);
        }
        states[id] = state;
        return state;
    }

    public static int Points(int id) { return Get(id).Points; }
    public static int ExtraCells(int id) { return Get(id).ExtraCells; }
    public static int PointsToNextCell(int id) { return Get(id).PointsToNextCell; }
    public static bool HasSaveError(int id) { Get(id); return unreadable.Contains(id); }
    public static bool CanTalk(int id)
    {
        XTapAffinityState state = Get(id);
        return !unreadable.Contains(id) && state.CanTalk(IsCaptured(id), Today);
    }
    public static bool ClockBehind(int id) { return Get(id).lastTalkDay > Today; }

    public static bool TryTalk(int id, out bool expanded, out bool saveFailed)
    {
        expanded = false;
        saveFailed = false;
        XTapAffinityState state = Get(id);
        if (unreadable.Contains(id)) { saveFailed = true; return false; }
        int oldPoints = state.points, oldDay = state.lastTalkDay, oldCells = state.ExtraCells;
        if (!state.TryTalk(IsCaptured(id), Today)) return false;
        string key = KeyPrefix + id;
        bool hadKey = PlayerPrefs.HasKey(key);
        string previous = PlayerPrefs.GetString(key, "");
        try
        {
            // Score and daily receipt travel together in one saved record.
            // Save before any animation/callback can accept a second tap.
            PlayerPrefs.SetString(key, JsonUtility.ToJson(state));
            PlayerPrefs.Save();
        }
        catch (Exception e)
        {
            state.points = oldPoints;
            state.lastTalkDay = oldDay;
            if (hadKey) PlayerPrefs.SetString(key, previous); else PlayerPrefs.DeleteKey(key);
            saveFailed = true;
            Debug.LogError("X탑 호감도 저장 실패: " + e.Message);
            return false;
        }
        expanded = state.ExtraCells > oldCells;
        return true;
    }
}
