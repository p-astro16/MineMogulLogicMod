using System.Reflection;
using MineMogulMod.Modules.Splitter;
using UnityEngine;

namespace MineMogulMod.Modules
{
    /// <summary>
    /// Echt held-item dat de speler in de hotbar kan zetten.
    /// LMB  = configureer de splitter waar je naar kijkt (RollerSplitter of ConveyorSplitterT2).
    /// RMB  = open globaal overzicht van alle splitters.
    /// </summary>
    public class SplitterWrench : BaseHeldTool
    {
        private const float RAY_RANGE = 8f;

        private CursorLockMode _prevLock;
        private bool           _prevVisible;

        // ── Initialisatie ─────────────────────────────────────────────────────

        private bool _modelCopied;

        private new void Awake()
        {
            // Vul verplichte BaseHeldTool-fields in
            base.Name        = "Splitter Wrench";
            base.Description = "LMB: configure splitter you're looking at | RMB: overview of all splitters";
            base.MaxAmount   = 1;
            base.Quantity    = 1;

            // Maak een eenvoudig wit icon programmatisch
            if (base.InventoryIcon == null)
            {
                var tex  = new Texture2D(32, 32);
                var col  = new Color(0.3f, 0.85f, 1f);
                var px   = new Color[32 * 32];
                for (int i = 0; i < px.Length; i++) px[i] = col;
                tex.SetPixels(px);
                tex.Apply();
                base.InventoryIcon = Sprite.Create(tex,
                    new Rect(0, 0, 32, 32), new Vector2(0.5f, 0.5f));
                base.ProgrammerInventoryIcon = base.InventoryIcon;
            }
        }

        // ── Equip / UnEquip ────────────────────────────────────────────────────

        public override void Equip()
        {
            base.Equip();
            _prevLock    = Cursor.lockState;
            _prevVisible = Cursor.visible;
            ShowCursor(true);
            TryCopyResourceScannerModel();
        }

        public override void UnEquip()
        {
            base.UnEquip();
            SplitterConfigUI.Instance?.CloseAll();
            ShowCursor(false);
        }

        /// <summary>LMB — configureer de splitter waar je naar kijkt.</summary>
        public override void PrimaryFire()
        {
            var cam = GetCamera();
            if (cam == null) return;

            if (!Physics.Raycast(cam.transform.position, cam.transform.forward,
                    out RaycastHit hit, RAY_RANGE)) return;

            var cfg = TryGetSplitterConfig(hit.collider.gameObject);
            if (cfg == null)
            {
                Plugin.Logger.LogInfo("[SplitterWrench] No splitter in range.");
                return;
            }
            SplitterStorage.ApplyToConfig(cfg);
            SplitterConfigUI.Instance?.OpenSingleConfig(cfg);
        }

        /// <summary>RMB — globaal overzicht van alle splitters.</summary>
        public override void SecondaryFire()
        {
            SplitterConfigUI.Instance?.OpenGlobalManager();
        }

        public override string GetControlsText() =>
            "LMB: Configure Splitter  |  RMB: All Splitters";

        public override UnityEngine.Sprite GetIcon() => base.InventoryIcon;

        // Geen saves nodig voor dit tool
        public new bool ShouldBeSaved() => false;
        public override string GetCustomSaveData() => "";
        public override void LoadFromSave(string json) { }

        // ── Helpers ───────────────────────────────────────────────────────────

        private Camera? GetCamera()
        {
            if (Owner != null) return Owner.PlayerCamera;
#pragma warning disable CS0618
            return FindObjectOfType<PlayerController>()?.PlayerCamera
                ?? Camera.main;
#pragma warning restore CS0618
        }

        private static MMLSplitterConfig? TryGetSplitterConfig(GameObject go)
        {
            // Zoek in het object en alle parents
            var t = go.transform;
            while (t != null)
            {
                if (t.TryGetComponent<RollerSplitter>(out _) ||
                    t.TryGetComponent<ConveyorSplitterT2>(out _))
                {
                    var cfg = t.GetComponent<MMLSplitterConfig>()
                           ?? t.gameObject.AddComponent<MMLSplitterConfig>();
                    return cfg;
                }
                t = t.parent;
            }
            return null;
        }

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
            mf.sharedMesh      = srcMf.sharedMesh;
            mr.sharedMaterials = srcMr.sharedMaterials;
            _modelCopied = true;
            Plugin.Logger.LogInfo("[SplitterWrench] ResourceScanner model copied.");
        }

        private void ShowCursor(bool show)
        {
            if (show)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible   = true;
            }
            else
            {
                Cursor.lockState = _prevLock;
                Cursor.visible   = _prevVisible;
            }
        }

        // ── Statische factory — maak een wrench en geef die aan de speler ────

        /// <summary>
        /// Maak een SplitterWrench GameObject en voeg het toe aan de inventory
        /// van de lokale PlayerController.
        /// </summary>
        public static SplitterWrench? SpawnAndGiveToPlayer()
        {
#pragma warning disable CS0618
            var player    = FindObjectOfType<PlayerController>();
#pragma warning restore CS0618
            if (player == null)
            {
                Plugin.Logger.LogWarning("[SplitterWrench] No PlayerController found.");
                return null;
            }

            // Maak een persistent GameObject voor het item
            var go = new GameObject("MML_SplitterWrench");
            Object.DontDestroyOnLoad(go);
            go.SetActive(false); // Zet uit voor AddComponent (vereist door BaseHeldTool)
            var wrench = go.AddComponent<SplitterWrench>();
            go.SetActive(true);

            // Voeg toe aan inventory van speler
            var inventory  = player.Inventory;
            bool added = false;
            if (inventory != null)
            {
                // TryAddToInventory(BaseHeldTool, int slotIndex) via reflection (-1 = auto slot)
                var method = typeof(PlayerInventory).GetMethod("TryAddToInventory",
                    new[] { typeof(BaseHeldTool), typeof(int) });
                if (method != null)
                {
                    // Try slots 0–9; first success wins
                    for (int slot = 0; slot < 10 && !added; slot++)
                    {
                        var result = method.Invoke(inventory, new object[] { wrench, slot });
                        added = result is bool b && b;
                    }
                }
            }

            if (added)
                Plugin.Logger.LogInfo("[SplitterWrench] Added to player inventory.");
            else
                Plugin.Logger.LogWarning("[SplitterWrench] Could not add to inventory (might be full).");

            return wrench;
        }
    }
}
