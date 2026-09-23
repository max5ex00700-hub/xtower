using System;
using UnityEngine;
using UnityEngine.UI;

public static class XTapUiSkin
{
    static bool attempted;
    static Texture2D atlas;

    public static Sprite Panel { get; private set; }
    public static Sprite Slot { get; private set; }
    public static Sprite TabNormal { get; private set; }
    public static Sprite TabSelected { get; private set; }
    public static Sprite ButtonNeutral { get; private set; }
    public static Sprite ButtonPrimary { get; private set; }
    public static Sprite ListRow { get; private set; }
    public static Sprite StatusBar { get; private set; }

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

        try
        {
            TextAsset encoded = Resources.Load<TextAsset>("XTapBlacksmithUI/atlas");
            if (encoded == null || string.IsNullOrWhiteSpace(encoded.text))
                return;

            byte[] bytes = Convert.FromBase64String(encoded.text.Trim());
            atlas = new Texture2D(2, 2, TextureFormat.RGB24, false);

            if (!atlas.LoadImage(bytes, false))
            {
                UnityEngine.Object.Destroy(atlas);
                atlas = null;
                return;
            }

            atlas.wrapMode = TextureWrapMode.Clamp;
            atlas.filterMode = FilterMode.Bilinear;

            Panel = MakeSprite(144, 0, 112, 84, new Vector4(18f, 18f, 18f, 18f));
            Slot = MakeSprite(144, 84, 112, 112, new Vector4(18f, 18f, 18f, 18f));
            TabNormal = MakeSprite(0, 256, 128, 43, new Vector4(22f, 10f, 22f, 10f));
            TabSelected = MakeSprite(128, 256, 128, 43, new Vector4(22f, 10f, 22f, 10f));
            ButtonNeutral = MakeSprite(0, 299, 128, 43, new Vector4(22f, 10f, 22f, 10f));
            ButtonPrimary = MakeSprite(128, 299, 128, 43, new Vector4(22f, 10f, 22f, 10f));
            ListRow = MakeSprite(0, 342, 256, 64, new Vector4(26f, 12f, 26f, 12f));
            StatusBar = MakeSprite(0, 406, 256, 64, new Vector4(26f, 12f, 26f, 12f));
        }
        catch (Exception e)
        {
            Debug.LogWarning("X탑 공용 UI 스킨 로드 실패: " + e.Message);
        }
    }

    static Sprite MakeSprite(int x, int yFromTop, int width, int height, Vector4 border)
    {
        if (atlas == null) return null;

        int y = atlas.height - yFromTop - height;
        Rect rect = new Rect(x, y, width, height);

        return Sprite.Create(
            atlas,
            rect,
            new Vector2(.5f, .5f),
            100f,
            0,
            SpriteMeshType.FullRect,
            border
        );
    }

    public static void Apply(Image image, Sprite sprite, Color? tint = null)
    {
        if (image == null || sprite == null) return;

        image.sprite = sprite;
        image.type = Image.Type.Sliced;
        image.color = tint ?? Color.white;
    }

    public static void Apply(RectTransform rect, Sprite sprite, Color? tint = null)
    {
        if (rect == null) return;
        Apply(rect.GetComponent<Image>(), sprite, tint);
    }

    public static void Apply(Button button, Sprite sprite, Color? tint = null)
    {
        if (button == null) return;
        Apply(button.targetGraphic as Image, sprite, tint);
    }
}
