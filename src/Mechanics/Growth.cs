using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using MythicAscension.Content;
using MythicAscension.Util;
using Services.CharacterSystem.Stats;
using Services.InventorySystem;
using static Services.SaveSystem.SaveDatas.InventorySaveData;

namespace MythicAscension.Mechanics
{
    /// «Жажда крови»: предметы с эффектом GROW набирают убийства и растут.
    /// Уровень хранится как дополнительный ItemStat в самом предмете сохранения (UniqueID = "MYTHGROW_n"),
    /// поэтому игра сама его применяет, показывает и сохраняет. Счётчик убийств — в файле прогресса мода.
    public static class Growth
    {
        const string Marker = "MYTHGROW_";
        static Dictionary<string, int> _kills;
        static bool _dirty;

        static string FilePath => Path.Combine(BepInEx.Paths.ConfigPath, "MythicAscension.progress.json");

        static Dictionary<string, int> Kills
        {
            get
            {
                if (_kills != null) return _kills;
                try
                {
                    _kills = File.Exists(FilePath)
                        ? JsonSerializer.Deserialize<Dictionary<string, int>>(File.ReadAllText(FilePath)) ?? new()
                        : new();
                }
                catch (Exception e)
                {
                    Plugin.L.LogWarning("[Growth] progress: " + e.Message);
                    _kills = new();
                }
                return _kills;
            }
        }

        public static void Flush()
        {
            if (!_dirty) return;
            _dirty = false;
            try { File.WriteAllText(FilePath, JsonSerializer.Serialize(Kills)); }
            catch (Exception e) { Plugin.L.LogWarning("[Growth] save: " + e.Message); }
        }

        static string Key(StashItem si, int idx) => si.UniqueID + "#" + idx;

        /// Текущий уровень, записанный в статах предмета.
        public static int CurrentLevel(StashItem si, int idx, Effect e)
        {
            int i = FindStat(si.ItemStats, idx);
            if (i < 0) return 0;
            int blocks = si.ItemStats[i].Blocks;
            return e.A > 0 ? blocks / e.A : 0;
        }

        public static int KillCount(StashItem si, int idx, Effect e)
        {
            Kills.TryGetValue(Key(si, idx), out var k);
            return Math.Max(k, CurrentLevel(si, idx, e) * e.B); // если файл прогресса потерян — не меньше уровня
        }

        static int FindStat(Il2CppSystem.Collections.Generic.List<ItemStat> stats, int idx)
        {
            if (stats == null) return -1;
            string id = Marker + idx;
            for (int i = 0; i < stats.Count; i++)
                if (stats[i].UniqueID == id) return i;
            return -1;
        }

        /// Начисляет убийства всем растущим предметам носителя. Возвращает true, если какой-то предмет вырос.
        public static bool AddKills(GearSummary gear, int credit)
        {
            bool leveled = false;
            foreach (var p in gear.Pieces)
            {
                int idx = 0;
                int mult = gear.GrowMultiplier(p.Def);
                foreach (var e in p.Effects)
                {
                    if (e.Kind != EffectKind.Grow) continue;
                    int before = CurrentLevel(p.Stash, idx, e);
                    int kills = KillCount(p.Stash, idx, e) + credit * mult;
                    Kills[Key(p.Stash, idx)] = kills;
                    _dirty = true;
                    int level = Math.Min(e.C, e.B > 0 ? kills / e.B : 0);
                    if (level > before)
                    {
                        SetLevel(p.Stash, idx, e, level);
                        leveled = true;
                        Plugin.L.LogInfo($"[Growth] {p.Def.Id}: уровень {before} → {level} ({kills} убийств)");
                    }
                    idx++;
                }
            }
            return leveled;
        }

        static void SetLevel(StashItem si, int idx, Effect e, int level)
        {
            var stats = si.ItemStats;
            if (stats == null) return;
            var st = new ItemStat();
            st.StatType = e.Stat;
            st.ModifierType = StatModifierType.Flat;
            st.Blocks = e.A * level;
            st.Value = ItemDefs.ValueOf(e.Stat, e.A * level, false);
            st.StatTag = StatTag.Base;
            st.UniqueID = Marker + idx;

            int i = FindStat(stats, idx);
            if (i >= 0) stats.RemoveAt(i);
            Il2CppRaw.ListAddStruct(stats, st);
        }

        /// Строки прогресса для подсказки.
        public static IEnumerable<string> ProgressLines(StashItem si, ItemDef def, bool ru)
        {
            int idx = 0;
            foreach (var e in Gear.EffectsOf(def))
            {
                if (e.Kind != EffectKind.Grow) continue;
                int level = CurrentLevel(si, idx, e);
                int kills = KillCount(si, idx, e);
                string tail = level >= e.C
                    ? (ru ? "МАКС." : "MAX")
                    : $"{kills}/{(level + 1) * e.B}";
                yield return ru
                    ? $"Жажда крови: ур. {level}/{e.C} · убийства {tail}"
                    : $"Bloodthirst: level {level}/{e.C} · kills {tail}";
                idx++;
            }
        }
    }
}
