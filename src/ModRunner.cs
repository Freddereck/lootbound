using System;
using System.Collections.Generic;
using MythicAscension.Content;
using UnityEngine;

namespace MythicAscension
{
    /// Постоянный объект мода: ждёт загрузки конфигов игры, периодически досинхронизирует контент
    /// и выполняет отложенные действия (через N кадров) — для UI, который игра достраивает позже.
    public class ModRunner : MonoBehaviour
    {
        public ModRunner(IntPtr ptr) : base(ptr) { }

        float _next;
        bool _rarityLocAdded;

        static readonly List<(int frame, Action action)> Deferred = new();

        /// Выполнить действие через указанное число кадров.
        public static void Later(int frames, Action a) => Deferred.Add((Time.frameCount + Math.Max(1, frames), a));

        void Update()
        {
            if (Deferred.Count > 0)
            {
                int now = Time.frameCount;
                for (int i = Deferred.Count - 1; i >= 0; i--)
                {
                    if (Deferred[i].frame > now) continue;
                    var a = Deferred[i].action;
                    Deferred.RemoveAt(i);
                    Step("later", a);
                }
            }

            if (Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + 1.5f;

            Step("content", ContentInjector.Tick);
            Step("rarity", Mythic.RegisterInConfigs);
            if (!_rarityLocAdded)
            {
                _rarityLocAdded = true;
                Loc.AddBeside("RARITY_DIVINE", Mythic.RarityKey, "Мифический", "Mythic");
            }
            Step("loc", Loc.Inject);
            Step("expansion", Mechanics.Expansion.Tick);
            if (Plugin.DumpData.Value) Step("dump", DataDumper.TryDump);
        }

        static void Step(string name, Action a)
        {
            try { a(); }
            catch (Exception e) { Plugin.L.LogError($"[{name}] {e}"); }
        }
    }
}
