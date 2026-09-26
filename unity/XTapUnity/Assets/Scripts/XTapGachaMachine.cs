using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public sealed class XTapGachaMachine : MonoBehaviour
{
    // 11.40: APK runtime safety fallback. Some Android builds can fail to resolve
    // dynamically-added .bytes Resources even when the files passed editor validation.
    // Keep the approved machine art embedded as a last-resort copy so the UI can
    // never collapse to the empty black/gold fallback frame again.
    static readonly string EmbeddedBlockGearMachineJpegBase64 = "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";
    static readonly string EmbeddedBlockGearWheelJpegBase64 = "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";
    const float UiFontScale = 3.84f;
    public bool IsOpen { get; private set; }

    RectTransform host;
    Font font;
    Action onCollected;
    XTapInventory bag;

    GameObject overlay;
    RectTransform machine;
    RectTransform wheel;
    Image wheelCore;
    RectTransform chute;
    RectTransform rewardRoot;
    Text title;
    Text correctionText;
    Text ticketStatusText;
    Text nameText;
    Text statsText;
    Text hintText;

    Texture2D machineSkinTexture;
    Texture2D wheelSkinTexture;
    Sprite machineSkin;
    Sprite wheelSkin;

    bool readyToCollect;
    Coroutine playRoutine;
    Outcome pendingOutcome;
    int activeCharacterId = 1;
    int activeProgressStep;

    readonly int[] corrections = {0, 10, 20, 30, 40, 50, -50, -40, -30, -20, -10};
    readonly int[] allowedSizes = {1, 2, 3, 4, 5, 6, 9, 12};
    readonly int[] baseBudgets = {10, 22, 35, 50, 66, 84, 135, 190};

    readonly string[] nouns =
    {
        "단검", "장검", "도끼", "철퇴", "창", "활",
        "방패", "갑옷", "투구", "장갑", "장화", "반지", "목걸이"
    };

    sealed class DescriptorDef
    {
        public readonly string id;
        public readonly string word;

        public DescriptorDef(string descriptorId, string descriptorWord)
        {
            id = descriptorId;
            word = descriptorWord;
        }
    }

    readonly DescriptorDef[] descriptorDefs =
    {
        new DescriptorDef("splendid", "화려한"),
        new DescriptorDef("solid", "단단한"),
        new DescriptorDef("fine", "멋진"),
        new DescriptorDef("sharp", "날카로운"),
        new DescriptorDef("sturdy", "견고한"),
        new DescriptorDef("vital", "생명력 넘치는"),
        new DescriptorDef("balanced", "균형 잡힌"),
        new DescriptorDef("precise", "정교한"),
        new DescriptorDef("guardian", "수호의"),
        new DescriptorDef("fierce", "맹렬한"),
        new DescriptorDef("unyielding", "불굴의"),
        new DescriptorDef("heavy", "묵직한"),
        new DescriptorDef("shining", "빛나는"),
        new DescriptorDef("dark", "어둠의"),
        new DescriptorDef("frosted", "서리 맺힌"),
        new DescriptorDef("burning", "불타는"),
        new DescriptorDef("storm", "폭풍의"),
        new DescriptorDef("thunder", "천둥의"),
        new DescriptorDef("swift", "신속한"),
        new DescriptorDef("silent", "고요한"),
        new DescriptorDef("forgotten", "잊힌"),
        new DescriptorDef("ancient", "고대의"),
        new DescriptorDef("cursed", "저주받은"),
        new DescriptorDef("blessed", "축복받은"),
        new DescriptorDef("bloodstained", "피로 물든"),
        new DescriptorDef("moonlit", "달빛의"),
        new DescriptorDef("solar", "태양의"),
        new DescriptorDef("starlit", "별빛의"),
        new DescriptorDef("abyssal", "심연의"),
        new DescriptorDef("golden", "황금빛"),
        new DescriptorDef("silver", "은빛"),
        new DescriptorDef("bronze", "청동의"),
        new DescriptorDef("steel", "강철의"),
        new DescriptorDef("obsidian", "흑요석의"),
        new DescriptorDef("crystal", "수정의"),
        new DescriptorDef("runed", "룬 각인된"),
        new DescriptorDef("enchanted", "마력 깃든"),
        new DescriptorDef("soul", "영혼의"),
        new DescriptorDef("valiant", "용맹한"),
        new DescriptorDef("cruel", "잔혹한"),
        new DescriptorDef("elegant", "우아한"),
        new DescriptorDef("rough", "거친"),
        new DescriptorDef("weighty", "무거운"),
        new DescriptorDef("light", "가벼운"),
        new DescriptorDef("agile", "민첩한"),
        new DescriptorDef("tenacious", "집요한"),
        new DescriptorDef("persistent", "끈질긴"),
        new DescriptorDef("lethal", "치명적인"),
        new DescriptorDef("tranquil", "잔잔한"),
        new DescriptorDef("unstable", "불안정한"),
        new DescriptorDef("stable", "안정된"),
        new DescriptorDef("explosive", "폭발적인"),
        new DescriptorDef("frozen", "얼어붙은"),
        new DescriptorDef("heated", "뜨거운"),
        new DescriptorDef("coldhearted", "냉혹한"),
        new DescriptorDef("madness", "광기의"),
        new DescriptorDef("holy", "성스러운"),
        new DescriptorDef("evil", "사악한"),
        new DescriptorDef("purewhite", "순백의"),
        new DescriptorDef("pitchblack", "칠흑의"),
        new DescriptorDef("crimson", "핏빛"),
        new DescriptorDef("clear", "청명한"),
        new DescriptorDef("excellent", "탁월한"),
        new DescriptorDef("strange", "기묘한"),
        new DescriptorDef("mystic", "신비한"),
        new DescriptorDef("radiant", "찬란한"),
        new DescriptorDef("faint", "흐릿한"),
        new DescriptorDef("sparkling", "반짝이는"),
        new DescriptorDef("silence", "침묵의"),
        new DescriptorDef("roaring", "포효하는"),
        new DescriptorDef("hunter", "사냥꾼의"),
        new DescriptorDef("warrior", "전사의"),
        new DescriptorDef("knightly", "기사단의"),
        new DescriptorDef("royal", "왕가의"),
        new DescriptorDef("imperial", "제국의"),
        new DescriptorDef("wanderer", "방랑자의"),
        new DescriptorDef("assassin", "암살자의"),
        new DescriptorDef("smith", "대장장이의"),
        new DescriptorDef("alchemist", "연금술사의"),
        new DescriptorDef("mage", "마도사의"),
        new DescriptorDef("paladin", "성기사의"),
        new DescriptorDef("demonic", "악마의"),
        new DescriptorDef("angelic", "천사의"),
        new DescriptorDef("dragon", "용의"),
        new DescriptorDef("wolf", "늑대의"),
        new DescriptorDef("raven", "까마귀의"),
        new DescriptorDef("lion", "사자의"),
        new DescriptorDef("serpent", "뱀의"),
        new DescriptorDef("hawk", "매의"),
        new DescriptorDef("giant", "거인의"),
        new DescriptorDef("fairy", "요정의"),
        new DescriptorDef("ghost", "유령의"),
        new DescriptorDef("undead", "망자의"),
        new DescriptorDef("immortal", "불멸의"),
        new DescriptorDef("ruin", "파멸의"),
        new DescriptorDef("salvation", "구원의"),
        new DescriptorDef("vengeance", "복수의"),
        new DescriptorDef("destiny", "운명의"),
        new DescriptorDef("miracle", "기적의"),
        new DescriptorDef("apocalypse", "종말의")
    };

    public void Initialize(RectTransform parent, Font uiFont, Action collected, XTapInventory inventory)
    {
        host = parent;
        font = uiFont;
        onCollected = collected;
        bag = inventory;
        LoadVisualAssets();
        BuildUi();
        overlay.SetActive(false);
    }

    void Update()
    {
        if (!IsOpen || !readyToCollect) return;

        bool pressed = false;
        if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began) pressed = true;
#if UNITY_EDITOR
        if (Input.GetMouseButtonDown(0)) pressed = true;
#endif
        if (!pressed) return;

        CloseAndCollect();
    }

    public void PlayReward(int characterId, int progressStep)
    {
        if (host == null || overlay == null) return;
        int normalizedCharacter = Mathf.Max(1, characterId);
        activeCharacterId = ((normalizedCharacter - 1) % 10) + 1;
        activeProgressStep = Mathf.Max(0, progressStep);

        if (playRoutine != null) StopCoroutine(playRoutine);
        playRoutine = StartCoroutine(PlayRoutine());
    }

    public void PlayReward(int characterId)
    {
        PlayReward(characterId, 0);
    }

    public void PlayReward()
    {
        PlayReward(1, 0);
    }

    public void PlayTicketRewards(int ticketCount, int progressStep, Action onTicketConsumed)
    {
        if (host == null || overlay == null || ticketCount <= 0) return;

        activeProgressStep = Mathf.Max(0, progressStep);
        int highestFloor = activeProgressStep / 10 + 1;
        activeCharacterId = ((highestFloor - 1) % 10) + 1;

        if (playRoutine != null) StopCoroutine(playRoutine);
        playRoutine = StartCoroutine(PlayTicketRoutine(ticketCount, onTicketConsumed));
    }

    IEnumerator PlayTicketRoutine(int ticketCount, Action onTicketConsumed)
    {
        IsOpen = true;
        readyToCollect = false;
        pendingOutcome = null;
        overlay.SetActive(true);
        overlay.transform.SetAsLastSibling();

        ClearReward();
        correctionText.text = "";
        if (ticketStatusText != null)
        {
            ticketStatusText.text = "";
            ticketStatusText.gameObject.SetActive(false);
        }
        nameText.text = "";
        statsText.text = "";

        int highestFloor = activeProgressStep / 10 + 1;
        title.text = "";
        if (ticketStatusText != null)
        {
            ticketStatusText.gameObject.SetActive(true);
            ticketStatusText.text = "최고 " + highestFloor + "층 블록  ·  무료 티켓 " + ticketCount + "장";
        }

        machine.localScale = Vector3.one * .90f;
        machine.anchoredPosition = new Vector2(0f, -40f);

        float intro = 0f;
        while (intro < .22f)
        {
            intro += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(intro / .22f);
            float e = 1f - Mathf.Pow(1f - p, 3f);
            machine.localScale = Vector3.one * Mathf.Lerp(.90f, 1f, e);
            machine.anchoredPosition = new Vector2(0f, Mathf.Lerp(-40f, 0f, e));
            yield return null;
        }

        for (int spin = 0; spin < ticketCount; spin++)
        {
            int remaining = ticketCount - spin;
            ClearReward();
            correctionText.text = "";
            nameText.text = "";
            statsText.text = "";
            hintText.text = "무료 티켓 자동 가챠  ·  남은 " + remaining + "장";

            if (ticketStatusText != null)
                ticketStatusText.text =
                    "최고 " + highestFloor + "층 블록  ·  " + (spin + 1) + " / " + ticketCount;

            int correctionIndex = UnityEngine.Random.Range(0, corrections.Length);
            int correction = corrections[correctionIndex];
            float segment = 360f / corrections.Length;

            float startAngle = NormalizeSignedAngle(wheel.localEulerAngles.z);
            float targetAngle = correctionIndex * segment;
            float clockwiseDelta = Mathf.Repeat(startAngle - targetAngle, 360f);
            float totalSpin = 360f * UnityEngine.Random.Range(4, 7) + clockwiseDelta;

            float duration = spin == 0 ? 1.55f : 1.20f;
            float t = 0f;

            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float p = Mathf.Clamp01(t / duration);
                float e = 1f - Mathf.Pow(1f - p, 4f);
                float angle = startAngle - totalSpin * e;
                wheel.localRotation = Quaternion.Euler(0f, 0f, angle);

                float shake = Mathf.Sin(Time.unscaledTime * 72f) * (1f - p) * 5f;
                machine.anchoredPosition = new Vector2(shake, 0f);

                float pulse = 1f + Mathf.Sin(Time.unscaledTime * 17f) * .05f;
                wheelCore.rectTransform.localScale = Vector3.one * pulse;
                yield return null;
            }

            wheel.localRotation = Quaternion.Euler(0f, 0f, targetAngle);
            machine.anchoredPosition = Vector2.zero;
            wheelCore.rectTransform.localScale = Vector3.one;

            yield return ChuteKick();

            // Hourly tickets always create a block. They never roll capture,
            // even when the visible correction lands on 0%.
            pendingOutcome = new Outcome();
            pendingOutcome.correction = correction;
            pendingOutcome.block = RollBlock(correction, false);

            ShowOutcome(pendingOutcome);
            title.text = "";
            if (ticketStatusText != null)
                ticketStatusText.text =
                    "최고 " + highestFloor + "층 블록  ·  " + (spin + 1) + " / " + ticketCount;

            yield return DropReward();

            if (bag == null || !bag.AddToGround(pendingOutcome.block))
            {
                hintText.text = "바닥 저장 실패 · 이 티켓은 차감되지 않습니다";
                pendingOutcome = null;
                yield return new WaitForSecondsRealtime(1.2f);
                break;
            }

            pendingOutcome = null;
            if (onTicketConsumed != null)
                onTicketConsumed();

            hintText.text = "바닥에 적재 완료";
            yield return new WaitForSecondsRealtime(.55f);
        }

        ClearReward();
        if (ticketStatusText != null)
        {
            ticketStatusText.text = "";
            ticketStatusText.gameObject.SetActive(false);
        }
        readyToCollect = false;
        IsOpen = false;
        pendingOutcome = null;
        overlay.SetActive(false);
        playRoutine = null;

        if (onCollected != null)
            onCollected();
    }

    IEnumerator PlayRoutine()
    {
        IsOpen = true;
        readyToCollect = false;
        pendingOutcome = null;
        overlay.SetActive(true);
        overlay.transform.SetAsLastSibling();

        ClearReward();
        correctionText.text = "";
        if (ticketStatusText != null)
        {
            ticketStatusText.text = "";
            ticketStatusText.gameObject.SetActive(false);
        }
        nameText.text = "";
        statsText.text = "";
        hintText.text = "보정 룰렛 회전 중";
        title.text = "";

        machine.localScale = Vector3.one * .90f;
        machine.anchoredPosition = new Vector2(0f, -40f);

        float intro = 0f;
        while (intro < .22f)
        {
            intro += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(intro / .22f);
            float e = 1f - Mathf.Pow(1f - p, 3f);
            machine.localScale = Vector3.one * Mathf.Lerp(.90f, 1f, e);
            machine.anchoredPosition = new Vector2(0f, Mathf.Lerp(-40f, 0f, e));
            yield return null;
        }

        int correctionIndex = UnityEngine.Random.Range(0, corrections.Length);
        int correction = corrections[correctionIndex];

        float segment = 360f / corrections.Length;

        // Slot 0 is authored at the top pointer, and slot indices increase clockwise.
        // A positive Unity Z rotation brings a clockwise-authored slot back to the top pointer.
        // Always spin from the current wheel orientation to the ABSOLUTE target slot.
        // This prevents visual selection from drifting away from the actual correction
        // after the first reward spin.
        float startAngle = NormalizeSignedAngle(wheel.localEulerAngles.z);
        float targetAngle = correctionIndex * segment;
        float clockwiseDelta = Mathf.Repeat(startAngle - targetAngle, 360f);
        float totalSpin = 360f * UnityEngine.Random.Range(4, 7) + clockwiseDelta;

        float duration = 1.85f;
        float t = 0f;

        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / duration);
            float e = 1f - Mathf.Pow(1f - p, 4f);
            float angle = startAngle - totalSpin * e;
            wheel.localRotation = Quaternion.Euler(0f, 0f, angle);

            float shake = Mathf.Sin(Time.unscaledTime * 72f) * (1f - p) * 5f;
            machine.anchoredPosition = new Vector2(shake, 0f);

            float pulse = 1f + Mathf.Sin(Time.unscaledTime * 17f) * .05f;
            wheelCore.rectTransform.localScale = Vector3.one * pulse;
            yield return null;
        }

        // Snap to the exact selected slot so the pointer and applied value
        // can never disagree because of accumulated rotation or frame rounding.
        wheel.localRotation = Quaternion.Euler(0f, 0f, targetAngle);
        machine.anchoredPosition = Vector2.zero;
        wheelCore.rectTransform.localScale = Vector3.one;

        yield return ChuteKick();

        // The exact value shown under the pointer is the value applied to the block.
        pendingOutcome = RollOutcome(corrections[correctionIndex]);
        ShowOutcome(pendingOutcome);
        yield return DropReward();

        hintText.text = pendingOutcome.block != null
            ? "화면을 터치해서 보상을 바닥에 내려놓기"
            : "화면을 터치해서 계속";
        readyToCollect = true;
    }

    Outcome RollOutcome(int correction)
    {
        Outcome outcome = new Outcome();
        outcome.correction = correction;

        if (correction == 0)
        {
            bool captured = IsCharacterCaptured(activeCharacterId);
            if (!captured)
            {
                outcome.captureAttempt = true;
                outcome.captureSucceeded = UnityEngine.Random.value < 1f; // TEST: 100% capture for uncaptured character

                if (outcome.captureSucceeded)
                {
                    PlayerPrefs.SetInt(CaptureKey(activeCharacterId), 1);
                    PlayerPrefs.Save();
                }
                return outcome;
            }

            outcome.block = RollBlock(0, true);
            return outcome;
        }

        outcome.block = RollBlock(correction, false);
        return outcome;
    }

    XTapGearBlockData RollBlock(int correction, bool exclusive)
    {
        int sizeIndex = UnityEngine.Random.Range(0, allowedSizes.Length);
        int cells = allowedSizes[sizeIndex];
        int budget = baseBudgets[sizeIndex];

        // Block power grows by +5% for each tower progress step.
        // 1-1 = 100%, 1-2 = 105%, ... 1-10 = 145%, 2-1 = 150%.
        double blockGrowthMultiplier = 1d + activeProgressStep * .05d;

        // Roulette values are correction percentages, not raw stat values.
        // Example: +20% multiplies the progressed block budget by 1.20.
        double correctionMultiplier = 1d + correction / 100d;
        double progressedBudget = budget * blockGrowthMultiplier;
        int total = Mathf.Max(3, Mathf.RoundToInt((float)(progressedBudget * correctionMultiplier)));

        float a = UnityEngine.Random.Range(.15f, .70f);
        float d = UnityEngine.Random.Range(.10f, .65f);
        float h = UnityEngine.Random.Range(.10f, .65f);
        float sum = a + d + h;

        int attack = Mathf.Max(1, Mathf.RoundToInt(total * a / sum));
        int defense = Mathf.Max(1, Mathf.RoundToInt(total * d / sum));
        int hp = Mathf.Max(1, total - attack - defense);

        XTapGearBlockData r = new XTapGearBlockData();
        r.id = Guid.NewGuid().ToString("N");
        r.cellCount = cells;
        r.attack = attack;
        r.defense = defense;
        r.hp = hp;
        r.correction = correction;
        r.exclusive = exclusive;
        r.characterId = activeCharacterId;

        string noun = nouns[UnityEngine.Random.Range(0, nouns.Length)];
        ApplySequentialDescriptors(r, noun);

        if (exclusive)
            r.displayName = "캐릭터 " + activeCharacterId + " 전용 " + r.displayName;

        r.shape = EncodeShape(ShapeFor(cells));
        return r;
    }

    void ApplySequentialDescriptors(XTapGearBlockData item, string noun)
    {
        List<DescriptorDef> selected = new List<DescriptorDef>();

        // Each next roll only exists if the previous 1% roll succeeded.
        // Exact probabilities:
        // 0 descriptors = 99%
        // exactly 1 = 0.99%
        // exactly 2 = 0.0099%
        // exactly 3 = 0.0001%
        for (int slot = 0; slot < 3; slot++)
        {
            if (UnityEngine.Random.value >= .01f)
                break;

            DescriptorDef pick = null;
            for (int guard = 0; guard < 32 && pick == null; guard++)
            {
                DescriptorDef candidate = descriptorDefs[UnityEngine.Random.Range(0, descriptorDefs.Length)];
                bool duplicate = false;
                for (int i = 0; i < selected.Count; i++)
                {
                    if (selected[i].id == candidate.id)
                    {
                        duplicate = true;
                        break;
                    }
                }

                if (!duplicate)
                    pick = candidate;
            }

            if (pick == null)
                break;

            selected.Add(pick);
        }

        item.descriptorCount = selected.Count;
        item.descriptorFormulaVersion = 2;

        if (selected.Count == 0)
        {
            item.descriptorIds = "";
            item.descriptorWords = "";
            item.descriptorEffectText = "";
            item.displayName = noun;
            return;
        }

        List<string> ids = new List<string>();
        List<string> words = new List<string>();

        for (int i = 0; i < selected.Count; i++)
        {
            DescriptorDef descriptor = selected[i];
            ids.Add(descriptor.id);
            words.Add(descriptor.word);
        }

        int bonusPercent = selected.Count == 1 ? 25 : (selected.Count == 2 ? 50 : 100);
        item.descriptorIds = string.Join(",", ids.ToArray());
        item.descriptorWords = string.Join("|", words.ToArray());
        item.descriptorEffectText =
            "수식어 " + selected.Count + "개 · 최종 공/방/체 +" + bonusPercent + "%";
        item.displayName = string.Join(" ", words.ToArray()) + " " + noun;
    }

    void ShowOutcome(Outcome outcome)
    {
        ClearReward();

        int appliedCorrection = outcome.block != null
            ? outcome.block.correction
            : outcome.correction;
        string corr = appliedCorrection > 0
            ? "+" + appliedCorrection + "%"
            : appliedCorrection + "%";
        correctionText.text = "보정  " + corr;

        if (outcome.captureAttempt)
        {
            title.text = outcome.captureSucceeded ? "CAPTURE SUCCESS" : "CAPTURE BALL";
            nameText.text = outcome.captureSucceeded ? "포획 성공!" : "포획 실패";
            statsText.text = outcome.captureSucceeded
                ? "캐릭터 " + activeCharacterId + " 포획 완료"
                : "포획 확률 1%";

            if (outcome.captureSucceeded)
            {
                Sprite cap = XTapOriginalApkAssets.Instance != null
                    ? XTapOriginalApkAssets.Instance.GetSprite("assets/f" + activeCharacterId + "_cap.jpg")
                    : null;

                if (cap != null)
                {
                    DrawCaptureSuccessImage(cap);
                    XTapCodex.MarkImageDiscovered(activeCharacterId, "cap", bag);
                }
                else
                {
                    DrawCaptureBall(true);
                }
            }
            else
            {
                DrawCaptureBall(false);
            }
            return;
        }

        if (outcome.block == null) return;

        XTapGearBlockData r = outcome.block;
        title.text = r.exclusive ? "EXCLUSIVE BLOCK" : "";
        nameText.text = XTapGearNameColor.Rich(r) + "  ·  " + r.cellCount + "칸";
        double descriptorMultiplier = DescriptorFinalMultiplier(r.descriptorCount);
        statsText.text =
            XTapStatFormat.BlockTriplet(
                r.attack * descriptorMultiplier,
                r.defense * descriptorMultiplier,
                r.hp * descriptorMultiplier,
                "     ") +
            (string.IsNullOrEmpty(r.descriptorEffectText) ? "" : "\n" + r.descriptorEffectText);
        DrawBlock(r);
    }

    static double DescriptorFinalMultiplier(int count)
    {
        if (count >= 3) return 2d;
        if (count == 2) return 1.5d;
        if (count == 1) return 1.25d;
        return 1d;
    }

    void DrawCaptureSuccessImage(Sprite sprite)
    {
        GameObject go = new GameObject("CaptureSuccessArt", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(rewardRoot, false);
        Image image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.color = Color.white;
        image.preserveAspect = true;
        image.raycastTarget = false;

        RectTransform rt = image.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(.5f, .5f);
        rt.sizeDelta = new Vector2(420f, 560f);
        rt.anchoredPosition = new Vector2(0f, 70f);
    }

    void DrawCaptureBall(bool success)
    {
        GameObject outer = new GameObject("CaptureBall", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        outer.transform.SetParent(rewardRoot, false);
        Image img = outer.GetComponent<Image>();
        img.color = success ? new Color(.96f, .72f, .18f, 1f) : new Color(.36f, .38f, .44f, 1f);
        img.raycastTarget = false;

        RectTransform rt = img.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(.5f, .5f);
        rt.sizeDelta = new Vector2(150f, 150f);
        rt.anchoredPosition = Vector2.zero;

        GameObject core = new GameObject("Core", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        core.transform.SetParent(outer.transform, false);
        Image ci = core.GetComponent<Image>();
        ci.color = new Color(.08f, .09f, .11f, 1f);
        ci.raycastTarget = false;
        RectTransform cr = ci.rectTransform;
        cr.anchorMin = cr.anchorMax = new Vector2(.5f, .5f);
        cr.sizeDelta = new Vector2(62f, 62f);

        Text x = MakeText(core.transform, "X", 42, TextAnchor.MiddleCenter, true);
        x.color = new Color(.96f, .78f, .22f, 1f);
        Anchor(x.rectTransform, 0f, 0f, 1f, 1f);
    }

    void DrawBlock(XTapGearBlockData r)
    {
        List<Vector2Int> cells = DecodeShape(r.shape);
        float cell = 44f;
        int minX = 999, maxX = -999, minY = 999, maxY = -999;
        for (int i = 0; i < cells.Count; i++)
        {
            minX = Mathf.Min(minX, cells[i].x);
            maxX = Mathf.Max(maxX, cells[i].x);
            minY = Mathf.Min(minY, cells[i].y);
            maxY = Mathf.Max(maxY, cells[i].y);
        }

        float width = (maxX - minX + 1) * cell;
        float height = (maxY - minY + 1) * cell;
        Color blockColor = r.exclusive
            ? new Color(.66f, .22f, .82f, 1f)
            : (r.correction > 0 ? new Color(.94f, .65f, .14f, 1f) : new Color(.40f, .47f, .57f, 1f));

        for (int i = 0; i < cells.Count; i++)
        {
            Vector2Int p = cells[i];
            GameObject outer = new GameObject("BlockCell", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            outer.transform.SetParent(rewardRoot, false);
            Image img = outer.GetComponent<Image>();
            img.color = blockColor;
            img.raycastTarget = false;

            RectTransform rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(.5f, .5f);
            rt.sizeDelta = new Vector2(cell - 3f, cell - 3f);
            rt.anchoredPosition = new Vector2(
                (p.x - minX + .5f) * cell - width * .5f,
                height * .5f - (p.y - minY + .5f) * cell
            );

            GameObject inner = new GameObject("Inset", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            inner.transform.SetParent(outer.transform, false);
            Image ii = inner.GetComponent<Image>();
            ii.color = new Color(.10f, .105f, .12f, .92f);
            ii.raycastTarget = false;
            Anchor(ii.rectTransform, .13f, .13f, .87f, .87f);
        }
    }

    void CloseAndCollect()
    {
        // Original inventory flow: gacha rewards first land on the floor.
        // A full 8x3 equipment grid must never destroy or block a reward.
        if (pendingOutcome != null && pendingOutcome.block != null)
        {
            if (bag == null || !bag.AddToGround(pendingOutcome.block))
            {
                hintText.text = "보상을 바닥에 저장하지 못했습니다.";
                readyToCollect = true;
                return;
            }
        }

        readyToCollect = false;
        IsOpen = false;
        pendingOutcome = null;
        overlay.SetActive(false);
        if (onCollected != null) onCollected();
    }

    bool IsCharacterCaptured(int characterId)
    {
        return PlayerPrefs.GetInt(CaptureKey(characterId), 0) == 1;
    }

    string CaptureKey(int characterId)
    {
        return "xtap_captured_char_" + characterId;
    }

    IEnumerator ChuteKick()
    {
        Vector2 basePos = chute.anchoredPosition;
        float t = 0f;
        while (t < .16f)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / .16f);
            chute.anchoredPosition = basePos + Vector2.down * Mathf.Sin(p * Mathf.PI) * 18f;
            yield return null;
        }
        chute.anchoredPosition = basePos;
    }

    IEnumerator DropReward()
    {
        Vector2 end = rewardRoot.anchoredPosition;
        Vector2 start = end + Vector2.up * 160f;
        rewardRoot.anchoredPosition = start;
        bool capturePop = pendingOutcome != null &&
                          pendingOutcome.captureAttempt &&
                          pendingOutcome.captureSucceeded;
        rewardRoot.localScale = Vector3.one * (capturePop ? .28f : .55f);

        float t = 0f;
        while (t < (capturePop ? .58f : .42f))
        {
            t += Time.unscaledDeltaTime;
            float duration = capturePop ? .58f : .42f;
            float p = Mathf.Clamp01(t / duration);
            float e = 1f - Mathf.Pow(1f - p, 3f);
            float bounce = Mathf.Sin(p * Mathf.PI * 2f) * (1f - p) * (capturePop ? 28f : 18f);
            rewardRoot.anchoredPosition = Vector2.Lerp(start, end, e) + Vector2.up * bounce;

            if (capturePop)
            {
                float scale = p < .72f
                    ? Mathf.Lerp(.28f, 1.16f, p / .72f)
                    : Mathf.Lerp(1.16f, 1f, (p - .72f) / .28f);
                rewardRoot.localScale = Vector3.one * scale;
            }
            else
            {
                rewardRoot.localScale = Vector3.one * Mathf.Lerp(.55f, 1f, e);
            }
            yield return null;
        }

        rewardRoot.anchoredPosition = end;
        rewardRoot.localScale = Vector3.one;
    }

    List<Vector2Int> ShapeFor(int count)
    {
        List<List<Vector2Int>> options = new List<List<Vector2Int>>();

        if (count == 1)
        {
            options.Add(S(new int[]{0,0}));
        }
        else if (count == 2)
        {
            options.Add(S(new int[]{0,0, 1,0}));
        }
        else if (count == 3)
        {
            options.Add(S(new int[]{0,0, 1,0, 2,0}));
            options.Add(S(new int[]{0,0, 0,1, 1,0}));
        }
        else if (count == 4)
        {
            options.Add(S(new int[]{0,0, 1,0, 2,0, 3,0}));
            options.Add(S(new int[]{0,0, 1,0, 0,1, 1,1}));
            options.Add(S(new int[]{0,0, 1,0, 2,0, 1,1}));
            options.Add(S(new int[]{0,0, 0,1, 0,2, 1,0}));
            options.Add(S(new int[]{0,0, 1,0, 1,1, 2,1}));
        }
        else if (count == 5)
        {
            options.Add(S(new int[]{0,0, 1,0, 2,0, 3,0, 4,0}));
            options.Add(S(new int[]{0,0, 0,1, 0,2, 0,3, 1,0}));
            options.Add(S(new int[]{0,0, 1,0, 2,0, 1,1, 1,2}));
            options.Add(S(new int[]{0,0, 2,0, 0,1, 1,1, 2,1}));
            options.Add(S(new int[]{0,0, 0,1, 0,2, 1,2, 2,2}));
            options.Add(S(new int[]{0,0, 1,0, 0,1, 1,1, 0,2}));
            options.Add(S(new int[]{0,0, 1,0, 1,1, 2,1, 2,2}));
            options.Add(S(new int[]{1,0, 0,1, 1,1, 2,1, 1,2}));
        }
        else if (count == 6)
        {
            options.Add(S(new int[]{0,0, 1,0, 2,0, 0,1, 1,1, 2,1}));
            options.Add(S(new int[]{0,0, 1,0, 2,0, 3,0, 4,0, 5,0}));
            options.Add(S(new int[]{0,0, 0,1, 0,2, 0,3, 1,0, 2,0}));
        }
        else if (count == 9)
        {
            options.Add(S(new int[]{0,0,1,0,2,0, 0,1,1,1,2,1, 0,2,1,2,2,2}));
            options.Add(S(new int[]{0,0,1,0,2,0,3,0,4,0, 0,1,1,1,2,1,3,1}));
        }
        else
        {
            options.Add(S(new int[]{0,0,1,0,2,0,3,0, 0,1,1,1,2,1,3,1, 0,2,1,2,2,2,3,2}));
            options.Add(S(new int[]{0,0,1,0,2,0,3,0,4,0,5,0, 0,1,1,1,2,1,3,1,4,1,5,1}));
        }

        return options[UnityEngine.Random.Range(0, options.Count)];
    }

    List<Vector2Int> S(int[] xy)
    {
        List<Vector2Int> list = new List<Vector2Int>();
        for (int i = 0; i + 1 < xy.Length; i += 2)
            list.Add(new Vector2Int(xy[i], xy[i + 1]));
        return list;
    }

    string EncodeShape(List<Vector2Int> cells)
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        for (int i = 0; i < cells.Count; i++)
        {
            if (i > 0) sb.Append(';');
            sb.Append(cells[i].x).Append(',').Append(cells[i].y);
        }
        return sb.ToString();
    }

    List<Vector2Int> DecodeShape(string encoded)
    {
        List<Vector2Int> list = new List<Vector2Int>();
        string[] pts = encoded.Split(';');
        for (int i = 0; i < pts.Length; i++)
        {
            string[] xy = pts[i].Split(',');
            int x, y;
            if (xy.Length == 2 && int.TryParse(xy[0], out x) && int.TryParse(xy[1], out y))
                list.Add(new Vector2Int(x, y));
        }
        return list;
    }

    void ClearReward()
    {
        if (rewardRoot == null) return;
        for (int i = rewardRoot.childCount - 1; i >= 0; i--)
        {
            GameObject child = rewardRoot.GetChild(i).gameObject;
            child.SetActive(false);
            Destroy(child);
        }
    }

    void BuildUi()
    {
        overlay = new GameObject("GachaOverlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        overlay.transform.SetParent(host, false);
        Image dim = overlay.GetComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, .82f);
        dim.raycastTarget = true;
        Anchor(dim.rectTransform, 0f, 0f, 1f, 1f);

        machine = new GameObject("GachaMachine", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<RectTransform>();
        machine.SetParent(overlay.transform, false);
        Image body = machine.GetComponent<Image>();
        if (machineSkin != null)
        {
            body.sprite = machineSkin;
            body.type = Image.Type.Simple;
            body.preserveAspect = false;
            body.color = Color.white;
        }
        else
        {
            body.color = new Color(.075f, .085f, .11f, 1f);
        }
        body.raycastTarget = true;
        machine.anchorMin = machine.anchorMax = new Vector2(.5f, .5f);
        machine.pivot = new Vector2(.5f, .5f);
        machine.sizeDelta = new Vector2(900f, 1600f);

        if (machineSkin == null)
            MakeFrame(machine, new Color(.84f, .60f, .13f, 1f), 18f);

        // BLOCK GEAR title is part of the full 9:16 background art.
        // Runtime title is only used for capture/exclusive special outcomes.
        title = MakeText(machine, "", 28, TextAnchor.MiddleCenter, true);
        title.color = new Color(1f, .84f, .39f, 1f);
        Anchor(title.rectTransform, .06f, .905f, .94f, .985f);

        ticketStatusText = MakeText(machine, "", 14, TextAnchor.MiddleCenter, true);
        ticketStatusText.color = new Color(1f, .87f, .48f, 1f);
        Anchor(ticketStatusText.rectTransform, .08f, .855f, .92f, .905f);
        ticketStatusText.gameObject.SetActive(false);

        correctionText = MakeText(machine, "", 18, TextAnchor.MiddleCenter, true);
        correctionText.color = new Color(.96f, .76f, .25f, 1f);
        Anchor(correctionText.rectTransform, .12f, .805f, .88f, .855f);

        // Keep the entire authored 9:16 background visible.
        // This transparent stage only positions the live wheel over the wheel painted in the art.
        RectTransform window = MakePanel(machine, "WheelWindow", new Color(0f, 0f, 0f, 0f));
        Anchor(window, .12f, .300f, .88f, .720f);

        wheel = new GameObject(
            "Wheel",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image)
        ).GetComponent<RectTransform>();
        wheel.SetParent(window, false);
        wheel.anchorMin = wheel.anchorMax = new Vector2(.5f, .5f);
        wheel.sizeDelta = new Vector2(620f, 620f);
        wheel.anchoredPosition = Vector2.zero;

        wheelCore = wheel.GetComponent<Image>();
        wheelCore.sprite = wheelSkin;
        wheelCore.color = wheelSkin != null
            ? Color.white
            : new Color(1f, 1f, 1f, 0f);
        wheelCore.preserveAspect = true;
        wheelCore.raycastTarget = false;

        // No extra Unity pointer. The pointer/jewel already exists in the full background art.
        // Only the circular roulette art rotates.

        // Cover only the fixed sample result area so the real block/name/stats can be drawn.
        // The rest of the authored 9:16 image remains fully visible.
        chute = MakePanel(machine, "Chute", new Color(.008f, .010f, .016f, .76f));
        Anchor(chute, .13f, .080f, .87f, .355f);
        MakeFrame(chute, new Color(.66f, .43f, .18f, .88f), 4f);

        rewardRoot = new GameObject("RewardBlock", typeof(RectTransform)).GetComponent<RectTransform>();
        rewardRoot.SetParent(machine, false);
        rewardRoot.anchorMin = rewardRoot.anchorMax = new Vector2(.5f, .5f);
        rewardRoot.sizeDelta = new Vector2(420f, 250f);
        rewardRoot.anchoredPosition = new Vector2(0f, -455f);

        nameText = MakeText(machine, "", 20, TextAnchor.MiddleCenter, true);
        nameText.color = new Color(1f, .95f, .82f, 1f);
        Anchor(nameText.rectTransform, .12f, .125f, .88f, .190f);

        statsText = MakeText(machine, "", 18, TextAnchor.MiddleCenter, true);
        statsText.color = new Color(.96f, .82f, .38f, 1f);
        statsText.resizeTextForBestFit = true;
        statsText.resizeTextMinSize = 48;
        statsText.resizeTextMaxSize = 70;
        Anchor(statsText.rectTransform, .10f, .075f, .90f, .130f);

        hintText = MakeText(machine, "", 15, TextAnchor.MiddleCenter, false);
        hintText.color = new Color(.94f, .86f, .68f, 1f);
        Anchor(hintText.rectTransform, .08f, .020f, .92f, .060f);
    }

    void LoadVisualAssets()
    {
        machineSkin = LoadImageResource(
            "XTapGachaUI/block_gear_machine",
            false,
            out machineSkinTexture
        );
        wheelSkin = LoadImageResource(
            "XTapGachaUI/block_gear_wheel",
            true,
            out wheelSkinTexture
        );
    }

    Sprite LoadImageResource(string resourcePath, bool circularMask, out Texture2D texture)
    {
        texture = null;
        try
        {
            texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            bool loaded = false;
            TextAsset source = Resources.Load<TextAsset>(resourcePath);

            if (source != null)
            {
                try
                {
                    if (source.bytes != null && source.bytes.Length >= 128)
                        loaded = texture.LoadImage(source.bytes, false);
                }
                catch
                {
                    loaded = false;
                }

                if (!loaded)
                {
                    try
                    {
                        string encoded = source.text != null ? source.text.Trim() : "";
                        if (!string.IsNullOrEmpty(encoded))
                            loaded = texture.LoadImage(Convert.FromBase64String(encoded), false);
                    }
                    catch
                    {
                        loaded = false;
                    }
                }
            }

            // 11.40: do not show the empty fallback frame if Resources lookup/import
            // fails on the installed APK. Decode the exact approved repository art
            // embedded in this script as a guaranteed final source.
            if (!loaded)
            {
                string embedded = resourcePath.IndexOf("block_gear_machine", StringComparison.OrdinalIgnoreCase) >= 0
                    ? EmbeddedBlockGearMachineJpegBase64
                    : EmbeddedBlockGearWheelJpegBase64;

                try
                {
                    loaded = !string.IsNullOrEmpty(embedded) &&
                             texture.LoadImage(Convert.FromBase64String(embedded), false);
                    if (loaded)
                        Debug.LogWarning("X탑 블록 머신 Resources 로드 실패. 내장 이미지 fallback 사용: " + resourcePath);
                }
                catch
                {
                    loaded = false;
                }
            }

            if (!loaded)
            {
                Destroy(texture);
                texture = null;
                Debug.LogError("X탑 블록 머신 이미지 최종 로드 실패: " + resourcePath);
                return null;
            }

            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;

            if (circularMask)
                texture = CreateCircularRgbaTexture(texture);

            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(.5f, .5f),
                100f
            );
            sprite.name = circularMask ? "XTapBlockGearWheel" : "XTapBlockGearMachine";
            return sprite;
        }
        catch (Exception e)
        {
            if (texture != null)
            {
                Destroy(texture);
                texture = null;
            }
            Debug.LogError("X탑 블록 머신 에셋 로드 실패: " + resourcePath + " / " + e.Message);
            return null;
        }
    }

    static Texture2D CreateCircularRgbaTexture(Texture2D source)
    {
        if (source == null || !source.isReadable) return source;

        int w = source.width;
        int h = source.height;
        Color32[] pixels = source.GetPixels32();
        Texture2D texture = new Texture2D(w, h, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;
        float cx = (w - 1) * .5f;
        float cy = (h - 1) * .5f;
        float radius = Mathf.Min(w, h) * .498f;
        float feather = Mathf.Max(1f, Mathf.Min(w, h) * .018f);
        float solidRadius = radius - feather;

        for (int y = 0; y < h; y++)
        {
            float dy = y - cy;
            for (int x = 0; x < w; x++)
            {
                float dx = x - cx;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                int i = y * w + x;

                if (d >= radius)
                {
                    pixels[i].a = 0;
                }
                else if (d > solidRadius)
                {
                    float edge = Mathf.Clamp01((radius - d) / feather);
                    pixels[i].a = (byte)Mathf.RoundToInt(pixels[i].a * edge);
                }
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, false);
        UnityEngine.Object.Destroy(source);
        return texture;
    }

    RectTransform MakePanel(Transform parent, string n, Color c)
    {
        GameObject go = new GameObject(n, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        Image i = go.GetComponent<Image>();
        i.color = c;
        i.raycastTarget = false;
        return i.rectTransform;
    }

    void MakeFrame(RectTransform parent, Color color, float thickness)
    {
        RectTransform top = MakePanel(parent, "Top", color);
        Anchor(top, 0f, 1f, 1f, 1f);
        top.sizeDelta = new Vector2(0f, thickness);
        RectTransform bottom = MakePanel(parent, "Bottom", color);
        Anchor(bottom, 0f, 0f, 1f, 0f);
        bottom.sizeDelta = new Vector2(0f, thickness);
        RectTransform left = MakePanel(parent, "Left", color);
        Anchor(left, 0f, 0f, 0f, 1f);
        left.sizeDelta = new Vector2(thickness, 0f);
        RectTransform right = MakePanel(parent, "Right", color);
        Anchor(right, 1f, 0f, 1f, 1f);
        right.sizeDelta = new Vector2(thickness, 0f);
    }

    Text MakeText(Transform parent, string value, int size, TextAnchor anchor, bool bold)
    {
        GameObject go = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        go.transform.SetParent(parent, false);
        Text t = go.GetComponent<Text>();
        t.text = value;
        t.font = font;
        t.fontSize = Mathf.RoundToInt(size * UiFontScale);
        t.alignment = anchor;
        t.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
        t.supportRichText = true;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Truncate;
        t.raycastTarget = false;
        return t;
    }

    static void Anchor(RectTransform r, float x1, float y1, float x2, float y2)
    {
        r.anchorMin = new Vector2(x1, y1);
        r.anchorMax = new Vector2(x2, y2);
        r.offsetMin = Vector2.zero;
        r.offsetMax = Vector2.zero;
    }

    static float NormalizeSignedAngle(float angle)
    {
        angle = Mathf.Repeat(angle + 180f, 360f) - 180f;
        return angle;
    }

    sealed class Outcome
    {
        public int correction;
        public bool captureAttempt;
        public bool captureSucceeded;
        public XTapGearBlockData block;
    }
}
