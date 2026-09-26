using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class XTapSigilBeatInput : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    public Action<Vector2, Vector2> Released;
    Vector2 down;
    bool pressed;

    public void OnPointerDown(PointerEventData eventData)
    {
        pressed = true;
        down = eventData.position;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!pressed) return;
        pressed = false;
        if (Released != null) Released(down, eventData.position);
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

    const double Bpm = 120.0;
    const double BeatSeconds = 60.0 / Bpm;
    const double ApproachSeconds = 1.60;
    const double FirstHitDelay = 2.00;
    const double PerfectWindow = 0.085;
    const double GoodWindow = 0.170;
    const int NoteCount = 32;

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

        ringSprite = CreateRingSprite(192, 12);
        runeSprite = CreateRuneSprite(160);

        beatSource = gameObject.AddComponent<AudioSource>();
        beatSource.playOnAwake = false;
        beatSource.spatialBlend = 0f;
        beatSource.volume = .70f;
        backingClip = CreatePrototypeBeat();

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

        Sprite reina = assets.GetSprite("assets/f1_p00.jpg");
        if (reina == null) reina = assets.GetSprite("assets/f1_p02.jpg");
        portrait.sprite = reina;
        portrait.enabled = reina != null;

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

        if (resolvedCount >= NoteCount && now > lastHitDsp + .80)
            FinishGame();
    }

    void BuildUi()
    {
        overlay = new GameObject("SigilBeatMiniGame", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        overlay.transform.SetParent(host, false);
        Image bg = overlay.GetComponent<Image>();
        bg.color = new Color(.006f, .006f, .012f, .998f);
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

        judgementText = MakeText(overlay.transform, "", 34, TextAnchor.MiddleCenter, true);
        judgementText.color = Color.white;
        judgementText.resizeTextForBestFit = true;
        judgementText.resizeTextMinSize = 42;
        judgementText.resizeTextMaxSize = 76;
        Anchor(judgementText.rectTransform, .16f, .50f, .84f, .60f);

        guideText = MakeText(
            overlay.transform,
            "룬의 타이밍 링이 판정 문양과 겹치는 순간\n표시 방향으로 손가락을 베십시오",
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
        startButton.interactable = true;
        startButtonText.text = "X SIGIL BEAT 시작";
        inputSurface.gameObject.SetActive(false);

        for (int i = 0; i < targetRings.Length; i++)
            targetRings[i].rectTransform.localScale = Vector3.one;
    }

    void StartGame()
    {
        if (!IsOpen || playing) return;

        StopGameAudio();
        ClearNotes();

        score = combo = maxCombo = perfectCount = goodCount = missCount = resolvedCount = 0;
        RefreshHud();

        judgementText.text = "READY";
        judgementText.color = Color.white;
        guideText.text = "탭 룬은 짧게 터치 · 방향 룬은 손가락으로 베기";
        recordText.text = "";
        startButton.gameObject.SetActive(false);
        inputSurface.gameObject.SetActive(true);

        BuildChart();

        songStartDsp = AudioSettings.dspTime + .85;
        for (int i = 0; i < notes.Count; i++)
        {
            notes[i].hitDsp = songStartDsp + FirstHitDelay + i * BeatSeconds;
            notes[i].spawnDsp = notes[i].hitDsp - ApproachSeconds;
        }

        lastHitDsp = notes.Count > 0 ? notes[notes.Count - 1].hitDsp : songStartDsp;

        if (beatSource != null && backingClip != null)
        {
            beatSource.clip = backingClip;
            beatSource.PlayScheduled(songStartDsp);
        }

        PlayerPrefs.SetInt(PlaysKey, PlayerPrefs.GetInt(PlaysKey, 0) + 1);
        PlayerPrefs.Save();

        playing = true;
    }

    void BuildChart()
    {
        notes.Clear();

        SlashDirection[] pattern =
        {
            SlashDirection.Tap,
            SlashDirection.Left,
            SlashDirection.Right,
            SlashDirection.Up,
            SlashDirection.Down,
            SlashDirection.Left,
            SlashDirection.Tap,
            SlashDirection.Right,
            SlashDirection.Up,
            SlashDirection.Down,
            SlashDirection.Right,
            SlashDirection.Left,
            SlashDirection.Tap,
            SlashDirection.Up,
            SlashDirection.Right,
            SlashDirection.Down
        };

        for (int i = 0; i < NoteCount; i++)
        {
            int target = (i * 3 + i / 4) % 4;
            SlashDirection direction = pattern[i % pattern.Length];

            Note note = new Note();
            note.index = i;
            note.target = target;
            note.direction = direction;
            note.targetNorm = TargetNorms[target];
            note.spawnNorm = SpawnFor(target, i);
            notes.Add(note);
        }
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
        core.sprite = runeSprite;
        core.color = new Color(TargetColors[note.target].r, TargetColors[note.target].g, TargetColors[note.target].b, .96f);
        core.raycastTarget = false;
        SetSize(core.rectTransform, 104f, 104f);
        note.core = core;

        Text label = MakeText(core.transform, DirectionLabel(note.direction), 16, TextAnchor.MiddleCenter, true);
        label.color = Color.white;
        label.resizeTextForBestFit = true;
        label.resizeTextMinSize = 22;
        label.resizeTextMaxSize = 38;
        Anchor(label.rectTransform, .08f, .08f, .92f, .92f);
        note.label = label;

        active.Add(note);
    }

    void HandleGesture(Vector2 startScreen, Vector2 endScreen)
    {
        if (!playing) return;

        Vector2 delta = endScreen - startScreen;
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

    AudioClip CreatePrototypeBeat()
    {
        const int sampleRate = 44100;
        const float lengthSeconds = 21f;
        int frames = Mathf.CeilToInt(sampleRate * lengthSeconds);
        float[] data = new float[frames];

        for (int i = 0; i < frames; i++)
        {
            double t = (double)i / sampleRate;
            double beatPos = t / BeatSeconds;
            int beatIndex = (int)Math.Floor(beatPos);
            double withinBeat = t - beatIndex * BeatSeconds;

            float sample = 0f;

            if (withinBeat < .11)
            {
                float env = Mathf.Exp((float)(-withinBeat * 36.0));
                float freq = (beatIndex % 4 == 0) ? 92f : 132f;
                sample += Mathf.Sin((float)(withinBeat * Math.PI * 2.0 * freq)) * env * .34f;
            }

            double half = BeatSeconds * .5;
            double withinHalf = t - Math.Floor(t / half) * half;
            if (withinHalf < .024)
            {
                float env = Mathf.Exp((float)(-withinHalf * 110.0));
                sample += Mathf.Sin((float)(withinHalf * Math.PI * 2.0 * 760.0)) * env * .08f;
            }

            data[i] = Mathf.Clamp(sample, -.8f, .8f);
        }

        AudioClip clip = AudioClip.Create("SigilBeatPrototype120BPM", frames, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
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
