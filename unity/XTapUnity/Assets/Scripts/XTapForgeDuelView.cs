using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Presentation only: the inventory controller supplies the single decided outcome.
public sealed class XTapForgeDuelView : MonoBehaviour
{
    const float Width = 1080f;
    const float ActorSize = 600f;
    readonly List<Texture2D> textures = new List<Texture2D>();
    readonly List<Sprite> sprites = new List<Sprite>();
    RectTransform root, content, stage, anvil, workpiece, resultPlate;
    Image angel, demon, flash;
    Sprite[] angelPoses, demonPoses;
    Text heading, chance, details, phase, result, hint;
    XTapForgeImpactGraphic impact;
    Vector2 stageHome, lastViewport;
    Vector2 contactPoint = new Vector2(0f, 55f);
    Rect lastSafeArea;
    Font font;

    public void Initialize(Font uiFont)
    {
        font = uiFont;
        root = (RectTransform)transform;
        Stretch(root);
        Image shield = gameObject.AddComponent<Image>();
        shield.color = Color.black;
        shield.raycastTarget = true;

        Sprite background = LoadSprite("forge_duel_background");
        Image backdrop = Picture(root, "FullBleedForge", background);
        AspectRatioFitter fitter = backdrop.gameObject.AddComponent<AspectRatioFitter>();
        fitter.aspectRatio = background.rect.width / background.rect.height;
        fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        RectTransform shade = MakeRect(root, "ReadabilityShade");
        Stretch(shade);
        shade.gameObject.AddComponent<XTapGachaShadeGraphic>().raycastTarget = false;

        content = MakeRect(root, "ResponsiveForgeContent");
        heading = Label(content, "대장간", 68, new Color(1f, .88f, .64f));
        chance = Label(content, "", 56, new Color(1f, .83f, .43f));
        details = Label(content, "", 30, new Color(.86f, .89f, .96f));
        details.resizeTextForBestFit = true;
        details.resizeTextMinSize = 24;
        details.resizeTextMaxSize = 30;

        stage = MakeRect(content, "HammerStage");
        stage.sizeDelta = new Vector2(1080f, 1000f);
        angelPoses = LoadPoses("forge_angel_poses");
        demonPoses = LoadPoses("forge_demon_poses");
        // Anvil is behind the swinging hammers. All actors use the same impact point.
        Image anvilImage = Picture(stage, "SingleAnvil", LoadSprite("forge_anvil"));
        anvil = anvilImage.rectTransform;
        anvil.sizeDelta = new Vector2(520f, 520f * 2f / 3f);
        anvil.anchoredPosition = new Vector2(0f, -190f);
        workpiece = MakeRect(stage, "SelectedBlockOnAnvil");
        workpiece.sizeDelta = new Vector2(160f, 110f);
        angel = Picture(stage, "AngelSmith", angelPoses[0]);
        demon = Picture(stage, "DemonSmith", demonPoses[0]);
        PlaceActor(angel, new Vector2(.923f, .746f));
        PlaceActor(demon, new Vector2(.102f, .787f));
        anvil.SetAsLastSibling();
        workpiece.SetAsLastSibling();

        RectTransform burst = MakeRect(stage, "HammerContactSparks");
        burst.anchoredPosition = new Vector2(0f, 55f);
        burst.sizeDelta = new Vector2(400f, 400f);
        impact = burst.gameObject.AddComponent<XTapForgeImpactGraphic>();
        impact.raycastTarget = false;
        impact.Progress = 1f;
        flash = Picture(root, "ImpactLight", null);
        Stretch(flash.rectTransform);
        flash.color = Color.clear;

        resultPlate = MakeRect(content, "CompactResult");
        Image plate = resultPlate.gameObject.AddComponent<Image>();
        plate.color = new Color(.012f, .02f, .035f, .82f);
        plate.raycastTarget = false;
        Image topLine = Picture(resultPlate, "GoldRule", null);
        topLine.color = new Color(.74f, .53f, .25f, .8f);
        topLine.rectTransform.anchorMin = new Vector2(.08f, 1f);
        topLine.rectTransform.anchorMax = new Vector2(.92f, 1f);
        topLine.rectTransform.sizeDelta = new Vector2(0f, 2f);
        phase = Label(resultPlate, "", 32, new Color(.85f, .89f, .97f));
        Anchor(phase.rectTransform, .04f, .58f, .96f, .94f);
        result = Label(resultPlate, "", 70, Color.white);
        Anchor(result.rectTransform, .04f, .10f, .96f, .61f);
        hint = Label(content, "천사 막타: 성공  ·  악마 막타: 실패", 28, new Color(.94f, .86f, .70f));
        ApplyLayout(true);
    }

    void PlaceActor(Image actor, Vector2 contactFromTop)
    {
        actor.rectTransform.sizeDelta = Vector2.one * ActorSize;
        // These measured frame-2 hammer-face coordinates land on the block's top.
        Vector2 localContact = new Vector2(contactFromTop.x - .5f, .5f - contactFromTop.y) * ActorSize;
        actor.rectTransform.anchoredPosition = contactPoint - localContact;
    }

    public IEnumerator Play(string operation, int successChance, bool success,
        bool destructive, XTapGearBlockData selectedBlock, int sacrificeValue)
    {
        ApplyLayout(true);
        heading.text = "대장간 · " + operation;
        chance.text = "성공률 " + successChance + "%";
        details.text = "제물 가치 " + sacrificeValue +
            (selectedBlock == null ? "" : "  ·  " + selectedBlock.displayName);
        phase.text = "천사와 악마의 망치 대결";
        result.text = "";
        angel.color = demon.color = Color.white;
        angel.sprite = angelPoses[0];
        demon.sprite = demonPoses[0];
        workpiece.localScale = Vector3.one;
        DrawBlock(selectedBlock);
        yield return new WaitForSecondsRealtime(.45f);

        // The lead-in is identical for success and failure; it cannot expose the roll.
        for (int i = 0; i < 6; i++)
            yield return Strike(i % 2 == 0, false, Mathf.Lerp(.64f, .44f, i / 5f), null);

        phase.text = "마지막 일격";
        yield return new WaitForSecondsRealtime(.30f);
        yield return Strike(success, true, 1.05f, delegate
        {
            phase.text = success ? "천사의 마지막 일격" : "악마의 마지막 일격";
            phase.color = success ? new Color(.66f, .89f, 1f) : new Color(1f, .48f, .36f);
            result.text = destructive && !success ? "강화 실패 · 파괴" : operation + (success ? " 성공" : " 실패");
            result.color = success ? new Color(1f, .89f, .50f) : new Color(1f, .34f, .25f);
            result.resizeTextForBestFit = true;
            result.resizeTextMinSize = 48;
            result.resizeTextMaxSize = 70;
            (success ? demon : angel).color = new Color(.62f, .62f, .68f, 1f);
#if UNITY_ANDROID && !UNITY_EDITOR
            if (PlayerPrefs.GetInt("xtap_option_vibration", 1) == 1) Handheld.Vibrate();
#endif
        });

        float time = 0f;
        while (time < .35f)
        {
            time += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(time / .35f);
            workpiece.localScale = Vector3.one * Mathf.Lerp(1f, success ? 1.16f : .65f, p);
            yield return null;
        }
        yield return new WaitForSecondsRealtime(1.1f);
    }

    IEnumerator Strike(bool angelHit, bool finalHit, float duration, Action onContact)
    {
        Image actor = angelHit ? angel : demon;
        Sprite[] poses = angelHit ? angelPoses : demonPoses;
        phase.text = finalHit ? "마지막 일격" : (angelHit ? "천사의 타격" : "악마의 타격");
        phase.color = angelHit ? new Color(.70f, .88f, 1f) : new Color(1f, .51f, .40f);
        actor.transform.SetAsLastSibling();
        anvil.SetAsLastSibling();
        workpiece.SetAsLastSibling();
        impact.transform.SetAsLastSibling();
        // Distinct hand/hammer poses; the character is never rotated as a rigid card.
        actor.sprite = poses[1];
        yield return new WaitForSecondsRealtime(duration * .46f);
        actor.sprite = poses[0];
        yield return new WaitForSecondsRealtime(duration * .08f);
        actor.sprite = poses[2];
        if (onContact != null) onContact();
        Color glow = angelHit ? new Color(1f, .86f, .38f) : new Color(1f, .25f, .12f);
        impact.color = glow;
        float elapsed = 0f;
        float decay = duration * .46f;
        while (elapsed < decay)
        {
            elapsed += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(elapsed / decay);
            impact.Progress = p;
            impact.rectTransform.localScale = Vector3.one * (finalHit ? 1.3f : .75f);
            float strength = (finalHit ? 12f : 5f) * (1f - p);
            stage.anchoredPosition = stageHome + new Vector2(Mathf.Sin(p * 65f), Mathf.Sin(p * 47f) * .4f) * strength;
            flash.color = new Color(glow.r, glow.g, glow.b, (1f - p) * (finalHit ? .13f : .035f));
            yield return null;
        }
        impact.Progress = 1f;
        flash.color = Color.clear;
        stage.anchoredPosition = stageHome;
        if (!finalHit) actor.sprite = poses[0];
    }

    void DrawBlock(XTapGearBlockData item)
    {
        for (int i = workpiece.childCount - 1; i >= 0; i--)
        {
            workpiece.GetChild(i).gameObject.SetActive(false);
            Destroy(workpiece.GetChild(i).gameObject);
        }
        var cells = new List<Vector2Int>();
        if (item != null && !string.IsNullOrEmpty(item.shape))
            foreach (string point in item.shape.Split(';'))
            {
                string[] pair = point.Split(',');
                int x, y;
                if (pair.Length == 2 && int.TryParse(pair[0], out x) && int.TryParse(pair[1], out y))
                    cells.Add(new Vector2Int(x, y));
            }
        if (cells.Count == 0) cells.Add(Vector2Int.zero);
        int minX = cells[0].x, maxX = minX, minY = cells[0].y, maxY = minY;
        foreach (Vector2Int cell in cells)
        {
            minX = Mathf.Min(minX, cell.x); maxX = Mathf.Max(maxX, cell.x);
            minY = Mathf.Min(minY, cell.y); maxY = Mathf.Max(maxY, cell.y);
        }
        float size = Mathf.Min(42f, 156f / (maxX - minX + 1), 106f / (maxY - minY + 1));
        float width = (maxX - minX + 1) * size;
        float height = (maxY - minY + 1) * size;
        contactPoint = new Vector2(0f, -53f + height);
        PlaceActor(angel, new Vector2(.923f, .746f));
        PlaceActor(demon, new Vector2(.102f, .787f));
        impact.rectTransform.anchoredPosition = contactPoint;
        // Rest the block on the anvil and strike its top, including short shapes.
        foreach (Vector2Int cell in cells)
        {
            Image square = Picture(workpiece, "BlockCell", null);
            square.color = item != null && item.exclusive ? new Color(.73f, .39f, 1f) : new Color(1f, .74f, .27f);
            square.rectTransform.sizeDelta = Vector2.one * (size - 2f);
            square.rectTransform.anchoredPosition = new Vector2((cell.x - minX + .5f) * size - width / 2f,
                -53f + height - (cell.y - minY + .5f) * size);
            Image inset = Picture(square.transform, "Inset", null);
            Anchor(inset.rectTransform, .15f, .15f, .85f, .85f);
            inset.color = new Color(.08f, .055f, .035f, 1f);
        }
    }

    void LateUpdate() { ApplyLayout(false); }

    void ApplyLayout(bool force)
    {
        if (content == null) return;
        Vector2 viewport = root.rect.size;
        Rect safe = Screen.safeArea;
        if (viewport.x <= 0f || viewport.y <= 0f) return;
        if (!force && viewport == lastViewport && safe == lastSafeArea) return;
        lastViewport = viewport; lastSafeArea = safe;
        float scale = viewport.x / Width;
        float height = viewport.y / scale;
        float pixels = Width / Mathf.Max(1, Screen.width);
        float top = Mathf.Max(28f, (Screen.height - safe.yMax) * pixels + 20f);
        float bottom = Mathf.Max(28f, safe.yMin * pixels + 20f);
        float side = Mathf.Max(42f, Mathf.Max(safe.xMin, Screen.width - safe.xMax) * pixels + 20f);
        float innerWidth = Width - 2f * side;
        content.sizeDelta = new Vector2(Width, height);
        content.localScale = Vector3.one * scale;
        float resultTop = height - bottom - 80f - 220f;
        float stageScale = Mathf.Min(innerWidth / 1080f, (resultTop - top - 330f) / 950f);
        stageScale = Mathf.Max(.4f, stageScale);
        stage.anchorMin = stage.anchorMax = new Vector2(.5f, 1f);
        stageHome = new Vector2(0f, -(resultTop - 400f * stageScale - 26f));
        stage.anchoredPosition = stageHome;
        stage.localScale = Vector3.one * stageScale;
        TopRect(heading.rectTransform, side, top, innerWidth, 100f);
        TopRect(chance.rectTransform, side, top + 118f, innerWidth, 82f);
        TopRect(details.rectTransform, side, top + 208f, innerWidth, 90f);
        TopRect(resultPlate, side, resultTop, innerWidth, 220f);
        TopRect(hint.rectTransform, side, height - bottom - 66f, innerWidth, 56f);
    }

    Sprite[] LoadPoses(string name)
    {
        Texture2D texture = LoadTexture(name);
        if (texture.width != texture.height * 3)
            throw new InvalidOperationException("대장간 3프레임 비율 오류: " + name);
        Sprite[] frames = new Sprite[3];
        for (int i = 0; i < frames.Length; i++)
        {
            frames[i] = Sprite.Create(texture, new UnityEngine.Rect(i * texture.height, 0, texture.height, texture.height),
                new Vector2(.5f, .5f), 100f, 0, SpriteMeshType.FullRect);
            sprites.Add(frames[i]);
        }
        return frames;
    }

    Texture2D LoadTexture(string name)
    {
        TextAsset source = Resources.Load<TextAsset>("XTapBlacksmithUI/" + name);
        Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        textures.Add(texture);
        if (source == null || !texture.LoadImage(source.bytes, true))
            throw new InvalidOperationException("대장간 필수 이미지 디코딩 실패: " + name);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;
        return texture;
    }

    Sprite LoadSprite(string name)
    {
        Texture2D texture = LoadTexture(name);
        Sprite sprite = Sprite.Create(texture, new UnityEngine.Rect(0, 0, texture.width, texture.height),
            new Vector2(.5f, .5f), 100f, 0, SpriteMeshType.FullRect);
        sprites.Add(sprite);
        return sprite;
    }

    static RectTransform MakeRect(Transform parent, string name)
    {
        RectTransform rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
        return rect;
    }

    static Image Picture(Transform parent, string name, Sprite sprite)
    {
        Image image = MakeRect(parent, name).gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.raycastTarget = false;
        return image;
    }

    Text Label(Transform parent, string value, int size, Color color)
    {
        Text text = MakeRect(parent, "Label").gameObject.AddComponent<Text>();
        text.font = font;
        text.text = value;
        text.fontSize = size;
        text.fontStyle = FontStyle.Bold;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = color;
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        Outline outline = text.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(.01f, .015f, .025f, .92f);
        outline.effectDistance = new Vector2(2f, -2f);
        return text;
    }

    static void Stretch(RectTransform rect) { Anchor(rect, 0f, 0f, 1f, 1f); }
    static void Anchor(RectTransform rect, float x0, float y0, float x1, float y1)
    {
        rect.anchorMin = new Vector2(x0, y0); rect.anchorMax = new Vector2(x1, y1);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }
    static void TopRect(RectTransform rect, float x, float y, float width, float height)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(width, height);
    }
    void OnDestroy()
    {
        foreach (Sprite sprite in sprites) if (sprite != null) Destroy(sprite);
        foreach (Texture2D texture in textures) if (texture != null) Destroy(texture);
    }
}

[RequireComponent(typeof(CanvasRenderer))]
public sealed class XTapForgeImpactGraphic : MaskableGraphic
{
    float progress = 1f;
    public float Progress { set { progress = Mathf.Clamp01(value); SetVerticesDirty(); } }
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (progress >= 1f) return;
        Color tint = color;
        tint.a *= 1f - progress;
        for (int i = 0; i < 20; i++)
        {
            float angle = i * Mathf.PI * 2f / 20f;
            Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            Vector2 normal = new Vector2(-direction.y, direction.x);
            float radius = Mathf.Lerp(12f, 170f, progress) * (i % 3 == 0 ? 1f : .68f);
            float length = (1f - progress) * (i % 3 == 0 ? 55f : 28f);
            int start = vh.currentVertCount;
            Add(vh, direction * radius - normal * 3f, tint);
            Add(vh, direction * radius + normal * 3f, tint);
            Add(vh, direction * (radius + length), tint);
            vh.AddTriangle(start, start + 1, start + 2);
        }
    }
    static void Add(VertexHelper vh, Vector2 position, Color tint)
    {
        UIVertex vertex = UIVertex.simpleVert;
        vertex.position = position;
        vertex.color = tint;
        vh.AddVert(vertex);
    }
}
