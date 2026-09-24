using System;
using UnityEngine;

public static class XTapMainSkin
{
    static bool attempted;
    static Texture2D atlas;

    public static Sprite FloorPanel { get; private set; }
    public static Sprite FightButton { get; private set; }
    public static Sprite OptionButton { get; private set; }
    public static Sprite CodexButton { get; private set; }
    public static Sprite UtilityButton { get; private set; }
    public static Sprite InfoTabButton { get; private set; }
    public static Sprite BottomRail { get; private set; }
    public static Sprite PlayerPanel { get; private set; }
    public static Sprite NavButton { get; private set; }
    public static Sprite NavPrevButton { get; private set; }
    public static Sprite NavBagButton { get; private set; }
    public static Sprite NavJailButton { get; private set; }
    public static Sprite NavForgeButton { get; private set; }
    public static Sprite NavNextButton { get; private set; }

    public static bool Ready
    {
        get
        {
            EnsureLoaded();
            return FightButton != null &&
                   OptionButton != null &&
                   CodexButton != null &&
                   InfoTabButton != null &&
                   NavPrevButton != null &&
                   NavBagButton != null &&
                   NavJailButton != null &&
                   NavForgeButton != null &&
                   NavNextButton != null;
        }
    }

    public static void EnsureLoaded()
    {
        if (attempted) return;
        attempted = true;

        LoadLegacyPanelAtlas();

        // 11.10: use the actual button artwork extracted from the approved
        // reference image. Procedural art is fallback only.
        FightButton = LoadReferenceButtonSprite("XTapMainUI/ref_fight_runtime", "XTapReferenceFight") ?? CreateFightButton();
        OptionButton = LoadReferenceButtonSprite("XTapMainUI/ref_option_runtime", "XTapReferenceOption") ?? CreateUtilityButton();
        CodexButton = LoadReferenceButtonSprite("XTapMainUI/ref_codex_runtime", "XTapReferenceCodex") ?? OptionButton;
        InfoTabButton = LoadReferenceButtonSprite("XTapMainUI/ref_tab_runtime", "XTapReferenceInfoTab") ?? CreateInfoTabButton();

        NavPrevButton = LoadReferenceButtonSprite("XTapMainUI/ref_nav_prev_runtime", "XTapReferenceNavPrev") ?? CreateNavButton();
        NavBagButton = LoadReferenceButtonSprite("XTapMainUI/ref_nav_bag_runtime", "XTapReferenceNavBag") ?? CreateNavButton();
        NavJailButton = LoadReferenceButtonSprite("XTapMainUI/ref_nav_jail_runtime", "XTapReferenceNavJail") ?? CreateNavButton();
        NavForgeButton = LoadReferenceButtonSprite("XTapMainUI/ref_nav_forge_runtime", "XTapReferenceNavForge") ?? CreateNavButton();
        NavNextButton = LoadReferenceButtonSprite("XTapMainUI/ref_nav_next_runtime", "XTapReferenceNavNext") ?? CreateNavButton();

        // Compatibility aliases for older callers/fallback paths.
        UtilityButton = OptionButton;
        NavButton = NavBagButton;
    }

    static Sprite LoadReferenceButtonSprite(string resourcePath, string spriteName)
    {
        try
        {
            TextAsset encoded = Resources.Load<TextAsset>(resourcePath);
            if (encoded == null || string.IsNullOrWhiteSpace(encoded.text))
                return null;

            byte[] bytes = Convert.FromBase64String(encoded.text.Trim());
            Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            texture.name = spriteName + "Texture";
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;

            if (!texture.LoadImage(bytes, false))
            {
                UnityEngine.Object.Destroy(texture);
                return null;
            }

            return Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(.5f, .5f),
                100f,
                0,
                SpriteMeshType.FullRect
            );
        }
        catch (Exception e)
        {
            Debug.LogWarning("X탑 기준 이미지 버튼 로드 실패: " + resourcePath + " / " + e.Message);
            return null;
        }
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
        // One-piece gothic battle plaque. Keep every decorative edge connected
        // to the body so scaling on portrait devices never looks fragmented.
        Vector2[] outer =
        {
            new Vector2(.020f,.50f), new Vector2(.060f,.68f), new Vector2(.130f,.78f),
            new Vector2(.220f,.86f), new Vector2(.420f,.86f), new Vector2(.500f,.950f),
            new Vector2(.580f,.86f), new Vector2(.780f,.86f), new Vector2(.870f,.78f),
            new Vector2(.940f,.68f), new Vector2(.980f,.50f), new Vector2(.940f,.32f),
            new Vector2(.870f,.22f), new Vector2(.780f,.14f), new Vector2(.580f,.14f),
            new Vector2(.500f,.050f), new Vector2(.420f,.14f), new Vector2(.220f,.14f),
            new Vector2(.130f,.22f), new Vector2(.060f,.32f)
        };

        Vector2[] inner =
        {
            new Vector2(.075f,.50f), new Vector2(.105f,.64f), new Vector2(.175f,.72f),
            new Vector2(.255f,.79f), new Vector2(.435f,.79f), new Vector2(.500f,.855f),
            new Vector2(.565f,.79f), new Vector2(.745f,.79f), new Vector2(.825f,.72f),
            new Vector2(.895f,.64f), new Vector2(.925f,.50f), new Vector2(.895f,.36f),
            new Vector2(.825f,.28f), new Vector2(.745f,.21f), new Vector2(.565f,.21f),
            new Vector2(.500f,.145f), new Vector2(.435f,.21f), new Vector2(.255f,.21f),
            new Vector2(.175f,.28f), new Vector2(.105f,.36f)
        };

        Vector2[] core =
        {
            new Vector2(.145f,.50f), new Vector2(.185f,.63f), new Vector2(.250f,.69f),
            new Vector2(.750f,.69f), new Vector2(.815f,.63f), new Vector2(.855f,.50f),
            new Vector2(.815f,.37f), new Vector2(.750f,.31f), new Vector2(.250f,.31f),
            new Vector2(.185f,.37f)
        };

        return BuildButtonSprite(520, 285, outer, inner, core, true, true);
    }

    static Sprite CreateNavButton()
    {
        // Tall one-piece black-iron menu plate.
        Vector2[] outer =
        {
            new Vector2(.10f,.10f), new Vector2(.055f,.23f), new Vector2(.055f,.77f),
            new Vector2(.12f,.87f), new Vector2(.34f,.90f), new Vector2(.50f,.975f),
            new Vector2(.66f,.90f), new Vector2(.88f,.87f), new Vector2(.945f,.77f),
            new Vector2(.945f,.23f), new Vector2(.90f,.10f), new Vector2(.66f,.075f),
            new Vector2(.50f,.025f), new Vector2(.34f,.075f)
        };

        Vector2[] inner =
        {
            new Vector2(.16f,.16f), new Vector2(.115f,.28f), new Vector2(.115f,.72f),
            new Vector2(.18f,.80f), new Vector2(.37f,.83f), new Vector2(.50f,.89f),
            new Vector2(.63f,.83f), new Vector2(.82f,.80f), new Vector2(.885f,.72f),
            new Vector2(.885f,.28f), new Vector2(.84f,.16f), new Vector2(.63f,.14f),
            new Vector2(.50f,.10f), new Vector2(.37f,.14f)
        };

        Vector2[] core =
        {
            new Vector2(.22f,.22f), new Vector2(.18f,.32f), new Vector2(.18f,.68f),
            new Vector2(.24f,.76f), new Vector2(.76f,.76f), new Vector2(.82f,.68f),
            new Vector2(.82f,.32f), new Vector2(.78f,.22f), new Vector2(.68f,.17f),
            new Vector2(.32f,.17f)
        };

        return BuildButtonSprite(256, 352, outer, inner, core, false, true);
    }

    static Sprite CreateUtilityButton()
    {
        // Compact connected square plate for options/codex.
        Vector2[] outer =
        {
            new Vector2(.16f,.08f), new Vector2(.07f,.20f), new Vector2(.04f,.36f),
            new Vector2(.04f,.64f), new Vector2(.07f,.80f), new Vector2(.16f,.92f),
            new Vector2(.36f,.96f), new Vector2(.50f,1.00f), new Vector2(.64f,.96f),
            new Vector2(.84f,.92f), new Vector2(.93f,.80f), new Vector2(.96f,.64f),
            new Vector2(.96f,.36f), new Vector2(.93f,.20f), new Vector2(.84f,.08f),
            new Vector2(.64f,.04f), new Vector2(.50f,0f), new Vector2(.36f,.04f)
        };

        Vector2[] inner =
        {
            new Vector2(.21f,.15f), new Vector2(.13f,.25f), new Vector2(.11f,.39f),
            new Vector2(.11f,.61f), new Vector2(.13f,.75f), new Vector2(.21f,.85f),
            new Vector2(.39f,.89f), new Vector2(.50f,.92f), new Vector2(.61f,.89f),
            new Vector2(.79f,.85f), new Vector2(.87f,.75f), new Vector2(.89f,.61f),
            new Vector2(.89f,.39f), new Vector2(.87f,.25f), new Vector2(.79f,.15f),
            new Vector2(.61f,.11f), new Vector2(.50f,.08f), new Vector2(.39f,.11f)
        };

        Vector2[] core =
        {
            new Vector2(.27f,.23f), new Vector2(.20f,.34f), new Vector2(.20f,.66f),
            new Vector2(.27f,.77f), new Vector2(.73f,.77f), new Vector2(.80f,.66f),
            new Vector2(.80f,.34f), new Vector2(.73f,.23f)
        };

        return BuildButtonSprite(256, 256, outer, inner, core, false, true);
    }

    static Sprite CreateInfoTabButton()
    {
        // Dedicated narrow vertical plate. Never stretch the square utility art
        // into the drawer tab because that tears the ornament visually.
        Vector2[] outer =
        {
            new Vector2(.24f,.02f), new Vector2(.76f,.02f), new Vector2(.91f,.08f),
            new Vector2(.97f,.18f), new Vector2(.97f,.82f), new Vector2(.91f,.92f),
            new Vector2(.76f,.98f), new Vector2(.24f,.98f), new Vector2(.09f,.92f),
            new Vector2(.03f,.82f), new Vector2(.03f,.18f), new Vector2(.09f,.08f)
        };

        Vector2[] inner =
        {
            new Vector2(.30f,.08f), new Vector2(.70f,.08f), new Vector2(.82f,.13f),
            new Vector2(.88f,.22f), new Vector2(.88f,.78f), new Vector2(.82f,.87f),
            new Vector2(.70f,.92f), new Vector2(.30f,.92f), new Vector2(.18f,.87f),
            new Vector2(.12f,.78f), new Vector2(.12f,.22f), new Vector2(.18f,.13f)
        };

        Vector2[] core =
        {
            new Vector2(.34f,.14f), new Vector2(.66f,.14f), new Vector2(.77f,.21f),
            new Vector2(.80f,.79f), new Vector2(.66f,.86f), new Vector2(.34f,.86f),
            new Vector2(.20f,.79f), new Vector2(.23f,.21f)
        };

        return BuildButtonSprite(96, 240, outer, inner, core, false, true);
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

        if (redCore)
            DrawFightCrest(texture);

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

    static void DrawFightCrest(Texture2D texture)
    {
        int width = texture.width;
        int height = texture.height;

        Vector2 swordA0 = new Vector2(.385f, .855f);
        Vector2 swordA1 = new Vector2(.555f, .585f);
        Vector2 swordB0 = new Vector2(.615f, .855f);
        Vector2 swordB1 = new Vector2(.445f, .585f);

        for (int y = 0; y < height; y++)
        {
            float v = (y + .5f) / height;

            for (int x = 0; x < width; x++)
            {
                float u = (x + .5f) / width;
                Vector2 p = new Vector2(u, v);

                float da = DistanceToSegment(p, swordA0, swordA1);
                float db = DistanceToSegment(p, swordB0, swordB1);
                float d = Mathf.Min(da, db);

                if (d < .016f)
                {
                    Color blade = d < .004f
                        ? new Color(.98f, .96f, .90f, 1f)
                        : (d < .010f
                            ? new Color(.74f, .72f, .69f, 1f)
                            : new Color(.18f, .14f, .13f, 1f));
                    texture.SetPixel(x, y, blade);
                }

                float gem = Mathf.Abs(u - .5f) / .028f + Mathf.Abs(v - .895f) / .050f;
                if (gem <= 1f)
                {
                    texture.SetPixel(
                        x,
                        y,
                        gem < .45f
                            ? new Color(1f, .18f, .045f, 1f)
                            : new Color(.46f, .018f, .015f, 1f)
                    );
                }
            }
        }
    }

    static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float lengthSq = Mathf.Max(.000001f, Vector2.Dot(ab, ab));
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / lengthSq);
        return Vector2.Distance(p, a + ab * t);
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
