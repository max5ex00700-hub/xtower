using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class XTapParryTapReceiver : MonoBehaviour, IPointerDownHandler
{
    public Action<PointerEventData> Pressed;

    public void OnPointerDown(PointerEventData eventData)
    {
        if (Pressed != null) Pressed(eventData);
    }
}

public sealed class XTapReinaParryMiniGame : MonoBehaviour
{
    const float UiFontScale = 2.15f;
    const int ReinaCharacterId = 1;
    const string VibrationSettingKey = "xtap_option_vibration";
    const string SfxSettingKey = "xtap_option_sfx";
    const string BestScoreKey = "xtap_minigame_reina_parry_best_score";
    const string BestGradeKey = "xtap_minigame_reina_parry_best_grade";
    const string PlaysKey = "xtap_minigame_reina_parry_plays";
    const string ClearsKey = "xtap_minigame_reina_parry_clears";

    enum Phase
    {
        Idle,
        Countdown,
        Telegraph,
        Feint,
        Strike,
        Counter,
        Result
    }

    enum ParryResult
    {
        None,
        Perfect,
        Good,
        Miss
    }

    public bool IsOpen { get; private set; }

    RectTransform host;
    Font font;
    XTapOriginalApkAssets assets;
    Action onClosed;

    GameObject overlay;
    RectTransform gameArea;
    Image portrait;
    Image timingRing;
    Image attackMarker;
    Image attackTrail;
    Image counterTarget;
    Image hitFlash;
    Text attackMarkerText;
    Text counterText;
    Text titleText;
    Text scoreText;
    Text guardText;
    Text comboText;
    Text statusText;
    Text cueText;
    Text bubbleText;
    Text recordText;
    Text startButtonText;
    Button startButton;
    Button closeButton;
    XTapParryTapReceiver tapReceiver;
    XTapParryTapReceiver counterReceiver;

    AudioSource audioSource;
    readonly Dictionary<string, AudioClip> sfxClips =
        new Dictionary<string, AudioClip>(StringComparer.OrdinalIgnoreCase);

    Sprite ringSprite;
    Coroutine gameRoutine;
    Coroutine flashRoutine;

    Phase phase = Phase.Idle;
    ParryResult lastResult = ParryResult.None;

    int score;
    int combo;
    int guard;
    int round;
    int perfectCount;
    int goodCount;
    int missCount;
    int bestScore;

    bool inputConsumed;
    float strikeProgress;

    Vector2 counterNorm;
    Vector2 counterVelocity;

    readonly string[] fallbackSuccess =
    {
        "제법이군.",
        "이번 건 인정하지.",
        "좋아. 다시 받아 봐.",
        "칼끝을 읽었군."
    };

    readonly string[] fallbackMiss =
    {
        "느려.",
        "눈이 검을 못 따라오는군.",
        "페이스를 빼앗겼어.",
        "그 정도로는 못 막아."
    };

    public void Initialize(RectTransform parent, Font uiFont, XTapOriginalApkAssets originalAssets, Action closed)
    {
        host = parent;
        font = uiFont;
        assets = originalAssets;
        onClosed = closed;

        ringSprite = CreateRingSprite(160, 10);
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
        audioSource.volume = 1f;

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

        ResetIdleView();
    }

    public void Close()
    {
        if (!IsOpen) return;

        if (gameRoutine != null)
        {
            StopCoroutine(gameRoutine);
            gameRoutine = null;
        }

        if (flashRoutine != null)
        {
            StopCoroutine(flashRoutine);
            flashRoutine = null;
        }

        phase = Phase.Idle;
        IsOpen = false;
        ResetTransientVisuals();

        if (overlay != null) overlay.SetActive(false);
        if (onClosed != null) onClosed();
    }

    void BuildUi()
    {
        overlay = new GameObject("ReinaParryMiniGame", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        overlay.transform.SetParent(host, false);
        Image overlayBg = overlay.GetComponent<Image>();
        overlayBg.color = new Color(.008f, .009f, .014f, .995f);
        overlayBg.raycastTarget = true;
        Anchor(overlayBg.rectTransform, 0f, 0f, 1f, 1f);

        portrait = new GameObject("ReinaPortrait", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
        portrait.transform.SetParent(overlay.transform, false);
        portrait.color = Color.white;
        portrait.preserveAspect = true;
        portrait.raycastTarget = false;
        Anchor(portrait.rectTransform, .02f, .08f, .98f, .92f);

        Image darken = MakeImage(overlay.transform, "CinematicShade", new Color(.005f, .006f, .010f, .34f), 0f, 0f, 1f, 1f);
        darken.raycastTarget = false;

        gameArea = new GameObject("ParryGameArea", typeof(RectTransform)).GetComponent<RectTransform>();
        gameArea.SetParent(overlay.transform, false);
        Anchor(gameArea, .02f, .10f, .98f, .86f);

        Image inputSurface = new GameObject(
            "ParryInputSurface",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(XTapParryTapReceiver)
        ).GetComponent<Image>();
        inputSurface.transform.SetParent(gameArea, false);
        inputSurface.color = new Color(1f, 1f, 1f, .001f);
        inputSurface.raycastTarget = true;
        Anchor(inputSurface.rectTransform, 0f, 0f, 1f, 1f);
        tapReceiver = inputSurface.GetComponent<XTapParryTapReceiver>();
        tapReceiver.Pressed = delegate { HandleParryPress(); };

        timingRing = MakeImage(gameArea, "ParryTimingRing", new Color(1f, .72f, .18f, .76f), .5f, .5f, .5f, .5f);
        timingRing.sprite = ringSprite;
        timingRing.raycastTarget = false;
        SetSize(timingRing.rectTransform, 230f, 230f);
        timingRing.rectTransform.anchoredPosition = new Vector2(0f, 120f);
        timingRing.gameObject.SetActive(false);

        attackTrail = MakeImage(gameArea, "AttackTrail", new Color(1f, .18f, .10f, .74f), .5f, .5f, .5f, .5f);
        attackTrail.raycastTarget = false;
        SetSize(attackTrail.rectTransform, 260f, 14f);
        attackTrail.gameObject.SetActive(false);

        attackMarker = MakeImage(gameArea, "AttackMarker", new Color(1f, .22f, .10f, .98f), .5f, .5f, .5f, .5f);
        attackMarker.sprite = ringSprite;
        attackMarker.raycastTarget = false;
        SetSize(attackMarker.rectTransform, 104f, 104f);
        attackMarker.gameObject.SetActive(false);

        attackMarkerText = MakeText(attackMarker.transform, "!", 28, TextAnchor.MiddleCenter, true);
        attackMarkerText.color = Color.white;
        Anchor(attackMarkerText.rectTransform, .12f, .08f, .88f, .92f);

        counterTarget = MakeImage(gameArea, "CounterTarget", new Color(1f, .76f, .16f, .98f), .5f, .5f, .5f, .5f);
        counterTarget.sprite = ringSprite;
        counterTarget.raycastTarget = true;
        SetSize(counterTarget.rectTransform, 150f, 150f);
        counterReceiver = counterTarget.gameObject.AddComponent<XTapParryTapReceiver>();
        counterReceiver.Pressed = delegate { HandleCounterPress(); };
        counterTarget.gameObject.SetActive(false);

        counterText = MakeText(counterTarget.transform, "COUNTER", 16, TextAnchor.MiddleCenter, true);
        counterText.color = new Color(1f, .94f, .64f, 1f);
        counterText.resizeTextForBestFit = true;
        counterText.resizeTextMinSize = 20;
        counterText.resizeTextMaxSize = 34;
        counterText.raycastTarget = false;
        Anchor(counterText.rectTransform, .06f, .20f, .94f, .80f);

        hitFlash = MakeImage(overlay.transform, "ParryFlash", new Color(1f, 1f, 1f, 0f), 0f, 0f, 1f, 1f);
        hitFlash.raycastTarget = false;

        titleText = MakeText(overlay.transform, "REINA  ·  PARRY TRIAL", 22, TextAnchor.MiddleLeft, true);
        titleText.color = new Color(1f, .87f, .63f, 1f);
        Anchor(titleText.rectTransform, .055f, .925f, .73f, .985f);

        closeButton = MakeButton(overlay.transform, "닫기", 16, new Color(.08f, .075f, .09f, .98f));
        Anchor(closeButton.GetComponent<RectTransform>(), .79f, .928f, .95f, .982f);
        closeButton.onClick.AddListener(Close);

        scoreText = MakeText(overlay.transform, "SCORE 0", 17, TextAnchor.MiddleLeft, true);
        scoreText.color = Color.white;
        Anchor(scoreText.rectTransform, .055f, .865f, .36f, .918f);

        guardText = MakeText(overlay.transform, "GUARD ◆◆◆", 17, TextAnchor.MiddleCenter, true);
        guardText.color = new Color(.55f, .83f, 1f, 1f);
        Anchor(guardText.rectTransform, .355f, .865f, .67f, .918f);

        comboText = MakeText(overlay.transform, "COMBO 0", 17, TextAnchor.MiddleRight, true);
        comboText.color = new Color(1f, .72f, .20f, 1f);
        Anchor(comboText.rectTransform, .66f, .865f, .945f, .918f);

        cueText = MakeText(overlay.transform, "", 30, TextAnchor.MiddleCenter, true);
        cueText.color = new Color(1f, .84f, .45f, 1f);
        cueText.resizeTextForBestFit = true;
        cueText.resizeTextMinSize = 42;
        cueText.resizeTextMaxSize = 72;
        Anchor(cueText.rectTransform, .12f, .735f, .88f, .84f);

        Image bubblePanel = MakeImage(overlay.transform, "ReinaDialoguePanel", new Color(.025f, .030f, .045f, .88f), .14f, .635f, .86f, .715f);
        bubblePanel.raycastTarget = false;
        bubbleText = MakeText(bubblePanel.transform, "", 18, TextAnchor.MiddleCenter, true);
        bubbleText.color = new Color(.97f, .97f, 1f, 1f);
        bubbleText.resizeTextForBestFit = true;
        bubbleText.resizeTextMinSize = 36;
        bubbleText.resizeTextMaxSize = 54;
        Anchor(bubbleText.rectTransform, .035f, .08f, .965f, .92f);

        statusText = MakeText(overlay.transform, "", 24, TextAnchor.MiddleCenter, true);
        statusText.color = Color.white;
        Anchor(statusText.rectTransform, .10f, .18f, .90f, .28f);

        recordText = MakeText(overlay.transform, "", 15, TextAnchor.MiddleCenter, true);
        recordText.color = new Color(.70f, .76f, .86f, 1f);
        Anchor(recordText.rectTransform, .08f, .118f, .92f, .172f);

        startButton = MakeButton(overlay.transform, "훈련 시작", 20, new Color(.34f, .08f, .075f, .98f));
        Anchor(startButton.GetComponent<RectTransform>(), .21f, .035f, .79f, .105f);
        startButton.onClick.AddListener(StartGame);
        startButtonText = startButton.GetComponentInChildren<Text>();

        closeButton.transform.SetAsLastSibling();
    }

    void ResetIdleView()
    {
        if (gameRoutine != null)
        {
            StopCoroutine(gameRoutine);
            gameRoutine = null;
        }

        phase = Phase.Idle;
        score = 0;
        combo = 0;
        guard = 3;
        round = 0;
        perfectCount = 0;
        goodCount = 0;
        missCount = 0;
        bestScore = PlayerPrefs.GetInt(BestScoreKey, 0);

        cueText.text = "검이 닿기 직전에 화면을 터치";
        statusText.color = Color.white;
        statusText.text = "3연속 패링 → 페이크 판별 → 최종 COUNTER";
        bubbleText.text = "레이나의 검을 읽고 정확한 순간에 받아내십시오.";
        recordText.text = "BEST  " + bestScore + "  ·  " + PlayerPrefs.GetString(BestGradeKey, "-");
        startButton.gameObject.SetActive(true);
        startButton.interactable = true;
        startButtonText.text = "훈련 시작";

        ResetTransientVisuals();
        RefreshHud();
    }

    void StartGame()
    {
        if (!IsOpen || (phase != Phase.Idle && phase != Phase.Result))
            return;

        if (gameRoutine != null)
            StopCoroutine(gameRoutine);

        gameRoutine = StartCoroutine(GameRoutine());
    }

    IEnumerator GameRoutine()
    {
        score = 0;
        combo = 0;
        guard = 3;
        round = 0;
        perfectCount = 0;
        goodCount = 0;
        missCount = 0;
        startButton.gameObject.SetActive(false);
        recordText.text = "";
        bubbleText.text = "";
        RefreshHud();

        phase = Phase.Countdown;
        for (int i = 3; i >= 1; i--)
        {
            cueText.text = i.ToString();
            PlaySfx("fight_swing_light", .36f);
            yield return new WaitForSecondsRealtime(.42f);
        }

        cueText.text = "PARRY";
        yield return new WaitForSecondsRealtime(.24f);

        int previousDirection = -1;

        for (int i = 0; i < 3; i++)
        {
            round = i + 1;
            int direction = PickDirection(previousDirection);
            previousDirection = direction;

            float duration = .72f - i * .055f;
            yield return RunStrike(direction, duration, .34f, false);

            if (guard <= 0)
            {
                FinishGame(false, "GUARD BREAK");
                yield break;
            }

            yield return new WaitForSecondsRealtime(.28f);
        }

        round = 4;
        int fakeDirection = PickDirection(previousDirection);
        yield return RunFeint(fakeDirection);

        if (guard <= 0)
        {
            FinishGame(false, "FEINT BREAK");
            yield break;
        }

        int realDirection = OppositeDirection(fakeDirection);
        yield return RunStrike(realDirection, .54f, .12f, true);

        if (guard <= 0)
        {
            FinishGame(false, "GUARD BREAK");
            yield break;
        }

        yield return new WaitForSecondsRealtime(.24f);
        bool counterSuccess = false;
        yield return RunCounter(delegate { counterSuccess = true; });

        if (!counterSuccess)
        {
            FinishGame(false, "COUNTER MISS");
            yield break;
        }

        FinishGame(true, "CLEAR");
    }

    IEnumerator RunStrike(int direction, float duration, float telegraphSeconds, bool sudden)
    {
        inputConsumed = false;
        lastResult = ParryResult.None;
        strikeProgress = 0f;

        if (telegraphSeconds > 0f)
        {
            phase = Phase.Telegraph;
            cueText.text = DirectionCue(direction, sudden);
            statusText.text = sudden ? "진짜다!" : "ROUND " + round;
            statusText.color = Color.white;
            PlaySfx("fight_swing_light", sudden ? .72f : .48f);
            yield return new WaitForSecondsRealtime(telegraphSeconds);
        }

        phase = Phase.Strike;
        timingRing.gameObject.SetActive(true);
        attackMarker.gameObject.SetActive(true);
        attackTrail.gameObject.SetActive(true);

        Vector2 start = DirectionStart(direction);
        Vector2 target = new Vector2(0f, 120f);
        attackMarker.rectTransform.anchoredPosition = start;
        portrait.rectTransform.localScale = Vector3.one;

        float t = 0f;
        while (t < duration && !inputConsumed)
        {
            t += Time.unscaledDeltaTime;
            strikeProgress = Mathf.Clamp01(t / duration);

            float e = 1f - Mathf.Pow(1f - strikeProgress, 2.4f);
            Vector2 p = Vector2.Lerp(start, target, e);
            attackMarker.rectTransform.anchoredPosition = p;
            UpdateTrail(start, p);

            float ringPulse = Mathf.Lerp(1.24f, .82f, strikeProgress);
            timingRing.rectTransform.localScale = Vector3.one * ringPulse;
            float markerPulse = 1f + Mathf.Sin(Time.unscaledTime * 18f) * .10f;
            attackMarker.rectTransform.localScale = Vector3.one * markerPulse;

            float lunge = Mathf.Sin(strikeProgress * Mathf.PI) * .018f;
            portrait.rectTransform.localScale = Vector3.one * (1f + lunge);
            yield return null;
        }

        if (!inputConsumed)
        {
            strikeProgress = 1f;
            ResolveParry(ParryResult.Miss, "TOO LATE");
        }

        yield return new WaitForSecondsRealtime(lastResult == ParryResult.Miss ? .34f : .22f);
        ResetStrikeVisuals();
    }

    IEnumerator RunFeint(int direction)
    {
        phase = Phase.Telegraph;
        inputConsumed = false;
        cueText.text = DirectionCue(direction, false);
        statusText.color = Color.white;
        statusText.text = "레이나가 자세를 낮춘다";
        PlaySfx("fight_swing_light", .44f);
        yield return new WaitForSecondsRealtime(.30f);

        phase = Phase.Feint;
        inputConsumed = false;
        attackMarker.gameObject.SetActive(true);
        attackTrail.gameObject.SetActive(true);
        timingRing.gameObject.SetActive(false);

        Vector2 start = DirectionStart(direction);
        Vector2 fakeEnd = Vector2.Lerp(start, new Vector2(0f, 120f), .58f);

        float t = 0f;
        const float duration = .34f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / duration);
            float wave = Mathf.Sin(p * Mathf.PI);
            Vector2 pos = Vector2.Lerp(start, fakeEnd, wave);
            attackMarker.rectTransform.anchoredPosition = pos;
            UpdateTrail(start, pos);
            yield return null;
        }

        if (!inputConsumed)
        {
            score += 100;
            combo += 1;
            statusText.color = new Color(.52f, .80f, 1f, 1f);
            statusText.text = "FEINT READ  +100";
            bubbleText.text = "…안 속았군.";
            RefreshHud();
            Flash(new Color(.26f, .56f, 1f, .24f), .12f);
        }

        ResetStrikeVisuals();
        yield return new WaitForSecondsRealtime(.14f);
        cueText.text = "!";
    }

    IEnumerator RunCounter(Action success)
    {
        phase = Phase.Counter;
        inputConsumed = false;
        cueText.text = "COUNTER!";
        statusText.color = Color.white;
        statusText.text = "움직이는 표적을 직접 터치";
        bubbleText.text = "이번엔 네 차례다.";

        counterNorm = new Vector2(
            UnityEngine.Random.Range(.35f, .65f),
            UnityEngine.Random.Range(.35f, .68f)
        );
        counterVelocity = UnityEngine.Random.insideUnitCircle.normalized * .72f;
        if (counterVelocity.sqrMagnitude < .01f) counterVelocity = new Vector2(.72f, .35f);

        counterTarget.gameObject.SetActive(true);
        counterTarget.transform.SetAsLastSibling();
        closeButton.transform.SetAsLastSibling();
        PositionCounter();

        PlaySfx("fight_swing_heavy", .76f);

        float t = 0f;
        const float duration = 1.05f;

        while (t < duration && !inputConsumed)
        {
            t += Time.unscaledDeltaTime;
            counterNorm += counterVelocity * Time.unscaledDeltaTime;

            if (counterNorm.x < .22f || counterNorm.x > .78f)
            {
                counterVelocity.x *= -1f;
                counterNorm.x = Mathf.Clamp(counterNorm.x, .22f, .78f);
            }

            if (counterNorm.y < .24f || counterNorm.y > .76f)
            {
                counterVelocity.y *= -1f;
                counterNorm.y = Mathf.Clamp(counterNorm.y, .24f, .76f);
            }

            float pulse = Mathf.Lerp(.72f, 1.26f, (Mathf.Sin(Time.unscaledTime * 11f) + 1f) * .5f);
            counterTarget.rectTransform.localScale = Vector3.one * pulse;
            PositionCounter();
            yield return null;
        }

        counterTarget.gameObject.SetActive(false);
        counterTarget.rectTransform.localScale = Vector3.one;

        if (inputConsumed)
        {
            score += 500 + combo * 30;
            combo += 1;
            statusText.color = new Color(1f, .82f, .28f, 1f);
            statusText.text = "COUNTER!  +500";
            bubbleText.text = "…좋아. 제대로 들어왔다.";
            PlaySfx("fight_smash_heavy", 1f);
            Vibrate();
            Flash(new Color(1f, .78f, .22f, .34f), .18f);
            RefreshHud();
            if (success != null) success();
            yield return new WaitForSecondsRealtime(.34f);
        }
        else
        {
            missCount += 1;
            combo = 0;
            statusText.color = new Color(1f, .22f, .16f, 1f);
            statusText.text = "COUNTER MISS";
            bubbleText.text = PickMissLine();
            PlaySfx("fight_swing_heavy", .92f);
            Flash(new Color(.92f, .04f, .03f, .34f), .22f);
            RefreshHud();
            yield return new WaitForSecondsRealtime(.38f);
        }
    }

    void HandleParryPress()
    {
        if (!IsOpen || inputConsumed) return;

        if (phase == Phase.Feint)
        {
            inputConsumed = true;
            ResolveParry(ParryResult.Miss, "FEINT!");
            return;
        }

        if (phase != Phase.Strike) return;

        inputConsumed = true;

        if (strikeProgress >= .76f && strikeProgress <= .90f)
            ResolveParry(ParryResult.Perfect, "PERFECT");
        else if (strikeProgress >= .64f && strikeProgress <= .96f)
            ResolveParry(ParryResult.Good, "GOOD");
        else
            ResolveParry(ParryResult.Miss, strikeProgress < .64f ? "TOO EARLY" : "TOO LATE");
    }

    void HandleCounterPress()
    {
        if (!IsOpen || phase != Phase.Counter || inputConsumed) return;
        inputConsumed = true;
    }

    void ResolveParry(ParryResult result, string label)
    {
        lastResult = result;

        if (result == ParryResult.Perfect)
        {
            int gain = 300 + combo * 30;
            score += gain;
            combo += 1;
            perfectCount += 1;
            statusText.text = label + "  +" + gain;
            statusText.color = new Color(1f, .82f, .28f, 1f);
            bubbleText.text = PickSuccessLine();
            PlaySfx("fight_smash_heavy", 1f);
            Vibrate();
            Flash(new Color(1f, .80f, .24f, .32f), .16f);
        }
        else if (result == ParryResult.Good)
        {
            int gain = 160 + combo * 18;
            score += gain;
            combo += 1;
            goodCount += 1;
            statusText.text = label + "  +" + gain;
            statusText.color = new Color(.54f, .83f, 1f, 1f);
            bubbleText.text = PickSuccessLine();
            PlaySfx("fight_punch_medium", .88f);
            Flash(new Color(.32f, .58f, 1f, .22f), .12f);
        }
        else
        {
            guard = Mathf.Max(0, guard - 1);
            combo = 0;
            missCount += 1;
            statusText.text = label;
            statusText.color = new Color(1f, .22f, .16f, 1f);
            bubbleText.text = PickMissLine();
            PlaySfx("fight_swing_heavy", .84f);
            Vibrate();
            Flash(new Color(.92f, .03f, .025f, .34f), .20f);
        }

        RefreshHud();
    }

    void FinishGame(bool clear, string reason)
    {
        phase = Phase.Result;
        ResetTransientVisuals();

        int plays = PlayerPrefs.GetInt(PlaysKey, 0) + 1;
        PlayerPrefs.SetInt(PlaysKey, plays);

        if (clear)
        {
            int clears = PlayerPrefs.GetInt(ClearsKey, 0) + 1;
            PlayerPrefs.SetInt(ClearsKey, clears);
        }

        string grade = GradeFor(score, clear);
        bestScore = PlayerPrefs.GetInt(BestScoreKey, 0);

        if (clear && score > bestScore)
        {
            bestScore = score;
            PlayerPrefs.SetInt(BestScoreKey, bestScore);
            PlayerPrefs.SetString(BestGradeKey, grade);
        }

        PlayerPrefs.Save();

        cueText.text = clear ? grade : "FAILED";
        statusText.color = clear ? new Color(1f, .82f, .28f, 1f) : new Color(1f, .25f, .18f, 1f);
        statusText.text =
            reason + "  ·  SCORE " + score +
            "\nPERFECT " + perfectCount + "  GOOD " + goodCount + "  MISS " + missCount;

        bubbleText.text = clear
            ? "흥. 이번에는 인정하지."
            : "다시 와. 검을 보기 전에 움직이지 마.";

        recordText.text = "BEST  " + bestScore + "  ·  " + PlayerPrefs.GetString(BestGradeKey, "-");
        startButton.gameObject.SetActive(true);
        startButton.interactable = true;
        startButtonText.text = "다시 도전";
        gameRoutine = null;
    }

    void RefreshHud()
    {
        scoreText.text = "SCORE " + score;
        comboText.text = "COMBO " + combo;

        if (guard >= 3) guardText.text = "GUARD ◆◆◆";
        else if (guard == 2) guardText.text = "GUARD ◆◆◇";
        else if (guard == 1) guardText.text = "GUARD ◆◇◇";
        else guardText.text = "GUARD ◇◇◇";
    }

    void ResetTransientVisuals()
    {
        ResetStrikeVisuals();

        if (counterTarget != null)
        {
            counterTarget.gameObject.SetActive(false);
            counterTarget.rectTransform.localScale = Vector3.one;
        }

        if (hitFlash != null)
            hitFlash.color = new Color(hitFlash.color.r, hitFlash.color.g, hitFlash.color.b, 0f);

        if (portrait != null)
        {
            portrait.rectTransform.localScale = Vector3.one;
            portrait.rectTransform.anchoredPosition = Vector2.zero;
        }
    }

    void ResetStrikeVisuals()
    {
        if (attackMarker != null)
        {
            attackMarker.gameObject.SetActive(false);
            attackMarker.rectTransform.localScale = Vector3.one;
        }

        if (attackTrail != null)
            attackTrail.gameObject.SetActive(false);

        if (timingRing != null)
        {
            timingRing.gameObject.SetActive(false);
            timingRing.rectTransform.localScale = Vector3.one;
        }

        if (portrait != null)
            portrait.rectTransform.localScale = Vector3.one;
    }

    int PickDirection(int avoid)
    {
        int value = UnityEngine.Random.Range(0, 3);
        if (value == avoid)
            value = (value + UnityEngine.Random.Range(1, 3)) % 3;
        return value;
    }

    int OppositeDirection(int direction)
    {
        if (direction == 0) return 1;
        if (direction == 1) return 0;
        return UnityEngine.Random.value < .5f ? 0 : 1;
    }

    string DirectionCue(int direction, bool sudden)
    {
        string prefix = sudden ? "REAL  " : "";
        if (direction == 0) return prefix + "←";
        if (direction == 1) return prefix + "→";
        return prefix + "↓";
    }

    Vector2 DirectionStart(int direction)
    {
        float w = Mathf.Max(720f, gameArea.rect.width);
        float h = Mathf.Max(1050f, gameArea.rect.height);

        if (direction == 0) return new Vector2(-w * .47f, 120f);
        if (direction == 1) return new Vector2(w * .47f, 120f);
        return new Vector2(0f, h * .44f);
    }

    void UpdateTrail(Vector2 start, Vector2 current)
    {
        Vector2 delta = current - start;
        RectTransform r = attackTrail.rectTransform;
        r.anchoredPosition = (start + current) * .5f;
        r.sizeDelta = new Vector2(Mathf.Max(20f, delta.magnitude), 14f);
        r.localEulerAngles = new Vector3(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
    }

    void PositionCounter()
    {
        float w = Mathf.Max(720f, gameArea.rect.width);
        float h = Mathf.Max(1050f, gameArea.rect.height);
        counterTarget.rectTransform.anchoredPosition = new Vector2(
            (counterNorm.x - .5f) * w,
            (counterNorm.y - .5f) * h
        );
    }

    string PickSuccessLine()
    {
        string[] lines = XTapCharacterDialogue.Combat(ReinaCharacterId);
        if (lines != null && lines.Length > 0 && UnityEngine.Random.value < .65f)
            return lines[UnityEngine.Random.Range(0, lines.Length)];

        return fallbackSuccess[UnityEngine.Random.Range(0, fallbackSuccess.Length)];
    }

    string PickMissLine()
    {
        string[] lines = XTapCharacterDialogue.Dodge(ReinaCharacterId);
        if (lines != null && lines.Length > 0 && UnityEngine.Random.value < .78f)
            return lines[UnityEngine.Random.Range(0, lines.Length)];

        return fallbackMiss[UnityEngine.Random.Range(0, fallbackMiss.Length)];
    }

    string GradeFor(int value, bool clear)
    {
        if (!clear) return "FAIL";
        if (value >= 1750) return "SS";
        if (value >= 1500) return "S";
        if (value >= 1220) return "A";
        if (value >= 900) return "B";
        return "C";
    }

    void Flash(Color color, float duration)
    {
        if (flashRoutine != null)
            StopCoroutine(flashRoutine);

        flashRoutine = StartCoroutine(FlashRoutine(color, duration));
    }

    IEnumerator FlashRoutine(Color color, float duration)
    {
        hitFlash.transform.SetAsLastSibling();
        closeButton.transform.SetAsLastSibling();

        float half = Mathf.Max(.04f, duration * .5f);
        float t = 0f;

        while (t < half)
        {
            t += Time.unscaledDeltaTime;
            Color c = color;
            c.a = Mathf.Lerp(0f, color.a, Mathf.Clamp01(t / half));
            hitFlash.color = c;
            yield return null;
        }

        t = 0f;
        while (t < half)
        {
            t += Time.unscaledDeltaTime;
            Color c = color;
            c.a = Mathf.Lerp(color.a, 0f, Mathf.Clamp01(t / half));
            hitFlash.color = c;
            yield return null;
        }

        Color end = color;
        end.a = 0f;
        hitFlash.color = end;
        flashRoutine = null;
    }

    void Vibrate()
    {
        if (PlayerPrefs.GetInt(VibrationSettingKey, 1) != 1) return;
#if UNITY_ANDROID && !UNITY_EDITOR
        try { Handheld.Vibrate(); } catch { }
#endif
    }

    void PlaySfx(string id, float volume)
    {
        if (PlayerPrefs.GetInt(SfxSettingKey, 1) != 1 || audioSource == null) return;
        AudioClip clip = LoadSfx(id);
        if (clip != null)
            audioSource.PlayOneShot(clip, Mathf.Clamp01(volume));
    }

    AudioClip LoadSfx(string id)
    {
        AudioClip cached;
        if (sfxClips.TryGetValue(id, out cached)) return cached;

        try
        {
            TextAsset encoded = Resources.Load<TextAsset>("XTapCombatSfx/" + id);
            if (encoded == null || string.IsNullOrWhiteSpace(encoded.text)) return null;

            byte[] wav = Convert.FromBase64String(encoded.text.Trim());
            AudioClip clip = DecodePcm16Wav(wav, "reina_" + id);
            if (clip != null) sfxClips[id] = clip;
            return clip;
        }
        catch (Exception e)
        {
            Debug.LogWarning("레이나 패링 효과음 로드 실패: " + id + " / " + e.Message);
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

        if (format != 1 || channels < 1 || channels > 2 || sampleRate <= 0 || bits != 16)
            return null;

        int declaredBytes = BitConverter.ToInt32(wav, data + 4);
        int start = data + 8;
        int byteCount = Mathf.Min(declaredBytes, wav.Length - start);
        int sampleCount = byteCount / 2;
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
            samples[i] = BitConverter.ToInt16(wav, start + i * 2) / 32768f;

        int frames = sampleCount / channels;
        AudioClip clip = AudioClip.Create(clipName, frames, channels, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    int FindWavChunk(byte[] wav, string fourCC)
    {
        int i = 12;
        while (i + 8 <= wav.Length)
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

    Image MakeImage(Transform parent, string name, Color color, float x1, float y1, float x2, float y2)
    {
        Image image = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
        image.transform.SetParent(parent, false);
        image.color = color;
        Anchor(image.rectTransform, x1, y1, x2, y2);
        return image;
    }

    Text MakeText(Transform parent, string value, int fontSize, TextAnchor alignment, bool bold)
    {
        Text t = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text)).GetComponent<Text>();
        t.transform.SetParent(parent, false);
        t.font = font;
        t.text = value;
        t.fontSize = Mathf.RoundToInt(fontSize * UiFontScale);
        t.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
        t.alignment = alignment;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Truncate;
        t.raycastTarget = false;

        Outline outline = t.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, .82f);
        outline.effectDistance = new Vector2(2f, -2f);
        return t;
    }

    Button MakeButton(Transform parent, string label, int fontSize, Color color)
    {
        GameObject go = new GameObject(label + "Button", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);

        Image bg = go.GetComponent<Image>();
        bg.color = color;

        Text t = MakeText(go.transform, label, fontSize, TextAnchor.MiddleCenter, true);
        t.color = new Color(.96f, .94f, .90f, 1f);
        Anchor(t.rectTransform, .03f, .04f, .97f, .96f);

        Button b = go.GetComponent<Button>();
        b.targetGraphic = bg;

        ColorBlock cb = b.colors;
        cb.normalColor = Color.white;
        cb.highlightedColor = new Color(1f, .90f, .78f, 1f);
        cb.pressedColor = new Color(.76f, .62f, .54f, 1f);
        cb.selectedColor = Color.white;
        cb.disabledColor = new Color(.42f, .42f, .42f, .65f);
        b.colors = cb;
        return b;
    }

    Sprite CreateRingSprite(int size, int thickness)
    {
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = "ReinaParryRing";
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

    static void SetSize(RectTransform r, float width, float height)
    {
        r.anchorMin = r.anchorMax = new Vector2(.5f, .5f);
        r.pivot = new Vector2(.5f, .5f);
        r.sizeDelta = new Vector2(width, height);
    }

    static void Anchor(RectTransform r, float x1, float y1, float x2, float y2)
    {
        r.anchorMin = new Vector2(x1, y1);
        r.anchorMax = new Vector2(x2, y2);
        r.offsetMin = Vector2.zero;
        r.offsetMax = Vector2.zero;
    }
}
