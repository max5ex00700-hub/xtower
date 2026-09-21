using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public sealed class XTapBattleController : MonoBehaviour
{
    const int TestFloor = 1;
    const int EnemyMaxHp = 60;

    XTapOriginalApkAssets assets;
    AudioSource audioSource;

    Canvas canvas;
    RectTransform root;
    Image battleImage;
    AspectRatioFitter battleFitter;
    Image bubblePanel;
    Text bubbleText;
    CanvasGroup bubbleGroup;
    Image weakPoint;
    XTapGachaMachine gachaMachine;

    GameObject mainOverlay;
    Text mainFloorText;
    Text mainStatusText;

    Font koreanFont;
    Sprite ringSprite;
    Sprite speechBubbleSprite;

    int enemyHp = EnemyMaxHp;
    int hitCount;
    int fightCount;
    bool busy;
    bool won;

    Vector2 pointerStart;
    float pointerStartTime;
    bool pointerTracking;

    bool weakActive;
    float weakUntil;
    Vector2 weakNorm;
    Vector2 weakVelocity;

    readonly string[][] zoneTalk =
    {
        new []{"머리… 만지지 마.","흩트리지 마.","전투 중이잖아."},
        new []{"얼굴을 노리는 거야?","가까이 오지 마.","…시선이 거슬려."},
        new []{"윽… 거긴.","정면으로 오는군.","숨이 막혀."},
        new []{"거긴 안 돼!","손 치워!","잠깐…!"},
        new []{"다리를 노리는군.","균형이…","무릎이 풀려."},
        new []{"발끝까지 노려?","거긴 빗나갔어.","아래를 보는군."},
        new []{"허공이야.","나는 이쪽이야.","빗나갔어."}
    };

    readonly string[] dodgeTalk = {"느려.","피했어.","거긴 아니야.","다 보여."};
    readonly string[] criticalTalk = {"윽… 거긴!","잠깐…!","그걸 찾았어?","균형이… 깨졌어."};

    IEnumerator Start()
    {
        Application.targetFrameRate = 60;
        Screen.orientation = ScreenOrientation.Portrait;
        Screen.fullScreen = true;

        koreanFont = CreateKoreanFont();
        ringSprite = CreateRingSprite(128, 9);
        speechBubbleSprite = CreateSpeechBubbleSprite(320, 120);

        BuildBattleOnlyUi();
        BuildMainUi();

        gachaMachine = gameObject.AddComponent<XTapGachaMachine>();
        gachaMachine.Initialize(root, koreanFont, ReturnToMain);

        var assetGo = new GameObject("OriginalApkAssets");
        assets = assetGo.AddComponent<XTapOriginalApkAssets>();
        yield return assets.Load();

        audioSource = gameObject.AddComponent<AudioSource>();

        if (!assets.Ready)
        {
            ShowBubble("전투 이미지 데이터를 불러오지 못했습니다.", 10f);
            yield break;
        }

        yield return PreloadCurrentImages();
        ReturnToMain();
    }

    void Update()
    {
        if (assets == null || !assets.Ready) return;

        UpdateWeakPoint();

        if (mainOverlay != null && mainOverlay.activeSelf) return;
        if (gachaMachine != null && gachaMachine.IsOpen) return;
        if (busy) return;

        if (Input.touchCount > 0)
        {
            Touch t = Input.GetTouch(0);
            if (t.phase == TouchPhase.Began)
            {
                pointerStart = t.position;
                pointerStartTime = Time.unscaledTime;
                pointerTracking = true;
            }
            else if (pointerTracking && (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled))
            {
                pointerTracking = false;
                ProcessGesture(pointerStart, t.position, Time.unscaledTime - pointerStartTime);
            }
            return;
        }

#if UNITY_EDITOR
        if (Input.GetMouseButtonDown(0))
        {
            pointerStart = Input.mousePosition;
            pointerStartTime = Time.unscaledTime;
            pointerTracking = true;
        }
        else if (pointerTracking && Input.GetMouseButtonUp(0))
        {
            pointerTracking = false;
            ProcessGesture(pointerStart, Input.mousePosition, Time.unscaledTime - pointerStartTime);
        }
#endif
    }

    void BuildBattleOnlyUi()
    {
        // Unity UI Buttons need an EventSystem. The battle prototype previously
        // used raw Input touches only, so no EventSystem existed and every main
        // screen Button looked correct but ignored taps on device.
        if (FindObjectOfType<EventSystem>() == null)
        {
            var eventGo = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            eventGo.transform.SetParent(transform, false);
        }

        var canvasGo = new GameObject("BattleCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);
        canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = .5f;

        root = new GameObject("FullScreen", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(RectMask2D))
            .GetComponent<RectTransform>();
        root.SetParent(canvas.transform, false);
        Anchor(root, 0, 0, 1, 1);
        root.GetComponent<Image>().color = Color.black;

        battleImage = new GameObject("Character", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(AspectRatioFitter))
            .GetComponent<Image>();
        battleImage.transform.SetParent(root, false);
        battleImage.color = Color.white;
        battleImage.raycastTarget = false;
        battleImage.preserveAspect = false;
        battleFitter = battleImage.GetComponent<AspectRatioFitter>();
        battleFitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        Anchor(battleImage.rectTransform, 0, 0, 1, 1);

        weakPoint = new GameObject("WeakPoint", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
        weakPoint.transform.SetParent(root, false);
        weakPoint.sprite = ringSprite;
        weakPoint.color = new Color(1f, .78f, .16f, .95f);
        weakPoint.raycastTarget = false;
        weakPoint.gameObject.SetActive(false);
        SetSize(weakPoint.rectTransform, 94, 94);

        bubblePanel = new GameObject("SpeechBubble", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup)).GetComponent<Image>();
        bubblePanel.transform.SetParent(root, false);
        bubblePanel.sprite = speechBubbleSprite;
        bubblePanel.color = new Color(1f, .98f, .94f, .97f);
        bubblePanel.raycastTarget = false;
        RectTransform bubbleRect = bubblePanel.rectTransform;
        bubbleRect.anchorMin = bubbleRect.anchorMax = new Vector2(0f, 1f);
        bubbleRect.pivot = new Vector2(0f, 1f);
        bubbleRect.sizeDelta = new Vector2(430f, 132f);
        bubbleRect.anchoredPosition = new Vector2(26f, -34f);

        bubbleGroup = bubblePanel.GetComponent<CanvasGroup>();
        bubbleGroup.alpha = 0;

        bubbleText = MakeText(bubblePanel.transform, "", 28, TextAnchor.MiddleCenter, true);
        bubbleText.color = new Color(.12f, .08f, .09f, 1);
        Anchor(bubbleText.rectTransform, .08f, .20f, .92f, .92f);
    }


    void BuildMainUi()
    {
        mainOverlay = new GameObject("MainScreen", typeof(RectTransform));
        mainOverlay.transform.SetParent(root, false);
        RectTransform mainRoot = mainOverlay.GetComponent<RectTransform>();
        Anchor(mainRoot, 0, 0, 1, 1);

        // The combat art remains the live background so the main screen can later
        // follow each floor/character instead of becoming one flattened screenshot.
        MakePanel(mainOverlay.transform, "TopShade", new Color(0f, 0f, 0f, .48f), 0f, .66f, 1f, 1f);
        MakePanel(mainOverlay.transform, "BottomShade", new Color(.015f, .012f, .014f, .82f), 0f, 0f, 1f, .37f);
        MakePanel(mainOverlay.transform, "LeftMist", new Color(.02f, .012f, .018f, .34f), 0f, .53f, .48f, .88f);

        // X탑 logo: large blood-red X with pale stone-white 탑.
        Text logoX = MakeOutlinedText(mainOverlay.transform, "X", 132, TextAnchor.MiddleCenter, true);
        logoX.color = new Color(.62f, .015f, .02f, 1f);
        Anchor(logoX.rectTransform, .025f, .815f, .185f, .985f);

        Text logoTower = MakeOutlinedText(mainOverlay.transform, "탑", 116, TextAnchor.MiddleCenter, true);
        logoTower.color = new Color(.93f, .91f, .87f, 1f);
        Anchor(logoTower.rectTransform, .155f, .82f, .355f, .98f);

        string displayVersion = Application.version;
        int dash = displayVersion.IndexOf('-');
        if (dash > 0) displayVersion = displayVersion.Substring(0, dash);
        Text version = MakeOutlinedText(mainOverlay.transform, "v " + displayVersion, 29, TextAnchor.MiddleCenter, false);
        version.color = new Color(.82f, .78f, .74f, 1f);
        Anchor(version.rectTransform, .205f, .785f, .365f, .83f);

        Image codePlate = MakePanel(mainOverlay.transform, "BuildCode", new Color(.035f, .03f, .03f, .88f), .785f, .935f, .965f, .982f);
        AddFrame(codePlate.rectTransform, new Color(.48f, .43f, .36f, .85f), 2.5f);
        Text codeText = MakeOutlinedText(codePlate.transform, "코드 1035", 25, TextAnchor.MiddleCenter, false);
        codeText.color = new Color(.90f, .87f, .82f, 1f);
        Anchor(codeText.rectTransform, .05f, .04f, .95f, .96f);

        // Floor information sits on the left, matching the supplied gothic concept.
        Text floorWord = MakeOutlinedText(mainOverlay.transform, "FLOOR", 48, TextAnchor.MiddleLeft, true);
        floorWord.color = new Color(.92f, .91f, .88f, 1f);
        Anchor(floorWord.rectTransform, .045f, .675f, .245f, .745f);

        mainFloorText = MakeOutlinedText(mainOverlay.transform, TestFloor.ToString(), 84, TextAnchor.MiddleLeft, true);
        mainFloorText.color = new Color(.73f, .015f, .02f, 1f);
        Anchor(mainFloorText.rectTransform, .245f, .66f, .36f, .755f);

        mainStatusText = MakeOutlinedText(mainOverlay.transform, "그녀가 기다리고 있다...", 30, TextAnchor.MiddleLeft, false);
        mainStatusText.color = new Color(.94f, .91f, .86f, 1f);
        Anchor(mainStatusText.rectTransform, .045f, .625f, .53f, .675f);

        // Main-screen speech bubble. This is separate from the combat reaction bubble.
        Image mainBubble = new GameObject("MainSpeechBubble", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
        mainBubble.transform.SetParent(mainOverlay.transform, false);
        mainBubble.sprite = speechBubbleSprite;
        mainBubble.color = new Color(.96f, .95f, .92f, .97f);
        mainBubble.raycastTarget = false;
        Anchor(mainBubble.rectTransform, .665f, .705f, .965f, .795f);
        Text mainBubbleText = MakeText(mainBubble.transform, "...또 오는 거야?", 27, TextAnchor.MiddleCenter, false);
        mainBubbleText.color = new Color(.08f, .065f, .06f, 1f);
        Anchor(mainBubbleText.rectTransform, .07f, .18f, .94f, .92f);

        // Large central action button.
        Button fight = MakeGothicButton(mainOverlay.transform, "그녀를 베다", 52);
        RectTransform fr = fight.GetComponent<RectTransform>();
        fr.anchorMin = new Vector2(.205f, .185f);
        fr.anchorMax = new Vector2(.795f, .305f);
        fr.offsetMin = fr.offsetMax = Vector2.zero;
        fight.onClick.AddListener(BeginBattle);

        // Bottom navigation bar. Only systems already present are interactive.
        Image navRail = MakePanel(mainOverlay.transform, "BottomRail", new Color(.018f, .016f, .018f, .96f), 0f, 0f, 1f, .155f);
        AddFrame(navRail.rectTransform, new Color(.36f, .31f, .25f, 1f), 2f);

        string[] icons = {"↻", "↓", "↑", "▣", "⚒", "▥"};
        string[] labels = {"다시", "↓", "↑", "배낭", "대장간", "감옥"};

        for (int i = 0; i < labels.Length; i++)
        {
            Button b = MakeNavButton(navRail.transform, icons[i], labels[i]);
            RectTransform br = b.GetComponent<RectTransform>();
            float x1 = .018f + i * .1635f;
            float x2 = x1 + .145f;
            br.anchorMin = new Vector2(x1, .10f);
            br.anchorMax = new Vector2(x2, .88f);
            br.offsetMin = br.offsetMax = Vector2.zero;

            if (i == 0)
                b.onClick.AddListener(ReturnToMain);
            else
            {
                // Floor navigation / bag / forge / jail are intentionally not
                // faked here. Their visuals are present, but functionality will
                // be connected in their own implementation steps.
                b.interactable = false;
                ColorBlock cb = b.colors;
                cb.disabledColor = Color.white;
                b.colors = cb;
            }
        }

        mainOverlay.SetActive(false);
    }

    Image MakePanel(Transform parent, string name, Color color, float x1, float y1, float x2, float y2)
    {
        Image panel = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
        panel.transform.SetParent(parent, false);
        panel.color = color;
        panel.raycastTarget = false;
        Anchor(panel.rectTransform, x1, y1, x2, y2);
        return panel;
    }

    Text MakeOutlinedText(Transform parent, string value, int size, TextAnchor alignment, bool bold)
    {
        Text t = MakeText(parent, value, size, alignment, bold);
        Outline outline = t.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, .88f);
        outline.effectDistance = new Vector2(2.5f, -2.5f);
        outline.useGraphicAlpha = true;
        return t;
    }

    void AddFrame(RectTransform parent, Color color, float thickness)
    {
        Image top = MakePanel(parent, "FrameTop", color, 0f, 1f, 1f, 1f);
        top.rectTransform.sizeDelta = new Vector2(0f, thickness);
        Image bottom = MakePanel(parent, "FrameBottom", color, 0f, 0f, 1f, 0f);
        bottom.rectTransform.sizeDelta = new Vector2(0f, thickness);
        Image left = MakePanel(parent, "FrameLeft", color, 0f, 0f, 0f, 1f);
        left.rectTransform.sizeDelta = new Vector2(thickness, 0f);
        Image right = MakePanel(parent, "FrameRight", color, 1f, 0f, 1f, 1f);
        right.rectTransform.sizeDelta = new Vector2(thickness, 0f);
    }

    Button MakeGothicButton(Transform parent, string label, int fontSize)
    {
        GameObject go = new GameObject("MainFightButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);

        Image outer = go.GetComponent<Image>();
        outer.color = new Color(.22f, .018f, .025f, .98f);
        AddFrame(outer.rectTransform, new Color(.72f, .13f, .10f, 1f), 6f);

        Image inner = MakePanel(go.transform, "Inset", new Color(.31f, .025f, .028f, .96f), .025f, .08f, .975f, .92f);
        AddFrame(inner.rectTransform, new Color(.46f, .18f, .14f, .95f), 2f);

        Text text = MakeOutlinedText(inner.transform, label, fontSize, TextAnchor.MiddleCenter, true);
        text.color = new Color(.96f, .92f, .84f, 1f);
        Anchor(text.rectTransform, .03f, .03f, .97f, .97f);

        Button button = go.GetComponent<Button>();
        button.targetGraphic = outer;
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, .92f, .90f, 1f);
        colors.pressedColor = new Color(.72f, .58f, .58f, 1f);
        colors.selectedColor = Color.white;
        colors.fadeDuration = .06f;
        button.colors = colors;
        return button;
    }

    Button MakeNavButton(Transform parent, string icon, string label)
    {
        GameObject go = new GameObject(label + "NavButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);

        Image bg = go.GetComponent<Image>();
        bg.color = new Color(.055f, .052f, .052f, .94f);
        AddFrame(bg.rectTransform, new Color(.47f, .40f, .32f, .95f), 2f);

        Text iconText = MakeOutlinedText(go.transform, icon, 45, TextAnchor.MiddleCenter, true);
        iconText.color = new Color(.78f, .76f, .71f, 1f);
        Anchor(iconText.rectTransform, .05f, .39f, .95f, .91f);

        Text labelText = MakeOutlinedText(go.transform, label, 23, TextAnchor.MiddleCenter, false);
        labelText.color = new Color(.91f, .88f, .81f, 1f);
        Anchor(labelText.rectTransform, .03f, .06f, .97f, .40f);

        Button button = go.GetComponent<Button>();
        button.targetGraphic = bg;
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, .93f, .85f, 1f);
        colors.pressedColor = new Color(.68f, .60f, .56f, 1f);
        colors.selectedColor = Color.white;
        colors.fadeDuration = .06f;
        button.colors = colors;
        return button;
    }

    void BeginBattle()
    {
        if (mainOverlay != null) mainOverlay.SetActive(false);
        ResetFight();
    }

    void ReturnToMain()
    {
        ResetFight();
        SetStageOrFallback(0);
        if (mainFloorText != null) mainFloorText.text = TestFloor.ToString();
        if (mainStatusText != null) mainStatusText.text = "그녀가 기다리고 있다...";
        if (mainOverlay != null)
        {
            mainOverlay.SetActive(true);
            mainOverlay.transform.SetAsLastSibling();
        }
    }

    IEnumerator PreloadCurrentImages()
    {
        string[] prefixes = {"p","k","b","d"};
        int c = 0;
        foreach (string prefix in prefixes)
        {
            for (int i = 0; i < 10; i++)
            {
                assets.GetSprite("assets/f" + TestFloor + "_" + prefix + i.ToString("00") + ".jpg");
                if (++c % 5 == 0) yield return null;
            }
        }
        assets.GetSprite("assets/f" + TestFloor + "_cap.jpg");
    }

    void ResetFight()
    {
        enemyHp = EnemyMaxHp;
        hitCount = 0;
        won = false;
        busy = false;
        weakActive = false;
        weakPoint.gameObject.SetActive(false);
        HideBubble();
        SetStageOrFallback(0);
    }

    void ProcessGesture(Vector2 start, Vector2 end, float duration)
    {
        if (won)
        {
            return;
        }

        Vector2 delta = end - start;
        float swipeThreshold = Mathf.Max(85f, Screen.width * .085f);
        bool swipe = delta.magnitude >= swipeThreshold && duration <= .65f;
        Vector2 impact = swipe ? Vector2.Lerp(start, end, .55f) : end;

        int zone = ZoneOf(impact);
        if (zone == 6)
        {
            ShowBubble(RandomLine(zoneTalk[zone]), 1.0f);
            VibrateTouch(false);
            StartCoroutine(TouchPulse(impact, false, false));
            return;
        }

        StartCoroutine(ResolveAttack(impact, zone, swipe, delta));
    }

    IEnumerator ResolveAttack(Vector2 impact, int zone, bool swipe, Vector2 swipeDelta)
    {
        busy = true;

        bool weakHit = weakActive && Vector2.Distance(
            new Vector2(weakNorm.x * Screen.width, weakNorm.y * Screen.height), impact
        ) <= Mathf.Max(58f, Screen.width * .06f);

        bool dodged = !weakHit && UnityEngine.Random.value < .17f;

        if (dodged)
        {
            HideWeakPoint();
            SetActionSprite("d");
            ShowBubble(RandomLine(dodgeTalk), 1.0f);
            Play("res/raw/dodge.wav");
            yield return TouchPulse(impact, false, swipe);
            yield return CharacterRecoil(impact, false, true);
            yield return new WaitForSecondsRealtime(.10f);
            SetStageOrFallback(Stage());
            busy = false;
            yield break;
        }

        string prefix = PrefixFor(zone, swipe, swipeDelta);
        SetActionSprite(prefix);

        int damage = weakHit ? 18 : (swipe ? 8 : 5);
        enemyHp = Mathf.Max(0, enemyHp - damage);
        hitCount++;

        if (weakHit)
        {
            weakActive = false;
            ShowBubble(RandomLine(criticalTalk), 1.25f);
            VibrateTouch(true);
            Play("assets/hit.wav");
            yield return WeakPointHitBurst();
            HideWeakPoint();
            yield return TouchPulse(impact, true, swipe);
            yield return CharacterRecoil(impact, true, false);
        }
        else
        {
            ShowBubble(RandomLine(zoneTalk[zone]), 1.05f);
            VibrateTouch(false);
            Play("assets/hit.wav");
            yield return TouchPulse(impact, false, swipe);
            yield return CharacterRecoil(impact, false, false);
        }

        if (enemyHp <= 0)
        {
            won = true;
            fightCount++;
            HideWeakPoint();
            var cap = assets.GetSprite("assets/f" + TestFloor + "_cap.jpg");
            if (cap != null) SetSprite(cap);
            ShowBubble("…끝났어.", 30f);
            Play("assets/win.wav");
            yield return new WaitForSecondsRealtime(.45f);
            if (gachaMachine != null) gachaMachine.PlayReward();
            busy = false;
            yield break;
        }

        SetStageOrFallback(Stage());

        if (!weakActive && hitCount >= 4)
        {
            hitCount = 0;
            StartWeakPoint();
        }

        busy = false;
    }

    int ZoneOf(Vector2 screen)
    {
        float nx = screen.x / Mathf.Max(1f, Screen.width);
        float nyTop = 1f - screen.y / Mathf.Max(1f, Screen.height);

        if (nx < .16f || nx > .84f) return 6;
        if (nyTop < .16f) return 0;
        if (nyTop < .30f) return 1;
        if (nyTop < .48f) return 2;
        if (nyTop < .59f && nx > .37f && nx < .63f) return 3;
        if (nyTop < .79f) return 4;
        return 5;
    }

    string PrefixFor(int zone, bool swipe, Vector2 delta)
    {
        if (swipe)
        {
            if (Mathf.Abs(delta.y) > Mathf.Abs(delta.x)) return "b";
            return delta.y > 0 ? "p" : "k";
        }

        if (zone <= 1) return "p";
        if (zone <= 3) return "b";
        return "k";
    }

    int Stage()
    {
        float q = enemyHp / (float)EnemyMaxHp;
        if (q <= 0f) return 4;
        if (q <= .25f) return 3;
        if (q <= .50f) return 2;
        if (q <= .75f) return 1;
        return 0;
    }

    void SetStageOrFallback(int stage)
    {
        // Floor battles must only use that floor's character art.
        // The original generic assets/s0..s4 are unrelated characters and caused
        // different women to appear between hits on floor 1.
        Sprite s = TryFloorStageSprite(TestFloor, stage);
        if (s == null)
            s = assets.GetSprite("assets/f" + TestFloor + "_p00.jpg");
        if (s != null) SetSprite(s);
    }

    Sprite TryFloorStageSprite(int floor, int stage)
    {
        // Use a stable pose sequence from the current floor only.
        // stage 0..4 -> p00, p02, p04, p06, p08.
        int index = Mathf.Clamp(stage, 0, 4) * 2;
        return assets.GetSprite("assets/f" + floor + "_p" + index.ToString("00") + ".jpg");
    }

    void SetActionSprite(string prefix)
    {
        int i = UnityEngine.Random.Range(0, 10);
        Sprite s = assets.GetSprite("assets/f" + TestFloor + "_" + prefix + i.ToString("00") + ".jpg");
        if (s != null) SetSprite(s);
    }

    void SetSprite(Sprite s)
    {
        battleImage.sprite = s;
        if (s.rect.height > 0)
            battleFitter.aspectRatio = s.rect.width / s.rect.height;
    }

    void StartWeakPoint()
    {
        weakActive = true;
        weakUntil = Time.unscaledTime + 1.05f;
        weakNorm = new Vector2(UnityEngine.Random.Range(.34f, .66f), UnityEngine.Random.Range(.34f, .68f));
        weakVelocity = UnityEngine.Random.insideUnitCircle.normalized * .34f;
        weakPoint.gameObject.SetActive(true);
        PositionWeakPoint();
    }

    void UpdateWeakPoint()
    {
        if (!weakActive) return;

        if (Time.unscaledTime >= weakUntil)
        {
            HideWeakPoint();
            return;
        }

        weakNorm += weakVelocity * Time.unscaledDeltaTime;

        if (weakNorm.x < .25f || weakNorm.x > .75f)
        {
            weakVelocity.x *= -1f;
            weakNorm.x = Mathf.Clamp(weakNorm.x, .25f, .75f);
        }
        if (weakNorm.y < .27f || weakNorm.y > .73f)
        {
            weakVelocity.y *= -1f;
            weakNorm.y = Mathf.Clamp(weakNorm.y, .27f, .73f);
        }

        // Obvious breathing target: roughly 55% -> 130% size, not a subtle wobble.
        float phase = (Mathf.Sin(Time.unscaledTime * 8.5f) + 1f) * .5f;
        float pulse = Mathf.Lerp(.55f, 1.30f, phase);
        weakPoint.rectTransform.localScale = Vector3.one * pulse;
        PositionWeakPoint();
    }

    void PositionWeakPoint()
    {
        Vector2 screen = new Vector2(weakNorm.x * Screen.width, weakNorm.y * Screen.height);
        Vector2 local;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screen, null, out local);
        weakPoint.rectTransform.anchoredPosition = local;
    }

    void HideWeakPoint()
    {
        weakActive = false;
        if (weakPoint != null) weakPoint.gameObject.SetActive(false);
    }

    IEnumerator TouchPulse(Vector2 screenPos, bool heavy, bool swipe)
    {
        var go = new GameObject("TouchFx", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var img = go.GetComponent<Image>();
        go.transform.SetParent(root, false);
        img.sprite = ringSprite;
        img.raycastTarget = false;
        img.color = heavy ? new Color(1f, .65f, .12f, .98f) : new Color(1f, .93f, .78f, .9f);

        RectTransform r = img.rectTransform;
        SetSize(r, heavy ? 125 : 92, heavy ? 125 : 92);

        Vector2 local;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screenPos, null, out local);
        r.anchoredPosition = local;

        float total = heavy ? .26f : .20f;
        float t = 0;
        while (t < total)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / total);
            float scale = Mathf.Lerp(.55f, heavy ? 2.4f : 1.85f, p);
            r.localScale = new Vector3(scale, swipe ? .62f * scale : scale, 1f);
            Color c = img.color;
            c.a = 1f - p;
            img.color = c;
            yield return null;
        }

        Destroy(go);
    }

    IEnumerator CharacterRecoil(Vector2 impact, bool heavy, bool dodge)
    {
        RectTransform r = battleImage.rectTransform;
        Vector2 basePos = r.anchoredPosition;
        Vector3 baseScale = r.localScale;

        Vector2 center = new Vector2(Screen.width * .5f, Screen.height * .5f);
        Vector2 dir = (center - impact).normalized;
        float power = dodge ? 24f : (heavy ? 34f : 17f);

        float total = dodge ? .17f : .13f;
        float t = 0;
        while (t < total)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / total);
            float wave = Mathf.Sin(p * Mathf.PI);
            r.anchoredPosition = basePos + dir * power * wave;
            float s = 1f + (heavy ? .022f : .010f) * wave;
            r.localScale = baseScale * s;
            yield return null;
        }

        r.anchoredPosition = basePos;
        r.localScale = baseScale;
    }

    IEnumerator WeakPointHitBurst()
    {
        if (weakPoint == null || !weakPoint.gameObject.activeSelf) yield break;

        RectTransform r = weakPoint.rectTransform;
        Vector3 start = r.localScale;
        Color startColor = weakPoint.color;
        float total = .10f;
        float t = 0f;

        while (t < total)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / total);
            r.localScale = Vector3.Lerp(start * 1.12f, Vector3.one * .18f, p);
            weakPoint.color = Color.Lerp(new Color(1f, .82f, .16f, 1f), Color.white, p);
            yield return null;
        }

        weakPoint.color = startColor;
    }

    void VibrateTouch(bool strong)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            Handheld.Vibrate();
        }
        catch { }
#endif
    }

    void ShowBubble(string text, float seconds)
    {
        StopCoroutine("HideBubbleLater");
        bubbleText.text = text;
        bubbleGroup.alpha = 1;
        if (seconds < 20f) StartCoroutine(HideBubbleLater(seconds));
    }

    IEnumerator HideBubbleLater(float seconds)
    {
        yield return new WaitForSecondsRealtime(seconds);
        HideBubble();
    }

    void HideBubble()
    {
        if (bubbleGroup != null) bubbleGroup.alpha = 0;
    }

    void Play(string entry)
    {
        if (assets == null || audioSource == null) return;
        AudioClip clip = assets.GetWav(entry);
        if (clip != null) audioSource.PlayOneShot(clip);
    }

    string RandomLine(string[] lines)
    {
        return lines[UnityEngine.Random.Range(0, lines.Length)];
    }

    Font CreateKoreanFont()
    {
        try
        {
            string[] installed = Font.GetOSInstalledFontNames();
            if (installed != null && installed.Length > 0)
            {
                var preferred = new List<string>();
                string[] keys = {"Noto","Samsung","Malgun","Nanum","Droid","Arial"};
                for (int k = 0; k < keys.Length; k++)
                    for (int i = 0; i < installed.Length; i++)
                        if (installed[i].IndexOf(keys[k], StringComparison.OrdinalIgnoreCase) >= 0 && !preferred.Contains(installed[i]))
                            preferred.Add(installed[i]);

                for (int i = 0; i < installed.Length; i++)
                    if (!preferred.Contains(installed[i])) preferred.Add(installed[i]);

                Font f = Font.CreateDynamicFontFromOSFont(preferred.ToArray(), 64);
                if (f != null) return f;
            }
        }
        catch { }

        return Resources.GetBuiltinResource<Font>("Arial.ttf");
    }

    Text MakeText(Transform parent, string value, int size, TextAnchor anchor, bool bold)
    {
        var go = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        go.transform.SetParent(parent, false);
        Text t = go.GetComponent<Text>();
        t.text = value;
        t.font = koreanFont;
        t.fontSize = size;
        t.resizeTextForBestFit = false;
        t.alignment = anchor;
        t.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Truncate;
        t.raycastTarget = false;
        return t;
    }

    Sprite CreateSpeechBubbleSprite(int width, int height)
    {
        Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        Color clear = new Color(1f, 1f, 1f, 0f);
        Color solid = Color.white;
        Color[] pixels = new Color[width * height];

        float left = 5f;
        float right = width - 5f;
        float bottom = 22f;
        float top = height - 5f;
        float radius = 24f;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                bool inside = false;

                if (x >= left + radius && x <= right - radius && y >= bottom && y <= top) inside = true;
                else if (x >= left && x <= right && y >= bottom + radius && y <= top - radius) inside = true;
                else
                {
                    Vector2[] centers =
                    {
                        new Vector2(left + radius, bottom + radius),
                        new Vector2(right - radius, bottom + radius),
                        new Vector2(left + radius, top - radius),
                        new Vector2(right - radius, top - radius)
                    };
                    for (int i = 0; i < centers.Length; i++)
                        if (Vector2.Distance(new Vector2(x, y), centers[i]) <= radius) { inside = true; break; }
                }

                // Small comic-style tail at the lower-left.
                if (!inside && y >= 3f && y < bottom + 3f)
                {
                    float yy = (y - 3f) / Mathf.Max(1f, bottom);
                    float minX = Mathf.Lerp(40f, 66f, yy);
                    float maxX = Mathf.Lerp(40f, 98f, yy);
                    if (x >= minX && x <= maxX) inside = true;
                }

                pixels[y * width + x] = inside ? solid : clear;
            }
        }

        tex.SetPixels(pixels);
        tex.Apply(false, false);
        return Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(.5f, .5f), 100f);
    }

    Sprite CreateRingSprite(int size, int thickness)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        Color clear = new Color(1, 1, 1, 0);
        Color solid = Color.white;
        Vector2 c = new Vector2((size - 1) * .5f, (size - 1) * .5f);
        float outer = size * .47f;
        float inner = outer - thickness;

        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), c);
                pixels[y * size + x] = d <= outer && d >= inner ? solid : clear;
            }
        }
        tex.SetPixels(pixels);
        tex.Apply(false, false);

        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(.5f, .5f), 100f);
    }

    static void SetSize(RectTransform r, float w, float h)
    {
        r.anchorMin = r.anchorMax = new Vector2(.5f, .5f);
        r.sizeDelta = new Vector2(w, h);
    }

    static void Anchor(RectTransform r, float x1, float y1, float x2, float y2)
    {
        r.anchorMin = new Vector2(x1, y1);
        r.anchorMax = new Vector2(x2, y2);
        r.offsetMin = Vector2.zero;
        r.offsetMax = Vector2.zero;
    }
}
