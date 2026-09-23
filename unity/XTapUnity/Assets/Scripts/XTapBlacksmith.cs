using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public sealed class XTapBlacksmith : MonoBehaviour
{
    const float UiFontScale = 1.70f;
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
    Text targetSlotText;
    Text materialSlotText;
    RectTransform targetSlot;
    RectTransform materialSlot;

    Texture2D skinAtlas;
    Sprite backgroundSkin;
    Sprite panelSkin;
    Sprite slotSkin;
    Sprite tabNormalSkin;
    Sprite tabSelectedSkin;
    Sprite buttonNeutralSkin;
    Sprite buttonPrimarySkin;
    Sprite listRowSkin;
    Sprite statusBarSkin;

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

        LoadVisualAssets();
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
        if (backgroundSkin != null)
        {
            dim.sprite = backgroundSkin;
            dim.type = Image.Type.Simple;
            dim.preserveAspect = false;
            dim.color = Color.white;
        }
        else
        {
            dim.color = new Color(.012f, .008f, .006f, .992f);
        }
        dim.raycastTarget = true;
        Anchor(dim.rectTransform, 0f, 0f, 1f, 1f);

        panel = new GameObject("ForgePanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<RectTransform>();
        panel.SetParent(overlay.transform, false);
        Anchor(panel, 0f, 0f, 1f, 1f);

        Image body = panel.GetComponent<Image>();
        body.color = new Color(0f, 0f, 0f, .16f);
        body.raycastTarget = true;

        Text title = MakeText(panel, "대장간", 31, TextAnchor.MiddleLeft, true);
        title.color = new Color(1f, .73f, .30f, 1f);
        Anchor(title.rectTransform, .045f, .935f, .62f, .995f);

        Button close = MakeButton(panel, "닫기", 17, new Color(.13f, .075f, .050f, 1f));
        ApplyButtonSkin(close, buttonNeutralSkin);
        Anchor(close.GetComponent<RectTransform>(), .80f, .945f, .955f, .990f);
        close.onClick.AddListener(Close);

        enhanceTab = MakeButton(panel, "강화", 19, new Color(.28f, .12f, .035f, 1f));
        synthesisTab = MakeButton(panel, "합성", 19, new Color(.09f, .07f, .065f, 1f));
        dismantleTab = MakeButton(panel, "분해", 19, new Color(.09f, .07f, .065f, 1f));
        ApplyButtonSkin(enhanceTab, tabNormalSkin);
        ApplyButtonSkin(synthesisTab, tabNormalSkin);
        ApplyButtonSkin(dismantleTab, tabNormalSkin);

        Anchor(enhanceTab.GetComponent<RectTransform>(), .035f, .855f, .325f, .925f);
        Anchor(synthesisTab.GetComponent<RectTransform>(), .355f, .855f, .645f, .925f);
        Anchor(dismantleTab.GetComponent<RectTransform>(), .675f, .855f, .965f, .925f);

        enhanceTab.onClick.AddListener(delegate { SetMode(ForgeMode.Enhance); });
        synthesisTab.onClick.AddListener(delegate { SetMode(ForgeMode.Synthesis); });
        dismantleTab.onClick.AddListener(delegate { SetMode(ForgeMode.Dismantle); });

        Text slotGuide = MakeText(panel, "소지품/바닥 블록을 선택해 슬롯에 넣으세요", 13, TextAnchor.MiddleCenter, false);
        slotGuide.color = new Color(.82f, .72f, .60f, 1f);
        Anchor(slotGuide.rectTransform, .05f, .815f, .95f, .850f);

        targetSlot = MakeSlotPanel(panel, "TargetSlot", .055f, .595f, .465f, .805f);
        materialSlot = MakeSlotPanel(panel, "MaterialSlot", .535f, .595f, .945f, .805f);

        Text targetLabel = MakeText(targetSlot, "대상", 18, TextAnchor.UpperCenter, true);
        targetLabel.color = new Color(1f, .80f, .42f, 1f);
        Anchor(targetLabel.rectTransform, .04f, .72f, .96f, .98f);

        targetSlotText = MakeText(targetSlot, "+", 18, TextAnchor.MiddleCenter, true);
        targetSlotText.color = new Color(.92f, .87f, .80f, 1f);
        Anchor(targetSlotText.rectTransform, .08f, .10f, .92f, .72f);

        Text materialLabel = MakeText(materialSlot, "제물", 18, TextAnchor.UpperCenter, true);
        materialLabel.color = new Color(1f, .80f, .42f, 1f);
        Anchor(materialLabel.rectTransform, .04f, .72f, .96f, .98f);

        materialSlotText = MakeText(materialSlot, "+", 18, TextAnchor.MiddleCenter, true);
        materialSlotText.color = new Color(.92f, .87f, .80f, 1f);
        Anchor(materialSlotText.rectTransform, .08f, .10f, .92f, .72f);

        RectTransform ruleBar = MakePanel(panel, "RuleBar", new Color(.04f, .03f, .025f, .92f));
        Anchor(ruleBar, .035f, .525f, .965f, .592f);
        ApplyPanelSkin(ruleBar, statusBarSkin);

        ruleText = MakeText(panel, "", 13, TextAnchor.MiddleCenter, true);
        ruleText.color = new Color(1f, .86f, .58f, 1f);
        Anchor(ruleText.rectTransform, .055f, .535f, .945f, .585f);

        Text inventoryTitle = MakeText(panel, "소지품 · 바닥", 18, TextAnchor.MiddleLeft, true);
        inventoryTitle.color = new Color(.98f, .90f, .78f, 1f);
        Anchor(inventoryTitle.rectTransform, .045f, .490f, .55f, .535f);

        RectTransform listFrame = MakePanel(panel, "ForgeItems", new Color(.020f, .018f, .018f, 1f));
        Anchor(listFrame, .035f, .165f, .965f, .490f);
        ApplyPanelSkin(listFrame, panelSkin);

        GameObject scrollGo = new GameObject("ForgeScroll", typeof(RectTransform), typeof(ScrollRect));
        scrollGo.transform.SetParent(listFrame, false);
        RectTransform scrollRectTransform = scrollGo.GetComponent<RectTransform>();
        Anchor(scrollRectTransform, .012f, .012f, .988f, .988f);

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
        layout.padding = new RectOffset(10, 10, 10, 10);
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

        RectTransform statusBar = MakePanel(panel, "StatusBar", new Color(.04f, .03f, .025f, .92f));
        Anchor(statusBar, .035f, .080f, .965f, .162f);
        ApplyPanelSkin(statusBar, statusBarSkin);

        selectionText = MakeText(panel, "", 12, TextAnchor.MiddleLeft, true);
        selectionText.color = new Color(.92f, .84f, .72f, 1f);
        Anchor(selectionText.rectTransform, .045f, .120f, .70f, .160f);

        chanceText = MakeText(panel, "", 14, TextAnchor.MiddleRight, true);
        chanceText.color = new Color(1f, .66f, .20f, 1f);
        Anchor(chanceText.rectTransform, .70f, .120f, .955f, .160f);

        resultText = MakeText(panel, "블록을 선택하세요.", 12, TextAnchor.MiddleCenter, false);
        resultText.color = new Color(.88f, .82f, .74f, 1f);
        Anchor(resultText.rectTransform, .045f, .085f, .955f, .120f);

        Button clear = MakeButton(panel, "초기화", 16, new Color(.095f, .075f, .065f, 1f));
        ApplyButtonSkin(clear, buttonNeutralSkin);
        Anchor(clear.GetComponent<RectTransform>(), .045f, .020f, .405f, .078f);
        clear.onClick.AddListener(delegate
        {
            ClearSelection();
            resultText.text = "선택을 초기화했습니다.";
            Refresh();
        });

        executeButton = MakeButton(panel, "작업", 18, new Color(.38f, .16f, .045f, 1f));
        ApplyButtonSkin(executeButton, buttonPrimarySkin);
        Anchor(executeButton.GetComponent<RectTransform>(), .595f, .020f, .955f, .078f);
        executeButton.onClick.AddListener(Execute);

        SetMode(ForgeMode.Enhance);
    }

    RectTransform MakeSlotPanel(Transform parent, string name, float x1, float y1, float x2, float y2)
    {
        RectTransform slot = MakePanel(parent, name, new Color(.055f, .050f, .050f, 1f));
        Anchor(slot, x1, y1, x2, y2);
        ApplyPanelSkin(slot, slotSkin);
        if (slotSkin == null)
            Frame(slot, new Color(.38f, .27f, .17f, 1f), 2f);
        return slot;
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
        if (image == null) return;

        Sprite skin = selected ? tabSelectedSkin : tabNormalSkin;
        if (skin != null)
        {
            image.sprite = skin;
            image.type = Image.Type.Sliced;
            image.color = Color.white;
        }
        else
        {
            image.color = selected
                ? new Color(.34f, .16f, .055f, 1f)
                : new Color(.105f, .072f, .060f, 1f);
        }

        Text label = button.GetComponentInChildren<Text>();
        if (label != null)
            label.color = selected
                ? new Color(1f, .90f, .60f, 1f)
                : new Color(.91f, .84f, .73f, 1f);
    }

    void RefreshRules()
    {
        if (ruleText == null) return;

        if (mode == ForgeMode.Enhance)
            ruleText.text = "제물 1개당 성공률 +10%  ·  성공 시 공+1 / 체+5 / 짝수 강화 방+1";
        else if (mode == ForgeMode.Synthesis)
            ruleText.text = "대상 1개 + 제물 1개  ·  성공률 1%  ·  성공 시 제물 능력 흡수";
        else
            ruleText.text = "제물 최대 10개  ·  1개당 10%  ·  성공 시 플레이어 가방 +1칸";
    }

    void RebuildItemList()
    {
        for (int i = listContent.childCount - 1; i >= 0; i--)
            Destroy(listContent.GetChild(i).gameObject);

        List<XTapGearBlockData> allItems = inventory.GetForgeItems();
        List<XTapGearBlockData> items = new List<XTapGearBlockData>();

        for (int i = 0; i < allItems.Count; i++)
        {
            XTapGearBlockData item = allItems[i];
            if (item == null) continue;
            if (item.location == XTapGearBlockData.LocationHeld ||
                item.location == XTapGearBlockData.LocationGround)
                items.Add(item);
        }

        if (items.Count == 0)
        {
            Text empty = MakeText(listContent, "사용할 블록이 없습니다.", 21, TextAnchor.MiddleCenter, false);
            empty.color = new Color(.55f, .50f, .47f, 1f);
            LayoutElement le = empty.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = 170f;
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
            if (listRowSkin != null)
            {
                bg.sprite = listRowSkin;
                bg.type = Image.Type.Sliced;
            }

            if (isTarget)
                bg.color = new Color(1f, .72f, .30f, 1f);
            else if (isMaterial)
                bg.color = new Color(.76f, .88f, .62f, 1f);
            else
                bg.color = Color.white;

            LayoutElement le = row.GetComponent<LayoutElement>();
            le.preferredHeight = 112f;

            string prefix = isTarget ? "[대상] " : (isMaterial ? "[제물] " : "");
            string enhance = item.enhanceLevel > 0 ? " +" + item.enhanceLevel : "";
            string line1 = prefix + "[" + LocationName(item) + "] " + item.displayName + enhance;
            string line2 = XTapStatFormat.BlockTriplet(item.attack, item.defense, item.hp, "   ");

            Text t = MakeText(row.transform, line1 + "\n" + line2, 14, TextAnchor.MiddleLeft, isTarget || isMaterial);
            t.color = isTarget
                ? new Color(1f, .78f, .34f, 1f)
                : (isMaterial ? new Color(.80f, .90f, .66f, 1f) : new Color(.91f, .87f, .81f, 1f));
            t.resizeTextForBestFit = true;
            t.resizeTextMinSize = 18;
            t.resizeTextMaxSize = 26;
            Anchor(t.rectTransform, .14f, .10f, .90f, .90f);

            Button b = row.GetComponent<Button>();
            b.targetGraphic = bg;
            string capturedId = id;
            b.onClick.AddListener(delegate { OnItemPressed(capturedId); });
        }
    }

    string LocationName(XTapGearBlockData item)
    {
        if (item == null) return "없음";
        if (item.location == XTapGearBlockData.LocationHeld) return "소지품";
        if (item.location == XTapGearBlockData.LocationGround) return "바닥";
        return "장착";
    }

    void OnItemPressed(string id)
    {
        if (string.IsNullOrEmpty(id)) return;
        XTapGearBlockData pressed = inventory.FindForgeItem(id);
        if (pressed == null) return;
        if (pressed.location != XTapGearBlockData.LocationHeld &&
            pressed.location != XTapGearBlockData.LocationGround)
            return;

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

        if (targetSlot != null)
            targetSlot.gameObject.SetActive(mode != ForgeMode.Dismantle);

        if (materialSlot != null)
        {
            if (mode == ForgeMode.Dismantle)
                Anchor(materialSlot, .18f, .595f, .82f, .805f);
            else
                Anchor(materialSlot, .535f, .595f, .945f, .805f);
        }

        if (targetSlotText != null)
        {
            targetSlotText.text = target == null
                ? "+"
                : target.displayName +
                  (target.enhanceLevel > 0 ? "  +" + target.enhanceLevel : "") +
                  "\n" + XTapStatFormat.BlockTriplet(target.attack, target.defense, target.hp, "   ");
        }

        double sacrificeAttack = 0d;
        double sacrificeDefense = 0d;
        double sacrificeHp = 0d;
        int sacrificeCount = 0;
        XTapGearBlockData singleSacrifice = null;

        for (int i = 0; i < materialIds.Count; i++)
        {
            XTapGearBlockData material = inventory.FindForgeItem(materialIds[i]);
            if (material == null) continue;

            sacrificeCount++;
            sacrificeAttack = XTapStatFormat.SafeAdd(sacrificeAttack, Math.Max(0d, material.attack));
            sacrificeDefense = XTapStatFormat.SafeAdd(sacrificeDefense, Math.Max(0d, material.defense));
            sacrificeHp = XTapStatFormat.SafeAdd(sacrificeHp, Math.Max(0d, material.hp));
            if (sacrificeCount == 1)
                singleSacrifice = material;
        }

        if (materialSlotText != null)
        {
            if (sacrificeCount == 0)
            {
                materialSlotText.text = "+";
            }
            else if (sacrificeCount == 1 && singleSacrifice != null)
            {
                materialSlotText.text =
                    singleSacrifice.displayName +
                    (singleSacrifice.enhanceLevel > 0 ? "  +" + singleSacrifice.enhanceLevel : "") +
                    "\n" + XTapStatFormat.BlockTriplet(singleSacrifice.attack, singleSacrifice.defense, singleSacrifice.hp, "   ");
            }
            else
            {
                materialSlotText.text =
                    "제물 " + sacrificeCount + "개" +
                    "\n합계  " + XTapStatFormat.BlockTriplet(sacrificeAttack, sacrificeDefense, sacrificeHp, "   ");
            }
        }

        if (mode == ForgeMode.Enhance)
        {
            int chance = Mathf.Clamp(sacrificeCount * 10, 0, 100);
            selectionText.text = sacrificeCount + "/10 제물";
            chanceText.text = "성공률 " + chance + "%";
            executeButton.interactable = target != null && sacrificeCount > 0 && target.enhanceLevel < 10;
        }
        else if (mode == ForgeMode.Synthesis)
        {
            selectionText.text = sacrificeCount == 1 ? "대상 + 제물 준비" : "대상 + 제물 1개";
            chanceText.text = "성공률 1%";
            executeButton.interactable = target != null && sacrificeCount == 1;
        }
        else
        {
            int chance = Mathf.Clamp(sacrificeCount * 10, 0, 100);
            selectionText.text = "제물 " + sacrificeCount + "/10";
            chanceText.text = "가방 +1칸  " + chance + "%";
            executeButton.interactable = sacrificeCount > 0;
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
            target.attack = XTapStatFormat.SafeAdd(target.attack, 1d);
            target.hp = XTapStatFormat.SafeAdd(target.hp, 5d);

            if (target.enhanceLevel % 2 == 0)
                target.defense = XTapStatFormat.SafeAdd(target.defense, 1d);

            inventory.CommitForgeChanges();
            resultText.text =
                "강화 성공!  +" + target.enhanceLevel +
                "   공 " + XTapStatFormat.Compact(target.attack) +
                " / 방 " + XTapStatFormat.Compact(target.defense) +
                " / 체 " + XTapStatFormat.Compact(target.hp);
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

        double addAttack = material.attack;
        double addDefense = material.defense;
        double addHp = material.hp;
        string consumedName = material.displayName;

        bool success = UnityEngine.Random.Range(0f, 100f) < 1f;
        inventory.RemoveForgeItem(material.id);

        if (success)
        {
            target.attack = XTapStatFormat.SafeAdd(target.attack, addAttack);
            target.defense = XTapStatFormat.SafeAdd(target.defense, addDefense);
            target.hp = XTapStatFormat.SafeAdd(target.hp, addHp);
            inventory.CommitForgeChanges();

            resultText.text =
                "합성 성공!  " + consumedName + " 능력을 흡수했습니다.  " +
                XTapStatFormat.BlockTriplet(addAttack, addDefense, addHp, " / ");
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

    void LoadVisualAssets()
    {
        try
        {
            TextAsset encoded = Resources.Load<TextAsset>("XTapBlacksmithUI/atlas");
            if (encoded == null || string.IsNullOrWhiteSpace(encoded.text))
                return;

            byte[] bytes = Convert.FromBase64String(encoded.text.Trim());
            skinAtlas = new Texture2D(2, 2, TextureFormat.RGB24, false);
            if (!skinAtlas.LoadImage(bytes, false))
            {
                Destroy(skinAtlas);
                skinAtlas = null;
                return;
            }

            skinAtlas.wrapMode = TextureWrapMode.Clamp;
            skinAtlas.filterMode = FilterMode.Bilinear;

            // Atlas is 256x512. Coordinates below use top-left design coordinates.
            backgroundSkin = MakeAtlasSprite(0, 0, 144, 256, Vector4.zero);
            panelSkin = MakeAtlasSprite(144, 0, 112, 84, new Vector4(18f, 18f, 18f, 18f));
            slotSkin = MakeAtlasSprite(144, 84, 112, 112, new Vector4(18f, 18f, 18f, 18f));
            tabNormalSkin = MakeAtlasSprite(0, 256, 128, 43, new Vector4(22f, 10f, 22f, 10f));
            tabSelectedSkin = MakeAtlasSprite(128, 256, 128, 43, new Vector4(22f, 10f, 22f, 10f));
            buttonNeutralSkin = MakeAtlasSprite(0, 299, 128, 43, new Vector4(22f, 10f, 22f, 10f));
            buttonPrimarySkin = MakeAtlasSprite(128, 299, 128, 43, new Vector4(22f, 10f, 22f, 10f));
            listRowSkin = MakeAtlasSprite(0, 342, 256, 64, new Vector4(26f, 12f, 26f, 12f));
            statusBarSkin = MakeAtlasSprite(0, 406, 256, 64, new Vector4(26f, 12f, 26f, 12f));
        }
        catch (Exception e)
        {
            Debug.LogWarning("X탑 대장간 UI 에셋 로드 실패: " + e.Message);
        }
    }

    Sprite MakeAtlasSprite(int x, int yFromTop, int width, int height, Vector4 border)
    {
        if (skinAtlas == null) return null;

        int y = skinAtlas.height - yFromTop - height;
        Rect rect = new Rect(x, y, width, height);
        return Sprite.Create(
            skinAtlas,
            rect,
            new Vector2(.5f, .5f),
            100f,
            0,
            SpriteMeshType.FullRect,
            border
        );
    }

    void ApplyPanelSkin(RectTransform rect, Sprite skin)
    {
        if (rect == null || skin == null) return;
        Image image = rect.GetComponent<Image>();
        if (image == null) return;

        image.sprite = skin;
        image.type = Image.Type.Sliced;
        image.color = Color.white;
    }

    void ApplyButtonSkin(Button button, Sprite skin)
    {
        if (button == null || skin == null) return;
        Image image = button.targetGraphic as Image;
        if (image == null) return;

        image.sprite = skin;
        image.type = Image.Type.Sliced;
        image.color = Color.white;
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
        t.fontSize = Mathf.RoundToInt(size * UiFontScale);
        t.alignment = align;
        t.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
        t.supportRichText = true;
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
