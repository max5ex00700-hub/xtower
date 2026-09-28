using System;
using UnityEngine;
using UnityEngine.UI;

public sealed class XTapJail : MonoBehaviour
{
    const float Width = 1080f;
    static readonly Color Gold = new Color(1f, .82f, .47f);
    static readonly Color Pink = new Color(1f, .48f, .67f);
    static readonly Color Muted = new Color(.62f, .69f, .80f);
    public bool IsOpen { get; private set; }

    RectTransform host, content, hero, affinityPanel, statsPanel, speechPanel;
    Font font;
    XTapInventory inventory;
    XTapOriginalApkAssets assets;
    Action onClosed;
    Action<int> openMiniGame;
    GameObject overlay;
    ScrollRect pageScroll;
    Image portrait, progressFill;
    AspectRatioFitter portraitFit;
    Button portraitButton, talkButton, bagButton, gameButton, closeButton;
    Text title, subtitle, nameText, portraitHint, speechText, feedbackText;
    Text affinityText, nextCellText, capacityText, multiplierText, statsText;
    Text talkLabel, resetText, bagLabel, gameLabel;
    readonly Button[] cards = new Button[10];
    readonly Text[] cardLabels = new Text[10];
    RectTransform selector;
    int selectedCharacterId, displayedCharacterId = -1, displayedDay, dialogueIndex;
    float nextClockCheck, nextTouchTime, feedbackAge = 10f;
    Vector2 lastViewport;
    Rect lastSafeArea;

    public void Initialize(RectTransform parent, Font uiFont, XTapInventory bag, Action closed)
    {
        host = parent; font = uiFont; inventory = bag; onClosed = closed;
        BuildUi();
        overlay.SetActive(false);
    }

    public void SetAssets(XTapOriginalApkAssets loadedAssets) { assets = loadedAssets; }
    public void SetMiniGameLauncher(Action<int> launcher)
    {
        openMiniGame = launcher;
        if (IsOpen) RefreshDetail();
    }

    public void Open()
    {
        if (overlay == null) return;
        IsOpen = true;
        overlay.SetActive(true);
        overlay.transform.SetAsLastSibling();
        if (!XTapJailAffinity.IsCaptured(selectedCharacterId)) selectedCharacterId = FirstCapturedCharacter();
        displayedCharacterId = -1;
        feedbackAge = 10f;
        ApplyLayout(true);
        Refresh();
        Canvas.ForceUpdateCanvases();
        pageScroll.verticalNormalizedPosition = 1f;
    }

    public void Close()
    {
        IsOpen = false;
        if (overlay != null) overlay.SetActive(false);
        if (onClosed != null) onClosed();
    }

    public void RefreshIfOpen() { if (IsOpen) Refresh(); }
    void OnApplicationFocus(bool focus) { if (focus && IsOpen) Refresh(); }
    void OnApplicationPause(bool paused) { if (!paused && IsOpen) Refresh(); }

    void Update()
    {
        if (!IsOpen) return;
        ApplyLayout(false);
        if (Time.unscaledTime >= nextClockCheck)
        {
            nextClockCheck = Time.unscaledTime + 1f;
            if (displayedDay != XTapJailAffinity.Today) Refresh();
        }
        feedbackAge += Time.unscaledDeltaTime;
        float pulse = Mathf.Sin(Mathf.Clamp01(feedbackAge / .36f) * Mathf.PI);
        // A small portrait response leaves the touch area stationary.
        portrait.rectTransform.localScale = Vector3.one * (1f + .018f * pulse);
        Color tint = feedbackText.color;
        tint.a = 1f - Mathf.Clamp01((feedbackAge - 1.8f) / .5f);
        feedbackText.color = tint;
    }

    void BuildUi()
    {
        overlay = new GameObject("JailOverlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
        overlay.transform.SetParent(host, false);
        Image bg = overlay.GetComponent<Image>();
        bg.color = new Color(.018f, .024f, .041f, 1f);
        bg.raycastTarget = true;
        Anchor(bg.rectTransform, 0f, 0f, 1f, 1f);
        content = Panel(overlay.transform, "JailContent", Color.clear);
        content.anchorMin = content.anchorMax = content.pivot = new Vector2(0f, 1f);
        pageScroll = overlay.GetComponent<ScrollRect>();
        pageScroll.viewport = bg.rectTransform; pageScroll.content = content;
        pageScroll.horizontal = false; pageScroll.vertical = true;
        pageScroll.movementType = ScrollRect.MovementType.Clamped;

        title = Label(content, "감옥 · 교감", 66, Gold);
        subtitle = Label(content, "", 34, Muted);
        closeButton = ButtonOf(content, "닫기", 40, new Color(.10f, .13f, .19f));
        closeButton.onClick.AddListener(Close);

        selector = Panel(content, "CharacterSelector", new Color(.035f, .045f, .065f));
        selector.gameObject.AddComponent<RectMask2D>();
        selector.GetComponent<Image>().raycastTarget = true;
        ScrollRect strip = selector.gameObject.AddComponent<ScrollRect>();
        RectTransform stripContent = Panel(selector, "Characters", Color.clear);
        stripContent.anchorMin = stripContent.anchorMax = stripContent.pivot = new Vector2(0f, 1f);
        stripContent.sizeDelta = new Vector2(10 * 238f - 12f, 144f);
        strip.viewport = selector; strip.content = stripContent;
        strip.horizontal = true; strip.vertical = false;
        strip.movementType = ScrollRect.MovementType.Clamped;
        for (int i = 0; i < cards.Length; i++)
        {
            int id = i + 1;
            cards[i] = ButtonOf(stripContent, "", 32, new Color(.07f, .09f, .14f));
            cards[i].name = "Character_" + id;
            Top(cards[i].GetComponent<RectTransform>(), i * 238f, 0f, 226f, 144f);
            cardLabels[i] = cards[i].GetComponentInChildren<Text>();
            cards[i].onClick.AddListener(delegate { Select(id); });
        }

        hero = Panel(content, "PortraitStage", new Color(.045f, .055f, .085f));
        hero.gameObject.AddComponent<RectMask2D>();
        RectTransform portraitViewport = Panel(hero, "PortraitViewport", Color.clear);
        Anchor(portraitViewport, 0f, 0f, 1f, 1f);
        portraitViewport.offsetMin = new Vector2(0f, 190f);
        portraitViewport.offsetMax = new Vector2(0f, -126f);
        portraitViewport.gameObject.AddComponent<RectMask2D>();
        portrait = Panel(portraitViewport, "OriginalCharacterPortrait", Color.white).GetComponent<Image>();
        Anchor(portrait.rectTransform, 0f, 0f, 1f, 1f);
        // Fill the portrait window without stretching. Crop from the bottom on
        // short phones so the face stays visible below the name plate.
        portrait.rectTransform.pivot = new Vector2(.5f, 1f);
        portraitFit = portrait.gameObject.AddComponent<AspectRatioFitter>();
        portraitFit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        portrait.preserveAspect = true;
        // The full hero plate remains tappable even in the image's side margins.
        portraitButton = hero.gameObject.AddComponent<Button>();
        hero.GetComponent<Image>().raycastTarget = true;
        portraitButton.targetGraphic = hero.GetComponent<Image>();
        portraitButton.transition = Selectable.Transition.None;
        portraitButton.onClick.AddListener(Talk);
        RectTransform namePlate = Panel(hero, "NamePlate", new Color(.02f, .025f, .04f, .92f));
        Anchor(namePlate, 0f, 1f, 1f, 1f);
        namePlate.pivot = new Vector2(.5f, 1f); namePlate.sizeDelta = new Vector2(0f, 126f);
        nameText = Label(namePlate, "", 46, Gold);
        Anchor(nameText.rectTransform, .035f, .43f, .965f, 1f);
        portraitHint = Label(namePlate, "", 30, Muted);
        Anchor(portraitHint.rectTransform, .035f, 0f, .965f, .43f);
        speechPanel = Panel(hero, "Conversation", new Color(.025f, .035f, .065f, .95f));
        Anchor(speechPanel, .025f, .025f, .975f, .025f);
        speechPanel.pivot = new Vector2(.5f, 0f); speechPanel.sizeDelta = new Vector2(0f, 162f);
        speechText = Label(speechPanel, "", 42, Color.white);
        speechText.horizontalOverflow = HorizontalWrapMode.Wrap;
        Anchor(speechText.rectTransform, .035f, .08f, .965f, .92f);
        feedbackText = Label(hero, "", 51, Pink, TextAnchor.MiddleCenter);
        Anchor(feedbackText.rectTransform, .02f, .26f, .98f, .42f);
        feedbackText.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, .9f);

        affinityPanel = Panel(content, "AffinityCard", new Color(.085f, .045f, .075f));
        affinityText = Label(affinityPanel, "", 46, Pink);
        capacityText = Label(affinityPanel, "", 37, Gold, TextAnchor.MiddleRight);
        nextCellText = Label(affinityPanel, "", 32, Muted);
        Anchor(affinityText.rectTransform, .025f, .59f, .54f, .97f);
        Anchor(capacityText.rectTransform, .54f, .59f, .975f, .97f);
        Anchor(nextCellText.rectTransform, .025f, .035f, .975f, .37f);
        RectTransform track = Panel(affinityPanel, "ProgressTrack", new Color(.20f, .12f, .20f));
        Anchor(track, .025f, .41f, .975f, .50f);
        progressFill = Panel(track, "ProgressFill", Pink).GetComponent<Image>();

        statsPanel = Panel(content, "EquippedStats", new Color(.035f, .055f, .085f));
        multiplierText = Label(statsPanel, "", 33, Gold);
        statsText = Label(statsPanel, "", 40, Color.white);
        Anchor(multiplierText.rectTransform, .025f, .57f, .975f, .98f);
        Anchor(statsText.rectTransform, .025f, .015f, .975f, .56f);

        talkButton = ButtonOf(content, "", 43, new Color(.38f, .13f, .24f));
        talkButton.onClick.AddListener(Talk); talkLabel = talkButton.GetComponentInChildren<Text>();
        resetText = Label(content, "캐릭터마다 하루 한 번 +1 · 한국 시간 00:00 갱신", 29, Muted, TextAnchor.MiddleCenter);
        bagButton = ButtonOf(content, "", 39, new Color(.16f, .125f, .075f));
        bagButton.onClick.AddListener(OpenSelectedBag); bagLabel = bagButton.GetComponentInChildren<Text>();
        gameButton = ButtonOf(content, "", 34, new Color(.09f, .14f, .22f));
        gameButton.onClick.AddListener(OpenSelectedMiniGame); gameLabel = gameButton.GetComponentInChildren<Text>();
    }

    void ApplyLayout(bool force)
    {
        Vector2 viewport = host.rect.size;
        Rect safe = Screen.safeArea;
        if (viewport.x <= 0f || viewport.y <= 0f) return;
        if (!force && viewport == lastViewport && safe == lastSafeArea) return;
        lastViewport = viewport; lastSafeArea = safe;
        float scale = viewport.x / Width;
        float pixels = Width / Mathf.Max(1, Screen.width);
        float top = Mathf.Max(28f, (Screen.height - safe.yMax) * pixels + 16f);
        float bottom = Mathf.Max(28f, safe.yMin * pixels + 16f);
        float side = Mathf.Max(32f, Mathf.Max(safe.xMin, Screen.width - safe.xMax) * pixels + 16f);
        float w = Width - 2f * side;
        float h = Mathf.Max(1700f + top + bottom, viewport.y / scale);
        content.sizeDelta = new Vector2(Width, h);
        content.localScale = Vector3.one * scale;
        Top(title.rectTransform, side, top, w - 210f, 88f);
        Top(closeButton.GetComponent<RectTransform>(), Width - side - 180f, top, 180f, 88f);
        Top(subtitle.rectTransform, side, top + 96f, w, 56f);
        Top(selector, side, top + 170f, w, 144f);
        float heroTop = top + 332f;
        float affinityTop = h - bottom - 626f;
        Top(hero, side, heroTop, w, affinityTop - heroTop - 18f);
        Top(affinityPanel, side, affinityTop, w, 170f);
        Top(statsPanel, side, affinityTop + 186f, w, 142f);
        Top(talkButton.GetComponent<RectTransform>(), side, affinityTop + 346f, w, 112f);
        Top(resetText.rectTransform, side, affinityTop + 466f, w, 44f);
        Top(bagButton.GetComponent<RectTransform>(), side, affinityTop + 530f, w * .57f, 96f);
        Top(gameButton.GetComponent<RectTransform>(), side + w * .59f, affinityTop + 530f, w * .41f, 96f);
    }

    void Select(int id)
    {
        if (!XTapJailAffinity.IsCaptured(id)) return;
        selectedCharacterId = id;
        feedbackAge = 10f;
        Refresh();
    }

    void Refresh()
    {
        displayedDay = XTapJailAffinity.Today;
        int captured = 0, available = 0;
        for (int i = 0; i < cards.Length; i++)
        {
            int id = i + 1;
            bool owned = XTapJailAffinity.IsCaptured(id);
            bool ready = owned && XTapJailAffinity.CanTalk(id);
            if (owned) captured++;
            if (ready) available++;
            cards[i].interactable = owned;
            cards[i].GetComponent<Image>().color = selectedCharacterId == id
                ? new Color(.27f, .15f, .22f) : new Color(.07f, .09f, .14f);
            string dailyStatus = !owned ? "" : XTapJailAffinity.HasSaveError(id) ? "저장 확인 필요"
                : XTapJailAffinity.ClockBehind(id) ? "날짜 확인 필요" : ready ? "대화 +1 가능" : "대화 보상 완료";
            cardLabels[i].text = owned
                ? id + "층 · " + ShortName(id) + "\n호감도 " + XTapJailAffinity.Points(id) + "\n" + dailyStatus
                : id + "층\n미포획\n포획 후 교감";
            cardLabels[i].color = owned ? (ready ? Gold : Color.white) : Muted;
        }
        subtitle.text = "포획 " + captured + "/10명 · 오늘 대화 가능한 캐릭터 " + available + "명";
        RefreshDetail();
    }

    void RefreshDetail()
    {
        int id = selectedCharacterId;
        bool owned = XTapJailAffinity.IsCaptured(id);
        if (displayedCharacterId != id)
        {
            displayedCharacterId = id;
            portrait.sprite = owned && assets != null ? assets.GetSprite("assets/f" + id + "_cap.jpg") : null;
            if (owned && assets != null && portrait.sprite == null)
                portrait.sprite = assets.GetSprite("assets/f" + id + "_p00.jpg");
            if (portrait.sprite != null) portraitFit.aspectRatio = portrait.sprite.rect.width / portrait.sprite.rect.height;
            portrait.enabled = portrait.sprite != null;
            speechText.text = owned ? XTapJailDialogue.Line(id, 0) : "0% 룰렛에서 캐릭터를 포획하면\n대화와 전용 가방이 열립니다.";
        }
        nameText.text = owned ? id + "층 · " + XTapCharacterDialogue.CharacterName(id) : "아직 포획된 캐릭터가 없습니다";
        portraitHint.text = !owned ? "캐릭터를 포획하고 다시 찾아오세요"
            : portrait.sprite == null ? "초상화 로드 실패 · 아래 버튼으로 대화할 수 있습니다"
            : "캐릭터를 터치하면 대화합니다";
        int points = owned ? XTapJailAffinity.Points(id) : 0;
        int capacity = owned ? inventory.GetBagCapacity(id) : 24;
        affinityText.text = "호감도 " + points;
        capacityText.text = "전용 가방 " + capacity + "칸";
        nextCellText.text = owned ? "가방 +1칸까지 호감도 " + XTapJailAffinity.PointsToNextCell(id) + " · 누적 확장 +" + XTapJailAffinity.ExtraCells(id) + "칸"
            : "호감도 100마다 해당 캐릭터 가방 +1칸";
        Anchor(progressFill.rectTransform, 0f, 0f, (points % 100) / 100f, 1f);
        double attack = 0, defense = 0, hp = 0, multiplier = 1;
        if (owned) inventory.GetBagDisplayStats(id, out attack, out defense, out hp, out multiplier);
        multiplierText.text = "이 가방 장비 · 전체 수식어 ×" + XTapStatFormat.Compact(multiplier) + " 적용";
        statsText.text = "공격 +" + XTapStatFormat.Compact(attack) + "   방어 +" + XTapStatFormat.Compact(defense) + "   체력 +" + XTapStatFormat.Compact(hp);
        bool error = owned && XTapJailAffinity.HasSaveError(id);
        bool ready = owned && XTapJailAffinity.CanTalk(id);
        talkLabel.text = !owned ? "포획 후 대화 가능" : error ? "호감도 저장을 읽지 못했습니다"
            : ready ? "오늘의 대화 · 호감도 +1" : "다시 대화하기 · 오늘 +1 지급 완료";
        if (owned && XTapJailAffinity.ClockBehind(id)) talkLabel.text = "다시 대화하기 · 날짜 복구 후 보상 가능";
        talkButton.interactable = portraitButton.interactable = owned;
        bagButton.interactable = owned;
        bagLabel.text = "전용 가방 · " + (owned ? inventory.GetEquippedCellCount(id) : 0) + "/" + capacity + "칸";
        bool miniGame = owned && id == 1 && openMiniGame != null;
        gameButton.interactable = miniGame;
        gameLabel.text = miniGame ? "X SIGIL BEAT" : "미니게임 준비 중";
    }

    void Talk()
    {
        if (!IsOpen || inventory.IsOpen || !XTapJailAffinity.IsCaptured(selectedCharacterId) || Time.unscaledTime < nextTouchTime) return;
        nextTouchTime = Time.unscaledTime + .3f;
        bool expanded, saveFailed;
        bool awarded = XTapJailAffinity.TryTalk(selectedCharacterId, out expanded, out saveFailed);
        dialogueIndex++;
        speechText.text = XTapJailDialogue.Line(selectedCharacterId, dialogueIndex);
        feedbackText.text = saveFailed ? "저장 실패 · 다시 시도해 주세요"
            : expanded ? "호감도 +1\n전용 가방 +1칸!"
            : awarded ? "호감도 +1" : XTapJailAffinity.ClockBehind(selectedCharacterId)
            ? "날짜가 복구되면 다시 받을 수 있어요" : "오늘의 교감 보상은 받았어요";
        feedbackText.color = expanded ? Gold : Pink;
        feedbackAge = 0f;
        Refresh();
    }

    void OpenSelectedBag()
    {
        if (XTapJailAffinity.IsCaptured(selectedCharacterId)) inventory.OpenCharacterBag(selectedCharacterId);
    }
    void OpenSelectedMiniGame()
    {
        if (selectedCharacterId != 1 || !XTapJailAffinity.IsCaptured(selectedCharacterId) || openMiniGame == null) return;
        int id = selectedCharacterId; Close(); openMiniGame(id);
    }
    static int FirstCapturedCharacter()
    {
        for (int i = 1; i <= 10; i++) if (XTapJailAffinity.IsCaptured(i)) return i;
        return 0;
    }
    static string ShortName(int id)
    {
        string name = XTapCharacterDialogue.CharacterName(id);
        return string.IsNullOrEmpty(name) ? "캐릭터 " + id : name.Split(' ')[0];
    }
    RectTransform Panel(Transform parent, string name, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        Image img = go.GetComponent<Image>(); img.color = color; img.raycastTarget = false;
        return img.rectTransform;
    }
    Text Label(Transform parent, string text, int size, Color color, TextAnchor align = TextAnchor.MiddleLeft)
    {
        GameObject go = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        go.transform.SetParent(parent, false);
        Text label = go.GetComponent<Text>();
        label.text = text; label.font = font; label.fontSize = size; label.color = color;
        label.fontStyle = FontStyle.Bold; label.alignment = align; label.raycastTarget = false;
        label.horizontalOverflow = HorizontalWrapMode.Wrap; label.verticalOverflow = VerticalWrapMode.Truncate;
        label.resizeTextForBestFit = true; label.resizeTextMinSize = Mathf.RoundToInt(size * .82f); label.resizeTextMaxSize = size;
        return label;
    }
    Button ButtonOf(Transform parent, string text, int size, Color color)
    {
        RectTransform r = Panel(parent, "Button", color);
        Image bg = r.GetComponent<Image>(); bg.raycastTarget = true;
        Button button = r.gameObject.AddComponent<Button>(); button.targetGraphic = bg;
        ColorBlock colors = button.colors;
        colors.pressedColor = new Color(.68f, .63f, .72f); colors.selectedColor = Color.white;
        colors.disabledColor = new Color(.55f, .55f, .58f); button.colors = colors;
        Text label = Label(r, text, size, Gold, TextAnchor.MiddleCenter);
        Anchor(label.rectTransform, .03f, .06f, .97f, .94f);
        return button;
    }
    static void Top(RectTransform r, float x, float y, float w, float h)
    {
        r.anchorMin = r.anchorMax = r.pivot = new Vector2(0f, 1f);
        r.anchoredPosition = new Vector2(x, -y); r.sizeDelta = new Vector2(w, h);
    }
    static void Anchor(RectTransform r, float x1, float y1, float x2, float y2)
    {
        r.anchorMin = new Vector2(x1, y1); r.anchorMax = new Vector2(x2, y2);
        r.offsetMin = r.offsetMax = Vector2.zero;
    }
}
