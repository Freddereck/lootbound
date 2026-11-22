using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Services.CharacterSystem.Stats;

namespace MythicAscension.Content
{
    public enum EffectKind
    {
        Grow,       // GROW:Stat:blocksPerLevel:killsPerLevel:maxLevel
        Skill,      // SKILL:AbilityID:percent
        KillHeal,   // KILLHEAL:percentMaxHP
        KillMana,   // KILLMANA:amount
        Fury,       // FURY:percentPerKill  (до 5 стаков за бой)
        Pierce,     // PIERCE:chance
        Exec,       // EXEC:percent  (цели ниже 30% HP)
        First,      // FIRST:percent (первая атака носителя в бою)
        Dmg,        // DMG:percent   (ко всему урону)
        StatPct,    // STATPCT:Stat:percent
        Stat,       // STAT:Stat:flat
        MythMult,   // MYTHMULT:multiplier  (Мифическая мощь носителя ×N)
        MythPer,    // MYTHPER:percent      (Мифическая мощь: N% за предмет вместо базовых)
        GrowX,      // GROWX:multiplier     (бонус сета: предметы сета растут в N раз быстрее)
    }

    public class Effect
    {
        public EffectKind Kind;
        public StatType Stat;
        /// Для SKILL: ID способностей (герой и компаньон), разделённые '|' в items.json.
        public string[] Abilities = Array.Empty<string>();
        public int A, B, C;
        public string Source;

        public override string ToString() => Source;
    }

    public static class Effects
    {
        public const int FuryMaxStacks = 5;
        public const float ExecThreshold = 0.30f;

        static int I(string s) => int.Parse(s.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture);

        public static Effect Parse(string src)
        {
            try
            {
                var p = src.Split(':').Select(x => x.Trim()).ToArray();
                var e = new Effect { Source = src };
                switch (p[0].ToUpperInvariant())
                {
                    case "GROW": e.Kind = EffectKind.Grow; e.Stat = Enum.Parse<StatType>(p[1], true); e.A = I(p[2]); e.B = I(p[3]); e.C = I(p[4]); break;
                    case "SKILL":
                        e.Kind = EffectKind.Skill;
                        e.Abilities = p[1].Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                        e.A = I(p[2]);
                        break;
                    case "KILLHEAL": e.Kind = EffectKind.KillHeal; e.A = I(p[1]); break;
                    case "KILLMANA": e.Kind = EffectKind.KillMana; e.A = I(p[1]); break;
                    case "FURY": e.Kind = EffectKind.Fury; e.A = I(p[1]); break;
                    case "PIERCE": e.Kind = EffectKind.Pierce; e.A = I(p[1]); break;
                    case "EXEC": e.Kind = EffectKind.Exec; e.A = I(p[1]); break;
                    case "FIRST": e.Kind = EffectKind.First; e.A = I(p[1]); break;
                    case "DMG": e.Kind = EffectKind.Dmg; e.A = I(p[1]); break;
                    case "STATPCT": e.Kind = EffectKind.StatPct; e.Stat = Enum.Parse<StatType>(p[1], true); e.A = I(p[2]); break;
                    case "STAT": e.Kind = EffectKind.Stat; e.Stat = Enum.Parse<StatType>(p[1], true); e.A = I(p[2]); break;
                    case "MYTHMULT": e.Kind = EffectKind.MythMult; e.A = I(p[1]); break;
                    case "MYTHPER": e.Kind = EffectKind.MythPer; e.A = I(p[1]); break;
                    case "GROWX": e.Kind = EffectKind.GrowX; e.A = I(p[1]); break;
                    default: throw new FormatException("неизвестный эффект");
                }
                return e;
            }
            catch (Exception ex)
            {
                Plugin.L.LogWarning($"[Effects] Не разобран эффект '{src}': {ex.Message}");
                return null;
            }
        }

        public static List<Effect> ParseAll(IEnumerable<string> list)
            => (list ?? Enumerable.Empty<string>()).Select(Parse).Where(e => e != null).ToList();

        // ---------- Тексты ----------

        static readonly Dictionary<StatType, (string ru, string en)> StatNames = new()
        {
            [StatType.MaxHP] = ("Здоровье", "Health"),
            [StatType.MaxMP] = ("Мана", "Mana"),
            [StatType.Initiative] = ("Инициатива", "Initiative"),
            [StatType.MaxArmor] = ("Броня", "Armor"),
            [StatType.Damage] = ("Урон", "Damage"),
            [StatType.CriticalDamage] = ("Крит. урон", "Crit Damage"),
            [StatType.Resistance] = ("Сопротивление", "Resistance"),
            [StatType.DodgeChance] = ("Уклонение", "Dodge"),
            [StatType.CriticalChance] = ("Шанс крита", "Crit Chance"),
            [StatType.HealthRegen] = ("Реген. здоровья", "Health Regen"),
            [StatType.ManaRegen] = ("Реген. маны", "Mana Regen"),
            [StatType.ReflectedDamage] = ("Шипы", "Thorns"),
            [StatType.StealingHealth] = ("Вампиризм", "Lifesteal"),
            [StatType.AcidDamage] = ("Кислотный урон", "Acid Damage"),
            [StatType.CrushDamage] = ("Дробящий урон", "Crush Damage"),
            [StatType.SlashDamage] = ("Рубящий урон", "Slash Damage"),
            [StatType.BlessPower] = ("Сила благословения", "Bless Power"),
            [StatType.CursePower] = ("Сила проклятия", "Curse Power"),
            [StatType.AttackPower] = ("Сила атаки", "Attack Power"),
            [StatType.DefencePower] = ("Сила защиты", "Defence Power"),
        };

        /// Названия способностей для описаний (RU из игры, EN — перевод).
        public static readonly Dictionary<string, (string ru, string en)> AbilityNames = new()
        {
            ["HeroAbilityWarrior_8"] = ("Рубить и кромсать", "Hack and Slash"),
            ["HeroAbilityWarrior_4"] = ("Удар Наступления", "Onslaught Strike"),
            ["HeroAbilityWarrior_1"] = ("Кромсание", "Shred"),
            ["HeroAbilityWitch_3"] = ("Кислотное щупальце", "Acid Tentacle"),
            ["HeroAbilityWitch_5"] = ("Клич баньши", "Banshee Wail"),
            ["HeroAbilityWitch_7"] = ("Приговор Хаоса", "Chaos Verdict"),
            ["HeroAbilityPaladin_10"] = ("Удар стража крепости", "Fortress Warden Strike"),
            ["HeroAbilityPaladin_4"] = ("Сокрушение скал", "Rock Crusher"),
            ["HeroAbilityPaladin_1"] = ("Крушащий удар", "Crushing Blow"),
            ["HeroAbilityBard_9"] = ("Песня боли и жизни", "Song of Pain and Life"),
            ["HeroAbilityBard_10"] = ("Ритм боя", "Battle Rhythm"),
            ["HeroAbilityBard_7"] = ("Нота сокрушения", "Note of Ruin"),
            // Способности компаньонов
            ["CompanionAbilityWarrior_2"] = ("Штурмовой выпад", "Assault Lunge"),
            ["CompanionAbilityWarrior_3"] = ("Взмах гнева", "Wrathful Swing"),
            ["CompanionAbilityWitch_3"] = ("Крушение Хаоса", "Chaos Collapse"),
            ["CompanionAbilityWitch_4"] = ("Стрела Энтропии", "Entropy Bolt"),
            ["CompanionAbilityPaladin_2"] = ("Осадный удар", "Siege Strike"),
            ["CompanionAbilityPaladin_3"] = ("Стремительное крушение", "Swift Crush"),
            ["CompanionAbilityBard_1"] = ("Соната рассечения", "Sonata of Cleaving"),
            ["CompanionAbilityBard_3"] = ("Крушащий полонез", "Crushing Polonaise"),
        };

        public static string StatName(StatType st, bool ru)
            => StatNames.TryGetValue(st, out var n) ? (ru ? n.ru : n.en) : st.ToString();

        static string AbilityName(string id, bool ru)
            => AbilityNames.TryGetValue(id, out var n) ? (ru ? n.ru : n.en) : id;

        public static string Describe(Effect e, bool ru)
        {
            switch (e.Kind)
            {
                case EffectKind.Grow:
                    int per = ItemDefs.ValueOf(e.Stat, e.A, false);
                    return ru
                        ? $"Жажда крови: +{per} {StatName(e.Stat, true)} за каждые {e.B} убийств носителя (до {e.C} ур.)"
                        : $"Bloodthirst: +{per} {StatName(e.Stat, false)} per {e.B} kills by the wearer (up to level {e.C})";
                case EffectKind.Skill:
                {
                    var hero = e.Abilities.FirstOrDefault(a => !a.StartsWith("Companion")) ?? e.Abilities.FirstOrDefault() ?? "?";
                    var comp = e.Abilities.FirstOrDefault(a => a.StartsWith("Companion") && a != hero);
                    if (ru)
                        return $"«{AbilityName(hero, true)}» наносит +{e.A}% урона"
                               + (comp != null ? $" (у компаньона — «{AbilityName(comp, true)}»)" : "");
                    return $"\"{AbilityName(hero, false)}\" deals +{e.A}% damage"
                           + (comp != null ? $" (as companion: \"{AbilityName(comp, false)}\")" : "");
                }
                case EffectKind.KillHeal:
                    return ru ? $"Убийство: лечит носителя на {e.A}% макс. здоровья" : $"On kill: heal {e.A}% of max health";
                case EffectKind.KillMana:
                    return ru ? $"Убийство: +{e.A} маны" : $"On kill: restore {e.A} mana";
                case EffectKind.Fury:
                    return ru ? $"Ярость: +{e.A}% урона за каждое убийство в этом бою (до {FuryMaxStacks})"
                              : $"Fury: +{e.A}% damage per kill this fight (up to {FuryMaxStacks})";
                case EffectKind.Pierce:
                    return ru ? $"{e.A}% шанс, что атака проигнорирует броню" : $"{e.A}% chance for attacks to ignore armor";
                case EffectKind.Exec:
                    return ru ? $"Палач: +{e.A}% урона по целям ниже {(int)(ExecThreshold * 100)}% здоровья"
                              : $"Executioner: +{e.A}% damage to targets below {(int)(ExecThreshold * 100)}% health";
                case EffectKind.First:
                    return ru ? $"Первая атака в бою: +{e.A}% урона" : $"First attack each fight: +{e.A}% damage";
                case EffectKind.Dmg:
                    return ru ? $"+{e.A}% ко всему урону" : $"+{e.A}% to all damage";
                case EffectKind.StatPct:
                    return ru ? $"+{e.A}% {StatName(e.Stat, true)}" : $"+{e.A}% {StatName(e.Stat, false)}";
                case EffectKind.Stat:
                    return $"+{e.A} {StatName(e.Stat, ru)}";
                case EffectKind.MythMult:
                    return ru ? $"Мифическая мощь носителя усилена ×{e.A}" : $"Wearer's Mythic Might is multiplied by {e.A}";
                case EffectKind.MythPer:
                    return ru ? $"Мифическая мощь: +{e.A}% за предмет вместо +{Plugin.MythicMightPerItem.Value}%"
                              : $"Mythic Might: +{e.A}% per item instead of +{Plugin.MythicMightPerItem.Value}%";
                case EffectKind.GrowX:
                    return ru ? $"Предметы сета растут в {e.A} раза быстрее" : $"Set items grow {e.A}x faster";
            }
            return e.Source;
        }

        /// Полное описание предмета для подсказки.
        public static string BuildDescription(ItemDef d, bool ru)
        {
            var sb = new StringBuilder();
            if (!string.IsNullOrEmpty(d.Expand))
            {
                bool col = d.Expand.Equals("Column", StringComparison.OrdinalIgnoreCase);
                int max = Mechanics.Expansion.MaxPerType;
                sb.AppendLine(ru
                    ? $"Пока лежит в инвентаре: +1 {(col ? "столбец" : "строка")} в экипировке всех героев и в рюкзаке (до {max} таких артефактов)."
                    : $"While in your inventory: +1 {(col ? "column" : "row")} to every hero's equipment grid and to the backpack (up to {max} of these).");
                sb.AppendLine(ru ? "Нельзя выбросить. Действует до конца забега. Новые клетки появятся при следующем открытии инвентаря."
                                 : "Cannot be discarded. Lasts until the end of the run. New cells appear the next time the inventory is opened.");
                return sb.ToString().TrimEnd();
            }

            foreach (var e in ParseAll(d.Effects))
                sb.AppendLine(Describe(e, ru));

            if (string.Equals(d.Rarity, "Mythic", StringComparison.OrdinalIgnoreCase))
                sb.AppendLine(ru
                    ? $"Мифическая мощь: +{Plugin.MythicMightPerItem.Value}% урона за каждый надетый мифический предмет (до +{Plugin.MythicMightCap.Value}%)"
                    : $"Mythic Might: +{Plugin.MythicMightPerItem.Value}% damage per equipped Mythic item (up to +{Plugin.MythicMightCap.Value}%)");

            if (!string.IsNullOrEmpty(d.Set) && ItemDefs.SetsById.TryGetValue(d.Set, out var set))
            {
                sb.AppendLine((ru ? "Сет «" : "Set \"") + ItemDefs.Text(set.Name, ru ? "ru" : "en") + (ru ? "»" : "\""));
                sb.AppendLine("(2): " + string.Join("; ", ParseAll(set.Bonus2).Select(x => Describe(x, ru))));
                sb.AppendLine("(4): " + string.Join("; ", ParseAll(set.Bonus4).Select(x => Describe(x, ru))));
            }
            return sb.ToString().TrimEnd();
        }
    }
}
