using MineMogulMod.Modules.Splitter;
using UnityEngine;

namespace MineMogulMod.Modules
{
    /// <summary>
    /// Splitter Inspector
    /// ─ Kijk naar een splitter → hint verschijnt in beeld.
    /// ─ Druk op de interactieknop (F) → open configuratievenster.
    /// ─ RMB / Tab → open global splitter manager.
    /// Geen wrench-item nodig: werkt altijd.
    /// </summary>
    public class SplitterInspector : MonoBehaviour
    {
        public static SplitterInspector? Instance { get; private set; }

        private const float RAY_RANGE     = 8f;
        private const float HINT_W        = 280f;
        private const float HINT_H        = 48f;

        private Camera?         _cam;
        private float           _rayRefresh;
        private MMLSplitterConfig? _aimedCfg;
        private string?         _aimedName;

        private GUIStyle?       _hintStyle;
        private GUIStyle?       _hintSub;

        private void Awake() => Instance = this;

        // ── Update ─────────────────────────────────────────────────────────────
        private void Update()
        {
            _cam ??= Camera.main;
            if (_cam == null) return;

            // Raycast elke 0.1s
            if (Time.time >= _rayRefresh)
            {
                _rayRefresh = Time.time + 0.1f;
                UpdateAimedSplitter();
            }

            if (_aimedCfg == null) return;

            // F (of geconfigureerde key): open single config
            if (ModSettings.SplitterInteractKey.Value.IsDown())
            {
                SplitterConfigUI.Instance?.OpenSingleConfig(_aimedCfg);
                return;
            }

            // Tab: open global manager
            if (Input.GetKeyDown(KeyCode.Tab))
                SplitterConfigUI.Instance?.OpenGlobalManager();
        }

        // ── Crosshair raycast ─────────────────────────────────────────────────
        private void UpdateAimedSplitter()
        {
            _aimedCfg  = null;
            _aimedName = null;
            if (_cam == null) return;

            Ray ray = new(_cam.transform.position, _cam.transform.forward);
            if (!Physics.Raycast(ray, out var hit, RAY_RANGE)) return;

            var t = hit.collider.transform;
            while (t != null)
            {
                bool isSplitter = t.TryGetComponent<RollerSplitter>(out _) ||
                                  t.TryGetComponent<ConveyorSplitterT2>(out _);
                if (isSplitter)
                {
                    _aimedCfg  = t.GetComponent<MMLSplitterConfig>()
                               ?? t.gameObject.AddComponent<MMLSplitterConfig>();
                    _aimedName = t.gameObject.name;
                    return;
                }
                t = t.parent;
            }
        }

        // ── GUI ────────────────────────────────────────────────────────────────
        private void OnGUI()
        {
            if (_aimedCfg == null) return;

            EnsureStyles();

            string key = ModSettings.SplitterInteractKey.Value.ToString();

            float x = (Screen.width  - HINT_W) / 2f;
            float y =  Screen.height * 0.68f;
            var   r = new Rect(x, y, HINT_W, HINT_H);

            // Achtergrond
            DrawRect(r, new Color(0.05f, 0.06f, 0.09f, 0.88f));
            DrawRect(new Rect(r.x, r.y, r.width, 2), new Color(0.20f, 0.70f, 1f, 1f)); // accent lijn

            // Naam splitter
            string name = _aimedName ?? "Splitter";
            if (name.Length > 28) name = name[..28] + "…";
            GUI.Label(new Rect(r.x + 8, r.y + 4,  r.width - 16, 20), name, _hintStyle!);

            // Instructie
            string sub = $"[{key}] Configureren   [Tab] Alle splitters";
            GUI.Label(new Rect(r.x + 8, r.y + 24, r.width - 16, 18), sub, _hintSub!);
        }

        private static void DrawRect(Rect r, Color c)
        {
            var old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = old;
        }

        private void EnsureStyles()
        {
            if (_hintStyle != null) return;

            _hintStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize  = 12,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal    = { textColor = new Color(0.95f, 0.95f, 0.95f) }
            };

            _hintSub = new GUIStyle(GUI.skin.label)
            {
                fontSize  = 10,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleLeft,
                normal    = { textColor = new Color(0.45f, 0.75f, 1f) }
            };
        }
    }
}
