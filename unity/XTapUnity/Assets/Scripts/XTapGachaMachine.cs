using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public sealed class XTapGachaMachine : MonoBehaviour
{
    const float DesignWidth = 1080f;
    const float WheelDesignSize = 1000f;
    public bool IsOpen { get; private set; }

    RectTransform host;
    Font font;
    Action onCollected;
    XTapInventory bag;

    GameObject overlay;
    RectTransform machine;
    RectTransform wheelStage;
    RectTransform rewardSlot;
    RectTransform statRow;
    RectTransform footer;
    Text gameHeading;
    Text descriptorText;
    Text attackText;
    Text defenseText;
    Text hpText;
    Vector2 lastViewport;
    Rect lastSafeArea;
    RectTransform wheel;
    Image wheelCore;
    RectTransform fixedPointer;
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

    // Approved clockwise order, starting at 12 o'clock.
    readonly int[] corrections = {-30, -20, -10, 0, 10, 20, 30, 40, 50, -50, -40};
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

        ApplyResponsiveLayout(true);

        wheel.localScale = Vector3.one * .94f;
        float intro = 0f;
        while (intro < .18f)
        {
            intro += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(intro / .18f);
            float e = 1f - Mathf.Pow(1f - p, 3f);
            wheel.localScale = Vector3.one * Mathf.Lerp(.94f, 1f, e);
            yield return null;
        }
        wheel.localScale = Vector3.one;

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

            float ticketDuration = spin == 0 ? 2.20f : 1.75f;
            int ticketMinTurns = spin == 0 ? 5 : 4;
            int ticketMaxTurns = spin == 0 ? 8 : 7;
            yield return SpinWheelTo(correctionIndex, ticketDuration, ticketMinTurns, ticketMaxTurns);

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

        ApplyResponsiveLayout(true);

        wheel.localScale = Vector3.one * .94f;
        float intro = 0f;
        while (intro < .18f)
        {
            intro += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(intro / .18f);
            float e = 1f - Mathf.Pow(1f - p, 3f);
            wheel.localScale = Vector3.one * Mathf.Lerp(.94f, 1f, e);
            yield return null;
        }
        wheel.localScale = Vector3.one;

        int correctionIndex = UnityEngine.Random.Range(0, corrections.Length);
        int correction = corrections[correctionIndex];

        yield return SpinWheelTo(correctionIndex, 2.75f, 5, 8);

        yield return ChuteKick();

        // The exact value shown under the pointer is the value applied to the block.
        pendingOutcome = RollOutcome(corrections[correctionIndex]);
        ShowOutcome(pendingOutcome);
        yield return DropReward();

        hintText.text = pendingOutcome.block != null
            ? "터치하여 보상을 바닥에 놓기"
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
        item.descriptorFormulaVersion = 5;

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

        int equipMultiplier = selected.Count == 1 ? 2 : (selected.Count == 2 ? 3 : 5);
        item.descriptorIds = string.Join(",", ids.ToArray());
        item.descriptorWords = string.Join("|", words.ToArray());
        item.descriptorEffectText =
            "수식어 " + selected.Count + "개 · 착용 전체 능력 배수 +" + equipMultiplier;
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
            title.text = "포획 결과";
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
        title.text = r.exclusive ? "캐릭터 전용 블록" : "획득 보상";
        nameText.text = XTapGearNameColor.Rich(r) + "  ·  " + r.cellCount + "칸";
        statsText.text = "";
        statRow.gameObject.SetActive(true);
        attackText.text = "<size=23>공격</size>\n<color=#FFBF70>" + XTapStatFormat.Compact(r.attack) + "</color>";
        defenseText.text = "<size=23>방어</size>\n<color=#76CFFF>" + XTapStatFormat.Compact(r.defense) + "</color>";
        hpText.text = "<size=23>체력</size>\n<color=#FF8585>" + XTapStatFormat.Compact(r.hp) + "</color>";
        descriptorText.text = string.IsNullOrEmpty(r.descriptorEffectText) ? "" : r.descriptorEffectText;
        DrawBlock(r);
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
        Anchor(rt, .04f, .04f, .96f, .96f);
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
        int minX = 999, maxX = -999, minY = 999, maxY = -999;
        for (int i = 0; i < cells.Count; i++)
        {
            minX = Mathf.Min(minX, cells[i].x);
            maxX = Mathf.Max(maxX, cells[i].x);
            minY = Mathf.Min(minY, cells[i].y);
            maxY = Mathf.Max(maxY, cells[i].y);
        }

        if (cells.Count == 0) return;
        float cell = Mathf.Min(54f,
            (rewardRoot.rect.width - 16f) / (maxX - minX + 1),
            (rewardRoot.rect.height - 16f) / (maxY - minY + 1));
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
        // Keep the card and its frame stationary; only pulse its inner light.
        Image surface = chute.GetComponent<Image>();
        Color resting = new Color(.018f, .022f, .035f, .96f);
        float t = 0f;
        while (t < .16f)
        {
            t += Time.unscaledDeltaTime;
            surface.color = Color.Lerp(resting, new Color(.10f, .065f, .025f, .98f),
                Mathf.Sin(Mathf.Clamp01(t / .16f) * Mathf.PI));
            yield return null;
        }
        surface.color = resting;
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
        rewardRoot.anchoredPosition = Vector2.zero;
        rewardRoot.localScale = Vector3.one;
        if (statRow != null) statRow.gameObject.SetActive(false);
        if (descriptorText != null) descriptorText.text = "";
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
        Image inputShield = overlay.GetComponent<Image>();
        inputShield.color = Color.black;
        inputShield.raycastTarget = true;
        Anchor(inputShield.rectTransform, 0f, 0f, 1f, 1f);

        RectTransform backdrop = MakePanel(overlay.transform, "FullBleedBackdrop", Color.white);
        Image backdropImage = backdrop.GetComponent<Image>();
        backdropImage.sprite = machineSkin;
        backdropImage.preserveAspect = false;
        if (machineSkin != null)
        {
            AspectRatioFitter fitter = backdrop.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectRatio = machineSkin.rect.width / machineSkin.rect.height;
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        }
        else
        {
            backdropImage.color = new Color(.025f, .035f, .055f, 1f);
            Anchor(backdrop, 0f, 0f, 1f, 1f);
        }

        RectTransform shade = new GameObject("ReadabilityShade", typeof(RectTransform),
            typeof(CanvasRenderer), typeof(XTapGachaShadeGraphic)).GetComponent<RectTransform>();
        shade.SetParent(overlay.transform, false);
        Anchor(shade, 0f, 0f, 1f, 1f);
        shade.GetComponent<XTapGachaShadeGraphic>().raycastTarget = false;

        machine = new GameObject("GachaContent", typeof(RectTransform)).GetComponent<RectTransform>();
        machine.SetParent(overlay.transform, false);
        machine.anchorMin = machine.anchorMax = new Vector2(.5f, .5f);
        machine.pivot = new Vector2(.5f, .5f);

        gameHeading = MakeText(machine, "BLOCK GEAR", 76, TextAnchor.MiddleCenter, true);
        gameHeading.color = new Color(1f, .88f, .64f, 1f);
        Outline headingOutline = gameHeading.gameObject.AddComponent<Outline>();
        headingOutline.effectColor = new Color(.05f, .025f, .01f, 1f);
        headingOutline.effectDistance = new Vector2(3f, -3f);

        ticketStatusText = MakeText(machine, "", 28, TextAnchor.MiddleCenter, true);
        ticketStatusText.color = new Color(.96f, .86f, .65f, 1f);
        ticketStatusText.gameObject.SetActive(false);

        correctionText = MakeText(machine, "", 42, TextAnchor.MiddleCenter, true);
        correctionText.color = new Color(1f, .88f, .60f, 1f);
        Outline correctionOutline = correctionText.gameObject.AddComponent<Outline>();
        correctionOutline.effectColor = new Color(.015f, .02f, .04f, .95f);
        correctionOutline.effectDistance = new Vector2(2f, -2f);

        wheelStage = new GameObject("WheelStage", typeof(RectTransform)).GetComponent<RectTransform>();
        wheelStage.SetParent(machine, false);
        wheelStage.anchorMin = wheelStage.anchorMax = new Vector2(0f, 1f);
        wheelStage.pivot = new Vector2(.5f, .5f);
        wheelStage.sizeDelta = new Vector2(WheelDesignSize, WheelDesignSize);

        wheel = new GameObject("Wheel", typeof(RectTransform)).GetComponent<RectTransform>();
        wheel.SetParent(wheelStage, false);
        wheel.anchorMin = wheel.anchorMax = new Vector2(.5f, .5f);
        wheel.sizeDelta = new Vector2(WheelDesignSize, WheelDesignSize);

        RectTransform face = new GameObject("SingleWheelFace", typeof(RectTransform),
            typeof(CanvasRenderer), typeof(XTapRouletteFaceGraphic)).GetComponent<RectTransform>();
        face.SetParent(wheel, false);
        Anchor(face, 0f, 0f, 1f, 1f);
        face.GetComponent<XTapRouletteFaceGraphic>().raycastTarget = false;

        RectTransform ornament = MakePanel(wheel, "TransparentWheelOrnament", Color.white);
        Anchor(ornament, 0f, 0f, 1f, 1f);
        wheelCore = ornament.GetComponent<Image>();
        wheelCore.sprite = wheelSkin;
        wheelCore.preserveAspect = true;
        wheelCore.color = wheelSkin == null ? Color.clear : Color.white;
        BuildRouletteLabels();

        fixedPointer = new GameObject("SingleFixedPointer", typeof(RectTransform),
            typeof(CanvasRenderer), typeof(XTapFixedPointerGraphic)).GetComponent<RectTransform>();
        fixedPointer.SetParent(wheelStage, false);
        fixedPointer.anchorMin = fixedPointer.anchorMax = new Vector2(.5f, .5f);
        fixedPointer.sizeDelta = new Vector2(70f, 96f);
        fixedPointer.anchoredPosition = new Vector2(0f, 444f);
        fixedPointer.GetComponent<XTapFixedPointerGraphic>().raycastTarget = false;

        chute = MakePanel(machine, "RewardCard", new Color(.018f, .022f, .035f, .96f));
        MakeFrame(chute, new Color(.72f, .52f, .24f, .9f), 2f);
        RectTransform divider = MakePanel(chute, "RewardDivider", new Color(.72f, .52f, .24f, .35f));
        Anchor(divider, .305f, .12f, .305f, .88f);
        divider.sizeDelta = new Vector2(2f, 0f);

        rewardSlot = new GameObject("RewardArtSlot", typeof(RectTransform), typeof(RectMask2D))
            .GetComponent<RectTransform>();
        rewardSlot.SetParent(chute, false);
        Anchor(rewardSlot, .025f, .08f, .29f, .92f);
        rewardRoot = new GameObject("RewardBlock", typeof(RectTransform)).GetComponent<RectTransform>();
        rewardRoot.SetParent(rewardSlot, false);
        Anchor(rewardRoot, 0f, 0f, 1f, 1f);

        title = MakeText(chute, "", 23, TextAnchor.MiddleLeft, true);
        title.color = new Color(.88f, .74f, .49f, 1f);
        Anchor(title.rectTransform, .33f, .86f, .97f, .98f);

        nameText = MakeText(chute, "", 42, TextAnchor.MiddleLeft, true);
        nameText.color = new Color(1f, .97f, .90f, 1f);
        nameText.resizeTextForBestFit = true;
        nameText.resizeTextMinSize = 30;
        nameText.resizeTextMaxSize = 42;
        Anchor(nameText.rectTransform, .33f, .52f, .97f, .86f);

        statsText = MakeText(chute, "", 32, TextAnchor.MiddleLeft, true);
        statsText.color = new Color(.9f, .9f, .94f, 1f);
        Anchor(statsText.rectTransform, .33f, .18f, .97f, .50f);

        statRow = new GameObject("StatRow", typeof(RectTransform)).GetComponent<RectTransform>();
        statRow.SetParent(chute, false);
        Anchor(statRow, .33f, .20f, .97f, .50f);
        attackText = MakeText(statRow, "", 42, TextAnchor.MiddleLeft, true);
        defenseText = MakeText(statRow, "", 42, TextAnchor.MiddleLeft, true);
        hpText = MakeText(statRow, "", 42, TextAnchor.MiddleLeft, true);
        Anchor(attackText.rectTransform, 0f, 0f, .32f, 1f);
        Anchor(defenseText.rectTransform, .34f, 0f, .66f, 1f);
        Anchor(hpText.rectTransform, .68f, 0f, 1f, 1f);
        statRow.gameObject.SetActive(false);

        descriptorText = MakeText(chute, "", 23, TextAnchor.MiddleLeft, false);
        descriptorText.color = new Color(.85f, .77f, .97f, 1f);
        descriptorText.resizeTextForBestFit = true;
        descriptorText.resizeTextMinSize = 18;
        descriptorText.resizeTextMaxSize = 23;
        Anchor(descriptorText.rectTransform, .33f, .025f, .97f, .17f);

        footer = MakePanel(machine, "CollectHint", new Color(.008f, .012f, .024f, .78f));
        MakeFrame(footer, new Color(.72f, .52f, .24f, .45f), 1.5f);
        hintText = MakeText(footer, "", 30, TextAnchor.MiddleCenter, true);
        hintText.color = new Color(1f, .92f, .76f, 1f);
        Anchor(hintText.rectTransform, .035f, .10f, .965f, .90f);
        ApplyResponsiveLayout(true);
    }

    void LateUpdate()
    {
        if (IsOpen) ApplyResponsiveLayout(false);
    }

    void ApplyResponsiveLayout(bool force)
    {
        if (machine == null || overlay == null) return;
        Vector2 viewport = ((RectTransform)overlay.transform).rect.size;
        Rect safe = Screen.safeArea;
        if (viewport.x <= 0f || viewport.y <= 0f) return;
        if (!force && viewport == lastViewport && safe == lastSafeArea) return;
        lastViewport = viewport;
        lastSafeArea = safe;

        float scale = viewport.x / DesignWidth;
        float height = viewport.y / scale;
        float pixelToDesign = DesignWidth / Mathf.Max(1, Screen.width);
        float safeTop = Mathf.Max(24f, (Screen.height - safe.yMax) * pixelToDesign + 16f);
        float safeBottom = Mathf.Max(24f, safe.yMin * pixelToDesign + 16f);
        float side = Mathf.Max(42f, Mathf.Max(safe.xMin, Screen.width - safe.xMax) * pixelToDesign + 20f);
        float contentWidth = DesignWidth - side * 2f;

        machine.sizeDelta = new Vector2(DesignWidth, height);
        machine.localScale = Vector3.one * scale;
        machine.anchoredPosition = Vector2.zero;

        SetRectFromTop(gameHeading.rectTransform, side, safeTop, contentWidth, 108f);
        SetRectFromTop(ticketStatusText.rectTransform, side, safeTop + 112f, contentWidth, 56f);

        const float cardHeight = 300f;
        const float footerHeight = 76f;
        float footerTop = height - safeBottom - footerHeight;
        float cardTop = footerTop - 18f - cardHeight;
        float wheelBottom = cardTop - 28f;
        float wheelTopLimit = Mathf.Max(height * .32f, safeTop + 238f);
        float diameter = Mathf.Max(120f, Mathf.Min(980f, contentWidth, wheelBottom - wheelTopLimit));
        wheelStage.anchoredPosition = new Vector2(DesignWidth * .5f, -(wheelBottom - diameter * .5f));
        wheelStage.localScale = Vector3.one * (diameter / WheelDesignSize);

        SetRectFromTop(correctionText.rectTransform, side, wheelBottom - diameter - 64f, contentWidth, 56f);
        SetRectFromTop(chute, side, cardTop, contentWidth, cardHeight);
        SetRectFromTop(footer, side, footerTop, contentWidth, footerHeight);
    }

    static void SetRectFromTop(RectTransform rect, float x, float y, float width, float height)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(width, height);
    }

    void BuildRouletteLabels()
    {
        float step = 360f / corrections.Length;
        const float radius = 324f;
        for (int i = 0; i < corrections.Length; i++)
        {
            string value = corrections[i] == 0 ? "0%" :
                (corrections[i] > 0 ? "+" + corrections[i] : corrections[i].ToString());
            Text label = MakeText(wheel, value, 54, TextAnchor.MiddleCenter, true);
            label.name = "WheelValue_" + value;
            label.color = new Color(1f, .97f, .90f, 1f);
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            RectTransform rt = label.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(.5f, .5f);
            rt.sizeDelta = new Vector2(160f, 82f);
            float angle = i * step;
            float radians = angle * Mathf.Deg2Rad;
            rt.anchoredPosition = new Vector2(Mathf.Sin(radians) * radius, Mathf.Cos(radians) * radius);
            rt.localRotation = Quaternion.Euler(0f, 0f, -angle);
            Outline outline = label.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(.005f, .008f, .02f, .95f);
            outline.effectDistance = new Vector2(2f, -2f);
        }
    }


    float WheelVisualAngleForCorrectionIndex(int correctionIndex)
    {
        float segment = 360f / corrections.Length;

        // Labels and sectors share the canonical order. No numbers are baked into art.
        int wrapped = ((correctionIndex % corrections.Length) + corrections.Length) % corrections.Length;
        return wrapped * segment;
    }

    IEnumerator SpinWheelTo(int correctionIndex, float duration, int minTurns, int maxTurnsExclusive)
    {
        float segment = 360f / corrections.Length;
        float startAngle = NormalizeSignedAngle(wheel.localEulerAngles.z);
        float targetAngle = WheelVisualAngleForCorrectionIndex(correctionIndex);

        // Stop just past the target first, then bounce back into the exact slot.
        float overshootAngle = targetAngle - segment * .18f;
        float clockwiseDelta = Mathf.Repeat(startAngle - overshootAngle, 360f);
        float totalSpin = 360f * UnityEngine.Random.Range(minTurns, maxTurnsExclusive) + clockwiseDelta;

        float mainDuration = duration * .86f;
        float t = 0f;
        int lastTick = -1;
        float tickKick = 0f;

        while (t < mainDuration)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / mainDuration);

            // Long, readable deceleration. Most suspense is intentionally in the last third.
            float e = 1f - Mathf.Pow(1f - p, 3.2f);
            float travelled = totalSpin * e;
            wheel.localRotation = Quaternion.Euler(0f, 0f, startAngle - travelled);

            int tick = Mathf.FloorToInt(travelled / segment);
            if (tick != lastTick)
            {
                lastTick = tick;
                tickKick = 1f;
            }

            tickKick = Mathf.MoveTowards(tickKick, 0f, Time.unscaledDeltaTime * 7.5f);

            // Tick feedback belongs to the wheel/pointer only. Never shake the full-screen background.
            float wheelNudge = Mathf.Sin(Time.unscaledTime * 92f) * tickKick * 2.5f;
            wheel.anchoredPosition = new Vector2(wheelNudge, 0f);
            wheel.localScale = Vector3.one * (1f + tickKick * .025f);

            if (fixedPointer != null)
                fixedPointer.localScale = Vector3.one * (1f + tickKick * .11f);

            yield return null;
        }

        wheel.anchoredPosition = Vector2.zero;
        wheel.localScale = Vector3.one;
        if (fixedPointer != null) fixedPointer.localScale = Vector3.one;

        // A short held breath before the final settling click.
        yield return new WaitForSecondsRealtime(.06f);

        float settleDuration = Mathf.Max(.30f, duration * .14f);
        t = 0f;
        while (t < settleDuration)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / settleDuration);
            float e = Mathf.SmoothStep(0f, 1f, p);

            float angle = Mathf.LerpAngle(overshootAngle, targetAngle, e);
            angle += Mathf.Sin(p * Mathf.PI * 2.2f) * (1f - p) * segment * .035f;
            wheel.localRotation = Quaternion.Euler(0f, 0f, angle);

            if (fixedPointer != null)
                fixedPointer.localScale = Vector3.one * (1f + Mathf.Sin(p * Mathf.PI) * .08f);

            yield return null;
        }

        // Exact final slot: visual value and applied correction can never disagree.
        wheel.localRotation = Quaternion.Euler(0f, 0f, targetAngle);
        wheel.anchoredPosition = Vector2.zero;
        wheel.localScale = Vector3.one;
        if (fixedPointer != null) fixedPointer.localScale = Vector3.one;
    }

    void LoadVisualAssets()
    {
        machineSkin = LoadImageResource("XTapGachaUI/block_gear_machine", out machineSkinTexture);
        wheelSkin = LoadImageResource("XTapGachaUI/block_gear_wheel", out wheelSkinTexture);
    }

    Sprite LoadImageResource(string resourcePath, out Texture2D texture)
    {
        texture = null;
        try
        {
            TextAsset source = Resources.Load<TextAsset>(resourcePath);
            if (source == null)
                throw new InvalidOperationException("필수 Resources 에셋 누락");

            texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!texture.LoadImage(source.bytes, true))
                throw new InvalidOperationException("Unity 이미지 디코딩 실패");

            // The wheel is an authored transparent PNG. Preserve its original alpha.
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            Sprite sprite = Sprite.Create(texture,
                new Rect(0f, 0f, texture.width, texture.height), new Vector2(.5f, .5f), 100f,
                0, SpriteMeshType.FullRect);
            sprite.name = resourcePath;
            return sprite;
        }
        catch (Exception e)
        {
            if (texture != null) Destroy(texture);
            texture = null;
            Debug.LogError("X탑 블록 머신 에셋 로드 실패: " + resourcePath + " / " + e.Message);
            return null;
        }
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
        t.fontSize = size;
        t.lineSpacing = 1.05f;
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
