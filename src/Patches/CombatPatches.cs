using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using MythicAscension.Content;
using MythicAscension.Mechanics;
using Services.CharacterSystem;
using Services.CombatSystem;
using Services.CombatSystem.AbilitySystem;
using UnityEngine.Localization.Settings;

namespace MythicAscension.Patches
{
    [HarmonyPatch]
    public static class CombatPatches
    {
        [HarmonyPatch(typeof(CombatService), nameof(CombatService.StartCombat))]
        [HarmonyPostfix]
        static void StartCombat(CombatService __instance)
        {
            try
            {
                var team = __instance.GetPlayerTeam();
                var list = new List<Character>();
                if (team != null) for (int i = 0; i < team.Count; i++) list.Add(team[i]);
                Combat.OnCombatStart(list);
                Plugin.V($"[Combat] Начало боя, команда: {string.Join(", ", list.Select(c => c.Type))}");
            }
            catch (Exception e) { Plugin.L.LogError("[Combat] StartCombat: " + e); }
        }

        [HarmonyPatch(typeof(Character), nameof(Character.ExecuteAction))]
        [HarmonyPrefix]
        static void ExecuteAction(Character __instance, AbilityContext context)
        {
            try { Combat.OnAction(__instance, context?.Config?.AbilityID); }
            catch (Exception e) { Plugin.L.LogWarning("[Combat] ExecuteAction: " + e.Message); }
        }

        [HarmonyPatch(typeof(Character), nameof(Character.ApplyDamage))]
        [HarmonyPrefix]
        static void ApplyDamage(Character __instance, Il2CppSystem.Collections.Generic.List<DamageOptions> options)
        {
            if (options == null) return;
            try
            {
                for (int i = 0; i < options.Count; i++)
                    if (options[i] != null) Combat.ModifyDamage(__instance, options[i]);
            }
            catch (Exception e) { Plugin.L.LogWarning("[Combat] ApplyDamage: " + e.Message); }
        }

        [HarmonyPatch(typeof(DamageProcessor), nameof(DamageProcessor.RegisterDeath))]
        [HarmonyPostfix]
        static void RegisterDeath(Character character)
        {
            try { Combat.OnDeath(character); }
            catch (Exception e) { Plugin.L.LogError("[Combat] RegisterDeath: " + e); }
        }

        /// Запасной путь: если смерть прошла мимо RegisterDeath.
        [HarmonyPatch(typeof(CombatService), nameof(CombatService.CheckCharactersDeath))]
        [HarmonyPostfix]
        static void CheckDeaths(Il2CppSystem.Collections.Generic.List<Character> characters)
        {
            if (characters == null) return;
            try
            {
                for (int i = 0; i < characters.Count; i++)
                {
                    var c = characters[i];
                    if (c != null && !c.IsAlive) Combat.OnDeath(c);
                }
            }
            catch (Exception e) { Plugin.L.LogError("[Combat] CheckDeaths: " + e); }
        }
    }

    /// Описание эффектов и прогресс роста в подсказке предмета.
    [HarmonyPatch]
    public static class TooltipPatches
    {
        public static bool IsRussian()
        {
            try { return LocalizationSettings.SelectedLocale?.Identifier.Code?.StartsWith("ru") == true; }
            catch { return true; }
        }

        [HarmonyPatch(typeof(GameTooltipItems), nameof(GameTooltipItems.Setup))]
        [HarmonyPostfix]
        static void Setup(GameTooltipItems __instance, GameTooltipData data)
        {
            try
            {
                var d = data?.TryCast<GameTooltipItemsData>();
                var item = d?.ItemData;
                if (item == null || !ItemDefs.ById.TryGetValue(item.ItemID ?? "", out var def)) return;

                bool ru = IsRussian();
                var text = Effects.BuildDescription(def, ru);
                var si = d.StashItem;
                if (si != null && !string.IsNullOrEmpty(si.UniqueID))
                {
                    var progress = Growth.ProgressLines(si, def, ru).ToList();
                    if (progress.Count > 0) text = string.Join("\n", progress) + "\n" + text;
                }

                var t = __instance._descriptionText;
                if (t == null) return;
                t.text = text;
                for (var tr = t.transform; tr != null && tr != __instance.transform; tr = tr.parent)
                    if (!tr.gameObject.activeSelf) tr.gameObject.SetActive(true);
            }
            catch (Exception e) { Plugin.L.LogWarning("[Tooltip] " + e.Message); }
        }
    }
}
