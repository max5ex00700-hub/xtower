using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class XTapSigilBeatInput : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    public Action<Vector2, Vector2> Released;
    readonly Dictionary<int, Vector2> downs = new Dictionary<int, Vector2>();

    public void OnPointerDown(PointerEventData eventData)
    {
        downs[eventData.pointerId] = eventData.position;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        Vector2 down;
        if (!downs.TryGetValue(eventData.pointerId, out down)) return;
        downs.Remove(eventData.pointerId);
        if (Released != null) Released(down, eventData.position);
    }

    void OnDisable()
    {
        downs.Clear();
    }
}

public sealed class XTapSigilBeatMiniGame : MonoBehaviour
{
    const float UiFontScale = 2.15f;
    const int ReinaCharacterId = 1;
    const string BestScoreKey = "xtap_minigame_x_sigil_beat_best_score";
    const string BestComboKey = "xtap_minigame_x_sigil_beat_best_combo";
    const string PlaysKey = "xtap_minigame_x_sigil_beat_plays";
    const string ClearsKey = "xtap_minigame_x_sigil_beat_clears";

    const string MusicResource = "XTapSigilBeat/x_sigil_beat_stage1";
    const double Bpm = 133.92857142857142;
    const double ApproachSeconds = 1.60;
    const double PerfectWindow = 0.085;
    const double GoodWindow = 0.170;

    // Actual beat tracker timestamps from the supplied "We Come Alive" audio.
    // The gameplay clip begins two seconds before the first strong detected beat.
    static readonly double[] BeatHitTimes =
    {
        2.000, 2.437, 2.800, 3.184, 3.611, 4.069, 4.517, 4.976,
        5.445, 5.915, 6.352, 6.832, 7.312, 7.803, 8.229, 8.677,
        9.136, 9.573, 10.032, 10.512, 10.971, 11.472, 11.888, 12.347,
        12.741, 13.125, 13.520, 13.979, 14.427, 14.896, 15.355, 15.813,
        16.272, 16.731, 17.189, 17.659, 18.117, 18.576, 19.035, 19.493,
        19.952, 20.411, 20.859, 21.339, 21.797, 22.256, 22.715, 23.173,
        23.632, 24.091, 24.539, 25.019, 25.477, 25.936, 26.384, 26.843,
        27.301, 27.760, 28.208, 28.667, 29.125, 29.584, 30.043, 30.491,
        30.949, 31.408, 31.867, 32.325, 32.773, 33.232, 33.691, 34.139,
        34.608, 35.056, 35.515, 35.963, 36.421, 36.880, 37.339, 37.787
    };

    [Serializable]
    sealed class VisualPackData
    {
        public string sigil_background;
        public string thumb_left;
        public string thumb_right;
        public string timing_ring;
        public string note_down;
        public string note_right;
        public string note_left;
        public string note_up;
        public string note_tap;
        public string slash_left;
        public string slash_right;
        public string judgement_frame;
        public string title_plate;
    }

    enum SlashDirection
    {
        Tap,
        Up,
        Down,
        Left,
        Right
    }

    sealed class Note
    {
        public int index;
        public int target;
        public SlashDirection direction;
        public double hitOffset;
        public double hitDsp;
        public double spawnDsp;
        public bool spawned;
        public bool resolved;
        public RectTransform root;
        public Image core;
        public Image ring;
        public Text label;
        public Vector2 spawnNorm;
        public Vector2 targetNorm;
    }

    public bool IsOpen { get; private set; }

    RectTransform host;
    Font font;
    XTapOriginalApkAssets assets;
    Action onClosed;

    GameObject overlay;
    RectTransform gameArea;
    Image portrait;
    Image inputSurface;
    XTapSigilBeatInput inputReceiver;
    Text scoreText;
    Text comboText;
    Text accuracyText;
    Text judgementText;
    Text guideText;
    Text recordText;
    Text startButtonText;
    Button startButton;
    Button closeButton;
    RectTransform[] targetRoots = new RectTransform[4];
    Image[] targetRings = new Image[4];

    AudioSource beatSource;
    AudioClip backingClip;
    Sprite ringSprite;
    Sprite runeSprite;
    Sprite stageBackgroundSkin;
    Sprite thumbLeftSkin;
    Sprite thumbRightSkin;
    Sprite noteTapSkin;
    Sprite noteUpSkin;
    Sprite noteDownSkin;
    Sprite noteLeftSkin;
    Sprite noteRightSkin;
    Sprite slashLeftSkin;
    Sprite slashRightSkin;
    Sprite judgementFrameSkin;
    Sprite titlePlateSkin;
    Image leftThumb;
    Image rightThumb;
    Coroutine leftThumbRoutine;
    Coroutine rightThumbRoutine;

    readonly List<Note> notes = new List<Note>();
    readonly List<Note> active = new List<Note>();

    bool playing;
    double songStartDsp;
    double lastHitDsp;
    int score;
    int combo;
    int maxCombo;
    int perfectCount;
    int goodCount;
    int missCount;
    int resolvedCount;

    static readonly Vector2[] TargetNorms =
    {
        new Vector2(.20f, .28f),
        new Vector2(.40f, .38f),
        new Vector2(.60f, .38f),
        new Vector2(.80f, .28f)
    };

    static readonly Color[] TargetColors =
    {
        new Color(.74f, .33f, .94f, 1f),
        new Color(.96f, .67f, .20f, 1f),
        new Color(.28f, .86f, .72f, 1f),
        new Color(.90f, .36f, .62f, 1f)
    };

    public void Initialize(RectTransform parent, Font uiFont, XTapOriginalApkAssets originalAssets, Action closed)
    {
        host = parent;
        font = uiFont;
        assets = originalAssets;
        onClosed = closed;

        LoadVisualPack();
        if (ringSprite == null) ringSprite = CreateRingSprite(192, 12);
        if (runeSprite == null) runeSprite = CreateRuneSprite(160);

        beatSource = gameObject.AddComponent<AudioSource>();
        beatSource.playOnAwake = false;
        beatSource.spatialBlend = 0f;
        beatSource.volume = .82f;
        backingClip = Resources.Load<AudioClip>(MusicResource);
        if (backingClip == null)
            Debug.LogError("X SIGIL BEAT 음악 에셋 로드 실패: " + MusicResource);

        BuildUi();
        overlay.SetActive(false);
    }

    public void Open(int characterId)
    {
        if (characterId != ReinaCharacterId || overlay == null || assets == null || !assets.Ready)
            return;

        IsOpen = true;
        overlay.SetActive(true);
        overlay.transform.SetAsLastSibling();

        if (stageBackgroundSkin != null)
        {
            portrait.sprite = null;
            portrait.enabled = false;
        }
        else
        {
            Sprite reina = assets.GetSprite("assets/f1_p00.jpg");
            if (reina == null) reina = assets.GetSprite("assets/f1_p02.jpg");
            portrait.sprite = reina;
            portrait.enabled = reina != null;
        }

        ResetIdle();
    }

    public void Close()
    {
        StopGameAudio();
        ClearNotes();

        playing = false;
        IsOpen = false;

        if (overlay != null) overlay.SetActive(false);
        if (onClosed != null) onClosed();
    }

    void Update()
    {
        if (!IsOpen || !playing) return;

        double now = AudioSettings.dspTime;

        for (int i = 0; i < notes.Count; i++)
        {
            Note note = notes[i];
            if (!note.spawned && now >= note.spawnDsp)
                Spawn(note);
        }

        for (int i = active.Count - 1; i >= 0; i--)
        {
            Note note = active[i];
            if (note == null || note.resolved)
            {
                active.RemoveAt(i);
                continue;
            }

            double span = Math.Max(.001, note.hitDsp - note.spawnDsp);
            float p = Mathf.Clamp01((float)((now - note.spawnDsp) / span));
            float eased = 1f - Mathf.Pow(1f - p, 2.2f);
            Vector2 pos = Vector2.Lerp(note.spawnNorm, note.targetNorm, eased);
            SetAnchorPoint(note.root, pos);

            float scale = Mathf.Lerp(.48f, 1.05f, eased);
            note.core.rectTransform.localScale = Vector3.one * scale;

            float ringScale = Mathf.Lerp(2.10f, 1.00f, eased);
            note.ring.rectTransform.localScale = Vector3.one * ringScale;

            float alpha = Mathf.Lerp(.58f, 1f, eased);
            Color rc = note.ring.color;
            rc.a = alpha;
            note.ring.color = rc;

            if (now > note.hitDsp + GoodWindow)
                Resolve(note, "MISS", 0, false);
        }

        PulseTargets(now);

        if (notes.Count > 0 && resolvedCount >= notes.Count && now > lastHitDsp + .80)
            FinishGame();
    }

    void BuildUi()
    {
        overlay = new GameObject("SigilBeatMiniGame", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        overlay.transform.SetParent(host, false);
        Image bg = overlay.GetComponent<Image>();
        if (stageBackgroundSkin != null)
        {
            bg.sprite = stageBackgroundSkin;
            bg.type = Image.Type.Simple;
            bg.preserveAspect = false;
            bg.color = Color.white;
        }
        else
        {
            bg.color = new Color(.006f, .006f, .012f, .998f);
        }
        bg.raycastTarget = true;
        Anchor(bg.rectTransform, 0f, 0f, 1f, 1f);

        portrait = new GameObject("PrisonerPortrait", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
        portrait.transform.SetParent(overlay.transform, false);
        portrait.preserveAspect = true;
        portrait.raycastTarget = false;
        Anchor(portrait.rectTransform, .00f, .20f, 1f, 1f);

        Image shade = MakeImage(overlay.transform, "PrisonShade", new Color(.008f, .004f, .016f, .52f), 0f, 0f, 1f, 1f);
        shade.raycastTarget = false;

        Image topShade = MakeImage(overlay.transform, "TopHudShade", new Color(.010f, .008f, .018f, .92f), 0f, .86f, 1f, 1f);
        topShade.raycastTarget = false;

        Image bottomShade = MakeImage(overlay.transform, "BottomHudShade", new Color(.006f, .005f, .012f, .94f), 0f, 0f, 1f, .18f);
        bottomShade.raycastTarget = false;

        if (titlePlateSkin != null)
        {
            Image titlePlate = MakeImage(overlay.transform, "SigilTitlePlate", Color.white, .025f, .930f, .76f, .995f);
            titlePlate.sprite = titlePlateSkin;
            titlePlate.preserveAspect = false;
            titlePlate.raycastTarget = false;
        }

        Text title = MakeText(overlay.transform, "X SIGIL BEAT  ·  1F", 24, TextAnchor.MiddleLeft, true);
        title.color = new Color(1f, .86f, .56f, 1f);
        Anchor(title.rectTransform, .045f, .936f, .72f, .992f);

        closeButton = MakeButton(overlay.transform, "닫기", 16, new Color(.065f, .055f, .080f, .98f));
        Anchor(closeButton.GetComponent<RectTransform>(), .80f, .944f, .955f, .988f);
        closeButton.onClick.AddListener(Close);

        scoreText = MakeText(overlay.transform, "SCORE  0", 18, TextAnchor.MiddleLeft, true);
        scoreText.color = Color.white;
        Anchor(scoreText.rectTransform, .045f, .875f, .35f, .928f);

        comboText = MakeText(overlay.transform, "COMBO  0", 18, TextAnchor.MiddleCenter, true);
        comboText.color = new Color(1f, .72f, .24f, 1f);
        Anchor(comboText.rectTransform, .35f, .875f, .66f, .928f);

        accuracyText = MakeText(overlay.transform, "ACC  100.0%", 17, TextAnchor.MiddleRight, true);
        accuracyText.color = new Color(.72f, .95f, .88f, 1f);
        Anchor(accuracyText.rectTransform, .66f, .875f, .955f, .928f);

        gameArea = new GameObject("SigilBeatGameArea", typeof(RectTransform)).GetComponent<RectTransform>();
        gameArea.SetParent(overlay.transform, false);
        Anchor(gameArea, .02f, .17f, .98f, .86f);

        BuildPerspectiveGuides();
        BuildTargets();

        if (thumbLeftSkin != null)
        {
            leftThumb = MakeImage(overlay.transform, "LeftThumb", Color.white, .00f, .085f, .32f, .390f);
            leftThumb.sprite = thumbLeftSkin;
            leftThumb.preserveAspect = true;
            leftThumb.raycastTarget = false;
        }

        if (thumbRightSkin != null)
        {
            rightThumb = MakeImage(overlay.transform, "RightThumb", Color.white, .68f, .085f, 1.00f, .390f);
            rightThumb.sprite = thumbRightSkin;
            rightThumb.preserveAspect = true;
            rightThumb.raycastTarget = false;
        }

        inputSurface = new GameObject(
            "SigilBeatInput",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(XTapSigilBeatInput)
        ).GetComponent<Image>();
        inputSurface.transform.SetParent(gameArea, false);
        inputSurface.color = new Color(1f, 1f, 1f, .001f);
        inputSurface.raycastTarget = true;
        Anchor(inputSurface.rectTransform, 0f, 0f, 1f, 1f);
        inputReceiver = inputSurface.GetComponent<XTapSigilBeatInput>();
        inputReceiver.Released = HandleGesture;
        inputSurface.gameObject.SetActive(false);

        if (judgementFrameSkin != null)
        {
            Image judgementFrame = MakeImage(overlay.transform, "JudgementFrame", Color.white, .20f, .485f, .80f, .615f);
            judgementFrame.sprite = judgementFrameSkin;
            judgementFrame.preserveAspect = false;
            judgementFrame.raycastTarget = false;
        }

        judgementText = MakeText(overlay.transform, "", 34, TextAnchor.MiddleCenter, true);
        judgementText.color = Color.white;
        judgementText.resizeTextForBestFit = true;
        judgementText.resizeTextMinSize = 42;
        judgementText.resizeTextMaxSize = 76;
        Anchor(judgementText.rectTransform, .16f, .50f, .84f, .60f);

        guideText = MakeText(
            overlay.transform,
            "왼엄지 1·2  ·  오른엄지 3·4\n타이밍 링이 겹치는 순간 표시 방향으로 베십시오",
            17,
            TextAnchor.MiddleCenter,
            true
        );
        guideText.color = new Color(.95f, .91f, .84f, 1f);
        guideText.resizeTextForBestFit = true;
        guideText.resizeTextMinSize = 28;
        guideText.resizeTextMaxSize = 42;
        Anchor(guideText.rectTransform, .07f, .075f, .93f, .155f);

        recordText = MakeText(overlay.transform, "", 14, TextAnchor.MiddleCenter, true);
        recordText.color = new Color(.66f, .72f, .82f, 1f);
        Anchor(recordText.rectTransform, .07f, .025f, .93f, .072f);

        startButton = MakeButton(overlay.transform, "X SIGIL BEAT 시작", 20, new Color(.29f, .065f, .19f, .98f));
        Anchor(startButton.GetComponent<RectTransform>(), .20f, .185f, .80f, .245f);
        startButton.onClick.AddListener(StartGame);
        startButtonText = startButton.GetComponentInChildren<Text>();

        closeButton.transform.SetAsLastSibling();
    }

    void BuildPerspectiveGuides()
    {
        Vector2[] topPoints =
        {
            new Vector2(.02f, .92f),
            new Vector2(.30f, .98f),
            new Vector2(.70f, .98f),
            new Vector2(.98f, .92f)
        };

        for (int i = 0; i < 4; i++)
        {
            Image line = MakeImage(gameArea, "RunePath_" + i, new Color(TargetColors[i].r, TargetColors[i].g, TargetColors[i].b, .20f), 0f, 0f, 0f, 0f);
            line.raycastTarget = false;
            RectTransform r = line.rectTransform;
            Vector2 a = topPoints[i];
            Vector2 b = TargetNorms[i];
            r.anchorMin = r.anchorMax = (a + b) * .5f;
            r.pivot = new Vector2(.5f, .5f);
            r.sizeDelta = new Vector2(6f, 900f);
            float angle = Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg - 90f;
            r.localRotation = Quaternion.Euler(0f, 0f, angle);
        }
    }

    void BuildTargets()
    {
        for (int i = 0; i < 4; i++)
        {
            GameObject rootGo = new GameObject("ClashTarget_" + i, typeof(RectTransform));
            rootGo.transform.SetParent(gameArea, false);
            RectTransform root = rootGo.GetComponent<RectTransform>();
            SetAnchorPoint(root, TargetNorms[i]);
            root.sizeDelta = new Vector2(180f, 180f);
            targetRoots[i] = root;

            Image glow = new GameObject("Glow", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
            glow.transform.SetParent(root, false);
            glow.sprite = ringSprite;
            glow.color = new Color(TargetColors[i].r, TargetColors[i].g, TargetColors[i].b, .24f);
            glow.raycastTarget = false;
            SetSize(glow.rectTransform, 180f, 180f);

            Image ring = new GameObject("Ring", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
            ring.transform.SetParent(root, false);
            ring.sprite = ringSprite;
            ring.color = TargetColors[i];
            ring.raycastTarget = false;
            SetSize(ring.rectTransform, 136f, 136f);
            targetRings[i] = ring;

            Text t = MakeText(root, (i + 1).ToString(), 17, TextAnchor.MiddleCenter, true);
            t.color = new Color(1f, 1f, 1f, .62f);
            Anchor(t.rectTransform, .20f, .20f, .80f, .80f);
        }
    }

    void ResetIdle()
    {
        StopGameAudio();
        ClearNotes();

        playing = false;
        score = combo = maxCombo = perfectCount = goodCount = missCount = resolvedCount = 0;
        RefreshHud();

        judgementText.text = "X SIGIL BEAT";
        judgementText.color = new Color(1f, .82f, .42f, 1f);
        guideText.text = "룬의 타이밍 링이 판정 문양과 겹치는 순간\n표시 방향으로 손가락을 베십시오";
        recordText.text =
            "BEST  " + PlayerPrefs.GetInt(BestScoreKey, 0) +
            "  ·  MAX COMBO  " + PlayerPrefs.GetInt(BestComboKey, 0);

        startButton.gameObject.SetActive(true);
        startButton.interactable = backingClip != null;
        startButtonText.text = backingClip != null ? "X SIGIL BEAT 시작" : "음악 에셋 없음";
        inputSurface.gameObject.SetActive(false);

        for (int i = 0; i < targetRings.Length; i++)
            targetRings[i].rectTransform.localScale = Vector3.one;
    }

    void StartGame()
    {
        if (!IsOpen || playing || backingClip == null) return;

        StopGameAudio();
        ClearNotes();

        score = combo = maxCombo = perfectCount = goodCount = missCount = resolvedCount = 0;
        RefreshHud();

        judgementText.text = "READY";
        judgementText.color = Color.white;
        guideText.text = "양손 엄지 사용 · TAP은 짧게 · 방향 룬은 표시 방향으로 스와이프";
        recordText.text = "We Come Alive  ·  " + Bpm.ToString("0") + " BPM";
        startButton.gameObject.SetActive(false);
        inputSurface.gameObject.SetActive(true);

        BuildChart();

        songStartDsp = AudioSettings.dspTime + .85;
        for (int i = 0; i < notes.Count; i++)
        {
            notes[i].hitDsp = songStartDsp + notes[i].hitOffset;
            notes[i].spawnDsp = notes[i].hitDsp - ApproachSeconds;
        }

        lastHitDsp = notes.Count > 0 ? notes[notes.Count - 1].hitDsp : songStartDsp;

        beatSource.clip = backingClip;
        beatSource.PlayScheduled(songStartDsp);

        PlayerPrefs.SetInt(PlaysKey, PlayerPrefs.GetInt(PlaysKey, 0) + 1);
        PlayerPrefs.Save();

        playing = true;
    }

    void BuildChart()
    {
        notes.Clear();

        int[] lanePattern = { 0, 2, 1, 3, 0, 3, 1, 2 };
        SlashDirection[] directionPattern =
        {
            SlashDirection.Tap,
            SlashDirection.Tap,
            SlashDirection.Left,
            SlashDirection.Right,
            SlashDirection.Up,
            SlashDirection.Down,
            SlashDirection.Tap,
            SlashDirection.Tap
        };

        for (int i = 0; i < BeatHitTimes.Length; i++)
        {
            int target = lanePattern[i % lanePattern.Length];
            SlashDirection direction = i < 8
                ? SlashDirection.Tap
                : directionPattern[(i + i / 8) % directionPattern.Length];

            AddChartNote(BeatHitTimes[i], target, direction, i);

            // Chorus/build sections gain two-thumb chords. Each chord always spans
            // left and right halves so both thumbs are used at the same instant.
            if (i >= 28 && i <= 76 && (i % 4) == 0)
            {
                int opposite = target < 2 ? 2 + (target % 2) : target % 2;
                SlashDirection oppositeDirection = direction == SlashDirection.Tap
                    ? SlashDirection.Tap
                    : (target < 2 ? SlashDirection.Right : SlashDirection.Left);
                AddChartNote(BeatHitTimes[i], opposite, oppositeDirection, 1000 + i);
            }
        }
    }

    void AddChartNote(double hitOffset, int target, SlashDirection direction, int seed)
    {
        Note note = new Note();
        note.index = notes.Count;
        note.target = Mathf.Clamp(target, 0, 3);
        note.direction = direction;
        note.hitOffset = hitOffset;
        note.targetNorm = TargetNorms[note.target];
        note.spawnNorm = SpawnFor(note.target, seed);
        notes.Add(note);
    }

    Vector2 SpawnFor(int target, int index)
    {
        bool alternate = (index & 1) == 1;

        if (target == 0)
            return alternate ? new Vector2(.02f, .62f) : new Vector2(.06f, .94f);
        if (target == 1)
            return alternate ? new Vector2(.15f, .96f) : new Vector2(.35f, 1.02f);
        if (target == 2)
            return alternate ? new Vector2(.85f, .96f) : new Vector2(.65f, 1.02f);

        return alternate ? new Vector2(.98f, .62f) : new Vector2(.94f, .94f);
    }

    void Spawn(Note note)
    {
        if (note.spawned || note.resolved) return;
        note.spawned = true;

        GameObject rootGo = new GameObject("RuneNote_" + note.index, typeof(RectTransform));
        rootGo.transform.SetParent(gameArea, false);
        RectTransform root = rootGo.GetComponent<RectTransform>();
        root.sizeDelta = new Vector2(170f, 170f);
        SetAnchorPoint(root, note.spawnNorm);
        note.root = root;

        Image outer = new GameObject("TimingRing", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
        outer.transform.SetParent(root, false);
        outer.sprite = ringSprite;
        outer.color = TargetColors[note.target];
        outer.raycastTarget = false;
        SetSize(outer.rectTransform, 170f, 170f);
        note.ring = outer;

        Image core = new GameObject("RuneCore", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
        core.transform.SetParent(root, false);
        Sprite authoredNote = GetNoteSprite(note.direction);
        core.sprite = authoredNote != null ? authoredNote : runeSprite;
        core.color = Color.white;
        core.raycastTarget = false;
        SetSize(core.rectTransform, 118f, 132f);
        note.core = core;

        if (authoredNote == null)
        {
            Text label = MakeText(core.transform, DirectionLabel(note.direction), 16, TextAnchor.MiddleCenter, true);
            label.color = Color.white;
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 22;
            label.resizeTextMaxSize = 38;
            Anchor(label.rectTransform, .08f, .08f, .92f, .92f);
            note.label = label;
        }

        active.Add(note);
    }

    void HandleGesture(Vector2 startScreen, Vector2 endScreen)
    {
        if (!playing) return;

        Vector2 delta = endScreen - startScreen;
        bool leftSide = startScreen.x < Screen.width * .5f;
        PulseThumb(leftSide, delta);
        PlaySlashFx(leftSide, delta);
        SlashDirection inputDirection = DirectionFromGesture(delta);
        Vector2 sampleScreen = inputDirection == SlashDirection.Tap
            ? endScreen
            : (startScreen + endScreen) * .5f;

        Vector2 sampleNorm;
        if (!ScreenToGameNorm(sampleScreen, out sampleNorm))
            return;

        double now = AudioSettings.dspTime;
        Note best = null;
        double bestTime = double.MaxValue;

        for (int i = 0; i < active.Count; i++)
        {
            Note note = active[i];
            if (note == null || note.resolved || !note.spawned) continue;

            double dt = Math.Abs(now - note.hitDsp);
            if (dt > GoodWindow) continue;

            float spatial = Vector2.Distance(sampleNorm, note.targetNorm);
            if (spatial > .22f) continue;

            if (dt < bestTime)
            {
                bestTime = dt;
                best = note;
            }
        }

        if (best == null)
        {
            ShowJudgement("MISS", new Color(1f, .34f, .38f, 1f));
            combo = 0;
            RefreshHud();
            return;
        }

        if (best.direction != inputDirection)
        {
            Resolve(best, "WRONG", 0, false);
            return;
        }

        if (bestTime <= PerfectWindow)
            Resolve(best, "PERFECT", 300, true);
        else
            Resolve(best, "GOOD", 150, true);
    }

    SlashDirection DirectionFromGesture(Vector2 delta)
    {
        float threshold = Mathf.Max(62f, Mathf.Min(Screen.width, Screen.height) * .055f);
        if (delta.magnitude < threshold)
            return SlashDirection.Tap;

        if (Mathf.Abs(delta.x) > Mathf.Abs(delta.y))
            return delta.x >= 0f ? SlashDirection.Right : SlashDirection.Left;

        return delta.y >= 0f ? SlashDirection.Up : SlashDirection.Down;
    }

    void Resolve(Note note, string label, int baseScore, bool success)
    {
        if (note == null || note.resolved) return;
        note.resolved = true;
        resolvedCount++;

        if (success)
        {
            combo++;
            maxCombo = Mathf.Max(maxCombo, combo);

            if (label == "PERFECT")
            {
                perfectCount++;
                score += baseScore + Mathf.Min(200, combo * 4);
                ShowJudgement(label, new Color(1f, .84f, .30f, 1f));
            }
            else
            {
                goodCount++;
                score += baseScore + Mathf.Min(100, combo * 2);
                ShowJudgement(label, new Color(.42f, .92f, .76f, 1f));
            }

            FlashTarget(note.target);
        }
        else
        {
            missCount++;
            combo = 0;
            ShowJudgement(label == "WRONG" ? "MISS · 방향" : "MISS", new Color(1f, .34f, .38f, 1f));
        }

        if (note.root != null)
            Destroy(note.root.gameObject);

        RefreshHud();
    }

    void FinishGame()
    {
        if (!playing) return;
        playing = false;
        inputSurface.gameObject.SetActive(false);
        StopGameAudio();

        int total = perfectCount + goodCount + missCount;
        float accuracy = total <= 0 ? 0f : (perfectCount + goodCount * .5f) / total;
        bool clear = accuracy >= .70f;

        int best = PlayerPrefs.GetInt(BestScoreKey, 0);
        if (score > best) PlayerPrefs.SetInt(BestScoreKey, score);

        int bestCombo = PlayerPrefs.GetInt(BestComboKey, 0);
        if (maxCombo > bestCombo) PlayerPrefs.SetInt(BestComboKey, maxCombo);

        if (clear)
            PlayerPrefs.SetInt(ClearsKey, PlayerPrefs.GetInt(ClearsKey, 0) + 1);

        PlayerPrefs.Save();

        judgementText.color = clear
            ? new Color(1f, .82f, .32f, 1f)
            : new Color(1f, .42f, .42f, 1f);
        judgementText.text = clear ? "CLASH CLEAR" : "RETRY";

        guideText.text =
            "PERFECT " + perfectCount +
            "   GOOD " + goodCount +
            "   MISS " + missCount +
            "\nMAX COMBO " + maxCombo;

        recordText.text =
            "SCORE " + score +
            "  ·  ACC " + (accuracy * 100f).ToString("0.0") + "%";

        startButton.gameObject.SetActive(true);
        startButton.interactable = true;
        startButtonText.text = "다시 시작";
    }

    void PulseTargets(double dsp)
    {
        for (int i = 0; i < targetRings.Length; i++)
        {
            float pulse = 1f + Mathf.Sin((float)dsp * 5.6f + i * .8f) * .035f;
            targetRings[i].rectTransform.localScale = Vector3.one * pulse;
        }
    }

    void FlashTarget(int target)
    {
        if (target < 0 || target >= targetRings.Length) return;
        targetRings[target].rectTransform.localScale = Vector3.one * 1.28f;
    }

    void ShowJudgement(string value, Color color)
    {
        judgementText.text = value;
        judgementText.color = color;
    }

    void RefreshHud()
    {
        if (scoreText != null) scoreText.text = "SCORE  " + score;
        if (comboText != null) comboText.text = "COMBO  " + combo;

        int total = perfectCount + goodCount + missCount;
        float acc = total <= 0 ? 1f : (perfectCount + goodCount * .5f) / total;
        if (accuracyText != null)
            accuracyText.text = "ACC  " + (acc * 100f).ToString("0.0") + "%";
    }

    bool ScreenToGameNorm(Vector2 screen, out Vector2 norm)
    {
        norm = Vector2.zero;
        Vector2 local;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(gameArea, screen, null, out local))
            return false;

        Rect r = gameArea.rect;
        if (Mathf.Abs(r.width) < 1f || Mathf.Abs(r.height) < 1f)
            return false;

        norm.x = Mathf.InverseLerp(r.xMin, r.xMax, local.x);
        norm.y = Mathf.InverseLerp(r.yMin, r.yMax, local.y);
        return true;
    }

    string DirectionLabel(SlashDirection direction)
    {
        switch (direction)
        {
            case SlashDirection.Up: return "상";
            case SlashDirection.Down: return "하";
            case SlashDirection.Left: return "좌";
            case SlashDirection.Right: return "우";
            default: return "TAP";
        }
    }

    void ClearNotes()
    {
        for (int i = 0; i < active.Count; i++)
        {
            Note note = active[i];
            if (note != null && note.root != null)
                Destroy(note.root.gameObject);
        }

        active.Clear();
        notes.Clear();
    }

    void StopGameAudio()
    {
        if (beatSource != null)
            beatSource.Stop();
    }

    void LoadVisualPack()
    {
        try
        {
            TextAsset packAsset = Resources.Load<TextAsset>("XTapSigilBeat/visual_pack");
            if (packAsset == null || string.IsNullOrEmpty(packAsset.text))
            {
                Debug.LogError("X SIGIL BEAT visual pack 누락.");
                return;
            }

            VisualPackData pack = JsonUtility.FromJson<VisualPackData>(packAsset.text);
            if (pack == null)
            {
                Debug.LogError("X SIGIL BEAT visual pack 파싱 실패.");
                return;
            }

            stageBackgroundSkin = DecodePackSprite(pack.sigil_background, "SigilBackground");
            thumbLeftSkin = DecodePackSprite(pack.thumb_left, "SigilThumbLeft");
            thumbRightSkin = DecodePackSprite(pack.thumb_right, "SigilThumbRight");
            ringSprite = DecodePackSprite(pack.timing_ring, "SigilTimingRing");
            noteTapSkin = DecodePackSprite(pack.note_tap, "SigilNoteTap");
            noteUpSkin = DecodePackSprite(pack.note_up, "SigilNoteUp");
            noteDownSkin = DecodePackSprite(pack.note_down, "SigilNoteDown");
            noteLeftSkin = DecodePackSprite(pack.note_left, "SigilNoteLeft");
            noteRightSkin = DecodePackSprite(pack.note_right, "SigilNoteRight");
            slashLeftSkin = DecodePackSprite(pack.slash_left, "SigilSlashLeft");
            slashRightSkin = DecodePackSprite(pack.slash_right, "SigilSlashRight");
            judgementFrameSkin = DecodePackSprite(pack.judgement_frame, "SigilJudgementFrame");
            titlePlateSkin = DecodePackSprite(pack.title_plate, "SigilTitlePlate");
        }
        catch (Exception e)
        {
            Debug.LogError("X SIGIL BEAT visual pack 로드 실패: " + e.Message);
        }
    }

    Sprite DecodePackSprite(string encoded, string spriteName)
    {
        if (string.IsNullOrEmpty(encoded)) return null;
        try
        {
            byte[] bytes = Convert.FromBase64String(encoded);
            Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!texture.LoadImage(bytes, false))
            {
                Destroy(texture);
                return null;
            }
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(.5f, .5f), 100f);
            sprite.name = spriteName;
            return sprite;
        }
        catch
        {
            return null;
        }
    }

    Sprite GetNoteSprite(SlashDirection direction)
    {
        switch (direction)
        {
            case SlashDirection.Up: return noteUpSkin;
            case SlashDirection.Down: return noteDownSkin;
            case SlashDirection.Left: return noteLeftSkin;
            case SlashDirection.Right: return noteRightSkin;
            default: return noteTapSkin;
        }
    }

    void PulseThumb(bool left, Vector2 delta)
    {
        Image hand = left ? leftThumb : rightThumb;
        if (hand == null) return;

        Coroutine running = left ? leftThumbRoutine : rightThumbRoutine;
        if (running != null) StopCoroutine(running);

        Coroutine next = StartCoroutine(ThumbPulseRoutine(hand, left, delta));
        if (left) leftThumbRoutine = next;
        else rightThumbRoutine = next;
    }

    IEnumerator ThumbPulseRoutine(Image hand, bool left, Vector2 delta)
    {
        RectTransform rect = hand.rectTransform;
        Vector3 baseScale = Vector3.one;
        float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
        if (delta.sqrMagnitude < 36f) angle = left ? 8f : -8f;
        angle = Mathf.Clamp(angle, -55f, 55f);

        float t = 0f;
        while (t < .12f)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / .12f);
            float kick = Mathf.Sin(p * Mathf.PI);
            rect.localScale = baseScale * (1f + .18f * kick);
            rect.localRotation = Quaternion.Euler(0f, 0f, angle * .22f * kick);
            yield return null;
        }

        rect.localScale = baseScale;
        rect.localRotation = Quaternion.identity;
    }

    void PlaySlashFx(bool left, Vector2 delta)
    {
        Sprite skin = left ? slashLeftSkin : slashRightSkin;
        if (skin == null || gameArea == null) return;

        Image fx = new GameObject("ThumbSlashFx", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
        fx.transform.SetParent(gameArea, false);
        fx.sprite = skin;
        fx.preserveAspect = true;
        fx.raycastTarget = false;
        fx.color = new Color(1f, 1f, 1f, .94f);
        Anchor(fx.rectTransform, left ? .00f : .48f, .08f, left ? .52f : 1.00f, .48f);

        float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
        if (delta.sqrMagnitude > 36f) fx.rectTransform.localRotation = Quaternion.Euler(0f, 0f, angle * .18f);
        StartCoroutine(SlashFxRoutine(fx));
    }

    IEnumerator SlashFxRoutine(Image fx)
    {
        float t = 0f;
        while (fx != null && t < .18f)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / .18f);
            fx.rectTransform.localScale = Vector3.one * Mathf.Lerp(.82f, 1.16f, p);
            Color c = fx.color;
            c.a = 1f - p;
            fx.color = c;
            yield return null;
        }
        if (fx != null) Destroy(fx.gameObject);
    }

    Sprite CreateRingSprite(int size, int thickness)
    {
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        Color32[] pixels = new Color32[size * size];
        float center = (size - 1) * .5f;
        float outer = size * .46f;
        float inner = outer - thickness;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - center;
                float dy = y - center;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                byte a = (byte)(d <= outer && d >= inner ? 255 : 0);
                pixels[y * size + x] = new Color32(255, 255, 255, a);
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, false);
        return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(.5f, .5f), 100f);
    }

    Sprite CreateRuneSprite(int size)
    {
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;
        Color32[] pixels = new Color32[size * size];

        float c = (size - 1) * .5f;
        float radius = size * .34f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Abs(x - c);
                float dy = Mathf.Abs(y - c);
                float diamond = dx + dy;
                float edge = radius * 1.42f;
                byte a = diamond <= edge ? (byte)235 : (byte)0;

                if (a > 0 && diamond > edge - 9f)
                    pixels[y * size + x] = new Color32(255, 255, 255, 255);
                else
                    pixels[y * size + x] = new Color32(255, 255, 255, a);
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, false);
        return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(.5f, .5f), 100f);
    }

    Image MakeImage(Transform parent, string name, Color color, float x1, float y1, float x2, float y2)
    {
        Image image = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
        image.transform.SetParent(parent, false);
        image.color = color;
        Anchor(image.rectTransform, x1, y1, x2, y2);
        return image;
    }

    Text MakeText(Transform parent, string value, int size, TextAnchor align, bool bold)
    {
        Text t = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text)).GetComponent<Text>();
        t.transform.SetParent(parent, false);
        t.font = font;
        t.text = value;
        t.fontSize = Mathf.RoundToInt(size * UiFontScale);
        t.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
        t.alignment = align;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Truncate;
        t.raycastTarget = false;

        Outline outline = t.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, .84f);
        outline.effectDistance = new Vector2(2f, -2f);
        return t;
    }

    Button MakeButton(Transform parent, string label, int size, Color color)
    {
        GameObject go = new GameObject(label + "Button", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);

        Image bg = go.GetComponent<Image>();
        bg.color = color;

        Text t = MakeText(go.transform, label, size, TextAnchor.MiddleCenter, true);
        t.color = new Color(.98f, .96f, .92f, 1f);
        Anchor(t.rectTransform, .04f, .05f, .96f, .95f);

        Button b = go.GetComponent<Button>();
        b.targetGraphic = bg;
        return b;
    }

    static void SetAnchorPoint(RectTransform r, Vector2 normalized)
    {
        r.anchorMin = normalized;
        r.anchorMax = normalized;
        r.pivot = new Vector2(.5f, .5f);
        r.anchoredPosition = Vector2.zero;
    }

    static void SetSize(RectTransform r, float width, float height)
    {
        r.anchorMin = r.anchorMax = new Vector2(.5f, .5f);
        r.pivot = new Vector2(.5f, .5f);
        r.sizeDelta = new Vector2(width, height);
        r.anchoredPosition = Vector2.zero;
    }

    static void Anchor(RectTransform r, float x1, float y1, float x2, float y2)
    {
        r.anchorMin = new Vector2(x1, y1);
        r.anchorMax = new Vector2(x2, y2);
        r.offsetMin = Vector2.zero;
        r.offsetMax = Vector2.zero;
    }
}
