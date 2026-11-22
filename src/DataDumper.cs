using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using BepInEx;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;
using Configurations.ItemsListConfiguration;
using Configurations.RewardsConfiguration;
using Services.CharacterSystem;
using Services.CombatSystem.AbilitySystem;
using Services.CombatSystem.AbilitySystem.Actions;
using Services.InventorySystem;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MythicAscension
{
    /// Выгружает данные игры в текстовые файлы, чтобы сбалансировать новые предметы по реальным значениям.
    internal static class DataDumper
    {
        static readonly HashSet<string> Done = new();
        static string Dir => Path.Combine(Paths.BepInExRootPath, "MythicDump");

        static T[] All<T>() where T : Object
            => Resources.FindObjectsOfTypeAll(Il2CppType.Of<T>()).Select(o => o.Cast<T>()).ToArray();

        static string Loc(UnityEngine.Localization.LocalizedString s)
        {
            try { return s == null || s.IsEmpty ? "" : s.GetLocalizedString(); } catch { return "?"; }
        }

        static string TypeName(Il2CppObjectBase o)
            => o == null ? "null" : new Il2CppSystem.Object(o.Pointer).GetIl2CppType().Name;

        static void Write(string name, StringBuilder sb)
        {
            Directory.CreateDirectory(Dir);
            File.WriteAllText(Path.Combine(Dir, name), sb.ToString());
            Plugin.L.LogInfo($"[Dump] {name} ({sb.Length} chars)");
        }

        static string Stats(Il2CppSystem.Collections.Generic.List<ItemStat> list)
        {
            if (list == null || list.Count == 0) return "-";
            var parts = new List<string>();
            for (int i = 0; i < list.Count; i++)
            {
                var s = list[i];
                parts.Add($"{s.StatType}:{s.ModifierType}:{s.Value}" + (s.Blocks != 0 ? $"(b{s.Blocks})" : "") + (s.StatTag != StatTag.Base ? $"[{s.StatTag}]" : ""));
            }
            return string.Join(", ", parts);
        }

        public static void TryDump()
        {
            Safe(DumpItems);
            Safe(DumpAbilities);
            Safe(DumpRewards);
            Safe(DumpMisc);
            Safe(DumpGrids);
        }

        static string Grid(Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<bool> a, int w, int h)
        {
            if (a == null) return "  (null)\n";
            var sb = new StringBuilder();
            sb.AppendLine($"  len={a.Length} (w*h={w * h})");
            for (int y = 0; y < h; y++)
            {
                sb.Append("  ");
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x;
                    sb.Append(i < a.Length ? (a[i] ? '#' : '.') : '?');
                }
                sb.AppendLine();
            }
            return sb.ToString();
        }

        static void DumpGrids()
        {
            var sb = new StringBuilder();
            foreach (var d in All<InventoryStashData>())
            {
                sb.AppendLine($"== InventoryStashData {d.name}: {d.Width}x{d.Height}");
                sb.Append(Grid(d.InventoryLayout, d.Width, d.Height));
            }
            foreach (var c in All<InventoryStashCharacterData>())
                sb.AppendLine($"== CharacterData {c.name}: hero items {c.ItemsStashWidth}x{c.ItemsStashHeight}, companion items {c.CompanionItemsStashWidth}x{c.CompanionItemsStashHeight}, hero cons {c.ConsumablesStashWidth}x{c.ConsumablesStashHeight}, comp cons {c.CompanionConsumablesStashWidth}x{c.CompanionConsumablesStashHeight}");
            foreach (var d in All<Configurations.InventoryConfiguration.InventoryData>())
                sb.AppendLine($"== InventoryData {d.name}: {d.Width}x{d.Height}, equipment {d.EquipmentWidth}x{d.EquipmentHeight}");

            var unlockers = All<ItemData>().Where(i => i.ItemCategory == ItemCategory.Unlocker).ToArray();
            sb.AppendLine($"== Unlocker items: {unlockers.Length}");
            foreach (var u in unlockers) sb.AppendLine($"  {u.ItemID} {u.Layout.Width}x{u.Layout.Height} {u.Rarity} icon={(u.ItemIcon ? u.ItemIcon.name : "-")} {Loc(u.LocalizedItemName)}");

            string runKey = "norun";
            try
            {
                var svc = Core.Container.Resolve<InventoryStashService>();
                var save = svc?._inventorySaveData;
                if (save != null)
                {
                    var data = svc._inventoryStashData;
                    int w = data != null ? data.Width : 0, h = data != null ? data.Height : 0;
                    sb.AppendLine($"== Save StashLayout (main stash {w}x{h}), items={save.StashItems?.Count}");
                    sb.Append(Grid(save.StashLayout, w, h));
                    var cs = save.CharactersStash;
                    if (cs != null)
                        for (int i = 0; i < cs.Count; i++)
                        {
                            var s = cs[i];
                            var items = s.CharacterItems;
                            var pos = new List<string>();
                            if (items != null) for (int j = 0; j < items.Count; j++) pos.Add($"{items[j].ItemID}@{items[j].IndexInStash}");
                            sb.AppendLine($"  char {s.CharacterID}: {string.Join(", ", pos)}");
                        }
                    runKey = $"run{save.StashItems?.Count}_{cs?.Count}";
                }
            }
            catch (Exception e) { sb.AppendLine("Save: " + e.Message); }

            if (!Done.Add("grids_" + runKey + "_" + unlockers.Length)) return;
            Write("grids.txt", sb);
        }

        static void Safe(Action a)
        {
            try { a(); } catch (Exception e) { Plugin.L.LogError($"[Dump] {a.Method.Name}: {e}"); }
        }

        static void DumpItems()
        {
            var items = All<ItemData>();
            if (items.Length == 0) return;
            string key = "items" + items.Length;
            if (!Done.Add(key)) return;

            var sb = new StringBuilder();
            sb.AppendLine("ID\tName\tRarity\tCategory\tSub\tChar\tWxH\tPrice\tEnh\tStats\tSynergy\tSynStats\tFlags\tIcon\tDescription");
            foreach (var it in items.OrderBy(i => (int)i.ItemCategory).ThenBy(i => (int)i.Rarity).ThenBy(i => i.ItemID))
            {
                var flags = new List<string>();
                if (it.FragileItem) flags.Add($"Fragile{it.BattlesTillDestroy}");
                if (it.SelfImprovingItem)
                {
                    var si = new List<string>();
                    var l = it.SelfImprovingStats;
                    if (l != null) for (int i = 0; i < l.Count; i++) si.Add($"{l[i].StatType}+{l[i].Value}");
                    flags.Add("SelfImproving[" + string.Join(",", si) + "]");
                }
                if (it.DoubleAuraStats) flags.Add("DoubleAura");
                if (it.DoubleBlacksmithStats) flags.Add("DoubleBlacksmith");
                if (it.CantBeDiscarded) flags.Add("NoDiscard");
                if (it.IsLocked) flags.Add("Locked");
                if (it.UsageStrategy != null) flags.Add("Usage");
                if (it.StackableCategories != ItemCategory.None) flags.Add("Stack:" + it.StackableCategories);
                string syn = it.SynergyType == SynergyType.None ? "-" :
                    $"{it.SynergyType}/{it.SynergyItemCategory}/{it.SynergyItemSubCategory}/{it.SynergyBeamDirection}/{it.SynergyActivationBorderEdge}";
                sb.AppendLine(string.Join("\t", it.ItemID, Loc(it.LocalizedItemName), it.Rarity, it.ItemCategory, it.ItemSubCategory,
                    it.ValidCharacter, $"{it.Layout.Width}x{it.Layout.Height}", it.Price, it.EnhanceSlots, Stats(it.Stats), syn,
                    Stats(it.SynergyStats), string.Join(",", flags), it.ItemIcon ? it.ItemIcon.name : "-",
                    Loc(it.LocalizedItemDescription).Replace("\n", " ")));
            }
            Write("items.tsv", sb);

            var sb2 = new StringBuilder();
            foreach (var cfg in All<ItemsListConfig>())
            {
                sb2.AppendLine($"== {cfg.name} ({TypeName(cfg)})");
                foreach (var kv in cfg.Items)
                    sb2.AppendLine($"  {kv.Key}: {kv.Value.Count} items");
            }
            Write("item_lists.txt", sb2);
        }

        static void DumpAbilities()
        {
            var abil = All<AbilityConfig>();
            var chars = All<CharacterData>();
            if (abil.Length == 0) return;
            if (!Done.Add("abil" + abil.Length + "_" + chars.Length)) return;

            var owner = new Dictionary<string, string>();
            foreach (var c in chars)
            {
                var l = c.Actions?.AbilityConfigs;
                if (l == null) continue;
                for (int i = 0; i < l.Count; i++) if (l[i] != null) owner[l[i].AbilityID] = c.name;
            }

            var sb = new StringBuilder();
            sb.AppendLine("ID\tOwner\tTitle\tType\tTarget\tMana\tDefault\tBehaviour\tActions\tDescription");
            foreach (var a in abil.OrderBy(a => a.AbilityID))
            {
                var acts = new List<string>();
                var ca = a._castActions;
                if (ca != null)
                    for (int i = 0; i < ca.Length; i++)
                    {
                        var act = ca[i];
                        var tn = TypeName(act);
                        var dd = act?.TryCast<DealDamage>();
                        if (dd != null && dd._damages != null)
                        {
                            var ds = new List<string>();
                            for (int j = 0; j < dd._damages.Count; j++) ds.Add($"{dd._damages[j].DamageStatType}+{dd._damages[j].AdditionalDamage}");
                            tn += "(" + string.Join(",", ds) + ")";
                        }
                        acts.Add(tn);
                    }
                owner.TryGetValue(a.AbilityID ?? "", out var own);
                sb.AppendLine(string.Join("\t", a.AbilityID, own ?? "-", Loc(a.Title), a.ActionType, a.CurrentTarget, a.ManaCost,
                    a.IsDefault, a.BehaviourType, string.Join(";", acts), Loc(a.Description).Replace("\n", " ")));
            }
            Write("abilities.tsv", sb);

            var sb2 = new StringBuilder();
            foreach (var c in chars) sb2.AppendLine($"{c.name}\t{Loc(c.CharacterType)}\t{Loc(c.CharacterDescription).Replace("\n", " ")}");
            Write("characters.tsv", sb2);
        }

        static void DumpRewards()
        {
            var cfgs = All<RewardConfig>();
            if (cfgs.Length == 0 || !Done.Add("rewards" + cfgs.Length)) return;
            var sb = new StringBuilder();
            foreach (var cfg in cfgs)
            {
                sb.AppendLine($"== {cfg.name}");
                foreach (var kv in cfg._rewardChances)
                {
                    var v = kv.Value;
                    sb.AppendLine($"[{kv.Key}] cards={v.CardsRangeAmount} money={v.MoneyForCompletion} idol%={v.ChanceToGetIdol}");
                    var cats = v.ItemCategoryDropChance;
                    if (cats == null) continue;
                    for (int i = 0; i < cats.Count; i++)
                    {
                        var c = cats[i];
                        var rs = new List<string>();
                        var rl = c.ItemRarityDropChance;
                        if (rl != null) for (int j = 0; j < rl.Count; j++) rs.Add($"{rl[j].Rarity}={rl[j].Power}");
                        sb.AppendLine($"    {c.Category} power={c.Power}: {string.Join(" ", rs)}");
                    }
                }
            }
            Write("rewards.txt", sb);
        }

        static void DumpMisc()
        {
            if (Done.Contains("misc")) return;
            var price = All<ItemPriceRulesData>();
            var tip = All<GameTooltipItemsConfig>();
            var rar = All<InventoryStashRarityData>();
            var enh = All<ItemEnhanceSlotsRulesData>();
            var merch = All<MerchantRewardItemsListConfig>();
            if (price.Length + tip.Length + rar.Length == 0) return;
            Done.Add("misc");
            var sb = new StringBuilder();
            foreach (var p in price)
            {
                sb.AppendLine($"== Price {p.name} perCell={p.PricePreCell}");
                foreach (var kv in p.RarityMultiplier) sb.AppendLine($"  rarity {kv.Key} x{kv.Value}");
                foreach (var kv in p.ItemCategoryMultiplier) sb.AppendLine($"  cat {kv.Key} x{kv.Value}");
            }
            foreach (var t in tip)
            {
                sb.AppendLine($"== Tooltip {t.name}");
                foreach (var kv in t.RarityTooltipData) sb.AppendLine($"  {kv.Key} color={kv.Value.RarityColor} body={(kv.Value.TooltipBody ? kv.Value.TooltipBody.name : "-")}");
            }
            foreach (var r in rar)
            {
                sb.AppendLine($"== RaritySprites {r.name}");
                foreach (var rs in r.RaritySprites) sb.AppendLine($"  {rs.Rarity} {(rs.Sprite ? rs.Sprite.name : "-")}");
            }
            foreach (var e in enh)
            {
                sb.AppendLine($"== Enhance {e.name}");
                foreach (var kv in e.EnhanceSlotsData)
                {
                    var parts = new List<string>();
                    for (int i = 0; i < kv.Value.Count; i++) parts.Add($"{kv.Value[i].ItemRarity}={kv.Value[i].EnhanceSlots}");
                    sb.AppendLine($"  {kv.Key}: {string.Join(" ", parts)}");
                }
            }
            foreach (var m in merch)
            {
                sb.AppendLine($"== Merchant {m.name}");
                foreach (var kv in m._rarityPrice) sb.AppendLine($"  {kv.Key} price={kv.Value}");
            }
            try
            {
                foreach (RarityItemType r in Enum.GetValues(typeof(RarityItemType)))
                    sb.AppendLine($"RarityKey {r} = {Services.InventorySystem.StaticData.ItemLocalization.GetRarityKey(r)}");
            }
            catch (Exception ex) { sb.AppendLine("RarityKey error " + ex.Message); }
            Write("misc.txt", sb);
        }
    }
}
