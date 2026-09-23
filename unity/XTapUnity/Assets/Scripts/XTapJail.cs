using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public sealed class XTapJail : MonoBehaviour
{
    const float UiFontScale = 3.84f;
    public bool IsOpen { get; private set; }

    RectTransform host;
    Font font;
    XTapInventory inventory;
    Action onClosed;

    GameObject overlay;
    RectTransform listContent;
    Text detailText;
    Button openBagButton;

    int selectedCharacterId;

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

        if (selectedCharacterId <= 0 || !IsCaptured(selectedCharacterId))
            selectedCharacterId = FirstCapturedCharacter();

        Refresh();
    }

    public void Close()
    {
        IsOpen = false;
        if (overlay != null) overlay.SetActive(false);
        if (onClosed != null) onClosed();
    }

    public void RefreshIfOpen()
    {
        if (IsOpen) Refresh();
    }

    void BuildUi()
    {
        overlay = new GameObject("JailOverlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        overlay.transform.SetParent(host, false);

        Image dim = overlay.GetComponent<Image>();
        dim.color = new Color(.010f, .012f, .017f, .985f);
        dim.raycastTarget = true;
        Anchor(dim.rectTransform, 0f, 0f, 1f, 1f);

        RectTransform panel = new GameObject("JailPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<RectTransform>();
        panel.SetParent(overlay.transform, false);
        Anchor(panel, .018f, .018f, .982f, .982f);

        Image body = panel.GetComponent<Image>();
        body.color = new Color(.030f, .034f, .044f, .998f);
        body.raycastTarget = true;
        Frame(panel, new Color(.32f, .41f, .56f, 1f), 4f);

        Text title = MakeText(panel, "감옥", 34, TextAnchor.MiddleLeft, true);
        title.color = new Color(.86f, .91f, 1f, 1f);
        Anchor(title.rectTransform, .055f, .902f, .62f, .985f);

        Button close = MakeButton(panel, "닫기", 18, new Color(.075f, .085f, .105f, 1f));
        Anchor(close.GetComponent<RectTransform>(), .76f, .912f, .945f, .975f);
        close.onClick.AddListener(Close);

        Text sub = MakeText(panel, "포획 캐릭터 · 각 캐릭터 전용 8×3 가방", 15, TextAnchor.MiddleLeft, true);
        sub.color = new Color(.64f, .73f, .88f, 1f);
        sub.resizeTextForBestFit = true;
        sub.resizeTextMinSize = 42;
        sub.resizeTextMaxSize = 58;
        Anchor(sub.rectTransform, .06f, .842f, .94f, .895f);

        RectTransform listFrame = MakePanel(panel, "CharacterList", new Color(.018f, .020f, .027f, 1f));
        Anchor(listFrame, .05f, .325f, .95f, .825f);
        Frame(listFrame, new Color(.22f, .29f, .40f, 1f), 3f);

        GameObject scrollGo = new GameObject("JailScroll", typeof(RectTransform), typeof(ScrollRect));
        scrollGo.transform.SetParent(listFrame, false);
        Anchor(scrollGo.GetComponent<RectTransform>(), .015f, .015f, .985f, .985f);

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
        layout.padding = new RectOffset(12, 12, 12, 12);
        layout.spacing = 12f;
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

        detailText = MakeText(panel, "", 16, TextAnchor.MiddleCenter, true);
        detailText.color = new Color(.90f, .94f, 1f, 1f);
        detailText.resizeTextForBestFit = true;
        detailText.resizeTextMinSize = 44;
        detailText.resizeTextMaxSize = 62;
        Anchor(detailText.rectTransform, .06f, .185f, .94f, .305f);

        openBagButton = MakeButton(panel, "선택 캐릭터 가방 열기", 18, new Color(.12f, .18f, .27f, 1f));
        Anchor(openBagButton.GetComponent<RectTransform>(), .055f, .055f, .70f, .145f);
        openBagButton.onClick.AddListener(OpenSelectedBag);

        Button closeBottom = MakeButton(panel, "닫기", 17, new Color(.075f, .085f, .105f, 1f));
        Anchor(closeBottom.GetComponent<RectTransform>(), .73f, .055f, .945f, .145f);
        closeBottom.onClick.AddListener(Close);
    }

    void Refresh()
    {
        if (listContent == null || inventory == null) return;

        for (int i = listContent.childCount - 1; i >= 0; i--)
            Destroy(listContent.GetChild(i).gameObject);

        for (int characterId = 1; characterId <= 10; characterId++)
        {
            bool captured = IsCaptured(characterId);
            bool selected = selectedCharacterId == characterId;
            int capturedId = characterId;

            GameObject row = new GameObject(
                "JailCharacter_" + characterId,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Button),
                typeof(LayoutElement)
            );
            row.transform.SetParent(listContent, false);

            Image bg = row.GetComponent<Image>();
            if (!captured)
                bg.color = new Color(.045f, .048f, .056f, .88f);
            else if (selected)
                bg.color = new Color(.12f, .21f, .34f, 1f);
            else
                bg.color = new Color(.075f, .10f, .145f, 1f);

            LayoutElement le = row.GetComponent<LayoutElement>();
            le.preferredHeight = 180f;

            string state = captured ? "포획됨" : "미포획";
            string stats = captured
                ? "가방 " + inventory.GetEquippedCellCount(characterId) + "/24칸  ·  공+" +
                  XTapStatFormat.Compact(inventory.GetEquippedAttack(characterId)) + "  방+" +
                  XTapStatFormat.Compact(inventory.GetEquippedDefense(characterId)) + "  체+" +
                  XTapStatFormat.Compact(inventory.GetEquippedHp(characterId))
                : "0% 룰렛에서 포획 성공 시 해금";

            Text t = MakeText(
                row.transform,
                "캐릭터 " + characterId + "   " + state + "\n" + stats,
                16,
                TextAnchor.MiddleLeft,
                captured
            );
            t.color = captured
                ? new Color(.88f, .92f, 1f, 1f)
                : new Color(.43f, .45f, .50f, 1f);
            t.resizeTextForBestFit = true;
            t.resizeTextMinSize = 44;
            t.resizeTextMaxSize = 62;
            Anchor(t.rectTransform, .045f, .10f, .955f, .90f);

            Button b = row.GetComponent<Button>();
            b.targetGraphic = bg;
            b.interactable = captured;
            b.onClick.AddListener(delegate
            {
                selectedCharacterId = capturedId;
                Refresh();
            });
        }

        RefreshDetail();
    }

    void RefreshDetail()
    {
        if (selectedCharacterId <= 0 || !IsCaptured(selectedCharacterId))
        {
            detailText.text = "아직 포획된 캐릭터가 없습니다.";
            openBagButton.interactable = false;
            return;
        }

        detailText.text =
            "캐릭터 " + selectedCharacterId + " 전용 가방   8 × 3 / 24칸\n" +
            "장착 합계  공 +" + XTapStatFormat.Compact(inventory.GetEquippedAttack(selectedCharacterId)) +
            "   방 +" + XTapStatFormat.Compact(inventory.GetEquippedDefense(selectedCharacterId)) +
            "   체 +" + XTapStatFormat.Compact(inventory.GetEquippedHp(selectedCharacterId));

        openBagButton.interactable = true;
    }

    void OpenSelectedBag()
    {
        if (selectedCharacterId <= 0 || !IsCaptured(selectedCharacterId)) return;
        inventory.OpenCharacterBag(selectedCharacterId);
    }

    int FirstCapturedCharacter()
    {
        for (int i = 1; i <= 10; i++)
            if (IsCaptured(i))
                return i;

        return 0;
    }

    bool IsCaptured(int characterId)
    {
        return PlayerPrefs.GetInt("xtap_captured_char_" + characterId, 0) == 1;
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

        RectTransform accent = MakePanel(go.transform, "TypeAccent", new Color(.38f, .56f, .82f, .90f));
        Anchor(accent, .18f, .90f, .82f, .925f);

        Text t = MakeText(go.transform, label, fontSize + 1, TextAnchor.MiddleCenter, true);
        t.color = new Color(.94f, .96f, 1f, 1f);
        Outline outline = t.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, .88f);
        outline.effectDistance = new Vector2(2f, -2f);
        Anchor(t.rectTransform, .04f, .04f, .96f, .90f);

        Button b = go.GetComponent<Button>();
        b.targetGraphic = bg;

        ColorBlock cb = b.colors;
        cb.normalColor = Color.white;
        cb.highlightedColor = new Color(.84f, .90f, 1f, 1f);
        cb.pressedColor = new Color(.52f, .62f, .78f, 1f);
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
        t.fontSize = Mathf.RoundToInt(size * UiFontScale);
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
