using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MineMogulMod.Modules
{
    /// <summary>
    /// #4 — Meet hoeveel items per minuut door elke ConveyorBelt gaan.
    /// Andere modules lezen hier uit via GetThroughput(belt).
    /// </summary>
    public class ThroughputTracker : MonoBehaviour
    {
        public static ThroughputTracker? Instance { get; private set; }

        // Per belt: lijst van UTC-tijden waarop een item de belt passeerde
        private Dictionary<ConveyorBelt, Queue<float>> _beltTimestamps = new();

        private void Awake() => Instance = this;

        private void Update()
        {
            if (!ModSettings.EnableThroughputTracker.Value) return;
            float now = Time.time;
            float WindowSeconds = ModSettings.ThroughputWindowSeconds.Value;
            float cutoff = now - WindowSeconds;

            // Opruimen: verwijder oude timestamps
            foreach (var queue in _beltTimestamps.Values)
                while (queue.Count > 0 && queue.Peek() < cutoff)
                    queue.Dequeue();

            // Verwijder belts die niet meer bestaan
            var dead = _beltTimestamps.Keys.Where(b => b == null).ToList();
            foreach (var b in dead) _beltTimestamps.Remove(b);
        }

        /// <summary>Registreer een item-passering op een belt (aangeroepen via Harmony patch).</summary>
        public void RecordItem(ConveyorBelt belt)
        {
            if (belt == null) return;
            if (!_beltTimestamps.TryGetValue(belt, out var queue))
            {
                queue = new Queue<float>();
                _beltTimestamps[belt] = queue;
            }
            queue.Enqueue(Time.time);
        }

        /// <summary>Items per minuut voor een specifieke belt.</summary>
        public float GetThroughput(ConveyorBelt belt)
        {
            if (belt == null || !_beltTimestamps.TryGetValue(belt, out var queue))
                return 0f;

            float windowSeconds = ModSettings.ThroughputWindowSeconds.Value;
            float elapsed = Mathf.Min(Time.time, windowSeconds);
            if (elapsed <= 0f) return 0f;
            return queue.Count / elapsed * 60f;
        }

        /// <summary>Geef alle belts terug met hun throughput (items/min), gesorteerd laag→hoog.</summary>
        public List<(ConveyorBelt belt, float itemsPerMin)> GetAllThroughputs()
        {
            return _beltTimestamps
                .Where(kv => kv.Key != null)
                .Select(kv => (kv.Key, GetThroughput(kv.Key)))
                .OrderBy(t => t.Item2)
                .ToList();
        }
    }
}
