using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using MineMogulMod.Modules;
using MineMogulMod.Modules.Splitter;
using MineMogulMod.Patches;
using UnityEngine;

namespace MineMogulMod
{
    [BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        internal static new ManualLogSource Logger = null!;
        internal static Plugin Instance = null!;

        private Harmony _harmony = null!;

        private void Awake()
        {
            Instance = this;
            Logger   = base.Logger;

            ModSettings.Init(Config);

            _harmony = new Harmony(MyPluginInfo.PLUGIN_GUID);
            _harmony.PatchAll();

            Logger.LogInfo($"[MML] {MyPluginInfo.PLUGIN_NAME} v{MyPluginInfo.PLUGIN_VERSION} loaded.");
            Logger.LogInfo("[MML] F5 = Factory HUD  |  F6 = Belt Counter  |  F7 = Spawn Splitter Wrench");
        }

        private void Start()
        {
            var root = new GameObject("MML_Root");
            DontDestroyOnLoad(root);

            AddSafe<ThroughputTracker>  (root, "ThroughputTracker");
            AddSafe<SalesTracker>       (root, "SalesTracker");
            AddSafe<BottleneckDetector> (root, "BottleneckDetector");
            AddSafe<BeltItemCounter>    (root, "BeltItemCounter");
            AddSafe<FactoryHUD>         (root, "FactoryHUD");
            AddSafe<SplitterConfigUI>   (root, "SplitterConfigUI");
            AddSafe<WrenchSpawnWatcher> (root, "WrenchSpawnWatcher");

            try { SplitterStorage.Load(); }
            catch (System.Exception ex) { Logger.LogWarning("[MML] SplitterStorage.Load failed: " + ex.Message); }
        }

        private static void AddSafe<T>(GameObject root, string name) where T : MonoBehaviour
        {
            try
            {
                root.AddComponent<T>();
                Logger.LogInfo($"[MML] {name} OK");
            }
            catch (System.Exception ex)
            {
                Logger.LogWarning($"[MML] {name} failed to initialise: {ex.Message}");
            }
        }

        private void OnDestroy()
        {
            _harmony.UnpatchSelf();
        }
    }
}
