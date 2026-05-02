using BepInEx.Configuration;
using UnityEngine;

namespace MineMogulMod
{
    public static class ModSettings
    {
        // ── Module toggles ─────────────────────────────────────────────────────
        public static ConfigEntry<bool> EnableThroughputTracker  = null!;
        public static ConfigEntry<bool> EnableBottleneckDetector = null!;
        public static ConfigEntry<bool> EnableSalesTracker       = null!;
        public static ConfigEntry<bool> EnableOreAnalyserUpgrade = null!;
        public static ConfigEntry<bool> EnableBeltCounter        = null!;

        // ── Hotkeys ────────────────────────────────────────────────────────────
        public static ConfigEntry<KeyboardShortcut> HUDToggleKey         = null!;
        public static ConfigEntry<KeyboardShortcut> BeltCounterToggleKey = null!;
        public static ConfigEntry<KeyboardShortcut> WrenchSpawnKey       = null!;

        // ── Throughput ─────────────────────────────────────────────────────────
        public static ConfigEntry<float> ThroughputWindowSeconds = null!;

        // ── Belt Counter ───────────────────────────────────────────────────────
        public static ConfigEntry<float> BeltCounterMaxDistance = null!;
        public static ConfigEntry<float> BeltCounterMinIPM      = null!;

        // ── Bottleneck ─────────────────────────────────────────────────────────
        public static ConfigEntry<float> BottleneckRefreshInterval = null!;
        public static ConfigEntry<float> BottleneckMinFactoryIPM   = null!;

        // ── HUD ────────────────────────────────────────────────────────────────
        public static ConfigEntry<float> HUDRefreshInterval = null!;

        // ── Ore Analyser ───────────────────────────────────────────────────────
        public static ConfigEntry<bool> AnalyserShowSalesHistory = null!;

        internal static void Init(ConfigFile cfg)
        {
            const string sMod = "1. Modules";
            EnableThroughputTracker  = cfg.Bind(sMod, "ThroughputTracker",  true, "Measure items/min on every belt.");
            EnableBottleneckDetector = cfg.Bind(sMod, "BottleneckDetector", true, "Flag the slowest belts.");
            EnableSalesTracker       = cfg.Bind(sMod, "SalesTracker",       true, "Track session revenue.");
            EnableOreAnalyserUpgrade = cfg.Bind(sMod, "OreAnalyserUpgrade", true, "Enhanced ore scanner info.");
            EnableBeltCounter        = cfg.Bind(sMod, "BeltCounter",        true, "Floating items/min labels.");

            const string sKeys = "2. Hotkeys";
            HUDToggleKey         = cfg.Bind(sKeys, "HUDToggle",         new KeyboardShortcut(KeyCode.F5), "Toggle Factory HUD.");
            BeltCounterToggleKey = cfg.Bind(sKeys, "BeltCounterToggle", new KeyboardShortcut(KeyCode.F6), "Toggle belt item counter.");
            WrenchSpawnKey       = cfg.Bind(sKeys, "WrenchSpawn",       new KeyboardShortcut(KeyCode.F7), "Spawn Splitter Wrench into inventory.");

            const string sTP = "3. Throughput";
            ThroughputWindowSeconds = cfg.Bind(sTP, "WindowSeconds", 60f,
                new ConfigDescription("Measurement window in seconds.", new AcceptableValueRange<float>(10f, 300f)));

            const string sBC = "4. Belt Counter";
            BeltCounterMaxDistance = cfg.Bind(sBC, "MaxDistance", 30f,
                new ConfigDescription("Max distance (m) to show labels.", new AcceptableValueRange<float>(5f, 100f)));
            BeltCounterMinIPM = cfg.Bind(sBC, "MinItemsPerMin", 0.1f,
                new ConfigDescription("Minimum items/min before label is shown.", new AcceptableValueRange<float>(0f, 50f)));

            const string sBN = "5. Bottleneck";
            BottleneckRefreshInterval = cfg.Bind(sBN, "RefreshInterval", 5f,
                new ConfigDescription("How often (sec) to recalculate.", new AcceptableValueRange<float>(1f, 60f)));
            BottleneckMinFactoryIPM = cfg.Bind(sBN, "MinFactoryIPM", 5f,
                new ConfigDescription("Minimum avg factory throughput before bottleneck detection.", new AcceptableValueRange<float>(0f, 100f)));

            const string sHUD = "6. Factory HUD";
            HUDRefreshInterval = cfg.Bind(sHUD, "RefreshInterval", 3f,
                new ConfigDescription("How often (sec) to refresh data.", new AcceptableValueRange<float>(0.5f, 30f)));

            const string sAN = "7. Ore Analyser";
            AnalyserShowSalesHistory = cfg.Bind(sAN, "ShowSalesHistory", true, "Show session sales in scanner.");
        }
    }
}
