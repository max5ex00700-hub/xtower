using System;

// Also runnable outside Unity against the same runtime rule class. These checks
// use temporary records only and never award rewards or write PlayerPrefs.
public static class XTapAffinityChecks
{
    public static int Run(Func<XTapAffinityState, XTapAffinityState> reload)
    {
        int checks = 0;
        Action<bool, string> require = (ok, reason) => {
            checks++;
            if (!ok) throw new InvalidOperationException("호감도 검증 실패: " + reason);
        };
        int day = XTapAffinityState.KoreanDay(new DateTime(2026, 9, 28, 14, 59, 59, DateTimeKind.Utc));
        require(XTapAffinityState.KoreanDay(new DateTime(2026, 9, 28, 15, 0, 0, DateTimeKind.Utc)) == day + 1, "한국 자정 경계");
        var a = new XTapAffinityState();
        require(a.Points == 0 && a.ExtraCells == 0 && a.PointsToNextCell == 100, "기존 유저 초기값");
        require(!a.TryTalk(false, day) && a.Points == 0, "미포획 보상 차단");
        require(a.TryTalk(true, day) && a.Points == 1, "첫 대화 +1");
        for (int i = 0; i < 1000; i++) require(!a.TryTalk(true, day), "동일 날짜 연타 차단");
        a = reload(a);
        require(a.Points == 1 && a.lastTalkDay == day && !a.TryTalk(true, day), "재접속 후 중복 차단");
        require(!a.TryTalk(true, day - 1), "시계 역행 차단");
        require(a.TryTalk(true, day + 1) && a.Points == 2, "다음 날 대화");
        require(a.TryTalk(true, day + 8) && a.Points == 3, "미접속 일수 자동 적립 없음");
        var b = new XTapAffinityState();
        require(b.TryTalk(true, day) && b.Points == 1 && a.Points == 3, "캐릭터별 독립 보상");
        foreach (int milestone in new[] {100, 200, 800, 900})
        {
            var state = new XTapAffinityState { points = milestone - 1, lastTalkDay = day - 1 };
            int oldCells = state.ExtraCells;
            require(state.PointsToNextCell == 1, "확장까지 남은 호감도");
            require(state.TryTalk(true, day) && state.ExtraCells == oldCells + 1, "100마다 정확히 +1칸");
            state = reload(state);
            require(state.Points == milestone && state.ExtraCells == milestone / 100 &&
                state.PointsToNextCell == 100 && !state.TryTalk(true, day), "확장 재접속 유지 및 중복 방지");
        }
        require(new XTapAffinityState { points = 900 }.ExtraCells == 9, "미확정 32칸 상한 미적용");
        var max = new XTapAffinityState { points = int.MaxValue, lastTalkDay = day - 1 };
        require(!max.TryTalk(true, day) && max.Points == int.MaxValue, "정수 오버플로 방지");
        return checks;
    }
}
