using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public sealed class XTapBlacksmith : MonoBehaviour
{
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
    RectTransform probabilityMachine;
    RectTransform probabilityWheel;
    Image probabilityWheelCore;
    Text probabilityTitleText;
    Text probabilityChanceText;
    Text probabilityRollText;
    Text probabilityPhaseText;
    Text probabilityResultText;
    Image probabilityChargeFill;
    readonly Image[] probabilityLamps = new Image[3];
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
        probabilityOverlay = new GameObject("ForgeProbabilityOverlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        probabilityOverlay.transform.SetParent(overlay.transform, false);

        Image dim = probabilityOverlay.GetComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, .82f);
        dim.raycastTarget = true;
        Anchor(dim.rectTransform, 0f, 0f, 1f, 1f);

        // Reuse the BLOCK reward machine composition. Forge only changes the
        // numeric ranges and operation labels.
        probabilityMachine = new GameObject(
            "ForgeBlockMachine",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image)
        ).GetComponent<RectTransform>();
        probabilityMachine.SetParent(probabilityOverlay.transform, false);

        Image body = probabilityMachine.GetComponent<Image>();
        body.color = new Color(.075f, .085f, .11f, 1f);
        body.raycastTarget = true;

        probabilityMachine.anchorMin = probabilityMachine.anchorMax = new Vector2(.5f, .5f);
        probabilityMachine.pivot = new Vector2(.5f, .5f);
        probabilityMachine.sizeDelta = new Vector2(900f, 1600f);
        probabilityMachine.anchoredPosition = Vector2.zero;
        Frame(probabilityMachine, new Color(.84f, .60f, .13f, 1f), 18f);

        probabilityTitleText = MakeText(probabilityMachine, "X-TOWER  FORGE", 28, TextAnchor.MiddleCenter, true);
        probabilityTitleText.color = new Color(1f, .84f, .39f, 1f);
        Anchor(probabilityTitleText.rectTransform, .06f, .905f, .94f, .985f);

        probabilityChanceText = MakeText(probabilityMachine, "", 20, TextAnchor.MiddleCenter, true);
        probabilityChanceText.color = new Color(.96f, .76f, .25f, 1f);
        Anchor(probabilityChanceText.rectTransform, .12f, .830f, .88f, .905f);

        RectTransform window = MakePanel(probabilityMachine, "WheelWindow", new Color(.025f, .03f, .045f, 1f));
        Anchor(window, .17f, .455f, .83f, .820f);
        Frame(window, new Color(.42f, .44f, .50f, 1f), 8f);

        probabilityWheel = new GameObject("Wheel", typeof(RectTransform)).GetComponent<RectTransform>();
        probabilityWheel.SetParent(window, false);
        probabilityWheel.anchorMin = probabilityWheel.anchorMax = new Vector2(.5f, .5f);
        probabilityWheel.sizeDelta = new Vector2(430f, 430f);
        probabilityWheel.anchoredPosition = Vector2.zero;

        // The real calculation still rolls one hidden value from 00 to 99.
        // The player only sees the two possible outcomes: SUCCESS or FAIL.
        string[] visibleOutcomes = { "성공", "실패" };
        Color[] outcomeColors =
        {
            new Color(.12f, .42f, .22f, 1f),
            new Color(.48f, .10f, .08f, 1f)
        };

        for (int i = 0; i < visibleOutcomes.Length; i++)
        {
            float a = i * 180f * Mathf.Deg2Rad;

            GameObject slot = new GameObject(
                i == 0 ? "SuccessSlot" : "FailSlot",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image)
            );
            slot.transform.SetParent(probabilityWheel, false);

            Image si = slot.GetComponent<Image>();
            si.color = outcomeColors[i];
            si.raycastTarget = false;

            RectTransform sr = si.rectTransform;
            sr.anchorMin = sr.anchorMax = new Vector2(.5f, .5f);
            sr.sizeDelta = new Vector2(180f, 74f);
            sr.anchoredPosition = new Vector2(Mathf.Sin(a), Mathf.Cos(a)) * 176f;

            Text lt = MakeText(slot.transform, visibleOutcomes[i], 18, TextAnchor.MiddleCenter, true);
            lt.color = new Color(1f, .95f, .84f, 1f);
            Anchor(lt.rectTransform, 0f, 0f, 1f, 1f);
        }

        GameObject coreGo = new GameObject("WheelCore", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        coreGo.transform.SetParent(probabilityWheel, false);
        probabilityWheelCore = coreGo.GetComponent<Image>();
        probabilityWheelCore.color = new Color(.13f, .15f, .20f, 1f);
        probabilityWheelCore.raycastTarget = false;

        RectTransform cr = probabilityWheelCore.rectTransform;
        cr.anchorMin = cr.anchorMax = new Vector2(.5f, .5f);
        cr.sizeDelta = new Vector2(200f, 200f);

        probabilityRollText = MakeText(coreGo.transform, "판정", 30, TextAnchor.MiddleCenter, true);
        probabilityRollText.color = new Color(1f, .78f, .20f, 1f);
        Anchor(probabilityRollText.rectTransform, 0f, 0f, 1f, 1f);

        RectTransform arrow = MakePanel(window, "Pointer", new Color(1f, .78f, .18f, 1f));
        arrow.anchorMin = arrow.anchorMax = new Vector2(.5f, 1f);
        arrow.pivot = new Vector2(.5f, 1f);
        arrow.sizeDelta = new Vector2(42f, 72f);
        arrow.anchoredPosition = new Vector2(0f, -7f);

        RectTransform resultChute = MakePanel(probabilityMachine, "ResultChute", new Color(.025f, .03f, .04f, 1f));
        Anchor(resultChute, .25f, .285f, .75f, .420f);
        Frame(resultChute, new Color(.50f, .52f, .58f, 1f), 7f);

        probabilityPhaseText = MakeText(resultChute, "", 18, TextAnchor.MiddleCenter, true);
        probabilityPhaseText.color = new Color(.90f, .82f, .62f, 1f);
        Anchor(probabilityPhaseText.rectTransform, .04f, .54f, .96f, .94f);

        probabilityResultText = MakeText(resultChute, "", 26, TextAnchor.MiddleCenter, true);
        probabilityResultText.color = Color.white;
        Anchor(probabilityResultText.rectTransform, .04f, .06f, .96f, .56f);

        Text guide = MakeText(probabilityMachine, "최종 결과는 성공 또는 실패", 15, TextAnchor.MiddleCenter, false);
        guide.color = new Color(.72f, .74f, .80f, 1f);
        Anchor(guide.rectTransform, .05f, .105f, .95f, .185f);

        Text touchGuide = MakeText(probabilityMachine, "확률 계산은 내부에서 정확히 판정됩니다", 13, TextAnchor.MiddleCenter, false);
        touchGuide.color = new Color(.58f, .60f, .66f, 1f);
        Anchor(touchGuide.rectTransform, .05f, .025f, .95f, .095f);

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
            ruleText.text = "+1~+10 안전 강화 · 블록 고유 공/방/체 단계당 +10%  ·  +11~+20 성공률 10% / 실패 시 대상 파괴";
        else if (mode == ForgeMode.Synthesis)
            ruleText.text = "대상 블록 + 제물 블록  ·  성공률 1%  ·  제물의 강화 포함 현재 공/방/체 전부 합산";
        else
            ruleText.text = "제물 최대 10개  ·  1개당 10%  ·  성공 시 플레이어 가방 +1칸";
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
                : XTapGearNameColor.Rich(target) +
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
                    XTapGearNameColor.Rich(singleSacrifice) +
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
            bool destructive = target != null && target.enhanceLevel >= 10;
            int chance = destructive ? 10 : Mathf.Clamp(sacrificeCount * 10, 0, 100);
            selectionText.text = destructive
                ? "파괴 강화  +" + (target.enhanceLevel + 1) + "  ·  제물 " + sacrificeCount + "개"
                : "안전 강화  +" + (target != null ? target.enhanceLevel + 1 : 1) + "  ·  제물 " + sacrificeCount + "/10";
            chanceText.text = destructive
                ? "성공률 10%  ·  실패 시 대상 파괴"
                : "성공률 " + chance + "%  ·  실패 시 대상 유지";
            executeButton.interactable = target != null && sacrificeCount > 0 && target.enhanceLevel < 20;
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
                : Mathf.Clamp(materialIds.Count * 10, 0, 100);
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

        chance = Mathf.Clamp(materialIds.Count * 10, 0, 100);
        return true;
    }

    IEnumerator RunProbabilityMachine(ForgeMode operationMode, int chance)
    {
        probabilityBusy = true;
        if (executeButton != null) executeButton.interactable = false;

        probabilityOverlay.SetActive(true);
        probabilityOverlay.transform.SetAsLastSibling();

        string operationName = operationMode == ForgeMode.Enhance
            ? "강화"
            : (operationMode == ForgeMode.Synthesis ? "합성" : "분해");

        XTapGearBlockData enhanceTarget = operationMode == ForgeMode.Enhance
            ? inventory.FindForgeItem(targetId)
            : null;
        bool destructiveEnhance = enhanceTarget != null && enhanceTarget.enhanceLevel >= 10;

        probabilityTitleText.text = "X-TOWER  " + operationName.ToUpper();
        probabilityChanceText.text = "성공률 " + chance + "%  ·  성공 / 실패";
        probabilityPhaseText.text = "룰렛 회전 중";
        probabilityResultText.text = "";
        probabilityRollText.text = "판정";
        probabilityRollText.color = new Color(1f, .78f, .20f, 1f);

        // Pick the exact hidden 00-99 result once. The player only sees success or failure.
        int finalRoll = UnityEngine.Random.Range(0, 100);
        bool success = finalRoll < chance;

        probabilityMachine.localScale = Vector3.one * .90f;
        probabilityMachine.anchoredPosition = new Vector2(0f, -40f);

        float intro = 0f;
        while (intro < .22f)
        {
            intro += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(intro / .22f);
            float e = 1f - Mathf.Pow(1f - p, 3f);
            probabilityMachine.localScale = Vector3.one * Mathf.Lerp(.90f, 1f, e);
            probabilityMachine.anchoredPosition = new Vector2(0f, Mathf.Lerp(-40f, 0f, e));
            yield return null;
        }

        // The hidden 00-99 roll decides the outcome. The visible wheel only
        // lands on one of two result plates.
        float startAngle = NormalizeSignedAngle(probabilityWheel.localEulerAngles.z);
        float targetAngle = success ? 0f : 180f;
        float clockwiseDelta = Mathf.Repeat(startAngle - targetAngle, 360f);
        float totalSpin = 360f * UnityEngine.Random.Range(4, 7) + clockwiseDelta;

        float duration = 1.85f;
        float t = 0f;

        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / duration);
            float e = 1f - Mathf.Pow(1f - p, 4f);
            float angle = startAngle - totalSpin * e;
            probabilityWheel.localRotation = Quaternion.Euler(0f, 0f, angle);

            float shake = Mathf.Sin(Time.unscaledTime * 72f) * (1f - p) * 5f;
            probabilityMachine.anchoredPosition = new Vector2(shake, 0f);

            if (probabilityWheelCore != null)
            {
                float pulse = 1f + Mathf.Sin(Time.unscaledTime * 17f) * .05f;
                probabilityWheelCore.rectTransform.localScale = Vector3.one * pulse;
            }

            yield return null;
        }

        // Same snap behavior as the block reward machine.
        probabilityWheel.localRotation = Quaternion.Euler(0f, 0f, targetAngle);
        probabilityMachine.anchoredPosition = Vector2.zero;
        if (probabilityWheelCore != null)
            probabilityWheelCore.rectTransform.localScale = Vector3.one;

        // Never expose the hidden 00-99 roll. Only reveal the binary outcome.
        probabilityRollText.text = success ? "성공" : "실패";
        probabilityRollText.color = success
            ? new Color(.62f, 1f, .52f, 1f)
            : new Color(1f, .34f, .28f, 1f);
        probabilityRollText.transform.localScale = Vector3.one * 1.24f;

        probabilityPhaseText.text = destructiveEnhance
            ? "실패 시 대상 파괴"
            : operationName + " 판정";
        probabilityPhaseText.color = destructiveEnhance
            ? new Color(1f, .34f, .26f, 1f)
            : new Color(.90f, .82f, .62f, 1f);

        probabilityResultText.text = success ? "성공" : "실패";
        probabilityResultText.color = success
            ? new Color(.58f, 1f, .58f, 1f)
            : new Color(1f, .42f, .38f, 1f);

        VibrateForgeResult();

        yield return new WaitForSecondsRealtime(.16f);
        probabilityRollText.transform.localScale = Vector3.one;
        yield return new WaitForSecondsRealtime(.72f);

        if (operationMode == ForgeMode.Enhance)
            ResolveEnhance(success);
        else if (operationMode == ForgeMode.Synthesis)
            ResolveSynthesis(success);
        else
            ResolveDismantle(success);

        probabilityOverlay.SetActive(false);
        probabilityBusy = false;
        probabilityRoutine = null;
        Refresh();
    }

    static float NormalizeSignedAngle(float angle)
    {
        return Mathf.Repeat(angle + 180f, 360f) - 180f;
    }

    void VibrateForgeResult()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (PlayerPrefs.GetInt("xtap_option_vibration", 1) == 1)
            Handheld.Vibrate();
#endif
    }

    void ResolveEnhance(bool success)
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

        if (success)
        {
            target.enhanceLevel++;
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

    void ResolveSynthesis(bool success)
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

        if (success)
        {
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

    void ResolveDismantle(bool success)
    {
        int count = materialIds.Count;
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
