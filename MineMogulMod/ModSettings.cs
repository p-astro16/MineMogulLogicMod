using BepInEx.Configuration;

namespace MineMogulMod
{
    /// <summary>
    /// Centrale instellingen voor de hele mod.
    /// Worden opgeslagen in BepInEx/config/com.minemogul.mml.cfg
    /// en zijn live aanpasbaar vanuit de in-game HUD.
    /// </summary>
    public static class ModSettings
    {
        // ── Module aan/uit ────────────────────────────────────────────────────
        public static ConfigEntry<bool> EnableThroughputTracker   = null!;
        public static ConfigEntry<bool> EnableBottleneckDetector  = null!;
        public static ConfigEntry<bool> EnableSalesTracker        = null!;
        public static ConfigEntry<bool> EnableOreAnalyserUpgrade  = null!;

        // ── ThroughputTracker instellingen ────────────────────────────────────
        public static ConfigEntry<float> ThroughputWindowSeconds  = null!;

        public static ConfigEntry<float> BeltCounterMaxDistance   = null!;
        public static ConfigEntry<float> BeltCounterMinIPM        = null!;

        // ── BottleneckDetector instellingen ───────────────────────────────────
        public static ConfigEntry<float> BottleneckRefreshInterval = null!;
        public static ConfigEntry<float> BottleneckMinFactoryIPM  = null!;

        // ── FactoryHUD instellingen ───────────────────────────────────────────
        public static ConfigEntry<float> HUDRefreshInterval       = null!;
        public static ConfigEntry<KeyboardShortcut> HUDToggleKey  = null!;
        // ── Ore Analyser instellingen ─────────────────────────────────────────
        public static ConfigEntry<bool> AnalyserShowSalesHistory  = null!;

        internal static void Init(ConfigFile cfg)
        {
            // ── Modules ───────────────────────────────────────────────────────
            const string sectionModules = "1. Modules";
            EnableThroughputTracker  = cfg.Bind(sectionModules, "ThroughputTracker",  true,  "Meet items/min op elke conveyor belt.");
            EnableBottleneckDetector = cfg.Bind(sectionModules, "BottleneckDetector", true,  "Detecteert de traagste schakels in de fabriek.");
            EnableSalesTracker       = cfg.Bind(sectionModules, "SalesTracker",       true,  "Houdt verkoopstatistieken bij per resource type.");
            EnableOreAnalyserUpgrade = cfg.Bind(sectionModules, "OreAnalyserUpgrade", true,  "Verbeterde ore-analyser met waarde, polish en belt info.");

            // ── Throughput ────────────────────────────────────────────────────
            const string sectionTP = "2. Throughput Tracker";
            ThroughputWindowSeconds = cfg.Bind(sectionTP, "WindowSeconds", 60f,
                new ConfigDescription("Tijdvenster (seconden) waarover items/min wordt berekend.", new AcceptableValueRange<float>(10f, 300f)));

            // ── Belt Counter ──────────────────────────────────────────────────
            const string sectionBC = "3. Belt Item Counter";
            BeltCounterMaxDistance = cfg.Bind(sectionBC, "MaxDistance", 30f,
                new ConfigDescription("Max afstand (meters) waarop belt-labels zichtbaar zijn.", new AcceptableValueRange<float>(5f, 100f)));
            BeltCounterMinIPM = cfg.Bind(sectionBC, "MinItemsPerMin", 0.1f,
                new ConfigDescription("Minimale items/min voordat een label getoond wordt.", new AcceptableValueRange<float>(0f, 50f)));

            // ── Bottleneck ────────────────────────────────────────────────────
            const string sectionBN = "4. Bottleneck Detector";
            BottleneckRefreshInterval = cfg.Bind(sectionBN, "RefreshInterval", 5f,
                new ConfigDescription("Hoe vaak (seconden) bottlenecks opnieuw berekend worden.", new AcceptableValueRange<float>(1f, 60f)));
            BottleneckMinFactoryIPM = cfg.Bind(sectionBN, "MinFactoryIPM", 5f,
                new ConfigDescription("Minimale gem. fabriek doorvoer voordat bottlenecks worden gedetecteerd.", new AcceptableValueRange<float>(0f, 100f)));

            // ── HUD ───────────────────────────────────────────────────────────
            const string sectionHUD = "5. Factory HUD";
            HUDRefreshInterval   = cfg.Bind(sectionHUD, "RefreshInterval", 3f,
                new ConfigDescription("Hoe vaak (seconden) de machine-lijst ververst wordt.", new AcceptableValueRange<float>(0.5f, 30f)));
            HUDToggleKey         = cfg.Bind(sectionHUD, "ToggleKey", new KeyboardShortcut(UnityEngine.KeyCode.F5),
                "Toets om de Factory HUD te openen/sluiten.");

            // ── Analyser ──────────────────────────────────────────────────────
            const string sectionAN = "6. Ore Analyser";
            AnalyserShowSalesHistory = cfg.Bind(sectionAN, "ShowSalesHistory", true,
                "Toon sessie-verkoopdata in de ore analyser.");
        }
    }
}
