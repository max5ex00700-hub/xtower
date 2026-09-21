using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[Serializable]
public sealed class XTapGearBlockData
{
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
}

public sealed class XTapInventory : MonoBehaviour
{
    const int GridW = 8;
    const int GridH = 3;
    const float CellSize = 68f;
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
    RectTransform panel;
    RectTransform gridRoot;
    Text countText;
    Text totalText;
    Text detailText;
    Button rotateButton;
    Button tidyButton;

    readonly List<XTapGearBlockData> items = new List<XTapGearBlockData>();
    readonly Dictionary<string, RectTransform> itemViews = new Dictionary<string, RectTransform>();
    readonly Image[,] gridCells = new Image[GridW, GridH];

    string selectedId;
    string draggingId;
    int dragOffsetX;
    int dragOffsetY;
    Vector2 dragOriginalPos;
    int previewX = -99;
    int previewY = -99;

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
        draggingId = null;
        ClearGridHighlight();
        if (overlay != null) overlay.SetActive(false);
        if (onClosed != null) onClosed();
    }

    public bool TryAddBlock(XTapGearBlockData item)
    {
        if (item == null) return false;
        if (string.IsNullOrEmpty(item.id)) item.id = Guid.NewGuid().ToString("N");
        item.gridX = -1;
        item.gridY = -1;
        item.rotation = 0;

        if (!FindFirstPlacement(item))
            return false;

        items.Add(item);
        Save();
        if (IsOpen) Render();
        return true;
    }

    public bool HasSpaceFor(XTapGearBlockData item)
    {
        if (item == null) return false;
        int ox = item.gridX;
        int oy = item.gridY;
        int orot = item.rotation;
        bool fits = FindFirstPlacement(item);
        item.gridX = ox;
        item.gridY = oy;
        item.rotation = orot;
        return fits;
    }

    void BuildUi()
    {
        overlay = new GameObject("BagOverlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        overlay.transform.SetParent(host, false);
        Image dim = overlay.GetComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, .88f);
        dim.raycastTarget = true;
        Anchor(dim.rectTransform, 0f, 0f, 1f, 1f);

        panel = new GameObject("BagPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<RectTransform>();
        panel.SetParent(overlay.transform, false);
        Image body = panel.GetComponent<Image>();
        body.color = new Color(.045f, .042f, .045f, .99f);
        body.raycastTarget = true;
        panel.anchorMin = new Vector2(.5f, .5f);
        panel.anchorMax = new Vector2(.5f, .5f);
        panel.pivot = new Vector2(.5f, .5f);
        panel.sizeDelta = new Vector2(690f, 760f);
        Frame(panel, new Color(.52f, .43f, .30f, 1f), 4f);

        Text title = MakeText(panel, "배 낭   8 × 3", 36, TextAnchor.MiddleCenter, true);
        title.color = new Color(.96f, .90f, .78f, 1f);
        Anchor(title.rectTransform, .07f, .88f, .93f, .97f);

        countText = MakeText(panel, "", 22, TextAnchor.MiddleLeft, false);
        countText.color = new Color(.74f, .72f, .68f, 1f);
        Anchor(countText.rectTransform, .08f, .82f, .48f, .88f);

        totalText = MakeText(panel, "", 22, TextAnchor.MiddleRight, true);
        totalText.color = new Color(.96f, .75f, .30f, 1f);
        Anchor(totalText.rectTransform, .40f, .82f, .92f, .88f);

        gridRoot = new GameObject("BagGrid", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<RectTransform>();
        gridRoot.SetParent(panel, false);
        Image gridBg = gridRoot.GetComponent<Image>();
        gridBg.color = new Color(.025f, .025f, .030f, 1f);
        gridBg.raycastTarget = true;
        gridRoot.anchorMin = gridRoot.anchorMax = new Vector2(.5f, .5f);
        gridRoot.pivot = new Vector2(.5f, .5f);
        gridRoot.sizeDelta = new Vector2(GridW * CellSize, GridH * CellSize);
        gridRoot.anchoredPosition = new Vector2(0f, 70f);
        Frame(gridRoot, new Color(.43f, .38f, .31f, 1f), 3f);

        for (int y = 0; y < GridH; y++)
        {
            for (int x = 0; x < GridW; x++)
            {
                GameObject go = new GameObject("Cell_" + x + "_" + y, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                go.transform.SetParent(gridRoot, false);
                Image img = go.GetComponent<Image>();
                img.color = new Color(.075f, .075f, .085f, 1f);
                img.raycastTarget = false;
                RectTransform r = img.rectTransform;
                r.anchorMin = r.anchorMax = Vector2.zero;
                r.pivot = Vector2.zero;
                r.sizeDelta = new Vector2(CellSize - 4f, CellSize - 4f);
                r.anchoredPosition = new Vector2(x * CellSize + 2f, y * CellSize + 2f);
                gridCells[x, y] = img;
            }
        }

        detailText = MakeText(panel, "블록을 눌러 선택하거나 드래그해서 옮기세요.", 23, TextAnchor.MiddleCenter, false);
        detailText.color = new Color(.86f, .84f, .78f, 1f);
        detailText.resizeTextForBestFit = true;
        detailText.resizeTextMinSize = 17;
        detailText.resizeTextMaxSize = 23;
        Anchor(detailText.rectTransform, .08f, .31f, .92f, .43f);

        rotateButton = MakeButton(panel, "↻ 90° 회전", 24);
        Anchor(rotateButton.GetComponent<RectTransform>(), .10f, .18f, .47f, .28f);
        rotateButton.onClick.AddListener(RotateSelected);

        tidyButton = MakeButton(panel, "자동 정리", 24);
        Anchor(tidyButton.GetComponent<RectTransform>(), .53f, .18f, .90f, .28f);
        tidyButton.onClick.AddListener(Tidy);

        Button close = MakeButton(panel, "닫기", 25);
        Anchor(close.GetComponent<RectTransform>(), .31f, .055f, .69f, .145f);
        close.onClick.AddListener(Close);
    }

    void Render()
    {
        foreach (var kv in itemViews)
            if (kv.Value != null) Destroy(kv.Value.gameObject);
        itemViews.Clear();

        int atk = 0, def = 0, hp = 0;
        for (int i = 0; i < items.Count; i++)
        {
            XTapGearBlockData item = items[i];
            atk += item.attack;
            def += item.defense;
            hp += item.hp;
            CreateItemView(item);
        }

        countText.text = "블록 " + items.Count + "개";
        totalText.text = "공 +" + atk + "   방 +" + def + "   체 +" + hp;
        RefreshSelectionText();
    }

    void CreateItemView(XTapGearBlockData item)
    {
        if (item.gridX < 0 || item.gridY < 0) return;

        List<Vector2Int> cells = GetCells(item);
        int maxX = 0, maxY = 0;
        for (int i = 0; i < cells.Count; i++)
        {
            maxX = Mathf.Max(maxX, cells[i].x);
            maxY = Mathf.Max(maxY, cells[i].y);
        }

        GameObject go = new GameObject("Block_" + item.id, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(XTapBagItemTouch));
        go.transform.SetParent(gridRoot, false);
        Image hit = go.GetComponent<Image>();
        hit.color = new Color(1f, 1f, 1f, .001f);
        hit.raycastTarget = true;

        RectTransform root = go.GetComponent<RectTransform>();
        root.anchorMin = root.anchorMax = Vector2.zero;
        root.pivot = Vector2.zero;
        root.sizeDelta = new Vector2((maxX + 1) * CellSize, (maxY + 1) * CellSize);
        root.anchoredPosition = new Vector2(item.gridX * CellSize, item.gridY * CellSize);

        XTapBagItemTouch touch = go.GetComponent<XTapBagItemTouch>();
        touch.owner = this;
        touch.itemId = item.id;

        Color blockColor = item.exclusive
            ? new Color(.65f, .22f, .80f, .98f)
            : CorrectionColor(item.correction);

        for (int i = 0; i < cells.Count; i++)
        {
            Vector2Int p = cells[i];
            GameObject cg = new GameObject("Piece", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            cg.transform.SetParent(go.transform, false);
            Image ci = cg.GetComponent<Image>();
            ci.color = blockColor;
            ci.raycastTarget = false;

            RectTransform cr = ci.rectTransform;
            cr.anchorMin = cr.anchorMax = Vector2.zero;
            cr.pivot = Vector2.zero;
            cr.sizeDelta = new Vector2(CellSize - 6f, CellSize - 6f);
            cr.anchoredPosition = new Vector2(p.x * CellSize + 3f, p.y * CellSize + 3f);

            GameObject inset = new GameObject("Inset", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            inset.transform.SetParent(cg.transform, false);
            Image ii = inset.GetComponent<Image>();
            ii.color = new Color(.08f, .085f, .10f, .88f);
            ii.raycastTarget = false;
            Anchor(ii.rectTransform, .13f, .13f, .87f, .87f);
        }

        itemViews[item.id] = root;
    }

    Color CorrectionColor(int correction)
    {
        if (correction > 0) return new Color(.88f, .58f, .14f, 1f);
        if (correction < 0) return new Color(.34f, .42f, .52f, 1f);
        return new Color(.72f, .70f, .60f, 1f);
    }

    public void Select(string id)
    {
        selectedId = id;
        RefreshSelectionText();
    }

    public void BeginDrag(string id, Vector2 screen)
    {
        XTapGearBlockData item = Find(id);
        RectTransform view;
        if (item == null || !itemViews.TryGetValue(id, out view)) return;

        selectedId = id;
        draggingId = id;
        dragOriginalPos = view.anchoredPosition;

        int cellX, cellY;
        ScreenToCell(screen, out cellX, out cellY);
        dragOffsetX = cellX - item.gridX;
        dragOffsetY = cellY - item.gridY;
        view.SetAsLastSibling();
        RefreshSelectionText();
    }

    public void Drag(string id, Vector2 screen)
    {
        if (draggingId != id) return;
        XTapGearBlockData item = Find(id);
        RectTransform view;
        if (item == null || !itemViews.TryGetValue(id, out view)) return;

        Vector2 local;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(gridRoot, screen, null, out local);
        view.anchoredPosition = local + gridRoot.sizeDelta * .5f - new Vector2(dragOffsetX * CellSize, dragOffsetY * CellSize);

        int cx, cy;
        ScreenToCell(screen, out cx, out cy);
        previewX = cx - dragOffsetX;
        previewY = cy - dragOffsetY;
        HighlightPlacement(item, previewX, previewY);
    }

    public void EndDrag(string id, Vector2 screen)
    {
        if (draggingId != id) return;
        XTapGearBlockData item = Find(id);
        RectTransform view;
        if (item == null || !itemViews.TryGetValue(id, out view))
        {
            draggingId = null;
            return;
        }

        int cx, cy;
        ScreenToCell(screen, out cx, out cy);
        int tx = cx - dragOffsetX;
        int ty = cy - dragOffsetY;

        if (CanPlace(item, tx, ty, item.rotation, item.id))
        {
            item.gridX = tx;
            item.gridY = ty;
            Save();
        }

        draggingId = null;
        previewX = previewY = -99;
        ClearGridHighlight();
        Render();
    }

    void RotateSelected()
    {
        XTapGearBlockData item = Find(selectedId);
        if (item == null) return;

        int next = (item.rotation + 1) % 4;
        if (CanPlace(item, item.gridX, item.gridY, next, item.id))
        {
            item.rotation = next;
            Save();
            Render();
            return;
        }

        detailText.text = "그 자리에서는 회전할 공간이 부족합니다.";
    }

    void Tidy()
    {
        List<XTapGearBlockData> snapshot = new List<XTapGearBlockData>(items);
        for (int i = 0; i < items.Count; i++)
        {
            items[i].gridX = -1;
            items[i].gridY = -1;
        }

        snapshot.Sort((a, b) => b.cellCount.CompareTo(a.cellCount));
        bool ok = true;
        for (int i = 0; i < snapshot.Count; i++)
        {
            if (!FindFirstPlacement(snapshot[i]))
            {
                ok = false;
                break;
            }
        }

        if (!ok)
        {
            detailText.text = "현재 블록 조합은 8×3 안에 모두 배치할 수 없습니다.";
            Load();
        }
        else
        {
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
            if (other.id == ignoreId || other.gridX < 0) continue;
            List<Vector2Int> oc = GetCells(other);
            for (int j = 0; j < oc.Count; j++)
            {
                int x = other.gridX + oc[j].x;
                int y = other.gridY + oc[j].y;
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

        int minX = 999, minY = 999;
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
            int x, y;
            if (xy.Length == 2 && int.TryParse(xy[0], out x) && int.TryParse(xy[1], out y))
                result.Add(new Vector2Int(x, y));
        }
        if (result.Count == 0) result.Add(Vector2Int.zero);
        return result;
    }

    void HighlightPlacement(XTapGearBlockData item, int ox, int oy)
    {
        ClearGridHighlight();
        bool valid = CanPlace(item, ox, oy, item.rotation, item.id);
        List<Vector2Int> cells = GetCells(item);
        for (int i = 0; i < cells.Count; i++)
        {
            int x = ox + cells[i].x;
            int y = oy + cells[i].y;
            if (x < 0 || x >= GridW || y < 0 || y >= GridH) continue;
            gridCells[x, y].color = valid
                ? new Color(.18f, .42f, .22f, 1f)
                : new Color(.48f, .15f, .15f, 1f);
        }
    }

    void ClearGridHighlight()
    {
        for (int y = 0; y < GridH; y++)
            for (int x = 0; x < GridW; x++)
                if (gridCells[x, y] != null)
                    gridCells[x, y].color = new Color(.075f, .075f, .085f, 1f);
    }

    void ScreenToCell(Vector2 screen, out int x, out int y)
    {
        Vector2 local;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(gridRoot, screen, null, out local);
        Vector2 fromBottomLeft = local + gridRoot.sizeDelta * .5f;
        x = Mathf.FloorToInt(fromBottomLeft.x / CellSize);
        y = Mathf.FloorToInt(fromBottomLeft.y / CellSize);
    }

    XTapGearBlockData Find(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        for (int i = 0; i < items.Count; i++)
            if (items[i].id == id) return items[i];
        return null;
    }

    void RefreshSelectionText()
    {
        XTapGearBlockData item = Find(selectedId);
        if (item == null)
        {
            detailText.text = "블록을 눌러 선택하거나 드래그해서 옮기세요.";
            rotateButton.interactable = false;
            return;
        }

        rotateButton.interactable = true;
        string corr = item.correction > 0 ? "+" + item.correction + "%" : item.correction + "%";
        detailText.text = item.displayName + "  [" + item.cellCount + "칸 / " + corr + "]\n공 +" +
                          item.attack + "   방 +" + item.defense + "   체 +" + item.hp;
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
                items.AddRange(data.items);
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
        if (owner != null) owner.Select(itemId);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (owner != null) owner.BeginDrag(itemId, eventData.position);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (owner != null) owner.Drag(itemId, eventData.position);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (owner != null) owner.EndDrag(itemId, eventData.position);
    }
}
