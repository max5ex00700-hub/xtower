// New conversation lines follow the existing character personalities. Portrait
// touches and the conversation button share one reward gate in XTapJailAffinity.
public static class XTapJailDialogue
{
    static readonly string[][] Lines =
    {
        new[] { "왔나. 오늘은 무슨 이야기지?", "빙빙 돌리지 말고 말해. 듣고 있으니까.", "꾸준히 찾아오는 끈기는 인정하지." },
        new[] { "어머, 오늘도 나 보러 온 거야?", "무슨 이야기를 해 줄까? 네 표정부터 읽어 볼까?", "후후, 내일도 올 거지?" },
        new[] { "방문 시간을 잘 지키는군요.", "대화는 차분하게. 당신의 이야기를 듣겠습니다.", "작은 약속부터 지키는 것이 신뢰의 시작입니다." },
        new[] { "왔구나! 오늘 있었던 일 좀 들려줘.", "잠깐만 더 있어 줄래? 이야기가 재미있어서.", "네가 오면 여기도 조금 덜 조용해." },
        new[] { "왔네. 앉아.", "듣고 있어. 계속해.", "…내일도 이 시간이면 괜찮아." },
        new[] { "어서 와요. 오늘은 좀 쉬었나요?", "힘든 일이 있었다면 천천히 이야기해요.", "찾아와 줘서 고마워요. 당신도 잘 챙기고요." },
        new[] { "왔냐! 오늘은 재밌는 소식 없어?", "그 이야기 괜찮은데? 더 해 봐!", "하하, 다음엔 내가 들려줄 차례네!" },
        new[] { "또 이 시간이네. 오늘은 무슨 이야기야?", "똑같은 하루인 줄 알았는데, 조금 다르네.", "…그 이야기는 기억해 둘게." },
        new[] { "왔군. 오늘의 이야기를 들려 보아라.", "좋다. 계속해라. 듣고 있다.", "꾸준함도 자질이지. 내일도 지켜보겠다." },
        new[] { "방문을 확인했습니다. 대화를 시작할까요?", "이야기를 기록하고 있습니다. 계속해 주십시오.", "오늘의 대화가 저장되었습니다. 다음 방문을 기다리겠습니다." }
    };

    public static string Line(int id, int index)
    {
        if (id < 1 || id > Lines.Length) return "오늘은 어떤 이야기를 할까요?";
        string[] pool = Lines[id - 1];
        return pool[(index & int.MaxValue) % pool.Length];
    }
}
