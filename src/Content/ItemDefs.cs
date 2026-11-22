using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Services.CharacterSystem.Stats;

namespace MythicAscension.Content
{
    /// Описание одного нового предмета (items.json).
    public class ItemDef
    {
        public string Id { get; set; }
        /// ID существующего предмета игры: с него копируются иконка, форма, геометрия синергии и стратегия использования.
        public string Base { get; set; }
        /// Common / Rare / Legendary / Divine / Mythic
        public string Rarity { get; set; } = "Mythic";
        public Dictionary<string, string> Name { get; set; } = new();
        /// "SlashDamage:12, CriticalChance:4" — значения в БЛОКАХ.
        public string Stats { get; set; }
        /// Статы синергии в блоках. null — оставить как у базового предмета.
        public string Synergy { get; set; }
        /// Эффекты механик: "GROW:SlashDamage:2:8:10", "SKILL:HeroAbilityWarrior_8:80", "KILLHEAL:6" ...
        public List<string> Effects { get; set; } = new();
        public string Set { get; set; }
        public int? EnhanceSlots { get; set; }
        public int? Price { get; set; }
        /// Шанс выпадения относительно других мификов (вес).
        public int Weight { get; set; } = 100;
        /// Артефакт расширения: "Column" или "Row".
        public string Expand { get; set; }
        /// Убрать ауру/синергию базового предмета.
        public bool NoSynergy { get; set; }
        /// Нельзя выбросить/разобрать.
        public bool CantDiscard { get; set; }
    }

    public class SetDef
    {
        public string Id { get; set; }
        public Dictionary<string, string> Name { get; set; } = new();
        public List<string> Bonus2 { get; set; } = new();
        public List<string> Bonus4 { get; set; } = new();
    }

    public class ContentFile
    {
        public List<ItemDef> Items { get; set; } = new();
        public List<SetDef> Sets { get; set; } = new();
    }

    public struct StatBlocks
    {
        public StatType Stat;
        public int Blocks;
    }

    public static class ItemDefs
    {
        public static ContentFile Content = new();
        public static readonly Dictionary<string, ItemDef> ById = new();
        public static readonly Dictionary<string, SetDef> SetsById = new();

        /// Стоимость одного блока для базовых статов и статов синергии (выведено из данных игры).
        public static readonly Dictionary<StatType, (int Base, int Syn)> BlockValue = new()
        {
            [StatType.MaxHP] = (25, 20),
            [StatType.MaxMP] = (4, 2),
            [StatType.Initiative] = (6, 4),
            [StatType.MaxArmor] = (20, 15),
            [StatType.CriticalDamage] = (4, 2),
            [StatType.Resistance] = (4, 3),
            [StatType.DodgeChance] = (3, 2),
            [StatType.CriticalChance] = (2, 1),
            [StatType.ReflectedDamage] = (10, 6),
            [StatType.StealingHealth] = (9, 7),
            [StatType.AcidDamage] = (5, 3),
            [StatType.CrushDamage] = (10, 7),
            [StatType.SlashDamage] = (8, 5),
            [StatType.BlessPower] = (5, 3),
            [StatType.CursePower] = (4, 2),
            [StatType.HealthRegen] = (2, 1),
            [StatType.ManaRegen] = (1, 1),
            [StatType.Damage] = (5, 3),
        };

        public static int ValueOf(StatType st, int blocks, bool synergy)
        {
            if (!BlockValue.TryGetValue(st, out var v)) v = (1, 1);
            return blocks * (synergy ? v.Syn : v.Base);
        }

        public static List<StatBlocks> ParseStats(string s)
        {
            var list = new List<StatBlocks>();
            if (string.IsNullOrWhiteSpace(s)) return list;
            foreach (var part in s.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var kv = part.Split(':');
                if (kv.Length != 2 || !Enum.TryParse<StatType>(kv[0].Trim(), true, out var st)
                    || !int.TryParse(kv[1].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var b))
                {
                    Plugin.L.LogWarning($"[Items] Не разобран стат '{part}'");
                    continue;
                }
                list.Add(new StatBlocks { Stat = st, Blocks = b });
            }
            return list;
        }

        public static void Load(string path)
        {
            ById.Clear();
            SetsById.Clear();
            if (!File.Exists(path))
            {
                Plugin.L.LogError($"[Items] Не найден {path}");
                return;
            }
            var opts = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true,
            };
            Content = JsonSerializer.Deserialize<ContentFile>(File.ReadAllText(path), opts) ?? new ContentFile();
            foreach (var d in Content.Items)
            {
                if (string.IsNullOrEmpty(d.Id) || string.IsNullOrEmpty(d.Base)) { Plugin.L.LogWarning("[Items] Предмет без Id/Base пропущен"); continue; }
                if (ById.ContainsKey(d.Id)) { Plugin.L.LogWarning($"[Items] Дубликат {d.Id}"); continue; }
                ById[d.Id] = d;
            }
            foreach (var s in Content.Sets) SetsById[s.Id] = s;
            Plugin.L.LogInfo($"[Items] Загружено {ById.Count} предметов, {SetsById.Count} сетов из {Path.GetFileName(path)}");
        }

        public static string Text(Dictionary<string, string> d, string lang)
        {
            if (d == null || d.Count == 0) return "";
            if (d.TryGetValue(lang, out var t)) return t;
            if (d.TryGetValue("en", out t)) return t;
            return d.Values.First();
        }
    }
}
