using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public sealed class XTapBlacksmith : MonoBehaviour
{
    enum ForgeMode
    {
        Enhance,
        Synthesis,
        Dismantle
    }

    public bool IsOpen { get; private set; }

    RectTransform host;
    Font font;
    XTapInventory inventory;
    Action onClosed;

    GameObject overlay;
    RectTransform panel;
    RectTransform listContent;

    Text ruleText;
    Text selectionText;
    Text chanceText;
    Text resultText;

    Button enhanceTab;
    Button synthesisTab;
    Button dismantleTab;
    Button executeButton;

    ForgeMode mode = ForgeMode.Enhance;
    string targetId;
    readonly List<string> materialIds = new List<string>();

    public void Initialize(RectTransform parent, Font uiFont, XTapInventory bag, Action closed)
    {
        host = parent;
        font = uiFont;
        inventory = bag;
        onClosed = closed;

        BuildUi();
        overlay.SetActive(false);
    }

    public void Open()
    {
        if (overlay == null) return;

        IsOpen = true;
        overlay.SetActive(true);
        overlay.transform.SetAsLastSibling();
        ClearSelection();
        Refresh();
    }

    public void Close()
    {
        IsOpen = false;
        ClearSelection();
        if (overlay != null) overlay.SetActive(false);
        if (onClosed != null) onClosed();
    }

    void BuildUi()
    {
        overlay = new GameObject("BlacksmithOverlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        overlay.transform.SetParent(host, false);

        Image dim = overlay.GetComponent<Image>();
        dim.color = new Color(.015f, .010f, .008f, .96f);
        dim.raycastTarget = true;
        Anchor(dim.rectTransform, 0f, 0f, 1f, 1f);

        panel = new GameObject("ForgePanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<RectTransform>();
        panel.SetParent(overlay.transform, false);
        Anchor(panel, .025f, .025f, .975f, .975f);

        Image body = panel.GetComponent<Image>();
        body.color = new Color(.055f, .039f, .030f, .995f);
        body.raycastTarget = true;
        Frame(panel, new Color(.68f, .36f, .12f, 1f), 4f);

        Text title = MakeText(panel, "⚒  대 장 간", 42, TextAnchor.MiddleLeft, true);
        title.color = new Color(1f, .72f, .28f, 1f);
        Anchor(title.rectTransform, .05f, .925f, .70f, .985f);

        Text sub = MakeText(panel, "FORGE · 블록을 태워 더 강한 블록으로", 18, TextAnchor.MiddleLeft, false);
        sub.color = new Color(.72f, .62f, .54f, 1f);
        Anchor(sub.rectTransform, .055f, .890f, .80f, .928f);

        Button close = MakeButton(panel, "닫기", 22, new Color(.16f, .09f, .065f, 1f));
        Anchor(close.GetComponent<RectTransform>(), .79f, .932f, .94f, .980f);
        close.onClick.AddListener(Close);

        enhanceTab = MakeButton(panel, "강화", 24, new Color(.20f, .105f, .055f, 1f));
        synthesisTab = MakeButton(panel, "합성", 24, new Color(.12f, .08f, .07f, 1f));
        dismantleTab = MakeButton(panel, "분해", 24, new Color(.12f, .08f, .07f, 1f));

        Anchor(enhanceTab.GetComponent<RectTransform>(), .05f, .825f, .33f, .875f);
        Anchor(synthesisTab.GetComponent<RectTransform>(), .36f, .825f, .64f, .875f);
        Anchor(dismantleTab.GetComponent<RectTransform>(), .67f, .825f, .95f, .875f);

        enhanceTab.onClick.AddListener(delegate { SetMode(ForgeMode.Enhance); });
        synthesisTab.onClick.AddListener(delegate { SetMode(ForgeMode.Synthesis); });
        dismantleTab.onClick.AddListener(delegate { SetMode(ForgeMode.Dismantle); });

        ruleText = MakeText(panel, "", 20, TextAnchor.MiddleLeft, false);
        ruleText.color = new Color(.91f, .84f, .72f, 1f);
        ruleText.resizeTextForBestFit = true;
        ruleText.resizeTextMinSize = 14;
        ruleText.resizeTextMaxSize = 20;
        Anchor(ruleText.rectTransform, .055f, .742f, .945f, .815f);

        RectTransform listFrame = MakePanel(panel, "ForgeItems", new Color(.025f, .022f, .022f, 1f));
        Anchor(listFrame, .05f, .315f, .95f, .735f);
        Frame(listFrame, new Color(.31f, .22f, .17f, 1f), 2f);

        GameObject scrollGo = new GameObject("ForgeScroll", typeof(RectTransform), typeof(ScrollRect));
        scrollGo.transform.SetParent(listFrame, false);
        RectTransform scrollRectTransform = scrollGo.GetComponent<RectTransform>();
        Anchor(scrollRectTransform, .015f, .015f, .985f, .985f);

        GameObject viewportGo = new GameObject("Viewport", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(RectMask2D));
        viewportGo.transform.SetParent(scrollGo.transform, false);
        RectTransform viewport = viewportGo.GetComponent<RectTransform>();
        Anchor(viewport, 0f, 0f, 1f, 1f);
        viewportGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, .01f);

        GameObject contentGo = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        contentGo.transform.SetParent(viewport, false);
        listContent = contentGo.GetComponent<RectTransform>();
        listContent.anchorMin = new Vector2(0f, 1f);
        listContent.anchorMax = new Vector2(1f, 1f);
        listContent.pivot = new Vector2(.5f, 1f);
        listContent.offsetMin = Vector2.zero;
        listContent.offsetMax = Vector2.zero;

        VerticalLayoutGroup layout = contentGo.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(8, 8, 8, 8);
        layout.spacing = 8f;
        layout.childControlHeight = false;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;

        ContentSizeFitter fitter = contentGo.GetComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        ScrollRect scroll = scrollGo.GetComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.content = listContent;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Elastic;
        scroll.elasticity = .08f;
        scroll.inertia = true;

        selectionText = MakeText(panel, "", 20, TextAnchor.MiddleLeft, true);
        selectionText.color = new Color(.96f, .88f, .72f, 1f);
        selectionText.resizeTextForBestFit = true;
        selectionText.resizeTextMinSize = 14;
        selectionText.resizeTextMaxSize = 20;
        Anchor(selectionText.rectTransform, .055f, .235f, .945f, .305f);

        chanceText = MakeText(panel, "", 28, TextAnchor.MiddleCenter, true);
        chanceText.color = new Color(1f, .66f, .20f, 1f);
        Anchor(chanceText.rectTransform, .055f, .185f, .945f, .235f);

        resultText = MakeText(panel, "블록을 선택하세요.", 20, TextAnchor.MiddleCenter, false);
        resultText.color = new Color(.88f, .82f, .74f, 1f);
        resultText.resizeTextForBestFit = true;
        resultText.resizeTextMinSize = 14;
        resultText.resizeTextMaxSize = 20;
        Anchor(resultText.rectTransform, .055f, .125f, .945f, .185f);

        Button clear = MakeButton(panel, "선택 초기화", 20, new Color(.095f, .075f, .065f, 1f));
        Anchor(clear.GetComponent<RectTransform>(), .055f, .050f, .42f, .110f);
        clear.onClick.AddListener(delegate
        {
            ClearSelection();
            resultText.text = "선택을 초기화했습니다.";
            Refresh();
        });

        executeButton = MakeButton(panel, "작업 실행", 24, new Color(.36f, .15f, .055f, 1f));
        Anchor(executeButton.GetComponent<RectTransform>(), .58f, .050f, .945f, .110f);
        executeButton.onClick.AddListener(Execute);

        SetMode(ForgeMode.Enhance);
    }

    void SetMode(ForgeMode next)
    {
        mode = next;
        ClearSelection();
        if (resultText != null) resultText.text = "블록을 선택하세요.";
        Refresh();
    }

    void ClearSelection()
    {
        targetId = null;
        materialIds.Clear();
    }

    void Refresh()
    {
        if (panel == null || inventory == null) return;

        RefreshTabs();
        RefreshRules();
        RebuildItemList();
        RefreshSelectionInfo();
    }

    void RefreshTabs()
    {
        SetButtonTone(enhanceTab, mode == ForgeMode.Enhance);
        SetButtonTone(synthesisTab, mode == ForgeMode.Synthesis);
        SetButtonTone(dismantleTab, mode == ForgeMode.Dismantle);
    }

    void SetButtonTone(Button button, bool selected)
    {
        if (button == null) return;
        Image image = button.targetGraphic as Image;
        if (image != null)
            image.color = selected
                ? new Color(.34f, .16f, .055f, 1f)
                : new Color(.105f, .072f, .060f, 1f);
    }

    void RefreshRules()
    {
        if (ruleText == null) return;

        if (mode == ForgeMode.Enhance)
        {
            ruleText.text =
                "강화 · 대상 1개 + 재료 블록 · 재료 1개당 성공률 +10%\n" +
                "성공: 공 +1 / 체 +5 / 짝수 강화마다 방 +1 · 최대 +10 · 재료는 성공/실패 모두 소모";
        }
        else if (mode == ForgeMode.Synthesis)
        {
            ruleText.text =
                "합성 · 대상 1개 + 재료 1개 · 성공률 1%\n" +
                "성공하면 재료의 현재 공/방/체를 대상에 그대로 더함 · 실패해도 재료 소모";
        }
        else
        {
            ruleText.text =
                "분해 · 재료 최대 10개 · 1개=10% … 10개=100%\n" +
                "성공하면 가방 배치칸 +1 영구 확장 · 실패해도 선택 재료 전부 소모";
        }
    }

    void RebuildItemList()
    {
        for (int i = listContent.childCount - 1; i >= 0; i--)
            Destroy(listContent.GetChild(i).gameObject);

        List<XTapGearBlockData> items = inventory.GetForgeItems();

        if (items.Count == 0)
        {
            Text empty = MakeText(listContent, "사용할 블록이 없습니다.", 21, TextAnchor.MiddleCenter, false);
            empty.color = new Color(.55f, .50f, .47f, 1f);
            LayoutElement le = empty.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = 90f;
            return;
        }

        for (int i = 0; i < items.Count; i++)
        {
            XTapGearBlockData item = items[i];
            if (item == null || string.IsNullOrEmpty(item.id)) continue;

            string id = item.id;
            bool isTarget = targetId == id;
            bool isMaterial = materialIds.Contains(id);

            GameObject row = new GameObject("ForgeItem_" + id, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(LayoutElement));
            row.transform.SetParent(listContent, false);

            Image bg = row.GetComponent<Image>();
            if (isTarget)
                bg.color = new Color(.32f, .17f, .055f, 1f);
            else if (isMaterial)
                bg.color = new Color(.17f, .20f, .13f, 1f);
            else
                bg.color = new Color(.075f, .065f, .062f, 1f);

            LayoutElement le = row.GetComponent<LayoutElement>();
            le.preferredHeight = 92f;

            string prefix = isTarget ? "[대상] " : (isMaterial ? "[재료] " : "");
            string enhance = item.enhanceLevel > 0 ? " +" + item.enhanceLevel : "";
            string line1 = prefix + "[" + LocationName(item) + "] " + item.displayName + enhance;
            string line2 = item.cellCount + "칸 · 공 " + item.attack + " / 방 " + item.defense + " / 체 " + item.hp;

            Text t = MakeText(row.transform, line1 + "\n" + line2, 19, TextAnchor.MiddleLeft, isTarget || isMaterial);
            t.color = isTarget
                ? new Color(1f, .78f, .34f, 1f)
                : (isMaterial ? new Color(.80f, .90f, .66f, 1f) : new Color(.91f, .87f, .81f, 1f));
            t.resizeTextForBestFit = true;
            t.resizeTextMinSize = 13;
            t.resizeTextMaxSize = 19;
            Anchor(t.rectTransform, .04f, .08f, .96f, .92f);

            Button b = row.GetComponent<Button>();
            b.targetGraphic = bg;
            string capturedId = id;
            b.onClick.AddListener(delegate { OnItemPressed(capturedId); });
        }
    }

    string LocationName(XTapGearBlockData item)
    {
        if (item == null) return "없음";
        if (item.location == XTapGearBlockData.LocationBag)
            return item.bagOwnerCharacterId == 0
                ? "플레이어 가방"
                : "캐릭터 " + item.bagOwnerCharacterId + " 가방";
        if (item.location == XTapGearBlockData.LocationHeld) return "소지";
        return "바닥";
    }

    void OnItemPressed(string id)
    {
        if (string.IsNullOrEmpty(id) || inventory.FindForgeItem(id) == null) return;

        if (mode == ForgeMode.Dismantle)
        {
            ToggleMaterial(id, 10);
            Refresh();
            return;
        }

        if (string.IsNullOrEmpty(targetId))
        {
            targetId = id;
            materialIds.Remove(id);
            Refresh();
            return;
        }

        if (targetId == id)
        {
            targetId = null;
            materialIds.Remove(id);
            Refresh();
            return;
        }

        ToggleMaterial(id, mode == ForgeMode.Synthesis ? 1 : 10);
        Refresh();
    }

    void ToggleMaterial(string id, int maxCount)
    {
        if (materialIds.Contains(id))
        {
            materialIds.Remove(id);
            return;
        }

        if (materialIds.Count >= maxCount)
        {
            if (resultText != null)
                resultText.text = "재료는 최대 " + maxCount + "개까지 선택할 수 있습니다.";
            return;
        }

        materialIds.Add(id);
    }

    void RefreshSelectionInfo()
    {
        XTapGearBlockData target = inventory.FindForgeItem(targetId);

        if (mode == ForgeMode.Enhance)
        {
            string targetText = target == null
                ? "대상 없음"
                : target.displayName + " +" + target.enhanceLevel;

            int chance = Mathf.Clamp(materialIds.Count * 10, 0, 100);
            selectionText.text = "대상: " + targetText + "   /   재료 " + materialIds.Count + "개";
            chanceText.text = "강화 성공률  " + chance + "%";
            executeButton.interactable = target != null && materialIds.Count > 0 && target.enhanceLevel < 10;
        }
        else if (mode == ForgeMode.Synthesis)
        {
            string targetText = target == null ? "대상 없음" : target.displayName;
            string materialText = materialIds.Count == 0
                ? "재료 없음"
                : ItemName(materialIds[0]);

            selectionText.text = "대상: " + targetText + "   /   재료: " + materialText;
            chanceText.text = "합성 성공률  1%";
            executeButton.interactable = target != null && materialIds.Count == 1;
        }
        else
        {
            int chance = Mathf.Clamp(materialIds.Count * 10, 0, 100);
            selectionText.text = "분해 재료 " + materialIds.Count + "/10개   /   현재 가방 " + inventory.GridCapacity + "칸";
            chanceText.text = "가방 확장 성공률  " + chance + "%";
            executeButton.interactable = materialIds.Count > 0;
        }
    }

    string ItemName(string id)
    {
        XTapGearBlockData item = inventory.FindForgeItem(id);
        return item == null ? "없음" : item.displayName;
    }

    void Execute()
    {
        if (inventory == null) return;

        if (mode == ForgeMode.Enhance)
            ExecuteEnhance();
        else if (mode == ForgeMode.Synthesis)
            ExecuteSynthesis();
        else
            ExecuteDismantle();

        Refresh();
    }

    void ExecuteEnhance()
    {
        XTapGearBlockData target = inventory.FindForgeItem(targetId);

        if (target == null || materialIds.Count == 0)
        {
            resultText.text = "대상과 재료를 선택하세요.";
            return;
        }

        if (target.enhanceLevel >= 10)
        {
            resultText.text = "이미 +10 최대 강화입니다.";
            return;
        }

        int materialCount = materialIds.Count;
        int chance = Mathf.Clamp(materialCount * 10, 0, 100);
        bool success = UnityEngine.Random.Range(0f, 100f) < chance;

        ConsumeMaterials();

        if (success)
        {
            target.enhanceLevel++;
            target.attack += 1;
            target.hp += 5;

            if (target.enhanceLevel % 2 == 0)
                target.defense += 1;

            inventory.CommitForgeChanges();
            resultText.text =
                "강화 성공!  +" + target.enhanceLevel +
                "   공 " + target.attack + " / 방 " + target.defense + " / 체 " + target.hp;
        }
        else
        {
            inventory.CommitForgeChanges();
            resultText.text = "강화 실패. 재료 " + materialCount + "개가 소모됐습니다.";
        }

        ClearSelection();
    }

    void ExecuteSynthesis()
    {
        XTapGearBlockData target = inventory.FindForgeItem(targetId);

        if (target == null || materialIds.Count != 1)
        {
            resultText.text = "대상 1개와 재료 1개를 선택하세요.";
            return;
        }

        XTapGearBlockData material = inventory.FindForgeItem(materialIds[0]);
        if (material == null)
        {
            resultText.text = "재료 블록을 찾을 수 없습니다.";
            ClearSelection();
            return;
        }

        int addAttack = material.attack;
        int addDefense = material.defense;
        int addHp = material.hp;
        string consumedName = material.displayName;

        bool success = UnityEngine.Random.Range(0f, 100f) < 1f;
        inventory.RemoveForgeItem(material.id);

        if (success)
        {
            target.attack += addAttack;
            target.defense += addDefense;
            target.hp += addHp;
            inventory.CommitForgeChanges();

            resultText.text =
                "합성 성공!  " + consumedName + " 능력을 흡수했습니다.  " +
                "공 +" + addAttack + " / 방 +" + addDefense + " / 체 +" + addHp;
        }
        else
        {
            inventory.CommitForgeChanges();
            resultText.text = "합성 실패. " + consumedName + "이(가) 소모됐습니다.";
        }

        ClearSelection();
    }

    void ExecuteDismantle()
    {
        if (materialIds.Count <= 0)
        {
            resultText.text = "분해할 재료를 선택하세요.";
            return;
        }

        int count = materialIds.Count;
        int chance = Mathf.Clamp(count * 10, 0, 100);
        bool success = UnityEngine.Random.Range(0f, 100f) < chance;

        ConsumeMaterials();

        if (success)
        {
            inventory.AddGridCellExpansion(1);
            resultText.text =
                "분해 성공! 가방 배치칸 +1.  현재 " + inventory.GridCapacity + "칸";
        }
        else
        {
            inventory.CommitForgeChanges();
            resultText.text =
                "분해 실패. 재료 " + count + "개가 모두 소모됐습니다.";
        }

        ClearSelection();
    }

    void ConsumeMaterials()
    {
        string[] ids = materialIds.ToArray();

        for (int i = 0; i < ids.Length; i++)
        {
            if (ids[i] == targetId) continue;
            inventory.RemoveForgeItem(ids[i]);
        }
    }

    RectTransform MakePanel(Transform parent, string name, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);

        Image image = go.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image.rectTransform;
    }

    Button MakeButton(Transform parent, string label, int fontSize, Color color)
    {
        GameObject go = new GameObject(label + "Button", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);

        Image bg = go.GetComponent<Image>();
        bg.color = color;

        Text t = MakeText(go.transform, label, fontSize, TextAnchor.MiddleCenter, true);
        t.color = new Color(.96f, .89f, .78f, 1f);
        Anchor(t.rectTransform, .04f, .04f, .96f, .96f);

        Button b = go.GetComponent<Button>();
        b.targetGraphic = bg;

        ColorBlock cb = b.colors;
        cb.normalColor = Color.white;
        cb.highlightedColor = new Color(1f, .88f, .70f, 1f);
        cb.pressedColor = new Color(.66f, .48f, .36f, 1f);
        cb.selectedColor = Color.white;
        cb.fadeDuration = .06f;
        b.colors = cb;

        return b;
    }

    Text MakeText(Transform parent, string value, int size, TextAnchor align, bool bold)
    {
        GameObject go = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        go.transform.SetParent(parent, false);

        Text t = go.GetComponent<Text>();
        t.text = value;
        t.font = font;
        t.fontSize = size;
        t.alignment = align;
        t.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Truncate;
        t.raycastTarget = false;
        return t;
    }

    void Frame(RectTransform parent, Color color, float thickness)
    {
        RectTransform top = MakePanel(parent, "FrameTop", color);
        Anchor(top, 0f, 1f, 1f, 1f);
        top.sizeDelta = new Vector2(0f, thickness);

        RectTransform bottom = MakePanel(parent, "FrameBottom", color);
        Anchor(bottom, 0f, 0f, 1f, 0f);
        bottom.sizeDelta = new Vector2(0f, thickness);

        RectTransform left = MakePanel(parent, "FrameLeft", color);
        Anchor(left, 0f, 0f, 0f, 1f);
        left.sizeDelta = new Vector2(thickness, 0f);

        RectTransform right = MakePanel(parent, "FrameRight", color);
        Anchor(right, 1f, 0f, 1f, 1f);
        right.sizeDelta = new Vector2(thickness, 0f);
    }

    static void Anchor(RectTransform r, float x1, float y1, float x2, float y2)
    {
        r.anchorMin = new Vector2(x1, y1);
        r.anchorMax = new Vector2(x2, y2);
        r.offsetMin = Vector2.zero;
        r.offsetMax = Vector2.zero;
    }
}
