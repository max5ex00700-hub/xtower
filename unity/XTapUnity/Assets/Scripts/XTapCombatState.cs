using System;

public enum XTapCombatCue { None, Hit, Followup, Shield, Counter }

// One combat clock for cue motion/deadlines. UI, audio and the rest of the app
// keep their normal clocks; presentation locks cannot consume a cue's lifetime.
public sealed class XTapCombatState
{
    public const double CueSeconds = 1.05d;
    public const double FollowupSeconds = .70d;
    public const double PerfectSeconds = .18d;
    public double Clock { get; private set; }
    public double CueUntil { get; private set; }
    public XTapCombatCue Cue { get; private set; }
    public int Combo { get; private set; }
    public bool Finished { get; private set; }
    public bool IsHitCue { get { return Cue == XTapCombatCue.Hit || Cue == XTapCombatCue.Followup || Cue == XTapCombatCue.Counter; } }
    public double Remaining { get { return Math.Max(0d, CueUntil - Clock); } }
    public bool Expired { get { return Cue != XTapCombatCue.None && Clock >= CueUntil; } }
    public bool PerfectWindow { get { return Cue == XTapCombatCue.Shield && !Expired && Remaining <= PerfectSeconds + 1e-9d; } }
    public int ComboTier { get { return Combo >= 10 ? 3 : Combo >= 6 ? 2 : Combo >= 3 ? 1 : 0; } }

    public void Reset()
    {
        Clock = CueUntil = 0d; Cue = XTapCombatCue.None; Combo = 0; Finished = false;
    }
    public void Advance(double seconds, bool paused)
    {
        if (!paused && !Finished && seconds > 0d && !double.IsInfinity(seconds) && !double.IsNaN(seconds)) Clock += seconds;
    }
    public void OpenCue(XTapCombatCue cue)
    {
        if (Finished) return;
        Cue = cue;
        CueUntil = Clock + (cue == XTapCombatCue.Followup ? FollowupSeconds : CueSeconds);
    }
    public void CloseCue() { Cue = XTapCombatCue.None; CueUntil = Clock; }
    public bool ConsumeCue(out XTapCombatCue cue, out bool perfect)
    {
        cue = Cue; perfect = PerfectWindow;
        if (cue == XTapCombatCue.None || Expired || Finished) return false;
        CloseCue();
        return true;
    }
    public int RegisterHit()
    {
        if (!Finished && Combo < int.MaxValue) Combo++;
        return Combo;
    }
    public void RegisterMistake() { Combo = 0; }
    // Random dodges and unavoidable normal counterattacks do not call
    // RegisterMistake. Combo measures player accuracy, not a random outcome.
    public bool ClaimFinish(double hp)
    {
        if (Finished || double.IsNaN(hp) || hp > 0d) return false;
        Finished = true; CloseCue(); return true;
    }
    public static double AttackMultiplier(XTapCombatCue consumedCue, bool swipe)
    {
        if (consumedCue == XTapCombatCue.Followup || consumedCue == XTapCombatCue.Counter) return 5d;
        if (consumedCue == XTapCombatCue.Hit) return 3.6d;
        return swipe ? 1.6d : 1d;
    }
}

// One physical touch owns a gesture until release/cancel. A down-event cue
// success is consumed immediately, so release cannot deal a second attack.
public sealed class XTapCombatPointer
{
    int activeFinger = -1;
    bool armed;
    public bool Begin(int fingerId, bool allowGesture)
    {
        if (activeFinger != -1) return false;
        activeFinger = fingerId; armed = allowGesture; return true;
    }
    public void Consume() { armed = false; }
    public bool End(int fingerId, bool canceled)
    {
        if (activeFinger != fingerId) return false;
        bool dispatch = armed && !canceled;
        Clear(); return dispatch;
    }
    public void Clear() { activeFinger = -1; armed = false; }
}
