using System;
using UnityEngine;
using UnityEngine.UI;

public sealed class XTapJail : MonoBehaviour
{
    const float Width = 1080f;
    static readonly Color Gold = new Color(.89f, .75f, .49f);
    static readonly Color Pink = new Color(.93f, .53f, .65f);
    static readonly Color Muted = new Color(.64f, .68f, .75f);
    const float CardStep = 188f;
    const float CardWidth = 176f;
    const float CardHeight = 166f;
    // Framing measured against the ten existing cap portraits. Their faces
    // sit at different heights; keep them between the roster and name panel.
    static readonly float[] PortraitTopOffsets = { 0f, -120f, -100f, 50f, 90f, 110f, -250f, -100f, 20f, 10f };
    static readonly float[] FaceCentersFromTop = { .28f, .29f, .29f, .16f, .12f, .09f, .40f, .26f, .18f, .19f };
    public bool IsOpen { get; private set; }

    RectTransform host, content, hero, portraitViewport, portraitTouchArea, identity, affinityPanel, statsPanel, speechPanel;
    RectTransform headerRule, selectionHint, emptyState;
    ScrollRect characterScroll;
    CanvasGroup portraitFade;
    Text floorLabel, dailyBadge, affinityCaption, progressCaption, equipmentCaption, speakerLabel;
    Text attackValue, defenseValue, hpValue, emptyTitle, emptyHint;
    XTapJailGraphic bondIcon;
    readonly RawImage[] cardPortraits = new RawImage[10];
    readonly Text[] cardStatuses = new Text[10];
    readonly XTapJailGraphic[] cardFrames = new XTapJailGraphic[10];
    readonly GameObject[] cardLocks = new GameObject[10];
    readonly Sprite[] portraitCache = new Sprite[10];
    readonly RectTransform[] motes = new RectTransform[9];
    float portraitAge = 1f, shownProgress, targetProgress;
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
    Text affinityText, nextCellText, capacityText, multiplierText;
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
        RevealSelection();
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
        portraitAge += Time.unscaledDeltaTime;
        float reveal = Mathf.Clamp01(portraitAge / .32f);
        portraitFade.alpha = reveal;
        portrait.rectTransform.localScale = Vector3.one * (1f + .018f * pulse + .018f * (1f - reveal));
        shownProgress = Mathf.MoveTowards(shownProgress, targetProgress, Time.unscaledDeltaTime * 1.5f);
        Anchor(progressFill.rectTransform, 0f, 0f, shownProgress, 1f);
        for (int i = 0; i < motes.Length; i++)
        {
            float travel = Mathf.Repeat(Time.unscaledTime * (9f + i * 1.8f) + i * 153f, 820f);
            motes[i].anchoredPosition = new Vector2(65f + i * 118f + Mathf.Sin(Time.unscaledTime * .2f + i) * 16f, -1020f + travel);
        }
        Color tint = feedbackText.color;
        tint.a = 1f - Mathf.Clamp01((feedbackAge - 1.8f) / .5f);
        feedbackText.color = tint;
    }

    void BuildUi()
    {
        overlay = new GameObject("JailOverlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
        overlay.transform.SetParent(host, false);
        Image bg = overlay.GetComponent<Image>();
        bg.color = new Color(.018f, .025f, .037f, 1f);
        bg.raycastTarget = true;
        Anchor(bg.rectTransform, 0f, 0f, 1f, 1f);
        content = Panel(overlay.transform, "JailContent", Color.clear);
        content.anchorMin = content.anchorMax = content.pivot = new Vector2(0f, 1f);
        pageScroll = overlay.GetComponent<ScrollRect>();
        pageScroll.viewport = bg.rectTransform; pageScroll.content = content;
        pageScroll.horizontal = false; pageScroll.vertical = true;
        pageScroll.movementType = ScrollRect.MovementType.Clamped;
        pageScroll.decelerationRate = .08f;

        // Full-width original portrait, with UI gradients above it. No new character art.
        hero = Panel(content, "PortraitStage", new Color(.025f, .035f, .055f));
        hero.gameObject.AddComponent<RectMask2D>();
        portraitViewport = Panel(hero, "PortraitViewport", Color.clear);
        portraitViewport.gameObject.AddComponent<RectMask2D>();
        portrait = Panel(portraitViewport, "OriginalCharacterPortrait", Color.white).GetComponent<Image>();
        Anchor(portrait.rectTransform, 0f, 0f, 1f, 1f);
        portrait.rectTransform.pivot = new Vector2(.5f, 1f);
        portraitFit = portrait.gameObject.AddComponent<AspectRatioFitter>();
        portraitFit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        portrait.preserveAspect = true;
        portraitFade = portrait.gameObject.AddComponent<CanvasGroup>();
        Shade(hero, "TopShade", 0f, .67f, 1f, 1f, new Color(.015f, .023f, .035f, 1f), Color.clear);
        Shade(hero, "LowerShade", 0f, 0f, 1f, .44f, Color.clear, new Color(.018f, .025f, .037f, 1f));
        RectTransform galleryFrame = Panel(hero, "GalleryFrame", Color.clear);
        Anchor(galleryFrame, .028f, .02f, .972f, .72f);
        Frame(galleryFrame, new Color(Gold.r, Gold.g, Gold.b, .24f));
        for (int i = 0; i < motes.Length; i++)
        {
            motes[i] = Panel(hero, "LightMote_" + i, new Color(1f, .84f, .54f, .16f + (i % 3) * .1f));
            Top(motes[i], 50f + 118f * i, 600f, 3f + i % 3, 3f + i % 3);
            motes[i].localRotation = Quaternion.Euler(0f, 0f, 45f);
        }
        portraitTouchArea = Panel(hero, "PortraitTouchArea", Color.clear);
        portraitTouchArea.GetComponent<Image>().raycastTarget = true;
        portraitButton = portraitTouchArea.gameObject.AddComponent<Button>();
        portraitButton.targetGraphic = portraitTouchArea.GetComponent<Image>();
        portraitButton.transition = Selectable.Transition.None;
        portraitButton.onClick.AddListener(Talk);
        emptyState = Panel(hero, "EmptyCell", Color.clear);
        Anchor(emptyState, .07f, .22f, .93f, .61f);
        XTapJailGraphic emptySeal = Icon(emptyState, XTapJailGraphic.Symbol.Gate, Gold);
        Anchor(emptySeal.rectTransform, .39f, .46f, .61f, .98f);
        emptyTitle = Label(emptyState, "아직 비어 있는 감옥", 56, Gold, TextAnchor.MiddleCenter);
        Anchor(emptyTitle.rectTransform, 0f, .22f, 1f, .45f);
        emptyHint = Label(emptyState, "전투 후 블럭 머신에서 첫 인연을 만나세요", 32, Muted, TextAnchor.MiddleCenter);
        Anchor(emptyHint.rectTransform, 0f, 0f, 1f, .20f);

        XTapJailGraphic crest = Icon(content, XTapJailGraphic.Symbol.Gate, Gold);
        crest.name = "JailCrest";
        title = Label(content, "감옥", 72, new Color(.98f, .94f, .86f));
        subtitle = Label(content, "", 32, Muted);
        closeButton = ButtonOf(content, "닫기", 34, new Color(.065f, .075f, .095f, .95f));
        closeButton.onClick.AddListener(Close);
        headerRule = Panel(content, "HeaderRule", new Color(Gold.r, Gold.g, Gold.b, .32f));
        selectionHint = Label(content, "캐릭터 선택  /  좌우로 넘기기", 26, Muted).rectTransform;

        selector = Panel(content, "CharacterSelector", Color.clear);
        selector.gameObject.AddComponent<RectMask2D>();
        selector.GetComponent<Image>().raycastTarget = true;
        characterScroll = selector.gameObject.AddComponent<ScrollRect>();
        RectTransform stripContent = Panel(selector, "Characters", Color.clear);
        stripContent.anchorMin = stripContent.anchorMax = stripContent.pivot = new Vector2(0f, 1f);
        stripContent.sizeDelta = new Vector2(10 * CardStep - 12f, CardHeight);
        characterScroll.viewport = selector; characterScroll.content = stripContent;
        characterScroll.horizontal = true; characterScroll.vertical = false;
        characterScroll.movementType = ScrollRect.MovementType.Clamped;
        characterScroll.decelerationRate = .08f;
        for (int i = 0; i < cards.Length; i++)
        {
            int id = i + 1;
            RectTransform card = Panel(stripContent, "Character_" + id, new Color(.038f, .047f, .066f, .95f));
            card.GetComponent<Image>().raycastTarget = true;
            cards[i] = card.gameObject.AddComponent<Button>();
            cards[i].targetGraphic = card.GetComponent<Image>();
            cards[i].transition = Selectable.Transition.None;
            Top(card, i * CardStep, 0f, CardWidth, CardHeight);
            RectTransform face = Panel(card, "Portrait", new Color(.06f, .075f, .095f));
            Top(face, 7f, 7f, 162f, 91f);
            face.gameObject.AddComponent<RectMask2D>();
            cardPortraits[i] = new GameObject("CapturedPortrait", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage)).GetComponent<RawImage>();
            cardPortraits[i].transform.SetParent(face, false);
            Anchor(cardPortraits[i].rectTransform, 0f, 0f, 1f, 1f);
            cardPortraits[i].raycastTarget = false;
            Shade(face, "FaceShade", 0f, 0f, 1f, .55f, Color.clear, new Color(0f, 0f, 0f, .65f));
            XTapJailGraphic cellLock = Icon(face, XTapJailGraphic.Symbol.Lock, new Color(.38f, .43f, .51f));
            Top(cellLock.rectTransform, 57f, 15f, 48f, 55f);
            cardLocks[i] = cellLock.gameObject;
            Text number = Label(face, id.ToString("00"), 25, Gold);
            Top(number.rectTransform, 8f, 1f, 52f, 32f);
            cardLabels[i] = Label(card, "", 30, Color.white, TextAnchor.MiddleCenter);
            Top(cardLabels[i].rectTransform, 4f, 100f, 168f, 35f);
            cardStatuses[i] = Label(card, "", 23, Muted, TextAnchor.MiddleCenter);
            Top(cardStatuses[i].rectTransform, 4f, 134f, 168f, 29f);
            cardFrames[i] = Frame(card, Gold);
            cards[i].onClick.AddListener(delegate { Select(id); });
        }

        identity = Panel(content, "CharacterIdentity", Color.clear);
        Shade(identity, "NameReadability", -.012f, -.02f, 1.012f, 1.02f,
            new Color(.015f, .022f, .034f, .42f), new Color(.015f, .022f, .034f, .87f));
        floorLabel = Label(identity, "", 31, Gold);
        Anchor(floorLabel.rectTransform, .005f, .70f, .68f, 1f);
        dailyBadge = Label(identity, "", 30, Pink, TextAnchor.MiddleRight);
        Anchor(dailyBadge.rectTransform, .62f, .70f, .995f, 1f);
        nameText = Label(identity, "", 62, new Color(1f, .97f, .9f));
        Anchor(nameText.rectTransform, 0f, .24f, 1f, .72f);
        nameText.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(0f, -3f);
        portraitHint = Label(identity, "", 28, new Color(.78f, .81f, .85f));
        Anchor(portraitHint.rectTransform, .005f, 0f, .995f, .25f);

        speechPanel = Surface(content, "Conversation", new Color(.035f, .042f, .060f, .97f), .52f);
        speakerLabel = Label(speechPanel, "", 26, Gold);
        Anchor(speakerLabel.rectTransform, .035f, .72f, .965f, .95f);
        speechText = Label(speechPanel, "", 42, new Color(.96f, .95f, .94f));
        speechText.lineSpacing = 1.12f;
        Anchor(speechText.rectTransform, .035f, .09f, .965f, .73f);
        feedbackText = Label(content, "", 43, Pink, TextAnchor.MiddleCenter);
        feedbackText.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, .9f);

        affinityPanel = Surface(content, "AffinityCard", new Color(.10f, .052f, .070f, .98f), .55f);
        bondIcon = Icon(affinityPanel, XTapJailGraphic.Symbol.Heart, Pink);
        Top(bondIcon.rectTransform, 24f, 24f, 44f, 44f);
        affinityCaption = Label(affinityPanel, "호감도", 30, new Color(.83f, .68f, .73f));
        Anchor(affinityCaption.rectTransform, .087f, .60f, .23f, .94f);
        affinityText = Label(affinityPanel, "", 56, Pink);
        Anchor(affinityText.rectTransform, .23f, .57f, .53f, .97f);
        capacityText = Label(affinityPanel, "", 36, Gold, TextAnchor.MiddleRight);
        Anchor(capacityText.rectTransform, .53f, .63f, .97f, .94f);
        nextCellText = Label(affinityPanel, "", 29, Muted);
        Anchor(nextCellText.rectTransform, .035f, .02f, .73f, .30f);
        progressCaption = Label(affinityPanel, "", 28, Pink, TextAnchor.MiddleRight);
        Anchor(progressCaption.rectTransform, .73f, .02f, .97f, .30f);
        RectTransform track = Panel(affinityPanel, "ProgressTrack", new Color(.025f, .018f, .030f));
        Anchor(track, .035f, .37f, .965f, .45f);
        progressFill = Panel(track, "ProgressFill", Pink).GetComponent<Image>();
        progressFill.gameObject.AddComponent<XTapJailGradient>();

        statsPanel = Surface(content, "EquippedStats", new Color(.033f, .049f, .068f, .98f), .30f);
        equipmentCaption = Label(statsPanel, "장비 능력치", 26, Muted);
        Anchor(equipmentCaption.rectTransform, .025f, .69f, .32f, .98f);
        multiplierText = Label(statsPanel, "", 27, Gold, TextAnchor.MiddleRight);
        Anchor(multiplierText.rectTransform, .32f, .69f, .975f, .98f);
        attackValue = StatColumn(statsPanel, "공격", .025f, new Color(1f, .74f, .57f));
        defenseValue = StatColumn(statsPanel, "방어", .355f, new Color(.57f, .77f, .91f));
        hpValue = StatColumn(statsPanel, "체력", .685f, new Color(.94f, .58f, .64f));

        talkButton = ButtonOf(content, "", 43, new Color(.39f, .20f, .22f));
        talkButton.onClick.AddListener(Talk); talkLabel = talkButton.GetComponentInChildren<Text>();
        IconInButton(talkButton, XTapJailGraphic.Symbol.Heart, Pink);
        resetText = Label(content, "하루 첫 대화 +1  ·  한국 시간 00:00 갱신", 27, Muted, TextAnchor.MiddleCenter);
        bagButton = ButtonOf(content, "", 36, new Color(.12f, .102f, .075f));
        bagButton.onClick.AddListener(OpenSelectedBag); bagLabel = bagButton.GetComponentInChildren<Text>();
        IconInButton(bagButton, XTapJailGraphic.Symbol.Bag, Gold);
        gameButton = ButtonOf(content, "", 32, new Color(.055f, .085f, .125f));
        gameButton.onClick.AddListener(OpenSelectedMiniGame); gameLabel = gameButton.GetComponentInChildren<Text>();
        IconInButton(gameButton, XTapJailGraphic.Symbol.Rune, new Color(.59f, .78f, .94f));
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
        float top = Mathf.Max(32f, (Screen.height - safe.yMax) * pixels + 16f);
        float bottom = Mathf.Max(28f, safe.yMin * pixels + 16f);
        float side = Mathf.Max(34f, Mathf.Max(safe.xMin, Screen.width - safe.xMax) * pixels + 16f);
        float w = Width - 2f * side;
        float h = Mathf.Max(1920f, Mathf.Max(top + bottom + 1780f, viewport.y / scale));
        content.sizeDelta = new Vector2(Width, h);
        content.localScale = Vector3.one * scale;
        Top((RectTransform)content.Find("JailCrest"), side, top + 3f, 70f, 82f);
        Top(title.rectTransform, side + 92f, top - 4f, w - 280f, 100f);
        Top(closeButton.GetComponent<RectTransform>(), Width - side - 154f, top + 4f, 154f, 80f);
        Top(subtitle.rectTransform, side, top + 96f, w, 44f);
        Top(headerRule, side, top + 153f, w, 1.5f);
        Top(selector, side, top + 202f, w, CardHeight);
        Top(selectionHint, side, top + 160f, w, 36f);

        float resetTop = h - bottom - 40f;
        float actionsTop = resetTop - 132f;
        float talkTop = actionsTop - 138f;
        float statsTop = talkTop - 140f;
        float bondTop = statsTop - 196f;
        float speechTop = bondTop - 190f;
        float nameTop = speechTop - 156f;
        Top(hero, 0f, 0f, Width, bondTop + 160f);
        float portraitTop = top + 252f + (selectedCharacterId >= 1 && selectedCharacterId <= 10
            ? PortraitTopOffsets[selectedCharacterId - 1] : 0f);
        Top(portraitViewport, 0f, portraitTop, Width, bondTop + 160f - portraitTop);
        Top(portraitTouchArea, side, top + 384f, w, nameTop - top - 384f);
        Top(emptyState, side, (top + 368f + nameTop - 360f) * .5f, w, 360f);
        Top(identity, side + 8f, nameTop, w - 16f, 140f);
        Top(speechPanel, side, speechTop, w, 174f);
        Top(affinityPanel, side, bondTop, w, 180f);
        Top(statsPanel, side, statsTop, w, 124f);
        Top(talkButton.GetComponent<RectTransform>(), side, talkTop, w, 118f);
        Top(bagButton.GetComponent<RectTransform>(), side, actionsTop, (w - 18f) * .55f, 112f);
        Top(gameButton.GetComponent<RectTransform>(), side + (w - 18f) * .55f + 18f, actionsTop, (w - 18f) * .45f, 112f);
        Top(resetText.rectTransform, side, resetTop, w, 40f);
        Top(feedbackText.rectTransform, side, nameTop - 104f, w, 98f);
    }

    void RevealSelection()
    {
        if (characterScroll == null || selectedCharacterId < 1) return;
        float overflow = characterScroll.content.rect.width - selector.rect.width;
        characterScroll.StopMovement();
        characterScroll.horizontalNormalizedPosition = overflow <= 0f ? 0f :
            Mathf.Clamp01(((selectedCharacterId - 1) * CardStep - (selector.rect.width - CardWidth) * .5f) / overflow);
    }

    Sprite PortraitFor(int id)
    {
        if (id < 1 || id > 10 || assets == null || !XTapJailAffinity.IsCaptured(id)) return null;
        if (portraitCache[id - 1] == null)
        {
            portraitCache[id - 1] = assets.GetSprite("assets/f" + id + "_cap.jpg");
            if (portraitCache[id - 1] == null) portraitCache[id - 1] = assets.GetSprite("assets/f" + id + "_p00.jpg");
        }
        return portraitCache[id - 1];
    }

    void Select(int id)
    {
        if (!XTapJailAffinity.IsCaptured(id)) return;
        selectedCharacterId = id;
        feedbackAge = 10f;
        Refresh();
        RevealSelection();
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
            bool selected = owned && selectedCharacterId == id;
            cards[i].GetComponent<Image>().color = selected
                ? new Color(.15f, .12f, .095f, .98f) : new Color(.035f, .045f, .064f, .96f);
            cardFrames[i].color = selected ? Gold : new Color(Gold.r, Gold.g, Gold.b, owned ? .35f : .15f);
            string dailyStatus = !owned ? "포획 후 해금" : XTapJailAffinity.HasSaveError(id) ? "저장 확인"
                : XTapJailAffinity.ClockBehind(id) ? "날짜 확인" : ready ? "교감 +1 가능" : "오늘 교감 완료";
            cardLabels[i].text = owned ? ShortName(id) : "미포획";
            cardStatuses[i].text = dailyStatus;
            cardLabels[i].color = owned ? (selected ? Gold : Color.white) : Muted;
            cardStatuses[i].color = ready ? Pink : Muted;
            Sprite face = owned ? PortraitFor(id) : null;
            cardPortraits[i].texture = face != null ? face.texture : null;
            cardPortraits[i].enabled = face != null;
            if (face != null)
            {
                float cropHeight = Mathf.Clamp01(face.rect.width / face.rect.height * 91f / 162f);
                float cropY = Mathf.Clamp(1f - FaceCentersFromTop[i] - cropHeight * .5f, 0f, 1f - cropHeight);
                cardPortraits[i].uvRect = new Rect(0f, cropY, 1f, cropHeight);
            }
            cardLocks[i].SetActive(!owned);
        }
        subtitle.text = "포획 " + captured + " / 10명    ·    오늘 교감 가능 " + available + "명";
        RefreshDetail();
    }

    void RefreshDetail()
    {
        int id = selectedCharacterId;
        bool owned = XTapJailAffinity.IsCaptured(id);
        if (displayedCharacterId != id)
        {
            displayedCharacterId = id;
            portrait.sprite = owned ? PortraitFor(id) : null;
            portraitAge = 0f;
            portraitFade.alpha = 0f;
            if (portrait.sprite != null) portraitFit.aspectRatio = portrait.sprite.rect.width / portrait.sprite.rect.height;
            portrait.enabled = portrait.sprite != null;
            ApplyLayout(true);
            speechText.text = owned ? XTapJailDialogue.Line(id, 0) : "블럭 머신에서 0 선택 후 포획 확률 1%\n포획하면 대화와 전용 가방이 열립니다.";
        }
        emptyState.gameObject.SetActive(!owned || portrait.sprite == null);
        emptyTitle.text = owned ? "초상화를 불러오지 못했습니다" : "아직 비어 있는 감옥";
        emptyHint.text = owned ? "대화와 전용 가방은 아래에서 이용하실 수 있습니다" : "전투 후 블럭 머신에서 첫 인연을 만나세요";
        floorLabel.text = owned ? id.ToString("00") + "층  /  포획 캐릭터" : "교감의 기록";
        nameText.text = owned ? XTapCharacterDialogue.CharacterName(id) : "첫 만남을 기다리며";
        speakerLabel.text = owned ? ShortName(id) + "의 이야기" : "감옥 이용 안내";
        portraitHint.text = !owned ? "캐릭터를 포획하고 다시 찾아오세요"
            : portrait.sprite == null ? "초상화 로드 실패 · 아래 버튼으로 대화할 수 있습니다"
            : "캐릭터를 터치해 이야기를 나누세요";
        int points = owned ? XTapJailAffinity.Points(id) : 0;
        int capacity = owned ? inventory.GetBagCapacity(id) : 24;
        affinityText.text = XTapStatFormat.Compact(points);
        capacityText.text = "가방 " + capacity + "칸 · 확장 +" + (owned ? XTapJailAffinity.ExtraCells(id) : 0);
        nextCellText.text = owned ? "가방 +1칸까지 " + XTapJailAffinity.PointsToNextCell(id) + " 남음"
            : "호감도 100마다 전용 가방 +1칸";
        progressCaption.text = (points % 100) + " / 100";
        targetProgress = (points % 100) / 100f;
        double attack = 0, defense = 0, hp = 0, multiplier = 1;
        if (owned) inventory.GetBagDisplayStats(id, out attack, out defense, out hp, out multiplier);
        multiplierText.text = "수식어 ×" + XTapStatFormat.Compact(multiplier) +
            " · 전체 블럭 +" + inventory.ExclusiveEquipmentBonusPercent + "%";
        attackValue.text = "+" + XTapStatFormat.Compact(attack);
        defenseValue.text = "+" + XTapStatFormat.Compact(defense);
        hpValue.text = "+" + XTapStatFormat.Compact(hp);
        bool error = owned && XTapJailAffinity.HasSaveError(id);
        bool ready = owned && XTapJailAffinity.CanTalk(id);
        dailyBadge.text = !owned ? "미포획" : error ? "저장 확인 필요" : ready ? "오늘 +1 가능" : "오늘 +1 완료";
        talkLabel.text = !owned ? "포획 후 대화 가능" : error ? "대화하기 · 저장 확인 필요"
            : ready ? "오늘의 대화   ·   호감도 +1" : "다시 이야기 나누기";
        if (owned && XTapJailAffinity.ClockBehind(id)) dailyBadge.text = "날짜 확인 필요";
        talkButton.interactable = portraitButton.interactable = owned;
        bagButton.interactable = owned;
        bagLabel.text = "전용 가방\n" + (owned ? inventory.GetEquippedCellCount(id) : 0) + "/" + capacity + "칸";
        bool miniGame = owned && id == 1 && openMiniGame != null;
        gameButton.interactable = miniGame;
        gameLabel.text = miniGame ? "미니게임\nX SIGIL BEAT" : "미니게임\n준비 중";
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
    RectTransform Surface(Transform parent, string name, Color tint, float edgeAlpha)
    {
        RectTransform rect = Panel(parent, name, tint);
        rect.gameObject.AddComponent<XTapJailGradient>();
        Frame(rect, new Color(Gold.r, Gold.g, Gold.b, edgeAlpha));
        return rect;
    }
    XTapJailGraphic Frame(RectTransform parent, Color tint)
    {
        XTapJailGraphic frame = Icon(parent, XTapJailGraphic.Symbol.Frame, tint);
        Anchor(frame.rectTransform, 0f, 0f, 1f, 1f);
        return frame;
    }
    XTapJailGraphic Icon(Transform parent, XTapJailGraphic.Symbol symbol, Color tint)
    {
        var icon = new GameObject(symbol.ToString(), typeof(RectTransform), typeof(CanvasRenderer), typeof(XTapJailGraphic)).GetComponent<XTapJailGraphic>();
        icon.transform.SetParent(parent, false);
        icon.symbol = symbol; icon.color = tint; icon.raycastTarget = false;
        icon.SetVerticesDirty();
        return icon;
    }
    void Shade(RectTransform parent, string name, float x1, float y1, float x2, float y2, Color top, Color bottom)
    {
        RectTransform rect = Panel(parent, name, Color.white);
        Anchor(rect, x1, y1, x2, y2);
        XTapJailGradient gradient = rect.gameObject.AddComponent<XTapJailGradient>();
        gradient.top = top; gradient.bottom = bottom;
        rect.GetComponent<Image>().SetVerticesDirty();
    }
    Text StatColumn(RectTransform parent, string caption, float left, Color tint)
    {
        Text label = Label(parent, caption, 28, Muted);
        Anchor(label.rectTransform, left, .09f, left + .10f, .64f);
        Text value = Label(parent, "", 43, tint, TextAnchor.MiddleRight);
        Anchor(value.rectTransform, left + .10f, .09f, left + .285f, .64f);
        return value;
    }
    void IconInButton(Button button, XTapJailGraphic.Symbol symbol, Color tint)
    {
        XTapJailGraphic icon = Icon(button.transform, symbol, tint);
        icon.rectTransform.anchorMin = icon.rectTransform.anchorMax = new Vector2(0f, .5f);
        icon.rectTransform.sizeDelta = new Vector2(42f, 42f);
        icon.rectTransform.anchoredPosition = new Vector2(46f, 0f);
        Text label = button.GetComponentInChildren<Text>();
        Anchor(label.rectTransform, 0f, .05f, 1f, .95f);
        label.rectTransform.offsetMin = new Vector2(82f, 0f);
        label.rectTransform.offsetMax = new Vector2(-18f, 0f);
    }
    Button ButtonOf(Transform parent, string text, int size, Color tint)
    {
        RectTransform r = Surface(parent, "Button", tint, .58f);
        Image bg = r.GetComponent<Image>(); bg.raycastTarget = true;
        Button button = r.gameObject.AddComponent<Button>(); button.targetGraphic = bg;
        ColorBlock colors = button.colors;
        colors.highlightedColor = new Color(1.12f, 1.08f, 1.02f);
        colors.pressedColor = new Color(.68f, .63f, .68f); colors.selectedColor = Color.white;
        colors.disabledColor = new Color(.48f, .48f, .50f); button.colors = colors;
        Text label = Label(r, text, size, new Color(.97f, .9f, .76f), TextAnchor.MiddleCenter);
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
