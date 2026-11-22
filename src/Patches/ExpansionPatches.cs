using System;
using HarmonyLib;
using MythicAscension.Mechanics;

namespace MythicAscension.Patches
{
    /// Пересчёт размеров сеток перед тем, как игра их построит.
    [HarmonyPatch]
    public static class ExpansionPatches
    {
        [HarmonyPatch(typeof(InventoryStashGridBuilder), nameof(InventoryStashGridBuilder.OnEnable))]
        [HarmonyPrefix]
        static void GridBuilderEnable()
        {
            try { Expansion.Tick(); }
            catch (Exception e) { Plugin.L.LogError("[Expand] GridBuilder: " + e); }
        }

        [HarmonyPatch(typeof(InventoryStashService), nameof(InventoryStashService.Setup))]
        [HarmonyPostfix]
        static void ServiceSetup()
        {
            try { Expansion.Tick(); }
            catch (Exception e) { Plugin.L.LogError("[Expand] Setup: " + e); }
        }
    }
}
