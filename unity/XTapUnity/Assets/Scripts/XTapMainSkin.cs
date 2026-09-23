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
            return atlas != null;
        }
    }

    public static void EnsureLoaded()
    {
        if (attempted) return;
        attempted = true;

        atlas = Resources.Load<Texture2D>("XTapMainUI/main_atlas");
        if (atlas == null)
        {
            Debug.LogWarning("X탑 메인 UI 아틀라스를 불러오지 못했습니다.");
            return;
        }

        atlas.wrapMode = TextureWrapMode.Clamp;
        atlas.filterMode = FilterMode.Bilinear;

        // Atlas: 512x512. Rect Y uses Unity bottom-left coordinates.
        FloorPanel   = MakeSprite(0,   429, 256, 83,  new Vector4(34f, 22f, 34f, 22f));
        FightButton  = MakeSprite(256, 429, 256, 83,  new Vector4(34f, 22f, 34f, 22f));
        UtilityButton= MakeSprite(0,   346, 256, 83,  new Vector4(34f, 22f, 34f, 22f));
        BottomRail   = MakeSprite(256, 346, 256, 83,  new Vector4(34f, 22f, 34f, 22f));
        PlayerPanel  = MakeSprite(0,   154, 128, 192, new Vector4(24f, 34f, 24f, 34f));
        NavButton    = MakeSprite(128, 154, 128, 192, new Vector4(24f, 34f, 24f, 34f));
    }

    static Sprite MakeSprite(int x, int y, int width, int height, Vector4 border)
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
