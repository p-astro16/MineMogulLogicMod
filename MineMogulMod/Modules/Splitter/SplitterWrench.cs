using MineMogulMod.Modules.Splitter;
using UnityEngine;

namespace MineMogulMod.Modules
{
    /// <summary>
    /// Splitter Wrench — press F7 to receive it in your inventory.
    /// LMB: configure the splitter you're looking at.
    /// RMB: open the global splitter manager.
    /// </summary>
    public class SplitterWrench : BaseHeldTool
    {
        private const float RAY_RANGE = 8f;
        private bool _modelCopied;

        // ── Initialise ─────────────────────────────────────────────────────────
        private new void Awake()
        {
            Name        = "Splitter Wrench";
            Description = "LMB: configure the splitter you're aiming at.  RMB: all splitters.";
            MaxAmount   = 1;
            Quantity    = 1;

            if (InventoryIcon == null)
            {
                var tex = new Texture2D(32, 32);
                var px  = new Color[32 * 32];
                var col = new Color(0.3f, 0.85f, 1f);
                for (int i = 0; i < px.Length; i++) px[i] = col;
                tex.SetPixels(px);
                tex.Apply();
                InventoryIcon             = Sprite.Create(tex, new Rect(0, 0, 32, 32), new Vector2(0.5f, 0.5f));
                ProgrammerInventoryIcon   = InventoryIcon;
            }
        }

        // ── Equip ──────────────────────────────────────────────────────────────
        public override void Equip()
        {
            base.Equip();
            TryCopyResourceScannerModel();
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible   = true;
        }

        public override void UnEquip()
        {
            base.UnEquip();
            SplitterConfigUI.Instance?.CloseAll();
        }

        // ── Actions ────────────────────────────────────────────────────────────
        public override void PrimaryFire()
        {
            var cam = GetCam();
            if (cam == null) return;
            if (!Physics.Raycast(cam.transform.position, cam.transform.forward, out var hit, RAY_RANGE)) return;

            var cfg = FindSplitterConfig(hit.collider.gameObject);
            if (cfg == null) { Plugin.Logger.LogInfo("[Wrench] No splitter in range."); return; }

            SplitterStorage.ApplyToConfig(cfg);
            SplitterConfigUI.Instance?.OpenSingleConfig(cfg);
        }

        public override void SecondaryFire()
        {
            SplitterConfigUI.Instance?.OpenGlobalManager();
        }

        public override string GetControlsText() =>
            "LMB: Configure Splitter  |  RMB: All Splitters";

        public override UnityEngine.Sprite GetIcon() => InventoryIcon;

        public new bool ShouldBeSaved() => false;
        public override string GetCustomSaveData() => "";
        public override void LoadFromSave(string json) { }

        // ── Helpers ────────────────────────────────────────────────────────────
        private static Camera? GetCam()
        {
#pragma warning disable CS0618
            return FindObjectOfType<PlayerController>()?.PlayerCamera ?? Camera.main;
#pragma warning restore CS0618
        }

        private static MMLSplitterConfig? FindSplitterConfig(GameObject go)
        {
            var t = go.transform;
            while (t != null)
            {
                if (t.TryGetComponent<RollerSplitter>(out _) ||
                    t.TryGetComponent<ConveyorSplitterT2>(out _))
                {
                    return t.GetComponent<MMLSplitterConfig>()
                        ?? t.gameObject.AddComponent<MMLSplitterConfig>();
                }
                t = t.parent;
            }
            return null;
        }

        private void TryCopyResourceScannerModel()
        {
            if (_modelCopied) return;
#pragma warning disable CS0618
            var scanner = FindObjectOfType<ToolResourceScanner>();
#pragma warning restore CS0618
            if (scanner == null) return;
            var srcMf = scanner.GetComponentInChildren<MeshFilter>(true);
            var srcMr = scanner.GetComponentInChildren<MeshRenderer>(true);
            if (srcMf == null || srcMr == null) return;
            var mf = gameObject.GetComponent<MeshFilter>() ?? gameObject.AddComponent<MeshFilter>();
            var mr = gameObject.GetComponent<MeshRenderer>() ?? gameObject.AddComponent<MeshRenderer>();
            mf.sharedMesh      = srcMf.sharedMesh;
            mr.sharedMaterials = srcMr.sharedMaterials;
            _modelCopied       = true;
        }

        // ── Static factory ─────────────────────────────────────────────────────
        /// <summary>Press F7 in-game → calls this to hand the wrench to the player.</summary>
        public static void SpawnAndGiveToPlayer()
        {
#pragma warning disable CS0618
            var player = FindObjectOfType<PlayerController>();
#pragma warning restore CS0618
            if (player == null)
            {
                Plugin.Logger.LogWarning("[Wrench] PlayerController not found.");
                return;
            }

            // Create the wrench GameObject (inactive during AddComponent so Awake doesn't fire twice)
            var go = new GameObject("MML_SplitterWrench");
            DontDestroyOnLoad(go);
            go.SetActive(false);
            var wrench = go.AddComponent<SplitterWrench>();
            go.SetActive(true);

            // Add to the first free inventory slot
            bool added = false;
            for (int slot = 0; slot < 10 && !added; slot++)
            {
                try { added = player.Inventory.TryAddToInventory(wrench, slot); }
                catch { /* slot may be occupied */ }
            }

            Plugin.Logger.LogInfo(added
                ? "[Wrench] Added to inventory."
                : "[Wrench] Inventory full — could not add wrench.");
        }
    }
}
