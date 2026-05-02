using System.Collections.Generic;
using UnityEngine;

namespace MineMogulMod.Modules
{
    /// <summary>
    /// Belt Item Counter
    /// ─ Crosshair raycast: detail panel (rechtsonder) van de belt waar je naar kijkt.
    /// ─ F6: toggle global floating labels op alle naburige belts.
    /// </summary>
    public class BeltItemCounter : MonoBehaviour
    {
        public static BeltItemCounter? Instance { get; private set; }

        // ── State ──────────────────────────────────────────────────────────────
        private bool           _globalActive;
        private Camera?        _cam;
        private GUIStyle?      _style;
        private GUIStyle?      _shadowStyle;
        private GUIStyle?      _labelBold;
        private GUIStyle?      _labelMuted;

        private float          _nextRefresh;
        private readonly List<ConveyorBelt> _nearbyBelts = new();

        // currently-aimed belt (crosshair raycast)
        private ConveyorBelt?  _aimedBelt;
        private float          _aimedRefresh;

        private const float RAY_RANGE    = 30f;
        private const float DETAIL_W     = 220f;
        private const float DETAIL_H     = 120f;
        private const float PANEL_MARGIN = 16f;

        private void Awake() => Instance = this;

        // ── Update ─────────────────────────────────────────────────────────────
        private void Update()
        {
            if (!ModSettings.EnableBeltCounter.Value) return;

            // F6 = toggle global labels
            if (ModSettings.BeltCounterToggleKey.Value.IsDown())
                _globalActive = !_globalActive;

            _cam ??= Camera.main;
            if (_cam == null) return;

            // Crosshair raycast elke 0.15s
            if (Time.time >= _aimedRefresh)
            {
                _aimedRefresh = Time.time + 0.15f;
                UpdateAimedBelt();
            }

            // Global list refresh elke 1s
            if (_globalActive && Time.time >= _nextRefresh)
            {
                _nextRefresh = Time.time + 1f;
                RefreshNearbyBelts();
            }
        }

        // ── Aimed belt via crosshair ───────────────────────────────────────────
        private void UpdateAimedBelt()
        {
            _aimedBelt = null;
            if (_cam == null) return;

            Ray ray = new Ray(_cam.transform.position, _cam.transform.forward);
            if (!Physics.Raycast(ray, out var hit, RAY_RANGE)) return;

            // Loop door hierarchy om ConveyorBelt te vinden
            var t = hit.collider.transform;
            while (t != null)
            {
                if (t.TryGetComponent<ConveyorBelt>(out var belt))
                {
                    _aimedBelt = belt;
                    return;
                }
                t = t.parent;
            }
        }

        // ── Global nearby belts ────────────────────────────────────────────────
        private void RefreshNearbyBelts()
        {
            _nearbyBelts.Clear();
            if (_cam == null) return;

            float maxDistSq = ModSettings.BeltCounterMaxDistance.Value;
            maxDistSq *= maxDistSq;
            Vector3 camPos = _cam.transform.position;

#pragma warning disable CS0618
            foreach (var belt in FindObjectsOfType<ConveyorBelt>())
#pragma warning restore CS0618
            {
                if (belt == null) continue;
                if ((belt.transform.position - camPos).sqrMagnitude <= maxDistSq)
                    _nearbyBelts.Add(belt);
            }
        }

        // ── GUI ────────────────────────────────────────────────────────────────
        private void OnGUI()
        {
            if (!ModSettings.EnableBeltCounter.Value) return;
            _cam ??= Camera.main;
            if (_cam == null) return;

            EnsureStyles();
            var tracker = ThroughputTracker.Instance;

            // ── Aimed belt: detail panel rechtsonder ──────────────────────────
            if (_aimedBelt != null && tracker != null)
                DrawDetailPanel(_aimedBelt, tracker);

            // ── Global floating labels ────────────────────────────────────────
            if (_globalActive && tracker != null)
                DrawGlobalLabels(tracker);

            // ── F6-hint (als global uit is) ───────────────────────────────────
            if (!_globalActive)
            {
                var hint = new GUIStyle(GUI.skin.label)
                {
                    fontSize  = 10,
                    alignment = TextAnchor.LowerLeft,
                    normal    = { textColor = new Color(1f, 1f, 1f, 0.35f) }
                };
                GUI.Label(new Rect(8, Screen.height - 22, 240, 20), "F6: toon belt labels", hint);
            }
        }

        // ── Detail panel voor belt waar je naar kijkt ──────────────────────────
        private void DrawDetailPanel(ConveyorBelt belt, ThroughputTracker tracker)
        {
            float ipm        = tracker.GetThroughput(belt);
            bool  bottleneck = BottleneckDetector.Instance?.IsBottleneck(belt) ?? false;
            bool  disabled   = belt.Disabled;

            float x = Screen.width  - DETAIL_W - PANEL_MARGIN;
            float y = Screen.height - DETAIL_H - PANEL_MARGIN;
            var   r = new Rect(x, y, DETAIL_W, DETAIL_H);

            // Achtergrond
            DrawRect(r, new Color(0.05f, 0.05f, 0.07f, 0.92f));
            DrawRect(new Rect(r.x, r.y, r.width, 22), new Color(0.12f, 0.14f, 0.20f, 1f));

            // Header
            _labelBold!.normal.textColor = new Color(0.3f, 0.75f, 1f);
            GUI.Label(new Rect(r.x + 6, r.y + 3, r.width - 12, 18), "● Belt Info", _labelBold);
            _labelBold.normal.textColor = new Color(0.95f, 0.95f, 0.95f);

            float ly = r.y + 26;
            const float lh = 18f;

            string statusTxt = disabled ? "DISABLED" : (bottleneck ? "BOTTLENECK" : "OK");
            Color  statusCol = disabled  ? new Color(0.8f, 0.5f, 0.0f)
                             : bottleneck ? new Color(1f, 0.25f, 0.25f)
                             : new Color(0.25f, 0.95f, 0.45f);
            DrawLabelRow(r.x, ly, r.width, lh, "Status", statusTxt, statusCol); ly += lh + 2;

            Color ipmCol = ipm < 20f ? new Color(1f, 0.80f, 0.20f) : new Color(0.90f, 0.95f, 0.90f);
            DrawLabelRow(r.x, ly, r.width, lh, "Doorvoer", $"{ipm:F1} items/min", ipmCol); ly += lh + 2;

            DrawLabelRow(r.x, ly, r.width, lh, "Snelheid", $"{belt.Speed:F2}", new Color(0.7f, 0.85f, 1f)); ly += lh + 2;

            string bname = belt.gameObject.name;
            if (bname.Length > 22) bname = bname[..22] + "…";
            DrawLabelRow(r.x, ly, r.width, lh, "Object", bname, new Color(0.6f, 0.6f, 0.6f));
        }

        private void DrawLabelRow(float x, float y, float w, float h, string key, string val, Color valCol)
        {
            float kw = w * 0.45f;
            GUI.Label(new Rect(x + 6, y, kw, h), key, _labelMuted!);
            _labelBold!.normal.textColor = valCol;
            GUI.Label(new Rect(x + kw, y, w - kw - 4, h), val, _labelBold);
            _labelBold.normal.textColor = new Color(0.95f, 0.95f, 0.95f);
        }

        // ── Floating labels (global view) ─────────────────────────────────────
        private void DrawGlobalLabels(ThroughputTracker tracker)
        {
            foreach (var belt in _nearbyBelts)
            {
                if (belt == null) continue;

                float ipm = tracker.GetThroughput(belt);
                if (ipm < ModSettings.BeltCounterMinIPM.Value) continue;

                bool bottleneck = BottleneckDetector.Instance?.IsBottleneck(belt) ?? false;

                Vector3 screen = _cam!.WorldToScreenPoint(belt.transform.position + Vector3.up * 0.7f);
                if (screen.z <= 0f) continue;

                float sx = screen.x;
                float sy = Screen.height - screen.y;

                const float W = 80f, H = 18f;
                var rect   = new Rect(sx - W / 2f, sy - H / 2f, W, H);
                var shadow = new Rect(rect.x + 1, rect.y + 1, W, H);

                Color col = bottleneck   ? new Color(1f, 0.25f, 0.25f)
                          : ipm < 20f   ? new Color(1f, 0.80f, 0.20f)
                                        : new Color(0.25f, 0.95f, 0.45f);

                string lbl = $"{ipm:F1} ipm";
                _style!.normal.textColor       = col;
                _shadowStyle!.normal.textColor = new Color(0, 0, 0, 0.7f);

                GUI.Label(shadow, lbl, _shadowStyle);
                GUI.Label(rect,   lbl, _style);
            }
        }

        // ── Helpers ────────────────────────────────────────────────────────────
        private static void DrawRect(Rect r, Color c)
        {
            var old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = old;
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

            _labelBold = new GUIStyle(GUI.skin.label)
            {
                fontSize  = 11,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal    = { textColor = new Color(0.95f, 0.95f, 0.95f) }
            };

            _labelMuted = new GUIStyle(GUI.skin.label)
            {
                fontSize  = 11,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleLeft,
                normal    = { textColor = new Color(0.55f, 0.60f, 0.65f) }
            };
        }
    }
}

