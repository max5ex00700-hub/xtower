using System;
using UnityEngine;

public static class XTapCharacterDialogue
{
    [Serializable]
    public sealed class TouchLines
    {
        public string[] hair;
        public string[] face;
        public string[] chest;
        public string[] groin;
        public string[] thigh;
        public string[] boot;
    }

    [Serializable]
    public sealed class CharacterEntry
    {
        public int id;
        public string name;
        public string personality;
        public string[] combat;
        public string[] dodge;
        public TouchLines touch;
    }

    [Serializable]
    public sealed class DialogueRoot
    {
        public string version;
        public CharacterEntry[] characters;
    }

    static DialogueRoot data;
    static bool loadAttempted;

    static void EnsureLoaded()
    {
        if (loadAttempted) return;
        loadAttempted = true;

        try
        {
            TextAsset json = Resources.Load<TextAsset>("XTapDialogue/character_dialogues");
            if (json == null || string.IsNullOrWhiteSpace(json.text))
            {
                Debug.LogWarning("X탑 캐릭터 전용 대사 JSON을 찾지 못했습니다.");
                return;
            }

            data = JsonUtility.FromJson<DialogueRoot>(json.text);
        }
        catch (Exception e)
        {
            Debug.LogWarning("X탑 캐릭터 전용 대사 로드 실패: " + e.Message);
            data = null;
        }
    }

    static CharacterEntry Find(int characterId)
    {
        EnsureLoaded();
        if (data == null || data.characters == null) return null;

        int normalized = ((Mathf.Max(1, characterId) - 1) % 10) + 1;
        for (int i = 0; i < data.characters.Length; i++)
        {
            CharacterEntry entry = data.characters[i];
            if (entry != null && entry.id == normalized)
                return entry;
        }
        return null;
    }

    public static string[] Combat(int characterId)
    {
        CharacterEntry entry = Find(characterId);
        return entry != null ? entry.combat : null;
    }

    public static string[] Dodge(int characterId)
    {
        CharacterEntry entry = Find(characterId);
        return entry != null ? entry.dodge : null;
    }

    public static string[] Touch(int characterId, string zone)
    {
        CharacterEntry entry = Find(characterId);
        if (entry == null || entry.touch == null || string.IsNullOrEmpty(zone))
            return null;

        switch (zone)
        {
            case "hair": return entry.touch.hair;
            case "face": return entry.touch.face;
            case "chest": return entry.touch.chest;
            case "groin": return entry.touch.groin;
            case "thigh": return entry.touch.thigh;
            case "boot": return entry.touch.boot;
            default: return null;
        }
    }

    public static string CharacterName(int characterId)
    {
        CharacterEntry entry = Find(characterId);
        return entry != null ? entry.name : "";
    }

    public static string Personality(int characterId)
    {
        CharacterEntry entry = Find(characterId);
        return entry != null ? entry.personality : "";
    }
}
