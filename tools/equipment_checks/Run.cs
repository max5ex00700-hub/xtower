using System;
using System.Collections.Generic;

// Only Unity preference state/math are adapted. All tested stat member bodies
// and the serialized item model come directly from the production source.
namespace UnityEngine
{
    public static class Mathf
    {
        public static int Clamp(int v, int lo, int hi) { return Math.Min(hi, Math.Max(lo, v)); }
        public static int Max(int a, int b) { return Math.Max(a, b); }
    }
    public static class PlayerPrefs
    {
        public static readonly Dictionary<string, int> Values = new Dictionary<string, int>();
        public static int GetInt(string key, int fallback) { return Values.TryGetValue(key, out int v) ? v : fallback; }
    }
}

public sealed partial class BattleProbe
{
    public XTapInventory inventory;
    public double Attack { get { return CurrentPlayerAttack(); } }
    public double Defense { get { return CurrentPlayerDefense(); } }
    public double Hp { get { return CurrentPlayerMaxHp(); } }
}

static class Run
{
    static int checks;
    static void Equal(double actual, double expected, string label)
    {
        checks++;
        if (double.IsNaN(actual) || double.IsInfinity(actual) ||
            Math.Abs(actual - expected) > Math.Max(1e-8, Math.Abs(expected) * 1e-12))
            throw new Exception(label + ": expected " + expected + ", got " + actual);
    }
    static void Stats(XTapInventory bag, double attack, double defense, double hp, string label)
    {
        Equal(bag.EquippedAttack, attack, label + " attack");
        Equal(bag.EquippedDefense, defense, label + " defense");
        Equal(bag.EquippedHp, hp, label + " hp");
    }
    static XTapGearBlockData Block(int owner, int cells, double attack, double defense, double hp)
    {
        return new XTapGearBlockData { exclusive = owner > 0, characterId = owner,
            bagOwnerCharacterId = owner, cellCount = cells, attack = attack, defense = defense,
            hp = hp, location = XTapGearBlockData.LocationBag, gridX = 0, gridY = 0 };
    }
    static void Captured(int id, bool value)
    {
        UnityEngine.PlayerPrefs.Values["xtap_captured_char_" + id] = value ? 1 : 0;
    }
    static void Main()
    {
        var bag = new XTapInventory();
        var battle = new BattleProbe { inventory = bag };
        Equal(bag.ExclusiveEquipmentBonusPercent, 0, "empty bonus");
        Equal(bag.ExclusiveEquipmentMultiplier, 1, "empty multiplier");
        Stats(bag, 0, 0, 0, "empty");
        Equal(battle.Attack, 5, "base attack");
        Equal(battle.Defense, 1, "base defense");
        Equal(battle.Hp, 100, "base hp");

        var player = Block(0, 24, 1000, 200, 3000);
        var a = Block(1, 12, 250, 50, 500);
        var b = Block(1, 12, 250, 50, 500);
        bag.items.AddRange(new[] { player, a, b });
        Stats(bag, 1000, 200, 3000, "uncaptured bag excluded");
        Equal(bag.ExclusiveEquipmentBonusPercent, 0, "normal player bag has no cell bonus");
        Captured(1, true);
        Equal(bag.ExclusiveEquipmentBonusPercent, 24, "two twelve-cell blocks");
        Stats(bag, 1860, 372, 4960, "player plus character times 1.24");
        Equal(battle.Attack, 1865, "bonus excludes base attack");
        Equal(battle.Defense, 373, "bonus excludes base defense");
        Equal(battle.Hp, 5060, "bonus excludes base hp");

        player.descriptorCount = 1;
        Equal(battle.Attack, 3730, "descriptor applied once after base and equipment");
        Equal(battle.Defense, 746, "descriptor defense");
        Equal(battle.Hp, 10120, "descriptor hp");
        double at, de, hp, multiplier;
        bag.GetBagDisplayStats(0, out at, out de, out hp, out multiplier);
        Equal(at, 2480, "player bag display includes both effects");
        Equal(de, 496, "player bag defense display");
        Equal(hp, 7440, "player bag hp display");
        Equal(multiplier, 2, "descriptor output is not mixed with cell multiplier");
        double playerDisplay = at;
        bag.GetBagDisplayStats(1, out at, out de, out hp, out multiplier);
        Equal(at, 1240, "character bag display");
        Equal(de, 248, "character defense display");
        Equal(hp, 2480, "character hp display");
        Equal(playerDisplay + at + 10, battle.Attack, "display contributions reconcile with battle");
        Equal(bag.GetEquippedAttack(1), 500, "per-bag raw getter remains raw");

        var c = Block(2, 12, 150, 30, 400);
        var d = Block(2, 12, 150, 30, 400);
        Captured(2, true);
        bag.items.AddRange(new[] { c, d });
        Equal(bag.ExclusiveEquipmentBonusPercent, 48, "two full character bags add percentages");
        Stats(bag, 2664, 532.8, 7104, "48 percent is additive not compounded");
        b.location = XTapGearBlockData.LocationHeld;
        Equal(bag.ExclusiveEquipmentBonusPercent, 36, "unequip updates bonus immediately");
        Stats(bag, 2108, 421.6, 5848, "held item removed from base and bonus");
        d.location = XTapGearBlockData.LocationGround;
        Equal(bag.ExclusiveEquipmentBonusPercent, 24, "ground item excluded");
        Stats(bag, 1736, 347.2, 4836, "ground stat exclusion");
        Captured(1, false);
        Equal(bag.ExclusiveEquipmentBonusPercent, 12, "capture loss removes character contribution");
        bag.GetBagDisplayStats(1, out at, out de, out hp, out multiplier);
        Equal(at, 0, "uncaptured display has no stat contribution");
        Captured(1, true);
        bag.items.Remove(c);
        Equal(bag.ExclusiveEquipmentBonusPercent, 12, "forge removal updates bonus");

        // Invalid owner/type combinations cannot sneak a global bonus or stats in.
        var invalid = Block(2, 12, 999999, 999999, 999999);
        invalid.bagOwnerCharacterId = 1;
        invalid.descriptorCount = 3;
        bag.items.Add(invalid);
        Stats(bag, 1400, 280, 3920, "wrong character owner excluded");
        Equal(bag.DescriptorSetMultiplier, 2, "wrong owner descriptor excluded");
        invalid.bagOwnerCharacterId = 0;
        Equal(bag.ExclusiveEquipmentBonusPercent, 12, "exclusive in player bag excluded");
        invalid.exclusive = false;
        invalid.bagOwnerCharacterId = 2;
        Equal(bag.ExclusiveEquipmentBonusPercent, 12, "ordinary in character bag excluded");
        Equal(bag.EquippedAttack, 1400, "ordinary in character bag cannot contribute");
        invalid.exclusive = true;
        invalid.bagOwnerCharacterId = 12;
        Equal(bag.ExclusiveEquipmentBonusPercent, 12, "out-of-range owner excluded");

        // All character families and real generated sizes obey the same rule.
        foreach (int owner in new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 })
        {
            Captured(owner, true);
            foreach (int cells in new[] { 1, 2, 3, 4, 5, 6, 9, 12 })
            {
                var sized = new XTapInventory();
                sized.items.Add(Block(0, 24, 100, 100, 100));
                sized.items.Add(Block(owner, cells, 0, 0, 0));
                Equal(sized.ExclusiveEquipmentBonusPercent, cells, "cell count not block count");
                Stats(sized, 100 + cells, 100 + cells, 100 + cells, "each occupied cell adds one percent");
            }
        }
        var singles = new XTapInventory();
        singles.items.Add(Block(0, 24, 1000, 200, 3000));
        for (int i = 0; i < 24; i++) singles.items.Add(Block(1, 1, 0, 0, 0));
        Stats(singles, 1240, 248, 3720, "24 one-cell blocks also grant 24 percent");
        singles.items.Add(Block(1, 1, 0, 0, 0));
        Equal(singles.ExclusiveEquipmentBonusPercent, 25, "equipped affinity expansion cell counts");

        var forge = new XTapInventory();
        var enhanced = Block(1, 12, 250, 50, 500);
        forge.items.Add(enhanced);
        forge.EnsureEnhancementBaseStats(enhanced);
        enhanced.enhanceLevel = 10;
        forge.RecalculateEnhancedStats(enhanced);
        forge.MergeSynthesisStats(enhanced, 50, 20, 100);
        Equal(forge.ExclusiveEquipmentBonusPercent, 12, "enhancement/synthesis do not multiply cell bonus");
        Stats(forge, 616, 134.4, 1232, "bonus follows enhancement and synthesis sum");
        for (int i = 0; i < 5; i++)
        {
            forge.GetBagDisplayStats(1, out at, out de, out hp, out multiplier);
            Equal(at, 616, "repeated display cannot compound bonus");
        }
        Equal(enhanced.attack, 550, "saved effective attack unchanged by global bonus");
        Equal(enhanced.baseAttack, 275, "existing synthesis rebase unchanged by global bonus");
        Equal(enhanced.synthesisAttack, 0, "existing cleared synthesis bucket unchanged");

        var large = new XTapInventory();
        large.items.Add(Block(0, 1, double.MaxValue, double.MaxValue, double.MaxValue));
        large.items.Add(Block(1, 12, 1, 1, 1));
        Stats(large, double.MaxValue, double.MaxValue, double.MaxValue, "overflow saturates");
        Equal(XTapInventory.ExclusiveBlockBonusPercent(null), 0, "null item");
        Equal(XTapInventory.ExclusiveBlockBonusPercent(Block(1, -5, 0, 0, 0)), 0, "negative cells");
        Console.WriteLine("PASS: " + checks + " actual inventory/battle stat adapter checks.");
        Console.WriteLine("Unity import/API compilation, layout, save serialization and APK execution remain untested.");
    }
}
