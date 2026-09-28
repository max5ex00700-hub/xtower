using System;

// Decide once before presentation. The finisher and actual reward can differ.
public struct XTapForgeOutcome
{
    public bool AngelFinisher { get; private set; }
    public bool Reversal { get; private set; }
    public bool Success { get { return AngelFinisher != Reversal; } }
    public int RewardMultiplier { get { return !Success ? 0 : (Reversal ? 2 : 1); } }

    public static XTapForgeOutcome FromRolls(int chance, int finisherRoll, int reversalRoll)
    {
        if (finisherRoll < 0 || finisherRoll >= 100 || reversalRoll < 0 || reversalRoll >= 100)
            throw new ArgumentOutOfRangeException("Forge rolls must be in [0, 100).");
        return new XTapForgeOutcome
        {
            AngelFinisher = finisherRoll < Math.Max(0, Math.Min(100, chance)),
            Reversal = reversalRoll == 0
        };
    }

    public int EnhancedLevel(int currentLevel)
    {
        return Math.Min(20, currentLevel + RewardMultiplier);
    }
}

public static class XTapForgeDialogue
{
    public static readonly string[] AngelFinishers =
    {
        "빛이여, 힘을!", "이번엔 내가!", "잘 받아 줘!", "행운을 담아서!", "정성껏 만들었어!",
        "조금만 더... 됐다!", "반짝여라!", "내가 지켜 줄게!", "좋아, 완벽해!", "축복을 받아!"
    };
    public static readonly string[] DemonFinishers =
    {
        "내 차례다!", "어림없지!", "부서져라!", "기대는 여기까지!", "운도 끝났네!",
        "이건 내가 접수!", "한 방이면 끝!", "울어도 소용없어!", "이 맛에 망치질하지!", "마지막은 내 거야!"
    };
}
