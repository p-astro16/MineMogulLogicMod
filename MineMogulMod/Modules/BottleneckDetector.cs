using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MineMogulMod.Modules
{
    /// <summary>
    /// #16 — Detecteert de traagste belts/machines in de fabriek.
    /// Een belt wordt een "bottleneck" als:
    ///   - Zijn throughput lager is dan het gemiddelde - 1 standaarddeviatie
    ///   - EN er minstens 5 items/min actief zijn in de fabriek (niet idle)
    /// </summary>
    public class BottleneckDetector : MonoBehaviour
    {
        public static BottleneckDetector? Instance { get; private set; }

        private HashSet<ConveyorBelt> _bottlenecks = new();

        private float _nextRefresh;

        private void Awake() => Instance = this;

        private void Update()
        {
            if (!ModSettings.EnableBottleneckDetector.Value) { _bottlenecks.Clear(); return; }
            float interval = ModSettings.BottleneckRefreshInterval.Value;
            if (Time.time >= _nextRefresh)
            {
                _nextRefresh = Time.time + interval;
                RecalculateBottlenecks();
            }
        }

        private void RecalculateBottlenecks()
        {
            _bottlenecks.Clear();

            var tracker = ThroughputTracker.Instance;
            if (tracker == null) return;

            var all = tracker.GetAllThroughputs()
                .Where(t => t.belt != null)
                .ToList();

            if (all.Count < 2) return;

            // Reken gemiddelde en standaarddeviatie
            float avg = all.Average(t => t.itemsPerMin);
            float variance = all.Average(t => (t.itemsPerMin - avg) * (t.itemsPerMin - avg));
            float stdDev = Mathf.Sqrt(variance);

            float threshold = avg - stdDev;

            // Alleen als fabriek actief is
            if (avg < ModSettings.BottleneckMinFactoryIPM.Value) return;

            foreach (var (belt, ipm) in all)
            {
                if (ipm < threshold && ipm < avg * 0.5f)
                    _bottlenecks.Add(belt);
            }

            Plugin.Logger.LogInfo($"[BottleneckDetector] {_bottlenecks.Count} bottleneck(s) gevonden. " +
                                  $"Gem: {avg:F1}, Drempel: {threshold:F1} items/min");
        }

        public bool IsBottleneck(ConveyorBelt? belt) =>
            belt != null && _bottlenecks.Contains(belt);

        /// <summary>Geef de N ergste bottlenecks terug, gesorteerd van ergst naar minder erg.</summary>
        public List<(ConveyorBelt belt, float itemsPerMin)> GetWorstBottlenecks(int n = 5)
        {
            var tracker = ThroughputTracker.Instance;
            if (tracker == null) return new();

            return _bottlenecks
                .Where(b => b != null)
                .Select(b => (b, tracker.GetThroughput(b)))
                .OrderBy(t => t.Item2)
                .Take(n)
                .ToList();
        }
    }
}
