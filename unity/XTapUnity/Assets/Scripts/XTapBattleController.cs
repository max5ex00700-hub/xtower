using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public sealed class XTapBattleController : MonoBehaviour
{
    const int BaseEnemyHp = 60;
    const int StagesPerFloor = 10;
    const string CurrentStepKey = "xtap_current_progress_step";
    const string MaxUnlockedStepKey = "xtap_max_unlocked_progress_step";

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
    XTapInventory inventory;
    XTapBlacksmith blacksmith;
    XTapJail jail;
    Coroutine bubbleAnimRoutine;

    GameObject mainOverlay;
    Text mainFloorText;
    Text mainStatusText;

    Font koreanFont;
    Sprite ringSprite;
    Sprite speechBubbleSprite;
    Material jellyMaterial;
    Coroutine jellyRoutine;

    static readonly int JellyTouchUvId = Shader.PropertyToID("_TouchUV");
    static readonly int JellyStrengthId = Shader.PropertyToID("_TouchStrength");
    static readonly int JellyRadiusId = Shader.PropertyToID("_TouchRadius");
    static readonly int JellyDirectionId = Shader.PropertyToID("_TouchDirection");
    static readonly int JellySwipeId = Shader.PropertyToID("_SwipeStrength");
    static readonly int JellyAspectId = Shader.PropertyToID("_Aspect");

    int currentStep;
    int maxUnlockedStep;
    int enemyMaxHp = BaseEnemyHp;
    int enemyHp = BaseEnemyHp;
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

    readonly Dictionary<string, AudioClip> voiceClips = new Dictionary<string, AudioClip>(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string, AudioClip> combatSfxClips = new Dictionary<string, AudioClip>(StringComparer.OrdinalIgnoreCase);
    // Adult female combat reactions. Keep most hits breathy/soft, with screams rare.
    readonly string[] normalHitVoices =
    {
        "female_moan1", "female_moan2", "female_moan3", "female_moan4",
        "female_exhale1", "female_breath1",
        "female_sigh1", "female_sigh2",
        "female_ooh1", "female_gasp1",
        "female_grunt1"
    };

    readonly string[] swipeHitVoices =
    {
        "female_moan2", "female_moan3", "female_moan4", "female_moan4",
        "female_ooh1", "female_exhale1",
        "female_breath1", "female_gasp2",
        "female_grunt2"
    };

    // Criticals use stronger moans/agony, but true screams stay uncommon.
    readonly string[] criticalHitVoices =
    {
        "female_moan3", "female_moan3",
        "female_moan4", "female_moan4",
        "female_ooh1",
        "female_agony1", "female_agony2",
        "female_agony2",
        "female_scream1"
    };

    readonly string[] lowHpVoices =
    {
        "female_whimper1", "female_whimper1",
        "female_breath1", "female_sigh1", "female_sigh2",
        "female_moan4", "female_exhale1"
    };

    IEnumerator Start()
    {
        Application.targetFrameRate = 60;
        Screen.orientation = ScreenOrientation.Portrait;
        Screen.fullScreen = true;

        LoadProgress();

        koreanFont = CreateKoreanFont();
        ringSprite = CreateRingSprite(128, 9);
        speechBubbleSprite = CreateSpeechBubbleSprite(320, 120);

        BuildBattleOnlyUi();
        BuildMainUi();

        inventory = gameObject.AddComponent<XTapInventory>();
        inventory.Initialize(root, koreanFont, RefreshMainProgressUi);

        gachaMachine = gameObject.AddComponent<XTapGachaMachine>();
        gachaMachine.Initialize(root, koreanFont, ReturnToMain, inventory);

        blacksmith = gameObject.AddComponent<XTapBlacksmith>();
        blacksmith.Initialize(root, koreanFont, inventory, RefreshMainProgressUi);

        jail = gameObject.AddComponent<XTapJail>();
        jail.Initialize(root, koreanFont, inventory, RefreshMainProgressUi);

        var assetGo = new GameObject("OriginalApkAssets");
        assets = assetGo.AddComponent<XTapOriginalApkAssets>();
        yield return assets.Load();

        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
        audioSource.volume = 1f;
        PreloadCombatVoices();
        PreloadCombatSfx();

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
        SetupJellyMaterial();

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
        // Put combat dialogue beside the face instead of using a giant HUD box
        // stuck to the upper-left corner. The lower-left tail points toward the character.
        bubbleRect.anchorMin = bubbleRect.anchorMax = new Vector2(1f, 1f);
        bubbleRect.pivot = new Vector2(1f, 1f);
        bubbleRect.sizeDelta = new Vector2(330f, 116f);
        bubbleRect.anchoredPosition = new Vector2(-38f, -250f);

        bubbleGroup = bubblePanel.GetComponent<CanvasGroup>();
        bubbleGroup.alpha = 0;

        bubbleText = MakeText(bubblePanel.transform, "", 30, TextAnchor.MiddleCenter, true);
        bubbleText.color = new Color(.10f, .065f, .07f, 1f);
        bubbleText.resizeTextForBestFit = true;
        bubbleText.resizeTextMinSize = 22;
        bubbleText.resizeTextMaxSize = 30;
        Anchor(bubbleText.rectTransform, .08f, .22f, .92f, .91f);
    }


    void BuildMainUi()
    {
        mainOverlay = new GameObject("MainScreen", typeof(RectTransform));
        mainOverlay.transform.SetParent(root, false);
        RectTransform mainRoot = mainOverlay.GetComponent<RectTransform>();
        Anchor(mainRoot, 0, 0, 1, 1);

        // The combat art remains the live background so the main screen can later
        // follow each floor/character instead of becoming one flattened screenshot.
        MakePanel(mainOverlay.transform, "TopShade", new Color(0f, 0f, 0f, .20f), 0f, .72f, 1f, 1f);
        MakePanel(mainOverlay.transform, "BottomShade", new Color(.015f, .012f, .014f, .42f), 0f, 0f, 1f, .29f);

        // X탑 logo: large blood-red X with pale stone-white 탑.
        Text logoX = MakeOutlinedText(mainOverlay.transform, "X", 132, TextAnchor.MiddleCenter, true);
        logoX.color = new Color(.62f, .015f, .02f, 1f);
        Anchor(logoX.rectTransform, .020f, .842f, .165f, .995f);

        Text logoTower = MakeOutlinedText(mainOverlay.transform, "탑", 116, TextAnchor.MiddleCenter, true);
        logoTower.color = new Color(.93f, .91f, .87f, 1f);
        Anchor(logoTower.rectTransform, .135f, .848f, .315f, .992f);

        string displayVersion = Application.version;
        int dash = displayVersion.IndexOf('-');
        if (dash > 0) displayVersion = displayVersion.Substring(0, dash);
        Text version = MakeOutlinedText(mainOverlay.transform, "v " + displayVersion, 29, TextAnchor.MiddleCenter, false);
        version.color = new Color(.82f, .78f, .74f, 1f);
        Anchor(version.rectTransform, .170f, .808f, .360f, .848f);
        version.resizeTextForBestFit = true;
        version.resizeTextMinSize = 15;
        version.resizeTextMaxSize = 29;

        Image codePlate = MakePanel(mainOverlay.transform, "BuildCode", new Color(.035f, .03f, .03f, .84f), .770f, .932f, .985f, .985f);
        AddFrame(codePlate.rectTransform, new Color(.48f, .43f, .36f, .85f), 2.5f);
        Text codeText = MakeOutlinedText(codePlate.transform, "코드 1051", 27, TextAnchor.MiddleCenter, false);
        codeText.resizeTextForBestFit = true;
        codeText.resizeTextMinSize = 16;
        codeText.resizeTextMaxSize = 27;
        codeText.color = new Color(.90f, .87f, .82f, 1f);
        Anchor(codeText.rectTransform, .05f, .04f, .95f, .96f);

        // Floor information sits on the left, matching the supplied gothic concept.
        Text floorWord = MakeOutlinedText(mainOverlay.transform, "FLOOR", 48, TextAnchor.MiddleLeft, true);
        floorWord.color = new Color(.92f, .91f, .88f, 1f);
        Anchor(floorWord.rectTransform, .045f, .748f, .255f, .805f);

        mainFloorText = MakeOutlinedText(mainOverlay.transform, StageLabel(), 84, TextAnchor.MiddleLeft, true);
        mainFloorText.color = new Color(.73f, .015f, .02f, 1f);
        Anchor(mainFloorText.rectTransform, .235f, .735f, .510f, .820f);
        mainFloorText.resizeTextForBestFit = true;
        mainFloorText.resizeTextMinSize = 36;
        mainFloorText.resizeTextMaxSize = 84;

        mainStatusText = MakeOutlinedText(mainOverlay.transform, "그녀가 기다리고 있다...", 30, TextAnchor.MiddleLeft, false);
        mainStatusText.color = new Color(.94f, .91f, .86f, 1f);
        Anchor(mainStatusText.rectTransform, .045f, .700f, .580f, .748f);
        mainStatusText.resizeTextForBestFit = true;
        mainStatusText.resizeTextMinSize = 18;
        mainStatusText.resizeTextMaxSize = 30;

        // Main-screen speech bubble. This is separate from the combat reaction bubble.
        Image mainBubble = new GameObject("MainSpeechBubble", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
        mainBubble.transform.SetParent(mainOverlay.transform, false);
        mainBubble.sprite = speechBubbleSprite;
        mainBubble.color = new Color(.96f, .95f, .92f, .97f);
        mainBubble.raycastTarget = false;
        Anchor(mainBubble.rectTransform, .690f, .790f, .980f, .885f);
        Text mainBubbleText = MakeText(mainBubble.transform, "...또 오는 거야?", 27, TextAnchor.MiddleCenter, false);
        mainBubbleText.color = new Color(.08f, .065f, .06f, 1f);
        Anchor(mainBubbleText.rectTransform, .06f, .18f, .95f, .92f);
        mainBubbleText.resizeTextForBestFit = true;
        mainBubbleText.resizeTextMinSize = 17;
        mainBubbleText.resizeTextMaxSize = 27;

        // Large central action button.
        Button fight = MakeGothicButton(mainOverlay.transform, "그녀를 베다", 52);
        RectTransform fr = fight.GetComponent<RectTransform>();
        fr.anchorMin = new Vector2(.205f, .190f);
        fr.anchorMax = new Vector2(.795f, .305f);
        fr.offsetMin = fr.offsetMax = Vector2.zero;
        fight.onClick.AddListener(BeginBattle);

        // Bottom navigation bar. Only systems already present are interactive.
        Image navRail = MakePanel(mainOverlay.transform, "BottomRail", new Color(.012f, .011f, .012f, .985f), 0f, 0f, 1f, .165f);
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
            {
                b.onClick.AddListener(ReturnToMain);
            }
            else if (i == 1)
            {
                b.onClick.AddListener(delegate { MoveProgress(-1); });
            }
            else if (i == 2)
            {
                b.onClick.AddListener(delegate { MoveProgress(1); });
            }
            else if (i == 3)
            {
                b.onClick.AddListener(OpenInventory);
            }
            else if (i == 4)
            {
                b.onClick.AddListener(OpenBlacksmith);
            }
            else if (i == 5)
            {
                b.onClick.AddListener(OpenJail);
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
        outer.sprite = CreateBloodButtonSprite(512, 160);
        outer.type = Image.Type.Sliced;
        outer.color = Color.white;
        AddFrame(outer.rectTransform, new Color(.76f, .08f, .055f, 1f), 5f);

        Image inner = MakePanel(go.transform, "Inset", new Color(.16f, .012f, .016f, .18f), .025f, .07f, .975f, .93f);
        AddFrame(inner.rectTransform, new Color(.58f, .12f, .09f, .92f), 2f);

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

    void OpenInventory()
    {
        if (inventory != null) inventory.Open();
    }

    void OpenBlacksmith()
    {
        if (blacksmith != null) blacksmith.Open();
    }

    void OpenJail()
    {
        if (jail != null) jail.Open();
    }

    void ReturnToMain()
    {
        ResetFight();
        SetStageOrFallback(0);
        RefreshMainProgressUi();

        if (mainOverlay != null)
        {
            mainOverlay.SetActive(true);
            mainOverlay.transform.SetAsLastSibling();
        }
    }

    void MoveProgress(int delta)
    {
        int target = currentStep + delta;

        if (target < 0)
        {
            if (mainStatusText != null) mainStatusText.text = "여기가 시작점입니다.";
            return;
        }

        if (target > maxUnlockedStep)
        {
            if (mainStatusText != null)
                mainStatusText.text = StageLabel() + "을 먼저 클리어해야 올라갈 수 있습니다.";
            return;
        }

        currentStep = target;
        SaveProgress();
        ResetFight();
        SetStageOrFallback(0);
        RefreshMainProgressUi();
    }

    void RefreshMainProgressUi()
    {
        if (mainFloorText != null)
            mainFloorText.text = StageLabel();

        if (mainStatusText == null) return;

        int growthPercent = Mathf.RoundToInt(ProgressMultiplier() * 100f);
        string clearText = currentStep < maxUnlockedStep ? "클리어 · ↑ 이동 가능" : "도전 중";
        string gearText = "";

        if (inventory != null)
        {
            gearText = " · 장비 공+" + inventory.EquippedAttack +
                       " 방+" + inventory.EquippedDefense +
                       " 체+" + inventory.EquippedHp;
        }

        mainStatusText.text = clearText + " · 성장 " + growthPercent + "%" + gearText;
    }

    void LoadProgress()
    {
        currentStep = Mathf.Max(0, PlayerPrefs.GetInt(CurrentStepKey, 0));
        maxUnlockedStep = Mathf.Max(0, PlayerPrefs.GetInt(MaxUnlockedStepKey, 0));

        if (currentStep > maxUnlockedStep)
            currentStep = maxUnlockedStep;
    }

    void SaveProgress()
    {
        PlayerPrefs.SetInt(CurrentStepKey, currentStep);
        PlayerPrefs.SetInt(MaxUnlockedStepKey, maxUnlockedStep);
        PlayerPrefs.Save();
    }

    int TowerFloor()
    {
        return currentStep / StagesPerFloor + 1;
    }

    int SubStage()
    {
        return currentStep % StagesPerFloor + 1;
    }

    string StageLabel()
    {
        return TowerFloor() + "-" + SubStage();
    }

    float ProgressMultiplier()
    {
        // 1-1=100%, 1-2=105% ... 1-10=145%, 2-1=150%.
        return 1f + currentStep * .05f;
    }

    int CurrentEnemyMaxHp()
    {
        return Mathf.Max(1, Mathf.RoundToInt(BaseEnemyHp * ProgressMultiplier()));
    }

    int CurrentCharacterId()
    {
        return ((TowerFloor() - 1) % 10) + 1;
    }

    int CurrentVisualFloor()
    {
        // Floor art is currently guaranteed for 1-3; higher floors cycle it.
        return ((TowerFloor() - 1) % 3) + 1;
    }

    IEnumerator PreloadCurrentImages()
    {
        string[] prefixes = {"p","k","b","d"};
        int c = 0;
        foreach (string prefix in prefixes)
        {
            for (int i = 0; i < 10; i++)
            {
                assets.GetSprite("assets/f" + CurrentVisualFloor() + "_" + prefix + i.ToString("00") + ".jpg");
                if (++c % 5 == 0) yield return null;
            }
        }
        assets.GetSprite("assets/f" + CurrentVisualFloor() + "_cap.jpg");
    }

    void ResetFight()
    {
        enemyMaxHp = CurrentEnemyMaxHp();
        enemyHp = enemyMaxHp;
        hitCount = 0;
        won = false;
        busy = false;
        weakActive = false;
        weakPoint.gameObject.SetActive(false);
        ResetJelly();
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
            // Dodge should read as fast body movement, not a UI/game "boing".
            // Reuse the clean light whoosh already bundled for combat movement.
            PlayCombatSfx("fight_swing_light", .62f);
            if (UnityEngine.Random.value < .45f) PlayVoice("female_gasp1", .68f);
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

        StartJellyImpact(impact, weakHit, swipe, swipeDelta);

        if (weakHit)
        {
            weakActive = false;
            ShowBubble(RandomLine(criticalTalk), 1.25f);
            VibrateTouch(true);
            PlayCombatImpact(prefix, swipe, true);
            PlayRandomVoice(criticalHitVoices, .84f);
            yield return WeakPointHitBurst();
            HideWeakPoint();
            yield return TouchPulse(impact, true, swipe);
            yield return CharacterRecoil(impact, true, false);
        }
        else
        {
            ShowBubble(RandomLine(zoneTalk[zone]), 1.05f);
            VibrateTouch(false);
            PlayCombatImpact(prefix, swipe, false);
            if (enemyHp <= Mathf.RoundToInt(enemyMaxHp * .25f) && UnityEngine.Random.value < .45f)
                PlayRandomVoice(lowHpVoices, .72f);
            else
                PlayRandomVoice(swipe ? swipeHitVoices : normalHitVoices, swipe ? .78f : .66f);
            yield return TouchPulse(impact, false, swipe);
            yield return CharacterRecoil(impact, false, false);
        }

        if (enemyHp <= 0)
        {
            won = true;
            fightCount++;
            HideWeakPoint();
            var cap = assets.GetSprite("assets/f" + CurrentVisualFloor() + "_cap.jpg");
            if (cap != null) SetSprite(cap);

            if (currentStep >= maxUnlockedStep)
            {
                maxUnlockedStep = currentStep + 1;
                SaveProgress();
            }

            ShowBubble("…끝났어.", 30f);
            Play("assets/win.wav");
            yield return new WaitForSecondsRealtime(.45f);

            if (gachaMachine != null)
                gachaMachine.PlayReward(CurrentCharacterId());

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
        float q = enemyHp / (float)Mathf.Max(1, enemyMaxHp);
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
        int visualFloor = CurrentVisualFloor();
        Sprite s = TryFloorStageSprite(visualFloor, stage);
        if (s == null)
            s = assets.GetSprite("assets/f" + visualFloor + "_p00.jpg");
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
        Sprite s = assets.GetSprite("assets/f" + CurrentVisualFloor() + "_" + prefix + i.ToString("00") + ".jpg");
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

    void SetupJellyMaterial()
    {
        Shader shader = Resources.Load<Shader>("XTapJellyTouch");
        if (shader == null)
        {
            Debug.LogWarning("X탑 탱글 터치 셰이더를 찾지 못했습니다.");
            return;
        }

        jellyMaterial = new Material(shader);
        jellyMaterial.name = "XTapJellyTouchRuntime";
        jellyMaterial.hideFlags = HideFlags.DontSave;
        jellyMaterial.SetFloat(JellyStrengthId, 0f);
        jellyMaterial.SetFloat(JellySwipeId, 0f);
        jellyMaterial.SetFloat(JellyRadiusId, .18f);
        jellyMaterial.SetVector(JellyTouchUvId, new Vector4(.5f, .5f, 0f, 0f));
        jellyMaterial.SetVector(JellyDirectionId, Vector4.zero);
        battleImage.material = jellyMaterial;
    }

    void StartJellyImpact(Vector2 screenPos, bool heavy, bool swipe, Vector2 swipeDelta)
    {
        if (jellyMaterial == null || battleImage == null) return;

        if (jellyRoutine != null)
            StopCoroutine(jellyRoutine);

        jellyRoutine = StartCoroutine(JellyImpactRoutine(screenPos, heavy, swipe, swipeDelta));
    }

    IEnumerator JellyImpactRoutine(Vector2 screenPos, bool heavy, bool swipe, Vector2 swipeDelta)
    {
        RectTransform r = battleImage.rectTransform;
        Vector2 local;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(r, screenPos, null, out local))
            yield break;

        Rect rect = r.rect;
        if (Mathf.Abs(rect.width) < .001f || Mathf.Abs(rect.height) < .001f)
            yield break;

        float u = Mathf.InverseLerp(rect.xMin, rect.xMax, local.x);
        float v = Mathf.InverseLerp(rect.yMin, rect.yMax, local.y);
        Vector2 uv = new Vector2(Mathf.Clamp01(u), Mathf.Clamp01(v));

        Vector2 dir = Vector2.zero;
        if (swipe && swipeDelta.sqrMagnitude > 1f)
        {
            dir = new Vector2(
                swipeDelta.x / Mathf.Max(1f, Mathf.Abs(rect.width)),
                swipeDelta.y / Mathf.Max(1f, Mathf.Abs(rect.height))
            ).normalized;
        }

        float radius = heavy ? .24f : (swipe ? .21f : .17f);
        float amplitude = heavy ? .30f : (swipe ? .24f : .20f);
        float total = heavy ? .42f : (swipe ? .34f : .28f);
        float swipeAmplitude = swipe ? (heavy ? .042f : .030f) : 0f;

        jellyMaterial.SetVector(JellyTouchUvId, new Vector4(uv.x, uv.y, 0f, 0f));
        jellyMaterial.SetFloat(JellyRadiusId, radius);
        jellyMaterial.SetVector(JellyDirectionId, new Vector4(dir.x, dir.y, 0f, 0f));
        jellyMaterial.SetFloat(JellyAspectId, Mathf.Abs(rect.width / rect.height));

        float t = 0f;
        while (t < total)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / total);

            // Starts compressed, overshoots outward, then settles through
            // two to three smaller rebounds: the local "탱글탱글" feel.
            float envelope = Mathf.Exp(-3.35f * p);
            float spring = Mathf.Cos(p * Mathf.PI * 5.6f) * envelope;
            float dragSpring = Mathf.Cos(p * Mathf.PI * 4.4f) * envelope;

            jellyMaterial.SetFloat(JellyStrengthId, amplitude * spring);
            jellyMaterial.SetFloat(JellySwipeId, swipeAmplitude * dragSpring);
            yield return null;
        }

        ClearJellyMaterial();
        jellyRoutine = null;
    }

    void ResetJelly()
    {
        if (jellyRoutine != null)
        {
            StopCoroutine(jellyRoutine);
            jellyRoutine = null;
        }

        ClearJellyMaterial();
    }

    void ClearJellyMaterial()
    {
        if (jellyMaterial == null) return;
        jellyMaterial.SetFloat(JellyStrengthId, 0f);
        jellyMaterial.SetFloat(JellySwipeId, 0f);
        jellyMaterial.SetVector(JellyDirectionId, Vector4.zero);
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

        if (bubbleAnimRoutine != null)
        {
            StopCoroutine(bubbleAnimRoutine);
            bubbleAnimRoutine = null;
        }

        LayoutBattleBubble(text);
        bubbleGroup.alpha = 1f;
        bubbleAnimRoutine = StartCoroutine(AnimateBubbleIn(text));

        if (seconds < 20f)
            StartCoroutine(HideBubbleLater(seconds));
    }

    void LayoutBattleBubble(string text)
    {
        if (bubblePanel == null) return;

        int count = string.IsNullOrEmpty(text) ? 1 : text.Length;
        float width = Mathf.Clamp(185f + count * 18f, 250f, 420f);
        float height = count > 16 ? 138f : (count > 10 ? 124f : 110f);

        RectTransform r = bubblePanel.rectTransform;
        r.sizeDelta = new Vector2(width, height);
        r.anchoredPosition = new Vector2(-38f, -250f);
    }

    IEnumerator AnimateBubbleIn(string text)
    {
        RectTransform r = bubblePanel.rectTransform;
        r.localScale = Vector3.one * .86f;
        bubbleText.text = "";

        float t = 0f;
        const float popTime = .11f;
        while (t < popTime)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / popTime);
            float s = p < .72f
                ? Mathf.Lerp(.86f, 1.045f, p / .72f)
                : Mathf.Lerp(1.045f, 1f, (p - .72f) / .28f);
            r.localScale = Vector3.one * s;
            yield return null;
        }

        r.localScale = Vector3.one;

        if (!string.IsNullOrEmpty(text))
        {
            for (int i = 1; i <= text.Length; i++)
            {
                bubbleText.text = text.Substring(0, i);
                yield return new WaitForSecondsRealtime(.014f);
            }
        }

        bubbleText.text = text;
        bubbleAnimRoutine = null;
    }

    IEnumerator HideBubbleLater(float seconds)
    {
        yield return new WaitForSecondsRealtime(seconds);
        HideBubble();
    }

    void HideBubble()
    {
        if (bubbleAnimRoutine != null)
        {
            StopCoroutine(bubbleAnimRoutine);
            bubbleAnimRoutine = null;
        }

        if (bubblePanel != null)
            bubblePanel.rectTransform.localScale = Vector3.one;

        if (bubbleGroup != null)
            bubbleGroup.alpha = 0f;
    }

    void Play(string entry)
    {
        if (assets == null || audioSource == null) return;
        AudioClip clip = assets.GetWav(entry);
        if (clip != null) audioSource.PlayOneShot(clip);
    }

    void PreloadCombatVoices()
    {
        string[] ids =
        {
            "female_grunt1", "female_grunt2",
            "female_gasp1", "female_gasp2",
            "female_agony1", "female_scream1",
            "female_whimper1",
            "female_moan1", "female_moan2",
            "female_moan3", "female_moan4",
            "female_exhale1", "female_breath1",
            "female_sigh1", "female_sigh2",
            "female_ooh1", "female_agony2"
        };

        for (int i = 0; i < ids.Length; i++)
            LoadVoice(ids[i]);
    }

    void PreloadCombatSfx()
    {
        string[] ids =
        {
            "fight_punch_light",
            "fight_punch_medium",
            "fight_smash_heavy",
            "fight_swing_light",
            "fight_swing_heavy"
        };

        for (int i = 0; i < ids.Length; i++)
            LoadCombatSfx(ids[i]);
    }

    void PlayCombatImpact(string prefix, bool swipe, bool critical)
    {
        if (critical)
        {
            PlayCombatSfx("fight_swing_heavy", .78f);
            PlayCombatSfx("fight_smash_heavy", 1f);
            return;
        }

        if (swipe)
        {
            PlayCombatSfx(prefix == "k" ? "fight_swing_heavy" : "fight_swing_light", .66f);
        }

        if (prefix == "k")
            PlayCombatSfx("fight_punch_medium", .95f);
        else if (prefix == "b")
            PlayCombatSfx("fight_punch_medium", .88f);
        else
            PlayCombatSfx(UnityEngine.Random.value < .55f ? "fight_punch_light" : "fight_punch_medium", .82f);
    }

    void PlayCombatSfx(string id, float volume)
    {
        if (audioSource == null) return;
        AudioClip clip = LoadCombatSfx(id);
        if (clip != null) audioSource.PlayOneShot(clip, Mathf.Clamp01(volume));
    }

    AudioClip LoadCombatSfx(string id)
    {
        AudioClip cached;
        if (combatSfxClips.TryGetValue(id, out cached)) return cached;

        try
        {
            TextAsset encoded = Resources.Load<TextAsset>("XTapCombatSfx/" + id);
            if (encoded == null || string.IsNullOrWhiteSpace(encoded.text)) return null;

            byte[] wav = Convert.FromBase64String(encoded.text.Trim());
            AudioClip clip = DecodePcm16Wav(wav, id);
            if (clip != null) combatSfxClips[id] = clip;
            return clip;
        }
        catch (Exception e)
        {
            Debug.LogWarning("X탑 격투 효과음 로드 실패: " + id + " / " + e.Message);
            return null;
        }
    }

    void PlayRandomVoice(string[] ids, float volume)
    {
        if (ids == null || ids.Length == 0) return;
        PlayVoice(ids[UnityEngine.Random.Range(0, ids.Length)], volume);
    }

    void PlayVoice(string id, float volume)
    {
        if (audioSource == null) return;
        AudioClip clip = LoadVoice(id);
        if (clip != null) audioSource.PlayOneShot(clip, Mathf.Clamp01(volume));
    }

    AudioClip LoadVoice(string id)
    {
        AudioClip cached;
        if (voiceClips.TryGetValue(id, out cached)) return cached;

        try
        {
            TextAsset encoded = Resources.Load<TextAsset>("XTapVoices/" + id);
            if (encoded == null || string.IsNullOrWhiteSpace(encoded.text)) return null;

            byte[] wav = Convert.FromBase64String(encoded.text.Trim());
            AudioClip clip = DecodePcm16Wav(wav, id);
            if (clip != null) voiceClips[id] = clip;
            return clip;
        }
        catch (Exception e)
        {
            Debug.LogWarning("X탑 전투 음성 로드 실패: " + id + " / " + e.Message);
            return null;
        }
    }

    AudioClip DecodePcm16Wav(byte[] wav, string clipName)
    {
        if (wav == null || wav.Length < 44) return null;
        if (wav[0] != 'R' || wav[1] != 'I' || wav[2] != 'F' || wav[3] != 'F') return null;

        int fmt = FindWavChunk(wav, "fmt ");
        int data = FindWavChunk(wav, "data");
        if (fmt < 0 || data < 0 || fmt + 24 > wav.Length || data + 8 > wav.Length) return null;

        int format = BitConverter.ToInt16(wav, fmt + 8);
        int channels = BitConverter.ToInt16(wav, fmt + 10);
        int sampleRate = BitConverter.ToInt32(wav, fmt + 12);
        int bits = BitConverter.ToInt16(wav, fmt + 22);

        if (format != 1 || channels < 1 || channels > 2 || sampleRate <= 0) return null;
        if (bits != 8 && bits != 16 && bits != 24 && bits != 32) return null;

        int declaredBytes = BitConverter.ToInt32(wav, data + 4);
        int start = data + 8;
        int byteCount = Mathf.Min(declaredBytes, wav.Length - start);
        int bytesPerSample = bits / 8;
        int sampleCount = byteCount / bytesPerSample;
        if (sampleCount <= 0) return null;

        float[] samples = new float[sampleCount];
        for (int i = 0; i < sampleCount; i++)
        {
            int p = start + i * bytesPerSample;

            if (bits == 8)
            {
                samples[i] = (wav[p] - 128f) / 128f;
            }
            else if (bits == 16)
            {
                samples[i] = BitConverter.ToInt16(wav, p) / 32768f;
            }
            else if (bits == 24)
            {
                int v = wav[p] | (wav[p + 1] << 8) | (wav[p + 2] << 16);
                if ((v & 0x800000) != 0) v |= unchecked((int)0xFF000000);
                samples[i] = v / 8388608f;
            }
            else
            {
                samples[i] = BitConverter.ToInt32(wav, p) / 2147483648f;
            }
        }

        int frames = sampleCount / channels;
        AudioClip clip = AudioClip.Create(clipName, frames, channels, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    int FindWavChunk(byte[] wav, string fourCC)
    {
        for (int i = 12; i + 8 <= wav.Length;)
        {
            if (wav[i] == fourCC[0] && wav[i + 1] == fourCC[1] &&
                wav[i + 2] == fourCC[2] && wav[i + 3] == fourCC[3])
                return i;

            int size = BitConverter.ToInt32(wav, i + 4);
            if (size < 0) return -1;
            i += 8 + size + (size & 1);
        }
        return -1;
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

    Sprite CreateBloodButtonSprite(int width, int height)
    {
        Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        Color[] pixels = new Color[width * height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float nx = x / Mathf.Max(1f, width - 1f);
                float ny = y / Mathf.Max(1f, height - 1f);

                float grain = Mathf.PerlinNoise(nx * 12.7f + 1.3f, ny * 7.9f + 3.1f);
                float vein = Mathf.Abs(Mathf.Sin(nx * 25f + grain * 4f) * Mathf.Sin(ny * 17f - grain * 3f));
                float edge = Mathf.Min(Mathf.Min(nx, 1f - nx), Mathf.Min(ny, 1f - ny));
                float vignette = Mathf.SmoothStep(0f, .18f, edge);

                float r = Mathf.Lerp(.10f, .28f, grain) + vein * .045f;
                float g = Mathf.Lerp(.005f, .025f, grain);
                float b = Mathf.Lerp(.008f, .018f, grain);

                r *= Mathf.Lerp(.72f, 1f, vignette);
                g *= Mathf.Lerp(.72f, 1f, vignette);
                b *= Mathf.Lerp(.72f, 1f, vignette);

                // Inner metallic-red rim.
                bool rim = x < 7 || x >= width - 7 || y < 7 || y >= height - 7;
                if (rim)
                {
                    r = .48f;
                    g = .035f;
                    b = .028f;
                }

                pixels[y * width + x] = new Color(r, g, b, 1f);
            }
        }

        tex.SetPixels(pixels);
        tex.Apply(false, false);
        return Sprite.Create(
            tex,
            new Rect(0, 0, width, height),
            new Vector2(.5f, .5f),
            100f,
            0,
            SpriteMeshType.FullRect,
            new Vector4(10f, 10f, 10f, 10f)
        );
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
