using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public sealed class XTapBlacksmith : MonoBehaviour
{
    // 11.40: runtime fallback copies for Android builds where dynamically-added
    // Resources .bytes files are present in source but fail to resolve after install.
    static readonly string EmbeddedBlacksmithAtlasJpegBase64 = "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";
    const float UiFontScale = 2.15f;
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
    RectTransform heldContent;
    RectTransform groundContent;
    Text heldCountText;
    Text groundCountText;
    Text heldPageText;
    Text groundPageText;
    Button heldPrevButton;
    Button heldNextButton;
    Button groundPrevButton;
    Button groundNextButton;
    int heldPage;
    int groundPage;

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
    Sprite statusBarSkin;

    Button enhanceTab;
    Button synthesisTab;
    Button dismantleTab;
    Button executeButton;

    GameObject probabilityOverlay;
    XTapForgeDuelView duelView;

    bool probabilityBusy;
    Coroutine probabilityRoutine;

    ForgeMode mode = ForgeMode.Enhance;
    string targetId;
    readonly List<string> materialIds = new List<string>();

    public void Initialize(RectTransform parent, Font uiFont, XTapInventory bag, Action closed)
    {
        host = parent;
        font = uiFont;
        inventory = bag;
        onClosed = closed;

        XTapUiSkin.EnsureLoaded();
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
        heldPage = 0;
        groundPage = 0;
        Refresh();
    }

    public void Close()
    {
        if (probabilityBusy) return;
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

        Text slotGuide = MakeText(panel, "선택하면 목록이 당겨집니다 · 위 슬롯을 누르면 선택 해제", 13, TextAnchor.MiddleCenter, false);
        slotGuide.color = new Color(.82f, .72f, .60f, 1f);
        Anchor(slotGuide.rectTransform, .05f, .815f, .95f, .850f);

        targetSlot = MakeSlotPanel(panel, "TargetSlot", .055f, .595f, .465f, .805f);
        materialSlot = MakeSlotPanel(panel, "MaterialSlot", .535f, .595f, .945f, .805f);

        Image targetSlotImage = targetSlot.GetComponent<Image>();
        targetSlotImage.raycastTarget = true;
        Button targetSlotButton = targetSlot.gameObject.AddComponent<Button>();
        targetSlotButton.targetGraphic = targetSlotImage;
        targetSlotButton.onClick.AddListener(ClearTargetSelection);

        Image materialSlotImage = materialSlot.GetComponent<Image>();
        materialSlotImage.raycastTarget = true;
        Button materialSlotButton = materialSlot.gameObject.AddComponent<Button>();
        materialSlotButton.targetGraphic = materialSlotImage;
        materialSlotButton.onClick.AddListener(ClearMaterialSelection);

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

        heldCountText = MakeText(panel, "", 17, TextAnchor.MiddleLeft, true);
        heldCountText.color = new Color(.98f, .91f, .78f, 1f);
        Anchor(heldCountText.rectTransform, .045f, .445f, .50f, .492f);

        heldPrevButton = MakeButton(panel, "◀", 18, new Color(.09f, .07f, .06f, 1f));
        ApplyButtonSkin(heldPrevButton, buttonNeutralSkin);
        Anchor(heldPrevButton.GetComponent<RectTransform>(), .635f, .445f, .735f, .492f);
        heldPrevButton.onClick.AddListener(delegate { ChangeStoragePage(XTapGearBlockData.LocationHeld, -1); });

        heldPageText = MakeText(panel, "", 17, TextAnchor.MiddleCenter, true);
        heldPageText.color = new Color(1f, .84f, .50f, 1f);
        Anchor(heldPageText.rectTransform, .740f, .445f, .855f, .492f);

        heldNextButton = MakeButton(panel, "▶", 18, new Color(.09f, .07f, .06f, 1f));
        ApplyButtonSkin(heldNextButton, buttonNeutralSkin);
        Anchor(heldNextButton.GetComponent<RectTransform>(), .860f, .445f, .960f, .492f);
        heldNextButton.onClick.AddListener(delegate { ChangeStoragePage(XTapGearBlockData.LocationHeld, 1); });

        RectTransform heldViewport;
        inventory.BuildSharedStorageZone(
            panel,
            "ForgeHeldZone",
            .035f, .335f, .965f, .445f,
            out heldViewport,
            out heldContent
        );

        groundCountText = MakeText(panel, "", 17, TextAnchor.MiddleLeft, true);
        groundCountText.color = new Color(.98f, .91f, .78f, 1f);
        Anchor(groundCountText.rectTransform, .045f, .290f, .50f, .337f);

        groundPrevButton = MakeButton(panel, "◀", 18, new Color(.09f, .07f, .06f, 1f));
        ApplyButtonSkin(groundPrevButton, buttonNeutralSkin);
        Anchor(groundPrevButton.GetComponent<RectTransform>(), .635f, .290f, .735f, .337f);
        groundPrevButton.onClick.AddListener(delegate { ChangeStoragePage(XTapGearBlockData.LocationGround, -1); });

        groundPageText = MakeText(panel, "", 17, TextAnchor.MiddleCenter, true);
        groundPageText.color = new Color(1f, .84f, .50f, 1f);
        Anchor(groundPageText.rectTransform, .740f, .290f, .855f, .337f);

        groundNextButton = MakeButton(panel, "▶", 18, new Color(.09f, .07f, .06f, 1f));
        ApplyButtonSkin(groundNextButton, buttonNeutralSkin);
        Anchor(groundNextButton.GetComponent<RectTransform>(), .860f, .290f, .960f, .337f);
        groundNextButton.onClick.AddListener(delegate { ChangeStoragePage(XTapGearBlockData.LocationGround, 1); });

        RectTransform groundViewport;
        inventory.BuildSharedStorageZone(
            panel,
            "ForgeGroundZone",
            .035f, .180f, .965f, .290f,
            out groundViewport,
            out groundContent
        );

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

        BuildProbabilityMachineUi();
        SetMode(ForgeMode.Enhance);
    }

    void BuildProbabilityMachineUi()
    {
        probabilityOverlay = new GameObject("ForgeDuelOverlay", typeof(RectTransform));
        probabilityOverlay.transform.SetParent(overlay.transform, false);
        duelView = probabilityOverlay.AddComponent<XTapForgeDuelView>();
        duelView.Initialize(font);
        probabilityOverlay.SetActive(false);
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
        if (probabilityBusy) return;
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
        RefreshSharedStorage();
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
            ruleText.text = "제물가치  -% 1 · +% 2 · 전용 3 · 수식어 5  |  안전 강화: 가치×10%";
        else if (mode == ForgeMode.Synthesis)
            ruleText.text = "대상 블록 + 제물 블록 1개  ·  기본 성공률 1%  ·  제물의 강화 포함 현재 공/방/체 전부 합산";
        else
            ruleText.text = "제물가치  -% 1 · +% 2 · 전용 3 · 수식어 5  |  분해: 가치×10%";
    }

    void RefreshSharedStorage()
    {
        int heldCount = inventory.GetStorageCount(XTapGearBlockData.LocationHeld, IsForgeListVisible);
        int groundCount = inventory.GetStorageCount(XTapGearBlockData.LocationGround, IsForgeListVisible);
        int heldPages = inventory.GetStoragePageCount(XTapGearBlockData.LocationHeld, IsForgeListVisible);
        int groundPages = inventory.GetStoragePageCount(XTapGearBlockData.LocationGround, IsForgeListVisible);

        heldPage = Mathf.Clamp(heldPage, 0, heldPages - 1);
        groundPage = Mathf.Clamp(groundPage, 0, groundPages - 1);

        if (heldCountText != null) heldCountText.text = "소지품    " + heldCount + "개";
        if (groundCountText != null) groundCountText.text = "바닥    " + groundCount + "개";
        if (heldPageText != null) heldPageText.text = heldCount == 0 ? "0 / 0" : (heldPage + 1) + " / " + heldPages;
        if (groundPageText != null) groundPageText.text = groundCount == 0 ? "0 / 0" : (groundPage + 1) + " / " + groundPages;

        if (heldPrevButton != null) heldPrevButton.interactable = heldPage > 0;
        if (heldNextButton != null) heldNextButton.interactable = heldCount > 0 && heldPage < heldPages - 1;
        if (groundPrevButton != null) groundPrevButton.interactable = groundPage > 0;
        if (groundNextButton != null) groundNextButton.interactable = groundCount > 0 && groundPage < groundPages - 1;

        inventory.RenderSharedStoragePage(
            XTapGearBlockData.LocationHeld,
            heldContent,
            heldPage,
            OnItemPressed,
            IsForgeSelected,
            IsForgeListVisible
        );
        inventory.RenderSharedStoragePage(
            XTapGearBlockData.LocationGround,
            groundContent,
            groundPage,
            OnItemPressed,
            IsForgeSelected,
            IsForgeListVisible
        );
    }

    bool IsForgeSelected(string id)
    {
        return targetId == id || materialIds.Contains(id);
    }

    bool IsForgeListVisible(string id)
    {
        return !IsForgeSelected(id);
    }

    void ClearTargetSelection()
    {
        if (probabilityBusy || string.IsNullOrEmpty(targetId)) return;
        targetId = null;
        Refresh();
    }

    void ClearMaterialSelection()
    {
        if (probabilityBusy || materialIds.Count == 0) return;
        materialIds.Clear();
        Refresh();
    }

    void ChangeStoragePage(int location, int delta)
    {
        if (location == XTapGearBlockData.LocationHeld)
            heldPage = Mathf.Max(0, heldPage + delta);
        else if (location == XTapGearBlockData.LocationGround)
            groundPage = Mathf.Max(0, groundPage + delta);

        RefreshSharedStorage();
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

    void ToggleMaterial(string id, int maxValue)
    {
        if (materialIds.Contains(id))
        {
            materialIds.Remove(id);
            return;
        }

        if (mode == ForgeMode.Synthesis)
        {
            if (materialIds.Count >= maxValue)
            {
                if (resultText != null)
                    resultText.text = "합성 제물은 1개만 선택할 수 있습니다.";
                return;
            }

            materialIds.Add(id);
            return;
        }

        XTapGearBlockData material = inventory.FindForgeItem(id);
        if (material == null) return;

        int currentValue = GetSelectedSacrificeValue();
        int addedValue = GetSacrificeValue(material);
        if (currentValue + addedValue > maxValue)
        {
            if (resultText != null)
                resultText.text =
                    "제물 가치는 최대 " + maxValue + "입니다.  현재 " +
                    currentValue + " + 선택 블럭 " + addedValue;
            return;
        }

        materialIds.Add(id);
    }

    int GetSacrificeValue(XTapGearBlockData item)
    {
        if (item == null) return 0;

        // Highest applicable tier wins so a rare block is never valued below
        // one of its lower traits.
        if (item.descriptorCount > 0) return 5;
        if (item.exclusive) return 3;
        if (item.correction > 0) return 2;

        // Negative-correction blocks are worth 1. A plain 0% non-exclusive
        // ticket block also falls back to 1 so every valid block can be used.
        return 1;
    }

    int GetSelectedSacrificeValue()
    {
        int total = 0;
        for (int i = 0; i < materialIds.Count; i++)
        {
            XTapGearBlockData item = inventory.FindForgeItem(materialIds[i]);
            total += GetSacrificeValue(item);
        }
        return total;
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
                : XTapGearNameColor.Rich(target) +
                  (target.enhanceLevel > 0 ? "  +" + target.enhanceLevel : "") +
                  "\n" + XTapStatFormat.BlockTriplet(target.attack, target.defense, target.hp, "   ");
        }

        double sacrificeAttack = 0d;
        double sacrificeDefense = 0d;
        double sacrificeHp = 0d;
        int sacrificeCount = 0;
        int sacrificeValue = 0;
        XTapGearBlockData singleSacrifice = null;

        for (int i = 0; i < materialIds.Count; i++)
        {
            XTapGearBlockData material = inventory.FindForgeItem(materialIds[i]);
            if (material == null) continue;

            sacrificeCount++;
            sacrificeValue += GetSacrificeValue(material);
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
                    XTapGearNameColor.Rich(singleSacrifice) +
                    (singleSacrifice.enhanceLevel > 0 ? "  +" + singleSacrifice.enhanceLevel : "") +
                    "\n제물 " + GetSacrificeValue(singleSacrifice) + "개분  ·  " +
                    XTapStatFormat.BlockTriplet(singleSacrifice.attack, singleSacrifice.defense, singleSacrifice.hp, "   ");
            }
            else
            {
                materialSlotText.text =
                    "블럭 " + sacrificeCount + "개  ·  제물가치 " + sacrificeValue + "/10" +
                    "\n합계  " + XTapStatFormat.BlockTriplet(sacrificeAttack, sacrificeDefense, sacrificeHp, "   ");
            }
        }

        if (mode == ForgeMode.Enhance)
        {
            bool destructive = target != null && target.enhanceLevel >= 10;
            int chance = destructive ? 10 : Mathf.Clamp(sacrificeValue * 10, 0, 100);
            selectionText.text = destructive
                ? "파괴 강화  +" + (target.enhanceLevel + 1) + "  ·  제물가치 " + sacrificeValue
                : "안전 강화  +" + (target != null ? target.enhanceLevel + 1 : 1) + "  ·  제물가치 " + sacrificeValue + "/10";
            chanceText.text = destructive
                ? "기본 10% · 실패 시 파괴"
                : "기본 " + chance + "% · 실패 시 유지";
            executeButton.interactable = target != null && sacrificeValue > 0 && target.enhanceLevel < 20;
        }
        else if (mode == ForgeMode.Synthesis)
        {
            selectionText.text = sacrificeCount == 1 ? "대상 + 제물 준비" : "대상 + 제물 1개";
            chanceText.text = "기본 성공률 1%";
            executeButton.interactable = target != null && sacrificeCount == 1;
        }
        else
        {
            int chance = Mathf.Clamp(sacrificeValue * 10, 0, 100);
            selectionText.text = "제물가치 " + sacrificeValue + "/10  ·  블럭 " + sacrificeCount + "개";
            chanceText.text = "기본 가방 +1칸  " + chance + "%";
            executeButton.interactable = sacrificeValue > 0;
        }
    }

    string ItemName(string id)
    {
        XTapGearBlockData item = inventory.FindForgeItem(id);
        return item == null ? "없음" : item.displayName;
    }

    void Execute()
    {
        if (inventory == null || probabilityBusy) return;

        int chance;
        if (!TryGetCurrentOperationChance(out chance))
            return;

        if (probabilityRoutine != null)
            StopCoroutine(probabilityRoutine);

        probabilityRoutine = StartCoroutine(RunProbabilityMachine(mode, chance));
    }

    bool TryGetCurrentOperationChance(out int chance)
    {
        chance = 0;
        int sacrificeValue = GetSelectedSacrificeValue();

        if (mode == ForgeMode.Enhance)
        {
            XTapGearBlockData target = inventory.FindForgeItem(targetId);
            if (target == null || materialIds.Count == 0)
            {
                resultText.text = "대상과 재료를 선택하세요.";
                return false;
            }

            if (target.enhanceLevel >= 20)
            {
                resultText.text = "이미 +20 최대 강화입니다.";
                return false;
            }

            chance = target.enhanceLevel >= 10
                ? 10
                : Mathf.Clamp(sacrificeValue * 10, 0, 100);
            return true;
        }

        if (mode == ForgeMode.Synthesis)
        {
            XTapGearBlockData target = inventory.FindForgeItem(targetId);
            if (target == null || materialIds.Count != 1)
            {
                resultText.text = "대상 1개와 재료 1개를 선택하세요.";
                return false;
            }

            XTapGearBlockData material = inventory.FindForgeItem(materialIds[0]);
            if (material == null)
            {
                resultText.text = "재료 블록을 찾을 수 없습니다.";
                ClearSelection();
                Refresh();
                return false;
            }

            chance = 1;
            return true;
        }

        if (materialIds.Count <= 0)
        {
            resultText.text = "분해할 재료를 선택하세요.";
            return false;
        }

        chance = Mathf.Clamp(sacrificeValue * 10, 0, 100);
        return true;
    }

    IEnumerator RunProbabilityMachine(ForgeMode operationMode, int chance)
    {
        probabilityBusy = true;
        if (executeButton != null) executeButton.interactable = false;
        probabilityOverlay.SetActive(true);
        probabilityOverlay.transform.SetAsLastSibling();

        string operationName = operationMode == ForgeMode.Enhance ? "강화" :
            (operationMode == ForgeMode.Synthesis ? "합성" : "분해");
        XTapGearBlockData target = operationMode == ForgeMode.Enhance
            ? inventory.FindForgeItem(targetId) : null;
        bool destructive = target != null && target.enhanceLevel >= 10;
        XTapGearBlockData displayedBlock = target;
        if (displayedBlock == null && materialIds.Count > 0)
            displayedBlock = inventory.FindForgeItem(materialIds[0]);

        // Independent 1% reversal, conditional on whichever finisher was selected.
        // Both rolls and the cosmetic line are fixed before any animation starts.
        XTapForgeOutcome outcome = XTapForgeOutcome.FromRolls(chance,
            UnityEngine.Random.Range(0, 100), UnityEngine.Random.Range(0, 100));
        string[] lines = outcome.AngelFinisher ? XTapForgeDialogue.AngelFinishers : XTapForgeDialogue.DemonFinishers;
        string finisherLine = lines[UnityEngine.Random.Range(0, lines.Length)];
        string reward = operationMode == ForgeMode.Dismantle ? "가방 배치칸 +" + outcome.RewardMultiplier :
            (operationMode == ForgeMode.Synthesis ? "제물 능력 흡수 ×" + outcome.RewardMultiplier :
            "강화 +" + (target == null ? 0 : outcome.EnhancedLevel(target.enhanceLevel)) + "  ·  최대 +20");
        bool applied = false;
        Action applyOutcome = delegate
        {
            if (applied) return;
            applied = true;
            if (operationMode == ForgeMode.Enhance) ResolveEnhance(outcome);
            else if (operationMode == ForgeMode.Synthesis) ResolveSynthesis(outcome);
            else ResolveDismantle(outcome);
            if (outcome.Reversal)
                resultText.text = (outcome.AngelFinisher ? "천사 실수! " : "악마 반전 · 보상 2배! ") + resultText.text;
            Debug.Log("X탑 FORGE_RESULT / mode=" + operationMode + " / baseChance=" + chance +
                " / angelFinisher=" + outcome.AngelFinisher + " / reversal=" + outcome.Reversal +
                " / success=" + outcome.Success + " / rewardMultiplier=" + outcome.RewardMultiplier);
        };
        try
        {
            yield return duelView.Play(operationName, chance, outcome, destructive,
                displayedBlock, GetSelectedSacrificeValue(), finisherLine, reward, applyOutcome);
        }
        finally
        {
            probabilityOverlay.SetActive(false);
            probabilityBusy = false;
            probabilityRoutine = null;
            Refresh();
        }
    }

    void ResolveEnhance(XTapForgeOutcome outcome)
    {
        XTapGearBlockData target = inventory.FindForgeItem(targetId);
        if (target == null)
        {
            resultText.text = "강화 대상을 찾을 수 없습니다.";
            ClearSelection();
            return;
        }

        int materialCount = materialIds.Count;
        bool destructive = target.enhanceLevel >= 10;
        inventory.EnsureEnhancementBaseStats(target);
        ConsumeMaterials();

        if (outcome.Success)
        {
            target.enhanceLevel = outcome.EnhancedLevel(target.enhanceLevel);
            inventory.RecalculateEnhancedStats(target);
            inventory.CommitForgeChanges();
            resultText.text =
                "강화 성공!  +" + target.enhanceLevel + "   " +
                XTapStatFormat.BlockTriplet(target.attack, target.defense, target.hp, " / ");
        }
        else if (destructive)
        {
            string destroyedName = XTapGearNameColor.Rich(target);
            inventory.RemoveForgeItem(target.id);
            inventory.CommitForgeChanges();
            resultText.text =
                "강화 실패. " + destroyedName + "이(가) 파괴되었습니다.  제물 " +
                materialCount + "개 소모";
        }
        else
        {
            inventory.CommitForgeChanges();
            resultText.text =
                "강화 실패. 대상은 유지됩니다.  제물 " + materialCount + "개 소모";
        }

        ClearSelection();
    }

    void ResolveSynthesis(XTapForgeOutcome outcome)
    {
        XTapGearBlockData target = inventory.FindForgeItem(targetId);
        XTapGearBlockData material = materialIds.Count == 1
            ? inventory.FindForgeItem(materialIds[0])
            : null;

        if (target == null || material == null)
        {
            resultText.text = "합성 대상 또는 재료를 찾을 수 없습니다.";
            ClearSelection();
            return;
        }

        double addAttack;
        double addDefense;
        double addHp;
        inventory.GetPreDescriptorStats(material, out addAttack, out addDefense, out addHp);
        string consumedName = XTapGearNameColor.Rich(material);

        inventory.RemoveForgeItem(material.id);

        if (outcome.Success)
        {
            if (outcome.RewardMultiplier == 2)
            {
                addAttack = XTapStatFormat.SafeAdd(addAttack, addAttack);
                addDefense = XTapStatFormat.SafeAdd(addDefense, addDefense);
                addHp = XTapStatFormat.SafeAdd(addHp, addHp);
            }
            inventory.MergeSynthesisStats(target, addAttack, addDefense, addHp);
            inventory.CommitForgeChanges();

            resultText.text =
                "합성 성공!  " + consumedName + " 능력을 흡수했습니다.  합성 후 " +
                XTapStatFormat.BlockTriplet(target.attack, target.defense, target.hp, " / ");
        }
        else
        {
            inventory.CommitForgeChanges();
            resultText.text = "합성 실패. " + consumedName + "이(가) 소모됐습니다.";
        }

        ClearSelection();
    }

    void ResolveDismantle(XTapForgeOutcome outcome)
    {
        int count = materialIds.Count;
        ConsumeMaterials();

        if (outcome.Success)
        {
            inventory.AddGridCellExpansion(outcome.RewardMultiplier);
            resultText.text =
                "분해 성공! 가방 배치칸 +" + outcome.RewardMultiplier + ".  현재 " + inventory.GridCapacity + "칸";
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
            byte[] bytes = null;
            TextAsset encoded = Resources.Load<TextAsset>("XTapBlacksmithUI/atlas");

            if (encoded != null && !string.IsNullOrWhiteSpace(encoded.text))
            {
                try
                {
                    bytes = Convert.FromBase64String(encoded.text.Trim());
                }
                catch
                {
                    bytes = null;
                }
            }

            // 11.40: guaranteed runtime fallback for the existing blacksmith atlas.
            if (bytes == null || bytes.Length < 1024)
            {
                bytes = Convert.FromBase64String(EmbeddedBlacksmithAtlasJpegBase64);
                Debug.LogWarning("X탑 대장간 atlas Resources 로드 실패. 내장 이미지 fallback 사용.");
            }

            skinAtlas = new Texture2D(2, 2, TextureFormat.RGB24, false);
            if (!skinAtlas.LoadImage(bytes, false))
            {
                Destroy(skinAtlas);
                skinAtlas = null;
                Debug.LogError("X탑 대장간 atlas 이미지 최종 로드 실패.");
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
            statusBarSkin = MakeAtlasSprite(0, 406, 256, 64, new Vector4(26f, 12f, 26f, 12f));
        }
        catch (Exception e)
        {
            Debug.LogError("X탑 대장간 UI 에셋 로드 실패: " + e.Message);
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

        RectTransform accent = MakePanel(go.transform, "TypeAccent", new Color(.72f, .43f, .18f, .95f));
        Anchor(accent, .18f, .90f, .82f, .925f);
        accent.GetComponent<Image>().raycastTarget = false;

        Text t = MakeText(go.transform, label, fontSize + 1, TextAnchor.MiddleCenter, true);
        t.color = new Color(1f, .91f, .78f, 1f);
        Outline outline = t.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, .90f);
        outline.effectDistance = new Vector2(2f, -2f);
        Anchor(t.rectTransform, .04f, .04f, .96f, .90f);

        Button b = go.GetComponent<Button>();
        b.targetGraphic = bg;

        ColorBlock cb = b.colors;
        cb.normalColor = Color.white;
        cb.highlightedColor = new Color(1f, .92f, .78f, 1f);
        cb.pressedColor = new Color(.62f, .43f, .30f, 1f);
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
