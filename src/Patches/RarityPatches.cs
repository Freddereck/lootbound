using System;
using HarmonyLib;
using MythicAscension.Content;
using Services.InventorySystem;
using Services.InventorySystem.StaticData;
using UnityEngine;
using UnityEngine.Localization;

namespace MythicAscension.Patches
{
    /// Визуальная и текстовая поддержка редкости Mythic во всех местах, где игра выбирает что-то по редкости.
    [HarmonyPatch]
    public static class RarityPatches
    {
        // ----- Название редкости -----

        [HarmonyPatch(typeof(ItemLocalization), nameof(ItemLocalization.GetRarityKey))]
        [HarmonyPrefix]
        static bool GetRarityKey(RarityItemType rarityItemType, ref string __result)
        {
            if (!Mythic.Is(rarityItemType)) return true;
            __result = Mythic.RarityKey;
            return false;
        }

        [HarmonyPatch(typeof(ItemLocalization), nameof(ItemLocalization.ForRarity))]
        [HarmonyPrefix]
        static void ForRarityPre(ref RarityItemType rarity, out bool __state)
        {
            __state = Mythic.Is(rarity);
            if (__state) rarity = RarityItemType.Divine; // берём ссылку на ту же таблицу, потом меняем ключ
        }

        [HarmonyPatch(typeof(ItemLocalization), nameof(ItemLocalization.ForRarity))]
        [HarmonyPostfix]
        static void ForRarityPost(bool __state, ref LocalizedString __result)
        {
            if (!__state || __result == null) return;
            try { __result.TableEntryReference = Mythic.RarityKey; }
            catch (Exception e) { Plugin.L.LogWarning("[Rarity] ForRarity: " + e.Message); }
        }

        // ----- Рамка ячейки -----
        // Игра ищет спрайт рамки перебором массива RaritySprites; для Mythic там пусто → Image без спрайта
        // рисуется серым квадратом. Поэтому после настройки ячейки подставляем красный спрайт сами.

        [HarmonyPatch(typeof(InventoryStashRarityData), nameof(InventoryStashRarityData.GetRaritySprite))]
        [HarmonyPrefix]
        static bool GetRaritySprite(RarityItemType rarity, ref Sprite __result)
        {
            if (!Mythic.Is(rarity)) return true;
            var s = Mythic.CellSprite();
            if (s == null) return true;
            __result = s;
            return false;
        }

        [HarmonyPatch(typeof(InventoryStashCell), nameof(InventoryStashCell.SetRarity))]
        [HarmonyPostfix]
        static void CellSetRarity(InventoryStashCell __instance, RarityItemType rarity)
        {
            if (!Mythic.Is(rarity)) return;
            try
            {
                var img = __instance._rarity;
                var s = Mythic.CellSprite();
                if (img != null && s != null) img.sprite = s;
            }
            catch (Exception e) { Plugin.L.LogWarning("[Rarity] Cell: " + e.Message); }
        }

        static int _diagLeft = 2;

        static string Describe(Transform t, int depth)
        {
            var sb = new System.Text.StringBuilder();
            var img = t.GetComponent<UnityEngine.UI.Image>();
            sb.Append(new string(' ', depth * 2)).Append(t.name)
              .Append(img != null ? $" [Image sprite={(img.sprite ? img.sprite.name : "null")} color={img.color}]" : "")
              .Append('\n');
            if (depth < 3)
                for (int i = 0; i < t.childCount && i < 4; i++) sb.Append(Describe(t.GetChild(i), depth + 1));
            return sb.ToString();
        }

        /// Ставит красный спрайт рамкам ячеек карточки награды. Вызывается отложенно — игра создаёт ячейки позже.
        static void FixRewardCard(InventoryStashRewardCardItem item)
        {
            if (item == null || item.ItemData == null || !Mythic.Is(item.ItemData.Rarity)) return;
            var s = Mythic.CellSprite();
            var grid = item._gridLayout;
            if (s == null || grid == null) return;
            int changed = 0;
            foreach (var img in grid.GetComponentsInChildren<UnityEngine.UI.Image>(true))
            {
                if (img.sprite == s) continue;
                if (img.sprite == null || img.sprite.name.StartsWith("UI_Rarity_Cell")) { img.sprite = s; changed++; }
            }
            if (_diagLeft > 0 && changed == 0)
            {
                _diagLeft--;
                Plugin.V("[Rarity] Карточка мифика, ячейки не найдены. Иерархия:\n" + Describe(item.transform, 0));
            }
            else if (changed > 0) Plugin.V($"[Rarity] Карточка {item.ItemData.ItemID}: рамок перекрашено {changed}");
        }

        static void ScheduleFix(InventoryStashRewardCardItem item)
        {
            ModRunner.Later(1, () => FixRewardCard(item));
            ModRunner.Later(5, () => FixRewardCard(item));
            ModRunner.Later(20, () => FixRewardCard(item));
        }

        [HarmonyPatch(typeof(InventoryStashRewardCardItem), nameof(InventoryStashRewardCardItem.SpawnGridCells))]
        [HarmonyPostfix]
        static void RewardGridCells(InventoryStashRewardCardItem __instance, RarityItemType rarityItemType)
        {
            if (Mythic.Is(rarityItemType)) ScheduleFix(__instance);
        }

        [HarmonyPatch(typeof(InventoryStashRewardCardItem), nameof(InventoryStashRewardCardItem.ShowRewardItem))]
        [HarmonyPostfix]
        static void RewardShow(InventoryStashRewardCardItem __instance)
        {
            try { if (__instance.ItemData != null && Mythic.Is(__instance.ItemData.Rarity)) ScheduleFix(__instance); }
            catch (Exception e) { Plugin.L.LogWarning("[Rarity] RewardShow: " + e.Message); }
        }

        // ----- Карточки наград: свечение и звук -----

        static void EnsureRewardBuilder(InventoryStashRewardBuilder b)
        {
            try
            {
                Mythic.EnsureColor(b._rewardGlowColors, Mythic.GlowColor);
                Mythic.EnsureCopy(b._rewardSFX);
            }
            catch (Exception e) { Plugin.L.LogWarning("[Rarity] RewardBuilder: " + e.Message); }
        }

        [HarmonyPatch(typeof(InventoryStashRewardBuilder), nameof(InventoryStashRewardBuilder.SetupRarityGlowColor))]
        [HarmonyPrefix]
        static void SetupGlow(InventoryStashRewardBuilder __instance) => EnsureRewardBuilder(__instance);

        [HarmonyPatch(typeof(InventoryStashRewardBuilder), nameof(InventoryStashRewardBuilder.ChangeRarityGlowColor))]
        [HarmonyPrefix]
        static void ChangeGlow(InventoryStashRewardBuilder __instance) => EnsureRewardBuilder(__instance);

        [HarmonyPatch(typeof(InventoryStashRewardBuilder), nameof(InventoryStashRewardBuilder.GetCardRewardSFX))]
        [HarmonyPrefix]
        static void RewardSfx(InventoryStashRewardBuilder __instance) => EnsureRewardBuilder(__instance);

        // ----- Коллекция предметов (хаб) -----

        [HarmonyPatch(typeof(ItemCollectionElement), nameof(ItemCollectionElement.Setup))]
        [HarmonyPrefix]
        static void CollectionSetup(ItemCollectionElement __instance, ItemData itemData)
        {
            if (itemData == null || !Mythic.Is(itemData.Rarity)) return;
            try
            {
                Mythic.EnsureSprite(__instance._cardRarity);
                Mythic.EnsureSprite(__instance._bubbleRarity);
                Mythic.EnsureColor(__instance._nameRarity, Mythic.NameColor);
                Mythic.EnsureColor(__instance._outlineRarity, Mythic.OutlineColor);
            }
            catch (Exception e) { Plugin.L.LogWarning("[Rarity] Collection: " + e.Message); }
        }
    }
}
