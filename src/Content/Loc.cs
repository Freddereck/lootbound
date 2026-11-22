using System;
using System.Collections.Generic;
using Il2CppInterop.Runtime;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Tables;

namespace MythicAscension.Content
{
    /// Добавляет строки мода в таблицы локализации игры (Unity Localization) прямо в рантайме.
    public static class Loc
    {
        class Entry
        {
            public string Table;   // имя коллекции таблицы, или null — тогда по якорю
            public string Anchor;  // ключ, рядом с которым надо добавить строку (в те же таблицы)
            public string Key;
            public string Ru;
            public string En;
        }

        /// Добавить строку во все таблицы, где уже есть ключ anchor (имя таблицы заранее неизвестно).
        public static void AddBeside(string anchor, string key, string ru, string en)
        {
            if (!Keys.Add("@" + anchor + "|" + key)) return;
            Entries.Add(new Entry { Anchor = anchor, Key = key, Ru = ru, En = en });
            _version++;
        }

        static readonly List<Entry> Entries = new();
        static readonly HashSet<string> Keys = new();
        static int _version;
        static readonly Dictionary<int, int> TableVersion = new();

        public static void Add(string table, string key, string ru, string en)
        {
            if (string.IsNullOrEmpty(table) || string.IsNullOrEmpty(key)) return;
            if (!Keys.Add(table + "|" + key))
            {
                var e = Entries.Find(x => x.Table == table && x.Key == key);
                if (e != null) { e.Ru = ru; e.En = en; }
            }
            else Entries.Add(new Entry { Table = table, Key = key, Ru = ru, En = en });
            _version++;
        }

        public static LocalizedString Make(string table, string key)
        {
            var ls = new LocalizedString();
            ls.TableReference = table;
            ls.TableEntryReference = key;
            return ls;
        }

        /// Вызывается периодически: дописывает строки в новые/перезагруженные таблицы.
        public static void Inject()
        {
            if (Entries.Count == 0) return;
            UnityEngine.Object[] tables;
            try { tables = Resources.FindObjectsOfTypeAll(Il2CppType.Of<StringTable>()); }
            catch { return; }

            foreach (var o in tables)
            {
                var t = o.TryCast<StringTable>();
                if (t == null) continue;
                int id = t.GetInstanceID();
                if (TableVersion.TryGetValue(id, out var v) && v == _version) continue;

                string coll;
                try { coll = t.TableCollectionName; } catch { continue; }
                string code = "";
                try { code = t.LocaleIdentifier.Code ?? ""; } catch { }
                bool ru = code.StartsWith("ru", StringComparison.OrdinalIgnoreCase);

                int added = 0;
                foreach (var e in Entries)
                {
                    if (e.Anchor != null)
                    {
                        bool hasAnchor = false;
                        try { hasAnchor = t.GetEntry(e.Anchor) != null; } catch { }
                        if (!hasAnchor) continue;
                    }
                    else if (e.Table != coll) continue;
                    var text = ru ? e.Ru : e.En;
                    try
                    {
                        var existing = t.GetEntry(e.Key);
                        if (existing != null) { if (existing.Value != text) existing.Value = text; }
                        else { t.AddEntry(e.Key, text); added++; }
                    }
                    catch (Exception ex) { Plugin.L.LogWarning($"[Loc] {coll}/{e.Key}: {ex.Message}"); }
                }
                TableVersion[id] = _version;
                if (added > 0) Plugin.V($"[Loc] {coll} ({code}): +{added} строк");
            }
        }
    }
}
