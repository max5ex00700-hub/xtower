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
    public int attack;
    public int defense;
    public int hp;
    public int correction;
    public bool exclusive;
    public int characterId;
    public string shape;
    public int gridX = -1;
    public int gridY = -1;
    public int rotation;
    public int location = LocationBag;
}

public sealed class XTapInventory : MonoBehaviour
{
    const int GridW = 8;
    const int GridH = 3;
    const float CellSize = 102f;
    const float MiniCell = 42f;
    const string SaveKey = "xtap_bag_v1";

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
    RectTransform gridRoot;

    RectTransform heldViewport;
    RectTransform heldContent;
    RectTransform groundViewport;
    RectTransform groundContent;
    Image heldZoneImage;
    Image groundZoneImage;

    Text bagCountText;
    Text totalText;
    Text heldCountText;
    Text groundCountText;
    Text detailText;
    Button tidyButton;

    readonly List<XTapGearBlockData> items = new List<XTapGearBlockData>();
    readonly Dictionary<string, RectTransform> itemViews = new Dictionary<string, RectTransform>();
    readonly Image[,] gridCells = new Image[GridW, GridH];

    string selectedId;
    string draggingId;
    int dragOffsetX;
    int dragOffsetY;

    int originalLocation;
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
        Load();
        BuildUi();
        overlay.SetActive(false);
    }

    public void Open()
    {
        if (overlay == null) return;
        IsOpen = true;
        overlay.SetActive(true);
        overlay.transform.SetAsLastSibling();
        Render();
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
        int oldX = item.gridX;
        int oldY = item.gridY;
        int oldRotation = item.rotation;

        bool fits = FindFirstPlacement(item);

        item.location = oldLocation;
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
        dim.color = new Color(0f, 0f, 0f, .90f);
        dim.raycastTarget = true;
        Anchor(overlayRect, 0f, 0f, 1f, 1f);

        panel = new GameObject("BagPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<RectTransform>();
        panel.SetParent(overlay.transform, false);
        Image body = panel.GetComponent<Image>();
        body.color = new Color(.042f, .039f, .043f, .995f);
        body.raycastTarget = true;
        // Full-screen inventory. The old centered popup made the 8x3 grid,
        // carried items and floor compete for too little space.
        panel.anchorMin = new Vector2(.018f, .018f);
        panel.anchorMax = new Vector2(.982f, .982f);
        panel.pivot = new Vector2(.5f, .5f);
        panel.offsetMin = Vector2.zero;
        panel.offsetMax = Vector2.zero;
        Frame(panel, new Color(.52f, .43f, .30f, 1f), 4f);

        Text title = MakeText(panel, "배 낭   8 × 3", 36, TextAnchor.MiddleLeft, true);
        title.color = new Color(.96f, .90f, .78f, 1f);
        Anchor(title.rectTransform, .07f, .925f, .72f, .982f);

        Button closeTop = MakeButton(panel, "닫기", 22);
        Anchor(closeTop.GetComponent<RectTransform>(), .78f, .928f, .93f, .978f);
        closeTop.onClick.AddListener(Close);

        Text guide = MakeText(panel, "그리드 · 소지품 · 바닥을 같이 두고 끌어 장착   /   블록 탭 = 90° 회전", 18, TextAnchor.MiddleCenter, false);
        guide.color = new Color(.74f, .70f, .65f, 1f);
        guide.resizeTextForBestFit = true;
        guide.resizeTextMinSize = 14;
        guide.resizeTextMaxSize = 18;
        Anchor(guide.rectTransform, .055f, .878f, .945f, .922f);

        bagCountText = MakeText(panel, "", 19, TextAnchor.MiddleLeft, false);
        bagCountText.color = new Color(.76f, .73f, .68f, 1f);
        Anchor(bagCountText.rectTransform, .075f, .835f, .40f, .877f);

        totalText = MakeText(panel, "", 20, TextAnchor.MiddleRight, true);
        totalText.color = new Color(.96f, .75f, .30f, 1f);
        Anchor(totalText.rectTransform, .36f, .835f, .925f, .877f);

        gridRoot = new GameObject("BagGrid", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<RectTransform>();
        gridRoot.SetParent(panel, false);
        Image gridBg = gridRoot.GetComponent<Image>();
        gridBg.color = new Color(.025f, .025f, .030f, 1f);
        gridBg.raycastTarget = true;
        gridRoot.anchorMin = gridRoot.anchorMax = new Vector2(.5f, .5f);
        gridRoot.pivot = new Vector2(.5f, .5f);
        gridRoot.sizeDelta = new Vector2(GridW * CellSize, GridH * CellSize);
        gridRoot.anchoredPosition = new Vector2(0f, 300f);
        Frame(gridRoot, new Color(.43f, .38f, .31f, 1f), 3f);

        for (int y = 0; y < GridH; y++)
        {
            for (int x = 0; x < GridW; x++)
            {
                GameObject go = new GameObject("Cell_" + x + "_" + y, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                go.transform.SetParent(gridRoot, false);
                Image img = go.GetComponent<Image>();
                img.color = BaseCellColor();
                img.raycastTarget = false;

                RectTransform r = img.rectTransform;
                r.anchorMin = r.anchorMax = Vector2.zero;
                r.pivot = Vector2.zero;
                r.sizeDelta = new Vector2(CellSize - 4f, CellSize - 4f);
                r.anchoredPosition = new Vector2(x * CellSize + 2f, y * CellSize + 2f);
                gridCells[x, y] = img;
            }
        }

        heldCountText = MakeText(panel, "", 20, TextAnchor.MiddleLeft, true);
        heldCountText.color = new Color(.90f, .85f, .75f, 1f);
        Anchor(heldCountText.rectTransform, .07f, .555f, .93f, .602f);

        BuildHorizontalZone(
            panel,
            "HeldZone",
            .07f, .420f, .93f, .555f,
            out heldViewport,
            out heldContent,
            out heldZoneImage
        );

        groundCountText = MakeText(panel, "", 20, TextAnchor.MiddleLeft, true);
        groundCountText.color = new Color(.90f, .85f, .75f, 1f);
        Anchor(groundCountText.rectTransform, .07f, .370f, .93f, .417f);

        BuildHorizontalZone(
            panel,
            "GroundZone",
            .07f, .235f, .93f, .370f,
            out groundViewport,
            out groundContent,
            out groundZoneImage
        );

        detailText = MakeText(panel, "블록을 탭하면 90° 회전합니다. 끌어서 원하는 영역에 놓으세요.", 21, TextAnchor.MiddleCenter, false);
        detailText.color = new Color(.86f, .84f, .78f, 1f);
        detailText.resizeTextForBestFit = true;
        detailText.resizeTextMinSize = 15;
        detailText.resizeTextMaxSize = 21;
        Anchor(detailText.rectTransform, .07f, .125f, .93f, .222f);

        tidyButton = MakeButton(panel, "가방 자동 정리", 22);
        Anchor(tidyButton.GetComponent<RectTransform>(), .08f, .045f, .47f, .108f);
        tidyButton.onClick.AddListener(TidyBag);

        Button closeBottom = MakeButton(panel, "닫기", 22);
        Anchor(closeBottom.GetComponent<RectTransform>(), .53f, .045f, .92f, .108f);
        closeBottom.onClick.AddListener(Close);
    }

    void BuildHorizontalZone(
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
        GameObject vp = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
        vp.transform.SetParent(parent, false);

        viewport = vp.GetComponent<RectTransform>();
        Anchor(viewport, x1, y1, x2, y2);

        zoneImage = vp.GetComponent<Image>();
        zoneImage.color = BaseZoneColor();
        zoneImage.raycastTarget = true;
        Frame(viewport, new Color(.34f, .31f, .28f, 1f), 2f);

        GameObject contentGo = new GameObject(name + "Content", typeof(RectTransform));
        contentGo.transform.SetParent(vp.transform, false);
        content = contentGo.GetComponent<RectTransform>();
        content.anchorMin = new Vector2(0f, 0f);
        content.anchorMax = new Vector2(0f, 1f);
        content.pivot = new Vector2(0f, .5f);
        content.sizeDelta = new Vector2(viewport.rect.width, 0f);
        content.anchoredPosition = Vector2.zero;

        ScrollRect scroll = vp.GetComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.content = content;
        scroll.horizontal = true;
        scroll.vertical = false;
        scroll.movementType = ScrollRect.MovementType.Elastic;
        scroll.elasticity = .08f;
        scroll.inertia = true;
        scroll.decelerationRate = .08f;
        scroll.scrollSensitivity = 25f;
    }

    void Render()
    {
        DestroyItemViews();
        ClearChildren(heldContent);
        ClearChildren(groundContent);

        int bagCount = 0;
        int heldCount = 0;
        int groundCount = 0;
        int atk = 0;
        int def = 0;
        int hp = 0;

        for (int i = 0; i < items.Count; i++)
        {
            XTapGearBlockData item = items[i];
            NormalizeItem(item);

            if (item.location == XTapGearBlockData.LocationBag)
            {
                bagCount++;
                atk += item.attack;
                def += item.defense;
                hp += item.hp;
                CreateGridItemView(item);
            }
            else if (item.location == XTapGearBlockData.LocationHeld)
            {
                heldCount++;
            }
            else
            {
                groundCount++;
            }
        }

        RenderRow(XTapGearBlockData.LocationHeld, heldContent);
        RenderRow(XTapGearBlockData.LocationGround, groundContent);

        bagCountText.text = "가방 " + bagCount + "개 · " + OccupiedCellCount() + "/24칸";
        totalText.text = "총합  공 +" + atk + "   방 +" + def + "   체 +" + hp;
        heldCountText.text = "소지품 " + heldCount + " · 끌어 가방/바닥으로 이동";
        groundCountText.text = "바닥 " + groundCount + " · 끌어 가방/소지품으로 이동";

        RefreshSelectionText();
    }

    void DestroyItemViews()
    {
        foreach (var kv in itemViews)
            if (kv.Value != null) Destroy(kv.Value.gameObject);
        itemViews.Clear();
    }

    void ClearChildren(RectTransform root)
    {
        if (root == null) return;
        for (int i = root.childCount - 1; i >= 0; i--)
            Destroy(root.GetChild(i).gameObject);
    }

    void RenderRow(int location, RectTransform content)
    {
        float x = 8f;
        int count = 0;

        for (int i = 0; i < items.Count; i++)
        {
            XTapGearBlockData item = items[i];
            if (item.location != location) continue;

            float width = CreateRowItemView(item, content, x);
            x += width + 9f;
            count++;
        }

        float minWidth = 560f;
        content.sizeDelta = new Vector2(Mathf.Max(minWidth, x + 8f), 0f);

        if (count == 0)
        {
            Text empty = MakeText(content, "(없음)", 19, TextAnchor.MiddleLeft, false);
            empty.color = new Color(.48f, .46f, .45f, 1f);
            RectTransform r = empty.rectTransform;
            r.anchorMin = r.anchorMax = new Vector2(0f, .5f);
            r.pivot = new Vector2(0f, .5f);
            r.sizeDelta = new Vector2(200f, 80f);
            r.anchoredPosition = new Vector2(18f, 0f);
        }
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
        root.anchorMin = root.anchorMax = Vector2.zero;
        root.pivot = Vector2.zero;
        root.sizeDelta = new Vector2((maxX + 1) * CellSize, (maxY + 1) * CellSize);
        root.anchoredPosition = new Vector2(item.gridX * CellSize, item.gridY * CellSize);

        SetupTouch(go, item.id);
        DrawShape(root, item, CellSize, BlockColor(item), false, false);
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
        float width = Mathf.Max(230f, shapeWidth + 32f);

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
        root.sizeDelta = new Vector2(width, 168f);
        root.anchoredPosition = new Vector2(x, 0f);
        Frame(root, selectedId == item.id ? new Color(.78f, .61f, .29f, 1f) : new Color(.30f, .28f, .26f, 1f), 2f);

        SetupTouch(go, item.id);

        RectTransform shapeRoot = new GameObject("Shape", typeof(RectTransform)).GetComponent<RectTransform>();
        shapeRoot.SetParent(root, false);
        shapeRoot.anchorMin = shapeRoot.anchorMax = new Vector2(.5f, 1f);
        shapeRoot.pivot = new Vector2(.5f, 1f);
        shapeRoot.sizeDelta = new Vector2(shapeWidth, (maxY + 1) * MiniCell);
        shapeRoot.anchoredPosition = new Vector2(0f, -9f);
        DrawShape(shapeRoot, item, MiniCell, BlockColor(item), true, false);

        Text name = MakeText(root, item.displayName, 18, TextAnchor.MiddleCenter, true);
        name.color = new Color(.92f, .89f, .82f, 1f);
        name.resizeTextForBestFit = true;
        name.resizeTextMinSize = 13;
        name.resizeTextMaxSize = 18;
        Anchor(name.rectTransform, .05f, .19f, .95f, .40f);

        Text stat = MakeText(root, "공 " + item.attack + "   방 " + item.defense + "   체 " + item.hp + "   [탭=회전]", 16, TextAnchor.MiddleCenter, true);
        stat.color = new Color(.74f, .71f, .66f, 1f);
        stat.resizeTextForBestFit = true;
        stat.resizeTextMinSize = 12;
        stat.resizeTextMaxSize = 16;
        Anchor(stat.rectTransform, .04f, .02f, .96f, .20f);

        itemViews[item.id] = root;
        return width;
    }

    void AddGridStatBadge(RectTransform root, XTapGearBlockData item)
    {
        GameObject badgeGo = new GameObject("StatsBadge", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        badgeGo.transform.SetParent(root, false);

        Image badge = badgeGo.GetComponent<Image>();
        badge.color = new Color(.025f, .022f, .024f, .82f);
        badge.raycastTarget = false;

        RectTransform br = badge.rectTransform;
        br.anchorMin = new Vector2(0f, 0f);
        br.anchorMax = new Vector2(1f, 0f);
        br.pivot = new Vector2(.5f, 0f);
        br.sizeDelta = new Vector2(0f, 34f);
        br.anchoredPosition = new Vector2(0f, 3f);

        Text t = MakeText(badgeGo.transform,
            "공 " + item.attack + "  방 " + item.defense + "  체 " + item.hp,
            18, TextAnchor.MiddleCenter, true);
        t.color = new Color(1f, .88f, .54f, 1f);
        t.resizeTextForBestFit = true;
        t.resizeTextMinSize = 11;
        t.resizeTextMaxSize = 18;
        Anchor(t.rectTransform, .03f, .02f, .97f, .98f);
    }

    public int EquippedAttack
    {
        get
        {
            int total = 0;
            for (int i = 0; i < items.Count; i++)
                if (items[i].location == XTapGearBlockData.LocationBag)
                    total += Mathf.Max(0, items[i].attack);
            return total;
        }
    }

    public int EquippedDefense
    {
        get
        {
            int total = 0;
            for (int i = 0; i < items.Count; i++)
                if (items[i].location == XTapGearBlockData.LocationBag)
                    total += Mathf.Max(0, items[i].defense);
            return total;
        }
    }

    public int EquippedHp
    {
        get
        {
            int total = 0;
            for (int i = 0; i < items.Count; i++)
                if (items[i].location == XTapGearBlockData.LocationBag)
                    total += Mathf.Max(0, items[i].hp);
            return total;
        }
    }

    void SetupTouch(GameObject go, string itemId)
    {
        XTapBagItemTouch touch = go.GetComponent<XTapBagItemTouch>();
        touch.owner = this;
        touch.itemId = itemId;
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
        float height = (maxY + 1) * cellSize;
        Vector2 origin = centered ? new Vector2(-width * .5f, -height) : Vector2.zero;

        for (int i = 0; i < cells.Count; i++)
        {
            Vector2Int p = cells[i];

            GameObject cg = new GameObject("Piece", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            cg.transform.SetParent(parent, false);
            Image ci = cg.GetComponent<Image>();
            ci.color = color;
            ci.raycastTarget = false;

            RectTransform cr = ci.rectTransform;
            cr.anchorMin = cr.anchorMax = centered ? new Vector2(.5f, 1f) : Vector2.zero;
            cr.pivot = Vector2.zero;
            cr.sizeDelta = new Vector2(cellSize - 5f, cellSize - 5f);
            cr.anchoredPosition = origin + new Vector2(p.x * cellSize + 2.5f, p.y * cellSize + 2.5f);

            GameObject inset = new GameObject("Inset", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            inset.transform.SetParent(cg.transform, false);
            Image ii = inset.GetComponent<Image>();
            ii.color = new Color(.08f, .085f, .10f, .88f);
            ii.raycastTarget = false;
            Anchor(ii.rectTransform, .13f, .13f, .87f, .87f);

            if (showStats && i == 0)
            {
                Text s = MakeText(cg.transform, item.attack + "/" + item.defense + "/" + item.hp, 12, TextAnchor.MiddleCenter, true);
                s.color = new Color(.96f, .92f, .82f, 1f);
                s.resizeTextForBestFit = true;
                s.resizeTextMinSize = 8;
                s.resizeTextMaxSize = 12;
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
            dragOffsetY = Mathf.Clamp(cellY - item.gridY, 0, GridH - 1);
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

        if (RectTransformUtility.RectangleContainsScreenPoint(gridRoot, screen, null))
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

        if (RectTransformUtility.RectangleContainsScreenPoint(gridRoot, screen, null))
        {
            int cx;
            int cy;
            ScreenToCell(screen, out cx, out cy);
            int tx = cx - dragOffsetX;
            int ty = cy - dragOffsetY;

            if (CanPlace(item, tx, ty, item.rotation, item.id))
            {
                item.location = XTapGearBlockData.LocationBag;
                item.gridX = tx;
                item.gridY = ty;
                moved = true;
            }
        }
        else if (RectTransformUtility.RectangleContainsScreenPoint(heldViewport, screen, null))
        {
            item.location = XTapGearBlockData.LocationHeld;
            item.gridX = -1;
            item.gridY = -1;
            moved = true;
        }
        else if (RectTransformUtility.RectangleContainsScreenPoint(groundViewport, screen, null))
        {
            item.location = XTapGearBlockData.LocationGround;
            item.gridX = -1;
            item.gridY = -1;
            moved = true;
        }

        if (!moved)
        {
            item.location = originalLocation;
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
        dragGhost.pivot = Vector2.zero;
        dragGhost.sizeDelta = new Vector2((maxX + 1) * CellSize, (maxY + 1) * CellSize);

        dragGhostGroup = dragGhost.GetComponent<CanvasGroup>();
        dragGhostGroup.alpha = .74f;
        dragGhostGroup.blocksRaycasts = false;
        dragGhostGroup.interactable = false;

        DrawShape(dragGhost, item, CellSize, BlockColor(item), false, false);
        dragGhost.SetAsLastSibling();
    }

    void DestroyDragGhost()
    {
        if (dragGhost != null)
            Destroy(dragGhost.gameObject);

        dragGhost = null;
        dragGhostGroup = null;
    }

    void PositionGhostAtGridOrigin(XTapGearBlockData item, int gridX, int gridY)
    {
        if (dragGhost == null) return;

        Vector3 gridLocal = new Vector3(
            gridRoot.rect.xMin + gridX * CellSize,
            gridRoot.rect.yMin + gridY * CellSize,
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
            dragOffsetY * CellSize + CellSize * .5f
        );
    }

    void TidyBag()
    {
        List<XTapGearBlockData> bagItems = new List<XTapGearBlockData>();
        Dictionary<string, Vector3Int> backup = new Dictionary<string, Vector3Int>();

        for (int i = 0; i < items.Count; i++)
        {
            XTapGearBlockData item = items[i];
            if (item.location != XTapGearBlockData.LocationBag) continue;

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

            for (int y = 0; y < GridH; y++)
            {
                for (int x = 0; x < GridW; x++)
                {
                    if (CanPlace(item, x, y, rot, item.id))
                    {
                        item.location = XTapGearBlockData.LocationBag;
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
        List<Vector2Int> cells = GetCells(item, rotation);
        HashSet<int> occupied = new HashSet<int>();

        for (int i = 0; i < items.Count; i++)
        {
            XTapGearBlockData other = items[i];
            if (other.id == ignoreId) continue;
            if (other.location != XTapGearBlockData.LocationBag) continue;
            if (other.gridX < 0 || other.gridY < 0) continue;

            List<Vector2Int> oc = GetCells(other);
            for (int j = 0; j < oc.Count; j++)
            {
                int x = other.gridX + oc[j].x;
                int y = other.gridY + oc[j].y;
                if (x >= 0 && x < GridW && y >= 0 && y < GridH)
                    occupied.Add(y * GridW + x);
            }
        }

        for (int i = 0; i < cells.Count; i++)
        {
            int x = ox + cells[i].x;
            int y = oy + cells[i].y;

            if (x < 0 || x >= GridW || y < 0 || y >= GridH) return false;
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

            if (x < 0 || x >= GridW || y < 0 || y >= GridH) continue;

            gridCells[x, y].color = valid
                ? new Color(.18f, .46f, .23f, 1f)
                : new Color(.53f, .14f, .15f, 1f);
        }
    }

    void ClearGridHighlight()
    {
        for (int y = 0; y < GridH; y++)
            for (int x = 0; x < GridW; x++)
                if (gridCells[x, y] != null)
                    gridCells[x, y].color = BaseCellColor();
    }

    void ClearZoneHighlight()
    {
        if (heldZoneImage != null) heldZoneImage.color = BaseZoneColor();
        if (groundZoneImage != null) groundZoneImage.color = BaseZoneColor();
    }

    Color BaseCellColor()
    {
        return new Color(.075f, .075f, .085f, 1f);
    }

    Color BaseZoneColor()
    {
        return new Color(.052f, .050f, .055f, 1f);
    }

    void ScreenToCell(Vector2 screen, out int x, out int y)
    {
        Vector2 local;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(gridRoot, screen, null, out local);
        Vector2 fromBottomLeft = local - gridRoot.rect.min;

        x = Mathf.FloorToInt(fromBottomLeft.x / CellSize);
        y = Mathf.FloorToInt(fromBottomLeft.y / CellSize);
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
            if (items[i].location == XTapGearBlockData.LocationBag)
                total += Mathf.Max(0, items[i].cellCount);

        return Mathf.Clamp(total, 0, GridW * GridH);
    }

    void RefreshSelectionText()
    {
        XTapGearBlockData item = Find(selectedId);

        if (item == null)
        {
            detailText.text = "블록을 탭하면 90° 회전합니다. 끌어서 원하는 영역에 놓으세요.";
            return;
        }

        string corr = item.correction > 0 ? "+" + item.correction + "%" : item.correction + "%";
        string zone = item.location == XTapGearBlockData.LocationBag
            ? "가방"
            : (item.location == XTapGearBlockData.LocationHeld ? "소지품" : "바닥");

        detailText.text =
            zone + " · " + item.displayName + "  [" + item.cellCount + "칸 / " + corr + "]\n" +
            "공 +" + item.attack + "   방 +" + item.defense + "   체 +" + item.hp +
            "     [탭=90° 회전 / 끌기=이동]";
    }

    void NormalizeItem(XTapGearBlockData item)
    {
        if (item == null) return;

        RecoverMissingBlockStats(item);
        item.rotation = ((item.rotation % 4) + 4) % 4;

        if (item.location < XTapGearBlockData.LocationBag || item.location > XTapGearBlockData.LocationGround)
            item.location = XTapGearBlockData.LocationHeld;

        // Old saves only had grid coordinates. Keep valid placed blocks equipped;
        // anything without a valid grid position becomes carried inventory.
        if (item.location == XTapGearBlockData.LocationBag && (item.gridX < 0 || item.gridY < 0))
            item.location = XTapGearBlockData.LocationHeld;

        if (item.location != XTapGearBlockData.LocationBag)
        {
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
        item.hp = Mathf.Max(1, total - item.attack - item.defense);
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
        Frame(bg.rectTransform, new Color(.52f, .43f, .30f, 1f), 2f);

        Text t = MakeText(go.transform, label, fontSize, TextAnchor.MiddleCenter, true);
        t.color = new Color(.95f, .90f, .80f, 1f);
        Anchor(t.rectTransform, .04f, .04f, .96f, .96f);

        Button b = go.GetComponent<Button>();
        b.targetGraphic = bg;

        ColorBlock cb = b.colors;
        cb.normalColor = Color.white;
        cb.highlightedColor = new Color(1f, .94f, .84f, 1f);
        cb.pressedColor = new Color(.70f, .62f, .55f, 1f);
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
