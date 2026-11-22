using System;
using System.Collections.Generic;
using System.Linq;
using MythicAscension.Content;
using Services.CharacterSystem;
using Services.InventorySystem;
using static Services.SaveSystem.SaveDatas.InventorySaveData;

namespace MythicAscension.Mechanics
{
    /// Надетый предмет мода и его разобранные эффекты.
    public class GearPiece
    {
        public StashItem Stash;
        public ItemDef Def;
        public List<Effect> Effects;
        public bool IsMythic;
    }

    /// Сводка всех эффектов персонажа: предметы + бонусы сетов.
    public class GearSummary
    {
        public readonly List<GearPiece> Pieces = new();
        public readonly List<Effect> All = new();               // эффекты предметов + активные бонусы сетов
        public readonly Dictionary<string, int> SetCounts = new();
        public int MythicCount;

        public int Sum(EffectKind k) => All.Where(e => e.Kind == k).Sum(e => e.A);
        public int Max(EffectKind k, int dflt) => All.Where(e => e.Kind == k).Select(e => e.A).DefaultIfEmpty(dflt).Max();
        public bool Has(EffectKind k) => All.Any(e => e.Kind == k);

        /// Мифическая мощь, %.
        public int MythicMight()
        {
            if (MythicCount == 0) return 0;
            int per = Max(EffectKind.MythPer, Plugin.MythicMightPerItem.Value);
            int mult = Max(EffectKind.MythMult, 1);
            return Math.Min(MythicCount * per, Plugin.MythicMightCap.Value) * mult;
        }

        /// Множитель скорости роста для предмета (бонус сета GROWX).
        public int GrowMultiplier(ItemDef def)
        {
            if (string.IsNullOrEmpty(def.Set) || !ItemDefs.SetsById.TryGetValue(def.Set, out var set)) return 1;
            if (!SetCounts.TryGetValue(def.Set, out var n) || n < 4) return 1;
            return Gear.SetEffects(set, 4).Where(e => e.Kind == EffectKind.GrowX).Select(e => e.A).DefaultIfEmpty(1).Max();
        }
    }

    public static class Gear
    {
        static readonly Dictionary<string, List<Effect>> EffectCache = new();
        static readonly Dictionary<string, (List<Effect> b2, List<Effect> b4)> SetCache = new();

        public static List<Effect> EffectsOf(ItemDef d)
        {
            if (!EffectCache.TryGetValue(d.Id, out var l)) EffectCache[d.Id] = l = Content.Effects.ParseAll(d.Effects);
            return l;
        }

        public static List<Effect> SetEffects(SetDef s, int pieces)
        {
            if (!SetCache.TryGetValue(s.Id, out var c))
                SetCache[s.Id] = c = (Content.Effects.ParseAll(s.Bonus2), Content.Effects.ParseAll(s.Bonus4));
            var res = new List<Effect>();
            if (pieces >= 2) res.AddRange(c.b2);
            if (pieces >= 4) res.AddRange(c.b4);
            return res;
        }

        /// Предметы, надетые на персонажа (из сохранения инвентаря).
        public static Il2CppSystem.Collections.Generic.List<StashItem> EquippedList(Character c)
        {
            if (c == null || c.IsEnemy) return null;
            var save = c._inventorySaveData;
            if (save == null) return null;
            var stash = save.GetCharacterStash(c.Type);
            return stash?.CharacterItems;
        }

        public static GearSummary Summarize(Character c)
        {
            var g = new GearSummary();
            Il2CppSystem.Collections.Generic.List<StashItem> list;
            try { list = EquippedList(c); }
            catch { return g; }
            if (list == null) return g;

            for (int i = 0; i < list.Count; i++)
            {
                var si = list[i];
                if (si == null || !ItemDefs.ById.TryGetValue(si.ItemID ?? "", out var def)) continue;
                var p = new GearPiece
                {
                    Stash = si,
                    Def = def,
                    Effects = EffectsOf(def),
                    IsMythic = string.Equals(def.Rarity, "Mythic", StringComparison.OrdinalIgnoreCase) && string.IsNullOrEmpty(def.Expand),
                };
                g.Pieces.Add(p);
                g.All.AddRange(p.Effects);
                if (p.IsMythic) g.MythicCount++;
                if (!string.IsNullOrEmpty(def.Set))
                    g.SetCounts[def.Set] = g.SetCounts.TryGetValue(def.Set, out var n) ? n + 1 : 1;
            }
            foreach (var kv in g.SetCounts)
                if (ItemDefs.SetsById.TryGetValue(kv.Key, out var set))
                    g.All.AddRange(SetEffects(set, kv.Value));
            return g;
        }
    }
}
