using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[Serializable]
public sealed class XTapGearBlockData
{
    // 0 = bag grid(equipped), 1 = carried inventory, 2 = ground.
    public const int LocationBag = 0;
    public const int LocationHeld = 1;
    public const int LocationGround = 2;

    public string id;
    public string displayName;
    public int cellCount;
    public double attack;
    public double defense;
    public double hp;
    public int correction;
    public bool exclusive;
    public int characterId;
    public string shape;
    public int gridX = -1;
    public int gridY = -1;
    public int rotation;
    public int location = LocationBag;
    public int bagOwnerCharacterId;
    public int enhanceLevel;
}

public sealed class XTapInventory : MonoBehaviour
{
    const float UiFontScale = 1.70f;
    const int GridW = 8;
    const int BaseGridCells = 24;
    const float CellSize = 118f;
    const float MiniCell = 42f;
    const int StoragePageSize = 2;
    public const int SharedStoragePageSize = StoragePageSize;
    const string SaveKey = "xtap_bag_v1";
    const string ExpansionKey = "xtap_bag_extra_cells";

    [Serializable]
    sealed class SaveData
    {
        public List<XTapGearBlockData> items = new List<XTapGearBlockData>();
    }

    public bool IsOpen { get; private set; }
    public int Count { get { return items.Count; } }

    RectTransform host;
    Font font;
    Action onClosed;

    GameObject overlay;
    RectTransform overlayRect;
    RectTransform panel;
    RectTransform gridViewport;
    RectTransform gridRoot;
    RectTransform gridCellRoot;
    ScrollRect gridScroll;

    RectTransform heldViewport;
    RectTransform heldContent;
    RectTransform groundViewport;
    RectTransform groundContent;
    Image heldZoneImage;
    Image groundZoneImage;

    Text bagTitleText;
    Text bagCountText;
    Text totalText;
    Text heldCountText;
    Text groundCountText;
    Text heldPageText;
    Text groundPageText;
    Text detailText;
    Button tidyButton;
    Button heldPrevButton;
    Button heldNextButton;
    Button groundPrevButton;
    Button groundNextButton;

    readonly List<XTapGearBlockData> items = new List<XTapGearBlockData>();
    readonly Dictionary<string, RectTransform> itemViews = new Dictionary<string, RectTransform>();
    readonly List<Image> gridCells = new List<Image>();

    int activeBagOwnerCharacterId;
    int heldPage;
    int groundPage;
    string selectedId;
    string draggingId;
    int dragOffsetX;
    int dragOffsetY;

    int originalLocation;
    int originalBagOwnerCharacterId;
    int originalX;
    int originalY;
    int originalRotation;

    int previewX = -99;
    int previewY = -99;

    RectTransform dragGhost;
    CanvasGroup dragGhostGroup;

    public void Initialize(RectTransform parent, Font uiFont, Action closed)
    {
        host = parent;
        font = uiFont;
        onClosed = closed;
        XTapUiSkin.EnsureLoaded();
        Load();
        BuildUi();
        overlay.SetActive(false);
    }

    public void Open()
    {
        OpenBagForOwner(0);
    }

    public void OpenCharacterBag(int characterId)
    {
        characterId = Mathf.Clamp(characterId, 1, 10);
        if (PlayerPrefs.GetInt("xtap_captured_char_" + characterId, 0) != 1)
            return;

        OpenBagForOwner(characterId);
    }

    void OpenBagForOwner(int ownerCharacterId)
    {
        if (overlay == null) return;

        activeBagOwnerCharacterId = Mathf.Max(0, ownerCharacterId);
        heldPage = 0;
        groundPage = 0;
        selectedId = null;
        IsOpen = true;
        overlay.SetActive(true);
        overlay.transform.SetAsLastSibling();
        Render();
        Canvas.ForceUpdateCanvases();
        if (gridScroll != null)
            gridScroll.verticalNormalizedPosition = 1f;
    }

    public void Close()
    {
        IsOpen = false;
        CancelDrag(false);
        ClearGridHighlight();
        ClearZoneHighlight();
        if (overlay != null) overlay.SetActive(false);
        if (onClosed != null) onClosed();
    }

    // Gacha rewards land on the floor. Floor/held storage does not consume the
    // 8x3 equipment grid, so a full bag never destroys or blocks a reward.
    public bool AddToGround(XTapGearBlockData item)
    {
        if (item == null) return false;
        if (string.IsNullOrEmpty(item.id)) item.id = Guid.NewGuid().ToString("N");

        item.location = XTapGearBlockData.LocationGround;
        item.bagOwnerCharacterId = 0;
        item.gridX = -1;
        item.gridY = -1;
        item.rotation = ((item.rotation % 4) + 4) % 4;

        items.Add(item);
        selectedId = item.id;
        Save();
        if (IsOpen) Render();
        return true;
    }

    // Legacy entry point. New rewards should use AddToGround.
    public bool TryAddBlock(XTapGearBlockData item)
    {
        return AddToGround(item);
    }

    public bool HasSpaceFor(XTapGearBlockData item)
    {
        if (item == null) return false;

        int oldLocation = item.location;
        int oldBagOwner = item.bagOwnerCharacterId;
        int oldX = item.gridX;
        int oldY = item.gridY;
        int oldRotation = item.rotation;

        bool fits = FindFirstPlacement(item);

        item.location = oldLocation;
        item.bagOwnerCharacterId = oldBagOwner;
        item.gridX = oldX;
        item.gridY = oldY;
        item.rotation = oldRotation;
        return fits;
    }

    void BuildUi()
    {
        overlay = new GameObject("BagOverlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        overlay.transform.SetParent(host, false);
        overlayRect = overlay.GetComponent<RectTransform>();

        Image dim = overlay.GetComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, .985f);
        dim.raycastTarget = true;
        Anchor(overlayRect, 0f, 0f, 1f, 1f);

        panel = new GameObject("BagPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<RectTransform>();
        panel.SetParent(overlay.transform, false);
        Anchor(panel, 0f, 0f, 1f, 1f);

        Image body = panel.GetComponent<Image>();
        body.color = new Color(.025f, .024f, .028f, 1f);
        body.raycastTarget = true;

        bagTitleText = MakeText(panel, "플레이어 가방", 25, TextAnchor.MiddleLeft, true);
        bagTitleText.color = new Color(.98f, .91f, .76f, 1f);
        Anchor(bagTitleText.rectTransform, .035f, .950f, .55f, .995f);

        bagCountText = MakeText(panel, "", 17, TextAnchor.MiddleLeft, true);
        bagCountText.color = new Color(1f, .72f, .26f, 1f);
        Anchor(bagCountText.rectTransform, .48f, .950f, .74f, .995f);

        Button closeTop = MakeButton(panel, "닫기", 16);
        Anchor(closeTop.GetComponent<RectTransform>(), .78f, .952f, .965f, .993f);
        closeTop.onClick.AddListener(Close);

        RectTransform statBar = Panel(panel, "BagStatBar", new Color(.035f, .032f, .030f, 1f));
        Anchor(statBar, .025f, .895f, .975f, .948f);
        ApplyPanelSkin(statBar, XTapUiSkin.StatusBar, new Color(.90f, .84f, .72f, 1f));

        totalText = MakeText(panel, "", 16, TextAnchor.MiddleCenter, true);
        totalText.color = new Color(1f, .82f, .42f, 1f);
        Anchor(totalText.rectTransform, .045f, .900f, .955f, .944f);

        GameObject viewportGo = new GameObject(
            "BagGridViewport",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(RectMask2D),
            typeof(ScrollRect)
        );
        viewportGo.transform.SetParent(panel, false);
        gridViewport = viewportGo.GetComponent<RectTransform>();
        Anchor(gridViewport, .02f, .400f, .98f, .900f);

        Image viewportBg = viewportGo.GetComponent<Image>();
        viewportBg.color = new Color(.035f, .045f, .060f, 1f);
        viewportBg.raycastTarget = true;
        ApplyImageSkin(viewportBg, XTapUiSkin.Panel, new Color(.76f, .80f, .88f, 1f));

        gridRoot = new GameObject("BagGrid", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<RectTransform>();
        gridRoot.SetParent(gridViewport, false);
        gridRoot.anchorMin = gridRoot.anchorMax = new Vector2(.5f, 1f);
        gridRoot.pivot = new Vector2(.5f, 1f);
        gridRoot.anchoredPosition = Vector2.zero;
        gridRoot.sizeDelta = new Vector2(GridW * CellSize, ActiveGridRows * CellSize);

        Image gridBg = gridRoot.GetComponent<Image>();
        gridBg.color = new Color(.055f, .070f, .095f, 1f);
        gridBg.raycastTarget = true;

        gridScroll = viewportGo.GetComponent<ScrollRect>();
        gridScroll.viewport = gridViewport;
        gridScroll.content = gridRoot;
        gridScroll.horizontal = false;
        gridScroll.vertical = true;
        gridScroll.movementType = ScrollRect.MovementType.Clamped;
        gridScroll.inertia = true;
        gridScroll.decelerationRate = .08f;
        gridScroll.scrollSensitivity = 65f;

        gridCellRoot = new GameObject("GridCells", typeof(RectTransform)).GetComponent<RectTransform>();
        gridCellRoot.SetParent(gridRoot, false);
        Anchor(gridCellRoot, 0f, 0f, 1f, 1f);
        RebuildGridCells();

        heldCountText = MakeText(panel, "", 17, TextAnchor.MiddleLeft, true);
        heldCountText.color = new Color(.98f, .91f, .78f, 1f);
        Anchor(heldCountText.rectTransform, .035f, .355f, .50f, .402f);

        heldPrevButton = MakeButton(panel, "◀", 18);
        StylePagerButton(heldPrevButton);
        Anchor(heldPrevButton.GetComponent<RectTransform>(), .635f, .355f, .735f, .402f);
        heldPrevButton.onClick.AddListener(() => ChangeStoragePage(XTapGearBlockData.LocationHeld, -1));

        heldPageText = MakeText(panel, "", 17, TextAnchor.MiddleCenter, true);
        heldPageText.color = new Color(1f, .84f, .50f, 1f);
        Anchor(heldPageText.rectTransform, .740f, .355f, .855f, .402f);

        heldNextButton = MakeButton(panel, "▶", 18);
        StylePagerButton(heldNextButton);
        Anchor(heldNextButton.GetComponent<RectTransform>(), .860f, .355f, .960f, .402f);
        heldNextButton.onClick.AddListener(() => ChangeStoragePage(XTapGearBlockData.LocationHeld, 1));

        BuildPagedZone(
            panel,
            "HeldZone",
            .03f, .245f, .97f, .358f,
            out heldViewport,
            out heldContent,
            out heldZoneImage
        );

        groundCountText = MakeText(panel, "", 17, TextAnchor.MiddleLeft, true);
        groundCountText.color = new Color(.98f, .91f, .78f, 1f);
        Anchor(groundCountText.rectTransform, .035f, .200f, .50f, .247f);

        groundPrevButton = MakeButton(panel, "◀", 18);
        StylePagerButton(groundPrevButton);
        Anchor(groundPrevButton.GetComponent<RectTransform>(), .635f, .200f, .735f, .247f);
        groundPrevButton.onClick.AddListener(() => ChangeStoragePage(XTapGearBlockData.LocationGround, -1));

        groundPageText = MakeText(panel, "", 17, TextAnchor.MiddleCenter, true);
        groundPageText.color = new Color(1f, .84f, .50f, 1f);
        Anchor(groundPageText.rectTransform, .740f, .200f, .855f, .247f);

        groundNextButton = MakeButton(panel, "▶", 18);
        StylePagerButton(groundNextButton);
        Anchor(groundNextButton.GetComponent<RectTransform>(), .860f, .200f, .960f, .247f);
        groundNextButton.onClick.AddListener(() => ChangeStoragePage(XTapGearBlockData.LocationGround, 1));

        BuildPagedZone(
            panel,
            "GroundZone",
            .03f, .090f, .97f, .203f,
            out groundViewport,
            out groundContent,
            out groundZoneImage
        );

        detailText = MakeText(panel, "", 12, TextAnchor.MiddleCenter, true);
        detailText.color = new Color(.90f, .87f, .80f, 1f);
        Anchor(detailText.rectTransform, .04f, .052f, .96f, .088f);

        tidyButton = MakeButton(panel, "자동 정리", 15);
        ApplyButtonSkin(tidyButton, XTapUiSkin.ButtonPrimary, new Color(.95f, .78f, .42f, 1f));
        Anchor(tidyButton.GetComponent<RectTransform>(), .03f, .010f, .97f, .050f);
        tidyButton.onClick.AddListener(TidyBag);
    }

    void BuildPagedZone(
        Transform parent,
        string name,
        float x1,
        float y1,
        float x2,
        float y2,
        out RectTransform viewport,
        out RectTransform content,
        out Image zoneImage)
    {
        GameObject vp = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(RectMask2D));
        vp.transform.SetParent(parent, false);

        viewport = vp.GetComponent<RectTransform>();
        Anchor(viewport, x1, y1, x2, y2);

        zoneImage = vp.GetComponent<Image>();
        zoneImage.color = BaseZoneColor();
        zoneImage.raycastTarget = true;
        ApplyImageSkin(zoneImage, XTapUiSkin.Panel, new Color(.82f, .78f, .72f, 1f));

        GameObject contentGo = new GameObject(name + "Content", typeof(RectTransform));
        contentGo.transform.SetParent(vp.transform, false);
        content = contentGo.GetComponent<RectTransform>();
        Anchor(content, 0f, 0f, 1f, 1f);
    }

    public void BuildSharedStorageZone(
        Transform parent,
        string name,
        float x1,
        float y1,
        float x2,
        float y2,
        out RectTransform viewport,
        out RectTransform content)
    {
        Image zoneImage;
        BuildPagedZone(parent, name, x1, y1, x2, y2, out viewport, out content, out zoneImage);
    }

    public int GetStorageCount(int location)
    {
        int count = 0;
        for (int i = 0; i < items.Count; i++)
            if (items[i] != null && items[i].location == location)
                count++;
        return count;
    }

    public int GetStoragePageCount(int location)
    {
        return Mathf.Max(1, Mathf.CeilToInt(GetStorageCount(location) / (float)StoragePageSize));
    }

    public void RenderSharedStoragePage(
        int location,
        RectTransform content,
        int page,
        Action<string> onPressed,
        Func<string, bool> isSelected)
    {
        if (content == null) return;

        ClearChildren(content);

        List<XTapGearBlockData> filtered = new List<XTapGearBlockData>();
        for (int i = 0; i < items.Count; i++)
            if (items[i] != null && items[i].location == location)
                filtered.Add(items[i]);

        int clampedPage = Mathf.Clamp(page, 0, Mathf.Max(0, Mathf.CeilToInt(filtered.Count / (float)StoragePageSize) - 1));
        int start = clampedPage * StoragePageSize;

        if (start >= filtered.Count)
        {
            Text empty = MakeText(content, "(없음)", 18, TextAnchor.MiddleCenter, false);
            empty.color = new Color(.48f, .46f, .45f, 1f);
            Anchor(empty.rectTransform, 0f, 0f, 1f, 1f);
            return;
        }

        float gap = 14f;
        float zoneWidth = Mathf.Max(900f, ((RectTransform)content.parent).rect.width);
        float cardWidth = (zoneWidth - gap) * .5f;
        int shown = 0;

        for (int i = start; i < filtered.Count && shown < StoragePageSize; i++, shown++)
        {
            float x = shown == 0 ? 0f : cardWidth + gap;
            bool selected = isSelected != null && isSelected(filtered[i].id);
            CreatePageItemView(filtered[i], content, x, cardWidth, onPressed, selected);
        }
    }

    void Render()
    {
        RebuildGridCells();
        DestroyItemViews();
        ClearChildren(heldContent);
        ClearChildren(groundContent);

        if (bagTitleText != null)
        {
            bagTitleText.text = activeBagOwnerCharacterId == 0
                ? "플레이어 가방"
                : "캐릭터 " + activeBagOwnerCharacterId + " 가방";
        }

        int bagCount = 0;
        int heldCount = 0;
        int groundCount = 0;
        double atk = 0d;
        double def = 0d;
        double hp = 0d;

        for (int i = 0; i < items.Count; i++)
        {
            XTapGearBlockData item = items[i];
            NormalizeItem(item);

            if (item.location == XTapGearBlockData.LocationBag &&
                item.bagOwnerCharacterId == activeBagOwnerCharacterId)
            {
                bagCount++;
                atk = XTapStatFormat.SafeAdd(atk, item.attack);
                def = XTapStatFormat.SafeAdd(def, item.defense);
                hp = XTapStatFormat.SafeAdd(hp, item.hp);
                CreateGridItemView(item);
            }
            else if (item.location == XTapGearBlockData.LocationHeld)
            {
                heldCount++;
            }
            else if (item.location == XTapGearBlockData.LocationGround)
            {
                groundCount++;
            }
        }

        int heldPages = Mathf.Max(1, Mathf.CeilToInt(heldCount / (float)StoragePageSize));
        int groundPages = Mathf.Max(1, Mathf.CeilToInt(groundCount / (float)StoragePageSize));
        heldPage = Mathf.Clamp(heldPage, 0, heldPages - 1);
        groundPage = Mathf.Clamp(groundPage, 0, groundPages - 1);

        RenderPage(XTapGearBlockData.LocationHeld, heldContent, heldPage);
        RenderPage(XTapGearBlockData.LocationGround, groundContent, groundPage);

        totalText.text = "공격력 +" + XTapStatFormat.Compact(atk) +
                         "     방어력 +" + XTapStatFormat.Compact(def) +
                         "     체력 +" + XTapStatFormat.Compact(hp);
        if (bagCountText != null)
            bagCountText.text = OccupiedCellCount() + " / " + ActiveGridCapacity + "칸";
        heldCountText.text = "소지품    " + heldCount + "개";
        groundCountText.text = "바닥    " + groundCount + "개";

        heldPageText.text = heldCount == 0 ? "0 / 0" : (heldPage + 1) + " / " + heldPages;
        groundPageText.text = groundCount == 0 ? "0 / 0" : (groundPage + 1) + " / " + groundPages;

        heldPrevButton.interactable = heldPage > 0;
        heldNextButton.interactable = heldCount > 0 && heldPage < heldPages - 1;
        groundPrevButton.interactable = groundPage > 0;
        groundNextButton.interactable = groundCount > 0 && groundPage < groundPages - 1;

        RefreshSelectionText();
    }

    void DestroyItemViews()
    {
        foreach (var kv in itemViews)
        {
            if (kv.Value == null) continue;
            kv.Value.gameObject.SetActive(false);
            Destroy(kv.Value.gameObject);
        }
        itemViews.Clear();
    }

    void ClearChildren(RectTransform root)
    {
        if (root == null) return;
        for (int i = root.childCount - 1; i >= 0; i--)
        {
            GameObject child = root.GetChild(i).gameObject;
            child.SetActive(false);
            Destroy(child);
        }
    }

    void ChangeStoragePage(int location, int delta)
    {
        if (location == XTapGearBlockData.LocationHeld)
            heldPage = Mathf.Max(0, heldPage + delta);
        else if (location == XTapGearBlockData.LocationGround)
            groundPage = Mathf.Max(0, groundPage + delta);

        Render();
    }

    void RenderPage(int location, RectTransform content, int page)
    {
        List<XTapGearBlockData> filtered = new List<XTapGearBlockData>();
        for (int i = 0; i < items.Count; i++)
            if (items[i].location == location)
                filtered.Add(items[i]);

        int start = page * StoragePageSize;
        int shown = 0;

        if (start >= filtered.Count)
        {
            Text empty = MakeText(content, "(없음)", 18, TextAnchor.MiddleCenter, false);
            empty.color = new Color(.48f, .46f, .45f, 1f);
            Anchor(empty.rectTransform, 0f, 0f, 1f, 1f);
            return;
        }

        float gap = 14f;
        float zoneWidth = Mathf.Max(900f, ((RectTransform)content.parent).rect.width);
        float cardWidth = (zoneWidth - gap) * .5f;

        for (int i = start; i < filtered.Count && shown < StoragePageSize; i++, shown++)
        {
            float x = shown == 0 ? 0f : cardWidth + gap;
            CreatePageItemView(filtered[i], content, x, cardWidth);
        }
    }

    void CreatePageItemView(XTapGearBlockData item, RectTransform content, float x, float width)
    {
        CreatePageItemView(item, content, x, width, null, selectedId == item.id);
    }

    void CreatePageItemView(
        XTapGearBlockData item,
        RectTransform content,
        float x,
        float width,
        Action<string> onPressed,
        bool selected)
    {
        List<Vector2Int> cells = GetCells(item);
        int maxX = 0;
        int maxY = 0;
        for (int i = 0; i < cells.Count; i++)
        {
            maxX = Mathf.Max(maxX, cells[i].x);
            maxY = Mathf.Max(maxY, cells[i].y);
        }

        GameObject go = new GameObject(
            "StoredBlock_" + item.id,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(CanvasGroup)
        );
        go.transform.SetParent(content, false);

        Image bg = go.GetComponent<Image>();
        bg.color = selected
            ? new Color(.17f, .145f, .11f, .98f)
            : new Color(.055f, .053f, .060f, .98f);
        bg.raycastTarget = true;

        RectTransform root = go.GetComponent<RectTransform>();
        root.anchorMin = root.anchorMax = new Vector2(0f, .5f);
        root.pivot = new Vector2(0f, .5f);
        root.sizeDelta = new Vector2(width, Mathf.Max(170f, ((RectTransform)content.parent).rect.height - 8f));
        root.anchoredPosition = new Vector2(x, 0f);

        if (onPressed == null)
        {
            XTapBagItemTouch touch = go.AddComponent<XTapBagItemTouch>();
            touch.owner = this;
            touch.itemId = item.id;
        }
        else
        {
            XTapStorageSelectTouch touch = go.AddComponent<XTapStorageSelectTouch>();
            touch.itemId = item.id;
            touch.onPressed = onPressed;
        }

        float shapeWidth = (maxX + 1) * MiniCell;
        float shapeHeight = (maxY + 1) * MiniCell;
        RectTransform shapeRoot = new GameObject("Shape", typeof(RectTransform)).GetComponent<RectTransform>();
        shapeRoot.SetParent(root, false);
        shapeRoot.anchorMin = shapeRoot.anchorMax = new Vector2(.04f, .5f);
        shapeRoot.pivot = new Vector2(0f, .5f);
        shapeRoot.sizeDelta = new Vector2(shapeWidth, shapeHeight);
        shapeRoot.anchoredPosition = Vector2.zero;
        DrawShape(shapeRoot, item, MiniCell, BlockColor(item), false, false);

        string forgeSuffix = item.enhanceLevel > 0 ? "  +" + item.enhanceLevel : "";
        Text name = MakeText(root, item.displayName + forgeSuffix, 13, TextAnchor.MiddleLeft, true);
        name.color = new Color(.92f, .89f, .82f, 1f);
        name.resizeTextForBestFit = true;
        name.resizeTextMinSize = 16;
        name.resizeTextMaxSize = 23;
        Anchor(name.rectTransform, .42f, .52f, .96f, .94f);

        Text stat = MakeText(root,
            XTapStatFormat.BlockTriplet(item.attack, item.defense, item.hp, "   "),
            15, TextAnchor.MiddleLeft, true);
        stat.color = Color.white;
        stat.resizeTextForBestFit = true;
        stat.resizeTextMinSize = 18;
        stat.resizeTextMaxSize = 28;
        Anchor(stat.rectTransform, .38f, .08f, .98f, .52f);

        if (onPressed == null)
            itemViews[item.id] = root;
    }

    void CreateGridItemView(XTapGearBlockData item)
    {
        if (item.gridX < 0 || item.gridY < 0) return;

        List<Vector2Int> cells = GetCells(item);
        int maxX = 0;
        int maxY = 0;
        for (int i = 0; i < cells.Count; i++)
        {
            maxX = Mathf.Max(maxX, cells[i].x);
            maxY = Mathf.Max(maxY, cells[i].y);
        }

        GameObject go = new GameObject(
            "BagBlock_" + item.id,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(CanvasGroup),
            typeof(XTapBagItemTouch)
        );
        go.transform.SetParent(gridRoot, false);

        Image hit = go.GetComponent<Image>();
        hit.color = new Color(1f, 1f, 1f, .001f);
        hit.raycastTarget = true;

        RectTransform root = go.GetComponent<RectTransform>();
        root.anchorMin = root.anchorMax = new Vector2(0f, 1f);
        root.pivot = new Vector2(0f, 1f);
        root.sizeDelta = new Vector2((maxX + 1) * CellSize, (maxY + 1) * CellSize);
        root.anchoredPosition = new Vector2(item.gridX * CellSize, -item.gridY * CellSize);

        SetupTouch(go, item.id);
        DrawGridShape(root, item, CellSize, BlockColor(item));
        AddGridStatBadge(root, item);

        itemViews[item.id] = root;
    }

    float CreateRowItemView(XTapGearBlockData item, RectTransform content, float x)
    {
        List<Vector2Int> cells = GetCells(item);
        int maxX = 0;
        int maxY = 0;
        for (int i = 0; i < cells.Count; i++)
        {
            maxX = Mathf.Max(maxX, cells[i].x);
            maxY = Mathf.Max(maxY, cells[i].y);
        }

        float shapeWidth = (maxX + 1) * MiniCell;
        float width = Mathf.Max(310f, shapeWidth + 40f);

        GameObject go = new GameObject(
            "StoredBlock_" + item.id,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(CanvasGroup),
            typeof(XTapBagItemTouch)
        );
        go.transform.SetParent(content, false);

        Image bg = go.GetComponent<Image>();
        bg.color = selectedId == item.id
            ? new Color(.18f, .155f, .13f, .98f)
            : new Color(.075f, .072f, .076f, .98f);
        bg.raycastTarget = true;

        RectTransform root = go.GetComponent<RectTransform>();
        root.anchorMin = root.anchorMax = new Vector2(0f, .5f);
        root.pivot = new Vector2(0f, .5f);
        root.sizeDelta = new Vector2(width, 240f);
        root.anchoredPosition = new Vector2(x, 0f);
        Frame(root, selectedId == item.id ? new Color(.78f, .61f, .29f, 1f) : new Color(.30f, .28f, .26f, 1f), 2f);

        SetupTouch(go, item.id);

        RectTransform shapeRoot = new GameObject("Shape", typeof(RectTransform)).GetComponent<RectTransform>();
        shapeRoot.SetParent(root, false);
        shapeRoot.anchorMin = shapeRoot.anchorMax = new Vector2(.5f, 1f);
        shapeRoot.pivot = new Vector2(.5f, 1f);
        shapeRoot.sizeDelta = new Vector2(shapeWidth, (maxY + 1) * MiniCell);
        shapeRoot.anchoredPosition = new Vector2(0f, -14f);
        DrawShape(shapeRoot, item, MiniCell, BlockColor(item), true, false);

        string forgeSuffix = item.enhanceLevel > 0 ? "  +" + item.enhanceLevel : "";
        Text name = MakeText(root, item.displayName + forgeSuffix, 15, TextAnchor.MiddleCenter, true);
        name.color = new Color(.92f, .89f, .82f, 1f);
        name.resizeTextForBestFit = true;
        name.resizeTextMinSize = 18;
        name.resizeTextMaxSize = 30;
        Anchor(name.rectTransform, .05f, .24f, .95f, .48f);

        Text stat = MakeText(root,
            XTapStatFormat.BlockTriplet(item.attack, item.defense, item.hp, "   "),
            14, TextAnchor.MiddleCenter, true);
        stat.color = Color.white;
        stat.resizeTextForBestFit = true;
        stat.resizeTextMinSize = 16;
        stat.resizeTextMaxSize = 28;
        Anchor(stat.rectTransform, .04f, .04f, .96f, .24f);

        itemViews[item.id] = root;
        return width;
    }

    void AddGridStatBadge(RectTransform root, XTapGearBlockData item)
    {
        GameObject badgeGo = new GameObject("StatsBadge", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        badgeGo.transform.SetParent(root, false);
        badgeGo.transform.SetAsLastSibling();

        Image badge = badgeGo.GetComponent<Image>();
        badge.color = new Color(.015f, .012f, .014f, .94f);
        badge.raycastTarget = false;

        RectTransform br = badge.rectTransform;
        br.anchorMin = new Vector2(0f, 1f);
        br.anchorMax = new Vector2(0f, 1f);
        br.pivot = new Vector2(0f, 1f);
        br.sizeDelta = new Vector2(
            Mathf.Max(CellSize, Mathf.Min(root.sizeDelta.x, CellSize * 1.65f)),
            94f
        );
        br.anchoredPosition = new Vector2(5f, -5f);

        string statSeparator = root.sizeDelta.x <= CellSize * 1.05f ? "\n" : "   ";
        Text t = MakeText(
            badgeGo.transform,
            XTapStatFormat.BlockTriplet(item.attack, item.defense, item.hp, statSeparator),
            13,
            TextAnchor.MiddleCenter,
            true
        );
        t.color = Color.white;
        t.resizeTextForBestFit = true;
        t.resizeTextMinSize = 16;
        t.resizeTextMaxSize = 24;
        Anchor(t.rectTransform, .04f, .05f, .96f, .95f);
    }

    bool CountsTowardPlayerStats(XTapGearBlockData item)
    {
        if (item == null || item.location != XTapGearBlockData.LocationBag)
            return false;

        int ownerCharacterId = item.bagOwnerCharacterId;

        // Player bag always counts.
        if (ownerCharacterId == 0)
            return true;

        // A captured character's equipped bag also contributes to the player.
        // Uncaptured/invalid character bags never contribute.
        if (ownerCharacterId < 1 || ownerCharacterId > 10)
            return false;

        return PlayerPrefs.GetInt("xtap_captured_char_" + ownerCharacterId, 0) == 1;
    }

    public double EquippedAttack
    {
        get
        {
            double total = 0d;
            for (int i = 0; i < items.Count; i++)
                if (CountsTowardPlayerStats(items[i]))
                    total = XTapStatFormat.SafeAdd(total, Math.Max(0d, items[i].attack));
            return total;
        }
    }

    public double EquippedDefense
    {
        get
        {
            double total = 0d;
            for (int i = 0; i < items.Count; i++)
                if (CountsTowardPlayerStats(items[i]))
                    total = XTapStatFormat.SafeAdd(total, Math.Max(0d, items[i].defense));
            return total;
        }
    }

    public double EquippedHp
    {
        get
        {
            double total = 0d;
            for (int i = 0; i < items.Count; i++)
                if (CountsTowardPlayerStats(items[i]))
                    total = XTapStatFormat.SafeAdd(total, Math.Max(0d, items[i].hp));
            return total;
        }
    }

    public double GetEquippedAttack(int ownerCharacterId)
    {
        double total = 0d;
        for (int i = 0; i < items.Count; i++)
            if (items[i].location == XTapGearBlockData.LocationBag &&
                items[i].bagOwnerCharacterId == ownerCharacterId)
                total = XTapStatFormat.SafeAdd(total, Math.Max(0d, items[i].attack));
        return total;
    }

    public double GetEquippedDefense(int ownerCharacterId)
    {
        double total = 0d;
        for (int i = 0; i < items.Count; i++)
            if (items[i].location == XTapGearBlockData.LocationBag &&
                items[i].bagOwnerCharacterId == ownerCharacterId)
                total = XTapStatFormat.SafeAdd(total, Math.Max(0d, items[i].defense));
        return total;
    }

    public double GetEquippedHp(int ownerCharacterId)
    {
        double total = 0d;
        for (int i = 0; i < items.Count; i++)
            if (items[i].location == XTapGearBlockData.LocationBag &&
                items[i].bagOwnerCharacterId == ownerCharacterId)
                total = XTapStatFormat.SafeAdd(total, Math.Max(0d, items[i].hp));
        return total;
    }

    public int GetEquippedCellCount(int ownerCharacterId)
    {
        int total = 0;
        for (int i = 0; i < items.Count; i++)
            if (items[i].location == XTapGearBlockData.LocationBag &&
                items[i].bagOwnerCharacterId == ownerCharacterId)
                total += Mathf.Max(0, items[i].cellCount);
        return total;
    }

    public int ExpansionBonus
    {
        get { return Mathf.Max(0, PlayerPrefs.GetInt(ExpansionKey, 0)); }
    }

    public int GridCapacity
    {
        get { return BaseGridCells + ExpansionBonus; }
    }

    int ActiveGridCapacity
    {
        get { return activeBagOwnerCharacterId == 0 ? GridCapacity : BaseGridCells; }
    }

    int ActiveGridRows
    {
        get { return Mathf.Max(3, Mathf.CeilToInt(ActiveGridCapacity / (float)GridW)); }
    }

    bool IsCellUnlocked(int x, int y)
    {
        if (x < 0 || x >= GridW || y < 0) return false;
        int index = y * GridW + x;
        return index >= 0 && index < ActiveGridCapacity;
    }

    void RebuildGridCells()
    {
        if (gridRoot == null || gridCellRoot == null) return;

        for (int i = gridCellRoot.childCount - 1; i >= 0; i--)
        {
            GameObject oldCell = gridCellRoot.GetChild(i).gameObject;
            oldCell.SetActive(false);
            Destroy(oldCell);
        }

        gridCells.Clear();

        gridRoot.sizeDelta = new Vector2(GridW * CellSize, ActiveGridRows * CellSize);

        for (int index = 0; index < ActiveGridCapacity; index++)
        {
            int x = index % GridW;
            int y = index / GridW;

            GameObject go = new GameObject("Cell_" + x + "_" + y, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(gridCellRoot, false);

            Image img = go.GetComponent<Image>();
            img.color = BaseCellColor();
            img.raycastTarget = false;
            ApplyImageSkin(img, XTapUiSkin.Slot, new Color(.72f, .84f, 1f, 1f));

            RectTransform r = img.rectTransform;
            r.anchorMin = r.anchorMax = new Vector2(0f, 1f);
            r.pivot = new Vector2(0f, 1f);
            r.sizeDelta = new Vector2(CellSize - 4f, CellSize - 4f);
            r.anchoredPosition = new Vector2(x * CellSize + 2f, -(y * CellSize + 2f));
            if (XTapUiSkin.Slot == null)
                Frame(r, new Color(.30f, .43f, .60f, 1f), 3f);

            gridCells.Add(img);
        }
    }

    public List<XTapGearBlockData> GetForgeItems()
    {
        return new List<XTapGearBlockData>(items);
    }

    public XTapGearBlockData FindForgeItem(string id)
    {
        return Find(id);
    }

    public bool RemoveForgeItem(string id)
    {
        XTapGearBlockData item = Find(id);
        if (item == null) return false;

        items.Remove(item);
        if (selectedId == id) selectedId = null;
        Save();
        if (IsOpen) Render();
        return true;
    }

    public void CommitForgeChanges()
    {
        Save();
        if (IsOpen) Render();
    }

    public int LoseHeldAndGroundOnDeath()
    {
        int removed = 0;

        for (int i = items.Count - 1; i >= 0; i--)
        {
            XTapGearBlockData item = items[i];
            if (item == null) continue;

            if (item.location == XTapGearBlockData.LocationHeld ||
                item.location == XTapGearBlockData.LocationGround)
            {
                if (selectedId == item.id)
                    selectedId = null;

                items.RemoveAt(i);
                removed++;
            }
        }

        heldPage = 0;
        groundPage = 0;
        Save();

        if (IsOpen)
            Render();

        return removed;
    }

    public void AddGridCellExpansion(int amount)
    {
        if (amount <= 0) return;

        PlayerPrefs.SetInt(ExpansionKey, ExpansionBonus + amount);
        PlayerPrefs.Save();

        if (IsOpen)
            Render();
    }

    void SetupTouch(GameObject go, string itemId)
    {
        XTapBagItemTouch touch = go.GetComponent<XTapBagItemTouch>();
        touch.owner = this;
        touch.itemId = itemId;
    }

    void DrawGridShape(RectTransform parent, XTapGearBlockData item, float cellSize, Color color)
    {
        List<Vector2Int> cells = GetCells(item);

        for (int i = 0; i < cells.Count; i++)
        {
            Vector2Int p = cells[i];

            GameObject cg = new GameObject("Piece", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            cg.transform.SetParent(parent, false);
            Image ci = cg.GetComponent<Image>();
            ci.color = color;
            ci.raycastTarget = false;

            RectTransform cr = ci.rectTransform;
            cr.anchorMin = cr.anchorMax = new Vector2(0f, 1f);
            cr.pivot = new Vector2(0f, 1f);
            cr.sizeDelta = new Vector2(cellSize - 5f, cellSize - 5f);
            cr.anchoredPosition = new Vector2(p.x * cellSize + 2.5f, -(p.y * cellSize + 2.5f));

            GameObject inset = new GameObject("Inset", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            inset.transform.SetParent(cg.transform, false);
            Image ii = inset.GetComponent<Image>();
            ii.color = new Color(.08f, .085f, .10f, .88f);
            ii.raycastTarget = false;
            Anchor(ii.rectTransform, .13f, .13f, .87f, .87f);
        }
    }

    void DrawShape(
        RectTransform parent,
        XTapGearBlockData item,
        float cellSize,
        Color color,
        bool centered,
        bool showStats)
    {
        List<Vector2Int> cells = GetCells(item);
        int maxX = 0;
        int maxY = 0;

        for (int i = 0; i < cells.Count; i++)
        {
            maxX = Mathf.Max(maxX, cells[i].x);
            maxY = Mathf.Max(maxY, cells[i].y);
        }

        float width = (maxX + 1) * cellSize;
        Vector2 origin = centered ? new Vector2(-width * .5f, 0f) : Vector2.zero;

        for (int i = 0; i < cells.Count; i++)
        {
            Vector2Int p = cells[i];

            GameObject cg = new GameObject("Piece", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            cg.transform.SetParent(parent, false);
            Image ci = cg.GetComponent<Image>();
            ci.color = color;
            ci.raycastTarget = false;

            RectTransform cr = ci.rectTransform;
            cr.anchorMin = cr.anchorMax = centered ? new Vector2(.5f, 1f) : new Vector2(0f, 1f);
            cr.pivot = new Vector2(0f, 1f);
            cr.sizeDelta = new Vector2(cellSize - 5f, cellSize - 5f);
            cr.anchoredPosition = origin + new Vector2(p.x * cellSize + 2.5f, -(p.y * cellSize + 2.5f));

            GameObject inset = new GameObject("Inset", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            inset.transform.SetParent(cg.transform, false);
            Image ii = inset.GetComponent<Image>();
            ii.color = new Color(.08f, .085f, .10f, .88f);
            ii.raycastTarget = false;
            Anchor(ii.rectTransform, .13f, .13f, .87f, .87f);

            if (showStats && i == 0)
            {
                Text s = MakeText(cg.transform,
                    XTapStatFormat.Compact(item.attack) + "/" +
                    XTapStatFormat.Compact(item.defense) + "/" +
                    XTapStatFormat.Compact(item.hp),
                    12, TextAnchor.MiddleCenter, true);
                s.color = new Color(.96f, .92f, .82f, 1f);
                s.resizeTextForBestFit = true;
                s.resizeTextMinSize = 12;
                s.resizeTextMaxSize = 18;
                Anchor(s.rectTransform, .02f, .02f, .98f, .98f);
            }
        }
    }

    Color BlockColor(XTapGearBlockData item)
    {
        if (item.exclusive) return new Color(.65f, .22f, .80f, .98f);
        if (item.correction > 0) return new Color(.88f, .58f, .14f, 1f);
        if (item.correction < 0) return new Color(.34f, .42f, .52f, 1f);
        return new Color(.72f, .70f, .60f, 1f);
    }

    public void TapBlock(string id)
    {
        XTapGearBlockData item = Find(id);
        if (item == null) return;

        selectedId = id;
        int next = (item.rotation + 1) % 4;

        if (item.location == XTapGearBlockData.LocationBag)
        {
            if (!CanPlace(item, item.gridX, item.gridY, next, item.id))
            {
                detailText.text = "그 자리에서는 90° 회전할 공간이 부족합니다.";
                return;
            }
        }

        item.rotation = next;
        Save();
        Render();
    }

    public void BeginDrag(string id, Vector2 screen)
    {
        XTapGearBlockData item = Find(id);
        if (item == null) return;

        selectedId = id;
        draggingId = id;

        originalLocation = item.location;
        originalBagOwnerCharacterId = item.bagOwnerCharacterId;
        originalX = item.gridX;
        originalY = item.gridY;
        originalRotation = item.rotation;

        dragOffsetX = 0;
        dragOffsetY = 0;

        if (item.location == XTapGearBlockData.LocationBag)
        {
            int cellX;
            int cellY;
            ScreenToCell(screen, out cellX, out cellY);
            dragOffsetX = Mathf.Clamp(cellX - item.gridX, 0, GridW - 1);
            dragOffsetY = Mathf.Clamp(cellY - item.gridY, 0, Mathf.Max(0, ActiveGridRows - 1));
        }

        RectTransform view;
        if (itemViews.TryGetValue(id, out view) && view != null)
        {
            CanvasGroup group = view.GetComponent<CanvasGroup>();
            if (group != null) group.alpha = .25f;
        }

        CreateDragGhost(item);
        UpdateDrag(id, screen);
        RefreshSelectionText();
    }

    public void UpdateDrag(string id, Vector2 screen)
    {
        if (draggingId != id) return;

        XTapGearBlockData item = Find(id);
        if (item == null) return;

        ClearGridHighlight();
        ClearZoneHighlight();

        if (RectTransformUtility.RectangleContainsScreenPoint(gridViewport, screen, null))
        {
            int cx;
            int cy;
            ScreenToCell(screen, out cx, out cy);

            previewX = cx - dragOffsetX;
            previewY = cy - dragOffsetY;

            HighlightPlacement(item, previewX, previewY);
            PositionGhostAtGridOrigin(item, previewX, previewY);
            return;
        }

        previewX = previewY = -99;

        if (RectTransformUtility.RectangleContainsScreenPoint(heldViewport, screen, null))
            heldZoneImage.color = new Color(.16f, .25f, .19f, 1f);
        else if (RectTransformUtility.RectangleContainsScreenPoint(groundViewport, screen, null))
            groundZoneImage.color = new Color(.24f, .19f, .14f, 1f);

        PositionGhostAtPointer(screen);
    }

    public void EndDrag(string id, Vector2 screen)
    {
        if (draggingId != id) return;

        XTapGearBlockData item = Find(id);
        if (item == null)
        {
            CancelDrag(true);
            return;
        }

        bool moved = false;

        if (RectTransformUtility.RectangleContainsScreenPoint(gridViewport, screen, null))
        {
            int cx;
            int cy;
            ScreenToCell(screen, out cx, out cy);
            int tx = cx - dragOffsetX;
            int ty = cy - dragOffsetY;

            if (CanPlace(item, tx, ty, item.rotation, item.id))
            {
                item.location = XTapGearBlockData.LocationBag;
                item.bagOwnerCharacterId = activeBagOwnerCharacterId;
                item.gridX = tx;
                item.gridY = ty;
                moved = true;
            }
        }
        else if (RectTransformUtility.RectangleContainsScreenPoint(heldViewport, screen, null))
        {
            item.location = XTapGearBlockData.LocationHeld;
            item.bagOwnerCharacterId = 0;
            item.gridX = -1;
            item.gridY = -1;
            moved = true;
        }
        else if (RectTransformUtility.RectangleContainsScreenPoint(groundViewport, screen, null))
        {
            item.location = XTapGearBlockData.LocationGround;
            item.bagOwnerCharacterId = 0;
            item.gridX = -1;
            item.gridY = -1;
            moved = true;
        }

        if (!moved)
        {
            item.location = originalLocation;
            item.bagOwnerCharacterId = originalBagOwnerCharacterId;
            item.gridX = originalX;
            item.gridY = originalY;
            item.rotation = originalRotation;
        }

        draggingId = null;
        previewX = previewY = -99;
        DestroyDragGhost();
        ClearGridHighlight();
        ClearZoneHighlight();

        Save();
        Render();
    }

    void CancelDrag(bool render)
    {
        if (!string.IsNullOrEmpty(draggingId))
        {
            XTapGearBlockData item = Find(draggingId);
            if (item != null)
            {
                item.location = originalLocation;
                item.bagOwnerCharacterId = originalBagOwnerCharacterId;
                item.gridX = originalX;
                item.gridY = originalY;
                item.rotation = originalRotation;
            }
        }

        draggingId = null;
        previewX = previewY = -99;
        DestroyDragGhost();
        ClearGridHighlight();
        ClearZoneHighlight();

        if (render && IsOpen) Render();
    }

    void CreateDragGhost(XTapGearBlockData item)
    {
        DestroyDragGhost();

        List<Vector2Int> cells = GetCells(item);
        int maxX = 0;
        int maxY = 0;
        for (int i = 0; i < cells.Count; i++)
        {
            maxX = Mathf.Max(maxX, cells[i].x);
            maxY = Mathf.Max(maxY, cells[i].y);
        }

        dragGhost = new GameObject("DropGhost", typeof(RectTransform), typeof(CanvasGroup)).GetComponent<RectTransform>();
        dragGhost.SetParent(overlay.transform, false);
        dragGhost.anchorMin = dragGhost.anchorMax = new Vector2(.5f, .5f);
        dragGhost.pivot = new Vector2(0f, 1f);
        dragGhost.sizeDelta = new Vector2((maxX + 1) * CellSize, (maxY + 1) * CellSize);

        dragGhostGroup = dragGhost.GetComponent<CanvasGroup>();
        dragGhostGroup.alpha = .74f;
        dragGhostGroup.blocksRaycasts = false;
        dragGhostGroup.interactable = false;

        DrawGridShape(dragGhost, item, CellSize, BlockColor(item));
        dragGhost.SetAsLastSibling();
    }

    void DestroyDragGhost()
    {
        if (dragGhost != null)
        {
            dragGhost.gameObject.SetActive(false);
            Destroy(dragGhost.gameObject);
        }

        dragGhost = null;
        dragGhostGroup = null;
    }

    void PositionGhostAtGridOrigin(XTapGearBlockData item, int gridX, int gridY)
    {
        if (dragGhost == null) return;

        Vector3 gridLocal = new Vector3(
            gridRoot.rect.xMin + gridX * CellSize,
            gridRoot.rect.yMax - gridY * CellSize,
            0f
        );
        Vector3 world = gridRoot.TransformPoint(gridLocal);
        Vector3 overlayLocal = overlayRect.InverseTransformPoint(world);
        dragGhost.anchoredPosition = new Vector2(overlayLocal.x, overlayLocal.y);
    }

    void PositionGhostAtPointer(Vector2 screen)
    {
        if (dragGhost == null) return;

        Vector2 local;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(overlayRect, screen, null, out local);
        dragGhost.anchoredPosition = local - new Vector2(
            dragOffsetX * CellSize + CellSize * .5f,
            -(dragOffsetY * CellSize + CellSize * .5f)
        );
    }

    void TidyBag()
    {
        List<XTapGearBlockData> bagItems = new List<XTapGearBlockData>();
        Dictionary<string, Vector3Int> backup = new Dictionary<string, Vector3Int>();

        for (int i = 0; i < items.Count; i++)
        {
            XTapGearBlockData item = items[i];
            if (item.location != XTapGearBlockData.LocationBag ||
                item.bagOwnerCharacterId != activeBagOwnerCharacterId) continue;

            bagItems.Add(item);
            backup[item.id] = new Vector3Int(item.gridX, item.gridY, item.rotation);
            item.gridX = -1;
            item.gridY = -1;
        }

        bagItems.Sort((a, b) => b.cellCount.CompareTo(a.cellCount));

        bool ok = true;
        for (int i = 0; i < bagItems.Count; i++)
        {
            if (!FindFirstPlacement(bagItems[i]))
            {
                ok = false;
                break;
            }
        }

        if (!ok)
        {
            foreach (XTapGearBlockData item in bagItems)
            {
                Vector3Int v;
                if (!backup.TryGetValue(item.id, out v)) continue;
                item.gridX = v.x;
                item.gridY = v.y;
                item.rotation = v.z;
                item.location = XTapGearBlockData.LocationBag;
            }
            detailText.text = "자동 정리 실패. 기존 배치를 유지합니다.";
        }
        else
        {
            detailText.text = "가방 블록을 큰 순서부터 다시 정리했습니다.";
            Save();
        }

        Render();
    }

    bool FindFirstPlacement(XTapGearBlockData item)
    {
        int originalRotation = item.rotation;

        for (int rotTry = 0; rotTry < 4; rotTry++)
        {
            int rot = (originalRotation + rotTry) % 4;

            for (int y = 0; y < ActiveGridRows; y++)
            {
                for (int x = 0; x < GridW; x++)
                {
                    if (CanPlace(item, x, y, rot, item.id))
                    {
                        item.location = XTapGearBlockData.LocationBag;
                        item.bagOwnerCharacterId = activeBagOwnerCharacterId;
                        item.gridX = x;
                        item.gridY = y;
                        item.rotation = rot;
                        return true;
                    }
                }
            }
        }

        item.rotation = originalRotation;
        return false;
    }

    bool CanPlace(XTapGearBlockData item, int ox, int oy, int rotation, string ignoreId)
    {
        if (item.exclusive && activeBagOwnerCharacterId != item.characterId)
            return false;

        List<Vector2Int> cells = GetCells(item, rotation);
        HashSet<int> occupied = new HashSet<int>();

        for (int i = 0; i < items.Count; i++)
        {
            XTapGearBlockData other = items[i];
            if (other.id == ignoreId) continue;
            if (other.location != XTapGearBlockData.LocationBag) continue;
            if (other.bagOwnerCharacterId != activeBagOwnerCharacterId) continue;
            if (other.gridX < 0 || other.gridY < 0) continue;

            List<Vector2Int> oc = GetCells(other);
            for (int j = 0; j < oc.Count; j++)
            {
                int x = other.gridX + oc[j].x;
                int y = other.gridY + oc[j].y;
                if (IsCellUnlocked(x, y))
                    occupied.Add(y * GridW + x);
            }
        }

        for (int i = 0; i < cells.Count; i++)
        {
            int x = ox + cells[i].x;
            int y = oy + cells[i].y;

            if (!IsCellUnlocked(x, y)) return false;
            if (occupied.Contains(y * GridW + x)) return false;
        }

        return true;
    }

    List<Vector2Int> GetCells(XTapGearBlockData item)
    {
        return GetCells(item, item.rotation);
    }

    List<Vector2Int> GetCells(XTapGearBlockData item, int rotation)
    {
        List<Vector2Int> source = DecodeShape(item.shape);
        List<Vector2Int> result = new List<Vector2Int>(source.Count);

        for (int i = 0; i < source.Count; i++)
        {
            Vector2Int p = source[i];
            int x = p.x;
            int y = p.y;

            for (int r = 0; r < rotation; r++)
            {
                int nx = -y;
                int ny = x;
                x = nx;
                y = ny;
            }

            result.Add(new Vector2Int(x, y));
        }

        int minX = 999;
        int minY = 999;

        for (int i = 0; i < result.Count; i++)
        {
            minX = Mathf.Min(minX, result[i].x);
            minY = Mathf.Min(minY, result[i].y);
        }

        for (int i = 0; i < result.Count; i++)
            result[i] = new Vector2Int(result[i].x - minX, result[i].y - minY);

        return result;
    }

    List<Vector2Int> DecodeShape(string encoded)
    {
        List<Vector2Int> result = new List<Vector2Int>();

        if (string.IsNullOrEmpty(encoded))
        {
            result.Add(Vector2Int.zero);
            return result;
        }

        string[] points = encoded.Split(';');

        for (int i = 0; i < points.Length; i++)
        {
            string[] xy = points[i].Split(',');
            int x;
            int y;

            if (xy.Length == 2 && int.TryParse(xy[0], out x) && int.TryParse(xy[1], out y))
                result.Add(new Vector2Int(x, y));
        }

        if (result.Count == 0) result.Add(Vector2Int.zero);
        return result;
    }

    void HighlightPlacement(XTapGearBlockData item, int ox, int oy)
    {
        bool valid = CanPlace(item, ox, oy, item.rotation, item.id);
        List<Vector2Int> cells = GetCells(item);

        for (int i = 0; i < cells.Count; i++)
        {
            int x = ox + cells[i].x;
            int y = oy + cells[i].y;

            if (!IsCellUnlocked(x, y)) continue;

            int index = y * GridW + x;
            if (index < 0 || index >= gridCells.Count || gridCells[index] == null) continue;

            gridCells[index].color = valid
                ? new Color(.18f, .46f, .23f, 1f)
                : new Color(.53f, .14f, .15f, 1f);
        }
    }

    void ClearGridHighlight()
    {
        for (int i = 0; i < gridCells.Count; i++)
            if (gridCells[i] != null)
                gridCells[i].color = BaseCellColor();
    }

    void ClearZoneHighlight()
    {
        if (heldZoneImage != null) heldZoneImage.color = BaseZoneColor();
        if (groundZoneImage != null) groundZoneImage.color = BaseZoneColor();
    }

    Color BaseCellColor()
    {
        return new Color(.115f, .145f, .195f, 1f);
    }

    Color BaseZoneColor()
    {
        return new Color(.070f, .067f, .072f, 1f);
    }

    void ScreenToCell(Vector2 screen, out int x, out int y)
    {
        Vector2 local;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(gridRoot, screen, null, out local);
        float fromLeft = local.x - gridRoot.rect.xMin;
        float fromTop = gridRoot.rect.yMax - local.y;

        x = Mathf.FloorToInt(fromLeft / CellSize);
        y = Mathf.FloorToInt(fromTop / CellSize);
    }

    XTapGearBlockData Find(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;

        for (int i = 0; i < items.Count; i++)
            if (items[i].id == id)
                return items[i];

        return null;
    }

    int OccupiedCellCount()
    {
        int total = 0;

        for (int i = 0; i < items.Count; i++)
            if (items[i].location == XTapGearBlockData.LocationBag &&
                items[i].bagOwnerCharacterId == activeBagOwnerCharacterId)
                total += Mathf.Max(0, items[i].cellCount);

        return Mathf.Clamp(total, 0, ActiveGridCapacity);
    }

    void RefreshSelectionText()
    {
        XTapGearBlockData item = Find(selectedId);

        if (item == null)
        {
            detailText.text = "";
            return;
        }

        string corr = item.correction > 0 ? "+" + item.correction + "%" : item.correction + "%";
        string zone = item.location == XTapGearBlockData.LocationBag
            ? (item.bagOwnerCharacterId == 0 ? "플레이어 가방" : "캐릭터 " + item.bagOwnerCharacterId + " 가방")
            : (item.location == XTapGearBlockData.LocationHeld ? "소지품" : "바닥");

        detailText.text =
            zone + " · " + item.displayName +
            (item.enhanceLevel > 0 ? "  +" + item.enhanceLevel : "") +
            "  [" + item.cellCount + "칸 / " + corr + "]\n" +
            XTapStatFormat.BlockTriplet(item.attack, item.defense, item.hp, "     ");
    }

    void NormalizeItem(XTapGearBlockData item)
    {
        if (item == null) return;

        RecoverMissingBlockStats(item);
        item.enhanceLevel = Mathf.Clamp(item.enhanceLevel, 0, 10);
        item.rotation = ((item.rotation % 4) + 4) % 4;

        if (item.location < XTapGearBlockData.LocationBag || item.location > XTapGearBlockData.LocationGround)
            item.location = XTapGearBlockData.LocationHeld;

        item.bagOwnerCharacterId = Mathf.Clamp(item.bagOwnerCharacterId, 0, 10);

        // Character-exclusive gear can only be equipped in that captured character's bag.
        if (item.exclusive &&
            item.location == XTapGearBlockData.LocationBag &&
            item.bagOwnerCharacterId != item.characterId)
        {
            item.location = XTapGearBlockData.LocationHeld;
            item.bagOwnerCharacterId = 0;
            item.gridX = -1;
            item.gridY = -1;
        }

        // Old saves only had grid coordinates. Keep valid placed blocks equipped;
        // anything without a valid grid position becomes carried inventory.
        if (item.location == XTapGearBlockData.LocationBag && (item.gridX < 0 || item.gridY < 0))
            item.location = XTapGearBlockData.LocationHeld;

        if (item.location != XTapGearBlockData.LocationBag)
        {
            item.bagOwnerCharacterId = 0;
            item.gridX = -1;
            item.gridY = -1;
        }
    }

    void RecoverMissingBlockStats(XTapGearBlockData item)
    {
        if (item == null) return;

        int decodedCells = DecodeShape(item.shape).Count;
        if (item.cellCount <= 0)
            item.cellCount = Mathf.Max(1, decodedCells);

        if (item.attack > 0 || item.defense > 0 || item.hp > 0)
            return;

        int[] sizes = {1, 2, 3, 4, 5, 6, 9, 12};
        int[] budgets = {10, 22, 35, 50, 66, 84, 135, 190};

        int index = 0;
        int bestDistance = int.MaxValue;
        for (int i = 0; i < sizes.Length; i++)
        {
            int d = Mathf.Abs(sizes[i] - item.cellCount);
            if (d < bestDistance)
            {
                bestDistance = d;
                index = i;
            }
        }

        float multiplier = Mathf.Max(.5f, 1f + item.correction / 100f);
        int total = Mathf.Max(3, Mathf.RoundToInt(budgets[index] * multiplier));

        // Legacy blocks created before stat persistence get a stable default split.
        item.attack = Mathf.Max(1, Mathf.RoundToInt(total * .40f));
        item.defense = Mathf.Max(1, Mathf.RoundToInt(total * .20f));
        item.hp = Math.Max(1d, total - item.attack - item.defense);
    }

    void Load()
    {
        items.Clear();

        string json = PlayerPrefs.GetString(SaveKey, "");
        if (string.IsNullOrEmpty(json)) return;

        try
        {
            SaveData data = JsonUtility.FromJson<SaveData>(json);
            if (data != null && data.items != null)
            {
                items.AddRange(data.items);
                for (int i = 0; i < items.Count; i++)
                    NormalizeItem(items[i]);
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("X탑 가방 불러오기 실패: " + e.Message);
        }
    }

    void Save()
    {
        try
        {
            SaveData data = new SaveData();
            data.items = new List<XTapGearBlockData>(items);
            PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(data));
            PlayerPrefs.Save();
        }
        catch (Exception e)
        {
            Debug.LogWarning("X탑 가방 저장 실패: " + e.Message);
        }
    }

    Button MakeButton(Transform parent, string label, int fontSize)
    {
        GameObject go = new GameObject(label + "Button", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);

        Image bg = go.GetComponent<Image>();
        bg.color = new Color(.11f, .10f, .10f, 1f);
        if (XTapUiSkin.ButtonNeutral != null)
        {
            bg.sprite = XTapUiSkin.ButtonNeutral;
            bg.type = Image.Type.Sliced;
            bg.color = new Color(.92f, .88f, .80f, 1f);
        }
        else
        {
            Frame(bg.rectTransform, new Color(.52f, .43f, .30f, 1f), 2f);
        }

        Text t = MakeText(go.transform, label, fontSize, TextAnchor.MiddleCenter, true);
        t.color = new Color(.95f, .90f, .80f, 1f);
        Anchor(t.rectTransform, .04f, .04f, .96f, .96f);

        Button b = go.GetComponent<Button>();
        b.targetGraphic = bg;

        ColorBlock cb = b.colors;
        cb.normalColor = Color.white;
        cb.highlightedColor = new Color(1f, .94f, .84f, 1f);
        cb.pressedColor = new Color(.70f, .62f, .55f, 1f);
        cb.disabledColor = new Color(.62f, .52f, .38f, .88f);
        b.colors = cb;

        return b;
    }

    void StylePagerButton(Button button)
    {
        if (button == null) return;

        Image bg = button.targetGraphic as Image;
        if (bg != null)
        {
            if (XTapUiSkin.TabSelected != null)
            {
                bg.sprite = XTapUiSkin.TabSelected;
                bg.type = Image.Type.Sliced;
                bg.color = new Color(1f, .86f, .52f, 1f);
            }
            else
            {
                bg.color = new Color(.18f, .12f, .055f, 1f);
            }
        }

        Text label = button.GetComponentInChildren<Text>();
        if (label != null)
        {
            label.color = new Color(1f, .82f, .38f, 1f);
            label.fontStyle = FontStyle.Bold;
        }
    }

    void ApplyImageSkin(Image image, Sprite sprite, Color tint)
    {
        if (image == null || sprite == null) return;
        image.sprite = sprite;
        image.type = Image.Type.Sliced;
        image.color = tint;
    }

    void ApplyPanelSkin(RectTransform rect, Sprite sprite, Color tint)
    {
        if (rect == null) return;
        ApplyImageSkin(rect.GetComponent<Image>(), sprite, tint);
    }

    void ApplyButtonSkin(Button button, Sprite sprite, Color tint)
    {
        if (button == null) return;
        ApplyImageSkin(button.targetGraphic as Image, sprite, tint);
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
        t.supportRichText = true;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Truncate;
        t.raycastTarget = false;

        return t;
    }

    RectTransform Panel(Transform parent, string name, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);

        Image i = go.GetComponent<Image>();
        i.color = color;
        i.raycastTarget = false;

        return i.rectTransform;
    }

    void Frame(RectTransform parent, Color color, float thickness)
    {
        RectTransform top = Panel(parent, "Top", color);
        Anchor(top, 0f, 1f, 1f, 1f);
        top.sizeDelta = new Vector2(0f, thickness);

        RectTransform bottom = Panel(parent, "Bottom", color);
        Anchor(bottom, 0f, 0f, 1f, 0f);
        bottom.sizeDelta = new Vector2(0f, thickness);

        RectTransform left = Panel(parent, "Left", color);
        Anchor(left, 0f, 0f, 0f, 1f);
        left.sizeDelta = new Vector2(thickness, 0f);

        RectTransform right = Panel(parent, "Right", color);
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

public sealed class XTapStorageSelectTouch : MonoBehaviour, IPointerClickHandler
{
    public string itemId;
    public Action<string> onPressed;

    public void OnPointerClick(PointerEventData eventData)
    {
        if (onPressed != null) onPressed(itemId);
    }
}

public sealed class XTapBagItemTouch : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public XTapInventory owner;
    public string itemId;

    public void OnPointerClick(PointerEventData eventData)
    {
        if (owner != null) owner.TapBlock(itemId);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (owner != null) owner.BeginDrag(itemId, eventData.position);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (owner != null) owner.UpdateDrag(itemId, eventData.position);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (owner != null) owner.EndDrag(itemId, eventData.position);
    }
}
