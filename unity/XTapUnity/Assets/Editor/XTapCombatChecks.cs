using System;

// Pure combat rules; no scene/PlayerPrefs mutation. Shared by preflight and the
// standalone C# runner. Rendering and device event delivery are separate checks.
public static class XTapCombatChecks
{
    public static int Run()
    {
        int count = 0;
        Action<bool, string> check = (ok, reason) => { count++; if (!ok) throw new InvalidOperationException("전투 검증 실패: " + reason); };
        var state = new XTapCombatState();
        foreach (XTapCombatCue kind in new[] { XTapCombatCue.Hit, XTapCombatCue.Followup, XTapCombatCue.Shield, XTapCombatCue.Counter })
        {
            state.Reset(); state.OpenCue(kind);
            double lifetime = state.Remaining;
            for (int frame = 0; frame < 120; frame++) state.Advance(1d/60d, true);
            check(state.Clock == 0d && state.Remaining == lifetime && !state.Expired, "연출 중 제한 시간 정지");
            state.Advance(lifetime - .001d, false);
            XTapCombatCue consumed; bool perfect;
            check(state.ConsumeCue(out consumed, out perfect) && consumed == kind, "만료 직전 판정");
            check(!state.ConsumeCue(out consumed, out perfect), "표적 중복 소비 차단");
            state.OpenCue(kind); state.Advance(lifetime, false);
            check(state.Expired && !state.ConsumeCue(out consumed, out perfect), "만료 후 판정 차단");
        }
        foreach (double elapsed in new[] {0d, .50d, .869d, .870d, .950d, 1.049d, 1.05d, 1.051d})
        {
            state.Reset(); state.OpenCue(XTapCombatCue.Shield); state.Advance(elapsed, false);
            check(state.PerfectWindow == (elapsed >= .870d && elapsed < 1.05d), "완벽 방어 0.18초 경계");
        }
        state.Reset(); state.OpenCue(XTapCombatCue.Shield); state.Advance(.9d, false);
        XTapCombatCue shield; bool isPerfect;
        check(state.ConsumeCue(out shield, out isPerfect) && isPerfect, "완벽 방어 소비");
        state.OpenCue(XTapCombatCue.Counter);
        check(state.IsHitCue && state.Remaining > 1.049d, "반격 HIT 전체 시간 제공");
        check(XTapCombatState.AttackMultiplier(XTapCombatCue.Counter, false) == 5d, "반격 배수");
        check(XTapCombatState.AttackMultiplier(XTapCombatCue.Hit, false) == 3.6d, "일반 HIT 배수 유지");
        check(XTapCombatState.AttackMultiplier(XTapCombatCue.Followup, false) == 5d, "추가 HIT 배수 유지");
        check(XTapCombatState.AttackMultiplier(XTapCombatCue.None, false) == 1d && XTapCombatState.AttackMultiplier(XTapCombatCue.None, true) == 1.6d, "기존 일반/스와이프 배수 유지");
        state.Reset();
        for (int i = 1; i <= 15; i++)
        {
            check(state.RegisterHit() == i, "적중 콤보 증가");
            check(state.ComboTier == (i >= 10 ? 3 : i >= 6 ? 2 : i >= 3 ? 1 : 0), "3/6/10 연출 단계");
        }
        state.RegisterMistake(); check(state.Combo == 0, "실수 콤보 초기화");
        check(!state.ClaimFinish(1d) && !state.ClaimFinish(double.NaN), "생존 중 피니시 금지");
        check(state.ClaimFinish(0d) && state.Finished && state.Cue == XTapCombatCue.None, "HP 0에서 피니시");
        check(!state.ClaimFinish(0d), "중복 승리/보상 차단");
        state.OpenCue(XTapCombatCue.Hit); check(state.Cue == XTapCombatCue.None, "피니시 중 표적 생성 차단");
        state.Reset(); check(!state.Finished && state.Combo == 0 && state.Clock == 0, "새 전투 초기화");

        var pointer = new XTapCombatPointer();
        for (int i = 0; i < 100; i++)
        {
            check(pointer.Begin(i, true), "새 손가락 입력");
            check(!pointer.Begin(i+1, true), "멀티터치 중복 차단");
            pointer.Consume();
            check(!pointer.End(i+1, false), "다른 손가락 release 무시");
            check(!pointer.End(i, false), "down 판정 후 release 재공격 차단");
            check(!pointer.End(i, false), "release 중복 차단");
        }
        check(pointer.Begin(1, true) && !pointer.End(1, true), "취소 터치 공격 금지");
        check(pointer.Begin(2, false) && !pointer.End(2, false), "연출 중 시작한 터치 공격 금지");
        check(pointer.Begin(3, true) && pointer.End(3, false), "일반 스와이프 release 허용");
        pointer.Begin(4, true); pointer.Clear(); check(!pointer.End(4, false), "백그라운드 복귀 유령 터치 차단");
        return count;
    }
}
