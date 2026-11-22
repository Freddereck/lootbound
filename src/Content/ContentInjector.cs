using System;
using System.Collections.Generic;
using System.Linq;
using Configurations.ItemsListConfiguration;
using Il2CppInterop.Runtime;
using Services.InventorySystem;
using Services.InventorySystem.StaticData;
using UnityEngine;

namespace MythicAscension.Content
{
    /// Создаёт ItemData новых предметов (клоны базовых) и добавляет их во все списки предметов игры.
    public static class ContentInjector
    {
        /// Все созданные предметы мода: ID → ItemData.
        public static readonly Dictionary<string, ItemData> Items = new();
        public static readonly List<ItemData> MythicItems = new();
        static bool _created;
        static bool _failedLogged;

        static T[] All<T>() where T : UnityEngine.Object
            => Resources.FindObjectsOfTypeAll(Il2CppType.Of<T>()).Select(o => o.Cast<T>()).ToArray();

        public static bool IsModItem(string id) => id != null && Items.ContainsKey(id);

        public static void Tick()
        {
            var lists = All<ItemsListConfig>();
            if (lists.Length == 0) return;

            if (!_created) Create(lists);
            if (!_created) return;

            foreach (var cfg in lists) AddToList(cfg);
            foreach (var m in All<MerchantRewardItemsListConfig>()) AddToMerchant(m);
        }

        static ItemData FindBase(ItemsListConfig[] lists, string id)
        {
            foreach (var cfg in lists)
            {
                try
                {
                    var d = cfg.GetItemDataById(id);
                    if (d != null) return d;
                }
                catch { }
            }
            return null;
        }

        static void Create(ItemsListConfig[] lists)
        {
            string itemsTable = ItemLocalization.ITEMS_TABLE;
            int ok = 0, fail = 0;
            var missing = new List<string>();

            foreach (var def in ItemDefs.ById.Values)
            {
                var baseItem = FindBase(lists, def.Base);
                if (baseItem == null) { missing.Add(def.Base); fail++; continue; }
                try
                {
                    var it = Build(def, baseItem, itemsTable);
                    Verify(def, it);
                    Items[def.Id] = it;
                    if (Mythic.Is(it.Rarity)) MythicItems.Add(it);
                    ok++;
                }
                catch (Exception e)
                {
                    fail++;
                    Plugin.L.LogError($"[Items] {def.Id}: {e}");
                }
            }

            if (missing.Count > 0 && !_failedLogged)
            {
                _failedLogged = true;
                Plugin.L.LogWarning($"[Items] Базовые предметы ещё не загружены или не существуют: {string.Join(", ", missing.Distinct())}");
            }
            // Если почти ничего не нашлось — конфиги ещё не готовы, попробуем позже.
            if (ok == 0) return;

            _created = true;
            Plugin.L.LogInfo($"[Items] Создано {ok} предметов ({MythicItems.Count} мифических), ошибок: {fail}");
        }

        static ItemData Build(ItemDef def, ItemData b, string itemsTable)
        {
            var it = UnityEngine.Object.Instantiate(b);
            it.name = def.Id;
            it.hideFlags |= HideFlags.DontUnloadUnusedAsset;

            var rarity = Mythic.ParseRarity(def.Rarity);
            it.ItemID = def.Id;
            it.Rarity = rarity;
            it.IsLocked = false;
            it.IgnoreBiomeSort = true;
            it.FragileItem = false;
            it.BattlesTillDestroy = 0;
            it.SelfImprovingItem = false;
            it.SelfImprovingStats = new Il2CppSystem.Collections.Generic.List<SelfImprovingStat>();

            // Статы
            var stats = new Il2CppSystem.Collections.Generic.List<ItemStat>();
            foreach (var s in ItemDefs.ParseStats(def.Stats))
                Util.Il2CppRaw.ListAddStruct(stats, MakeStat(s, false));
            it.Stats = stats;

            if (def.Synergy != null)
            {
                var syn = new Il2CppSystem.Collections.Generic.List<ItemStat>();
                foreach (var s in ItemDefs.ParseStats(def.Synergy))
                    Util.Il2CppRaw.ListAddStruct(syn, MakeStat(s, true));
                it.SynergyStats = syn;
            }

            // Слоты усиления и цена
            if (def.EnhanceSlots.HasValue) it.EnhanceSlots = def.EnhanceSlots.Value;
            else if (Mythic.Is(rarity)) it.EnhanceSlots = Plugin.MythicEnhanceSlots.Value;

            it.Price = def.Price ?? EstimatePrice(b, rarity);

            if (def.NoSynergy)
            {
                it.SynergyType = SynergyType.None;
                it.SynergyStats = new Il2CppSystem.Collections.Generic.List<ItemStat>();
            }
            if (def.CantDiscard) it.CantBeDiscarded = true;

            // Тексты
            string nameKey = "MYTH_" + def.Id + "_NAME";
            string descKey = "MYTH_" + def.Id + "_DESC";
            Loc.Add(itemsTable, nameKey, ItemDefs.Text(def.Name, "ru"), ItemDefs.Text(def.Name, "en"));
            Loc.Add(itemsTable, descKey, Effects.BuildDescription(def, true), Effects.BuildDescription(def, false));
            it.LocalizedItemName = Loc.Make(itemsTable, nameKey);
            it.LocalizedItemDescription = Loc.Make(itemsTable, descKey);

            // Иконка: своя PNG или иконка базового предмета
            var icon = SpriteUtil.LoadIconOverride(def.Id, b.ItemIcon);
            if (icon != null) it.ItemIcon = icon;

            return it;
        }

        /// Перечитывает статы из il2cpp-памяти и сверяет с ожидаемыми (ловит ошибки маршалинга структур).
        static void Verify(ItemDef def, ItemData it)
        {
            void Check(Il2CppSystem.Collections.Generic.List<ItemStat> list, List<StatBlocks> expected, bool syn)
            {
                if (list == null) return;
                if (list.Count != expected.Count)
                    throw new Exception($"{(syn ? "synergy" : "stats")}: count {list.Count} != {expected.Count}");
                for (int i = 0; i < expected.Count; i++)
                {
                    var s = list[i];
                    var want = syn ? StatTag.Synergy : StatTag.Base;
                    if (s.StatType != expected[i].Stat || s.Blocks != expected[i].Blocks || s.StatTag != want)
                        throw new Exception($"{(syn ? "synergy" : "stats")}[{i}] = {s.StatType}/{s.Blocks}/{s.StatTag}, ожидалось {expected[i].Stat}/{expected[i].Blocks}/{want}");
                }
            }
            Check(it.Stats, ItemDefs.ParseStats(def.Stats), false);
            if (def.Synergy != null) Check(it.SynergyStats, ItemDefs.ParseStats(def.Synergy), true);
        }

        static ItemStat MakeStat(StatBlocks s, bool synergy)
        {
            var st = new ItemStat();
            st.StatType = s.Stat;
            st.ModifierType = Services.CharacterSystem.Stats.StatModifierType.Flat;
            st.Blocks = s.Blocks;
            st.Value = ItemDefs.ValueOf(s.Stat, s.Blocks, synergy);
            st.StatTag = synergy ? StatTag.Synergy : StatTag.Base;
            st.UniqueID = "";
            return st;
        }

        static int EstimatePrice(ItemData b, RarityItemType target)
        {
            static float F(RarityItemType r) => r switch
            {
                RarityItemType.Common => 0.4f,
                RarityItemType.Rare => 0.6f,
                RarityItemType.Legendary => 0.8f,
                RarityItemType.Divine => 1f,
                _ => Plugin.MythicPriceMultiplier.Value,
            };
            float price = b.Price / Math.Max(0.1f, F(b.Rarity)) * F(target);
            return Math.Max(1, Mathf.RoundToInt(price));
        }

        static void AddToList(ItemsListConfig cfg)
        {
            var dict = cfg.Items;
            if (dict == null) return;
            int added = 0;
            foreach (var it in Items.Values)
            {
                var cat = it.ItemCategory;
                if (!dict.ContainsKey(cat)) continue; // не создаём категории, которых нет в этом списке
                var list = dict[cat];
                if (list.Contains(it)) continue;
                list.Add(it);
                added++;
            }
            if (added > 0) Plugin.V($"[Items] {cfg.name}: +{added}");
        }

        static void AddToMerchant(MerchantRewardItemsListConfig m)
        {
            var list = m.ItemsList;
            if (list == null) return;
            int added = 0;
            foreach (var it in MythicItems)
            {
                if (list.Contains(it)) continue;
                list.Add(it);
                added++;
            }
            if (added > 0) Plugin.V($"[Items] {m.name}: +{added} мификов");
        }

        // ---------- Выбор мифика для подмены награды ----------

        static readonly System.Random Rng = new();

        public static bool Roll(float percent)
            => Plugin.DebugForceMythic.Value || (percent > 0 && Rng.NextDouble() * 100.0 < percent);

        /// Случайный мифик той же категории и для того же героя, что и исходный предмет.
        public static ItemData PickMythicLike(ItemData original)
        {
            if (original == null) return null;
            var pool = MythicItems.Where(m => m.ItemCategory == original.ItemCategory
                                              && m.ValidCharacter == original.ValidCharacter).ToList();
            if (pool.Count == 0)
                pool = MythicItems.Where(m => m.ItemCategory == original.ItemCategory
                                              && m.ValidCharacter == Services.CharacterSystem.CharacterType.Default).ToList();
            if (pool.Count == 0) return null;
            int total = pool.Sum(p => ItemDefs.ById.TryGetValue(p.ItemID, out var d) ? d.Weight : 100);
            int r = Rng.Next(Math.Max(1, total));
            foreach (var p in pool)
            {
                r -= ItemDefs.ById.TryGetValue(p.ItemID, out var d) ? d.Weight : 100;
                if (r < 0) return p;
            }
            return pool[0];
        }
    }
}
