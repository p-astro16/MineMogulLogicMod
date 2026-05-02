using System.Collections.Generic;
using UnityEngine;

namespace MineMogulMod.Modules
{
    /// <summary>
    /// Belt Scanner — koopbaar item in de shop (Tools-categorie).
    /// Visueel model: ResourceScanner (wordt gekopieerd zodra equipment).
    /// Wanneer uitgerust: toont zwevende items/min tekst boven belts
    /// binnen ModSettings.BeltCounterMaxDistance meter.
    /// </summary>
    public class BeltScannerTool : BaseHeldTool
    {
        // ── State ─────────────────────────────────────────────────────────────
        private bool           _equipped;
        private Camera?        _cam;
        private GUIStyle?      _style;
        private float          _nextRefresh;
        private List<ConveyorBelt> _nearbyBelts = new();
        private bool           _modelCopied;

        // ── BaseHeldTool init ─────────────────────────────────────────────────

        private new void Awake()
        {
            base.Name        = "Belt Scanner";
            base.Description = "Scan nearby conveyor belts and see live items/min labels floating above them.";
            base.MaxAmount   = 1;
            base.Quantity    = 1;

            // Programmatic teal icon
            if (base.InventoryIcon == null)
            {
                var tex = new Texture2D(32, 32);
                var col = new Color(0.16f, 0.85f, 0.55f);
                var px  = new Color[32 * 32];
                for (int i = 0; i < px.Length; i++) px[i] = col;
                tex.SetPixels(px); tex.Apply();
                base.InventoryIcon = Sprite.Create(tex,
                    new Rect(0, 0, 32, 32), new Vector2(0.5f, 0.5f));
                base.ProgrammerInventoryIcon = base.InventoryIcon;
            }
        }

        // ── Equip / UnEquip ───────────────────────────────────────────────────

        public override void Equip()
        {
            base.Equip();
            _equipped = true;
            _cam = Camera.main;
            TryCopyResourceScannerModel();
        }

        public override void UnEquip()
        {
            base.UnEquip();
            _equipped = false;
            _nearbyBelts.Clear();
        }

        // ── Update ────────────────────────────────────────────────────────────

        private void Update()
        {
            if (!_equipped) return;
            _cam ??= Camera.main;

            if (Time.time >= _nextRefresh)
            {
                _nextRefresh = Time.time + 1f;
                RefreshNearbyBelts();
            }
        }

        // ── IMGUI world-space labels ──────────────────────────────────────────

        private void OnGUI()
        {
            if (!_equipped || _cam == null) return;

            _style ??= new GUIStyle(GUI.skin.label)
            {
                fontSize  = 12,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
            };

            var tracker = ThroughputTracker.Instance;
            if (tracker == null) return;

            foreach (var belt in _nearbyBelts)
            {
                if (belt == null) continue;

                float ipm = tracker.GetThroughput(belt);
                if (ipm < ModSettings.BeltCounterMinIPM.Value) continue;

                Vector3 worldPos = belt.transform.position + Vector3.up * 0.7f;
                Vector3 screen   = _cam.WorldToScreenPoint(worldPos);
                if (screen.z < 0) continue;

                float sx = screen.x;
                float sy = Screen.height - screen.y;

                bool isBottleneck = BottleneckDetector.Instance?.IsBottleneck(belt) ?? false;
                _style.normal.textColor = isBottleneck ? new Color(1f, 0.3f, 0.3f)
                    : ipm < 20f           ? new Color(1f, 0.85f, 0.2f)
                    :                       new Color(0.2f, 1f, 0.55f);

                // Shadow for readability
                var shadow = _style.normal.textColor * 0.35f;
                shadow.a = 1f;
                _style.normal.textColor = shadow;
                GUI.Label(new Rect(sx - 30 + 1, sy - 12 + 1, 62, 24), $"{ipm:F0}/min", _style);

                _style.normal.textColor = isBottleneck ? new Color(1f, 0.3f, 0.3f)
                    : ipm < 20f           ? new Color(1f, 0.85f, 0.2f)
                    :                       new Color(0.2f, 1f, 0.55f);
                GUI.Label(new Rect(sx - 30, sy - 12, 62, 24), $"{ipm:F0}/min", _style);
            }
        }

        // ── BaseHeldTool stubs ────────────────────────────────────────────────

        public override void PrimaryFire()   { }
        public override void SecondaryFire() { }
        public override string GetControlsText() => "Equip to see live belt throughput labels";
        public override UnityEngine.Sprite GetIcon() => base.InventoryIcon;
        public new bool ShouldBeSaved() => false;
        public override string GetCustomSaveData() => "";
        public override void LoadFromSave(string json) { }

        // ── Helpers ───────────────────────────────────────────────────────────

        private void RefreshNearbyBelts()
        {
            _nearbyBelts.Clear();
            if (_cam == null) return;
            var all = ConveyorBelt.AllConveyorBelts;
            if (all == null) return;
            float maxDist = ModSettings.BeltCounterMaxDistance.Value;
            Vector3 camPos = _cam.transform.position;
            foreach (var belt in all)
            {
                if (belt == null) continue;
                if (Vector3.Distance(belt.transform.position, camPos) <= maxDist)
                    _nearbyBelts.Add(belt);
            }
        }

        /// <summary>
        /// Kopieert mesh + materialen van de eerste ResourceScanner in de scene
        /// zodat het item er hetzelfde uitziet als de scanner tool.
        /// </summary>
        private void TryCopyResourceScannerModel()
        {
            if (_modelCopied) return;
#pragma warning disable CS0618
            var scanner = Object.FindObjectOfType<ToolResourceScanner>();
#pragma warning restore CS0618
            if (scanner == null) return;

            var srcMf = scanner.GetComponentInChildren<MeshFilter>(true);
            var srcMr = scanner.GetComponentInChildren<MeshRenderer>(true);
            if (srcMf == null || srcMr == null) return;

            var mf = gameObject.GetComponent<MeshFilter>() ?? gameObject.AddComponent<MeshFilter>();
            var mr = gameObject.GetComponent<MeshRenderer>() ?? gameObject.AddComponent<MeshRenderer>();
            mf.sharedMesh       = srcMf.sharedMesh;
            mr.sharedMaterials  = srcMr.sharedMaterials;
            _modelCopied = true;
            Plugin.Logger.LogInfo("[BeltScannerTool] ResourceScanner model copied.");
        }

        // ── Static factory ────────────────────────────────────────────────────

        /// <summary>
        /// Maak een BeltScannerTool GameObject en geef het aan de speler.
        /// </summary>
        public static BeltScannerTool? SpawnAndGiveToPlayer()
        {
#pragma warning disable CS0618
            var player = Object.FindObjectOfType<PlayerController>();
#pragma warning restore CS0618
            if (player == null)
            {
                Plugin.Logger.LogWarning("[BeltScannerTool] No PlayerController found.");
                return null;
            }

            var go = new GameObject("MML_BeltScanner");
            Object.DontDestroyOnLoad(go);
            go.SetActive(false);
            var tool = go.AddComponent<BeltScannerTool>();
            go.SetActive(true);

            var inventory = player.Inventory;
            bool added = false;
            if (inventory != null)
            {
                var method = typeof(PlayerInventory).GetMethod("TryAddToInventory",
                    new[] { typeof(BaseHeldTool), typeof(int) });
                if (method != null)
                {
                    for (int slot = 0; slot < 10 && !added; slot++)
                    {
                        var result = method.Invoke(inventory, new object[] { tool, slot });
                        added = result is bool b && b;
                    }
                }
            }

            if (added)
                Plugin.Logger.LogInfo("[BeltScannerTool] Added to player inventory.");
            else
                Plugin.Logger.LogWarning("[BeltScannerTool] Could not add to inventory.");

            return tool;
        }
    }
}