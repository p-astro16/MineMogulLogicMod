using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MineMogulMod.Modules.Splitter;
using UnityEngine;
using UnityEngine.UI;

namespace MineMogulMod.Modules
{
    /// <summary>
    /// Canvas-gebaseerde UI voor de Splitter Wrench.
    /// Twee panelen: SingleConfig (voor één splitter) en GlobalManager (overzicht).
    /// </summary>
    public class SplitterConfigUI : MonoBehaviour
    {
        public static SplitterConfigUI? Instance { get; private set; }

        // ── Dark theme kleuren ────────────────────────────────────────────────
        private static readonly Color BG          = new(0.08f, 0.09f, 0.11f, 0.97f);
        private static readonly Color CardBG      = new(0.12f, 0.14f, 0.18f, 1f);
        private static readonly Color HeaderBG    = new(0.10f, 0.12f, 0.17f, 1f);
        private static readonly Color Accent      = new(0.20f, 0.70f, 1.00f, 1f);
        private static readonly Color AccentDark  = new(0.10f, 0.45f, 0.70f, 1f);
        private static readonly Color Success     = new(0.20f, 0.85f, 0.40f, 1f);
        private static readonly Color Danger      = new(0.85f, 0.25f, 0.25f, 1f);
        private static readonly Color TextPrimary = new(0.95f, 0.95f, 0.95f, 1f);
        private static readonly Color TextMuted   = new(0.55f, 0.60f, 0.65f, 1f);
        private static readonly Color RowOdd      = new(0.10f, 0.12f, 0.16f, 1f);
        private static readonly Color RowEven     = new(0.13f, 0.16f, 0.20f, 1f);

        private Font      _font = null!;
        private Canvas    _canvas = null!;

        // ── Single config panel ───────────────────────────────────────────────
        private GameObject  _cfgRoot    = null!;
        private Text        _cfgTitle   = null!;
        private Slider      _globalSlider = null!;
        private Text        _globalLabel  = null!;
        private Transform   _rulesContent = null!;
        private MMLSplitterConfig? _currentConfig;

        // ── Global manager panel ──────────────────────────────────────────────
        private GameObject  _mgrRoot    = null!;
        private Transform   _mgrContent = null!;

        // ── Add-rule dropdowns (state) ────────────────────────────────────────
        private string _newRT = "(any)";
        private string _newPT = "(any)";
        private float  _newLP = 50f;
        private Slider _newSlider = null!;
        private Text   _newSliderLabel = null!;

        // ── Dropdown overlay ──────────────────────────────────────────────────
        private GameObject? _ddOverlay;
        private Action<string>? _ddCallback;
        private string[]?       _ddOptions;

        // ─────────────────────────────────────────────────────────────────────

        private void Awake()
        {
            Instance = this;
            _font    = Font.CreateDynamicFontFromOSFont("Arial", 13);
            BuildCanvas();
            BuildConfigPanel();
            BuildManagerPanel();
        }

        // ── Public API ────────────────────────────────────────────────────────

        public void OpenSingleConfig(MMLSplitterConfig cfg)
        {
            _currentConfig = cfg;
            _mgrRoot.SetActive(false);
            RefreshConfigPanel();
            _cfgRoot.SetActive(true);
        }

        public void OpenGlobalManager()
        {
            _cfgRoot.SetActive(false);
            RefreshManagerPanel();
            _mgrRoot.SetActive(true);
        }

        public void CloseAll()
        {
            _cfgRoot.SetActive(false);
            _mgrRoot.SetActive(false);
            DestroyDropdown();
        }

        // ── Canvas setup ──────────────────────────────────────────────────────

        private void BuildCanvas()
        {
            var go = new GameObject("MML_SplitterCanvas");
            UnityEngine.Object.DontDestroyOnLoad(go);
            _canvas = go.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 500;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            go.AddComponent<GraphicRaycaster>();
        }

        // ── Config panel ──────────────────────────────────────────────────────

        private void BuildConfigPanel()
        {
            _cfgRoot = CreatePanel(_canvas.transform, new Vector2(600, 640),
                new Vector2(0.5f, 0.5f), new Vector2(0, 0));
            _cfgRoot.SetActive(false);

            float y = -20f;

            // Header
            var header = CreateRect(_cfgRoot.transform, new Vector2(600, 48), new Vector2(0, -y));
            header.GetComponent<Image>().color = HeaderBG;
            _cfgTitle = CreateLabel(header.transform, "Splitter Configurator",
                new Vector2(480, 30), new Vector2(-50, 0), 15, FontStyle.Bold, TextAnchor.MiddleLeft);
            _cfgTitle.color = Accent;
            var closeBtn = CreateButton(header.transform, "✕", new Vector2(40, 40),
                new Vector2(275, 0), Danger, () => CloseAll());

            y += 58f;

            // Global ratio section
            CreateSectionHeader(_cfgRoot.transform, "GLOBAL SPLIT RATIO", new Vector2(0, -y)); y += 36f;
            var sliderRow = CreateRect(_cfgRoot.transform, new Vector2(560, 38), new Vector2(0, -y));
            sliderRow.GetComponent<Image>().color = CardBG;
            CreateLabel(sliderRow.transform, "Left  %", new Vector2(60, 30),
                new Vector2(-240, 0), 12, FontStyle.Normal, TextAnchor.MiddleLeft).color = TextMuted;
            _globalSlider = CreateSlider(sliderRow.transform, new Vector2(360, 18), new Vector2(25, 0));
            _globalSlider.minValue = 0; _globalSlider.maxValue = 100; _globalSlider.value = 50;
            _globalLabel = CreateLabel(sliderRow.transform, "50% / 50%",
                new Vector2(100, 30), new Vector2(230, 0), 12, FontStyle.Bold, TextAnchor.MiddleLeft);
            _globalLabel.color = Accent;
            _globalSlider.onValueChanged.AddListener(v => {
                _globalLabel.text = $"{v:F0}% / {100f - v:F0}%";
                if (_currentConfig != null) _currentConfig.GlobalLeftPercent = v;
            });
            y += 50f;

            // Rules section
            CreateSectionHeader(_cfgRoot.transform, "PER-ITEM RULES", new Vector2(0, -y)); y += 36f;
            var rulesCard = CreateRect(_cfgRoot.transform, new Vector2(560, 170), new Vector2(0, -y));
            rulesCard.GetComponent<Image>().color = CardBG;
            var scroll = CreateScrollView(rulesCard.transform, new Vector2(558, 165), Vector2.zero,
                out _rulesContent);
            y += 180f;

            // Add rule section
            CreateSectionHeader(_cfgRoot.transform, "ADD RULE", new Vector2(0, -y)); y += 36f;
            var addCard = CreateRect(_cfgRoot.transform, new Vector2(560, 130), new Vector2(0, -y));
            addCard.GetComponent<Image>().color = CardBG;
            BuildAddRuleSection(addCard.transform);
            y += 140f;

            // Save button
            CreateButton(_cfgRoot.transform, "Save & Close", new Vector2(200, 40),
                new Vector2(0, -y), Success, () => {
                    if (_currentConfig != null) SplitterStorage.Save(_currentConfig);
                    CloseAll();
                });
        }

        private void BuildAddRuleSection(Transform parent)
        {
            // Row 1: Resource dropdown + Piece dropdown
            CreateLabel(parent, "Resource:", new Vector2(80, 28),
                new Vector2(-235, 25), 12, FontStyle.Normal, TextAnchor.MiddleLeft).color = TextMuted;
            var rtBtn = CreateDropdownButton(parent, "(any)", new Vector2(160, 28),
                new Vector2(-100, 25));
            CreateLabel(parent, "Piece:", new Vector2(60, 28),
                new Vector2(55, 25), 12, FontStyle.Normal, TextAnchor.MiddleLeft).color = TextMuted;
            var ptBtn = CreateDropdownButton(parent, "(any)", new Vector2(160, 28),
                new Vector2(200, 25));

            // Wire dropdowns
            var rtText = rtBtn.GetComponentInChildren<Text>();
            var ptText = ptBtn.GetComponentInChildren<Text>();
            rtBtn.onClick.AddListener(() => ShowDropdown(
                rtBtn.transform.position, SplitterEnums.ResourceTypes, v => { _newRT = v; rtText.text = v; }));
            ptBtn.onClick.AddListener(() => ShowDropdown(
                ptBtn.transform.position, SplitterEnums.PieceTypes, v => { _newPT = v; ptText.text = v; }));

            // Row 2: Slider for left%
            CreateLabel(parent, "Left %:", new Vector2(60, 28),
                new Vector2(-245, -10), 12, FontStyle.Normal, TextAnchor.MiddleLeft).color = TextMuted;
            _newSlider = CreateSlider(parent, new Vector2(300, 18), new Vector2(20, -10));
            _newSlider.minValue = 0; _newSlider.maxValue = 100; _newSlider.value = 50;
            _newSliderLabel = CreateLabel(parent, "50% / 50%",
                new Vector2(90, 28), new Vector2(215, -10), 12, FontStyle.Bold);
            _newSliderLabel.color = Accent;
            _newSlider.onValueChanged.AddListener(v => {
                _newLP = v; _newSliderLabel.text = $"{v:F0}% / {100f - v:F0}%"; });

            // Row 3: Add button
            CreateButton(parent, "+ Add Rule", new Vector2(160, 32),
                new Vector2(-170, -45), Accent, () => {
                    if (_currentConfig == null) return;
                    _currentConfig.PerItemRules.Add(new ItemRatioRule {
                        ResourceTypeName = _newRT,
                        PieceTypeName    = _newPT,
                        LeftPercent      = _newLP
                    });
                    RefreshRulesList();
                });
        }

        private void RefreshConfigPanel()
        {
            if (_currentConfig == null) return;
            _cfgTitle.text = $"Splitter @ {_currentConfig.transform.position:F0}";
            _globalSlider.value = _currentConfig.GlobalLeftPercent;
            RefreshRulesList();
        }

        private void RefreshRulesList()
        {
            if (_currentConfig == null) return;
            foreach (Transform c in _rulesContent) Destroy(c.gameObject);

            var rules  = _currentConfig.PerItemRules;
            float rowH = 32f;
            var layout = _rulesContent.GetComponent<VerticalLayoutGroup>();
            if (layout != null) { layout.spacing = 2; layout.padding = new RectOffset(4, 4, 4, 4); }

            for (int i = 0; i < rules.Count; i++)
            {
                int idx  = i;
                var rule = rules[i];
                var row  = CreateRect(_rulesContent, new Vector2(530, rowH), Vector2.zero);
                row.GetComponent<Image>().color = idx % 2 == 0 ? RowOdd : RowEven;

                // Resource label
                var rl = CreateLabel(row.transform, rule.ResourceTypeName,
                    new Vector2(110, rowH - 4), new Vector2(-185, 0), 12);
                rl.color = TextPrimary; rl.alignment = TextAnchor.MiddleLeft;

                // Piece label
                var pl = CreateLabel(row.transform, rule.PieceTypeName,
                    new Vector2(100, rowH - 4), new Vector2(-55, 0), 12);
                pl.color = TextMuted; pl.alignment = TextAnchor.MiddleLeft;

                // Ratio label
                var ratio = CreateLabel(row.transform,
                    $"{rule.LeftPercent:F0}% L / {100f - rule.LeftPercent:F0}% R",
                    new Vector2(120, rowH - 4), new Vector2(80, 0), 12, FontStyle.Bold);
                ratio.color = Accent; ratio.alignment = TextAnchor.MiddleCenter;

                // Edit slider (inline)
                var sl = CreateSlider(row.transform, new Vector2(80, 14), new Vector2(190, 0));
                sl.minValue = 0; sl.maxValue = 100; sl.value = rule.LeftPercent;
                float capturedLeftPct = rule.LeftPercent;
                sl.onValueChanged.AddListener(v => {
                    rules[idx].LeftPercent = v;
                    ratio.text = $"{v:F0}% L / {100f - v:F0}% R";
                });

                // Delete button
                CreateButton(row.transform, "✕", new Vector2(26, 22),
                    new Vector2(250, 0), Danger, () => {
                        rules.RemoveAt(idx);
                        RefreshRulesList();
                    });
            }

            if (rules.Count == 0)
            {
                var empty = CreateLabel(_rulesContent, "No per-item rules. Items use the global ratio.",
                    new Vector2(500, 28), Vector2.zero, 12);
                empty.color = TextMuted; empty.alignment = TextAnchor.MiddleCenter;
            }

            // Update content rect height
            float total = Mathf.Max(rules.Count * 34f + 8f, 34f);
            var rectTf  = _rulesContent.GetComponent<RectTransform>();
            rectTf.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, total);
        }

        // ── Global manager panel ──────────────────────────────────────────────

        private void BuildManagerPanel()
        {
            _mgrRoot = CreatePanel(_canvas.transform, new Vector2(700, 500),
                new Vector2(0.5f, 0.5f), new Vector2(0, 0));
            _mgrRoot.SetActive(false);

            var header = CreateRect(_mgrRoot.transform, new Vector2(700, 48), new Vector2(0, -24));
            header.GetComponent<Image>().color = HeaderBG;
            var t = CreateLabel(header.transform, "ALL ROLLER SPLITTERS",
                new Vector2(580, 30), new Vector2(-50, 0), 15, FontStyle.Bold, TextAnchor.MiddleLeft);
            t.color = Accent;
            CreateButton(header.transform, "✕", new Vector2(40, 40),
                new Vector2(325, 0), Danger, () => CloseAll());

            // Column headers
            var colRow = CreateRect(_mgrRoot.transform, new Vector2(700, 28), new Vector2(0, -72));
            colRow.GetComponent<Image>().color = AccentDark;
            foreach (var (txt, xPos, w) in new[]{
                ("Position",   -240f, 180f),
                ("Type",       -100f, 100f),
                ("Global L%",    10f,  80f),
                ("Rules",       100f,  50f),
                ("",            290f,  90f)})
            {
                var lbl = CreateLabel(colRow.transform, txt, new Vector2(w, 26), new Vector2(xPos, 0),
                    11, FontStyle.Bold);
                lbl.color = TextPrimary;
            }

            // Scroll area
            var scrollCard = CreateRect(_mgrRoot.transform, new Vector2(680, 360), new Vector2(0, -160));
            scrollCard.GetComponent<Image>().color = CardBG;
            CreateScrollView(scrollCard.transform, new Vector2(678, 356), Vector2.zero, out _mgrContent);
        }

        private void RefreshManagerPanel()
        {
            foreach (Transform c in _mgrContent) Destroy(c.gameObject);

#pragma warning disable CS0618
            var rollers = FindObjectsOfType<RollerSplitter>();
            var t2s     = FindObjectsOfType<ConveyorSplitterT2>();
#pragma warning restore CS0618

            int total = rollers.Length + t2s.Length;
            if (total == 0)
            {
                var e = CreateLabel(_mgrContent, "No splitters found in current scene.",
                    new Vector2(600, 36), Vector2.zero, 13);
                e.color = TextMuted; e.alignment = TextAnchor.MiddleCenter;
                return;
            }

            int row = 0;
            foreach (var s in rollers)  AddManagerRow(s.gameObject, "Roller",    ref row);
            foreach (var s in t2s)      AddManagerRow(s.gameObject, "Smart T2",  ref row);

            var rt = _mgrContent.GetComponent<RectTransform>();
            rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Max(total * 38f + 4f, 38f));
        }

        private void AddManagerRow(GameObject go, string type, ref int rowIndex)
        {
            var cfg = go.GetComponent<MMLSplitterConfig>()
                   ?? go.AddComponent<MMLSplitterConfig>();
            SplitterStorage.ApplyToConfig(cfg);

            var row = CreateRect(_mgrContent, new Vector2(660, 34), Vector2.zero);
            row.GetComponent<Image>().color = rowIndex % 2 == 0 ? RowOdd : RowEven;

            CreateLabel(row.transform, go.transform.position.ToString("F0"),
                new Vector2(180, 28), new Vector2(-225, 0), 11, 
                FontStyle.Normal, TextAnchor.MiddleLeft).color = TextPrimary;
            CreateLabel(row.transform, type,
                new Vector2(100, 28), new Vector2(-95, 0), 11).color = TextMuted;
            CreateLabel(row.transform, $"{cfg.GlobalLeftPercent:F0}%",
                new Vector2(80, 28), new Vector2(15, 0), 11, FontStyle.Bold, 
                TextAnchor.MiddleCenter).color = Accent;
            CreateLabel(row.transform, cfg.PerItemRules.Count.ToString(),
                new Vector2(50, 28), new Vector2(100, 0), 11, FontStyle.Normal, 
                TextAnchor.MiddleCenter).color = TextMuted;

            var capturedCfg = cfg;
            CreateButton(row.transform, "Configure", new Vector2(90, 26),
                new Vector2(270, 0), AccentDark, () => {
                    OpenSingleConfig(capturedCfg);
                });
            rowIndex++;
        }

        // ── Custom dropdown ───────────────────────────────────────────────────

        private void ShowDropdown(Vector3 worldPos, string[] options, Action<string> onSelect)
        {
            DestroyDropdown();
            _ddCallback = onSelect;
            _ddOptions  = options;

            _ddOverlay = new GameObject("MML_Dropdown");
            _ddOverlay.transform.SetParent(_canvas.transform, false);
            var rt = _ddOverlay.AddComponent<RectTransform>();

            // Convert world pos to canvas space
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _canvas.GetComponent<RectTransform>(),
                RectTransformUtility.WorldToScreenPoint(null, worldPos),
                _canvas.worldCamera, out Vector2 localPt);

            float itemH = 28f;
            float panelH = Mathf.Min(options.Length * itemH, 250f);
            rt.anchoredPosition = localPt + new Vector2(0, -panelH / 2f - 16f);
            rt.sizeDelta = new Vector2(180, panelH);

            var bg = _ddOverlay.AddComponent<Image>();
            bg.color = new Color(0.06f, 0.08f, 0.12f, 0.99f);

            // Scroll view for options
            CreateScrollView(_ddOverlay.transform, new Vector2(180, panelH), Vector2.zero,
                out var content);

            foreach (var opt in options)
            {
                string captured = opt;
                var btn = CreateButton(content, opt, new Vector2(178, itemH - 2), Vector2.zero,
                    new Color(0.12f, 0.14f, 0.18f), () => {
                        _ddCallback?.Invoke(captured);
                        DestroyDropdown();
                    });
                var lbl = btn.GetComponentInChildren<Text>();
                lbl.alignment = TextAnchor.MiddleLeft;
                lbl.fontSize  = 12;
                var sr = btn.GetComponent<Image>();
                if (sr) sr.color = new Color(0.12f, 0.14f, 0.18f);
            }
        }

        private void DestroyDropdown()
        {
            if (_ddOverlay != null) { Destroy(_ddOverlay); _ddOverlay = null; }
        }

        // Click anywhere else closes dropdown
        private void Update()
        {
            if (_ddOverlay != null && Input.GetMouseButtonDown(0))
            {
                // check if click is outside dropdown
                var ddRT = _ddOverlay.GetComponent<RectTransform>();
                if (!RectTransformUtility.RectangleContainsScreenPoint(ddRT, Input.mousePosition))
                    DestroyDropdown();
            }
        }

        // ── UI builder helpers ────────────────────────────────────────────────

        private GameObject CreatePanel(Transform parent, Vector2 size, Vector2 anchorPivot, Vector2 anchoredPos)
        {
            var go = new GameObject("Panel");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = anchorPivot;
            rt.pivot     = anchorPivot;
            rt.sizeDelta = size;
            rt.anchoredPosition = anchoredPos;
            var img = go.AddComponent<Image>();
            img.color = BG;
            go.AddComponent<GraphicRaycaster>();
            return go;
        }

        private RectTransform CreateRect(Transform parent, Vector2 size, Vector2 anchoredPos)
        {
            var go = new GameObject("Rect");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot     = new Vector2(0.5f, 1f);
            rt.sizeDelta = size;
            rt.anchoredPosition = anchoredPos;
            go.AddComponent<Image>().color = Color.clear;
            return rt;
        }

        private Text CreateLabel(Transform parent, string text, Vector2 size, Vector2 pos,
            int fontSize = 12, FontStyle style = FontStyle.Normal,
            TextAnchor anchor = TextAnchor.MiddleCenter)
        {
            var go = new GameObject("Label");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
            var t = go.AddComponent<Text>();
            t.font      = _font;
            t.text      = text;
            t.fontSize  = fontSize;
            t.fontStyle = style;
            t.alignment = anchor;
            t.color     = TextPrimary;
            return t;
        }

        private void CreateSectionHeader(Transform parent, string text, Vector2 pos)
        {
            var go = new GameObject("SectionHeader");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot     = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(560, 26);
            rt.anchoredPosition = pos;
            var img = go.AddComponent<Image>();
            img.color = new Color(0.15f, 0.17f, 0.22f);
            var t = CreateLabel(go.transform, text, new Vector2(540, 22), new Vector2(0, 0),
                11, FontStyle.Bold, TextAnchor.MiddleLeft);
            t.color = new Color(0.6f, 0.8f, 1f);
            t.GetComponent<RectTransform>().anchoredPosition = new Vector2(10, 0);
        }

        private Button CreateButton(Transform parent, string label, Vector2 size, Vector2 pos,
            Color color, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject("Button");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
            var img = go.AddComponent<Image>();
            img.color = color;
            var btn = go.AddComponent<Button>();
            var cols        = btn.colors;
            cols.normalColor      = color;
            cols.highlightedColor = Color.Lerp(color, Color.white, 0.25f);
            cols.pressedColor     = Color.Lerp(color, Color.black, 0.2f);
            cols.fadeDuration     = 0.08f;
            btn.colors    = cols;
            btn.targetGraphic = img;
            btn.onClick.AddListener(onClick);

            var textGO = new GameObject("Text");
            textGO.transform.SetParent(go.transform, false);
            var trt = textGO.AddComponent<RectTransform>();
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
            trt.sizeDelta = Vector2.zero; trt.anchoredPosition = Vector2.zero;
            var t = textGO.AddComponent<Text>();
            t.font      = _font;
            t.text      = label;
            t.fontSize  = 12;
            t.alignment = TextAnchor.MiddleCenter;
            t.color     = Color.white;
            return btn;
        }

        private Button CreateDropdownButton(Transform parent, string label, Vector2 size, Vector2 pos)
        {
            var btn = CreateButton(parent, label, size, pos,
                new Color(0.15f, 0.18f, 0.24f), () => { });
            // Add dropdown arrow
            var arr = CreateLabel(btn.transform, "▼", new Vector2(18, size.y), new Vector2(size.x / 2f - 12f, 0), 9);
            arr.color = TextMuted;
            return btn;
        }

        private Slider CreateSlider(Transform parent, Vector2 size, Vector2 pos)
        {
            var go = new GameObject("Slider");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
            var slider = go.AddComponent<Slider>();

            // Background
            var bg = new GameObject("Background");
            bg.transform.SetParent(go.transform, false);
            var bgrt = bg.AddComponent<RectTransform>();
            bgrt.anchorMin = new Vector2(0, 0.25f); bgrt.anchorMax = new Vector2(1, 0.75f);
            bgrt.sizeDelta = Vector2.zero; bgrt.anchoredPosition = Vector2.zero;
            bg.AddComponent<Image>().color = new Color(0.18f, 0.2f, 0.25f);

            // Fill area
            var fillArea = new GameObject("Fill Area");
            fillArea.transform.SetParent(go.transform, false);
            var fart = fillArea.AddComponent<RectTransform>();
            fart.anchorMin = new Vector2(0, 0.25f); fart.anchorMax = new Vector2(1, 0.75f);
            fart.sizeDelta = new Vector2(-10, 0); fart.anchoredPosition = new Vector2(-5, 0);
            var fill = new GameObject("Fill");
            fill.transform.SetParent(fillArea.transform, false);
            var frt = fill.AddComponent<RectTransform>();
            frt.anchorMin = Vector2.zero; frt.anchorMax = new Vector2(0.5f, 1f);
            frt.sizeDelta = Vector2.zero;
            var fillImg = fill.AddComponent<Image>();
            fillImg.color = Accent;
            slider.fillRect = frt;

            // Handle
            var handleArea = new GameObject("Handle Slide Area");
            handleArea.transform.SetParent(go.transform, false);
            var hart = handleArea.AddComponent<RectTransform>();
            hart.anchorMin = Vector2.zero; hart.anchorMax = Vector2.one;
            hart.sizeDelta = new Vector2(-10, 0); hart.anchoredPosition = Vector2.zero;
            var handle = new GameObject("Handle");
            handle.transform.SetParent(handleArea.transform, false);
            var hrt = handle.AddComponent<RectTransform>();
            hrt.sizeDelta = new Vector2(16, 16);
            var himg = handle.AddComponent<Image>();
            himg.color = Color.white;
            slider.handleRect = hrt;
            slider.targetGraphic = himg;

            slider.direction = Slider.Direction.LeftToRight;
            return slider;
        }

        private ScrollRect CreateScrollView(Transform parent, Vector2 size, Vector2 pos, out Transform content)
        {
            var go = new GameObject("ScrollView");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
            go.AddComponent<Image>().color = Color.clear;
            var scroll = go.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical   = true;
            scroll.scrollSensitivity = 20f;

            // Viewport
            var vp = new GameObject("Viewport");
            vp.transform.SetParent(go.transform, false);
            var vprt = vp.AddComponent<RectTransform>();
            vprt.anchorMin = Vector2.zero; vprt.anchorMax = Vector2.one;
            vprt.sizeDelta = Vector2.zero; vprt.anchoredPosition = Vector2.zero;
            vp.AddComponent<Image>().color = Color.clear;
            var mask = vp.AddComponent<Mask>();
            mask.showMaskGraphic = false;
            scroll.viewport = vprt;

            // Content
            var ct = new GameObject("Content");
            ct.transform.SetParent(vp.transform, false);
            var ctrt = ct.AddComponent<RectTransform>();
            ctrt.anchorMin = new Vector2(0, 1); ctrt.anchorMax = new Vector2(1, 1);
            ctrt.pivot     = new Vector2(0.5f, 1f);
            ctrt.sizeDelta = new Vector2(0, 0);
            ctrt.anchoredPosition = Vector2.zero;
            scroll.content = ctrt;

            var layout = ct.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 2;
            layout.childControlWidth  = true;
            layout.childControlHeight = false;
            layout.childAlignment     = TextAnchor.UpperCenter;
            layout.padding = new RectOffset(2, 2, 2, 2);

            ct.AddComponent<ContentSizeFitter>().verticalFit =
                ContentSizeFitter.FitMode.PreferredSize;

            content = ctrt;
            return scroll;
        }
    }
}
