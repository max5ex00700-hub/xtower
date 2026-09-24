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
    Text nameText;
    Text statsText;
    Text hintText;

    bool readyToCollect;
    Coroutine playRoutine;
    Outcome pendingOutcome;
    int activeCharacterId = 1;
    int activeProgressStep;

    readonly int[] corrections = {-50, -40, -30, -20, -10, 0, 10, 20, 30, 40, 50};
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
        new DescriptorDef("heavy", "묵직한")
    };

    public void Initialize(RectTransform parent, Font uiFont, Action collected, XTapInventory inventory)
    {
        host = parent;
        font = uiFont;
        onCollected = collected;
        bag = inventory;
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

    IEnumerator PlayRoutine()
    {
        IsOpen = true;
        readyToCollect = false;
        pendingOutcome = null;
        overlay.SetActive(true);
        overlay.transform.SetAsLastSibling();

        ClearReward();
        correctionText.text = "";
        nameText.text = "";
        statsText.text = "";
        hintText.text = "보정 룰렛 회전 중";
        title.text = "X-TOWER  REWARD";

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
                outcome.captureSucceeded = UnityEngine.Random.value < .01f;

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
            title.text = "CAPTURE BALL";
            nameText.text = outcome.captureSucceeded ? "포획 성공!" : "포획 실패";
            statsText.text = outcome.captureSucceeded
                ? activeCharacterId + "층 캐릭터 포획 상태 저장"
                : "포획 확률 1%";
            DrawCaptureBall(outcome.captureSucceeded);
            return;
        }

        if (outcome.block == null) return;

        XTapGearBlockData r = outcome.block;
        title.text = r.exclusive ? "EXCLUSIVE BLOCK" : "BLOCK GEAR";
        nameText.text = r.displayName + "  ·  " + r.cellCount + "칸";
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
        rewardRoot.localScale = Vector3.one * .55f;

        float t = 0f;
        while (t < .42f)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / .42f);
            float e = 1f - Mathf.Pow(1f - p, 3f);
            float bounce = Mathf.Sin(p * Mathf.PI * 2f) * (1f - p) * 18f;
            rewardRoot.anchoredPosition = Vector2.Lerp(start, end, e) + Vector2.up * bounce;
            rewardRoot.localScale = Vector3.one * Mathf.Lerp(.55f, 1f, e);
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
        body.color = new Color(.075f, .085f, .11f, 1f);
        body.raycastTarget = true;
        machine.anchorMin = machine.anchorMax = new Vector2(.5f, .5f);
        machine.pivot = new Vector2(.5f, .5f);
        machine.sizeDelta = new Vector2(900f, 1600f);

        MakeFrame(machine, new Color(.84f, .60f, .13f, 1f), 18f);

        title = MakeText(machine, "X-TOWER  REWARD", 28, TextAnchor.MiddleCenter, true);
        title.color = new Color(1f, .84f, .39f, 1f);
        Anchor(title.rectTransform, .06f, .905f, .94f, .985f);

        correctionText = MakeText(machine, "", 20, TextAnchor.MiddleCenter, true);
        correctionText.color = new Color(.96f, .76f, .25f, 1f);
        Anchor(correctionText.rectTransform, .12f, .830f, .88f, .905f);

        RectTransform window = MakePanel(machine, "WheelWindow", new Color(.025f, .03f, .045f, 1f));
        Anchor(window, .17f, .455f, .83f, .820f);
        MakeFrame(window, new Color(.42f, .44f, .50f, 1f), 8f);

        wheel = new GameObject("Wheel", typeof(RectTransform)).GetComponent<RectTransform>();
        wheel.SetParent(window, false);
        wheel.anchorMin = wheel.anchorMax = new Vector2(.5f, .5f);
        wheel.sizeDelta = new Vector2(430f, 430f);
        wheel.anchoredPosition = Vector2.zero;

        float segment = 360f / corrections.Length;
        for (int i = 0; i < corrections.Length; i++)
        {
            float a = i * segment * Mathf.Deg2Rad;

            GameObject slot = new GameObject("Slot" + i, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            slot.transform.SetParent(wheel, false);
            Image si = slot.GetComponent<Image>();
            si.color = i == 5
                ? new Color(.58f, .16f, .62f, 1f)
                : (corrections[i] > 0 ? new Color(.76f, .45f, .10f, 1f) : new Color(.20f, .28f, .38f, 1f));
            si.raycastTarget = false;
            RectTransform sr = si.rectTransform;
            sr.anchorMin = sr.anchorMax = new Vector2(.5f, .5f);
            sr.sizeDelta = new Vector2(86f, 64f);
            sr.anchoredPosition = new Vector2(Mathf.Sin(a), Mathf.Cos(a)) * 176f;

            string label = (corrections[i] > 0 ? "+" + corrections[i] : corrections[i].ToString()) + "%";
            Text lt = MakeText(slot.transform, label, 12, TextAnchor.MiddleCenter, true);
            lt.color = Color.white;
            Anchor(lt.rectTransform, 0f, 0f, 1f, 1f);
        }

        GameObject coreGo = new GameObject("WheelCore", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        coreGo.transform.SetParent(wheel, false);
        wheelCore = coreGo.GetComponent<Image>();
        wheelCore.color = new Color(.13f, .15f, .20f, 1f);
        wheelCore.raycastTarget = false;
        RectTransform cr = wheelCore.rectTransform;
        cr.anchorMin = cr.anchorMax = new Vector2(.5f, .5f);
        cr.sizeDelta = new Vector2(200f, 200f);

        Text x = MakeText(coreGo.transform, "X", 46, TextAnchor.MiddleCenter, true);
        x.color = new Color(1f, .78f, .20f, 1f);
        Anchor(x.rectTransform, 0f, 0f, 1f, 1f);

        RectTransform arrow = MakePanel(window, "Pointer", new Color(1f, .78f, .18f, 1f));
        arrow.anchorMin = arrow.anchorMax = new Vector2(.5f, 1f);
        arrow.pivot = new Vector2(.5f, 1f);
        arrow.sizeDelta = new Vector2(42f, 72f);
        arrow.anchoredPosition = new Vector2(0f, -7f);

        chute = MakePanel(machine, "Chute", new Color(.025f, .03f, .04f, 1f));
        Anchor(chute, .25f, .285f, .75f, .420f);
        MakeFrame(chute, new Color(.50f, .52f, .58f, 1f), 7f);

        rewardRoot = new GameObject("RewardBlock", typeof(RectTransform)).GetComponent<RectTransform>();
        rewardRoot.SetParent(machine, false);
        rewardRoot.anchorMin = rewardRoot.anchorMax = new Vector2(.5f, .5f);
        rewardRoot.sizeDelta = new Vector2(380f, 240f);
        rewardRoot.anchoredPosition = new Vector2(0f, -360f);

        nameText = MakeText(machine, "", 20, TextAnchor.MiddleCenter, true);
        nameText.color = Color.white;
        Anchor(nameText.rectTransform, .06f, .185f, .94f, .265f);

        statsText = MakeText(machine, "", 18, TextAnchor.MiddleCenter, true);
        statsText.color = new Color(.96f, .82f, .38f, 1f);
        statsText.resizeTextForBestFit = true;
        statsText.resizeTextMinSize = 48;
        statsText.resizeTextMaxSize = 70;
        Anchor(statsText.rectTransform, .05f, .105f, .95f, .185f);

        hintText = MakeText(machine, "", 15, TextAnchor.MiddleCenter, false);
        hintText.color = new Color(.72f, .74f, .80f, 1f);
        Anchor(hintText.rectTransform, .05f, .025f, .95f, .095f);
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
