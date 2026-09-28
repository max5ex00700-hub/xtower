using UnityEngine;
using UnityEngine.UI;

// Small fixed pools: no Instantiate/Destroy loop during fast combat.
// All elements ignore raycasts. This component owns presentation only.
public sealed class XTapCombatEffects : MonoBehaviour
{
    sealed class Particle
    {
        public Image image;
        public Vector2 position, velocity, size;
        public float age, life, rotation, spin;
        public Color color;
        public bool ring;
    }
    sealed class Word
    {
        public Text text;
        public Vector2 origin;
        public Color color;
        public float age, life, rise, scale;
    }
    RectTransform host, layer;
    Font font;
    Sprite ringSprite;
    readonly Particle[] particles = new Particle[80];
    readonly Word[] words = new Word[10];
    readonly Sprite[] goldChips = new Sprite[16];
    readonly Sprite[] blueChips = new Sprite[16];
    Text comboText;
    Image finishTop, finishBottom;
    int nextParticle, nextWord;
    float finishAge = 2f, unit = 1f;
    readonly System.Random random = new System.Random();
    public bool Frozen { get; set; }

    public void Initialize(RectTransform parent, Font uiFont, Sprite ring, Sprite hit, Sprite shield)
    {
        host = parent; font = uiFont; ringSprite = ring;
        layer = new GameObject("CombatPresentation", typeof(RectTransform)).GetComponent<RectTransform>();
        layer.SetParent(parent, false); Stretch(layer);
        Slice(hit, goldChips); Slice(shield, blueChips);
        for (int i = 0; i < particles.Length; i++)
        {
            Image image = MakeImage("ImpactParticle_" + i);
            image.gameObject.SetActive(false);
            particles[i] = new Particle { image = image, life = 0f };
        }
        for (int i = 0; i < words.Length; i++)
        {
            Text text = MakeText("ImpactWord_" + i);
            text.gameObject.SetActive(false);
            words[i] = new Word { text = text, life = 0f };
        }
        comboText = MakeText("ComboCount");
        comboText.rectTransform.anchorMin = comboText.rectTransform.anchorMax = new Vector2(.5f, 1f);
        comboText.rectTransform.pivot = new Vector2(.5f, 1f);
        comboText.rectTransform.sizeDelta = new Vector2(620f, 130f);
        comboText.gameObject.SetActive(false);
        finishTop = MakeImage("FinishTopBar"); finishBottom = MakeImage("FinishBottomBar");
        finishTop.rectTransform.anchorMin = new Vector2(0f, .94f); finishTop.rectTransform.anchorMax = Vector2.one;
        finishBottom.rectTransform.anchorMin = Vector2.zero; finishBottom.rectTransform.anchorMax = new Vector2(1f, .06f);
        finishTop.rectTransform.offsetMin = finishTop.rectTransform.offsetMax = Vector2.zero;
        finishBottom.rectTransform.offsetMin = finishBottom.rectTransform.offsetMax = Vector2.zero;
        Clear();
    }

    public void Hit(Vector2 screen, double damage, XTapCombatCue kind, int combo)
    {
        bool heavy = kind != XTapCombatCue.None;
        int tier = combo >= 10 ? 3 : combo >= 6 ? 2 : combo >= 3 ? 1 : 0;
        Color color = kind == XTapCombatCue.Counter ? new Color(.40f, .86f, 1f)
            : kind == XTapCombatCue.Followup ? new Color(1f, .40f, .16f) : new Color(1f, .78f, .30f);
        Vector2 local = Local(screen);
        Burst(local, color, heavy ? 16 + tier * 3 : 7 + tier * 2, heavy ? 510f : 310f, heavy);
        Ring(local, color, heavy ? 360f : 195f, .32f);
        if (heavy) Ring(local, Color.white, 235f, .20f);
        Slash(local, color, kind == XTapCombatCue.Followup ? -25f : 32f, heavy ? 420f : 230f);
        if (heavy) Slash(local, Color.white, -42f, 300f);
        string prefix = kind == XTapCombatCue.Counter ? "반격! " : kind == XTapCombatCue.Followup ? "추가타! " : heavy ? "약점! " : "";
        Label(local + Vector2.up * 75f * unit, prefix + XTapStatFormat.Compact(damage), color,
            heavy ? 70 : 53, heavy ? .82f : .58f, 130f);
        SetCombo(combo);
        if (combo == 3 || combo == 6 || combo == 10)
        {
            string word = combo == 3 ? "3연속 적중!" : combo == 6 ? "6연속 · 맹공!" : "10연속 · 압도!";
            Label(new Vector2(0f, host.rect.height * .28f), word, color, combo == 10 ? 88 : 72, .85f, 20f);
            Burst(new Vector2(0f, host.rect.height * .28f), color, 6 + tier * 3, 270f, false);
        }
    }

    public void Guard(Vector2 screen, bool perfect)
    {
        Vector2 local = Local(screen);
        Color color = perfect ? new Color(1f, .91f, .52f) : new Color(.38f, .80f, 1f);
        Ring(local, color, perfect ? 490f : 310f, perfect ? .45f : .30f);
        Burst(local, color, perfect ? 20 : 8, perfect ? 570f : 330f, true, true);
        Label(local + Vector2.up * 110f * unit, perfect ? "완벽 방어!" : "방어!", color, perfect ? 79 : 60, .7f, 80f);
    }
    public void CounterReady(Vector2 screen)
    {
        Label(Local(screen) + Vector2.up * 155f * unit, "반격 기회!", new Color(.45f, .88f, 1f), 56, .58f, 40f);
    }
    public void Miss(Vector2 screen, string text)
    {
        SetCombo(0);
        Label(Local(screen), text, new Color(1f, .52f, .42f), 46, .55f, 60f);
    }
    public void Dodge(Vector2 screen)
    {
        Label(Local(screen), "회피", new Color(.74f, .83f, .95f), 44, .4f, 65f);
    }
    public void Finish(Vector2 screen)
    {
        Vector2 local = Local(screen);
        finishAge = 0f;
        finishTop.gameObject.SetActive(true); finishBottom.gameObject.SetActive(true);
        Color gold = new Color(1f, .85f, .44f);
        Burst(local, gold, 30, 820f, true);
        Ring(local, gold, 840f, .58f);
        Slash(local, Color.white, -18f, 1150f);
        Label(new Vector2(0f, host.rect.height * .24f), "FINISH", gold, 122, .58f, 15f);
    }
    public void SetCombo(int combo)
    {
        comboText.gameObject.SetActive(combo >= 2);
        comboText.text = combo + " COMBO";
        comboText.color = combo >= 10 ? new Color(1f, .44f, .18f)
            : combo >= 6 ? new Color(1f, .78f, .24f) : new Color(1f, .94f, .72f);
    }
    public void Clear()
    {
        Frozen = false; finishAge = 2f;
        for (int i = 0; i < particles.Length; i++) if (particles[i] != null) { particles[i].life = 0f; particles[i].image.gameObject.SetActive(false); }
        for (int i = 0; i < words.Length; i++) if (words[i] != null) { words[i].life = 0f; words[i].text.gameObject.SetActive(false); }
        if (comboText != null) comboText.gameObject.SetActive(false);
        if (finishTop != null) finishTop.gameObject.SetActive(false);
        if (finishBottom != null) finishBottom.gameObject.SetActive(false);
    }
    void Update()
    {
        if (host == null || Frozen) return;
        unit = host.rect.width / 1080f;
        float dt = Mathf.Min(.1f, Time.unscaledDeltaTime);
        float safeTop = (Screen.height - Screen.safeArea.yMax) / Mathf.Max(1f, Screen.height) * host.rect.height;
        comboText.rectTransform.anchoredPosition = new Vector2(0f, -(safeTop + 24f * unit));
        comboText.rectTransform.sizeDelta = new Vector2(620f, 130f) * unit;
        comboText.fontSize = Mathf.RoundToInt(53f * unit);
        foreach (Particle p in particles)
        {
            if (p.life <= 0f) continue;
            p.age += dt;
            float t = Mathf.Clamp01(p.age / p.life);
            p.position += p.velocity * dt;
            if (!p.ring) p.velocity += Vector2.down * (210f * unit * dt);
            p.rotation += p.spin * dt;
            RectTransform r = p.image.rectTransform;
            r.anchoredPosition = p.position;
            r.localRotation = Quaternion.Euler(0f, 0f, p.rotation);
            float size = p.ring ? Mathf.Lerp(.35f, 1.65f, t) : Mathf.Lerp(1f, .35f, t);
            r.sizeDelta = p.size * size;
            p.image.color = new Color(p.color.r, p.color.g, p.color.b, p.color.a * (1f-t) * (1f-t));
            if (t >= 1f) { p.life = 0f; p.image.gameObject.SetActive(false); }
        }
        foreach (Word w in words)
        {
            if (w.life <= 0f) continue;
            w.age += dt;
            float t = Mathf.Clamp01(w.age / w.life);
            w.text.rectTransform.anchoredPosition = w.origin + Vector2.up * w.rise * unit * t;
            w.text.rectTransform.localScale = Vector3.one * Mathf.Lerp(w.scale, 1f, Mathf.Clamp01(t * 6f));
            w.text.color = new Color(w.color.r, w.color.g, w.color.b, 1f-Mathf.Clamp01((t-.65f)/.35f));
            if (t >= 1f) { w.life = 0f; w.text.gameObject.SetActive(false); }
        }
        if (finishAge < .65f)
        {
            finishAge += dt;
            float alpha = .9f * (1f - Mathf.Clamp01((finishAge-.4f)/.2f));
            finishTop.color = finishBottom.color = new Color(.008f, .01f, .018f, alpha);
            if (finishAge >= .65f) { finishTop.gameObject.SetActive(false); finishBottom.gameObject.SetActive(false); }
        }
    }
    Vector2 Local(Vector2 screen)
    {
        unit = host.rect.width / 1080f;
        Vector2 local;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(host, screen, null, out local);
        return local;
    }
    void Burst(Vector2 at, Color color, int count, float speed, bool fragments, bool blue = false)
    {
        for (int i = 0; i < count; i++)
        {
            float angle = (i / (float)count * 360f + Range(-12f, 12f)) * Mathf.Deg2Rad;
            Vector2 velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * Range(.45f, 1f) * speed * unit;
            Sprite sprite = fragments && i % 2 == 0 ? (blue ? blueChips[i % 16] : goldChips[i % 16]) : null;
            Vector2 size = sprite == null ? new Vector2(Range(7f, 15f), Range(22f, 48f)) : Vector2.one * Range(42f, 80f);
            Spawn(at, velocity, size * unit, color, Range(.24f, .48f), sprite, false, Range(-320f, 320f));
        }
    }
    void Ring(Vector2 at, Color color, float size, float duration)
    {
        Spawn(at, Vector2.zero, Vector2.one * size * unit, color, duration, ringSprite, true, 0f);
    }
    void Slash(Vector2 at, Color color, float rotation, float length)
    {
        Particle p = Spawn(at, Vector2.zero, new Vector2(length, 8f) * unit, color, .15f, null, false, 0f);
        p.rotation = rotation; p.image.rectTransform.localRotation = Quaternion.Euler(0f, 0f, rotation);
    }
    Particle Spawn(Vector2 at, Vector2 velocity, Vector2 size, Color color, float life, Sprite sprite, bool ring, float spin)
    {
        Particle p = particles[nextParticle];
        nextParticle = (nextParticle + 1) % particles.Length;
        p.age = 0f; p.life = life; p.position = at; p.velocity = velocity; p.size = size;
        p.color = color; p.ring = ring; p.spin = spin; p.rotation = Range(0f, 360f);
        p.image.sprite = sprite; p.image.color = color;
        p.image.rectTransform.anchoredPosition = at; p.image.rectTransform.sizeDelta = ring ? size * .35f : size;
        p.image.rectTransform.localRotation = Quaternion.Euler(0f, 0f, p.rotation);
        p.image.gameObject.SetActive(true);
        return p;
    }
    void Label(Vector2 at, string value, Color color, int size, float life, float rise)
    {
        Word w = words[nextWord];
        nextWord = (nextWord + 1) % words.Length;
        float halfWidth = host.rect.width * .40f;
        at.x = Mathf.Clamp(at.x, -host.rect.width*.5f + halfWidth, host.rect.width*.5f-halfWidth);
        at.y = Mathf.Clamp(at.y, -host.rect.height*.40f, host.rect.height*.40f);
        w.origin = at; w.age = 0f; w.life = life; w.rise = rise; w.color = color; w.scale = 1.2f;
        w.text.text = value; w.text.fontSize = Mathf.RoundToInt(size * unit); w.text.color = color;
        w.text.rectTransform.sizeDelta = new Vector2(halfWidth*2f, 160f * unit);
        w.text.rectTransform.anchoredPosition = at; w.text.rectTransform.localScale = Vector3.one * w.scale;
        w.text.gameObject.SetActive(true);
    }
    Image MakeImage(string name)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(layer, false);
        Image image = go.GetComponent<Image>(); image.raycastTarget = false; return image;
    }
    Text MakeText(string name)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text), typeof(Outline));
        go.transform.SetParent(layer, false);
        Text text = go.GetComponent<Text>(); text.font = font; text.fontStyle = FontStyle.Bold;
        text.alignment = TextAnchor.MiddleCenter; text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Overflow; text.verticalOverflow = VerticalWrapMode.Overflow;
        Outline outline = go.GetComponent<Outline>(); outline.effectColor = new Color(.015f, .008f, .005f, .95f); outline.effectDistance = new Vector2(2.5f, -2.5f);
        return text;
    }
    static void Stretch(RectTransform r)
    {
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero;
    }
    float Range(float min, float max) { return min + (max-min) * (float)random.NextDouble(); }
    static void Slice(Sprite sprite, Sprite[] result)
    {
        if (sprite == null) return;
        float w = sprite.rect.width / 4f, h = sprite.rect.height / 4f;
        for (int i = 0; i < result.Length; i++)
            result[i] = Sprite.Create(sprite.texture, new Rect(sprite.rect.x + (i % 4)*w, sprite.rect.y + (i/4)*h, w, h), new Vector2(.5f,.5f), 100f, 0, SpriteMeshType.FullRect);
    }
    void OnDestroy()
    {
        foreach (Sprite s in goldChips) if (s != null) Destroy(s);
        foreach (Sprite s in blueChips) if (s != null) Destroy(s);
        if (layer != null) Destroy(layer.gameObject);
    }
}
