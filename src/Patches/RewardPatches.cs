using System;
using Configurations.ItemsListConfiguration;
using Configurations.RewardsConfiguration;
using HarmonyLib;
using Locations.Map.MapEvents;
using MythicAscension.Content;
using Services.InventorySystem;
using Services.RewardSystem;
using UI.Rest.RewardSelection;
using UnityEngine;

namespace MythicAscension.Patches
{
    /// Источники мифических предметов: награды за бои, сундуки/отдых, торговец.
    [HarmonyPatch]
    public static class RewardPatches
    {
        // ----- Награда за бой: бросок редкости -----

        [HarmonyPatch(typeof(RewardConfig), nameof(RewardConfig.GetRandomRarityItemType))]
        [HarmonyPostfix]
        static void RollRarity(MapEventType eventType, ItemCategory itemCategory, ref RarityItemType __result)
        {
            if (ContentInjector.MythicItems.Count == 0) return;
            if ((itemCategory & (ItemCategory.Weapon | ItemCategory.Armor | ItemCategory.Accessory)) == 0) return;

            float chance = eventType switch
            {
                MapEventType.Elite => Plugin.EliteMythicChance.Value,
                MapEventType.Battle => Plugin.BattleMythicChance.Value,
                MapEventType.Boss => Plugin.EliteMythicChance.Value * 3f,
                _ => 0f,
            };
            if (ContentInjector.Roll(chance))
            {
                Plugin.V($"[Drops] {eventType}/{itemCategory}: {__result} → Mythic");
                __result = Mythic.Rarity;
            }
        }

        /// Если мифика нужного размера/героя нет в пуле — откатываемся на Divine, чтобы не получить пустую карту.
        [HarmonyPatch(typeof(RewardService), nameof(RewardService.GetRandomItemForCardSlot))]
        [HarmonyPostfix]
        static void CardSlotFallback(RewardService __instance, Il2CppSystem.Collections.Generic.List<ItemData> itemsList,
            RarityItemType rarity, Vector2Int slotSize, ref ItemData __result)
        {
            if (__result != null || !Mythic.Is(rarity)) return;
            try
            {
                __result = __instance.GetRandomItemForCardSlot(itemsList, RarityItemType.Divine, slotSize);
                Plugin.V($"[Drops] Мифик под слот {slotSize} не найден — выдан Divine {__result?.ItemID}");
            }
            catch (Exception e) { Plugin.L.LogWarning("[Drops] fallback: " + e.Message); }
        }

        // ----- Сундук / отдых (выбор карт с броском кубика) -----

        [HarmonyPatch(typeof(ItemRewardSelectionStrategy), nameof(ItemRewardSelectionStrategy.CreateItemRewardDefinition))]
        [HarmonyPrefix]
        static void ChestSwap(ref ItemData itemData)
        {
            if (itemData == null || itemData.Rarity != RarityItemType.Divine) return;
            if (!ContentInjector.Roll(Plugin.ChestMythicChance.Value)) return;
            var m = ContentInjector.PickMythicLike(itemData);
            if (m == null) return;
            Plugin.V($"[Drops] Сундук: {itemData.ItemID} → {m.ItemID}");
            itemData = m;
        }

        // ----- Торговец -----

        [HarmonyPatch(typeof(MerchantRewardItemsListConfig), nameof(MerchantRewardItemsListConfig.GetRandomRarityItem))]
        [HarmonyPostfix]
        static void MerchantSwap(RarityItemType rarity, ref ItemData __result)
        {
            if (__result == null || rarity != RarityItemType.Divine) return;
            if (!ContentInjector.Roll(Plugin.MerchantMythicChance.Value)) return;
            var m = ContentInjector.PickMythicLike(__result);
            if (m == null) return;
            Plugin.V($"[Drops] Торговец: {__result.ItemID} → {m.ItemID}");
            __result = m;
        }

        [HarmonyPatch(typeof(MerchantItemToBuyElement), nameof(MerchantItemToBuyElement.Setup))]
        [HarmonyPrefix]
        static void MerchantPrice(ItemData itemData, ref int price)
        {
            if (itemData != null && Mythic.Is(itemData.Rarity))
                price += Plugin.MerchantMythicExtraPrice.Value;
        }
    }
}
