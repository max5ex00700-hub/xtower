using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public sealed class XTapGachaMachine : MonoBehaviour
{
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

    readonly int[] corrections = {-50, -40, -30, -20, -10, 0, 10, 20, 30, 40, 50};
    readonly int[] allowedSizes = {1, 2, 3, 4, 5, 6, 9, 12};
    readonly int[] baseBudgets = {10, 22, 35, 50, 66, 84, 135, 190};

    readonly string[] prefixes =
    {
        "검은", "붉은", "은빛", "낡은", "잊힌", "차가운",
        "불완전한", "무거운", "날카로운", "고요한", "균열난", "빛나는"
    };

    readonly string[] nouns =
    {
        "파편", "인장", "갑주", "장식", "핵", "조각",
        "결정", "부품", "흔적", "고리", "판금", "심장"
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

    public void PlayReward(int characterId)
    {
        if (host == null || overlay == null) return;
        activeCharacterId = Mathf.Max(1, characterId);

        if (playRoutine != null) StopCoroutine(playRoutine);
        playRoutine = StartCoroutine(PlayRoutine());
    }

    public void PlayReward()
    {
        PlayReward(1);
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
        float startAngle = wheel.localEulerAngles.z;
        float totalSpin = 360f * UnityEngine.Random.Range(4, 7) + correctionIndex * segment;
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

        machine.anchoredPosition = Vector2.zero;
        wheelCore.rectTransform.localScale = Vector3.one;

        yield return ChuteKick();

        pendingOutcome = RollOutcome(correction);
        ShowOutcome(pendingOutcome);
        yield return DropReward();

        hintText.text = pendingOutcome.block != null
            ? "화면을 터치해서 가방에 넣기"
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

        float multiplier = 1f + correction / 100f;
        int total = Mathf.Max(3, Mathf.RoundToInt(budget * multiplier));

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

        if (exclusive)
            r.displayName = activeCharacterId + "층 전용 " + nouns[UnityEngine.Random.Range(0, nouns.Length)];
        else
            r.displayName = prefixes[UnityEngine.Random.Range(0, prefixes.Length)] + " " +
                            nouns[UnityEngine.Random.Range(0, nouns.Length)];

        r.shape = EncodeShape(ShapeFor(cells));
        return r;
    }

    void ShowOutcome(Outcome outcome)
    {
        ClearReward();

        string corr = outcome.correction > 0
            ? "+" + outcome.correction + "%"
            : outcome.correction + "%";
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
        statsText.text = "공격 +" + r.attack + "     방어 +" + r.defense + "     체력 +" + r.hp;
        DrawBlock(r);
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
                (p.y - minY + .5f) * cell - height * .5f
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
        if (pendingOutcome != null && pendingOutcome.block != null)
        {
            if (bag == null || !bag.TryAddBlock(pendingOutcome.block))
            {
                hintText.text = "가방 8×3에 들어갈 공간이 없습니다.";
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
            options.Add(S(new int[]{0,0,1,0,2,0,3,0,4,0, 0,1,0,2,0,3,0,4}));
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
            Destroy(rewardRoot.GetChild(i).gameObject);
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
        machine.sizeDelta = new Vector2(760f, 1240f);

        MakeFrame(machine, new Color(.84f, .60f, .13f, 1f), 18f);

        title = MakeText(machine, "X-TOWER  REWARD", 34, TextAnchor.MiddleCenter, true);
        title.color = new Color(1f, .84f, .39f, 1f);
        Anchor(title.rectTransform, .08f, .91f, .92f, .97f);

        correctionText = MakeText(machine, "", 27, TextAnchor.MiddleCenter, true);
        correctionText.color = new Color(.96f, .76f, .25f, 1f);
        Anchor(correctionText.rectTransform, .15f, .855f, .85f, .91f);

        RectTransform window = MakePanel(machine, "WheelWindow", new Color(.025f, .03f, .045f, 1f));
        Anchor(window, .16f, .49f, .84f, .85f);
        MakeFrame(window, new Color(.42f, .44f, .50f, 1f), 8f);

        wheel = new GameObject("Wheel", typeof(RectTransform)).GetComponent<RectTransform>();
        wheel.SetParent(window, false);
        wheel.anchorMin = wheel.anchorMax = new Vector2(.5f, .5f);
        wheel.sizeDelta = new Vector2(390f, 390f);
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
            sr.sizeDelta = new Vector2(62f, 42f);
            sr.anchoredPosition = new Vector2(Mathf.Sin(a), Mathf.Cos(a)) * 160f;

            string label = corrections[i] > 0 ? "+" + corrections[i] : corrections[i].ToString();
            Text lt = MakeText(slot.transform, label, 17, TextAnchor.MiddleCenter, true);
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
        cr.sizeDelta = new Vector2(178f, 178f);

        Text x = MakeText(coreGo.transform, "X", 82, TextAnchor.MiddleCenter, true);
        x.color = new Color(1f, .78f, .20f, 1f);
        Anchor(x.rectTransform, 0f, 0f, 1f, 1f);

        RectTransform arrow = MakePanel(window, "Pointer", new Color(1f, .78f, .18f, 1f));
        arrow.anchorMin = arrow.anchorMax = new Vector2(.5f, 1f);
        arrow.pivot = new Vector2(.5f, 1f);
        arrow.sizeDelta = new Vector2(42f, 72f);
        arrow.anchoredPosition = new Vector2(0f, -7f);

        chute = MakePanel(machine, "Chute", new Color(.025f, .03f, .04f, 1f));
        Anchor(chute, .28f, .30f, .72f, .44f);
        MakeFrame(chute, new Color(.50f, .52f, .58f, 1f), 7f);

        rewardRoot = new GameObject("RewardBlock", typeof(RectTransform)).GetComponent<RectTransform>();
        rewardRoot.SetParent(machine, false);
        rewardRoot.anchorMin = rewardRoot.anchorMax = new Vector2(.5f, .5f);
        rewardRoot.sizeDelta = new Vector2(300f, 190f);
        rewardRoot.anchoredPosition = new Vector2(0f, -260f);

        nameText = MakeText(machine, "", 29, TextAnchor.MiddleCenter, true);
        nameText.color = Color.white;
        Anchor(nameText.rectTransform, .08f, .13f, .92f, .20f);

        statsText = MakeText(machine, "", 25, TextAnchor.MiddleCenter, true);
        statsText.color = new Color(.96f, .82f, .38f, 1f);
        statsText.resizeTextForBestFit = true;
        statsText.resizeTextMinSize = 18;
        statsText.resizeTextMaxSize = 25;
        Anchor(statsText.rectTransform, .05f, .075f, .95f, .135f);

        hintText = MakeText(machine, "", 22, TextAnchor.MiddleCenter, false);
        hintText.color = new Color(.72f, .74f, .80f, 1f);
        Anchor(hintText.rectTransform, .05f, .025f, .95f, .072f);
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
        t.alignment = anchor;
        t.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
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

    sealed class Outcome
    {
        public int correction;
        public bool captureAttempt;
        public bool captureSucceeded;
        public XTapGearBlockData block;
    }
}
