using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public sealed class XTapJail : MonoBehaviour
{
    const float UiFontScale = 1.28f;
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
        dim.color = new Color(.010f, .012f, .017f, .975f);
        dim.raycastTarget = true;
        Anchor(dim.rectTransform, 0f, 0f, 1f, 1f);

        RectTransform panel = new GameObject("JailPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<RectTransform>();
        panel.SetParent(overlay.transform, false);
        Anchor(panel, .025f, .025f, .975f, .975f);

        Image body = panel.GetComponent<Image>();
        body.color = new Color(.030f, .034f, .044f, .995f);
        body.raycastTarget = true;
        Frame(panel, new Color(.30f, .37f, .50f, 1f), 4f);

        Text title = MakeText(panel, "▥  감 옥", 42, TextAnchor.MiddleLeft, true);
        title.color = new Color(.84f, .88f, .96f, 1f);
        Anchor(title.rectTransform, .05f, .925f, .72f, .985f);

        Text sub = MakeText(panel, "포획 캐릭터 · 캐릭터별 독립 8 × 3 장비 가방", 19, TextAnchor.MiddleLeft, false);
        sub.color = new Color(.58f, .66f, .78f, 1f);
        Anchor(sub.rectTransform, .055f, .885f, .80f, .928f);

        Button close = MakeButton(panel, "닫기", 22, new Color(.075f, .085f, .105f, 1f));
        Anchor(close.GetComponent<RectTransform>(), .79f, .932f, .94f, .980f);
        close.onClick.AddListener(Close);

        RectTransform listFrame = MakePanel(panel, "CharacterList", new Color(.018f, .020f, .027f, 1f));
        Anchor(listFrame, .05f, .34f, .95f, .865f);
        Frame(listFrame, new Color(.19f, .24f, .33f, 1f), 2f);

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

        detailText = MakeText(panel, "", 24, TextAnchor.MiddleCenter, true);
        detailText.color = new Color(.88f, .91f, .98f, 1f);
        detailText.resizeTextForBestFit = true;
        detailText.resizeTextMinSize = 20;
        detailText.resizeTextMaxSize = 31;
        Anchor(detailText.rectTransform, .055f, .185f, .945f, .315f);

        openBagButton = MakeButton(panel, "선택 캐릭터 가방 열기", 25, new Color(.12f, .18f, .27f, 1f));
        Anchor(openBagButton.GetComponent<RectTransform>(), .20f, .075f, .80f, .155f);
        openBagButton.onClick.AddListener(OpenSelectedBag);
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
            le.preferredHeight = 100f;

            string state = captured ? "포획됨" : "미포획";
            string stats = captured
                ? "가방 " + inventory.GetEquippedCellCount(characterId) + "/24칸  ·  공+" +
                  inventory.GetEquippedAttack(characterId) + "  방+" +
                  inventory.GetEquippedDefense(characterId) + "  체+" +
                  inventory.GetEquippedHp(characterId)
                : "0% 룰렛에서 포획 성공 시 해금";

            Text t = MakeText(
                row.transform,
                characterId + "층 캐릭터   [" + state + "]\n" + stats,
                20,
                TextAnchor.MiddleLeft,
                captured
            );
            t.color = captured
                ? new Color(.88f, .92f, 1f, 1f)
                : new Color(.43f, .45f, .50f, 1f);
            t.resizeTextForBestFit = true;
            t.resizeTextMinSize = 18;
            t.resizeTextMaxSize = 26;
            Anchor(t.rectTransform, .04f, .08f, .96f, .92f);

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
            "장착 합계  공 +" + inventory.GetEquippedAttack(selectedCharacterId) +
            "   방 +" + inventory.GetEquippedDefense(selectedCharacterId) +
            "   체 +" + inventory.GetEquippedHp(selectedCharacterId);

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

        Text t = MakeText(go.transform, label, fontSize, TextAnchor.MiddleCenter, true);
        t.color = new Color(.92f, .94f, .98f, 1f);
        Anchor(t.rectTransform, .04f, .04f, .96f, .96f);

        Button b = go.GetComponent<Button>();
        b.targetGraphic = bg;

        ColorBlock cb = b.colors;
        cb.normalColor = Color.white;
        cb.highlightedColor = new Color(.84f, .90f, 1f, 1f);
        cb.pressedColor = new Color(.56f, .65f, .78f, 1f);
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
