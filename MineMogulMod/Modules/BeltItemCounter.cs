using System.Collections.Generic;
using UnityEngine;

namespace MineMogulMod.Modules
{
    /// <summary>
    /// Belt Item Counter — F6 toggles floating items/min labels above nearby belts.
    /// Simple MonoBehaviour, no shop integration needed.
    /// </summary>
    public class BeltItemCounter : MonoBehaviour
    {
        public static BeltItemCounter? Instance { get; private set; }

        private bool           _active;
        private Camera?        _cam;
        private GUIStyle?      _style;
        private GUIStyle?      _shadowStyle;
        private float          _nextRefresh;
        private readonly List<ConveyorBelt> _nearbyBelts = new();

        private void Awake() => Instance = this;

        private void Update()
        {
            if (ModSettings.BeltCounterToggleKey.Value.IsDown())
                _active = !_active;

            if (!_active || !ModSettings.EnableBeltCounter.Value) return;

            _cam ??= Camera.main;

            if (Time.time >= _nextRefresh)
            {
                _nextRefresh = Time.time + 1f;
                RefreshNearbyBelts();
            }
        }

        private void RefreshNearbyBelts()
        {
            _nearbyBelts.Clear();
            _cam ??= Camera.main;
            if (_cam == null) return;

            float maxDist = ModSettings.BeltCounterMaxDistance.Value;
            float maxDistSq = maxDist * maxDist;

            Vector3 camPos = _cam.transform.position;

#pragma warning disable CS0618
            var allBelts = FindObjectsOfType<ConveyorBelt>();
#pragma warning restore CS0618

            foreach (var belt in allBelts)
            {
                if (belt == null) continue;
                if ((belt.transform.position - camPos).sqrMagnitude <= maxDistSq)
                    _nearbyBelts.Add(belt);
            }
        }

        private void OnGUI()
        {
            if (!_active || !ModSettings.EnableBeltCounter.Value) return;
            _cam ??= Camera.main;
            if (_cam == null) return;

            EnsureStyles();

            var tracker = ThroughputTracker.Instance;
            if (tracker == null) return;

            foreach (var belt in _nearbyBelts)
            {
                if (belt == null) continue;

                float ipm = tracker.GetThroughput(belt);
                if (ipm < ModSettings.BeltCounterMinIPM.Value) continue;

                bool isBottleneck = BottleneckDetector.Instance?.IsBottleneck(belt) ?? false;

                Vector3 worldPos = belt.transform.position + Vector3.up * 0.7f;
                Vector3 screen   = _cam.WorldToScreenPoint(worldPos);

                if (screen.z <= 0f) continue; // behind camera

                float sx = screen.x;
                float sy = Screen.height - screen.y; // flip Y for GUI

                const float W = 80f, H = 18f;
                var rect   = new Rect(sx - W / 2f, sy - H / 2f, W, H);
                var shadow = new Rect(rect.x + 1, rect.y + 1, rect.width, rect.height);

                Color col = isBottleneck ? new Color(1f, 0.25f, 0.25f)
                          : ipm < 20f   ? new Color(1f, 0.80f, 0.20f)
                                        : new Color(0.25f, 0.95f, 0.45f);

                string label = $"{ipm:F1} ipm";

                _style!.normal.textColor   = col;
                _shadowStyle!.normal.textColor = new Color(0, 0, 0, 0.7f);

                GUI.Label(shadow, label, _shadowStyle);
                GUI.Label(rect,   label, _style);
            }
        }

        private void EnsureStyles()
        {
            if (_style != null) return;
            _style = new GUIStyle(GUI.skin.label)
            {
                fontSize  = 12,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
            };
            _shadowStyle = new GUIStyle(_style);
        }
    }
}
