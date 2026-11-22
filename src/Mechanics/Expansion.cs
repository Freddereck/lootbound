using System;
using MythicAscension.Content;
using Services.InventorySystem;
using Services.SaveSystem.SaveDatas;
using static Services.SaveSystem.SaveDatas.InventorySaveData;

namespace MythicAscension.Mechanics
{
    /// Артефакты расширения: пока лежат в инвентаре (где угодно), добавляют столбцы/строки
    /// сетке экипировки всех героев и рюкзаку. Не выбрасываются, поэтому держатся до конца забега.
    public static class Expansion
    {
        public const int MaxPerType = 2;

        static bool _baseCaptured;
        static int _heroW, _heroH, _compW, _compH, _mainW, _mainH;
        static int _appliedCols = -1, _appliedRows = -1;

        static InventoryStashService Service()
        {
            try { return Core.Container.Resolve<InventoryStashService>(); }
            catch { return null; }
        }

        static void Count(Il2CppSystem.Collections.Generic.List<StashItem> list, ref int cols, ref int rows)
        {
            if (list == null) return;
            for (int i = 0; i < list.Count; i++)
            {
                var id = list[i]?.ItemID;
                if (id == null || !ItemDefs.ById.TryGetValue(id, out var def) || string.IsNullOrEmpty(def.Expand)) continue;
                if (def.Expand.Equals("Column", StringComparison.OrdinalIgnoreCase)) cols++;
                else if (def.Expand.Equals("Row", StringComparison.OrdinalIgnoreCase)) rows++;
            }
        }

        public static (int cols, int rows) CountArtifacts(InventorySaveData save)
        {
            int cols = 0, rows = 0;
            if (save == null) return (0, 0);
            Count(save.StashItems, ref cols, ref rows);
            var cs = save.CharactersStash;
            if (cs != null)
                for (int i = 0; i < cs.Count; i++)
                    Count(cs[i]?.CharacterItems, ref cols, ref rows);
            return (Math.Min(cols, MaxPerType), Math.Min(rows, MaxPerType));
        }

        /// Периодическая синхронизация + вызывается перед построением сетки инвентаря.
        public static void Tick()
        {
            var svc = Service();
            if (svc == null) return;
            var charData = svc._inventoryStashCharacterData;
            var mainData = svc._inventoryStashData;
            var save = svc._inventorySaveData;
            if (charData == null || mainData == null) return;

            if (!_baseCaptured)
            {
                _heroW = charData.ItemsStashWidth; _heroH = charData.ItemsStashHeight;
                _compW = charData.CompanionItemsStashWidth; _compH = charData.CompanionItemsStashHeight;
                _mainW = mainData.Width; _mainH = mainData.Height;
                _baseCaptured = true;
                Plugin.V($"[Expand] База: герой {_heroW}x{_heroH}, компаньон {_compW}x{_compH}, рюкзак {_mainW}x{_mainH}");
            }

            var (cols, rows) = CountArtifacts(save);
            if (cols == _appliedCols && rows == _appliedRows) return;

            // Экипировка всех героев
            charData.ItemsStashWidth = _heroW + cols;
            charData.ItemsStashHeight = _heroH + rows;
            charData.CompanionItemsStashWidth = _compW + cols;
            charData.CompanionItemsStashHeight = _compH + rows;

            // Рюкзак
            try { ResizeMainStash(svc, mainData, save, _mainW + cols, _mainH + rows); }
            catch (Exception e) { Plugin.L.LogError("[Expand] Рюкзак: " + e); }

            Plugin.L.LogInfo($"[Expand] Столбцов +{cols}, строк +{rows}: экипировка {charData.ItemsStashWidth}x{charData.ItemsStashHeight}, рюкзак {mainData.Width}x{mainData.Height}");
            _appliedCols = cols;
            _appliedRows = rows;
        }

        static Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<bool> Remap(
            Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<bool> old, int oldW, int oldH, int w, int h)
        {
            var res = new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<bool>(w * h);
            bool allOpen = true;
            if (old != null) for (int i = 0; i < old.Length; i++) if (!old[i]) { allOpen = false; break; }
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    bool v = true; // новые клетки открыты
                    if (!allOpen && old != null && x < oldW && y < oldH && y * oldW + x < old.Length) v = old[y * oldW + x];
                    res[y * w + x] = v;
                }
            return res;
        }

        static void ResizeMainStash(InventoryStashService svc, InventoryStashData data, InventorySaveData save, int w, int h)
        {
            int oldW = data.Width, oldH = data.Height;
            if (oldW == w && oldH == h) return;

            data.InventoryLayout = Remap(data.InventoryLayout, oldW, oldH, w, h);
            data.Width = w;
            data.Height = h;
            if (save != null && save.StashLayout != null && save.StashLayout.Length != w * h)
                save.StashLayout = Remap(save.StashLayout, oldW, oldH, w, h);

            var proc = svc.GetMainStashProcess();
            if (proc != null)
            {
                proc.ProcessStashLayout();
                proc.ProcessStashAccessibility();
            }
            Plugin.V($"[Expand] Рюкзак {oldW}x{oldH} → {w}x{h}");
        }
    }
}
