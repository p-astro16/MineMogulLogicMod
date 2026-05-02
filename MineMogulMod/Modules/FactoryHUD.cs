using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MineMogulMod.Modules
{
    // ── Drag handler for the window title bar ────────────────────────────────
    internal class UIDragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler
    {
        public RectTransform? Target;
        private Vector2 _lastMouse;

        public void OnBeginDrag(PointerEventData e)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                Target!.parent as RectTransform, e.position, e.pressEventCamera, out _lastMouse);
        }

        public void OnDrag(PointerEventData e)
        {
            if (Target == null) return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                Target.parent as RectTransform, e.position, e.pressEventCamera, out var cur);
            Target.anchoredPosition += cur - _lastMouse;
            _lastMouse = cur;
        }
    }

    // ── Factory HUD — uGUI canvas overlay ────────────────────────────────────
    public class FactoryHUD : MonoBehaviour
    {
        public static FactoryHUD? Instance { get; private set; }

        // ── Colors ────────────────────────────────────────────────────────────
        static Color C(int r, int g, int b, int a = 255) =>
            new Color(r / 255f, g / 255f, b / 255f, a / 255f);

        static readonly Color ColBG        = C(8,   10,  16,  248);
        static readonly Color ColSurface   = C(13,  16,  26,  255);
        static readonly Color ColSurface2  = C(10,  13,  21,  255);
        static readonly Color ColAccent    = C(51,  181, 229, 255);
        static readonly Color ColGreen     = C(41,  210, 110, 255);
        static readonly Color ColRed       = C(220, 60,  60,  255);
        static readonly Color ColYellow    = C(230, 180, 30,  255);
        static readonly Color ColText      = C(210, 220, 240, 255);
        static readonly Color ColMuted     = C(90,  105, 140, 255);
        static readonly Color ColTabActive = C(33,  150, 243, 255);
        static readonly Color ColTabIdle   = C(20,  26,  44,  255);
        static readonly Color ColRowEven   = C(14,  17,  27,  255);
        static readonly Color ColRowOdd    = C(19,  23,  36,  255);
        static readonly Color ColSection   = C(25,  70,  115, 255);
        static readonly Color ColBorder    = C(30,  38,  62,  255);

        // ── State ─────────────────────────────────────────────────────────────
        private bool _uiReady  = false;
        private bool _visible  = false;
        private int  _tab      = 0;
        private readonly string[] _tabNames = { "Machines", "Belts", "Sales", "Settings" };

        // ── Cursor ────────────────────────────────────────────────────────────
        private CursorLockMode _prevLock;
        private bool           _prevCursorVis;

        // ── Data cache ────────────────────────────────────────────────────────
        private float _nextRefresh;
        private List<SorterMachine>    _sorters   = new();
        private List<ConveyorBelt>     _belts     = new();
        private List<PolishingMachine> _polishers = new();
        private List<SellerMachine>    _sellers   = new();

        // ── UI roots ──────────────────────────────────────────────────────────
        private GameObject? _canvasGO;
        private Text?        _headerTxt;
        private Button[]     _tabBtns    = Array.Empty<Button>();
        private Image[]      _tabBtnImgs = Array.Empty<Image>();
        private GameObject[] _tabPanels  = Array.Empty<GameObject>();

        private RectTransform? _machContent, _beltContent, _salesContent;

        private readonly List<(Text lbl, ConfigEntry<float> cfg, float min, float max, float step)>
            _stepRows = new();
        private readonly List<(Image dot, ConfigEntry<bool> cfg)> _toggleRows = new();

        // ── Public API ────────────────────────────────────────────────────────
        public void ToggleVisibility() => SetVisible(!_visible);

        // ── Lifecycle ─────────────────────────────────────────────────────────
        private void Awake()
        {
            Instance = this;
            try   { BuildCanvas(); _uiReady = true; }
            catch (Exception ex)
            {
                BepInEx.Logging.Logger.CreateLogSource("MML")
                    .LogError($"uGUI init failed, falling back to IMGUI: {ex}");
            }
            if (_canvasGO != null) _canvasGO.SetActive(false);
        }

        private void OnDestroy()
        {
            RestoreCursor();
            if (_canvasGO != null) Destroy(_canvasGO);
            Instance = null;
        }

        private void SetVisible(bool v)
        {
            _visible = v;
            if (_canvasGO != null) _canvasGO.SetActive(v);
            if (v)
            {
                _prevLock      = Cursor.lockState;
                _prevCursorVis = Cursor.visible;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible   = true;
                RefreshAll();
            }
            else RestoreCursor();
        }

        private void RestoreCursor()
        {
            Cursor.lockState = _prevLock;
            Cursor.visible   = _prevCursorVis;
        }

        // ── Update ────────────────────────────────────────────────────────────
        private void Update()
        {
            if (ModSettings.HUDToggleKey.Value.IsDown()) SetVisible(!_visible);
            if (!_visible || !_uiReady) return;
            if (Time.time >= _nextRefresh) RefreshAll();
            UpdateHeader();
            SyncSettingsRows();
        }

        private void RefreshAll()
        {
            _nextRefresh = Time.time + ModSettings.HUDRefreshInterval.Value;
#pragma warning disable CS0618
            _sorters   = FindObjectsOfType<SorterMachine>().ToList();
            _belts     = FindObjectsOfType<ConveyorBelt>().ToList();
            _polishers = FindObjectsOfType<PolishingMachine>().ToList();
            _sellers   = FindObjectsOfType<SellerMachine>().ToList();
#pragma warning restore CS0618
            RebuildMachinesContent();
            RebuildBeltsContent();
            RebuildSalesContent();
        }

        private void UpdateHeader()
        {
            if (_headerTxt == null) return;
            var s = SalesTracker.Instance;
            _headerTxt.text = s != null
                ? $"  💰  €{s.TotalEarned:N0}   ·   {s.TotalItemsSold} items   ·   €{s.EarnedInLast(60f):N0}/min"
                : "  No sales data yet";
        }

        private void SyncSettingsRows()
        {
            foreach (var (lbl, cfg, _, _, _) in _stepRows)
                if (lbl != null) lbl.text = cfg.Value.ToString("G4");
            foreach (var (dot, cfg) in _toggleRows)
                if (dot != null) dot.color = cfg.Value ? ColGreen : ColMuted;
        }

        // ── Tab switching ─────────────────────────────────────────────────────
        private void SwitchTab(int idx)
        {
            _tab = idx;
            for (int i = 0; i < _tabPanels.Length; i++)
            {
                if (_tabPanels[i] != null) _tabPanels[i].SetActive(i == idx);
                if (i < _tabBtnImgs.Length && _tabBtnImgs[i] != null)
                    _tabBtnImgs[i].color = (i == idx) ? ColTabActive : ColTabIdle;
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // BUILD CANVAS
        // ─────────────────────────────────────────────────────────────────────
        private void BuildCanvas()
        {
            _canvasGO = new GameObject("MML_HUD_Canvas");
            DontDestroyOnLoad(_canvasGO);

            var canvas = _canvasGO.AddComponent<Canvas>();
            canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            var scaler = _canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight  = 0.5f;
            _canvasGO.AddComponent<GraphicRaycaster>();

            EnsureEventSystem();

            // ── Window 480 × 640, anchored top-right ──────────────────────────
            var win = MakeGO("Window", _canvasGO);
            SetImg(win, ColBG);
            var winRT = win.GetComponent<RectTransform>();
            winRT.anchorMin = winRT.anchorMax = winRT.pivot = new Vector2(1f, 1f);
            winRT.anchoredPosition = new Vector2(-20f, -20f);
            winRT.sizeDelta = new Vector2(480f, 640f);

            // Border tint (sits behind everything)
            var border = MakeGO("Border", win);
            SetImg(border, ColBorder);
            StretchFill(border);
            border.transform.SetAsFirstSibling();

            // ── Title bar ────────────────────────────────────────────────────
            var titleBar = HRow("TitleBar", win, 38);
            SetImg(titleBar, ColSurface);
            titleBar.AddComponent<UIDragHandler>().Target = winRT;

            var titleTxt = AddText(titleBar, "  MML  —  Mine Mogul Logic  v1.0", 13, ColText, TextAnchor.MiddleLeft);
            titleTxt.GetComponent<LayoutElement>().flexibleWidth = 1;

            var closeGO = MakeGO("Close", titleBar);
            SetImg(closeGO, C(155, 30, 30, 220));
            var closeLe = closeGO.AddComponent<LayoutElement>();
            closeLe.preferredWidth  = 34;
            closeLe.preferredHeight = 38;
            var closeBtn = closeGO.AddComponent<Button>();
            closeBtn.targetGraphic = closeGO.GetComponent<Image>();
            closeBtn.onClick.AddListener(() => SetVisible(false));
            var closeTxt = AddText(closeGO, "✕", 14, ColText, TextAnchor.MiddleCenter);
            StretchFill(closeTxt.gameObject);

            // ── Stats bar ───────────────────────────────────────────────────
            var statsBar = HRow("Stats", win, 26);
            SetImg(statsBar, ColSurface2);
            _headerTxt = AddText(statsBar, "  —", 11, ColMuted, TextAnchor.MiddleLeft);
            _headerTxt.GetComponent<LayoutElement>().flexibleWidth = 1;
            var hintTxt = AddText(statsBar, "F5  ", 10, C(40, 50, 75), TextAnchor.MiddleRight);
            hintTxt.GetComponent<LayoutElement>().preferredWidth = 30;

            // ── Tab bar ─────────────────────────────────────────────────────
            var tabBar = HRow("TabBar", win, 32);
            SetImg(tabBar, C(10, 12, 20));
            _tabBtns    = new Button[_tabNames.Length];
            _tabBtnImgs = new Image[_tabNames.Length];
            _tabPanels  = new GameObject[_tabNames.Length];

            for (int i = 0; i < _tabNames.Length; i++)
            {
                int idx = i;
                var tbGO  = MakeGO("Tab" + i, tabBar);
                var tbImg = SetImg(tbGO, i == 0 ? ColTabActive : ColTabIdle);
                var tbLe  = tbGO.AddComponent<LayoutElement>();
                tbLe.preferredHeight = 32;
                tbLe.flexibleWidth   = 1;
                var tbBtn = tbGO.AddComponent<Button>();
                tbBtn.targetGraphic = tbImg;
                var tc = tbBtn.colors;
                tc.highlightedColor = C(28, 38, 62);
                tc.pressedColor     = C(20, 100, 180);
                tbBtn.colors = tc;
                tbBtn.onClick.AddListener(() => SwitchTab(idx));
                var tbTxt = AddText(tbGO, _tabNames[i], 12, ColText, TextAnchor.MiddleCenter);
                StretchFill(tbTxt.gameObject);
                _tabBtns[i]    = tbBtn;
                _tabBtnImgs[i] = tbImg;
            }

            // Accent separator
            var sep = HRow("Sep", win, 2);
            SetImg(sep, ColAccent);

            // ── Content area ────────────────────────────────────────────────
            var content = MakeGO("Content", win);
            SetImg(content, ColBG);
            var cLe = content.AddComponent<LayoutElement>();
            cLe.preferredHeight = 542;
            cLe.flexibleHeight  = 1;

            // VerticalLayoutGroup to stack the above rows automatically
            var vlg = win.AddComponent<VerticalLayoutGroup>();
            vlg.childForceExpandWidth  = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth      = true;
            vlg.childControlHeight     = true;
            vlg.spacing = 0;
            vlg.padding = new RectOffset(0, 0, 0, 0);

            // Tab panels all sit inside content, stacked (only one active at a time)
            for (int i = 0; i < _tabNames.Length; i++)
            {
                var panel = MakeGO("Panel" + i, content);
                SetImg(panel, ColBG);
                StretchFill(panel);
                _tabPanels[i] = panel;
                panel.SetActive(i == 0);
            }

            _machContent  = BuildScrollContent(_tabPanels[0]);
            _beltContent  = BuildScrollContent(_tabPanels[1]);
            _salesContent = BuildScrollContent(_tabPanels[2]);
            BuildSettingsContent(_tabPanels[3]);
        }

        // ── Scroll view factory ────────────────────────────────────────────────
        private RectTransform BuildScrollContent(GameObject panel)
        {
            var scrollGO = MakeGO("Scroll", panel);
            StretchFill(scrollGO);
            SetImg(scrollGO, C(0, 0, 0, 0));
            var scroll = scrollGO.AddComponent<ScrollRect>();
            scroll.horizontal        = false;
            scroll.vertical          = true;
            scroll.scrollSensitivity = 20f;

            var vp = MakeGO("Viewport", scrollGO);
            StretchFill(vp);
            var vpImg = SetImg(vp, C(0, 0, 0, 0));
            vp.AddComponent<Mask>().showMaskGraphic = false;
            scroll.viewport = vp.GetComponent<RectTransform>();

            var contentGO = MakeGO("Content", vp);
            var cRT = contentGO.AddComponent<RectTransform>();
            cRT.anchorMin = new Vector2(0, 1);
            cRT.anchorMax = new Vector2(1, 1);
            cRT.pivot     = new Vector2(0.5f, 1);
            cRT.anchoredPosition = Vector2.zero;
            cRT.sizeDelta = Vector2.zero;

            var cvlg = contentGO.AddComponent<VerticalLayoutGroup>();
            cvlg.childForceExpandWidth  = true;
            cvlg.childForceExpandHeight = false;
            cvlg.childControlWidth      = true;
            cvlg.childControlHeight     = true;
            cvlg.spacing = 1;
            cvlg.padding = new RectOffset(4, 4, 4, 4);
            contentGO.AddComponent<ContentSizeFitter>().verticalFit =
                ContentSizeFitter.FitMode.PreferredSize;

            scroll.content = cRT;
            return cRT;
        }

        // ── Settings content (built once) ─────────────────────────────────────
        private void BuildSettingsContent(GameObject panel)
        {
            var c = BuildScrollContent(panel);

            SectionRow(c, "Modules");
            TogRow(c, "Throughput Tracker",   ModSettings.EnableThroughputTracker);
            TogRow(c, "Bottleneck Detector",  ModSettings.EnableBottleneckDetector);
            TogRow(c, "Sales Tracker",        ModSettings.EnableSalesTracker);
            TogRow(c, "Ore Analyser Upgrade", ModSettings.EnableOreAnalyserUpgrade);

            SectionRow(c, "Throughput Tracker");
            StepRow(c, "Window (sec)",     ModSettings.ThroughputWindowSeconds,   10f, 300f, 5f);

            SectionRow(c, "Bottleneck Detector");
            StepRow(c, "Refresh (sec)",         ModSettings.BottleneckRefreshInterval, 1f, 60f, 1f);
            StepRow(c, "Min factory items/min", ModSettings.BottleneckMinFactoryIPM,   0f, 100f, 5f);

            SectionRow(c, "Factory HUD");
            StepRow(c, "Refresh (sec)", ModSettings.HUDRefreshInterval, 0.5f, 30f, 0.5f);

            SectionRow(c, "Ore Analyser");
            TogRow(c, "Show sales history", ModSettings.AnalyserShowSalesHistory);

            // Reset row
            var rRow = HRow("ResetRow", c.gameObject, 36);
            SetImg(rRow, C(0, 0, 0, 0));
            var rGO = MakeGO("ResetBtn", rRow);
            var rImg = SetImg(rGO, C(140, 30, 30, 220));
            var rLe  = rGO.AddComponent<LayoutElement>();
            rLe.flexibleWidth   = 1;
            rLe.preferredHeight = 28;
            var rBtn = rGO.AddComponent<Button>();
            rBtn.targetGraphic = rImg;
            var rc = rBtn.colors; rc.highlightedColor = C(180, 40, 40); rBtn.colors = rc;
            rBtn.onClick.AddListener(() => {
                ModSettings.EnableThroughputTracker.Value       = (bool)ModSettings.EnableThroughputTracker.DefaultValue;
                ModSettings.EnableBottleneckDetector.Value      = (bool)ModSettings.EnableBottleneckDetector.DefaultValue;
                ModSettings.EnableSalesTracker.Value            = (bool)ModSettings.EnableSalesTracker.DefaultValue;
                ModSettings.EnableOreAnalyserUpgrade.Value      = (bool)ModSettings.EnableOreAnalyserUpgrade.DefaultValue;
                ModSettings.ThroughputWindowSeconds.Value       = (float)ModSettings.ThroughputWindowSeconds.DefaultValue;
                ModSettings.BottleneckRefreshInterval.Value     = (float)ModSettings.BottleneckRefreshInterval.DefaultValue;
                ModSettings.BottleneckMinFactoryIPM.Value       = (float)ModSettings.BottleneckMinFactoryIPM.DefaultValue;
                ModSettings.HUDRefreshInterval.Value            = (float)ModSettings.HUDRefreshInterval.DefaultValue;
                ModSettings.AnalyserShowSalesHistory.Value      = (bool)ModSettings.AnalyserShowSalesHistory.DefaultValue;
            });
            var rTxt = AddText(rGO, "↺  Reset all settings to default", 11, ColText, TextAnchor.MiddleCenter);
            StretchFill(rTxt.gameObject);
        }

        // ── Dynamic content ────────────────────────────────────────────────────

        private void RebuildMachinesContent()
        {
            if (_machContent == null) return;
            ClearChildren(_machContent.gameObject);

            SectionRow(_machContent, $"Sorters  ({_sorters.Count})");
            bool even = false;
            foreach (var s in _sorters)
            {
                if (s == null) continue;
                float ipm  = ThroughputTracker.Instance?.GetThroughput(s.GetComponentInChildren<ConveyorBelt>()) ?? 0f;
                bool  isBot = BottleneckDetector.Instance?.IsBottleneck(s.GetComponentInChildren<ConveyorBelt>()) ?? false;
                int   crit  = ReflectionUtils.GetFilterCriteriaCount(s.Filter);
                string txt  = $"{(isBot ? "⚠  " : "")}Sorter   ·   {ipm:F1} ipm   ·   {crit} filter{(crit != 1 ? "s" : "")}";
                DataRow(_machContent, txt, isBot ? ColRed : ipm < 5f ? ColYellow : ColText, even);
                even = !even;
            }
            if (_sorters.Count == 0) DataRow(_machContent, "No sorters found.", ColMuted, false);

            SectionRow(_machContent, $"Polishers  ({_polishers.Count})");
            even = false;
            foreach (var p in _polishers)
            {
                if (p == null) continue;
                DataRow(_machContent, $"Polisher   ·   speed {ReflectionUtils.GetPolisherStandardSpeed(p):F1}", ColText, even);
                even = !even;
            }
            if (_polishers.Count == 0) DataRow(_machContent, "No polishers found.", ColMuted, false);
        }

        private void RebuildBeltsContent()
        {
            if (_beltContent == null) return;
            ClearChildren(_beltContent.gameObject);

            var tracker = ThroughputTracker.Instance;
            if (tracker == null) { DataRow(_beltContent, "Throughput tracker not active.", ColMuted, false); return; }

            var all = tracker.GetAllThroughputs();
            SectionRow(_beltContent, $"{all.Count} active belts  —  low → high");

            bool even = false; int i = 1;
            foreach (var (belt, ipm) in all)
            {
                if (belt == null) continue;
                Color col = ipm < 5f ? ColRed : ipm < 20f ? ColYellow : ColGreen;
                DataRow(_beltContent,
                    $"#{i++}   {ipm:F1} ipm   speed={belt.Speed:F1}   objs={ReflectionUtils.GetPhysicsObjectCount(belt)}",
                    col, even);
                even = !even;
            }
            if (all.Count == 0) DataRow(_beltContent, "No belt data yet.", ColMuted, false);
        }

        private void RebuildSalesContent()
        {
            if (_salesContent == null) return;
            ClearChildren(_salesContent.gameObject);

            var sales = SalesTracker.Instance;
            if (sales == null) { DataRow(_salesContent, "Sales tracker not active.", ColMuted, false); return; }

            float avgMin = sales.SessionDurationMinutes > 0
                ? sales.TotalEarned / sales.SessionDurationMinutes : 0f;

            SectionRow(_salesContent, "Summary");
            DataRow(_salesContent, $"Total earned       €{sales.TotalEarned:N2}",   ColGreen,  false);
            DataRow(_salesContent, $"Session duration   {sales.SessionDurationMinutes:F1} min", ColText, true);
            DataRow(_salesContent, $"Average per min    €{avgMin:N2}",               ColText,   false);
            DataRow(_salesContent, $"Total items sold   {sales.TotalItemsSold}",     ColText,   true);

            SectionRow(_salesContent, "Top Resources");
            bool even = false;
            foreach (var (resource, money, count) in sales.GetTopResources(20))
            {
                float perUnit = money / Mathf.Max(count, 1);
                DataRow(_salesContent, $"{resource,-14}  €{money:N2}  ({count}×  avg €{perUnit:N2})", ColText, even);
                even = !even;
            }
        }

        // ── Row builders ──────────────────────────────────────────────────────

        private void SectionRow(RectTransform parent, string t) => SectionRow(parent.gameObject, t);
        private void SectionRow(GameObject parent, string title)
        {
            var row = HRow("Sec", parent, 22);
            SetImg(row, ColSection);
            var lbl = AddText(row, $"  {title.ToUpper()}", 10, C(160, 210, 240), TextAnchor.MiddleLeft);
            lbl.GetComponent<LayoutElement>().flexibleWidth = 1;
            lbl.fontStyle = FontStyle.Bold;
        }

        private void DataRow(RectTransform parent, string text, Color col, bool even) =>
            DataRow(parent.gameObject, text, col, even);
        private void DataRow(GameObject parent, string text, Color col, bool even)
        {
            var row = HRow("Row", parent, 22);
            SetImg(row, even ? ColRowEven : ColRowOdd);
            // Left accent bar
            var acc = MakeGO("Acc", row);
            SetImg(acc, new Color(col.r, col.g, col.b, 0.45f));
            var accLe = acc.AddComponent<LayoutElement>();
            accLe.preferredWidth  = 3;
            accLe.preferredHeight = 22;
            var lbl = AddText(row, $"  {text}", 11, col, TextAnchor.MiddleLeft);
            lbl.GetComponent<LayoutElement>().flexibleWidth = 1;
        }

        private void TogRow(RectTransform parent, string label, ConfigEntry<bool> entry) =>
            TogRow(parent.gameObject, label, entry);
        private void TogRow(GameObject parent, string label, ConfigEntry<bool> entry)
        {
            var row = HRow("Tog_" + label, parent, 28);
            SetImg(row, ColRowEven);

            // Dot indicator
            var dotGO  = MakeGO("Dot", row);
            var dotImg = SetImg(dotGO, entry.Value ? ColGreen : ColMuted);
            var dotLe  = dotGO.AddComponent<LayoutElement>();
            dotLe.preferredWidth = dotLe.preferredHeight = 28;
            _toggleRows.Add((dotImg, entry));

            var lbl = AddText(row, label, 11, ColText, TextAnchor.MiddleLeft);
            lbl.GetComponent<LayoutElement>().flexibleWidth = 1;

            var rowImg = row.GetComponent<Image>();
            var rowBtn = row.AddComponent<Button>();
            rowBtn.targetGraphic = rowImg;
            var rc = rowBtn.colors;
            rc.normalColor      = ColRowEven;
            rc.highlightedColor = C(24, 32, 52);
            rc.pressedColor     = C(18, 24, 40);
            rowBtn.colors = rc;
            rowBtn.onClick.AddListener(() => {
                entry.Value = !entry.Value;
                dotImg.color = entry.Value ? ColGreen : ColMuted;
            });
        }

        private void StepRow(RectTransform parent, string label, ConfigEntry<float> entry,
            float min, float max, float step)
        {
            var row = HRow("Step_" + label, parent.gameObject, 30);
            SetImg(row, ColRowOdd);
            var lbl = AddText(row, $"  {label}", 11, ColText, TextAnchor.MiddleLeft);
            lbl.GetComponent<LayoutElement>().flexibleWidth = 1;

            SmallBtn(row, "−", () => { entry.Value = Mathf.Clamp(entry.Value - step, min, max); });

            var valGO = MakeGO("Val", row);
            SetImg(valGO, ColSurface);
            var valLe = valGO.AddComponent<LayoutElement>();
            valLe.preferredWidth  = 56;
            valLe.preferredHeight = 28;
            var valTxt = AddText(valGO, entry.Value.ToString("G4"), 11, ColAccent, TextAnchor.MiddleCenter);
            StretchFill(valTxt.gameObject);
            _stepRows.Add((valTxt, entry, min, max, step));

            SmallBtn(row, "+", () => { entry.Value = Mathf.Clamp(entry.Value + step, min, max); });
        }

        private static void SmallBtn(GameObject parent, string lbl, UnityEngine.Events.UnityAction action)
        {
            var go  = MakeGO(lbl + "SmBtn", parent);
            var img = SetImg(go, ColSurface);
            var le  = go.AddComponent<LayoutElement>();
            le.preferredWidth = le.preferredHeight = 28;
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            var c = btn.colors;
            c.highlightedColor = C(45, 130, 200);
            c.pressedColor     = C(30, 90, 150);
            btn.colors = c;
            btn.onClick.AddListener(action);
            var t = AddText(go, lbl, 14, ColAccent, TextAnchor.MiddleCenter);
            StretchFill(t.gameObject);
        }

        // ── Static helpers ────────────────────────────────────────────────────

        private static void EnsureEventSystem()
        {
#pragma warning disable CS0618
            if (FindObjectOfType<EventSystem>() != null) { return; }
#pragma warning restore CS0618
            var go = new GameObject("MML_EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<StandaloneInputModule>();
            DontDestroyOnLoad(go);
        }

        private static GameObject MakeGO(string name, GameObject parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            go.AddComponent<RectTransform>();
            return go;
        }

        private static GameObject MakeGO(string name, RectTransform parent) =>
            MakeGO(name, parent.gameObject);

        private static Image SetImg(GameObject go, Color color)
        {
            var img = go.GetComponent<Image>() ?? go.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = true;
            return img;
        }

        // Fixed-height horizontal row (adds LayoutElement + HorizontalLayoutGroup)
        private static GameObject HRow(string name, GameObject parent, int height)
        {
            var go  = MakeGO(name, parent);
            var le  = go.AddComponent<LayoutElement>();
            le.preferredHeight = height;
            le.minHeight       = height;
            le.flexibleWidth   = 1;
            var hlg = go.AddComponent<HorizontalLayoutGroup>();
            hlg.childAlignment         = TextAnchor.MiddleLeft;
            hlg.childForceExpandWidth  = false;
            hlg.childForceExpandHeight = true;
            hlg.childControlWidth      = true;
            hlg.childControlHeight     = true;
            hlg.spacing = 0;
            hlg.padding = new RectOffset(0, 0, 0, 0);
            return go;
        }

        private static GameObject HRow(string name, RectTransform parent, int height) =>
            HRow(name, parent.gameObject, height);

        private static Text AddText(GameObject parent, string text, int size, Color color,
            TextAnchor anchor = TextAnchor.MiddleLeft)
        {
            var go  = MakeGO("Txt", parent);
            var le  = go.AddComponent<LayoutElement>();
            le.flexibleWidth = 1;
            var txt = go.AddComponent<Text>();
            txt.text               = text;
            txt.fontSize           = size;
            txt.color              = color;
            txt.alignment          = anchor;
            txt.supportRichText    = true;
            txt.horizontalOverflow = HorizontalWrapMode.Overflow;
            txt.verticalOverflow   = VerticalWrapMode.Overflow;
            txt.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            return txt;
        }

        private static void StretchFill(GameObject go)
        {
            var rt = go.GetComponent<RectTransform>() ?? go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        private static void ClearChildren(GameObject go)
        {
            foreach (Transform child in go.transform)
                Destroy(child.gameObject);
        }
    }
}