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

    GameObject overlay;
    RectTransform machine;
    RectTransform wheel;
    Image wheelCore;
    RectTransform chute;
    RectTransform rewardRoot;
    Text title;
    Text nameText;
    Text statsText;
    Text hintText;

    bool readyToCollect;
    Coroutine playRoutine;

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

    public void Initialize(RectTransform parent, Font uiFont, Action collected)
    {
        host = parent;
        font = uiFont;
        onCollected = collected;
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

    public void PlayReward()
    {
        if (host == null || overlay == null) return;

        if (playRoutine != null) StopCoroutine(playRoutine);
        playRoutine = StartCoroutine(PlayRoutine());
    }

    IEnumerator PlayRoutine()
    {
        IsOpen = true;
        readyToCollect = false;
        overlay.SetActive(true);
        overlay.transform.SetAsLastSibling();

        ClearReward();
        nameText.text = "";
        statsText.text = "";
        hintText.text = "머신 작동 중";
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

        float startAngle = wheel.localEulerAngles.z;
        float totalSpin = UnityEngine.Random.Range(1280f, 1760f);
        float duration = 1.75f;
        float t = 0f;

        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / duration);
            float e = 1f - Mathf.Pow(1f - p, 4f);
            float angle = startAngle - totalSpin * e;
            wheel.localRotation = Quaternion.Euler(0f, 0f, angle);

            float shake = Mathf.Sin(Time.unscaledTime * 70f) * (1f - p) * 5f;
            machine.anchoredPosition = new Vector2(shake, 0f);

            float pulse = 1f + Mathf.Sin(Time.unscaledTime * 17f) * .05f;
            wheelCore.rectTransform.localScale = Vector3.one * pulse;
            yield return null;
        }

        machine.anchoredPosition = Vector2.zero;
        wheelCore.rectTransform.localScale = Vector3.one;

        yield return ChuteKick();

        Reward reward = RollReward();
        ShowReward(reward);
        yield return DropReward();

        hintText.text = "화면을 터치해서 획득";
        readyToCollect = true;
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

    void CloseAndCollect()
    {
        readyToCollect = false;
        IsOpen = false;
        overlay.SetActive(false);
        if (onCollected != null) onCollected();
    }

    Reward RollReward()
    {
        int sizeIndex = UnityEngine.Random.Range(0, allowedSizes.Length);
        int cells = allowedSizes[sizeIndex];
        int budget = baseBudgets[sizeIndex];

        float quality = UnityEngine.Random.Range(.88f, 1.13f);
        int total = Mathf.Max(3, Mathf.RoundToInt(budget * quality));

        float a = UnityEngine.Random.Range(.15f, .70f);
        float d = UnityEngine.Random.Range(.10f, .65f);
        float h = UnityEngine.Random.Range(.10f, .65f);
        float sum = a + d + h;

        int attack = Mathf.Max(1, Mathf.RoundToInt(total * a / sum));
        int defense = Mathf.Max(1, Mathf.RoundToInt(total * d / sum));
        int hp = Mathf.Max(1, total - attack - defense);

        Reward r = new Reward();
        r.cellCount = cells;
        r.attack = attack;
        r.defense = defense;
        r.hp = hp;
        r.displayName = prefixes[UnityEngine.Random.Range(0, prefixes.Length)] + " " +
                        nouns[UnityEngine.Random.Range(0, nouns.Length)];
        r.cells = ShapeFor(cells);
        return r;
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

    void ShowReward(Reward r)
    {
        ClearReward();

        nameText.text = r.displayName + "  ·  " + r.cellCount + "칸";
        statsText.text = "공격 +" + r.attack + "     방어 +" + r.defense + "     체력 +" + r.hp;

        float cell = 44f;
        int minX = 999, maxX = -999, minY = 999, maxY = -999;
        for (int i = 0; i < r.cells.Count; i++)
        {
            minX = Mathf.Min(minX, r.cells[i].x);
            maxX = Mathf.Max(maxX, r.cells[i].x);
            minY = Mathf.Min(minY, r.cells[i].y);
            maxY = Mathf.Max(maxY, r.cells[i].y);
        }

        float width = (maxX - minX + 1) * cell;
        float height = (maxY - minY + 1) * cell;

        for (int i = 0; i < r.cells.Count; i++)
        {
            Vector2Int p = r.cells[i];
            GameObject outer = new GameObject("BlockCell", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            outer.transform.SetParent(rewardRoot, false);
            Image img = outer.GetComponent<Image>();
            img.color = new Color(.94f, .70f, .16f, 1f);
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
            ii.color = new Color(.12f, .14f, .18f, 1f);
            ii.raycastTarget = false;
            Anchor(ii.rectTransform, .13f, .13f, .87f, .87f);
        }
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
        dim.color = new Color(0f, 0f, 0f, .78f);
        dim.raycastTarget = false;
        Anchor(dim.rectTransform, 0f, 0f, 1f, 1f);

        machine = new GameObject("GachaMachine", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<RectTransform>();
        machine.SetParent(overlay.transform, false);
        Image body = machine.GetComponent<Image>();
        body.color = new Color(.075f, .085f, .11f, 1f);
        body.raycastTarget = false;
        machine.anchorMin = machine.anchorMax = new Vector2(.5f, .5f);
        machine.pivot = new Vector2(.5f, .5f);
        machine.sizeDelta = new Vector2(760f, 1240f);

        MakeFrame(machine, new Color(.84f, .60f, .13f, 1f), 18f);

        title = MakeText(machine, "X-TOWER  REWARD", 34, TextAnchor.MiddleCenter, true);
        title.color = new Color(1f, .84f, .39f, 1f);
        Anchor(title.rectTransform, .08f, .89f, .92f, .965f);

        RectTransform window = MakePanel(machine, "WheelWindow", new Color(.025f, .03f, .045f, 1f));
        Anchor(window, .16f, .47f, .84f, .86f);
        MakeFrame(window, new Color(.42f, .44f, .50f, 1f), 8f);

        wheel = new GameObject("Wheel", typeof(RectTransform)).GetComponent<RectTransform>();
        wheel.SetParent(window, false);
        wheel.anchorMin = wheel.anchorMax = new Vector2(.5f, .5f);
        wheel.sizeDelta = new Vector2(380f, 380f);
        wheel.anchoredPosition = Vector2.zero;

        for (int i = 0; i < 12; i++)
        {
            GameObject light = new GameObject("WheelLight" + i, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            light.transform.SetParent(wheel, false);
            Image li = light.GetComponent<Image>();
            li.color = i % 2 == 0 ? new Color(1f, .72f, .15f, 1f) : new Color(.95f, .95f, .88f, 1f);
            li.raycastTarget = false;
            RectTransform lr = li.rectTransform;
            lr.anchorMin = lr.anchorMax = new Vector2(.5f, .5f);
            lr.sizeDelta = new Vector2(26f, 62f);
            float a = i * 30f * Mathf.Deg2Rad;
            lr.anchoredPosition = new Vector2(Mathf.Sin(a), Mathf.Cos(a)) * 160f;
            lr.localRotation = Quaternion.Euler(0f, 0f, -i * 30f);
        }

        GameObject coreGo = new GameObject("WheelCore", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        coreGo.transform.SetParent(wheel, false);
        wheelCore = coreGo.GetComponent<Image>();
        wheelCore.color = new Color(.13f, .15f, .20f, 1f);
        wheelCore.raycastTarget = false;
        RectTransform cr = wheelCore.rectTransform;
        cr.anchorMin = cr.anchorMax = new Vector2(.5f, .5f);
        cr.sizeDelta = new Vector2(190f, 190f);

        Text x = MakeText(coreGo.transform, "X", 88, TextAnchor.MiddleCenter, true);
        x.color = new Color(1f, .78f, .20f, 1f);
        Anchor(x.rectTransform, 0f, 0f, 1f, 1f);

        RectTransform arrow = MakePanel(window, "Pointer", new Color(1f, .78f, .18f, 1f));
        arrow.anchorMin = arrow.anchorMax = new Vector2(.5f, 1f);
        arrow.pivot = new Vector2(.5f, 1f);
        arrow.sizeDelta = new Vector2(44f, 72f);
        arrow.anchoredPosition = new Vector2(0f, -7f);

        chute = MakePanel(machine, "Chute", new Color(.025f, .03f, .04f, 1f));
        Anchor(chute, .28f, .29f, .72f, .43f);
        MakeFrame(chute, new Color(.50f, .52f, .58f, 1f), 7f);

        Text chuteText = MakeText(chute, "BLOCK GEAR", 24, TextAnchor.UpperCenter, true);
        chuteText.color = new Color(.72f, .74f, .79f, 1f);
        Anchor(chuteText.rectTransform, .05f, .58f, .95f, .92f);

        rewardRoot = new GameObject("RewardBlock", typeof(RectTransform)).GetComponent<RectTransform>();
        rewardRoot.SetParent(machine, false);
        rewardRoot.anchorMin = rewardRoot.anchorMax = new Vector2(.5f, .5f);
        rewardRoot.sizeDelta = new Vector2(300f, 190f);
        rewardRoot.anchoredPosition = new Vector2(0f, -258f);

        nameText = MakeText(machine, "", 29, TextAnchor.MiddleCenter, true);
        nameText.color = Color.white;
        Anchor(nameText.rectTransform, .08f, .13f, .92f, .20f);

        statsText = MakeText(machine, "", 26, TextAnchor.MiddleCenter, true);
        statsText.color = new Color(.96f, .82f, .38f, 1f);
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
        string[] names = {"Top", "Bottom", "Left", "Right"};
        for (int i = 0; i < 4; i++)
        {
            RectTransform r = MakePanel(parent, names[i], color);
            if (i == 0) Anchor(r, 0f, 1f, 1f, 1f);
            else if (i == 1) Anchor(r, 0f, 0f, 1f, 0f);
            else if (i == 2) Anchor(r, 0f, 0f, 0f, 1f);
            else Anchor(r, 1f, 0f, 1f, 1f);

            if (i < 2) r.sizeDelta = new Vector2(0f, thickness);
            else r.sizeDelta = new Vector2(thickness, 0f);
        }
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

    sealed class Reward
    {
        public string displayName;
        public int cellCount;
        public int attack;
        public int defense;
        public int hp;
        public List<Vector2Int> cells;
    }
}
