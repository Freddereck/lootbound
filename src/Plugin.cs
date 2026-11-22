using System.IO;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using MythicAscension.Content;

namespace MythicAscension
{
    [BepInPlugin(Guid, Name, Version)]
    public class Plugin : BasePlugin
    {
        public const string Guid = "mythicascension.lootbound";
        public const string Name = "Mythic Ascension";
        public const string Version = "0.5.0";

        internal static ManualLogSource L;
        internal static Plugin Instance;
        internal static string PluginDir;

        internal static ConfigEntry<bool> DumpData;
        internal static ConfigEntry<bool> DebugForceMythic;
        internal static ConfigEntry<bool> ExtraRotation;
        internal static ConfigEntry<bool> VerboseLog;
        internal static ConfigEntry<float> EliteMythicChance;
        internal static ConfigEntry<float> BattleMythicChance;
        internal static ConfigEntry<float> ChestMythicChance;
        internal static ConfigEntry<float> MerchantMythicChance;
        internal static ConfigEntry<int> MerchantMythicExtraPrice;
        internal static ConfigEntry<float> MythicPriceMultiplier;
        internal static ConfigEntry<int> MythicEnhanceSlots;
        internal static ConfigEntry<int> MythicMightPerItem;
        internal static ConfigEntry<int> MythicMightCap;

        public override void Load()
        {
            Instance = this;
            L = Log;
            PluginDir = Path.GetDirectoryName(typeof(Plugin).Assembly.Location);

            EliteMythicChance = Config.Bind("Drops", "EliteMythicChance", 7f, "Шанс (%), что карта награды за элитный бой станет мифической.");
            BattleMythicChance = Config.Bind("Drops", "BattleMythicChance", 0f, "Шанс (%) мифика в награде за обычный бой.");
            ChestMythicChance = Config.Bind("Drops", "ChestMythicChance", 10f, "Шанс (%), что Divine-предмет в сундуке/на отдыхе заменится мификом.");
            MerchantMythicChance = Config.Bind("Drops", "MerchantMythicChance", 25f, "Шанс (%), что Divine-товар у торговца заменится мификом.");
            MerchantMythicExtraPrice = Config.Bind("Drops", "MerchantMythicExtraPrice", 3, "Насколько мифик у торговца дороже Divine (в идолах).");
            MythicPriceMultiplier = Config.Bind("Balance", "MythicPriceMultiplier", 2.5f, "Множитель цены мифика относительно Divine.");
            MythicEnhanceSlots = Config.Bind("Balance", "MythicEnhanceSlots", 3, "Слотов усиления у мифических предметов.");
            MythicMightPerItem = Config.Bind("Balance", "MythicMightPerItem", 4, "Мифическая мощь: % урона за каждый надетый мифик.");
            MythicMightCap = Config.Bind("Balance", "MythicMightCap", 20, "Мифическая мощь: максимум %.");
            ExtraRotation = Config.Bind("Controls", "ExtraRotation", true, "Shift + поворот = 180°, Ctrl + поворот = 90° против часовой.");
            DebugForceMythic = Config.Bind("Debug", "ForceMythicRewards", false, "ТЕСТ: все награды за бои/сундуки/торговец — мифические.");
            VerboseLog = Config.Bind("Debug", "VerboseLog", true, "Подробный лог мода.");
            DumpData = Config.Bind("Debug", "DumpGameData", false, "Выгружать данные игры в BepInEx/MythicDump.");

            ItemDefs.Load(Path.Combine(PluginDir, "items.json"));

            AddComponent<ModRunner>();
            var h = new Harmony(Guid);
            h.PatchAll(typeof(Plugin).Assembly);
            foreach (var m in h.GetPatchedMethods())
                if (VerboseLog.Value) L.LogInfo($"[Harmony] patched {m.DeclaringType?.Name}.{m.Name}");
            L.LogInfo($"{Name} {Version} loaded");
        }

        internal static void V(string msg)
        {
            if (VerboseLog.Value) L.LogInfo(msg);
        }
    }
}
