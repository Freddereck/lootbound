using System;
using HarmonyLib;
using UnityEngine.InputSystem;
using static Services.SaveSystem.SaveDatas.InventorySaveData;

namespace MythicAscension.Patches
{
    /// Дополнительные повороты предмета: Shift + поворот = 180°, Ctrl + поворот = 90° против часовой.
    /// Реализовано повторным вызовом родного поворота на 90°, поэтому логика игры не меняется.
    [HarmonyPatch]
    public static class RotationPatches
    {
        static bool _inside;

        static int ExtraTurns()
        {
            var kb = Keyboard.current;
            if (kb == null) return 0;
            if (kb.ctrlKey.isPressed) return 2;   // 1 + 2 = 270° по часовой = 90° против
            if (kb.shiftKey.isPressed) return 1;  // 1 + 1 = 180°
            return 0;
        }

        [HarmonyPatch(typeof(InventoryStashProcessUtils), nameof(InventoryStashProcessUtils.RotateItem))]
        [HarmonyPrefix]
        static bool RotateItem(int rotateAmount, StashItem stashItem, ref StashItem __result)
        {
            if (_inside || !Plugin.ExtraRotation.Value) return true;
            int extra;
            try { extra = ExtraTurns(); } catch { return true; }
            if (extra == 0) return true;

            _inside = true;
            try
            {
                var r = InventoryStashProcessUtils.RotateItem(rotateAmount, stashItem);
                for (int i = 0; i < extra; i++)
                    r = InventoryStashProcessUtils.RotateItem(rotateAmount, r);
                __result = r;
                Plugin.V($"[Rotate] {stashItem.ItemID}: {stashItem.ItemDirection} → {r.ItemDirection} (+{extra} доп.)");
                return false;
            }
            catch (Exception e)
            {
                Plugin.L.LogWarning("[Rotate] " + e.Message);
                return true;
            }
            finally { _inside = false; }
        }
    }
}
