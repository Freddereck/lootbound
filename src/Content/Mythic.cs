using System;
using System.Collections.Generic;
using System.Linq;
using Configurations.ItemsListConfiguration;
using Il2CppInterop.Runtime;
using Services.InventorySystem;
using UnityEngine;

namespace MythicAscension.Content
{
    /// Пятая редкость: значение перечисления, цвета, спрайты и регистрация в таблицах игры.
    public static class Mythic
    {
        public const RarityItemType Rarity = (RarityItemType)4;
        public const string RarityKey = "RARITY_MYTHIC";

        public static readonly Color TooltipColor = new(1f, 0.42f, 0.22f, 1f);   // красно-оранжевый
        public static readonly Color GlowColor = new(1f, 0.30f, 0.12f, 1f);
        public static readonly Color NameColor = new(1f, 0.45f, 0.25f, 1f);
        public static readonly Color OutlineColor = new(1f, 0.62f, 0.18f, 1f);   // золотой контур
        /// Множитель цвета для золотых Divine-рамок → красно-оранжевые.
        public static readonly Color CellTint = new(1f, 0.42f, 0.42f, 1f);

        /// Золото Divine (~40°) → красно-оранжевый (~10°).
        public const float HueShift = -30f;

        public static bool Is(RarityItemType r) => r == Rarity;

        public static RarityItemType ParseRarity(string s)
        {
            if (string.Equals(s, "Mythic", StringComparison.OrdinalIgnoreCase)) return Rarity;
            return Enum.TryParse<RarityItemType>(s, true, out var r) ? r : RarityItemType.Common;
        }

        public static Sprite Recolor(Sprite divine) => SpriteUtil.HueShift(divine, HueShift, 1.2f, 1.0f);

        static readonly HashSet<int> Registered = new();

        static T[] All<T>() where T : UnityEngine.Object
            => Resources.FindObjectsOfTypeAll(Il2CppType.Of<T>()).Select(o => o.Cast<T>()).ToArray();

        static bool Once(UnityEngine.Object o) => o != null && Registered.Add(o.GetInstanceID());

        static Sprite _cellSprite;

        /// Красная рамка ячейки: перекрашенная копия Divine-спрайта из InventoryStashRarityData (кэшируется).
        public static Sprite CellSprite()
        {
            if (_cellSprite != null) return _cellSprite;
            foreach (var data in All<InventoryStashRarityData>())
            {
                var arr = data.RaritySprites;
                if (arr == null) continue;
                for (int i = 0; i < arr.Length; i++)
                    if (arr[i].Rarity == RarityItemType.Divine && arr[i].Sprite != null)
                        return _cellSprite = Recolor(arr[i].Sprite);
            }
            return null;
        }


        /// Добавляет Mythic во все загруженные конфиги, которые держат словари по редкости.
        public static void RegisterInConfigs()
        {

            foreach (var t in All<GameTooltipItemsConfig>())
            {
                if (!Once(t)) continue;
                var d = t.RarityTooltipData;
                if (d != null && !d.ContainsKey(Rarity) && d.ContainsKey(RarityItemType.Divine))
                {
                    var div = d[RarityItemType.Divine];
                    var tip = new GameTooltipItemsConfig.RarityTooltip();
                    tip.TooltipBody = Recolor(div.TooltipBody);
                    tip.RarityColor = TooltipColor;
                    Util.Il2CppRaw.DictAddEnumStruct(d, (int)Rarity, tip);
                    Plugin.V("[Mythic] Tooltip config: +Mythic");
                }
            }

            foreach (var p in All<ItemPriceRulesData>())
            {
                if (!Once(p)) continue;
                var d = p.RarityMultiplier;
                if (d != null && !d.ContainsKey(Rarity) && d.ContainsKey(RarityItemType.Divine))
                    Util.Il2CppRaw.DictAddEnumValue(d, (int)Rarity, d[RarityItemType.Divine] * Plugin.MythicPriceMultiplier.Value);
            }

            foreach (var e in All<ItemEnhanceSlotsRulesData>())
            {
                if (!Once(e)) continue;
                var d = e.EnhanceSlotsData;
                if (d == null) continue;
                foreach (var kv in d)
                {
                    var list = kv.Value;
                    bool has = false;
                    for (int i = 0; i < list.Count; i++) if (list[i].ItemRarity == Rarity) has = true;
                    if (!has)
                        Util.Il2CppRaw.ListAddValue(list, new ItemEnhanceSlotsRulesData.EnhanceRarityData { ItemRarity = Rarity, EnhanceSlots = Plugin.MythicEnhanceSlots.Value });
                }
            }

            foreach (var m in All<MerchantRewardItemsListConfig>())
            {
                var d = m._rarityPrice;
                if (d != null && !d.ContainsKey(Rarity) && d.ContainsKey(RarityItemType.Divine))
                {
                    Util.Il2CppRaw.DictAddEnumValue(d, (int)Rarity, d[RarityItemType.Divine] + Plugin.MerchantMythicExtraPrice.Value);
                    Plugin.V($"[Mythic] {m.name}: цена мифика {d[Rarity]}");
                }
            }
        }

        // ---------- Словари в MonoBehaviour-ах (заполняются лениво из патчей) ----------

        public static void EnsureColor(AYellowpaper.SerializedCollections.SerializedDictionary<RarityItemType, Color> d, Color c)
        {
            if (d != null && !d.ContainsKey(Rarity)) Util.Il2CppRaw.DictAddEnumValue(d, (int)Rarity, c);
        }

        public static void EnsureSprite(AYellowpaper.SerializedCollections.SerializedDictionary<RarityItemType, Sprite> d)
        {
            if (d == null || d.ContainsKey(Rarity)) return;
            if (d.ContainsKey(RarityItemType.Divine)) d.Add(Rarity, Recolor(d[RarityItemType.Divine]));
        }

        public static void EnsureCopy<TV>(AYellowpaper.SerializedCollections.SerializedDictionary<RarityItemType, TV> d)
        {
            if (d == null || d.ContainsKey(Rarity)) return;
            if (d.ContainsKey(RarityItemType.Divine)) d.Add(Rarity, d[RarityItemType.Divine]);
        }
    }
}
