using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using MineMogulMod.Modules;
using MineMogulMod.Modules.Splitter;
using UnityEngine;

namespace MineMogulMod.Patches
{
    // RollerSplitter ratio routing
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

    // ConveyorSplitterT2 ratio routing via RotatingThing angle
    [HarmonyPatch(typeof(ConveyorSplitterT2), "OnTriggerEnter")]
    internal static class ConveyorSplitterT2_OnTriggerEnter_Patch
    {
        private static readonly System.Reflection.FieldInfo? _rotField =
            typeof(ConveyorSplitterT2).GetField("RotatingThing",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        private static readonly System.Reflection.FieldInfo? _minYField =
            typeof(ConveyorSplitterT2).GetField("minY",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        private static readonly System.Reflection.FieldInfo? _maxYField =
            typeof(ConveyorSplitterT2).GetField("maxY",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

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
                var e = rt.localEulerAngles;
                rt.localEulerAngles = new Vector3(e.x, targetY, e.z);
            }
        }
    }

    // ComputerTerminal Factory Overview
    [HarmonyPatch(typeof(ComputerTerminal), "GetInteractions")]
    internal static class ComputerTerminal_GetInteractions_Patch
    {
        private static Interaction? _interaction;
        static void Postfix(List<Interaction> __result)
        {
            if (__result == null) return;
            if (_interaction == null)
            {
                _interaction = ScriptableObject.CreateInstance<Interaction>();
                _interaction.Name = "Factory Overview";
                _interaction.Description = "Open the MML Factory HUD (same as F5).";
            }
            if (!__result.Contains(_interaction)) __result.Add(_interaction);
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

    // Shop injection
    internal static class SplitterShopInjector
    {
        internal static ShopItemDefinition? ShopItemDef;
    }

    internal static class BeltScannerShopInjector
    {
        internal static ShopItemDefinition? ShopItemDef;
    }

    // Patch SetupCategories (runs AFTER Start, populates _categoryButtons)
    [HarmonyPatch(typeof(ComputerShopUI), "SetupCategories")]
    internal static class ComputerShopUI_SetupCategories_Patch
    {
        static void Postfix(ComputerShopUI __instance)
        {
            try
            {
                var field = AccessTools.Field(typeof(ComputerShopUI), "_categoryButtons");
                var buttons = field?.GetValue(__instance) as HashSet<ShopCategoryButton>;
                if (buttons == null || buttons.Count == 0)
                {
                    Plugin.Logger.LogWarning("[MML] _categoryButtons is null/empty after SetupCategories.");
                    return;
                }

                // Log all category names once so we can verify the name in logs
                var allCategories = buttons
                    .Select(b => b?.ShopCategory)
                    .Where(c => c != null)
                    .ToList();
                Plugin.Logger.LogInfo($"[MML] Shop categories: {string.Join(", ", allCategories.Select(c => $"\"{c!.CategoryName}\""))}");

                // Find Tools category — fall back to first non-trophy, non-holiday category
                var toolsCategory = allCategories.FirstOrDefault(c =>
                    c!.CategoryName != null &&
                    c.CategoryName.IndexOf("Tool", StringComparison.OrdinalIgnoreCase) >= 0);
                toolsCategory ??= allCategories.FirstOrDefault(c =>
                    c != null && !c.IsTrophyCategory && !c.IsSandboxOnlyCategory);
                if (toolsCategory == null)
                {
                    Plugin.Logger.LogWarning("[MML] No suitable shop category found for Splitter Wrench.");
                    return;
                }

                if (SplitterShopInjector.ShopItemDef == null)
                {
                    var dummyGO = new GameObject("MML_WrenchDummy");
                    UnityEngine.Object.DontDestroyOnLoad(dummyGO);
                    dummyGO.SetActive(false);
                    var def = ScriptableObject.CreateInstance<ShopItemDefinition>();
                    def.Name = "Splitter Wrench";
                    def.Description = "Configure splitter ratios. LMB targets a splitter, RMB opens all splitters.";
                    def.Price = 0;
                    def.IsDummyItem = true;
                    def.PrefabToSpawn = dummyGO;
                    SplitterShopInjector.ShopItemDef = def;
                }

                if (!toolsCategory.ShopItemDefinitions.Contains(SplitterShopInjector.ShopItemDef))
                {
                    toolsCategory.ShopItemDefinitions.Add(SplitterShopInjector.ShopItemDef);
                    Plugin.Logger.LogInfo($"[MML] Splitter Wrench injected into \"{toolsCategory.CategoryName}\".");
                }

                // Belt Scanner
                if (BeltScannerShopInjector.ShopItemDef == null)
                {
                    var dummyGO2 = new GameObject("MML_BeltScannerDummy");
                    UnityEngine.Object.DontDestroyOnLoad(dummyGO2);
                    dummyGO2.SetActive(false);
                    var def2 = ScriptableObject.CreateInstance<ShopItemDefinition>();
                    def2.Name = "Belt Scanner";
                    def2.Description = "Equip to see live items/min labels floating above nearby conveyor belts.";
                    def2.Price = 0;
                    def2.IsDummyItem = true;
                    def2.PrefabToSpawn = dummyGO2;
                    BeltScannerShopInjector.ShopItemDef = def2;
                }

                if (!toolsCategory.ShopItemDefinitions.Contains(BeltScannerShopInjector.ShopItemDef))
                {
                    toolsCategory.ShopItemDefinitions.Add(BeltScannerShopInjector.ShopItemDef);
                    Plugin.Logger.LogInfo($"[MML] Belt Scanner injected into \"{toolsCategory.CategoryName}\".");
                }

                // Always refresh the displayed list
                AccessTools.Method(typeof(ComputerShopUI), "RepopulateShopItemList")?.Invoke(__instance, null);
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogWarning("[MML] Shop injection skipped: " + ex.Message);
            }
        }
    }

    [HarmonyPatch(typeof(ComputerShopUI), "TrySpawnItem")]
    internal static class ComputerShopUI_TrySpawnItem_Patch
    {
        static bool Prefix(ShopItemDefinition item, int quantity)
        {
            if (item != null && item == SplitterShopInjector.ShopItemDef)
            {
                SplitterWrench.SpawnAndGiveToPlayer();
                return false;
            }
            if (item != null && item == BeltScannerShopInjector.ShopItemDef)
            {
                BeltScannerTool.SpawnAndGiveToPlayer();
                return false;
            }
            return true;
        }
    }
}