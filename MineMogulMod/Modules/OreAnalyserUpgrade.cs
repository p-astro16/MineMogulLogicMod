using System.Text;
using HarmonyLib;
using UnityEngine;

namespace MineMogulMod.Modules
{
    /// <summary>
    /// #17 — Verbetert de ingebouwde ore-analyser (ToolResourceScanner).
    /// Origineel toont hij alleen de naam. Nu toont hij:
    ///   - Resource type + piece type
    ///   - Verkoopwaarde (basis + gepolijst)
    ///   - Polish %
    ///   - Of het een bottleneck-belt is
    /// </summary>
    [HarmonyPatch(typeof(ToolResourceScanner), "GetThingNameText")]
    internal static class OreAnalyserUpgrade_Patch
    {
        static bool Prefix(ToolResourceScanner __instance, GameObject thing, ref string __result)
        {
            if (!ModSettings.EnableOreAnalyserUpgrade.Value) return true;
            if (thing == null) return true; // val terug op origineel

            var sb = new StringBuilder();

            // ── OrePiece info ──────────────────────────────────────────────
            var ore = thing.GetComponentInParent<OrePiece>();
            if (ore != null)
            {
                float baseValue   = ore.BaseSellValue;
                float sellValue   = ore.GetSellValue();
                float polished    = ore.PolishedPercent * 100f;
                float randomMult  = ore.RandomPriceMultiplier;

                sb.AppendLine($"<b>{ore.ResourceType}  —  {ore.PieceType}</b>");
                sb.AppendLine($"Waarde:       €{sellValue:N2}");
                sb.AppendLine($"Basiswaarde:  €{baseValue:N2}");
                if (randomMult != 1f)
                    sb.AppendLine($"Prijsmult.:   x{randomMult:F2}");
                if (ore.IsPolished)
                    sb.AppendLine($"✓ Gepolijst ({polished:F0}%)");
                else if (polished > 0f)
                    sb.AppendLine($"Polish: {polished:F0}%");

                // Verkoop-statistieken voor dit resource-type
                if (ModSettings.AnalyserShowSalesHistory.Value)
                {
                    var salesData = SalesTracker.Instance?.SalesByResource;
                    if (salesData != null && salesData.TryGetValue(ore.ResourceType, out var entry))
                        sb.AppendLine($"Sessie: {entry.Count}x  |  €{entry.TotalMoney:N0} totaal");
                }

                __result = sb.ToString().TrimEnd();
                return false;
            }

            // ── ConveyorBelt info ─────────────────────────────────────────
            var belt = thing.GetComponentInParent<ConveyorBelt>();
            if (belt != null)
            {
                float ipm = ThroughputTracker.Instance?.GetThroughput(belt) ?? 0f;
                bool isBottleneck = BottleneckDetector.Instance?.IsBottleneck(belt) ?? false;

                sb.AppendLine($"<b>Conveyor Belt</b>");
                sb.AppendLine($"Snelheid:    {belt.Speed:F2} m/s");
                sb.AppendLine($"Doorvoer:    {ipm:F1} items/min");
                sb.AppendLine($"Items op belt: {ReflectionUtils.GetPhysicsObjectCount(belt)}");
                if (isBottleneck)
                    sb.AppendLine("<color=red>⚠ BOTTLENECK gedetecteerd!</color>");

                __result = sb.ToString().TrimEnd();
                return false;
            }

            // ── SorterMachine info ────────────────────────────────────────
            var sorter = thing.GetComponentInParent<SorterMachine>();
            if (sorter != null)
            {
                int filters = sorter.Filter != null ? ReflectionUtils.GetFilterCriteriaCount(sorter.Filter) : 0;
                sb.AppendLine($"<b>Sorter Machine</b>");
                sb.AppendLine($"Flowrate: {sorter.BaseFlowRate:F2}");
                sb.AppendLine($"Actieve filters: {filters}");
                sb.AppendLine("Tip: open HUD (F5) → Sorter Presets");

                __result = sb.ToString().TrimEnd();
                return false;
            }

            return true; // Geen bekende component → origineel gedrag
        }
    }
}
