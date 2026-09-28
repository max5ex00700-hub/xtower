using System;
using System.Collections.Generic;
using UnityEngine;

// Original vector-authored skins. Only three immutable textures are cached.
public static class XTapCombatCueSkin
{
    public static readonly string[] RequiredNames = { "hit_gold", "hit_followup", "shield_crystal" };
    static readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();

    public static Sprite Get(string name)
    {
        Sprite cached;
        if (sprites.TryGetValue(name, out cached) && cached != null) return cached;

        TextAsset source = Resources.Load<TextAsset>("XTapCombatUI/" + name);
        if (source == null)
            throw new InvalidOperationException("X탑 전투 표적 에셋 누락: " + name);

        Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (!texture.LoadImage(source.bytes, true) || texture.width != 512 || texture.height != 512)
        {
            UnityEngine.Object.Destroy(texture);
            throw new InvalidOperationException("X탑 전투 표적 이미지 디코딩/해상도 오류: " + name);
        }
        texture.name = "XTapCombatCue_" + name;
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;
        Sprite sprite = Sprite.Create(texture, new Rect(0, 0, 512, 512),
            new Vector2(.5f, .5f), 100f, 0, SpriteMeshType.FullRect);
        sprite.name = texture.name;
        sprites[name] = sprite;
        return sprite;
    }
}
