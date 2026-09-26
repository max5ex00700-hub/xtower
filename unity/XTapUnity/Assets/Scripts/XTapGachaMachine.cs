using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public sealed class XTapGachaMachine : MonoBehaviour
{
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
            TextAsset source = Resources.Load<TextAsset>(resourcePath);
            if (source == null)
            {
                Debug.LogWarning("X탑 블록 머신 에셋 누락: " + resourcePath);
                return null;
            }

            texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            bool loaded = false;

            if (source.bytes != null && source.bytes.Length >= 1024)
                loaded = texture.LoadImage(source.bytes, false);

            // Some repository image assets may be stored as base64 text.
            // Support both formats so the 9:16 art cannot silently fall back to the plain panel.
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

            if (!loaded)
            {
                Destroy(texture);
                texture = null;
                Debug.LogWarning("X탑 블록 머신 이미지 로드 실패: " + resourcePath);
                return null;
            }

            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;

            if (circularMask)
                ApplyCircularAlphaMask(texture);

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
            Debug.LogWarning("X탑 블록 머신 에셋 로드 실패: " + resourcePath + " / " + e.Message);
            return null;
        }
    }

    static void ApplyCircularAlphaMask(Texture2D texture)
    {
        if (texture == null || !texture.isReadable) return;

        Color32[] pixels = texture.GetPixels32();
        int w = texture.width;
        int h = texture.height;
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
