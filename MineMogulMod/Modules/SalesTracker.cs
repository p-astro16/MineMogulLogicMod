using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace MineMogulMod.Modules
{
    /// <summary>
    /// #18 — Bijhouden van verkoopstatistieken per resource type en per sessie.
    /// Geeft FactoryHUD de data om te tonen.
    /// </summary>
    public class SalesTracker : MonoBehaviour
    {
        public static SalesTracker? Instance { get; private set; }

        // Per ResourceType: totaal verdiend + totaal stuks
        private Dictionary<ResourceType, (float TotalMoney, int Count)> _salesByResource = new();

        // Laatste 60 invoeren voor een "per-minuut" grafiek
        private Queue<(float Time, float Value)> _recentSales = new();

        public float TotalEarned { get; private set; }
        public int TotalItemsSold { get; private set; }

        // Startmoment van de sessie
        private float _sessionStart;

        private void Awake()
        {
            Instance = this;
            _sessionStart = Time.time;
        }

        /// <summary>Registreer een verkoop (aangeroepen via Harmony patch).</summary>
        public void RecordSale(ResourceType resource, PieceType piece, float value)
        {
            TotalEarned += value;
            TotalItemsSold++;

            if (!_salesByResource.TryGetValue(resource, out var entry))
                entry = (0f, 0);
            _salesByResource[resource] = (entry.TotalMoney + value, entry.Count + 1);

            _recentSales.Enqueue((Time.time, value));

            // Schoon oud op (>5 min)
            while (_recentSales.Count > 0 && _recentSales.Peek().Time < Time.time - 300f)
                _recentSales.Dequeue();
        }

        /// <summary>Geld verdiend in de afgelopen N seconden.</summary>
        public float EarnedInLast(float seconds)
        {
            float cutoff = Time.time - seconds;
            return _recentSales.Where(s => s.Time >= cutoff).Sum(s => s.Value);
        }

        public float SessionDurationMinutes => (Time.time - _sessionStart) / 60f;

        public IReadOnlyDictionary<ResourceType, (float TotalMoney, int Count)> SalesByResource => _salesByResource;

        /// <summary>Top N best verdienende resources.</summary>
        public List<(ResourceType Resource, float Money, int Count)> GetTopResources(int n = 5)
        {
            return _salesByResource
                .OrderByDescending(kv => kv.Value.TotalMoney)
                .Take(n)
                .Select(kv => (kv.Key, kv.Value.TotalMoney, kv.Value.Count))
                .ToList();
        }
    }

    // ── Harmony Patches ───────────────────────────────────────────────────────

    [HarmonyPatch(typeof(BaseSellableItem), nameof(BaseSellableItem.SellItem))]
    internal static class BaseSellableItem_SellItem_Patch
    {
        static void Prefix(BaseSellableItem __instance)
        {
            if (SalesTracker.Instance == null) return;
            if (!ModSettings.EnableSalesTracker.Value) return;

            float value = __instance.GetSellValue();
            var ore = __instance.GetComponent<OrePiece>();
            ResourceType resource = ore != null ? ore.ResourceType : ResourceType.INVALID;
            PieceType piece = ore != null ? ore.PieceType : PieceType.INVALID;

            SalesTracker.Instance.RecordSale(resource, piece, value);
        }
    }
}
