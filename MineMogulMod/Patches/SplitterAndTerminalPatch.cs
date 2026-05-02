using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using MineMogulMod.Modules;
using MineMogulMod.Modules.Splitter;
using UnityEngine;

namespace MineMogulMod.Patches
{
    // ── RollerSplitter ratio routing ──────────────────────────────────────────
    [HarmonyPatch(typeof(RollerSplitter), "SetCollisions")]
    internal static class RollerSplitter_SetCollisions_Patch
    {
        static void Prefix(RollerSplitter __instance, GameObject obj, ref bool goStraight)
        {
            var config = __instance.GetComponent<MMLSplitterConfig>()
                      ?? __instance.gameObject.AddComponent<MMLSplitterConfig>();
            SplitterStorage.ApplyToConfig(config);

            var ore = obj.GetComponent<OrePiece>() ?? obj.GetComponentInChildren<OrePiece>(true);
            goStraight = config.DecideShouldGoStraight(
                ore?.ResourceType ?? ResourceType.INVALID,
                ore?.PieceType    ?? PieceType.INVALID);
        }
    }

    // ── ConveyorSplitterT2 ratio routing ──────────────────────────────────────
    [HarmonyPatch(typeof(ConveyorSplitterT2), "OnTriggerEnter")]
    internal static class ConveyorSplitterT2_OnTriggerEnter_Patch
    {
        private static readonly FieldInfo? _rotField =
            typeof(ConveyorSplitterT2).GetField("RotatingThing", BindingFlags.Public | BindingFlags.Instance);
        private static readonly FieldInfo? _minYField =
            typeof(ConveyorSplitterT2).GetField("minY", BindingFlags.Public | BindingFlags.Instance);
        private static readonly FieldInfo? _maxYField =
            typeof(ConveyorSplitterT2).GetField("maxY", BindingFlags.Public | BindingFlags.Instance);

        static void Prefix(ConveyorSplitterT2 __instance, Collider other)
        {
            var config = __instance.GetComponent<MMLSplitterConfig>()
                      ?? __instance.gameObject.AddComponent<MMLSplitterConfig>();
            SplitterStorage.ApplyToConfig(config);

            var ore = other.GetComponent<OrePiece>() ?? other.GetComponentInChildren<OrePiece>(true);
            bool goLeft = config.DecideShouldGoStraight(
                ore?.ResourceType ?? ResourceType.INVALID,
                ore?.PieceType    ?? PieceType.INVALID);

            if (_rotField?.GetValue(__instance) is Transform rt &&
                _minYField?.GetValue(__instance) is float minY &&
                _maxYField?.GetValue(__instance) is float maxY)
            {
                float targetY = goLeft ? minY : maxY;
                var   e       = rt.localEulerAngles;
                rt.localEulerAngles = new Vector3(e.x, targetY, e.z);
            }
        }
    }

    // ── Computer Terminal  →  Factory Overview option ─────────────────────────
    [HarmonyPatch(typeof(ComputerTerminal), "GetInteractions")]
    internal static class ComputerTerminal_GetInteractions_Patch
    {
        private static Interaction? _factoryOverview;

        static void Postfix(List<Interaction> __result)
        {
            if (__result == null) return;
            if (_factoryOverview == null)
            {
                _factoryOverview             = ScriptableObject.CreateInstance<Interaction>();
                _factoryOverview.Name        = "Factory Overview";
                _factoryOverview.Description = "Open the MML Factory HUD (same as F5).";
            }
            if (!__result.Contains(_factoryOverview))
                __result.Add(_factoryOverview);
        }
    }

    [HarmonyPatch(typeof(ComputerTerminal), "Interact")]
    internal static class ComputerTerminal_Interact_Patch
    {
        static void Postfix(Interaction selectedInteraction)
        {
            if (selectedInteraction?.Name == "Factory Overview")
                FactoryHUD.Instance?.ToggleVisibility();
        }
    }

    // ── F7 = spawn Splitter Wrench ────────────────────────────────────────────
    // We use a MonoBehaviour update loop on the existing Plugin instead of
    // a Harmony patch because there is no suitable hook for raw key input.
    // This watcher is attached to the MML_Root GameObject in Plugin.Start.
    public class WrenchSpawnWatcher : MonoBehaviour
    {
        private void Update()
        {
            if (ModSettings.WrenchSpawnKey.Value.IsDown())
                SplitterWrench.SpawnAndGiveToPlayer();
        }
    }
}
