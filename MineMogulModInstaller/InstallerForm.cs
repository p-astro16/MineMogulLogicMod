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
        // ── Win32 ──────────────────────────────────────────────────────────────
        [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr h, int a, ref int v, int s);
        private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
        private const int DWMWCP_ROUND = 2;

        // ── Colors ─────────────────────────────────────────────────────────────
        static readonly Color BG          = Color.FromArgb(10, 11, 16);
        static readonly Color Surface     = Color.FromArgb(18, 21, 30);
        static readonly Color Accent      = Color.FromArgb(51, 181, 229);
        static readonly Color AccentGreen = Color.FromArgb(41, 210, 110);
        static readonly Color AccentRed   = Color.FromArgb(220, 60, 60);
        static readonly Color AccentWarn  = Color.FromArgb(230, 160, 30);
        static readonly Color TextPrimary = Color.FromArgb(220, 225, 240);
        static readonly Color TextMuted   = Color.FromArgb(90, 100, 130);
        static readonly Color LogGreen    = Color.FromArgb(100, 220, 120);

        // ── Layout ─────────────────────────────────────────────────────────────
        private const int W        = 640;
        private const int HEADER_H = 64;
        private const int PAD      = 14;
        private const int FORM_H   = 500;

        // ── Controls ───────────────────────────────────────────────────────────
        private readonly Panel      pnlHeader   = new();
        private readonly TextBox    txtGamePath = new();
        private readonly Panel      pnlStatus   = new();
        private readonly Button     btnBrowse   = new();
        private readonly Button     btnAuto     = new();
        private readonly Button     btnInstall  = new();
        private readonly Button     btnUninstall= new();
        private readonly Button     btnFolder   = new();
        private readonly CheckBox   chkRemoveBep= new();
        private readonly Panel      pnlProgress = new();
        private readonly RichTextBox txtLog     = new();

        // ── Status ─────────────────────────────────────────────────────────────
        private enum InstallStatus { None, NotInstalled, BepInExOnly, Installed }
        private InstallStatus _status = InstallStatus.None;

        // ── Animation ──────────────────────────────────────────────────────────
        private float _fadeOpacity    = 0f;
        private float _shimmerOffset  = -200f;
        private float _progressTarget = 0f;
        private float _progressVal    = 0f;
        private bool  _isBusy         = false;
        private readonly System.Windows.Forms.Timer _animTimer = new() { Interval = 16 };

        // ─────────────────────────────────────────────────────────────────────
        public InstallerForm()
        {
            SuspendLayout();
            AutoScaleMode   = AutoScaleMode.None;
            ClientSize      = new Size(W, FORM_H);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox     = false;
            StartPosition   = FormStartPosition.CenterScreen;
            Text            = "MML — Installer v1.0.0";
            BackColor       = BG;
            DoubleBuffered  = true;
            Opacity         = 0;

            BuildUI();
            ResumeLayout(false);

            try { int p = DWMWCP_ROUND; DwmSetWindowAttribute(Handle, DWMWA_WINDOW_CORNER_PREFERENCE, ref p, sizeof(int)); } catch { }
            Icon = LandingForm.LoadAppIcon() ?? SystemIcons.Application;

            _animTimer.Tick += OnAnimTick;
            _animTimer.Start();

            txtGamePath.TextChanged += (_, _) => UpdateStatus();
            TryAutoDetectGamePath();
        }

        // ── Build UI ───────────────────────────────────────────────────────────
        private void BuildUI()
        {
            int y = 0;

            // ── Header ─────────────────────────────────────────────────────────
            pnlHeader.Bounds = new Rectangle(0, 0, W, HEADER_H);
            pnlHeader.Paint  += PaintHeader;
            Controls.Add(pnlHeader);
            y = HEADER_H + 16;

            // ── Game Folder ─────────────────────────────────────────────────────
            AddSectionLabel("GAME FOLDER", PAD, y);
            y += 20;

            // Path textbox: fills to leave room for 2 buttons (76+80+8+8 = 172)
            txtGamePath.Bounds          = new Rectangle(PAD, y, W - PAD * 2 - 172, 26);
            txtGamePath.BackColor       = Color.FromArgb(20, 23, 34);
            txtGamePath.ForeColor       = TextPrimary;
            txtGamePath.BorderStyle     = BorderStyle.FixedSingle;
            txtGamePath.Font            = new Font("Segoe UI", 9.5f);
            txtGamePath.PlaceholderText = @"C:\Steam\steamapps\common\MineMogul";
            Controls.Add(txtGamePath);

            int bx = txtGamePath.Right + 8;
            StyleSmallButton(btnBrowse, "Browse", bx,      y, 76, 26, Accent,    btnBrowse_Click);
            StyleSmallButton(btnAuto,   "⟳ Auto",  bx + 84, y, 72, 26, TextMuted, btnAuto_Click);
            Controls.Add(btnBrowse);
            Controls.Add(btnAuto);
            y += 34;

            // ── Status ──────────────────────────────────────────────────────────
            pnlStatus.Bounds    = new Rectangle(PAD, y, W - PAD * 2, 24);
            pnlStatus.BackColor = Color.Transparent;
            pnlStatus.Paint     += PaintStatus;
            Controls.Add(pnlStatus);
            y += 32;

            // ── Actions ─────────────────────────────────────────────────────────
            AddSectionLabel("ACTIONS", PAD, y);
            y += 20;

            int actionW = (W - PAD * 2 - 8) / 3;
            StyleActionButton(btnInstall,    "↓  Install",      PAD,                    y, actionW, 44, Color.FromArgb(22, 80, 42), AccentGreen, btnInstall_Click);
            StyleActionButton(btnUninstall,  "×  Uninstall",    PAD + actionW + 4,      y, actionW, 44, Color.FromArgb(70, 18, 18), AccentRed,   btnUninstall_Click);
            StyleActionButton(btnFolder,     "📂  Open Folder", PAD + (actionW + 4) * 2, y, actionW, 44, Surface,                   TextMuted,   btnOpenFolder_Click);
            Controls.Add(btnInstall);
            Controls.Add(btnUninstall);
            Controls.Add(btnFolder);
            y += 52;

            // ── Checkbox ────────────────────────────────────────────────────────
            chkRemoveBep.Text      = "Also remove BepInEx folder on uninstall";
            chkRemoveBep.ForeColor = TextMuted;
            chkRemoveBep.BackColor = Color.Transparent;
            chkRemoveBep.Font      = new Font("Segoe UI", 8.5f);
            chkRemoveBep.Bounds    = new Rectangle(PAD, y, 340, 20);
            Controls.Add(chkRemoveBep);
            y += 28;

            // ── Progress bar ────────────────────────────────────────────────────
            pnlProgress.Bounds    = new Rectangle(PAD, y, W - PAD * 2, 5);
            pnlProgress.BackColor = Color.FromArgb(22, 26, 38);
            pnlProgress.Paint     += PaintProgress;
            Controls.Add(pnlProgress);
            y += 14;

            // ── Log ─────────────────────────────────────────────────────────────
            AddSectionLabel("LOG", PAD, y);
            y += 20;

            int logBottom = FORM_H - 28;
            txtLog.Bounds      = new Rectangle(PAD, y, W - PAD * 2, logBottom - y);
            txtLog.BackColor   = Color.FromArgb(10, 12, 18);
            txtLog.ForeColor   = LogGreen;
            txtLog.BorderStyle = BorderStyle.None;
            txtLog.Font        = TryFont("Cascadia Code", 8.5f) ?? new Font("Consolas", 8.5f);
            txtLog.ReadOnly    = true;
            txtLog.ScrollBars  = RichTextBoxScrollBars.Vertical;
            txtLog.WordWrap    = false;
            Controls.Add(txtLog);

            // ── Footer label ────────────────────────────────────────────────────
            var footer = new Label
            {
                Text      = "MML v1.0.0  •  BepInEx mod for MineMogul  •  Not affiliated with NoodleForge",
                ForeColor = Color.FromArgb(38, 44, 60),
                BackColor = Color.Transparent,
                Font      = new Font("Segoe UI", 7.5f),
                AutoSize  = false,
                TextAlign = ContentAlignment.MiddleCenter,
                Bounds    = new Rectangle(0, FORM_H - 24, W, 22)
            };
            Controls.Add(footer);
        }

        // ── Helpers ────────────────────────────────────────────────────────────
        private void StyleSmallButton(Button b, string text, int x, int y, int w, int h, Color fg, EventHandler click)
        {
            b.Text            = text;
            b.ForeColor       = fg;
            b.BackColor       = Surface;
            b.FlatStyle       = FlatStyle.Flat;
            b.FlatAppearance.BorderColor             = Color.FromArgb(55, fg);
            b.FlatAppearance.BorderSize              = 1;
            b.FlatAppearance.MouseOverBackColor      = Color.FromArgb(28, 32, 48);
            b.Font            = new Font("Segoe UI", 8.5f);
            b.Bounds          = new Rectangle(x, y, w, h);
            b.Click          += click;
            b.Cursor          = Cursors.Hand;
        }

        private void StyleActionButton(Button b, string text, int x, int y, int w, int h, Color bg, Color fg, EventHandler click)
        {
            b.Text            = text;
            b.ForeColor       = Color.FromArgb(230, fg);
            b.BackColor       = bg;
            b.FlatStyle       = FlatStyle.Flat;
            b.FlatAppearance.BorderColor        = Color.FromArgb(90, fg);
            b.FlatAppearance.BorderSize         = 1;
            b.FlatAppearance.MouseOverBackColor = Color.FromArgb(
                Math.Clamp(bg.R + 18, 0, 255),
                Math.Clamp(bg.G + 18, 0, 255),
                Math.Clamp(bg.B + 18, 0, 255));
            b.Font   = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            b.Bounds = new Rectangle(x, y, w, h);
            b.Click += click;
            b.Cursor = Cursors.Hand;
        }

        private Label AddSectionLabel(string text, int x, int y)
        {
            var lbl = new Label
            {
                Text      = text,
                Font      = new Font("Segoe UI", 7.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(55, 65, 95),
                BackColor = Color.Transparent,
                AutoSize  = false,
                Bounds    = new Rectangle(x, y, 200, 16)
            };
            Controls.Add(lbl);
            return lbl;
        }

        private static Font? TryFont(string name, float size)
        {
            try { var f = new Font(name, size); if (f.Name == name) return f; f.Dispose(); } catch { }
            return null;
        }

        // ── Animation ──────────────────────────────────────────────────────────
        private void OnAnimTick(object? sender, EventArgs e)
        {
            if (_fadeOpacity < 1f) { _fadeOpacity = Math.Min(1f, _fadeOpacity + 0.07f); Opacity = _fadeOpacity; }
            _shimmerOffset = _shimmerOffset < W + 200 ? _shimmerOffset + 2.5f : -200f;

            if (_isBusy) _progressTarget = Math.Min(0.93f, _progressTarget + 0.003f);
            _progressVal += (_progressTarget - _progressVal) * 0.08f;

            pnlHeader.Invalidate();
            pnlProgress.Invalidate();
        }

        // ── Painting ───────────────────────────────────────────────────────────
        private void PaintHeader(object? sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode     = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            var r = new Rectangle(0, 0, W, HEADER_H);
            using var bg = new LinearGradientBrush(r, Color.FromArgb(14, 17, 28), Color.FromArgb(10, 12, 20), 90f);
            g.FillRectangle(bg, r);

            // Shimmer sweep
            float so = _shimmerOffset;
            if (so > -120 && so < W + 120)
            {
                using var sh = new LinearGradientBrush(
                    new RectangleF(so - 80, 0, 160, HEADER_H), Color.Transparent, Color.Transparent, 0f)
                {
                    InterpolationColors = new ColorBlend
                    {
                        Positions = new[] { 0f, 0.5f, 1f },
                        Colors    = new[] { Color.Transparent, Color.FromArgb(10, 255, 255, 255), Color.Transparent }
                    }
                };
                g.FillRectangle(sh, so - 80, 0, 160, HEADER_H);
            }

            // Bottom separator
            using var lp = new Pen(Color.FromArgb(35, Accent), 1f);
            g.DrawLine(lp, 0, HEADER_H - 1, W, HEADER_H - 1);

            // Logo "MML"
            using var logoFont  = new Font("Segoe UI", 18f, FontStyle.Bold);
            using var logoBrush = new SolidBrush(TextPrimary);
            g.DrawString("MML", logoFont, logoBrush, 16, 10);

            // Sub-title
            using var subFont  = new Font("Segoe UI", 9.5f);
            using var subBrush = new SolidBrush(TextMuted);
            g.DrawString("Mine Mogul Logic", subFont, subBrush, 70, 20);

            // Right "Installer" badge
            using var bf    = new Font("Segoe UI", 8f, FontStyle.Bold);
            using var fb    = new SolidBrush(Accent);
            string badge    = "Installer";
            var ts          = g.MeasureString(badge, bf);
            float bx        = W - ts.Width - 30, by2 = 20;
            using var bb2   = new SolidBrush(Color.FromArgb(40, 25, 70, 115));
            g.FillRectangle(bb2, bx - 4, by2 - 2, ts.Width + 16, ts.Height + 6);
            using var bp2   = new Pen(Color.FromArgb(70, Accent));
            g.DrawRectangle(bp2, bx - 4, by2 - 2, ts.Width + 16, ts.Height + 6);
            g.DrawString(badge, bf, fb, bx + 4, by2);

            // Version
            using var vf = new Font("Segoe UI", 7.5f);
            using var vb = new SolidBrush(Color.FromArgb(45, 55, 75));
            g.DrawString("v1.0.0", vf, vb, 16, HEADER_H - 16);
        }

        private void PaintStatus(object? sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode     = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            (Color col, string text) = _status switch
            {
                InstallStatus.Installed    => (AccentGreen, "✓  MML is installed"),
                InstallStatus.BepInExOnly  => (AccentWarn,  "⚡  BepInEx found — MML not yet installed"),
                InstallStatus.NotInstalled => (AccentRed,   "✕  Not installed"),
                _                         => (TextMuted,    "—  Select a game folder above"),
            };

            using var dotBrush  = new SolidBrush(col);
            g.FillEllipse(dotBrush, 2, 7, 10, 10);

            using var textFont  = new Font("Segoe UI", 9f, FontStyle.Bold);
            using var textBrush = new SolidBrush(col);
            g.DrawString(text, textFont, textBrush, 18, 4);
        }

        private void PaintProgress(object? sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            int fw = Math.Max(0, (int)(pnlProgress.Width * _progressVal));
            if (fw < 6) return;
            using var fill = new LinearGradientBrush(
                new Rectangle(0, 0, fw, pnlProgress.Height), AccentGreen, Accent, 0f);
            g.FillRectangle(fill, 0, 0, fw, pnlProgress.Height);
        }

        // ── Event handlers ─────────────────────────────────────────────────────
        private void btnBrowse_Click(object? sender, EventArgs e)
        {
            using var dlg = new FolderBrowserDialog
            {
                Description            = "Select the MineMogul folder (contains MineMogul.exe)",
                UseDescriptionForTitle = true
            };
            if (dlg.ShowDialog() == DialogResult.OK) txtGamePath.Text = dlg.SelectedPath;
        }

        private void btnAuto_Click(object? sender, EventArgs e) => TryAutoDetectGamePath();

        public void TryAutoDetectGamePath()
        {
            try
            {
                string[] keys =
                {
                    @"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Valve\Steam",
                    @"HKEY_LOCAL_MACHINE\SOFTWARE\Valve\Steam",
                    @"HKEY_CURRENT_USER\SOFTWARE\Valve\Steam"
                };
                foreach (var key in keys)
                {
                    string? steamPath = Registry.GetValue(key, "InstallPath", null) as string;
                    if (string.IsNullOrEmpty(steamPath)) continue;
                    string dir = Path.Combine(steamPath, "steamapps", "common", "MineMogul");
                    if (Directory.Exists(dir) && File.Exists(Path.Combine(dir, "MineMogul.exe")))
                    {
                        txtGamePath.Text = dir;
                        Log("Found MineMogul at: " + dir);
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
            if (!Directory.Exists(p)) { _status = InstallStatus.None; pnlStatus.Invalidate(); return; }
            bool mod = File.Exists(Path.Combine(p, "BepInEx", "plugins", "MML.dll"));
            bool bep = File.Exists(Path.Combine(p, "winhttp.dll"));
            _status = mod ? InstallStatus.Installed : bep ? InstallStatus.BepInExOnly : InstallStatus.NotInstalled;
            pnlStatus.Invalidate();
        }

        private void btnInstall_Click(object? sender, EventArgs e)
        {
            string gamePath = txtGamePath.Text.Trim();
            if (!ValidateGamePath(gamePath)) return;
            try
            {
                SetBusy(true);
                _progressTarget = 0f; _progressVal = 0f;
                Log("");
                Log("─── Installation started ──────────────────────");
                Log("Extracting BepInEx...");
                _progressTarget = 0.55f;
                ExtractBepInExZip(gamePath);
                _progressTarget = 0.85f;
                string pluginsDir = Path.Combine(gamePath, "BepInEx", "plugins");
                Log("Copying MML.dll...");
                WriteEmbeddedMmlDll(pluginsDir);
                _progressTarget = 1f;
                Log("");
                Log("✓ Installation complete!");
                Log("  In-game: F5 = Factory HUD");
                UpdateStatus();
                MessageBox.Show(
                    "Installation successful!\nLaunch MineMogul and press F5 to open the mod overlay.",
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
            if (MessageBox.Show(
                "Remove MML?\n\nYour save data will NOT be affected.",
                "Confirm Uninstall", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            try
            {
                SetBusy(true);
                _progressTarget = 0f; _progressVal = 0f;
                Log("");
                Log("─── Uninstall started ─────────────────────────");
                foreach (var dll in new[]
                {
                    Path.Combine(gamePath, "BepInEx", "plugins", "MML.dll"),
                    Path.Combine(gamePath, "BepInEx", "plugins", "MineMogulMod.dll")
                })
                    if (File.Exists(dll)) { File.Delete(dll); Log("Removed: " + dll); }

                string cfg = Path.Combine(gamePath, "BepInEx", "config", "com.minemogul.mml.cfg");
                if (File.Exists(cfg)) { File.Delete(cfg); Log("Removed config."); }

                foreach (var f in new[] { "winhttp.dll", "doorstop_config.ini", ".doorstop_version", "changelog.txt" })
                {
                    string fp = Path.Combine(gamePath, f);
                    if (File.Exists(fp)) { File.Delete(fp); Log("Removed: " + f); }
                }

                if (chkRemoveBep.Checked)
                {
                    string bepDir = Path.Combine(gamePath, "BepInEx");
                    if (Directory.Exists(bepDir)) { Directory.Delete(bepDir, true); Log("BepInEx folder removed."); }
                }

                _progressTarget = 1f;
                Log("");
                Log("✓ Uninstall complete.");
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

        // ── Validation & embedded resources ────────────────────────────────────
        private bool ValidateGamePath(string path)
        {
            if (!Directory.Exists(path))
            { MessageBox.Show("Please select a valid folder.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning); return false; }
            if (!File.Exists(Path.Combine(path, "MineMogul.exe")))
            { MessageBox.Show("MineMogul.exe not found in this folder.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning); return false; }
            return true;
        }

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
            using var zip    = new ZipArchive(stream, ZipArchiveMode.Read);
            foreach (var entry in zip.Entries)
            {
                if (string.IsNullOrEmpty(entry.Name)) continue;
                string dest = Path.Combine(gamePath, entry.FullName);
                Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                entry.ExtractToFile(dest, overwrite: true);
                Log("  " + entry.FullName);
            }
        }

        private void WriteEmbeddedMmlDll(string pluginsDir)
        {
            Directory.CreateDirectory(pluginsDir);
            string dest = Path.Combine(pluginsDir, "MML.dll");
            using var stream = GetEmbeddedStream("MML.dll");
            using var fs     = File.Create(dest);
            stream.CopyTo(fs);
            Log("Mod copied → " + dest);
        }

        private void Log(string msg)
        {
            if (txtLog.InvokeRequired) { txtLog.Invoke(() => Log(msg)); return; }
            txtLog.AppendText(msg + Environment.NewLine);
            txtLog.ScrollToCaret();
        }

        private void SetBusy(bool busy)
        {
            _isBusy          = busy;
            btnInstall.Enabled    = !busy;
            btnUninstall.Enabled  = !busy;
            btnBrowse.Enabled     = !busy;
            btnAuto.Enabled       = !busy;
        }
    }
}
