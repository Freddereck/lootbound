using System;
using System.Collections.Generic;
using System.Linq;
using MythicAscension.Content;
using Services.CharacterSystem;
using Services.CharacterSystem.Stats;

namespace MythicAscension.Mechanics
{
    /// Состояние текущего боя и применение боевых эффектов.
    public static class Combat
    {
        static readonly Dictionary<IntPtr, string> CurrentAbility = new();   // персонаж → ID способности
        static readonly HashSet<IntPtr> HasActed = new();                    // уже совершал действие в этом бою
        static readonly HashSet<IntPtr> FirstActionActive = new();           // идёт первое действие в бою
        static readonly Dictionary<IntPtr, int> FuryStacks = new();
        static readonly Dictionary<IntPtr, Character> LastHitter = new();    // жертва → кто бил последним
        static readonly HashSet<IntPtr> Dead = new();
        static readonly System.Random Rng = new();

        /// Источник модификаторов статов сетов (чтобы снимать только свои).
        static Il2CppSystem.Object _setSource;
        static Il2CppSystem.Object SetSource => _setSource ??= new Il2CppSystem.Object();

        public static void Reset()
        {
            CurrentAbility.Clear();
            HasActed.Clear();
            FirstActionActive.Clear();
            FuryStacks.Clear();
            LastHitter.Clear();
            Dead.Clear();
        }

        // ---------- Начало боя: бонусы сетов ----------

        public static void OnCombatStart(IEnumerable<Character> team)
        {
            Reset();
            foreach (var c in team)
            {
                if (c == null) continue;
                try { ApplySetStats(c); }
                catch (Exception e) { Plugin.L.LogWarning($"[Sets] {c.Type}: {e.Message}"); }
            }
        }

        static void ApplySetStats(Character c)
        {
            var g = Gear.Summarize(c);
            var sc = c.StatsController;
            if (sc == null) return;
            sc.ClearModifiersBySource(SetSource);
            foreach (var e in g.All)
            {
                float flat;
                if (e.Kind == EffectKind.Stat) flat = e.A;
                else if (e.Kind == EffectKind.StatPct) flat = c.GetStatValue(e.Stat) * e.A / 100f;
                else continue;
                if (Math.Abs(flat) < 0.01f) continue;
                sc.AddFlatStatModifier(e.Stat, SetSource, flat, ModifierScope.Fight, 0, false);
                Plugin.V($"[Sets] {c.Type}: +{flat:0.#} {e.Stat} ({e.Source})");
            }
            foreach (var kv in g.SetCounts)
                if (kv.Value >= 2) Plugin.V($"[Sets] {c.Type}: сет {kv.Key} — {kv.Value} предм.");
        }

        // ---------- Действия ----------

        public static void OnAction(Character performer, string abilityId)
        {
            if (performer == null || performer.IsEnemy) return;
            var p = performer.Pointer;
            CurrentAbility[p] = abilityId;
            if (HasActed.Add(p)) FirstActionActive.Add(p);
            else FirstActionActive.Remove(p);
        }

        // ---------- Урон ----------

        public static void ModifyDamage(Character target, DamageOptions opt)
        {
            var performer = opt.Performer;
            if (performer == null || target == null) return;
            if (!performer.IsEnemy) LastHitter[target.Pointer] = performer;
            if (performer.IsEnemy || opt.IsConsumable || opt.Amount <= 0) return;

            var g = Gear.Summarize(performer);
            if (g.Pieces.Count == 0) return;
            var p = performer.Pointer;

            int pct = 0;
            var parts = new List<string>();
            void Add(int v, string why) { if (v == 0) return; pct += v; parts.Add($"{why}+{v}%"); }

            Add(g.MythicMight(), "Might");
            Add(g.Sum(EffectKind.Dmg), "Dmg");

            if (CurrentAbility.TryGetValue(p, out var ab) && ab != null)
                Add(g.All.Where(e => e.Kind == EffectKind.Skill && Array.IndexOf(e.Abilities, ab) >= 0).Sum(e => e.A), "Skill");

            if (FirstActionActive.Contains(p)) Add(g.Sum(EffectKind.First), "First");

            if (g.Has(EffectKind.Exec))
            {
                float maxHp = target.GetStatValue(StatType.MaxHP);
                float hp = target.GetAttributeValue(AttributeType.HP);
                if (maxHp > 0 && hp / maxHp < Effects.ExecThreshold) Add(g.Sum(EffectKind.Exec), "Exec");
            }

            if (FuryStacks.TryGetValue(p, out var stacks) && stacks > 0)
                Add(g.Sum(EffectKind.Fury) * stacks, "Fury");

            int pierce = g.Sum(EffectKind.Pierce);
            bool pierced = false;
            if (pierce > 0 && !opt.IgnoreArmor && Rng.Next(100) < pierce)
            {
                opt.IgnoreArmor = true;
                pierced = true;
            }

            if (pct != 0)
            {
                int before = opt.Amount;
                opt.Amount = Math.Max(1, (int)Math.Round(before * (1f + pct / 100f)));
                Plugin.V($"[Dmg] {performer.Type} [{ab}] → {target.Id}: {before} → {opt.Amount} ({string.Join(", ", parts)}){(pierced ? " +Pierce" : "")}");
            }
            else if (pierced) Plugin.V($"[Dmg] {performer.Type} → {target.Id}: Pierce");
        }

        // ---------- Смерть ----------

        static int KillCredit(Character victim) => victim.Rank switch
        {
            BattlerRank.Guardian => 2,
            BattlerRank.Elite => 3,
            BattlerRank.Boss => 10,
            _ => 1,
        };

        public static void OnDeath(Character victim)
        {
            if (victim == null || !victim.IsEnemy) return;
            if (!Dead.Add(victim.Pointer)) return;
            if (!LastHitter.TryGetValue(victim.Pointer, out var killer) || killer == null) return;

            var g = Gear.Summarize(killer);
            if (g.Pieces.Count == 0) return;
            int credit = KillCredit(victim);
            Plugin.V($"[Kill] {killer.Type} убил {victim.Id} ({victim.Rank}, +{credit})");

            if (g.Has(EffectKind.Fury))
                FuryStacks[killer.Pointer] = Math.Min(Effects.FuryMaxStacks,
                    (FuryStacks.TryGetValue(killer.Pointer, out var s) ? s : 0) + 1);

            int heal = g.Sum(EffectKind.KillHeal);
            if (heal > 0 && killer.IsAlive)
            {
                int amount = (int)Math.Round(killer.GetStatValue(StatType.MaxHP) * heal / 100f);
                if (amount > 0) killer.HandleRestoreResource(amount, AttributeType.HP);
            }
            int mana = g.Sum(EffectKind.KillMana);
            if (mana > 0 && killer.IsAlive) killer.HandleRestoreResource(mana, AttributeType.MP);

            if (Growth.AddKills(g, credit))
            {
                try { killer.RefreshAllItemsStats(); }
                catch (Exception e) { Plugin.L.LogWarning("[Growth] refresh: " + e.Message); }
            }
            Growth.Flush();
        }
    }
}
