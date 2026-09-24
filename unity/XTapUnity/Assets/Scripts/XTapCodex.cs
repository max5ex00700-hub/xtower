using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public sealed class XTapCodex : MonoBehaviour
{
    const int CharacterCount = 10;
    const int ImagesPerPage = 6;
    const string CharacterKeyPrefix = "xtap_codex_char_";
    const string ImageKeyPrefix = "xtap_codex_img_";

    RectTransform host;
    Font font;
    XTapOriginalApkAssets assets;
    XTapInventory inventory;
    Action onClosed;

    GameObject overlay;
    GameObject listRoot;
    GameObject galleryRoot;
    Text galleryTitle;
    Text galleryProgress;
    Text galleryPage;
    RectTransform galleryGrid;

    int selectedCharacter = 1;
    int galleryPageIndex;

    public bool IsOpen { get; private set; }

    public void Initialize(
        RectTransform parent,
        Font uiFont,
        XTapOriginalApkAssets assetProvider,
        XTapInventory bag,
        Action closed)
    {
        host = parent;
        font = uiFont;
        assets = assetProvider;
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
        ShowCharacterList();
    }

    public void Close()
    {
        IsOpen = false;
        if (overlay != null) overlay.SetActive(false);
        if (onClosed != null) onClosed();
    }

    public static void MarkImageDiscovered(int characterId, string imageCode, XTapInventory bag)
    {
        int c = NormalizeCharacter(characterId);
        if (c <= 0 || string.IsNullOrEmpty(imageCode)) return;

        string imageKey = ImageKey(c, imageCode);
        bool changed = false;

        if (PlayerPrefs.GetInt(CharacterKey(c), 0) != 1)
        {
            PlayerPrefs.SetInt(CharacterKey(c), 1);
            changed = true;
        }

        if (PlayerPrefs.GetInt(imageKey, 0) != 1)
        {
            PlayerPrefs.SetInt(imageKey, 1);
            changed = true;
        }

        if (changed)
            PlayerPrefs.Save();

        if (bag != null && IsCharacterComplete(c))
            bag.GrantCodexCompletionReward(c);
    }

    public static bool IsCharacterDiscovered(int characterId)
    {
        int c = NormalizeCharacter(characterId);
        return c > 0 && PlayerPrefs.GetInt(CharacterKey(c), 0) == 1;
    }

    public static bool IsImageDiscovered(int characterId, string imageCode)
    {
        int c = NormalizeCharacter(characterId);
        return c > 0 &&
               !string.IsNullOrEmpty(imageCode) &&
               PlayerPrefs.GetInt(ImageKey(c, imageCode), 0) == 1;
    }

    public static bool IsCharacterComplete(int characterId)
    {
        int c = NormalizeCharacter(characterId);
        if (c <= 0) return false;

        List<string> codes = AllImageCodes();
        for (int i = 0; i < codes.Count; i++)
            if (!IsImageDiscovered(c, codes[i]))
                return false;

        return true;
    }

    static int NormalizeCharacter(int characterId)
    {
        if (characterId <= 0) return 0;
        return ((characterId - 1) % CharacterCount) + 1;
    }

    static string CharacterKey(int characterId)
    {
        return CharacterKeyPrefix + characterId;
    }

    static string ImageKey(int characterId, string imageCode)
    {
        return ImageKeyPrefix + characterId + "_" + imageCode;
    }

    static List<string> AllImageCodes()
    {
        List<string> codes = new List<string>(41);
        string[] prefixes = {"p", "k", "b", "d"};

        for (int p = 0; p < prefixes.Length; p++)
            for (int i = 0; i < 10; i++)
                codes.Add(prefixes[p] + i.ToString("00"));

        codes.Add("cap");
        return codes;
    }

    int DiscoveredImageCount(int characterId)
    {
        int count = 0;
        List<string> codes = AllImageCodes();
        for (int i = 0; i < codes.Count; i++)
            if (IsImageDiscovered(characterId, codes[i]))
                count++;
        return count;
    }

    Sprite SpriteFor(int characterId, string code)
    {
        if (assets == null || !assets.Ready) return null;
        return assets.GetSprite("assets/f" + characterId + "_" + code + ".jpg");
    }

    Sprite FirstDiscoveredSprite(int characterId)
    {
        List<string> codes = AllImageCodes();
        for (int i = 0; i < codes.Count; i++)
        {
            if (!IsImageDiscovered(characterId, codes[i])) continue;
            Sprite s = SpriteFor(characterId, codes[i]);
            if (s != null) return s;
        }
        return null;
    }

    void BuildUi()
    {
        overlay = new GameObject("CodexOverlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        overlay.transform.SetParent(host, false);
        Image dim = overlay.GetComponent<Image>();
        dim.color = new Color(.008f, .006f, .010f, .985f);
        dim.raycastTarget = true;
        Anchor(dim.rectTransform, 0f, 0f, 1f, 1f);

        RectTransform header = MakePanel(overlay.transform, "CodexHeader", new Color(.030f, .020f, .018f, .99f));
        Anchor(header, .025f, .900f, .975f, .985f);
        Frame(header, new Color(.66f, .43f, .20f, 1f), 3f);

        Text title = MakeText(header, "X탑 도감", 25, TextAnchor.MiddleCenter, true);
        title.color = new Color(1f, .84f, .48f, 1f);
        Anchor(title.rectTransform, .15f, .05f, .85f, .95f);

        Button close = MakeButton(header, "닫기", 14);
        Anchor(close.GetComponent<RectTransform>(), .82f, .15f, .97f, .85f);
        close.onClick.AddListener(Close);

        listRoot = new GameObject("CharacterList", typeof(RectTransform));
        listRoot.transform.SetParent(overlay.transform, false);
        Anchor(listRoot.GetComponent<RectTransform>(), .035f, .035f, .965f, .885f);

        galleryRoot = new GameObject("CharacterGallery", typeof(RectTransform));
        galleryRoot.transform.SetParent(overlay.transform, false);
        Anchor(galleryRoot.GetComponent<RectTransform>(), .035f, .035f, .965f, .885f);

        Button back = MakeButton(galleryRoot.transform, "목록", 14);
        Anchor(back.GetComponent<RectTransform>(), .02f, .91f, .18f, .985f);
        back.onClick.AddListener(ShowCharacterList);

        galleryTitle = MakeText(galleryRoot.transform, "", 21, TextAnchor.MiddleCenter, true);
        galleryTitle.color = new Color(1f, .84f, .48f, 1f);
        Anchor(galleryTitle.rectTransform, .20f, .925f, .80f, .985f);

        galleryProgress = MakeText(galleryRoot.transform, "", 13, TextAnchor.MiddleRight, true);
        galleryProgress.color = new Color(.90f, .82f, .70f, 1f);
        Anchor(galleryProgress.rectTransform, .64f, .88f, .98f, .925f);

        galleryGrid = new GameObject("GalleryGrid", typeof(RectTransform)).GetComponent<RectTransform>();
        galleryGrid.SetParent(galleryRoot.transform, false);
        Anchor(galleryGrid, .02f, .12f, .98f, .875f);

        Button prev = MakeButton(galleryRoot.transform, "이전", 14);
        Anchor(prev.GetComponent<RectTransform>(), .08f, .02f, .28f, .095f);
        prev.onClick.AddListener(delegate
        {
            galleryPageIndex = Mathf.Max(0, galleryPageIndex - 1);
            RefreshGallery();
        });

        galleryPage = MakeText(galleryRoot.transform, "", 14, TextAnchor.MiddleCenter, true);
        galleryPage.color = new Color(.92f, .86f, .76f, 1f);
        Anchor(galleryPage.rectTransform, .34f, .02f, .66f, .095f);

        Button next = MakeButton(galleryRoot.transform, "다음", 14);
        Anchor(next.GetComponent<RectTransform>(), .72f, .02f, .92f, .095f);
        next.onClick.AddListener(delegate
        {
            int maxPage = Mathf.Max(0, Mathf.CeilToInt(AllImageCodes().Count / (float)ImagesPerPage) - 1);
            galleryPageIndex = Mathf.Min(maxPage, galleryPageIndex + 1);
            RefreshGallery();
        });

        galleryRoot.SetActive(false);
    }

    void ShowCharacterList()
    {
        if (listRoot == null || galleryRoot == null) return;
        listRoot.SetActive(true);
        galleryRoot.SetActive(false);
        RebuildCharacterList();
    }

    void RebuildCharacterList()
    {
        ClearChildren(listRoot.transform);

        for (int i = 0; i < CharacterCount; i++)
        {
            int characterId = i + 1;
            bool discovered = IsCharacterDiscovered(characterId);

            GameObject card = new GameObject(
                "Character" + characterId,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Button)
            );
            card.transform.SetParent(listRoot.transform, false);

            Image bg = card.GetComponent<Image>();
            bg.color = new Color(.035f, .025f, .024f, .99f);
            Frame(bg.rectTransform, discovered
                ? new Color(.68f, .45f, .20f, 1f)
                : new Color(.22f, .20f, .22f, 1f), 3f);

            int col = i % 2;
            int row = i / 2;
            float x1 = col == 0 ? .02f : .515f;
            float x2 = col == 0 ? .485f : .98f;
            float yTop = .985f - row * .195f;
            float yBottom = yTop - .175f;
            Anchor(bg.rectTransform, x1, yBottom, x2, yTop);

            if (discovered)
            {
                Sprite preview = FirstDiscoveredSprite(characterId);
                Image art = new GameObject("Preview", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
                art.transform.SetParent(card.transform, false);
                art.raycastTarget = false;
                art.preserveAspect = true;
                art.sprite = preview;
                art.color = preview != null ? Color.white : new Color(.08f, .07f, .08f, 1f);
                Anchor(art.rectTransform, .03f, .08f, .35f, .92f);

                Text name = MakeText(card.transform, "캐릭터 " + characterId, 16, TextAnchor.MiddleLeft, true);
                name.color = new Color(1f, .90f, .72f, 1f);
                Anchor(name.rectTransform, .39f, .48f, .96f, .88f);

                int collected = DiscoveredImageCount(characterId);
                Text progress = MakeText(card.transform, collected + " / 41", 13, TextAnchor.MiddleLeft, true);
                progress.color = collected >= 41
                    ? new Color(.62f, 1f, .64f, 1f)
                    : new Color(.78f, .76f, .72f, 1f);
                Anchor(progress.rectTransform, .39f, .12f, .96f, .46f);

                Button b = card.GetComponent<Button>();
                int capturedId = characterId;
                b.onClick.AddListener(delegate { OpenCharacter(capturedId); });
            }
            else
            {
                Text q = MakeText(card.transform, "?", 38, TextAnchor.MiddleCenter, true);
                q.color = new Color(.45f, .44f, .46f, 1f);
                Anchor(q.rectTransform, .05f, .08f, .95f, .92f);
                card.GetComponent<Button>().interactable = false;
            }
        }
    }

    void OpenCharacter(int characterId)
    {
        selectedCharacter = NormalizeCharacter(characterId);
        galleryPageIndex = 0;

        if (inventory != null && IsCharacterComplete(selectedCharacter))
            inventory.GrantCodexCompletionReward(selectedCharacter);

        listRoot.SetActive(false);
        galleryRoot.SetActive(true);
        RefreshGallery();
    }

    void RefreshGallery()
    {
        if (galleryGrid == null) return;
        ClearChildren(galleryGrid);

        List<string> codes = AllImageCodes();
        int pageCount = Mathf.CeilToInt(codes.Count / (float)ImagesPerPage);
        galleryPageIndex = Mathf.Clamp(galleryPageIndex, 0, Mathf.Max(0, pageCount - 1));

        int collected = DiscoveredImageCount(selectedCharacter);
        galleryTitle.text = "캐릭터 " + selectedCharacter + " 도감";
        galleryProgress.text = collected + " / " + codes.Count +
            (collected >= codes.Count ? "  완성" : "");
        galleryPage.text = (galleryPageIndex + 1) + " / " + pageCount;

        for (int slot = 0; slot < ImagesPerPage; slot++)
        {
            int index = galleryPageIndex * ImagesPerPage + slot;
            if (index >= codes.Count) break;

            string code = codes[index];
            bool discovered = IsImageDiscovered(selectedCharacter, code);

            RectTransform cell = MakePanel(galleryGrid, "Image" + index, new Color(.025f, .020f, .022f, 1f));
            int col = slot % 2;
            int row = slot / 2;
            float x1 = col == 0 ? .015f : .515f;
            float x2 = col == 0 ? .485f : .985f;
            float yTop = .985f - row * .33f;
            float yBottom = yTop - .30f;
            Anchor(cell, x1, yBottom, x2, yTop);
            Frame(cell, discovered
                ? new Color(.58f, .40f, .20f, 1f)
                : new Color(.18f, .17f, .19f, 1f), 2f);

            if (discovered)
            {
                Sprite s = SpriteFor(selectedCharacter, code);
                if (s != null)
                {
                    Image art = new GameObject("Art", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
                    art.transform.SetParent(cell, false);
                    art.sprite = s;
                    art.color = Color.white;
                    art.preserveAspect = true;
                    art.raycastTarget = false;
                    Anchor(art.rectTransform, .03f, .10f, .97f, .97f);
                }
            }
            else
            {
                Text q = MakeText(cell, "?", 44, TextAnchor.MiddleCenter, true);
                q.color = new Color(.42f, .41f, .44f, 1f);
                Anchor(q.rectTransform, .05f, .14f, .95f, .95f);
            }

            Text indexLabel = MakeText(cell, "IMAGE " + (index + 1).ToString("00"), 11, TextAnchor.MiddleCenter, true);
            indexLabel.color = new Color(.76f, .69f, .60f, 1f);
            Anchor(indexLabel.rectTransform, .05f, .01f, .95f, .11f);
        }
    }

    void ClearChildren(Transform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            GameObject child = parent.GetChild(i).gameObject;
            child.SetActive(false);
            Destroy(child);
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

    Button MakeButton(Transform parent, string label, int size)
    {
        GameObject go = new GameObject(label + "Button", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        Image bg = go.GetComponent<Image>();
        bg.color = new Color(.065f, .045f, .035f, 1f);
        Frame(bg.rectTransform, new Color(.58f, .40f, .20f, 1f), 2f);

        Text text = MakeText(go.transform, label, size, TextAnchor.MiddleCenter, true);
        text.color = new Color(1f, .90f, .74f, 1f);
        Anchor(text.rectTransform, .04f, .04f, .96f, .96f);

        Button button = go.GetComponent<Button>();
        button.targetGraphic = bg;
        return button;
    }

    Text MakeText(Transform parent, string value, int size, TextAnchor alignment, bool bold)
    {
        GameObject go = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        go.transform.SetParent(parent, false);
        Text t = go.GetComponent<Text>();
        t.text = value;
        t.font = font;
        t.fontSize = Mathf.RoundToInt(size * 2.15f);
        t.alignment = alignment;
        t.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
        t.color = Color.white;
        t.raycastTarget = false;
        t.supportRichText = true;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Truncate;
        return t;
    }

    void Frame(RectTransform parent, Color color, float thickness)
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

    static void Anchor(RectTransform r, float x1, float y1, float x2, float y2)
    {
        r.anchorMin = new Vector2(x1, y1);
        r.anchorMax = new Vector2(x2, y2);
        r.offsetMin = Vector2.zero;
        r.offsetMax = Vector2.zero;
    }
}
