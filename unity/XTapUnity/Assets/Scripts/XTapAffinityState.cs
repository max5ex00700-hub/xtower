using System;

// Pure rules: no UI, clock reads or PlayerPrefs writes. Milestone capacity is
// derived from the cumulative score, so reopening cannot grant a cell twice.
[Serializable]
public sealed class XTapAffinityState
{
    public int points;
    public int lastTalkDay;

    public int Points { get { return Math.Max(0, points); } }
    public int ExtraCells { get { return Points / 100; } }
    public int PointsToNextCell { get { return 100 - Points % 100; } }

    public bool CanTalk(bool captured, int day)
    {
        return captured && day > 0 && day > lastTalkDay && Points < int.MaxValue;
    }

    public bool TryTalk(bool captured, int day)
    {
        if (!CanTalk(captured, day)) return false;
        points = Points + 1;
        lastTalkDay = day;
        return true;
    }

    // All characters share the Korean calendar reset, independent of device
    // timezone. Moving the clock backwards cannot repeat an already claimed day.
    public static int KoreanDay(DateTime utc)
    {
        if (utc.Kind == DateTimeKind.Local) utc = utc.ToUniversalTime();
        return (int)(utc.AddHours(9).Date.Ticks / TimeSpan.TicksPerDay);
    }
}
