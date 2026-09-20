using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class XTapBattleController : MonoBehaviour
{
    private const int PlayerMaxHp = 100;
    private const int PlayerAtk = 5;
    private const int PlayerDef = 1;
    private const int EnemyMaxHp = 100;
    private const int EnemyAtk = 3;
    private const int EnemyDef = 1;
    private const float EnemyDodgeChance = 0.20f;

    private int playerHp = PlayerMaxHp;
    private int enemyHp = EnemyMaxHp;
    private int prisonCount;
    private bool busy;
    private bool over;
    private bool autoMode;
    private bool guardEvade;

    private RectTransform stage;
    private RectTransform bossBody;
    private Image flash;
    private Image enemyHpFill;
    private Image playerHpFill;
    private Text status;
    private Text enemyText;
    private Text playerText;
    private Text prisonText;
    private Button attackButton;
    private Button evadeButton;
    private Button autoButton;
    private Button resetButton;
    private Button captureButton;
    private Font font;

    private void Start()
    {
        Application.targetFrameRate = 60;
        Screen.orientation = ScreenOrientation.Portrait;
        BuildUi();
        Refresh();
    }

    private void BuildUi()
    {
        if (FindObjectOfType<EventSystem>() == null)
        {
            var es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            DontDestroyOnLoad(es);
        }

        font = Font.CreateDynamicFontFromOSFont(new[] { "Noto Sans CJK KR", "Noto Sans KR", "sans-serif" }, 32);
        if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");

        var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        var bg = Img("Background", canvas.transform, new Color(0.018f, 0.02f, 0.03f, 1f));
        Rect(bg.rectTransform, 0, 0, 1, 1);

        var top = Img("TopGlow", canvas.transform, new Color(0.33f, 0.03f, 0.12f, 0.35f));
        Rect(top.rectTransform, 0, 0.91f, 1, 1);

        var title = Txt("Title", canvas.transform, "X탑  1-1", 56, TextAnchor.MiddleLeft, FontStyle.Bold);
        Rect(title.rectTransform, 0.055f, 0.935f, 0.60f, 0.99f);

        prisonText = Txt("Prison", canvas.transform, "감옥 0", 28, TextAnchor.MiddleRight, FontStyle.Bold);
        Rect(prisonText.rectTransform, 0.63f, 0.94f, 0.945f, 0.985f);

        enemyText = Txt("Enemy", canvas.transform, "", 27, TextAnchor.MiddleLeft, FontStyle.Bold);
        Rect(enemyText.rectTransform, 0.055f, 0.875f, 0.945f, 0.915f);
        enemyHpFill = Bar("EnemyHP", canvas.transform, 0.055f, 0.842f, 0.945f, 0.875f, new Color(0.92f, 0.11f, 0.26f, 1f));

        var stageGo = new GameObject("Stage", typeof(Image), typeof(RectMask2D));
        stageGo.transform.SetParent(canvas.transform, false);
        stage = stageGo.GetComponent<RectTransform>();
        Rect(stage, 0.055f, 0.275f, 0.945f, 0.825f);
        stageGo.GetComponent<Image>().color = new Color(0.035f, 0.04f, 0.055f, 1f);

        var horizon = Img("Horizon", stage, new Color(0.12f, 0.02f, 0.08f, 1f));
        Rect(horizon.rectTransform, 0, 0.65f, 1, 1);

        var towerBack = Img("TowerBack", stage, new Color(0.07f, 0.075f, 0.095f, 1f));
        Rect(towerBack.rectTransform, 0.03f, 0.03f, 0.23f, 0.92f);
        var towerBack2 = Img("TowerBack2", stage, new Color(0.055f, 0.06f, 0.08f, 1f));
        Rect(towerBack2.rectTransform, 0.77f, 0.03f, 0.97f, 0.78f);

        var boss = Img("BossBody", stage, new Color(0.10f, 0.11f, 0.14f, 1f));
        bossBody = boss.rectTransform;
        Rect(bossBody, 0.31f, 0.17f, 0.69f, 0.78f);

        var bossHead = Img("BossHead", bossBody, new Color(0.14f, 0.15f, 0.19f, 1f));
        Rect(bossHead.rectTransform, 0.24f, 0.76f, 0.76f, 1.05f);

        var eye1 = Img("EyeL", bossHead.transform, new Color(1f, 0.08f, 0.25f, 1f));
        Rect(eye1.rectTransform, 0.21f, 0.42f, 0.39f, 0.52f);
        var eye2 = Img("EyeR", bossHead.transform, new Color(1f, 0.08f, 0.25f, 1f));
        Rect(eye2.rectTransform, 0.61f, 0.42f, 0.79f, 0.52f);

        var bossLabel = Txt("BossLabel", stage, "1층 보스", 44, TextAnchor.MiddleCenter, FontStyle.Bold);
        Rect(bossLabel.rectTransform, 0.20f, 0.05f, 0.80f, 0.14f);

        flash = Img("Flash", stage, new Color(1, 1, 1, 0));
        Rect(flash.rectTransform, 0, 0, 1, 1);
        flash.raycastTarget = false;

        status = Txt("Status", canvas.transform, "전투 준비", 38, TextAnchor.MiddleCenter, FontStyle.Bold);
        Rect(status.rectTransform, 0.06f, 0.222f, 0.94f, 0.27f);

        playerText = Txt("Player", canvas.transform, "", 27, TextAnchor.MiddleLeft, FontStyle.Bold);
        Rect(playerText.rectTransform, 0.055f, 0.183f, 0.945f, 0.218f);
        playerHpFill = Bar("PlayerHP", canvas.transform, 0.055f, 0.15f, 0.945f, 0.182f, new Color(0.15f, 0.78f, 0.50f, 1f));

        attackButton = Btn("Attack", canvas.transform, "공격", new Color(0.82f, 0.08f, 0.21f, 1f));
        Rect(attackButton.GetComponent<RectTransform>(), 0.055f, 0.055f, 0.355f, 0.13f);
        attackButton.onClick.AddListener(delegate { if (!busy && !over) StartCoroutine(PlayerAttack()); });

        evadeButton = Btn("Evade", canvas.transform, "회피", new Color(0.12f, 0.38f, 0.76f, 1f));
        Rect(evadeButton.GetComponent<RectTransform>(), 0.37f, 0.055f, 0.65f, 0.13f);
        evadeButton.onClick.AddListener(delegate { if (!busy && !over) StartCoroutine(PlayerEvade()); });

        autoButton = Btn("Auto", canvas.transform, "자동", new Color(0.25f, 0.27f, 0.33f, 1f));
        Rect(autoButton.GetComponent<RectTransform>(), 0.665f, 0.055f, 0.795f, 0.13f);
        autoButton.onClick.AddListener(ToggleAuto);

        resetButton = Btn("Reset", canvas.transform, "재시작", new Color(0.20f, 0.20f, 0.24f, 1f));
        Rect(resetButton.GetComponent<RectTransform>(), 0.81f, 0.055f, 0.945f, 0.13f);
        resetButton.onClick.AddListener(ResetBattle);

        captureButton = Btn("Capture", canvas.transform, "포획", new Color(0.55f, 0.12f, 0.72f, 1f));
        Rect(captureButton.GetComponent<RectTransform>(), 0.30f, 0.055f, 0.70f, 0.13f);
        captureButton.gameObject.SetActive(false);
        captureButton.onClick.AddListener(Capture);
    }

    private IEnumerator PlayerAttack()
    {
        busy = true;
        Buttons(false);
        status.text = "공격!";
        yield return PunchStage(26f, 0.12f);

        if (Random.value < EnemyDodgeChance)
        {
            status.text = "보스가 회피했다";
            yield return DodgeBoss();
        }
        else
        {
            int damage = Mathf.Max(1, (PlayerAtk - EnemyDef) * 4 + Random.Range(-2, 3));
            enemyHp = Mathf.Max(0, enemyHp - damage);
            status.text = damage + " 데미지";
            Refresh();
            yield return HitFlash();

            if (enemyHp <= 0)
            {
                yield return Victory();
                yield break;
            }
        }

        yield return new WaitForSeconds(0.18f);
        yield return EnemyTurn();
        busy = false;
        Buttons(true);
    }

    private IEnumerator PlayerEvade()
    {
        busy = true;
        Buttons(false);
        guardEvade = true;
        status.text = "회피 준비";
        yield return MoveStage(34f, 0.14f);
        yield return EnemyTurn();
        guardEvade = false;
        busy = false;
        Buttons(true);
    }

    private IEnumerator EnemyTurn()
    {
        status.text = "보스의 공격";
        yield return BossLunge();

        if (guardEvade || Random.value < 0.28f)
        {
            status.text = "회피 성공";
            yield return MoveStage(-48f, 0.16f);
            yield break;
        }

        int damage = Mathf.Max(1, (EnemyAtk - PlayerDef) * 3 + Random.Range(0, 3));
        playerHp = Mathf.Max(0, playerHp - damage);
        status.text = damage + " 피해";
        Refresh();
        yield return RedFlash();

        if (playerHp <= 0)
        {
            over = true;
            autoMode = false;
            status.text = "패배";
            Buttons(false);
            resetButton.interactable = true;
        }
    }

    private IEnumerator Victory()
    {
        over = true;
        autoMode = false;
        Buttons(false);
        status.text = "승리!";
        for (int i = 0; i < 3; i++)
        {
            yield return HitFlash();
            yield return new WaitForSeconds(0.08f);
        }
        captureButton.gameObject.SetActive(true);
        captureButton.interactable = true;
        resetButton.interactable = true;
    }

    private void Capture()
    {
        if (!over || enemyHp > 0) return;
        prisonCount++;
        prisonText.text = "감옥 " + prisonCount;
        status.text = "1층 보스 포획 완료";
        captureButton.interactable = false;
    }

    private void ToggleAuto()
    {
        if (over) return;
        autoMode = !autoMode;
        autoButton.GetComponentInChildren<Text>().text = autoMode ? "자동 ON" : "자동";
        if (autoMode) StartCoroutine(AutoLoop());
    }

    private IEnumerator AutoLoop()
    {
        while (autoMode && !over)
        {
            if (!busy) yield return PlayerAttack();
            yield return new WaitForSeconds(0.25f);
        }
    }

    private void ResetBattle()
    {
        StopAllCoroutines();
        playerHp = PlayerMaxHp;
        enemyHp = EnemyMaxHp;
        over = false;
        busy = false;
        autoMode = false;
        guardEvade = false;
        captureButton.gameObject.SetActive(false);
        autoButton.GetComponentInChildren<Text>().text = "자동";
        status.text = "1-1 전투 준비";
        Buttons(true);
        Refresh();
    }

    private IEnumerator HitFlash()
    {
        flash.color = new Color(1f, 0.86f, 0.72f, 0.75f);
        yield return PunchStage(34f, 0.08f);
        flash.color = new Color(1, 1, 1, 0);
    }

    private IEnumerator RedFlash()
    {
        flash.color = new Color(1f, 0.05f, 0.08f, 0.42f);
        yield return PunchStage(24f, 0.10f);
        flash.color = new Color(1, 1, 1, 0);
    }

    private IEnumerator BossLunge()
    {
        Vector2 start = bossBody.anchoredPosition;
        bossBody.anchoredPosition = start + new Vector2(0, -20);
        Vector3 baseScale = bossBody.localScale;
        bossBody.localScale = baseScale * 1.07f;
        yield return new WaitForSeconds(0.10f);
        bossBody.anchoredPosition = start;
        bossBody.localScale = baseScale;
    }

    private IEnumerator DodgeBoss()
    {
        Vector2 start = bossBody.anchoredPosition;
        bossBody.anchoredPosition = start + new Vector2(95, 0);
        bossBody.localRotation = Quaternion.Euler(0, 0, -4);
        yield return new WaitForSeconds(0.13f);
        bossBody.anchoredPosition = start;
        bossBody.localRotation = Quaternion.identity;
    }

    private IEnumerator PunchStage(float amount, float seconds)
    {
        Vector2 start = stage.anchoredPosition;
        int frames = 5;
        for (int i = 0; i < frames; i++)
        {
            stage.anchoredPosition = start + Random.insideUnitCircle * amount;
            yield return new WaitForSeconds(seconds / frames);
        }
        stage.anchoredPosition = start;
    }

    private IEnumerator MoveStage(float x, float seconds)
    {
        Vector2 start = stage.anchoredPosition;
        stage.anchoredPosition = start + new Vector2(x, 0);
        yield return new WaitForSeconds(seconds);
        stage.anchoredPosition = start;
    }

    private void Refresh()
    {
        enemyText.text = "1층 보스   HP " + enemyHp + " / ATK 3 / DEF 1";
        playerText.text = "조훈   HP " + playerHp + " / ATK 5 / DEF 1";
        enemyHpFill.fillAmount = enemyHp / (float)EnemyMaxHp;
        playerHpFill.fillAmount = playerHp / (float)PlayerMaxHp;
        prisonText.text = "감옥 " + prisonCount;
    }

    private void Buttons(bool enabled)
    {
        attackButton.interactable = enabled;
        evadeButton.interactable = enabled;
        autoButton.interactable = enabled && !over;
        resetButton.interactable = true;
    }

    private Image Bar(string name, Transform parent, float x1, float y1, float x2, float y2, Color color)
    {
        var back = Img(name + "Back", parent, new Color(0.12f, 0.13f, 0.16f, 1f));
        Rect(back.rectTransform, x1, y1, x2, y2);
        var fill = Img(name + "Fill", back.transform, color);
        Rect(fill.rectTransform, 0, 0, 1, 1);
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillOrigin = 0;
        return fill;
    }

    private Button Btn(string name, Transform parent, string label, Color color)
    {
        var go = new GameObject(name, typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color = color;
        var button = go.GetComponent<Button>();
        var text = Txt("Text", go.transform, label, 34, TextAnchor.MiddleCenter, FontStyle.Bold);
        Rect(text.rectTransform, 0, 0, 1, 1);
        return button;
    }

    private Image Img(string name, Transform parent, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        var image = go.GetComponent<Image>();
        image.color = color;
        return image;
    }

    private Text Txt(string name, Transform parent, string value, int size, TextAnchor anchor, FontStyle style)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<Text>();
        t.text = value;
        t.font = font;
        t.fontSize = size;
        t.fontStyle = style;
        t.alignment = anchor;
        t.color = Color.white;
        t.resizeTextForBestFit = false;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Truncate;
        return t;
    }

    private static void Rect(RectTransform rt, float x1, float y1, float x2, float y2)
    {
        rt.anchorMin = new Vector2(x1, y1);
        rt.anchorMax = new Vector2(x2, y2);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
