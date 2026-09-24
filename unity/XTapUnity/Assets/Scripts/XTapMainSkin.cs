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
        // Reference-inspired wide gothic battle plaque:
        // red core, black iron body, bronze spikes and crown-like tips.
        Vector2[] outer =
        {
            new Vector2(.025f,.50f), new Vector2(.065f,.64f), new Vector2(.050f,.79f),
            new Vector2(.135f,.78f), new Vector2(.175f,.92f), new Vector2(.285f,.87f),
            new Vector2(.435f,.87f), new Vector2(.50f,.995f), new Vector2(.565f,.87f),
            new Vector2(.715f,.87f), new Vector2(.825f,.92f), new Vector2(.865f,.78f),
            new Vector2(.950f,.79f), new Vector2(.935f,.64f), new Vector2(.975f,.50f),
            new Vector2(.935f,.36f), new Vector2(.950f,.21f), new Vector2(.865f,.22f),
            new Vector2(.825f,.08f), new Vector2(.715f,.13f), new Vector2(.565f,.13f),
            new Vector2(.50f,.005f), new Vector2(.435f,.13f), new Vector2(.285f,.13f),
            new Vector2(.175f,.08f), new Vector2(.135f,.22f), new Vector2(.050f,.21f),
            new Vector2(.065f,.36f)
        };

        Vector2[] inner =
        {
            new Vector2(.075f,.50f), new Vector2(.105f,.63f), new Vector2(.090f,.73f),
            new Vector2(.165f,.72f), new Vector2(.205f,.83f), new Vector2(.30f,.79f),
            new Vector2(.445f,.79f), new Vector2(.50f,.885f), new Vector2(.555f,.79f),
            new Vector2(.70f,.79f), new Vector2(.795f,.83f), new Vector2(.835f,.72f),
            new Vector2(.910f,.73f), new Vector2(.895f,.63f), new Vector2(.925f,.50f),
            new Vector2(.895f,.37f), new Vector2(.910f,.27f), new Vector2(.835f,.28f),
            new Vector2(.795f,.17f), new Vector2(.70f,.21f), new Vector2(.555f,.21f),
            new Vector2(.50f,.115f), new Vector2(.445f,.21f), new Vector2(.30f,.21f),
            new Vector2(.205f,.17f), new Vector2(.165f,.28f), new Vector2(.090f,.27f),
            new Vector2(.105f,.37f)
        };

        Vector2[] core =
        {
            new Vector2(.135f,.50f), new Vector2(.17f,.64f), new Vector2(.23f,.70f),
            new Vector2(.77f,.70f), new Vector2(.83f,.64f), new Vector2(.865f,.50f),
            new Vector2(.83f,.36f), new Vector2(.77f,.30f), new Vector2(.23f,.30f),
            new Vector2(.17f,.36f)
        };

        return BuildButtonSprite(640, 240, outer, inner, core, true, true);
    }

    static Sprite CreateNavButton()
    {
        // Reference-inspired tall black iron menu frame.
        Vector2[] outer =
        {
            new Vector2(.10f,.11f), new Vector2(.055f,.22f), new Vector2(.07f,.76f),
            new Vector2(.13f,.83f), new Vector2(.095f,.91f), new Vector2(.25f,.90f),
            new Vector2(.31f,.98f), new Vector2(.43f,.95f), new Vector2(.50f,1.00f),
            new Vector2(.57f,.95f), new Vector2(.69f,.98f), new Vector2(.75f,.90f),
            new Vector2(.905f,.91f), new Vector2(.87f,.83f), new Vector2(.93f,.76f),
            new Vector2(.945f,.22f), new Vector2(.90f,.11f), new Vector2(.80f,.07f),
            new Vector2(.75f,.015f), new Vector2(.58f,.045f), new Vector2(.50f,0f),
            new Vector2(.42f,.045f), new Vector2(.25f,.015f), new Vector2(.20f,.07f)
        };

        Vector2[] inner =
        {
            new Vector2(.16f,.16f), new Vector2(.115f,.26f), new Vector2(.13f,.71f),
            new Vector2(.19f,.78f), new Vector2(.16f,.84f), new Vector2(.29f,.84f),
            new Vector2(.34f,.90f), new Vector2(.66f,.90f), new Vector2(.71f,.84f),
            new Vector2(.84f,.84f), new Vector2(.81f,.78f), new Vector2(.87f,.71f),
            new Vector2(.885f,.26f), new Vector2(.84f,.16f), new Vector2(.76f,.13f),
            new Vector2(.71f,.075f), new Vector2(.29f,.075f), new Vector2(.24f,.13f)
        };

        Vector2[] core =
        {
            new Vector2(.23f,.20f), new Vector2(.18f,.30f), new Vector2(.19f,.68f),
            new Vector2(.25f,.75f), new Vector2(.75f,.75f), new Vector2(.81f,.68f),
            new Vector2(.82f,.30f), new Vector2(.77f,.20f), new Vector2(.69f,.15f),
            new Vector2(.31f,.15f)
        };

        return BuildButtonSprite(288, 320, outer, inner, core, false, true);
    }

    static Sprite CreateUtilityButton()
    {
        // Compact square button matching the same iron/bronze family.
        Vector2[] outer =
        {
            new Vector2(.12f,.17f), new Vector2(.055f,.30f), new Vector2(.055f,.70f),
            new Vector2(.12f,.83f), new Vector2(.20f,.86f), new Vector2(.245f,.95f),
            new Vector2(.39f,.94f), new Vector2(.45f,1.00f), new Vector2(.55f,1.00f),
            new Vector2(.61f,.94f), new Vector2(.755f,.95f), new Vector2(.80f,.86f),
            new Vector2(.88f,.83f), new Vector2(.945f,.70f), new Vector2(.945f,.30f),
            new Vector2(.88f,.17f), new Vector2(.80f,.14f), new Vector2(.755f,.05f),
            new Vector2(.61f,.06f), new Vector2(.55f,0f), new Vector2(.45f,0f),
            new Vector2(.39f,.06f), new Vector2(.245f,.05f), new Vector2(.20f,.14f)
        };

        Vector2[] inner =
        {
            new Vector2(.18f,.22f), new Vector2(.115f,.34f), new Vector2(.115f,.66f),
            new Vector2(.18f,.78f), new Vector2(.25f,.81f), new Vector2(.29f,.88f),
            new Vector2(.71f,.88f), new Vector2(.75f,.81f), new Vector2(.82f,.78f),
            new Vector2(.885f,.66f), new Vector2(.885f,.34f), new Vector2(.82f,.22f),
            new Vector2(.75f,.19f), new Vector2(.71f,.12f), new Vector2(.29f,.12f),
            new Vector2(.25f,.19f)
        };

        Vector2[] core =
        {
            new Vector2(.25f,.25f), new Vector2(.19f,.36f), new Vector2(.19f,.64f),
            new Vector2(.25f,.75f), new Vector2(.75f,.75f), new Vector2(.81f,.64f),
            new Vector2(.81f,.36f), new Vector2(.75f,.25f)
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
                            .13f + .47f * glow,
                            .008f + .026f * glow,
                            .012f + .018f * glow,
                            1f);
                    }
                    else
                    {
                        float shade = .040f + .028f * v;
                        c = new Color(shade, shade * .94f, shade * .96f, 1f);
                    }

                    if (DistanceToEdges(p, core) < .010f)
                        c = redCore
                            ? new Color(1f, .35f, .055f, 1f)
                            : new Color(.48f, .31f, .15f, 1f);
                }
                else if (Inside(p, inner))
                {
                    float metal = .075f + .045f * v;
                    c = new Color(metal, metal * .94f, metal * .96f, 1f);

                    if (DistanceToEdges(p, inner) < .010f)
                        c = new Color(.38f, .30f, .25f, 1f);
                }
                else
                {
                    float metal = .15f + .075f * v;
                    c = new Color(metal * 1.02f, metal * .76f, metal * .48f, 1f);

                    if (DistanceToEdges(p, outer) < .010f)
                        c = new Color(.94f, .63f, .29f, 1f);
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
