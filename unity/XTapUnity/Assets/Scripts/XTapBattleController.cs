using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public sealed class XTapBattleController : MonoBehaviour
{
    const float UiFontScale = 2.15f;
    const double BasePlayerHp = 100d;
    const double BasePlayerAttack = 5d;
    const double BasePlayerDefense = 1d;
    const double BaseEnemyHp = 100d;
    const double BaseEnemyAttack = 3d;
    const double BaseEnemyDefense = 1d;
    const float CharacterDodgeChance = .20f;
    const int StagesPerFloor = 10;
    const string CurrentStepKey = "xtap_current_progress_step";
    const string MaxUnlockedStepKey = "xtap_max_unlocked_progress_step";
    const string VibrationSettingKey = "xtap_option_vibration";
    const string SfxSettingKey = "xtap_option_sfx";
    const string BgmSettingKey = "xtap_option_bgm";

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
    XTapCodex codex;
    Coroutine bubbleAnimRoutine;

    GameObject mainOverlay;
    RectTransform mainInfoDrawer;
    RectTransform mainInfoTabRect;
    Text mainInfoTabText;
    Coroutine mainInfoDrawerRoutine;
    bool mainInfoDrawerOpen;
    Text mainFloorText;
    Text mainFloorSubText;
    Text mainStatusText;
    Text mainMoveText;
    Text mainAttackText;
    Text mainDefenseText;
    Text mainHpText;
    Image mainProgressToast;
    Text mainProgressToastFloorText;
    Text mainProgressToastStageText;
    CanvasGroup mainProgressToastGroup;
    Coroutine mainProgressToastRoutine;
    Image mainSpeechBubble;
    Text mainSpeechText;
    Coroutine mainSpeechRoutine;
    Coroutine mainTouchResponseRoutine;

    GameObject splashOverlay;
    Image splashFill;
    Text splashPromptText;

    GameObject optionsOverlay;
    Text vibrationOptionValue;
    Text sfxOptionValue;
    Text bgmOptionValue;
    bool vibrationEnabled = true;
    bool sfxEnabled = true;
    bool bgmEnabled = true;

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
    double enemyMaxHp = BaseEnemyHp;
    double enemyHp = BaseEnemyHp;
    double enemyAttack = BaseEnemyAttack;
    double enemyDefense = BaseEnemyDefense;
    double playerMaxHp = BasePlayerHp;
    double playerHp = BasePlayerHp;
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

    // Main-screen interaction dialogue, migrated from the original Android prototype.
    readonly string[] mainGeneralTalk = {"...또 왔네.","무슨 일이야?","왜 그렇게 보고 있어?","가만히 좀 있어."};
    readonly string[] mainHairTalk = {"머리… 만지지 마.","흩트리지 마.","손 치워. 전투 중이잖아."};
    readonly string[] mainFaceTalk = {"얼굴을 만지다니.","가까이 오지 마.","…시선이 거슬려."};
    readonly string[] mainChestTalk = {"거기 안 돼.","옷 밑으로 넣지 마.","하아… 정신 차려."};
    readonly string[] mainThighTalk = {"허벅지는 그만.","다리가 가렵다고 하지 마.","손 올려. 지금."};
    readonly string[] mainGroinTalk = {"거긴 절대 안 돼!","손 치워!!","어디서 손을…!!"};
    readonly string[] mainBootTalk = {"신발은 상관없어.","발끝은 봐 주지."};

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

        // X탑 전체 화면 기준: 세로 9:16 디자인 캔버스 + Android 풀스크린.
        Screen.orientation = ScreenOrientation.Portrait;
        Screen.autorotateToPortrait = false;
        Screen.autorotateToPortraitUpsideDown = false;
        Screen.autorotateToLandscapeLeft = false;
        Screen.autorotateToLandscapeRight = false;
        Screen.fullScreenMode = FullScreenMode.FullScreenWindow;
        Screen.fullScreen = true;

        LoadProgress();
        LoadOptionSettings();

        koreanFont = CreateKoreanFont();
        ringSprite = CreateRingSprite(128, 9);
        speechBubbleSprite = CreateSpeechBubbleSprite(320, 120);
        XTapMainSkin.EnsureLoaded();

        BuildBattleOnlyUi();
        BuildMainUi();

        // Canvas/root must exist before the startup splash is attached.
        BuildStartupSplash();
        SetStartupProgress(.06f);
        yield return null;
        SetStartupProgress(.16f);

        inventory = gameObject.AddComponent<XTapInventory>();
        inventory.Initialize(root, koreanFont, OnInventoryClosed);

        gachaMachine = gameObject.AddComponent<XTapGachaMachine>();
        gachaMachine.Initialize(root, koreanFont, ReturnToMain, inventory);

        blacksmith = gameObject.AddComponent<XTapBlacksmith>();
        blacksmith.Initialize(root, koreanFont, inventory, RefreshMainProgressUi);

        jail = gameObject.AddComponent<XTapJail>();
        jail.Initialize(root, koreanFont, inventory, RefreshMainProgressUi);
        SetStartupProgress(.28f);

        var assetGo = new GameObject("OriginalApkAssets");
        assets = assetGo.AddComponent<XTapOriginalApkAssets>();
        SetStartupProgress(.35f);
        yield return assets.Load();
        SetStartupProgress(.72f);

        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
        audioSource.volume = 1f;
        PreloadCombatVoices();
        PreloadCombatSfx();
        SetStartupProgress(.84f);

        if (!assets.Ready)
        {
            ShowBubble("전투 이미지 데이터를 불러오지 못했습니다.", 10f);
            yield break;
        }

        codex = gameObject.AddComponent<XTapCodex>();
        codex.Initialize(root, koreanFont, assets, inventory, RefreshMainProgressUi);

        SetStartupProgress(.90f);
        yield return PreloadCurrentImages();
        SetStartupProgress(1f);

        if (splashPromptText != null)
            splashPromptText.gameObject.SetActive(true);

        yield return WaitForStartupTap();
        CloseStartupSplash();
        ReturnToMain();
    }

    void BuildStartupSplash()
    {
        splashOverlay = new GameObject("XTapStartupSplash", typeof(RectTransform));
        splashOverlay.transform.SetParent(root, false);
        RectTransform sr = splashOverlay.GetComponent<RectTransform>();
        Anchor(sr, 0f, 0f, 1f, 1f);

        Image bg = new GameObject("SplashBackground", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
        bg.transform.SetParent(splashOverlay.transform, false);
        bg.color = Color.white;
        bg.raycastTarget = false;
        Anchor(bg.rectTransform, 0f, 0f, 1f, 1f);

        Texture2D tex = Resources.Load<Texture2D>("XTapSplash/xtower_splash");
        if (tex != null)
        {
            bg.sprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(.5f, .5f), 100f);
            bg.type = Image.Type.Simple;
            bg.preserveAspect = false;
        }
        else
        {
            bg.color = new Color(.025f, .008f, .010f, 1f);
            Text fallback = MakeOutlinedText(bg.transform, "X탑", 42, TextAnchor.MiddleCenter, true);
            fallback.color = new Color(1f, .74f, .48f, 1f);
            Anchor(fallback.rectTransform, .10f, .42f, .90f, .62f);
        }

        Image bottomShade = MakePanel(splashOverlay.transform, "SplashBottomShade", new Color(.012f, .006f, .008f, .78f), 0f, 0f, 1f, .205f);
        bottomShade.raycastTarget = false;

        Image track = MakePanel(splashOverlay.transform, "LoadingTrack", new Color(.035f, .025f, .028f, .98f), .075f, .105f, .925f, .132f);
        track.raycastTarget = false;
        AddFrame(track.rectTransform, new Color(.76f, .42f, .22f, 1f), 2f);

        splashFill = MakePanel(track.transform, "LoadingFill", new Color(.90f, .08f, .045f, .98f), .015f, .20f, .015f, .80f);
        splashFill.raycastTarget = false;

        splashPromptText = MakeOutlinedText(splashOverlay.transform, "준비 되면 화면을 터치 하세요", 18, TextAnchor.MiddleCenter, true);
        splashPromptText.color = new Color(1f, .88f, .72f, 1f);
        splashPromptText.resizeTextForBestFit = true;
        splashPromptText.resizeTextMinSize = 28;
        splashPromptText.resizeTextMaxSize = 44;
        Anchor(splashPromptText.rectTransform, .08f, .140f, .92f, .190f);
        splashPromptText.gameObject.SetActive(false);

        Text credit = MakeOutlinedText(splashOverlay.transform, "제작:포시즌Jo", 14, TextAnchor.MiddleCenter, true);
        credit.color = new Color(.90f, .82f, .70f, 1f);
        Anchor(credit.rectTransform, .08f, .035f, .92f, .082f);

        splashOverlay.transform.SetAsLastSibling();
    }

    void SetStartupProgress(float value)
    {
        if (splashFill == null) return;
        float p = Mathf.Clamp01(value);
        RectTransform r = splashFill.rectTransform;
        r.anchorMin = new Vector2(.015f, .20f);
        r.anchorMax = new Vector2(Mathf.Lerp(.015f, .985f, p), .80f);
        r.offsetMin = Vector2.zero;
        r.offsetMax = Vector2.zero;
    }

    IEnumerator WaitForStartupTap()
    {
        float pulse = 0f;

        while (true)
        {
            pulse += Time.unscaledDeltaTime;

            if (splashPromptText != null)
            {
                Color c = splashPromptText.color;
                c.a = Mathf.Lerp(.50f, 1f, (Mathf.Sin(pulse * 3.2f) + 1f) * .5f);
                splashPromptText.color = c;
            }

            bool pressed = false;
            if (Input.touchCount > 0)
            {
                Touch t = Input.GetTouch(0);
                pressed = t.phase == TouchPhase.Ended;
            }

#if UNITY_EDITOR || UNITY_STANDALONE
            if (Input.GetMouseButtonUp(0))
                pressed = true;
#endif

            if (pressed)
                yield break;

            yield return null;
        }
    }

    void CloseStartupSplash()
    {
        if (splashOverlay != null)
        {
            Destroy(splashOverlay);
            splashOverlay = null;
            splashFill = null;
            splashPromptText = null;
        }
    }

    void Update()
    {
        if (assets == null || !assets.Ready) return;

        UpdateWeakPoint();

        if (mainOverlay != null && mainOverlay.activeSelf)
        {
            HandleMainScreenInput();
            return;
        }
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
        // All gameplay/UI is authored against a portrait 9:16 reference.
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
        bubbleRect.sizeDelta = new Vector2(570f, 220f);
        bubbleRect.anchoredPosition = new Vector2(-38f, -235f);

        bubbleGroup = bubblePanel.GetComponent<CanvasGroup>();
        bubbleGroup.alpha = 0;

        bubbleText = MakeText(bubblePanel.transform, "", 16, TextAnchor.MiddleCenter, true);
        bubbleText.color = new Color(.10f, .065f, .07f, 1f);
        bubbleText.resizeTextForBestFit = true;
        bubbleText.resizeTextMinSize = 46;
        bubbleText.resizeTextMaxSize = 62;
        Anchor(bubbleText.rectTransform, .08f, .22f, .92f, .91f);

        // Battle HUD intentionally shows no HP/ATK/DEF values.
        // Enemy capabilities must be learned by fighting, not by reading a stat panel.
    }


    void BuildMainUi()
    {
        mainOverlay = new GameObject("MainScreen", typeof(RectTransform));
        mainOverlay.transform.SetParent(root, false);
        RectTransform mainRoot = mainOverlay.GetComponent<RectTransform>();
        Anchor(mainRoot, 0, 0, 1, 1);

        // Keep the character artwork dominant. Only the action button,
        // dialogue, options button and bottom navigation are always visible.
        MakePanel(mainOverlay.transform, "TopShade", new Color(0f, 0f, 0f, .10f), 0f, .72f, 1f, 1f);
        MakePanel(mainOverlay.transform, "BottomShade", new Color(.008f, .006f, .008f, .72f), 0f, 0f, 1f, .18f);

        GameObject optionButtonGo = new GameObject("OptionsButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        optionButtonGo.transform.SetParent(mainOverlay.transform, false);
        Image optionButtonImage = optionButtonGo.GetComponent<Image>();
        optionButtonImage.color = Color.white;
        ApplyMainButtonSkin(optionButtonImage, XTapMainSkin.UtilityButton);
        Anchor(optionButtonImage.rectTransform, .865f, .934f, .975f, .985f);
        Text optionButtonText = MakeOutlinedText(optionButtonGo.transform, "⚙", 22, TextAnchor.MiddleCenter, true);
        optionButtonText.color = new Color(.96f, .90f, .80f, 1f);
        Anchor(optionButtonText.rectTransform, .04f, .04f, .96f, .96f);
        Button optionButton = optionButtonGo.GetComponent<Button>();
        optionButton.targetGraphic = optionButtonImage;
        optionButton.onClick.AddListener(OpenOptions);

        Button codexButton = MakeUtilityButton(mainOverlay.transform, "▤", "도감");
        RectTransform codexButtonRect = codexButton.GetComponent<RectTransform>();
        codexButtonRect.anchorMin = new Vector2(.865f, .858f);
        codexButtonRect.anchorMax = new Vector2(.975f, .909f);
        codexButtonRect.offsetMin = codexButtonRect.offsetMax = Vector2.zero;
        codexButton.onClick.AddListener(OpenCodex);

        // Approved composition: no large outer box. FLOOR and player stats are
        // independent compact ornate panels so the character stays visible.
        GameObject infoRootGo = new GameObject("MainInfoDrawer", typeof(RectTransform));
        infoRootGo.transform.SetParent(mainOverlay.transform, false);
        mainInfoDrawer = infoRootGo.GetComponent<RectTransform>();
        Anchor(mainInfoDrawer, .018f, .655f, .300f, .965f);

        Image floorPanel = MakePanel(mainInfoDrawer, "FloorPanel", new Color(.018f, .014f, .014f, .96f), 0f, .565f, 1f, 1f);
        ApplyGothicPanel(floorPanel, XTapMainSkin.FloorPanel, Color.white);

        Text floorWord = MakeOutlinedText(floorPanel.transform, "FLOOR", 18, TextAnchor.MiddleCenter, true);
        floorWord.color = new Color(.94f, .87f, .73f, 1f);
        Anchor(floorWord.rectTransform, .08f, .72f, .92f, .98f);

        mainFloorText = MakeOutlinedText(floorPanel.transform, TowerFloor().ToString(), 38, TextAnchor.MiddleCenter, true);
        mainFloorText.color = new Color(1f, .72f, .22f, 1f);
        Anchor(mainFloorText.rectTransform, .08f, .28f, .92f, .72f);

        mainFloorSubText = MakeOutlinedText(floorPanel.transform, "", 16, TextAnchor.MiddleCenter, true);
        mainFloorSubText.color = new Color(.96f, .92f, .84f, 1f);
        Anchor(mainFloorSubText.rectTransform, .08f, .02f, .92f, .29f);

        Image statPanel = MakePanel(mainInfoDrawer, "PlayerStats", new Color(.018f, .014f, .014f, .96f), 0f, 0f, 1f, .535f);
        ApplyGothicPanel(statPanel, XTapMainSkin.PlayerPanel, Color.white);

        Text playerTitle = MakeOutlinedText(statPanel.transform, "플레이어", 20, TextAnchor.MiddleLeft, true);
        playerTitle.color = new Color(.98f, .92f, .80f, 1f);
        Anchor(playerTitle.rectTransform, .08f, .82f, .92f, .98f);

        mainMoveText = MakeOutlinedText(statPanel.transform, "", 14, TextAnchor.MiddleLeft, true);
        mainMoveText.color = new Color(.96f, .90f, .76f, 1f);
        Anchor(mainMoveText.rectTransform, .10f, .61f, .92f, .79f);

        mainAttackText = MakeOutlinedText(statPanel.transform, "", 14, TextAnchor.MiddleLeft, true);
        mainAttackText.color = new Color(1f, .70f, .22f, 1f);
        Anchor(mainAttackText.rectTransform, .10f, .40f, .94f, .59f);

        mainDefenseText = MakeOutlinedText(statPanel.transform, "", 14, TextAnchor.MiddleLeft, true);
        mainDefenseText.color = new Color(.48f, .76f, 1f, 1f);
        Anchor(mainDefenseText.rectTransform, .10f, .21f, .94f, .40f);

        mainHpText = MakeOutlinedText(statPanel.transform, "", 14, TextAnchor.MiddleLeft, true);
        mainHpText.color = new Color(1f, .46f, .46f, 1f);
        Anchor(mainHpText.rectTransform, .10f, .02f, .94f, .21f);

        // Thin edge tab is the only persistent hint that the drawer exists.
        GameObject tabGo = new GameObject("MainInfoTab", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        tabGo.transform.SetParent(mainOverlay.transform, false);
        Image tabBg = tabGo.GetComponent<Image>();
        tabBg.color = Color.white;
        ApplyMainButtonSkin(tabBg, XTapMainSkin.InfoTabButton);
        mainInfoTabRect = tabBg.rectTransform;
        Anchor(mainInfoTabRect, 0f, .565f, .052f, .645f);

        mainInfoTabText = MakeOutlinedText(tabGo.transform, "›", 26, TextAnchor.MiddleCenter, true);
        mainInfoTabText.color = new Color(1f, .82f, .42f, 1f);
        Anchor(mainInfoTabText.rectTransform, .05f, .05f, .95f, .95f);

        // Brief floor/stage indicator shown after a successful section move.
        mainProgressToast = new GameObject(
            "MainProgressToast",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(CanvasGroup)
        ).GetComponent<Image>();
        mainProgressToast.transform.SetParent(mainOverlay.transform, false);
        mainProgressToast.color = new Color(.018f, .014f, .014f, .96f);
        mainProgressToast.raycastTarget = false;
        ApplyGothicPanel(mainProgressToast, XTapMainSkin.FloorPanel, Color.white);
        Anchor(mainProgressToast.rectTransform, .030f, .815f, .265f, .930f);

        mainProgressToastGroup = mainProgressToast.GetComponent<CanvasGroup>();
        mainProgressToastGroup.alpha = 0f;
        mainProgressToastGroup.interactable = false;
        mainProgressToastGroup.blocksRaycasts = false;

        mainProgressToastFloorText = MakeOutlinedText(
            mainProgressToast.transform, "", 24, TextAnchor.MiddleCenter, true);
        mainProgressToastFloorText.color = new Color(1f, .72f, .22f, 1f);
        Anchor(mainProgressToastFloorText.rectTransform, .07f, .43f, .93f, .92f);

        mainProgressToastStageText = MakeOutlinedText(
            mainProgressToast.transform, "", 15, TextAnchor.MiddleCenter, true);
        mainProgressToastStageText.color = new Color(.96f, .92f, .84f, 1f);
        Anchor(mainProgressToastStageText.rectTransform, .07f, .08f, .93f, .45f);

        Button tabButton = tabGo.GetComponent<Button>();
        tabButton.targetGraphic = tabBg;
        tabButton.onClick.AddListener(ToggleMainInfoDrawer);
        tabGo.SetActive(true);

        // Speech bubble is invisible until the character is actually touched.
        mainSpeechBubble = new GameObject("MainSpeechBubble", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
        mainSpeechBubble.transform.SetParent(mainOverlay.transform, false);
        mainSpeechBubble.sprite = speechBubbleSprite;
        mainSpeechBubble.color = new Color(.98f, .97f, .93f, .98f);
        mainSpeechBubble.raycastTarget = false;
        Anchor(mainSpeechBubble.rectTransform, .535f, .760f, .965f, .890f);

        mainSpeechText = MakeText(mainSpeechBubble.transform, "", 17, TextAnchor.MiddleCenter, true);
        mainSpeechText.color = new Color(.08f, .055f, .05f, 1f);
        mainSpeechText.resizeTextForBestFit = true;
        mainSpeechText.resizeTextMinSize = 28;
        mainSpeechText.resizeTextMaxSize = 42;
        Anchor(mainSpeechText.rectTransform, .06f, .18f, .95f, .92f);
        mainSpeechBubble.gameObject.SetActive(false);

        // Large central action button.
        Button fight = MakeGothicButton(mainOverlay.transform, "전투", 24);
        RectTransform fr = fight.GetComponent<RectTransform>();
        fr.anchorMin = new Vector2(.270f, .135f);
        fr.anchorMax = new Vector2(.730f, .255f);
        fr.offsetMin = fr.offsetMax = Vector2.zero;
        fight.onClick.AddListener(BeginBattle);

        // Bottom navigation bar.
        Image navRail = MakePanel(mainOverlay.transform, "BottomRail", new Color(.010f, .008f, .010f, .985f), 0f, 0f, 1f, .125f);
        ApplyGothicPanel(navRail, XTapMainSkin.BottomRail, new Color(.025f, .018f, .018f, 1f));

        string[] icons = {"↓", "▣", "▥", "⚒", "↑"};
        string[] labels = {"이전 구간", "가방", "감옥", "대장간", "다음 구간"};

        for (int i = 0; i < labels.Length; i++)
        {
            Button b = MakeNavButton(navRail.transform, icons[i], labels[i]);
            RectTransform br = b.GetComponent<RectTransform>();
            float x1 = .015f + i * .197f;
            float x2 = x1 + .165f;
            br.anchorMin = new Vector2(x1, .08f);
            br.anchorMax = new Vector2(x2, .92f);
            br.offsetMin = br.offsetMax = Vector2.zero;

            if (i == 0)
                b.onClick.AddListener(delegate { MoveProgress(-1); });
            else if (i == 1)
                b.onClick.AddListener(OpenInventory);
            else if (i == 2)
                b.onClick.AddListener(OpenJail);
            else if (i == 3)
                b.onClick.AddListener(OpenBlacksmith);
            else if (i == 4)
                b.onClick.AddListener(delegate { MoveProgress(1); });
        }

        BuildOptionsUi();

        SetMainInfoDrawerOpen(false, true);
        mainOverlay.SetActive(false);
    }

    void BuildOptionsUi()
    {
        optionsOverlay = new GameObject("OptionsOverlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        optionsOverlay.transform.SetParent(mainOverlay.transform, false);
        Image dim = optionsOverlay.GetComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, .82f);
        dim.raycastTarget = true;
        Anchor(dim.rectTransform, 0f, 0f, 1f, 1f);

        Image panel = MakePanel(optionsOverlay.transform, "OptionsPanel", new Color(.020f, .017f, .018f, .985f), .12f, .24f, .88f, .79f);
        panel.raycastTarget = true;
        ApplyGothicPanel(panel, XTapMainSkin.PlayerPanel, Color.white);

        Text title = MakeOutlinedText(panel.transform, "옵션", 25, TextAnchor.MiddleCenter, true);
        title.color = new Color(1f, .88f, .62f, 1f);
        Anchor(title.rectTransform, .08f, .82f, .92f, .96f);

        Button vibrationButton = MakeOptionToggle(panel.transform, "진동", out vibrationOptionValue);
        Anchor(vibrationButton.GetComponent<RectTransform>(), .08f, .62f, .92f, .78f);
        vibrationButton.onClick.AddListener(ToggleVibrationSetting);

        Button sfxButton = MakeOptionToggle(panel.transform, "효과음", out sfxOptionValue);
        Anchor(sfxButton.GetComponent<RectTransform>(), .08f, .43f, .92f, .59f);
        sfxButton.onClick.AddListener(ToggleSfxSetting);

        Button bgmButton = MakeOptionToggle(panel.transform, "배경음악", out bgmOptionValue);
        Anchor(bgmButton.GetComponent<RectTransform>(), .08f, .24f, .92f, .40f);
        bgmButton.onClick.AddListener(ToggleBgmSetting);

        Text version = MakeOutlinedText(panel.transform, "버전 정보   11.09  (1109)", 14, TextAnchor.MiddleCenter, true);
        version.color = new Color(.72f, .69f, .64f, 1f);
        Anchor(version.rectTransform, .08f, .12f, .92f, .22f);

        GameObject closeGo = new GameObject("OptionsClose", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        closeGo.transform.SetParent(panel.transform, false);
        Image closeImage = closeGo.GetComponent<Image>();
        closeImage.color = new Color(.10f, .075f, .065f, .98f);
        AddFrame(closeImage.rectTransform, new Color(.60f, .42f, .22f, 1f), 2f);
        Anchor(closeImage.rectTransform, .30f, .025f, .70f, .11f);
        Text closeText = MakeOutlinedText(closeGo.transform, "닫기", 14, TextAnchor.MiddleCenter, true);
        closeText.color = new Color(.98f, .92f, .82f, 1f);
        Anchor(closeText.rectTransform, .04f, .04f, .96f, .96f);
        Button closeButton = closeGo.GetComponent<Button>();
        closeButton.targetGraphic = closeImage;
        closeButton.onClick.AddListener(CloseOptions);

        RefreshOptionLabels();
        optionsOverlay.SetActive(false);
    }

    Button MakeOptionToggle(Transform parent, string label, out Text valueText)
    {
        GameObject go = new GameObject(label + "Option", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);

        Image bg = go.GetComponent<Image>();
        bg.color = new Color(.040f, .034f, .034f, .98f);
        AddFrame(bg.rectTransform, new Color(.45f, .34f, .23f, .95f), 2f);

        Text labelText = MakeOutlinedText(go.transform, label, 16, TextAnchor.MiddleLeft, true);
        labelText.color = new Color(.96f, .90f, .80f, 1f);
        Anchor(labelText.rectTransform, .07f, .08f, .62f, .92f);

        valueText = MakeOutlinedText(go.transform, "", 15, TextAnchor.MiddleRight, true);
        Anchor(valueText.rectTransform, .60f, .08f, .92f, .92f);

        Button button = go.GetComponent<Button>();
        button.targetGraphic = bg;
        return button;
    }

    void OpenOptions()
    {
        if (optionsOverlay == null) return;
        RefreshOptionLabels();
        optionsOverlay.SetActive(true);
        optionsOverlay.transform.SetAsLastSibling();
    }

    void CloseOptions()
    {
        if (optionsOverlay != null) optionsOverlay.SetActive(false);
    }

    void LoadOptionSettings()
    {
        vibrationEnabled = PlayerPrefs.GetInt(VibrationSettingKey, 1) == 1;
        sfxEnabled = PlayerPrefs.GetInt(SfxSettingKey, 1) == 1;
        bgmEnabled = PlayerPrefs.GetInt(BgmSettingKey, 1) == 1;
    }

    void SaveOptionSettings()
    {
        PlayerPrefs.SetInt(VibrationSettingKey, vibrationEnabled ? 1 : 0);
        PlayerPrefs.SetInt(SfxSettingKey, sfxEnabled ? 1 : 0);
        PlayerPrefs.SetInt(BgmSettingKey, bgmEnabled ? 1 : 0);
        PlayerPrefs.Save();
    }

    void ToggleVibrationSetting()
    {
        vibrationEnabled = !vibrationEnabled;
        SaveOptionSettings();
        RefreshOptionLabels();
    }

    void ToggleSfxSetting()
    {
        sfxEnabled = !sfxEnabled;
        SaveOptionSettings();
        RefreshOptionLabels();
    }

    void ToggleBgmSetting()
    {
        bgmEnabled = !bgmEnabled;
        SaveOptionSettings();
        RefreshOptionLabels();
    }

    void RefreshOptionLabels()
    {
        SetOptionValue(vibrationOptionValue, vibrationEnabled);
        SetOptionValue(sfxOptionValue, sfxEnabled);
        SetOptionValue(bgmOptionValue, bgmEnabled);
    }

    void SetOptionValue(Text text, bool enabled)
    {
        if (text == null) return;
        text.text = enabled ? "ON" : "OFF";
        text.color = enabled
            ? new Color(.65f, 1f, .66f, 1f)
            : new Color(1f, .48f, .44f, 1f);
    }

    void ToggleMainInfoDrawer()
    {
        SetMainInfoDrawerOpen(!mainInfoDrawerOpen, false);
    }

    void SetMainInfoDrawerOpen(bool open, bool instant)
    {
        mainInfoDrawerOpen = open;

        if (mainInfoTabText != null)
            mainInfoTabText.text = open ? "‹" : "›";

        if (mainInfoTabRect != null)
        {
            if (open)
                Anchor(mainInfoTabRect, .300f, .565f, .352f, .645f);
            else
                Anchor(mainInfoTabRect, 0f, .565f, .052f, .645f);
        }

        if (mainInfoDrawer == null)
            return;

        float width = mainInfoDrawer.rect.width;
        if (width <= 1f)
            width = 480f;

        float targetX = open ? 0f : -width;

        if (mainInfoDrawerRoutine != null)
        {
            StopCoroutine(mainInfoDrawerRoutine);
            mainInfoDrawerRoutine = null;
        }

        if (instant)
        {
            Vector2 p = mainInfoDrawer.anchoredPosition;
            p.x = targetX;
            mainInfoDrawer.anchoredPosition = p;
            return;
        }

        mainInfoDrawerRoutine = StartCoroutine(AnimateMainInfoDrawer(targetX));
    }

    IEnumerator AnimateMainInfoDrawer(float targetX)
    {
        if (mainInfoDrawer == null)
            yield break;

        float startX = mainInfoDrawer.anchoredPosition.x;
        const float duration = .22f;
        float t = 0f;

        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / duration);
            p = p * p * (3f - 2f * p);

            Vector2 pos = mainInfoDrawer.anchoredPosition;
            pos.x = Mathf.Lerp(startX, targetX, p);
            mainInfoDrawer.anchoredPosition = pos;
            yield return null;
        }

        Vector2 finalPos = mainInfoDrawer.anchoredPosition;
        finalPos.x = targetX;
        mainInfoDrawer.anchoredPosition = finalPos;
        mainInfoDrawerRoutine = null;
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

    void ApplyGothicPanel(Image image, Sprite sprite, Color tint)
    {
        if (image == null) return;

        if (sprite != null)
        {
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            image.color = tint;
        }
        else
        {
            AddFrame(image.rectTransform, new Color(.62f, .43f, .22f, 1f), 3f);
        }
    }

    void ApplyMainButtonSkin(Image image, Sprite sprite)
    {
        if (image == null) return;

        if (sprite != null)
        {
            image.sprite = sprite;
            image.type = Image.Type.Simple;
            image.preserveAspect = true;
            image.color = Color.white;
        }
        else
        {
            image.color = new Color(.045f, .035f, .035f, 1f);
            AddFrame(image.rectTransform, new Color(.68f, .44f, .20f, 1f), 3f);
        }
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
        outer.color = Color.white;
        ApplyMainButtonSkin(outer, XTapMainSkin.FightButton);

        // Reference button uses the crossed-sword crest as the upper visual,
        // so keep only the large Korean action title in the lower plaque.
        Text text = MakeOutlinedText(go.transform, label, fontSize + 10, TextAnchor.MiddleCenter, true);
        text.color = new Color(1f, .97f, .90f, 1f);
        Anchor(text.rectTransform, .08f, .16f, .92f, .60f);

        Button button = go.GetComponent<Button>();
        button.targetGraphic = outer;
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.08f, 1.02f, .98f, 1f);
        colors.pressedColor = new Color(.72f, .58f, .52f, 1f);
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
        bg.color = Color.white;
        ApplyMainButtonSkin(bg, XTapMainSkin.NavButton);

        Text iconText = MakeOutlinedText(go.transform, icon, 24, TextAnchor.MiddleCenter, true);
        iconText.color = new Color(1f, .78f, .36f, 1f);
        Anchor(iconText.rectTransform, .12f, .49f, .88f, .83f);

        Text labelText = MakeOutlinedText(go.transform, label, 13, TextAnchor.MiddleCenter, true);
        labelText.color = new Color(.98f, .93f, .84f, 1f);
        Anchor(labelText.rectTransform, .08f, .16f, .92f, .46f);

        Button button = go.GetComponent<Button>();
        button.targetGraphic = bg;
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.10f, 1.04f, .96f, 1f);
        colors.pressedColor = new Color(.68f, .58f, .50f, 1f);
        colors.selectedColor = Color.white;
        colors.fadeDuration = .06f;
        button.colors = colors;
        return button;
    }

    Button MakeUtilityButton(Transform parent, string icon, string label)
    {
        GameObject go = new GameObject(label + "UtilityButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);

        Image bg = go.GetComponent<Image>();
        bg.color = Color.white;
        ApplyMainButtonSkin(bg, XTapMainSkin.UtilityButton);

        Text iconText = MakeOutlinedText(go.transform, icon, 16, TextAnchor.MiddleCenter, true);
        iconText.color = new Color(1f, .78f, .34f, 1f);
        Anchor(iconText.rectTransform, .10f, .40f, .90f, .84f);

        Text labelText = MakeOutlinedText(go.transform, label, 9, TextAnchor.MiddleCenter, true);
        labelText.color = new Color(.98f, .93f, .84f, 1f);
        Anchor(labelText.rectTransform, .08f, .10f, .92f, .41f);

        Button button = go.GetComponent<Button>();
        button.targetGraphic = bg;
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.06f, 1.06f, 1.06f, 1f);
        colors.pressedColor = new Color(.76f, .76f, .76f, 1f);
        colors.selectedColor = Color.white;
        colors.fadeDuration = .06f;
        button.colors = colors;
        return button;
    }

    void HandleMainScreenInput()
    {
        if (optionsOverlay != null && optionsOverlay.activeSelf) return;
        if (Input.touchCount <= 0) return;

        Touch touch = Input.GetTouch(0);
        if (touch.phase != TouchPhase.Ended) return;

        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(touch.fingerId))
            return;

        HandleMainCharacterTouch(touch.position);
    }

    void HandleMainCharacterTouch(Vector2 screen)
    {
        string zone = MainTouchZone(screen);
        string[] pool = mainGeneralTalk;

        if (zone == "hair") pool = mainHairTalk;
        else if (zone == "face") pool = mainFaceTalk;
        else if (zone == "chest") pool = mainChestTalk;
        else if (zone == "groin") pool = mainGroinTalk;
        else if (zone == "thigh") pool = mainThighTalk;
        else if (zone == "boot") pool = mainBootTalk;

        string line = pool[UnityEngine.Random.Range(0, pool.Length)];
        ShowMainSpeech(line);

        StartCoroutine(TouchPulse(screen, zone == "groin", false));
        VibrateTouch(zone == "groin");

        if (mainTouchResponseRoutine != null)
            StopCoroutine(mainTouchResponseRoutine);
        mainTouchResponseRoutine = StartCoroutine(MainTouchResponse());
    }

    string MainTouchZone(Vector2 screen)
    {
        float nx = screen.x / Mathf.Max(1f, Screen.width);
        float ny = 1f - screen.y / Mathf.Max(1f, Screen.height);

        // Original prototype touch regions. Outside the central character area
        // is treated as ordinary conversation rather than a body-part reaction.
        if (nx < .18f || nx > .82f) return "general";
        if (ny < .16f) return "hair";
        if (ny < .30f) return "face";
        if (ny < .48f) return "chest";
        if (ny < .58f && nx > .38f && nx < .62f) return "groin";
        if (ny < .78f) return "thigh";
        return "boot";
    }

    void ShowMainSpeech(string line)
    {
        if (mainSpeechBubble == null || mainSpeechText == null) return;

        if (mainSpeechRoutine != null)
        {
            StopCoroutine(mainSpeechRoutine);
            mainSpeechRoutine = null;
        }

        mainSpeechText.text = line;
        mainSpeechBubble.gameObject.SetActive(true);
        mainSpeechBubble.transform.SetAsLastSibling();
        mainSpeechRoutine = StartCoroutine(HideMainSpeechLater(1.6f));
    }

    IEnumerator HideMainSpeechLater(float seconds)
    {
        yield return new WaitForSecondsRealtime(seconds);
        if (mainSpeechBubble != null)
            mainSpeechBubble.gameObject.SetActive(false);
        mainSpeechRoutine = null;
    }

    IEnumerator MainTouchResponse()
    {
        if (battleImage == null) yield break;

        RectTransform r = battleImage.rectTransform;
        Vector3 baseScale = r.localScale;
        float total = .16f;
        float t = 0f;

        while (t < total)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / total);
            float wave = Mathf.Sin(p * Mathf.PI);
            r.localScale = baseScale * (1f + .008f * wave);
            yield return null;
        }

        r.localScale = baseScale;
        mainTouchResponseRoutine = null;
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

    void OnInventoryClosed()
    {
        RefreshMainProgressUi();
        if (jail != null) jail.RefreshIfOpen();
    }

    void OpenBlacksmith()
    {
        if (blacksmith != null) blacksmith.Open();
    }

    void OpenJail()
    {
        if (jail != null) jail.Open();
    }

    void OpenCodex()
    {
        if (codex != null) codex.Open();
    }

    void ReturnToMain()
    {
        ResetFight();
        SetStageOrFallback(0);
        RefreshMainProgressUi();
        SetMainInfoDrawerOpen(false, true);
        if (mainSpeechBubble != null) mainSpeechBubble.gameObject.SetActive(false);

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
        ShowMainProgressToast();
    }

    void ShowMainProgressToast()
    {
        if (mainProgressToast == null ||
            mainProgressToastGroup == null ||
            mainProgressToastFloorText == null ||
            mainProgressToastStageText == null)
            return;

        mainProgressToastFloorText.text = TowerFloor() + "층";
        mainProgressToastStageText.text = StageLabel();

        if (mainProgressToastRoutine != null)
            StopCoroutine(mainProgressToastRoutine);

        mainProgressToast.transform.SetAsLastSibling();
        mainProgressToastRoutine = StartCoroutine(AnimateMainProgressToast());
    }

    IEnumerator AnimateMainProgressToast()
    {
        const float fadeIn = .10f;
        const float hold = .85f;
        const float fadeOut = .24f;

        float t = 0f;
        mainProgressToastGroup.alpha = 0f;

        while (t < fadeIn)
        {
            t += Time.unscaledDeltaTime;
            mainProgressToastGroup.alpha = Mathf.Clamp01(t / fadeIn);
            yield return null;
        }

        mainProgressToastGroup.alpha = 1f;
        yield return new WaitForSecondsRealtime(hold);

        t = 0f;
        while (t < fadeOut)
        {
            t += Time.unscaledDeltaTime;
            mainProgressToastGroup.alpha = 1f - Mathf.Clamp01(t / fadeOut);
            yield return null;
        }

        mainProgressToastGroup.alpha = 0f;
        mainProgressToastRoutine = null;
    }

    void RefreshMainProgressUi()
    {
        if (mainFloorText != null)
            mainFloorText.text = TowerFloor() + "층";

        if (mainFloorSubText != null)
            mainFloorSubText.text = StageLabel();

        bool canMoveUp = currentStep < maxUnlockedStep;

        if (mainMoveText != null)
            mainMoveText.text = "이동    " + (canMoveUp ? "가능" : "진행 중");

        double atk = CurrentPlayerAttack();
        double def = CurrentPlayerDefense();
        double hp = CurrentPlayerMaxHp();

        if (mainAttackText != null)
            mainAttackText.text = "공격력  " + XTapStatFormat.Compact(atk);

        if (mainDefenseText != null)
            mainDefenseText.text = "방어력  " + XTapStatFormat.Compact(def);

        if (mainHpText != null)
            mainHpText.text = "체력    " + XTapStatFormat.Compact(hp);
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

    double ProgressMultiplier()
    {
        // Existing balance:
        // 1-1=100%, 1-2=110% ... 1-9=180%, 1-10=200%.
        // Every new floor starts at 3x the previous floor's 1-10 value,
        // so floor start multipliers are 1, 6, 36, 216 ...
        double floorStart = Math.Pow(6d, Math.Max(0, TowerFloor() - 1));
        if (double.IsInfinity(floorStart) || floorStart >= double.MaxValue)
            return double.MaxValue;

        int sub = SubStage();
        double subMultiplier = sub >= 10 ? 2d : 1d + (sub - 1) * .10d;
        return SafeMultiply(floorStart, subMultiplier);
    }

    double CurrentEnemyMaxHp()
    {
        return Math.Max(1d, SafeMultiply(BaseEnemyHp, ProgressMultiplier()));
    }

    double CurrentEnemyAttack()
    {
        return Math.Max(1d, SafeMultiply(BaseEnemyAttack, ProgressMultiplier()));
    }

    double CurrentEnemyDefense()
    {
        return Math.Max(0d, SafeMultiply(BaseEnemyDefense, ProgressMultiplier()));
    }

    double CurrentPlayerAttack()
    {
        double total = XTapStatFormat.SafeAdd(BasePlayerAttack, inventory != null ? inventory.EquippedAttack : 0d);
        return SafeMultiply(total, inventory != null ? inventory.DescriptorSetMultiplier : 1d);
    }

    double CurrentPlayerDefense()
    {
        double total = XTapStatFormat.SafeAdd(BasePlayerDefense, inventory != null ? inventory.EquippedDefense : 0d);
        return SafeMultiply(total, inventory != null ? inventory.DescriptorSetMultiplier : 1d);
    }

    double CurrentPlayerMaxHp()
    {
        double total = XTapStatFormat.SafeAdd(BasePlayerHp, inventory != null ? inventory.EquippedHp : 0d);
        return SafeMultiply(total, inventory != null ? inventory.DescriptorSetMultiplier : 1d);
    }

    double SafeMultiply(double a, double b)
    {
        if (double.IsNaN(a) || double.IsNaN(b)) return 0d;
        if (a == 0d || b == 0d) return 0d;

        double result = a * b;
        if (double.IsPositiveInfinity(result)) return double.MaxValue;
        if (double.IsNegativeInfinity(result)) return -double.MaxValue;
        return result;
    }

    int CurrentCharacterId()
    {
        return ((TowerFloor() - 1) % 10) + 1;
    }

    int CurrentVisualFloor()
    {
        // Each tower floor uses its own character art. Characters repeat only
        // after the 10-character cycle, never every 3 floors.
        return CurrentCharacterId();
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
        enemyAttack = CurrentEnemyAttack();
        enemyDefense = CurrentEnemyDefense();

        playerMaxHp = CurrentPlayerMaxHp();
        playerHp = playerMaxHp;

        hitCount = 0;
        won = false;
        busy = false;
        weakActive = false;
        weakPoint.gameObject.SetActive(false);
        ResetJelly();
        HideBubble();
        SetStageOrFallback(0);
        RefreshBattleStatUi();
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

        bool dodged = !weakHit && UnityEngine.Random.value < CharacterDodgeChance;

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

            if (ApplyEnemyCounterAttack())
            {
                busy = false;
                yield break;
            }

            busy = false;
            yield break;
        }

        string prefix = PrefixFor(zone, swipe, swipeDelta);
        SetActionSprite(prefix);

        double attackMultiplier = weakHit ? 3.6d : (swipe ? 1.6d : 1d);
        double rawAttack = SafeMultiply(CurrentPlayerAttack(), attackMultiplier);
        double damage = Math.Max(1d, Math.Floor(rawAttack - enemyDefense));
        enemyHp = Math.Max(0d, enemyHp - damage);
        hitCount++;
        RefreshBattleStatUi();

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
            if (enemyHp <= enemyMaxHp * .25d && UnityEngine.Random.value < .45f)
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

            int clearedCharacterId = CurrentCharacterId();

            // _cap is reserved for successful capture reveal only.
            // A normal battle victory must never display the capture illustration.

            // Winning advances the player's actual current progress.
            int clearedStep = currentStep;
            int nextStep = clearedStep + 1;

            if (nextStep > maxUnlockedStep)
                maxUnlockedStep = nextStep;

            currentStep = nextStep;
            SaveProgress();
            RefreshMainProgressUi();

            ShowBubble("…끝났어.", 30f);
            Play("assets/win.wav");
            yield return new WaitForSecondsRealtime(.45f);

            if (gachaMachine != null)
                gachaMachine.PlayReward(clearedCharacterId, clearedStep);

            busy = false;
            yield break;
        }

        if (ApplyEnemyCounterAttack())
        {
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

    bool ApplyEnemyCounterAttack()
    {
        double damage = Math.Max(1d, Math.Floor(enemyAttack - CurrentPlayerDefense()));
        playerHp = Math.Max(0d, playerHp - damage);
        RefreshBattleStatUi();

        if (playerHp > 0d)
            return false;

        HandlePlayerDeath();
        return true;
    }

    void HandlePlayerDeath()
    {
        HideWeakPoint();

        int lost = inventory != null ? inventory.LoseHeldAndGroundOnDeath() : 0;

        currentStep = Mathf.Max(0, currentStep - 1);
        maxUnlockedStep = Mathf.Min(maxUnlockedStep, currentStep);
        SaveProgress();

        ResetFight();
        RefreshMainProgressUi();

        if (mainStatusText != null)
            mainStatusText.text = "사망 · 소지품/바닥 " + lost + "개 소실 · " + StageLabel() + "로 후퇴";

        if (mainOverlay != null)
        {
            mainOverlay.SetActive(true);
            mainOverlay.transform.SetAsLastSibling();
        }
    }

    void RefreshBattleStatUi()
    {
        // Intentionally empty. Combat values are hidden from the battle screen.
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
        double q = enemyHp / Math.Max(1d, enemyMaxHp);
        if (q <= 0d) return 4;
        if (q <= .25d) return 3;
        if (q <= .50d) return 2;
        if (q <= .75d) return 1;
        return 0;
    }

    void SetStageOrFallback(int stage)
    {
        // Floor battles must only use that floor's character art.
        // Every image actually shown is registered in the codex.
        int visualFloor = CurrentVisualFloor();
        int index = Mathf.Clamp(stage, 0, 4) * 2;
        string code = "p" + index.ToString("00");
        Sprite s = assets.GetSprite("assets/f" + visualFloor + "_" + code + ".jpg");

        if (s == null)
        {
            code = "p00";
            s = assets.GetSprite("assets/f" + visualFloor + "_p00.jpg");
        }

        if (s != null)
        {
            XTapCodex.MarkImageDiscovered(visualFloor, code, inventory);
            SetSprite(s);
        }
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
        int visualFloor = CurrentVisualFloor();
        string code = prefix + i.ToString("00");
        Sprite s = assets.GetSprite("assets/f" + visualFloor + "_" + code + ".jpg");
        if (s != null)
        {
            XTapCodex.MarkImageDiscovered(visualFloor, code, inventory);
            SetSprite(s);
        }
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
        if (!vibrationEnabled) return;
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
        if (!sfxEnabled) return;
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
        if (!sfxEnabled) return;
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
        if (!sfxEnabled) return;
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

                // Avoid Noto Color Emoji and other symbol-only fonts. They can
                // render Hangul through fallback while silently dropping digits
                // in legacy Unity UI Text on some Samsung/Android devices.
                string[] priorityKeys =
                {
                    "Noto Sans CJK KR",
                    "Noto Sans KR",
                    "SamsungOneKorean",
                    "SamsungOne",
                    "Droid Sans Fallback",
                    "Malgun Gothic",
                    "NanumGothic",
                    "Roboto"
                };

                for (int k = 0; k < priorityKeys.Length; k++)
                {
                    for (int i = 0; i < installed.Length; i++)
                    {
                        string name = installed[i];
                        if (string.IsNullOrEmpty(name)) continue;
                        if (name.IndexOf("Emoji", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                        if (name.IndexOf("Color", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                        if (name.IndexOf(priorityKeys[k], StringComparison.OrdinalIgnoreCase) >= 0 &&
                            !preferred.Contains(name))
                            preferred.Add(name);
                    }
                }

                // Keep sane non-emoji fallbacks after the preferred Korean/Latin fonts.
                for (int i = 0; i < installed.Length; i++)
                {
                    string name = installed[i];
                    if (string.IsNullOrEmpty(name)) continue;
                    if (name.IndexOf("Emoji", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                    if (name.IndexOf("Color", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                    if (!preferred.Contains(name))
                        preferred.Add(name);
                }

                if (preferred.Count > 0)
                {
                    Font f = Font.CreateDynamicFontFromOSFont(preferred.ToArray(), 64);
                    if (f != null)
                    {
                        // Pre-warm every glyph family used by the HUD so digits never
                        // disappear while Hangul and symbols still render.
                        const string required =
                            "0123456789+-/%.,:()[] " +
                            "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz" +
                            "플레이어가방소지품바닥공격력방어력체력성장이동장비층구간칸개강화합성분해감옥대장간옵션진동효과음배경음악버전정보닫기";
                        f.RequestCharactersInTexture(required, 64, FontStyle.Normal);
                        f.RequestCharactersInTexture(required, 64, FontStyle.Bold);
                        return f;
                    }
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("X탑 폰트 초기화 실패: " + e.Message);
        }

        Font fallback = Resources.GetBuiltinResource<Font>("Arial.ttf");
        if (fallback != null)
            fallback.RequestCharactersInTexture("0123456789+-/%.,:()[]ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz", 64, FontStyle.Normal);
        return fallback;
    }

    Text MakeText(Transform parent, string value, int size, TextAnchor anchor, bool bold)
    {
        var go = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        go.transform.SetParent(parent, false);
        Text t = go.GetComponent<Text>();
        t.text = value;
        t.font = koreanFont;
        t.fontSize = Mathf.RoundToInt(size * UiFontScale);
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
