using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

namespace MineMogulModInstaller
{
    public partial class InstallerForm : Form
    {
        // ── Win32 ─────────────────────────────────────────────────────────────
        [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr h, int a, ref int v, int s);
        private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
        private const int DWMWCP_ROUND = 2;

        // ── Colors ────────────────────────────────────────────────────────────
        static readonly Color BG          = Color.FromArgb(10, 11, 16);
        static readonly Color Surface     = Color.FromArgb(15, 17, 24);
        static readonly Color CardBG      = Color.FromArgb(18, 21, 30);
        static readonly Color CardBorder  = Color.FromArgb(30, 35, 52);
        static readonly Color Accent      = Color.FromArgb(51, 181, 229);
        static readonly Color AccentGreen = Color.FromArgb(41, 210, 110);
        static readonly Color AccentRed   = Color.FromArgb(220, 60, 60);
        static readonly Color AccentWarn  = Color.FromArgb(230, 160, 30);
        static readonly Color TextPrimary = Color.FromArgb(220, 225, 240);
        static readonly Color TextMuted   = Color.FromArgb(90, 100, 130);
        static readonly Color LogGreen    = Color.FromArgb(100, 220, 120);

        // ── Game path ─────────────────────────────────────────────────────────

        // ── Controls ──────────────────────────────────────────────────────────
        private TextBox      txtGamePath    = new();
        private TextBox      txtLog         = new();
        private CheckBox     chkRemoveBepInEx = new();
        private Panel        pnlHeader      = new();
        private Panel        pnlProgress    = new();

        // ── Animation ─────────────────────────────────────────────────────────
        private float  _fadeOpacity    = 0f;
        private float  _glowPhase      = 0f;
        private float  _shimmerOffset  = -400f;
        private float  _statusPulse    = 0f;
        private float  _progressTarget = 0f;
        private float  _progressCurrent= 0f;
        private bool   _isBusy         = false;
        private readonly System.Windows.Forms.Timer _animTimer = new();

        // Per-button hover (0-1 float)
        private float _hoverInstall=0, _hoverUninstall=0, _hoverBrowse=0, _hoverAuto=0, _hoverFolder=0;
        private readonly System.Windows.Forms.Timer _btnTimer = new();
        private bool _mouseOnInstall, _mouseOnUninstall, _mouseOnBrowse, _mouseOnAuto, _mouseOnFolder;

        // ── Status ────────────────────────────────────────────────────────────
        private enum Status { None, NotInstalled, BepInExOnly, Installed }
        private Status _status = Status.None;

        // ── Layout constants ──────────────────────────────────────────────────
        private const int W = 660;
        private const int HEADER_H = 70;

        // ─────────────────────────────────────────────────────────────────────
        public InstallerForm()
        {
            SuspendLayout();
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode  = AutoScaleMode.Font;
            ClientSize     = new Size(W, 490);
            FormBorderStyle= FormBorderStyle.FixedSingle;
            MaximizeBox    = false;
            StartPosition  = FormStartPosition.CenterScreen;
            Text           = "MML  —  Installer  v1.0.0";
            BackColor      = BG;
            DoubleBuffered = true;
            Opacity        = 0;

            BuildUI();
            ResumeLayout(false);

            try { int p = DWMWCP_ROUND; DwmSetWindowAttribute(Handle, DWMWA_WINDOW_CORNER_PREFERENCE, ref p, sizeof(int)); } catch { }
            Icon = LandingForm.LoadAppIcon() ?? SystemIcons.Application;

            // Anim timers
            _animTimer.Interval = 16;
            _animTimer.Tick += OnAnimTick;
            _animTimer.Start();
            _btnTimer.Interval = 12;
            _btnTimer.Tick += OnBtnTick;
            _btnTimer.Start();

            txtGamePath.TextChanged += (_, _) => UpdateStatus();
            TryAutoDetectGamePath();
        }

        // ── UI construction ───────────────────────────────────────────────────
        private void BuildUI()
        {
            int y = HEADER_H;

            // Header panel (custom painted)
            pnlHeader.SetBounds(0, 0, W, HEADER_H);
            pnlHeader.Paint += (_, e) => DrawHeader(e.Graphics);
            Controls.Add(pnlHeader);

            // ── SECTION: Game folder ─────────────────────────────────────────
            y += 16;
            AddSectionLabel("GAME FOLDER", 16, y); y += 18;

            // Path input
            txtGamePath.BackColor       = Color.FromArgb(20, 23, 34);
            txtGamePath.ForeColor       = TextPrimary;
            txtGamePath.BorderStyle     = BorderStyle.FixedSingle;
            txtGamePath.Font            = new Font("Segoe UI", 9.5f);
            txtGamePath.PlaceholderText = @"C:\Steam\steamapps\common\MineMogul";
            txtGamePath.SetBounds(14, y, 468, 26);
            Controls.Add(txtGamePath);

            // Browse button
            var btnBrowse = MakeButton("Browse", 486, y, 72, 26, Surface, Accent, btnBrowse_Click);
            btnBrowse.MouseEnter += (_, _) => { _mouseOnBrowse = true; };
            btnBrowse.MouseLeave += (_, _) => { _mouseOnBrowse = false; };
            btnBrowse.Paint += (_, e) => PaintButton(e.Graphics, btnBrowse, "Browse", Accent, _hoverBrowse);
            Controls.Add(btnBrowse);

            // Auto detect button
            var btnAuto = MakeButton("⟳  Auto", 562, y, 84, 26, Surface, TextMuted, btnDetect_Click);
            btnAuto.MouseEnter += (_, _) => { _mouseOnAuto = true; };
            btnAuto.MouseLeave += (_, _) => { _mouseOnAuto = false; };
            btnAuto.Paint += (_, e) => PaintButton(e.Graphics, btnAuto, "⟳  Auto", TextMuted, _hoverAuto);
            Controls.Add(btnAuto);

            y += 34;

            // Status row (painted on form OnPaint)
            y += 26; // space for status badge drawn in OnPaint

            // ── SECTION: Actions ─────────────────────────────────────────────
            y += 8;
            AddSectionLabel("ACTIONS", 16, y); y += 18;

            // Install button
            var btnInstall = MakeButton("Install", 14, y, 192, 50, Color.FromArgb(24, 88, 45), AccentGreen, btnInstall_Click);
            btnInstall.MouseEnter += (_, _) => { _mouseOnInstall = true; };
            btnInstall.MouseLeave += (_, _) => { _mouseOnInstall = false; };
            btnInstall.Paint += (_, e) => PaintButton(e.Graphics, btnInstall, "↓  Install", AccentGreen, _hoverInstall, 11f, true);
            Controls.Add(btnInstall);

            // Uninstall button
            var btnUninstall = MakeButton("Uninstall", 212, y, 192, 50, Color.FromArgb(80, 20, 20), AccentRed, btnUninstall_Click);
            btnUninstall.MouseEnter += (_, _) => { _mouseOnUninstall = true; };
            btnUninstall.MouseLeave += (_, _) => { _mouseOnUninstall = false; };
            btnUninstall.Paint += (_, e) => PaintButton(e.Graphics, btnUninstall, "×  Uninstall", AccentRed, _hoverUninstall, 11f, true);
            Controls.Add(btnUninstall);

            // Open folder button
            var btnFolder = MakeButton("Open Folder", 410, y, 236, 50, Color.FromArgb(24, 26, 38), TextMuted, btnOpenFolder_Click);
            btnFolder.MouseEnter += (_, _) => { _mouseOnFolder = true; };
            btnFolder.MouseLeave += (_, _) => { _mouseOnFolder = false; };
            btnFolder.Paint += (_, e) => PaintButton(e.Graphics, btnFolder, "📂  Open Folder", TextMuted, _hoverFolder, 9.5f, false);
            Controls.Add(btnFolder);

            y += 58;

            // Remove BepInEx checkbox
            chkRemoveBepInEx.Text      = "Also remove BepInEx folder on uninstall";
            chkRemoveBepInEx.ForeColor = TextMuted;
            chkRemoveBepInEx.BackColor = Color.Transparent;
            chkRemoveBepInEx.Font      = new Font("Segoe UI", 8.5f);
            chkRemoveBepInEx.SetBounds(16, y, 380, 20);
            Controls.Add(chkRemoveBepInEx);

            y += 24;

            // Progress bar slot
            pnlProgress.SetBounds(14, y, W - 28, 6);
            pnlProgress.Paint += (_, e) => DrawProgressBar(e.Graphics);
            Controls.Add(pnlProgress);

            y += 14;

            // ── SECTION: Log ─────────────────────────────────────────────────
            AddSectionLabel("LOG", 16, y); y += 18;

            txtLog.BackColor   = Color.FromArgb(10, 12, 18);
            txtLog.ForeColor   = LogGreen;
            txtLog.BorderStyle = BorderStyle.None;
            txtLog.Font        = TryFont("Cascadia Code", 8.5f) ?? new Font("Consolas", 8.5f);
            txtLog.Multiline   = true;
            txtLog.ReadOnly    = true;
            txtLog.ScrollBars  = ScrollBars.Vertical;
            txtLog.SetBounds(14, y, W - 28, 142);
            Controls.Add(txtLog);

            y += 150;

            // Footer label (painted in OnPaint)
        }

        private Button MakeButton(string text, int x, int yy, int w, int h, Color bg, Color fg, EventHandler click)
        {
            var btn = new Button();
            btn.Text            = "";
            btn.BackColor       = bg;
            btn.FlatStyle       = FlatStyle.Flat;
            btn.FlatAppearance.BorderSize = 0;
            btn.FlatAppearance.MouseOverBackColor = bg;
            btn.SetBounds(x, yy, w, h);
            btn.Click += click;
            return btn;
        }

        private Label AddSectionLabel(string text, int x, int y)
        {
            var lbl = new Label();
            lbl.Text      = text;
            lbl.Font      = new Font("Segoe UI", 7.5f, FontStyle.Bold);
            lbl.ForeColor = Color.FromArgb(55, 65, 95);
            lbl.BackColor = Color.Transparent;
            lbl.AutoSize  = false;
            lbl.SetBounds(x, y, 200, 16);
            Controls.Add(lbl);
            return lbl;
        }

        private static Font? TryFont(string name, float size)
        {
            try { var f = new Font(name, size); if (f.Name == name) return f; f.Dispose(); } catch { }
            return null;
        }

        // ── Animation ─────────────────────────────────────────────────────────
        private void OnAnimTick(object? sender, EventArgs e)
        {
            if (_fadeOpacity < 1f) { _fadeOpacity = Math.Min(1f, _fadeOpacity + 0.06f); Opacity = _fadeOpacity; }
            _glowPhase     = (_glowPhase + 0.022f) % (MathF.PI * 2f);
            _shimmerOffset = (_shimmerOffset + 2f); if (_shimmerOffset > W + 300) _shimmerOffset = -300;
            _statusPulse   = (_statusPulse + 0.04f) % (MathF.PI * 2f);

            if (_isBusy) _progressTarget = Math.Min(0.95f, _progressTarget + 0.004f);
            _progressCurrent += (_progressTarget - _progressCurrent) * 0.08f;

            Invalidate(new Rectangle(0, 0, W, HEADER_H));               // header
            Invalidate(new Rectangle(14, HEADER_H + 16 + 18 + 34, W - 28, 28)); // status
            pnlHeader.Invalidate();
            pnlProgress.Invalidate();
        }

        private void OnBtnTick(object? sender, EventArgs e)
        {
            float spd = 0.1f, spd2 = 0.07f;
            _hoverInstall   = _mouseOnInstall   ? Math.Min(1f, _hoverInstall+spd)   : Math.Max(0f, _hoverInstall-spd2);
            _hoverUninstall = _mouseOnUninstall ? Math.Min(1f, _hoverUninstall+spd) : Math.Max(0f, _hoverUninstall-spd2);
            _hoverBrowse    = _mouseOnBrowse    ? Math.Min(1f, _hoverBrowse+spd)    : Math.Max(0f, _hoverBrowse-spd2);
            _hoverAuto      = _mouseOnAuto      ? Math.Min(1f, _hoverAuto+spd)      : Math.Max(0f, _hoverAuto-spd2);
            _hoverFolder    = _mouseOnFolder    ? Math.Min(1f, _hoverFolder+spd)    : Math.Max(0f, _hoverFolder-spd2);
            foreach (Control c in Controls)
                if (c is Button) c.Invalidate();
        }

        // ── Custom painting ───────────────────────────────────────────────────
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode     = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            DrawStatusBadge(g);
            DrawFooter(g);
        }

        private void DrawHeader(Graphics g)
        {
            g.SmoothingMode     = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            var r = new Rectangle(0, 0, W, HEADER_H);

            using var bg = new LinearGradientBrush(r, Color.FromArgb(14, 17, 28), Color.FromArgb(10, 12, 20), 90f);
            g.FillRectangle(bg, r);

            // Shimmer
            if (_shimmerOffset > -120 && _shimmerOffset < W + 120)
            {
                using var shimBrush = new LinearGradientBrush(
                    new RectangleF(_shimmerOffset - 100, 0, 200, HEADER_H), Color.Transparent, Color.Transparent, 0f)
                {
                    InterpolationColors = new ColorBlend
                    {
                        Positions = new[] { 0f, 0.5f, 1f },
                        Colors = new[] { Color.Transparent, Color.FromArgb(12, 255, 255, 255), Color.Transparent }
                    }
                };
                g.FillRectangle(shimBrush, _shimmerOffset - 100, 0, 200, HEADER_H);
            }

            // Bottom line
            using var lp = new Pen(Color.FromArgb(40, Accent), 1f);
            g.DrawLine(lp, 0, HEADER_H - 1, W, HEADER_H - 1);

            // Logo text
            using var logoFont = new Font("Segoe UI", 18f, FontStyle.Bold);
            using var logoBrush = new SolidBrush(TextPrimary);
            g.DrawString("MML", logoFont, logoBrush, 18, 16);

            using var logoAccentFont = new Font("Segoe UI", 10f);
            using var logoDimBrush   = new SolidBrush(TextMuted);
            g.DrawString("Mine Mogul Logic", logoAccentFont, logoDimBrush, 70, 23);

            // Right: "Installer" badge
            DrawBadge(g, "Installer", W - 92, 22, Color.FromArgb(25, 70, 115), Accent);

            // Tiny version
            using var verFont  = new Font("Segoe UI", 7.5f);
            using var verBrush = new SolidBrush(Color.FromArgb(45, 55, 75));
            g.DrawString("v1.0.0", verFont, verBrush, 18, HEADER_H - 18);
        }

        private void DrawStatusBadge(Graphics g)
        {
            float y = HEADER_H + 16 + 18 + 30;
            float pulse = 0.6f + 0.35f * MathF.Sin(_statusPulse);

            (Color col, string text) = _status switch
            {
                Status.Installed     => (AccentGreen, "✓  MML is installed"),
                Status.BepInExOnly   => (AccentWarn,  "⚡  BepInEx found — MML not yet installed"),
                Status.NotInstalled  => (AccentRed,   "✕  Not installed"),
                _                    => (TextMuted,   "—  Select a game folder above"),
            };

            // Pulsing circle
            int dotAlpha = (int)(pulse * (col == TextMuted ? 90 : 220));
            using var dotBrush = new SolidBrush(Color.FromArgb(dotAlpha, col));
            g.FillEllipse(dotBrush, new RectangleF(16, y + 4, 10, 10));
            using var dotGlow = new SolidBrush(Color.FromArgb((int)(pulse * 60), col));
            g.FillEllipse(dotGlow, new RectangleF(12, y, 18, 18));

            using var statusFont  = new Font("Segoe UI", 9f, FontStyle.Bold);
            using var statusBrush = new SolidBrush(col);
            g.DrawString(text, statusFont, statusBrush, 32, y + 2);
        }

        private void DrawProgressBar(Graphics g)
        {
            var r = new Rectangle(0, 0, pnlProgress.Width, pnlProgress.Height);
            using var bgBrush = new SolidBrush(Color.FromArgb(22, 26, 38));
            using var path = RoundRectPath(r, 3);
            g.FillPath(bgBrush, path);

            if (_progressCurrent > 0.005f)
            {
                int fillW = (int)(r.Width * _progressCurrent);
                if (fillW > 6)
                {
                    var fillR = new Rectangle(r.X, r.Y, fillW, r.Height);
                    using var fillPath = RoundRectPath(fillR, 3);
                    using var fillBrush = new LinearGradientBrush(fillR, AccentGreen, Accent, 0f);
                    g.FillPath(fillBrush, fillPath);
                }
            }
        }

        private void DrawFooter(Graphics g)
        {
            float y = ClientSize.Height - 22f;
            using var f = new Font("Segoe UI", 7.5f);
            using var b = new SolidBrush(Color.FromArgb(38, 44, 60));
            string txt = "MML v1.0.0  •  BepInEx mod for MineMogul  •  Not affiliated with NoodleForge";
            var ts = g.MeasureString(txt, f);
            g.DrawString(txt, f, b, (W - ts.Width) / 2f, y);
        }

        private static void PaintButton(Graphics g, Button btn, string text, Color fg, float hover, float fontSize = 9.5f, bool bold = false)
        {
            g.SmoothingMode     = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            var r = new Rectangle(0, 0, btn.Width, btn.Height);
            Color baseBG = btn.BackColor;
            Color hoverBG = BlendColor(baseBG, Color.FromArgb(
                Math.Min(baseBG.R + 20, 255), Math.Min(baseBG.G + 20, 255), Math.Min(baseBG.B + 20, 255)), hover);

            using var bgBrush = new SolidBrush(hoverBG);
            using var path = RoundRectPath(r, 8);
            g.FillPath(bgBrush, path);

            // Border
            int borderA = (int)(60 + 100 * hover);
            using var borderPen = new Pen(Color.FromArgb(borderA, fg), 1.5f);
            using var borderPath = RoundRectPath(new Rectangle(0, 0, btn.Width - 1, btn.Height - 1), 8);
            g.DrawPath(borderPen, borderPath);

            // Glow on hover
            if (hover > 0.1f)
            {
                for (int s = 3; s >= 1; s--)
                {
                    int a = (int)(hover * 30 / s);
                    var er = new Rectangle(-s*2, -s*2, btn.Width + s*4, btn.Height + s*4);
                    using var gpen = new Pen(Color.FromArgb(a, fg), 1f);
                    using var gpath = RoundRectPath(er, 10 + s);
                    g.DrawPath(gpen, gpath);
                }
            }

            // Text
            using var font  = new Font("Segoe UI", fontSize, bold ? FontStyle.Bold : FontStyle.Regular);
            var ts = g.MeasureString(text, font);
            using var tb = new SolidBrush(Color.FromArgb((int)(180 + 75 * hover), fg == TextMuted ? TextPrimary : Color.White));
            g.DrawString(text, font, tb, (btn.Width - ts.Width) / 2f, (btn.Height - ts.Height) / 2f);
        }

        // ── Event handlers ────────────────────────────────────────────────────
        private void btnBrowse_Click(object? sender, EventArgs e)
        {
            using var dlg = new FolderBrowserDialog { Description = "Select the MineMogul folder (contains MineMogul.exe)", UseDescriptionForTitle = true };
            if (dlg.ShowDialog() == DialogResult.OK) txtGamePath.Text = dlg.SelectedPath;
        }

        private void btnDetect_Click(object? sender, EventArgs e) => TryAutoDetectGamePath();

        public void TryAutoDetectGamePath()
        {
            try
            {
                string[] keys = {
                    @"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Valve\Steam",
                    @"HKEY_LOCAL_MACHINE\SOFTWARE\Valve\Steam",
                    @"HKEY_CURRENT_USER\SOFTWARE\Valve\Steam"
                };
                foreach (var key in keys)
                {
                    string? steamPath = Microsoft.Win32.Registry.GetValue(key, "InstallPath", null) as string;
                    if (string.IsNullOrEmpty(steamPath)) continue;
                    string dir = Path.Combine(steamPath, "steamapps", "common", "MineMogul");
                    if (Directory.Exists(dir) && File.Exists(Path.Combine(dir, "MineMogul.exe")))
                    {
                        txtGamePath.Text = dir;
                        Log("Found MineMogul at: " + dir);
                        UpdateStatus();
                        return;
                    }
                }
                Log("Auto-detect: MineMogul not found. Please select the folder manually.");
            }
            catch (Exception ex) { Log("Auto-detect error: " + ex.Message); }
        }

        private void UpdateStatus()
        {
            string p = txtGamePath.Text.Trim();
            if (!Directory.Exists(p)) { _status = Status.None; Invalidate(); return; }
            bool mod = File.Exists(Path.Combine(p, "BepInEx", "plugins", "MML.dll"));
            bool bep = File.Exists(Path.Combine(p, "winhttp.dll"));
            _status = mod ? Status.Installed : bep ? Status.BepInExOnly : Status.NotInstalled;
            Invalidate();
        }

        private void btnInstall_Click(object? sender, EventArgs e)
        {
            string gamePath = txtGamePath.Text.Trim();
            if (!ValidateGamePath(gamePath)) return;
            try
            {
                SetBusy(true);
                _progressTarget = 0f; _progressCurrent = 0f;
                Log(""); Log("─── Installation started ──────────────────");
                Log("Extracting BepInEx...");
                _progressTarget = 0.6f;
                ExtractBepInExZip(gamePath);

                _progressTarget = 0.85f;
                string pluginsDir = Path.Combine(gamePath, "BepInEx", "plugins");
                Log("Copying MML.dll...");
                WriteEmbeddedMmlDll(pluginsDir);

                _progressTarget = 1f;
                Log(""); Log("✓ Installation complete!  Launch MineMogul to activate.");
                Log("  In-game: F5 = Factory HUD  |  F6 = Belt counters");
                UpdateStatus();
                MessageBox.Show("Installation successful!\n\nLaunch MineMogul and press F5 to open the mod overlay.",
                    "MML Installer", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                Log("ERROR: " + ex.Message);
                MessageBox.Show("Installation failed:\n" + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally { SetBusy(false); }
        }

        private void btnUninstall_Click(object? sender, EventArgs e)
        {
            string gamePath = txtGamePath.Text.Trim();
            if (!ValidateGamePath(gamePath)) return;
            if (MessageBox.Show("Remove MML?\n\nYour save data will NOT be affected.",
                "Confirm Uninstall", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            try
            {
                SetBusy(true);
                _progressTarget = 0f; _progressCurrent = 0f;
                Log(""); Log("─── Uninstall started ─────────────────────");
                foreach (var dll in new[] {
                    Path.Combine(gamePath, "BepInEx", "plugins", "MML.dll"),
                    Path.Combine(gamePath, "BepInEx", "plugins", "MineMogulMod.dll") })
                    if (File.Exists(dll)) { File.Delete(dll); Log("Removed: " + dll); }
                string cfg = Path.Combine(gamePath, "BepInEx", "config", "com.minemogul.mml.cfg");
                if (File.Exists(cfg)) { File.Delete(cfg); Log("Removed config: " + cfg); }
                foreach (var f in new[]{ "winhttp.dll","doorstop_config.ini",".doorstop_version","changelog.txt" })
                { string fp = Path.Combine(gamePath, f); if (File.Exists(fp)) { File.Delete(fp); Log("Removed: " + f); } }
                string bepDir = Path.Combine(gamePath, "BepInEx");
                if (Directory.Exists(bepDir))
                {
                    if (chkRemoveBepInEx.Checked) { Directory.Delete(bepDir, true); Log("BepInEx folder removed."); }
                    else Log("BepInEx folder kept.");
                }
                _progressTarget = 1f;
                Log(""); Log("✓ Uninstall complete.");
                UpdateStatus();
                MessageBox.Show("MML has been removed.", "MML Installer", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                Log("ERROR: " + ex.Message);
                MessageBox.Show("Uninstall failed:\n" + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally { SetBusy(false); }
        }

        private void btnOpenFolder_Click(object? sender, EventArgs e)
        {
            string path = txtGamePath.Text.Trim();
            if (Directory.Exists(path)) Process.Start("explorer.exe", path);
        }

        // ── Helpers ───────────────────────────────────────────────────────────
        private bool ValidateGamePath(string path)
        {
            if (!Directory.Exists(path)) { MessageBox.Show("Please select a valid folder.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning); return false; }
            if (!File.Exists(Path.Combine(path, "MineMogul.exe"))) { MessageBox.Show("MineMogul.exe not found in this folder.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning); return false; }
            return true;
        }

        // ── Embedded resource helpers ──────────────────────────────────────────
        private static Stream GetEmbeddedStream(string name)
        {
            var asm = Assembly.GetExecutingAssembly();
            string fullName = asm.GetManifestResourceNames()
                .First(n => n.EndsWith(name, StringComparison.OrdinalIgnoreCase));
            return asm.GetManifestResourceStream(fullName)!;
        }

        private void ExtractBepInExZip(string gamePath)
        {
            using var stream = GetEmbeddedStream("BepInEx.zip");
            using var zip = new ZipArchive(stream, ZipArchiveMode.Read);
            foreach (var entry in zip.Entries)
            {
                if (string.IsNullOrEmpty(entry.Name)) continue; // directory entry
                string destPath = Path.Combine(gamePath, entry.FullName);
                Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
                entry.ExtractToFile(destPath, overwrite: true);
                Log("  " + entry.FullName);
            }
        }

        private void WriteEmbeddedMmlDll(string pluginsDir)
        {
            Directory.CreateDirectory(pluginsDir);
            string dest = Path.Combine(pluginsDir, "MML.dll");
            using var stream = GetEmbeddedStream("MML.dll");
            using var fs = File.Create(dest);
            stream.CopyTo(fs);
            Log("Mod copied \u2192 " + dest);
        }

        private void Log(string msg)
        {
            if (txtLog.InvokeRequired) txtLog.Invoke(() => Log(msg));
            else
            { txtLog.AppendText(msg + Environment.NewLine); txtLog.ScrollToCaret(); }
        }

        private void SetBusy(bool busy)
        {
            _isBusy = busy;
            foreach (Control c in Controls)
                if (c is Button b) { b.Enabled = !busy; b.Invalidate(); }
        }

        // ── Drawing utilities (shared with LandingForm style) ─────────────────
        private static void DrawBadge(Graphics g, string text, float x, float y, Color bg, Color fg)
        {
            using var f = new Font("Segoe UI", 8f, FontStyle.Bold);
            var ts = g.MeasureString(text, f);
            var r = new RectangleF(x, y, ts.Width + 16, ts.Height + 4);
            using var bb = new SolidBrush(Color.FromArgb(160, bg)); FillRoundRect(g, bb, r, 5);
            using var bp = new Pen(Color.FromArgb(180, fg), 1f); DrawRoundRect(g, bp, r, 5);
            using var tb = new SolidBrush(fg); g.DrawString(text, f, tb, x + 8, y + 2);
        }

        private static Color BlendColor(Color a, Color b, float t)
        {
            t = Math.Clamp(t, 0f, 1f);
            return Color.FromArgb(Math.Clamp((int)(a.R + (b.R - a.R) * t), 0, 255), Math.Clamp((int)(a.G + (b.G - a.G) * t), 0, 255), Math.Clamp((int)(a.B + (b.B - a.B) * t), 0, 255));
        }

        private static void FillRoundRect(Graphics g, Brush b, RectangleF r, int rad) { using var p = RoundRectPath(r, rad); g.FillPath(b, p); }
        private static void DrawRoundRect(Graphics g, Pen p, RectangleF r, int rad)   { using var path = RoundRectPath(r, rad); g.DrawPath(p, path); }
        private static void FillRoundRect(Graphics g, Brush b, Rectangle r, int rad) { using var p = RoundRectPath(r, rad); g.FillPath(b, p); }
        private static GraphicsPath RoundRectPath(Rectangle r, int rad) => RoundRectPath(new RectangleF(r.X, r.Y, r.Width, r.Height), rad);
        private static GraphicsPath RoundRectPath(RectangleF r, int rad)
        {
            var path = new GraphicsPath(); float d = rad * 2f;
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure(); return path;
        }
    }
}