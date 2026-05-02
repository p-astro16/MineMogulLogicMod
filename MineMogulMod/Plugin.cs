using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using MineMogulMod.Modules;
using MineMogulMod.Modules.Splitter;
using UnityEngine;

namespace MineMogulMod
{
    [BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        internal static new ManualLogSource Logger = null!;
        internal static Plugin Instance = null!;

        private Harmony _harmony = null!;
        private GameObject _modObject = null!;

        private void Awake()
        {
            Instance = this;
            Logger = base.Logger;

            ModSettings.Init(Config);

            _harmony = new Harmony(MyPluginInfo.PLUGIN_GUID);
            _harmony.PatchAll();

            Logger.LogInfo($"{MyPluginInfo.PLUGIN_NAME} v{MyPluginInfo.PLUGIN_VERSION} geladen!");
            Logger.LogInfo("In-game: F5 = MML HUD  |  F6 = Belt counters");
        }

        private void Start()
        {
            // Maak een persistent GameObject aan voor alle mod-componenten
            _modObject = new GameObject("MineMogulMod_Root");
            DontDestroyOnLoad(_modObject);

            _modObject.AddComponent<ThroughputTracker>();
            _modObject.AddComponent<SalesTracker>();
            _modObject.AddComponent<FactoryHUD>();
            _modObject.AddComponent<BottleneckDetector>();
            _modObject.AddComponent<SplitterConfigUI>();
            SplitterStorage.Load();
        }

        private void OnDestroy()
        {
            _harmony.UnpatchSelf();
        }
    }
}
