using System;
using UnityEngine;

public static class XTapMainSkin
{
    static bool attempted;
    static Texture2D atlas;

    public static Sprite FloorPanel { get; private set; }
    public static Sprite FightButton { get; private set; }
    public static Sprite UtilityButton { get; private set; }
    public static Sprite BottomRail { get; private set; }
    public static Sprite PlayerPanel { get; private set; }
    public static Sprite NavButton { get; private set; }

    public static bool Ready
    {
        get
        {
            EnsureLoaded();
            return FightButton != null && NavButton != null && UtilityButton != null;
        }
    }

    public static void EnsureLoaded()
    {
        if (attempted) return;
        attempted = true;

        LoadLegacyPanelAtlas();

        // Main buttons are generated at runtime so Cloud Build can never fall
        // back to flat white Unity Images because of an importer/meta problem.
        FightButton = CreateFightButton();
        NavButton = CreateNavButton();
        UtilityButton = CreateUtilityButton();
    }

    static void LoadLegacyPanelAtlas()
    {
        try
        {
            TextAsset encoded = Resources.Load<TextAsset>("XTapMainUI/main_atlas_runtime");
            if (encoded != null && !string.IsNullOrWhiteSpace(encoded.text))
            {
                byte[] bytes = Convert.FromBase64String(encoded.text.Trim());
                atlas = new Texture2D(2, 2, TextureFormat.RGBA32, false);

                if (!atlas.LoadImage(bytes, false))
                {
                    UnityEngine.Object.Destroy(atlas);
                    atlas = null;
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("X탑 메인 UI 런타임 아틀라스 로드 실패: " + e.Message);
            if (atlas != null)
            {
                UnityEngine.Object.Destroy(atlas);
                atlas = null;
            }
        }

        if (atlas == null)
            atlas = Resources.Load<Texture2D>("XTapMainUI/main_atlas");

        if (atlas == null)
        {
            Debug.LogWarning("X탑 메인 패널 아틀라스를 불러오지 못했습니다. 버튼은 런타임 스킨으로 계속 표시됩니다.");
            return;
        }

        atlas.wrapMode = TextureWrapMode.Clamp;
        atlas.filterMode = FilterMode.Bilinear;

        FloorPanel  = MakeAtlasSprite(0,   429, 256, 83,  new Vector4(34f, 22f, 34f, 22f));
        BottomRail  = MakeAtlasSprite(256, 346, 256, 83,  new Vector4(34f, 22f, 34f, 22f));
        PlayerPanel = MakeAtlasSprite(0,   154, 128, 192, new Vector4(24f, 34f, 24f, 34f));
    }

    static Sprite CreateFightButton()
    {
        Vector2[] outer =
        {
            new Vector2(.05f,.50f), new Vector2(.10f,.68f), new Vector2(.08f,.86f),
            new Vector2(.18f,.84f), new Vector2(.22f,.96f), new Vector2(.30f,.88f),
            new Vector2(.44f,.88f), new Vector2(.50f,.99f), new Vector2(.56f,.88f),
            new Vector2(.70f,.88f), new Vector2(.78f,.96f), new Vector2(.82f,.84f),
            new Vector2(.92f,.86f), new Vector2(.90f,.68f), new Vector2(.95f,.50f),
            new Vector2(.90f,.32f), new Vector2(.92f,.14f), new Vector2(.82f,.16f),
            new Vector2(.78f,.04f), new Vector2(.70f,.12f), new Vector2(.56f,.12f),
            new Vector2(.50f,.01f), new Vector2(.44f,.12f), new Vector2(.30f,.12f),
            new Vector2(.22f,.04f), new Vector2(.18f,.16f), new Vector2(.08f,.14f),
            new Vector2(.10f,.32f)
        };

        Vector2[] inner =
        {
            new Vector2(.10f,.50f), new Vector2(.14f,.67f), new Vector2(.12f,.79f),
            new Vector2(.20f,.78f), new Vector2(.24f,.88f), new Vector2(.31f,.82f),
            new Vector2(.45f,.82f), new Vector2(.50f,.91f), new Vector2(.55f,.82f),
            new Vector2(.69f,.82f), new Vector2(.76f,.88f), new Vector2(.80f,.78f),
            new Vector2(.88f,.79f), new Vector2(.86f,.67f), new Vector2(.90f,.50f),
            new Vector2(.86f,.33f), new Vector2(.88f,.21f), new Vector2(.80f,.22f),
            new Vector2(.76f,.12f), new Vector2(.69f,.18f), new Vector2(.55f,.18f),
            new Vector2(.50f,.09f), new Vector2(.45f,.18f), new Vector2(.31f,.18f),
            new Vector2(.24f,.12f), new Vector2(.20f,.22f), new Vector2(.12f,.21f),
            new Vector2(.14f,.33f)
        };

        Vector2[] core =
        {
            new Vector2(.15f,.50f), new Vector2(.18f,.70f), new Vector2(.23f,.76f),
            new Vector2(.77f,.76f), new Vector2(.82f,.70f), new Vector2(.85f,.50f),
            new Vector2(.82f,.30f), new Vector2(.77f,.24f), new Vector2(.23f,.24f),
            new Vector2(.18f,.30f)
        };

        return BuildButtonSprite(512, 192, outer, inner, core, true, true);
    }

    static Sprite CreateNavButton()
    {
        Vector2[] outer =
        {
            new Vector2(.10f,.16f), new Vector2(.07f,.26f), new Vector2(.08f,.78f),
            new Vector2(.13f,.85f), new Vector2(.10f,.92f), new Vector2(.24f,.92f),
            new Vector2(.29f,.98f), new Vector2(.71f,.98f), new Vector2(.76f,.92f),
            new Vector2(.90f,.92f), new Vector2(.87f,.85f), new Vector2(.92f,.78f),
            new Vector2(.93f,.26f), new Vector2(.90f,.16f), new Vector2(.82f,.10f),
            new Vector2(.76f,.02f), new Vector2(.24f,.02f), new Vector2(.18f,.10f)
        };

        Vector2[] inner =
        {
            new Vector2(.15f,.20f), new Vector2(.12f,.28f), new Vector2(.13f,.74f),
            new Vector2(.18f,.81f), new Vector2(.16f,.87f), new Vector2(.28f,.87f),
            new Vector2(.32f,.92f), new Vector2(.68f,.92f), new Vector2(.72f,.87f),
            new Vector2(.84f,.87f), new Vector2(.82f,.81f), new Vector2(.87f,.74f),
            new Vector2(.88f,.28f), new Vector2(.85f,.20f), new Vector2(.78f,.15f),
            new Vector2(.72f,.08f), new Vector2(.28f,.08f), new Vector2(.22f,.15f)
        };

        Vector2[] core =
        {
            new Vector2(.22f,.22f), new Vector2(.18f,.31f), new Vector2(.19f,.70f),
            new Vector2(.25f,.78f), new Vector2(.75f,.78f), new Vector2(.81f,.70f),
            new Vector2(.82f,.31f), new Vector2(.78f,.22f), new Vector2(.70f,.15f),
            new Vector2(.30f,.15f)
        };

        return BuildButtonSprite(256, 320, outer, inner, core, false, true);
    }

    static Sprite CreateUtilityButton()
    {
        Vector2[] outer =
        {
            new Vector2(.12f,.18f), new Vector2(.06f,.30f), new Vector2(.06f,.70f),
            new Vector2(.12f,.82f), new Vector2(.20f,.86f), new Vector2(.24f,.94f),
            new Vector2(.38f,.94f), new Vector2(.44f,.99f), new Vector2(.56f,.99f),
            new Vector2(.62f,.94f), new Vector2(.76f,.94f), new Vector2(.80f,.86f),
            new Vector2(.88f,.82f), new Vector2(.94f,.70f), new Vector2(.94f,.30f),
            new Vector2(.88f,.18f), new Vector2(.80f,.14f), new Vector2(.76f,.06f),
            new Vector2(.62f,.06f), new Vector2(.56f,.01f), new Vector2(.44f,.01f),
            new Vector2(.38f,.06f), new Vector2(.24f,.06f), new Vector2(.20f,.14f)
        };

        Vector2[] inner =
        {
            new Vector2(.17f,.21f), new Vector2(.11f,.32f), new Vector2(.11f,.68f),
            new Vector2(.17f,.79f), new Vector2(.24f,.82f), new Vector2(.28f,.89f),
            new Vector2(.72f,.89f), new Vector2(.76f,.82f), new Vector2(.83f,.79f),
            new Vector2(.89f,.68f), new Vector2(.89f,.32f), new Vector2(.83f,.21f),
            new Vector2(.76f,.18f), new Vector2(.72f,.11f), new Vector2(.28f,.11f),
            new Vector2(.24f,.18f)
        };

        Vector2[] core =
        {
            new Vector2(.24f,.24f), new Vector2(.18f,.35f), new Vector2(.18f,.65f),
            new Vector2(.24f,.76f), new Vector2(.76f,.76f), new Vector2(.82f,.65f),
            new Vector2(.82f,.35f), new Vector2(.76f,.24f)
        };

        return BuildButtonSprite(256, 256, outer, inner, core, false, true);
    }

    static Sprite BuildButtonSprite(
        int width,
        int height,
        Vector2[] outer,
        Vector2[] inner,
        Vector2[] core,
        bool redCore,
        bool jewel)
    {
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.name = "XTapRuntimeGothicButton";
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        Color32 transparent = new Color32(0, 0, 0, 0);

        for (int y = 0; y < height; y++)
        {
            float v = (y + .5f) / height;

            for (int x = 0; x < width; x++)
            {
                float u = (x + .5f) / width;
                Vector2 p = new Vector2(u, v);

                if (!Inside(p, outer))
                {
                    texture.SetPixel(x, y, transparent);
                    continue;
                }

                float noise = HashNoise(x, y) * .08f - .04f;
                Color c;

                if (Inside(p, core))
                {
                    if (redCore)
                    {
                        float glow = Mathf.Clamp01(1f - Mathf.Abs(v - .52f) * 1.45f);
                        c = new Color(
                            .20f + .28f * glow,
                            .018f + .020f * glow,
                            .024f + .018f * glow,
                            1f);
                    }
                    else
                    {
                        float shade = .065f + .035f * v;
                        c = new Color(shade, shade * .94f, shade * .96f, 1f);
                    }

                    if (DistanceToEdges(p, core) < .010f)
                        c = redCore
                            ? new Color(.92f, .28f, .055f, 1f)
                            : new Color(.48f, .31f, .15f, 1f);
                }
                else if (Inside(p, inner))
                {
                    float metal = .105f + .055f * v;
                    c = new Color(metal, metal * .94f, metal * .96f, 1f);

                    if (DistanceToEdges(p, inner) < .010f)
                        c = new Color(.38f, .30f, .25f, 1f);
                }
                else
                {
                    float metal = .18f + .09f * v;
                    c = new Color(metal * 1.15f, metal * .78f, metal * .46f, 1f);

                    if (DistanceToEdges(p, outer) < .010f)
                        c = new Color(.80f, .52f, .24f, 1f);
                }

                if (jewel)
                {
                    float dx = Mathf.Abs(u - .5f) / .030f;
                    float dyTop = Mathf.Abs(v - .90f) / .055f;
                    float dyBottom = Mathf.Abs(v - .10f) / .055f;
                    bool topGem = dx + dyTop <= 1f;
                    bool bottomGem = dx + dyBottom <= 1f;

                    if (topGem || bottomGem)
                    {
                        float q = topGem ? dx + dyTop : dx + dyBottom;
                        c = q < .45f
                            ? new Color(1f, .20f, .055f, 1f)
                            : new Color(.52f, .035f, .025f, 1f);
                    }
                }

                c.r = Mathf.Clamp01(c.r + noise);
                c.g = Mathf.Clamp01(c.g + noise);
                c.b = Mathf.Clamp01(c.b + noise);
                texture.SetPixel(x, y, c);
            }
        }

        texture.Apply(false, true);

        return Sprite.Create(
            texture,
            new Rect(0f, 0f, width, height),
            new Vector2(.5f, .5f),
            100f,
            0,
            SpriteMeshType.FullRect
        );
    }

    static bool Inside(Vector2 p, Vector2[] polygon)
    {
        bool inside = false;
        int j = polygon.Length - 1;

        for (int i = 0; i < polygon.Length; i++)
        {
            Vector2 a = polygon[i];
            Vector2 b = polygon[j];

            bool cross =
                ((a.y > p.y) != (b.y > p.y)) &&
                (p.x < (b.x - a.x) * (p.y - a.y) / Mathf.Max(.00001f, b.y - a.y) + a.x);

            if (cross)
                inside = !inside;

            j = i;
        }

        return inside;
    }

    static float DistanceToEdges(Vector2 p, Vector2[] polygon)
    {
        float best = float.MaxValue;

        for (int i = 0; i < polygon.Length; i++)
        {
            Vector2 a = polygon[i];
            Vector2 b = polygon[(i + 1) % polygon.Length];
            Vector2 ab = b - a;
            float lengthSq = Mathf.Max(.000001f, Vector2.Dot(ab, ab));
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / lengthSq);
            float d = Vector2.Distance(p, a + ab * t);
            if (d < best) best = d;
        }

        return best;
    }

    static float HashNoise(int x, int y)
    {
        unchecked
        {
            uint n = (uint)(x * 73856093 ^ y * 19349663);
            n ^= n << 13;
            n ^= n >> 17;
            n ^= n << 5;
            return (n & 1023u) / 1023f;
        }
    }

    static Sprite MakeAtlasSprite(int x, int y, int width, int height, Vector4 border)
    {
        if (atlas == null) return null;

        return Sprite.Create(
            atlas,
            new Rect(x, y, width, height),
            new Vector2(.5f, .5f),
            100f,
            0,
            SpriteMeshType.FullRect,
            border
        );
    }
}
