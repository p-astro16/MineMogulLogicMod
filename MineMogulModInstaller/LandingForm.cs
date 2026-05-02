using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace MineMogulModInstaller
{
    public partial class LandingForm : Form
    {
        [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr h, int a, ref int v, int s);
        private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
        private const int DWMWCP_ROUND = 2;

        // ── Colors ─────────────────────────────────────────────────────────────
        static readonly Color BG          = Color.FromArgb(10, 11, 16);
        static readonly Color Accent      = Color.FromArgb(51, 181, 229);
        static readonly Color AccentGreen = Color.FromArgb(41, 210, 110);
        static readonly Color AccentWarn  = Color.FromArgb(230, 160, 30);
        static readonly Color CardBG      = Color.FromArgb(17, 19, 28);
        static readonly Color CardBorder  = Color.FromArgb(32, 36, 52);
        static readonly Color TextPrimary = Color.FromArgb(230, 233, 245);
        static readonly Color TextMuted   = Color.FromArgb(100, 110, 140);

        // ── Layout ─────────────────────────────────────────────────────────────
        private const int W        = 640;
        private const int HEADER_H = 200;
        private const int FORM_H   = 610;

        // Feature cards: 2 columns, 3 rows
        private const int CARD_W   = (W - 36) / 2;   // 302
        private const int CARD_H   = 66;
        private const int CARD_GAP = 8;
        private const int CARDS_Y  = HEADER_H + 16;   // 216
        // 3 rows: 3*(66+8)-8 = 214px, end = 216+214 = 430

        // Install button
        private const int BTN_Y    = CARDS_Y + 3 * (CARD_H + CARD_GAP) - CARD_GAP + 16; // 430+16 = 446
        private const int BTN_H    = 48;
        // Footer at BTN_Y + BTN_H + 16 = 510 → form height 536, use 560 to be safe

        // ── Particles ─────────────────────────────────────────────────────────
        private struct Particle { public float X, Y, Speed, Size, Alpha; }
        private readonly List<Particle> _particles = new();
        private readonly Random         _rng       = new();

        // ── Animation state ───────────────────────────────────────────────────
        private float _fadeOpacity   = 0f;
        private float _glowPhase     = 0f;
        private float _shimmerOffset = -400f;
        private int   _titleReveal   = 0;
        private bool  _titleDone     = false;
        private float _btnHover      = 0f;
        private bool  _btnHovered    = false;

        private readonly System.Windows.Forms.Timer _animTimer = new() { Interval = 16 };

        // ── Feature definitions ───────────────────────────────────────────────
        private static readonly (string Icon, string Title, string Desc, string Tag, Color TagCol)[] Features =
        {
            ("📊", "Factory HUD",         "Live overlay: Machines, Belts, Sales & Settings tabs. Toggle with F5.",        "F5",  Color.FromArgb(51,181,229)),
            ("🔧", "Splitter Wrench",      "Buyable tool. LMB configures the aimed splitter, RMB shows all on the map.",   "SHOP",Color.FromArgb(41,210,110)),
            ("⚠️", "Bottleneck Detector",  "Highlights the slowest conveyor belt so you can fix the weakest link fast.",   "HUD", Color.FromArgb(230,160,30)),
            ("📡", "Belt Scanner",         "Equippable tool. Floating items-per-minute labels on nearby belts.",           "SHOP",Color.FromArgb(51,181,229)),
            ("💰", "Sales Tracker",        "Session revenue, top-selling resources, and live earnings rate per minute.",    "HUD", Color.FromArgb(51,181,229)),
            ("🖥️", "Terminal Integration", "Open Factory HUD from any Computer Terminal via Factory Overview.",            "NEW", Color.FromArgb(41,210,110)),
        };

        // ─────────────────────────────────────────────────────────────────────
        public LandingForm()
        {
            SuspendLayout();
            AutoScaleMode   = AutoScaleMode.None;
            ClientSize      = new Size(W, FORM_H);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox     = false;
            StartPosition   = FormStartPosition.CenterScreen;
            Text            = "MML — Mine Mogul Logic v1.0.0";
            BackColor       = BG;
            DoubleBuffered  = true;
            Opacity         = 0;
            ResumeLayout(false);

            try { int p = DWMWCP_ROUND; DwmSetWindowAttribute(Handle, DWMWA_WINDOW_CORNER_PREFERENCE, ref p, sizeof(int)); } catch { }
            Icon = LoadAppIcon() ?? SystemIcons.Application;

            for (int i = 0; i < 55; i++) _particles.Add(RandomParticle(true));

            _animTimer.Tick += OnAnimTick;
            _animTimer.Start();
        }

        // ─────────────────────────────────────────────────────────────────────
        public static Icon? LoadAppIcon()
        {
            try
            {
                var s = Assembly.GetExecutingAssembly()
                    .GetManifestResourceStream("MineMogulModInstaller.app.ico");
                if (s != null) return new Icon(s);
            }
            catch { }
            return null;
        }

        // ── Animation ─────────────────────────────────────────────────────────
        private void OnAnimTick(object? sender, EventArgs e)
        {
            if (_fadeOpacity < 1f) { _fadeOpacity = Math.Min(1f, _fadeOpacity + 0.05f); Opacity = _fadeOpacity; }
            _glowPhase     = (_glowPhase + 0.025f) % (MathF.PI * 2f);
            _shimmerOffset = _shimmerOffset < W + 400 ? _shimmerOffset + 2.5f : -400f;

            if (!_titleDone)
            {
                _titleReveal++;
                if (_titleReveal >= "Mine Mogul Logic".Length) _titleDone = true;
            }

            _btnHover = _btnHovered
                ? Math.Min(1f, _btnHover + 0.08f)
                : Math.Max(0f, _btnHover - 0.06f);

            for (int i = 0; i < _particles.Count; i++)
            {
                var p = _particles[i];
                p.Y -= p.Speed;
                if (p.Y < -10) p = RandomParticle(false);
                _particles[i] = p;
            }
            Invalidate();
        }

        private Particle RandomParticle(bool randomY) => new()
        {
            X     = (float)_rng.NextDouble() * W,
            Y     = randomY ? (float)_rng.NextDouble() * HEADER_H : HEADER_H + 5,
            Speed = 0.3f + (float)_rng.NextDouble() * 0.8f,
            Size  = 1f   + (float)_rng.NextDouble() * 2.5f,
            Alpha = 30   + (float)_rng.NextDouble() * 130f,
        };

        // ── Painting ──────────────────────────────────────────────────────────
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode     = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            DrawHeader(g);
            DrawFeatureCards(g);
            DrawInstallButton(g);
            DrawFooter(g);
        }

        // ── Header ────────────────────────────────────────────────────────────
        private void DrawHeader(Graphics g)
        {
            var r = new Rectangle(0, 0, W, HEADER_H);
            using var bgBrush = new LinearGradientBrush(r, Color.FromArgb(12, 14, 22), Color.FromArgb(8, 10, 16), 90f);
            g.FillRectangle(bgBrush, r);

            // Glow
            float glow = 0.4f + 0.15f * MathF.Sin(_glowPhase);
            using var glowPath = BuildEllipse(W / 2f, 85, 240, 120);
            using var glowBrush = new PathGradientBrush(glowPath)
            { CenterColor = Color.FromArgb((int)(glow * 40), Accent), SurroundColors = new[] { Color.Transparent } };
            g.FillPath(glowBrush, glowPath);

            // Shimmer
            float so = _shimmerOffset;
            if (so > -120 && so < W + 120)
            {
                using var sh = new LinearGradientBrush(
                    new RectangleF(so - 120, 0, 240, HEADER_H), Color.Transparent, Color.Transparent, 0f)
                {
                    InterpolationColors = new ColorBlend
                    {
                        Positions = new[] { 0f, 0.5f, 1f },
                        Colors    = new[] { Color.Transparent, Color.FromArgb(15, 255, 255, 255), Color.Transparent }
                    }
                };
                g.FillRectangle(sh, so - 120, 0, 240, HEADER_H);
            }

            // Particles
            foreach (var p in _particles)
            {
                int a = (int)Math.Clamp(p.Alpha, 0, 200);
                using var pb = new SolidBrush(Color.FromArgb(a, Accent));
                g.FillEllipse(pb, p.X - p.Size / 2, p.Y - p.Size / 2, p.Size, p.Size);
            }

            // Bottom line
            using var lp = new Pen(Color.FromArgb(55, Accent), 1f);
            g.DrawLine(lp, 0, HEADER_H - 1, W, HEADER_H - 1);

            // Version badge (top-right)
            DrawBadge(g, "v1.0.0", W - 86, 16, Color.FromArgb(30, 80, 130), Accent);

            // Title
            string full   = "Mine Mogul Logic";
            string shown  = full[..Math.Min(_titleReveal, full.Length)];
            using var tf  = new Font("Segoe UI", 28f, FontStyle.Bold);
            var ts        = g.MeasureString(shown, tf);
            float tx      = (W - g.MeasureString(full, tf).Width) / 2f;
            float ty      = 38f;
            using var tb  = new SolidBrush(TextPrimary);
            g.DrawString(shown, tf, tb, tx, ty);
            if (!_titleDone || DateTime.Now.Millisecond < 500)
            {
                using var cb = new SolidBrush(Accent);
                g.DrawString("|", tf, cb, tx + ts.Width, ty);
            }

            // Subtitle
            using var sf  = new Font("Segoe UI", 9.5f);
            using var sb  = new SolidBrush(TextMuted);
            string sub    = "Satisfactory-style factory analytics for MineMogul";
            var ss        = g.MeasureString(sub, sf);
            g.DrawString(sub, sf, sb, (W - ss.Width) / 2f, ty + 52f);

            // Tag pills
            DrawTagPills(g, ty + 76f);
        }

        private static void DrawTagPills(Graphics g, float y)
        {
            string[] tags = { "BepInEx 5.4.23", "MineMogul", "Free & Open-source" };
            using var f = new Font("Segoe UI", 8f);
            float totalW = 0;
            foreach (var t in tags) totalW += g.MeasureString(t, f).Width + 24;
            float x = (W - totalW) / 2f;
            foreach (var tag in tags)
            {
                float tw = g.MeasureString(tag, f).Width;
                var rect = new RectangleF(x, y, tw + 18, 20f);
                using var bgB = new SolidBrush(Color.FromArgb(40, Accent));   g.FillRectangle(bgB, rect);
                using var bp  = new Pen(Color.FromArgb(70, Accent), 1f);      g.DrawRectangle(bp, rect.X, rect.Y, rect.Width, rect.Height);
                using var tb  = new SolidBrush(Color.FromArgb(180, Accent));  g.DrawString(tag, f, tb, x + 9, y + 3);
                x += tw + 24;
            }
        }

        // ── Feature cards ─────────────────────────────────────────────────────
        private void DrawFeatureCards(Graphics g)
        {
            using var titleFont = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            using var descFont  = new Font("Segoe UI", 8.5f);
            using var iconFont  = new Font("Segoe UI Emoji", 14f);
            using var tagFont   = new Font("Segoe UI", 7f, FontStyle.Bold);

            for (int i = 0; i < Features.Length; i++)
            {
                var (ico, title, desc, tag, tagCol) = Features[i];
                int col = i % 2;
                int row = i / 2;
                int x   = 14 + col * (CARD_W + 8);
                int y   = CARDS_Y + row * (CARD_H + CARD_GAP);
                var cr  = new Rectangle(x, y, CARD_W, CARD_H);

                // Card background
                using var cardBrush = new SolidBrush(CardBG);   g.FillRectangle(cardBrush, cr);
                using var borderPen = new Pen(CardBorder, 1f);  g.DrawRectangle(borderPen, cr);

                // Left accent stripe
                using var stripe = new SolidBrush(Color.FromArgb(160, tagCol));
                g.FillRectangle(stripe, x, y + 8, 3, CARD_H - 16);

                // Icon
                g.DrawString(ico, iconFont, Brushes.White, x + 10, y + 12);

                // Title
                using var titleBrush = new SolidBrush(TextPrimary);
                g.DrawString(title, titleFont, titleBrush, x + 40, y + 8);

                // Tag pill
                float titleW = g.MeasureString(title, titleFont).Width;
                var tagStr   = g.MeasureString(tag, tagFont);
                var tagRect  = new RectangleF(x + 40 + titleW + 4, y + 10, tagStr.Width + 10, 14f);
                using var tagBG  = new SolidBrush(Color.FromArgb(50, tagCol)); g.FillRectangle(tagBG,  tagRect);
                using var tagTxt = new SolidBrush(Color.FromArgb(220, tagCol));g.DrawString(tag, tagFont, tagTxt, tagRect.X + 5, tagRect.Y + 1);

                // Description
                using var descBrush = new SolidBrush(TextMuted);
                g.DrawString(desc, descFont, descBrush, new RectangleF(x + 40, y + 28, CARD_W - 52, CARD_H - 34));
            }
        }

        // ── Install button ────────────────────────────────────────────────────
        private void DrawInstallButton(Graphics g)
        {
            var rect = new RectangleF(14, BTN_Y, W - 28f, BTN_H);

            // Glow
            float pulse = 0.5f + 0.3f * MathF.Sin(_glowPhase * 1.4f);
            for (int s = 5; s >= 1; s--)
            {
                int a = (int)((pulse * 0.35f + _btnHover * 0.3f) * 200 / s);
                var er = new RectangleF(rect.X - s, rect.Y - s, rect.Width + s * 2, rect.Height + s * 2);
                using var gp = new Pen(Color.FromArgb(Math.Clamp(a, 0, 255), AccentGreen), 1f);
                g.DrawRectangle(gp, er.X, er.Y, er.Width, er.Height);
            }

            // Fill
            Color col = Color.FromArgb(
                (int)(28 + _btnHover * 10), (int)(88 + _btnHover * 20), (int)(45 + _btnHover * 10));
            using var bb = new SolidBrush(col);          g.FillRectangle(bb, rect);
            using var bp = new Pen(Color.FromArgb(60, 170, 85), 1.5f); g.DrawRectangle(bp, rect.X, rect.Y, rect.Width, rect.Height);

            // Arrow
            using var af = new Font("Segoe UI", 13f, FontStyle.Bold);
            using var ab = new SolidBrush(Color.FromArgb((int)(160 + 60 * _btnHover), AccentGreen));
            g.DrawString("→", af, ab, rect.Right - 42 + _btnHover * 5, rect.Y + (BTN_H - 20) / 2f);

            // Text
            using var bf  = new Font("Segoe UI", 11f, FontStyle.Bold);
            const string txt = "Get Started  —  Install / Uninstall";
            var mts = g.MeasureString(txt, bf);
            using var btb = new SolidBrush(Color.White);
            g.DrawString(txt, bf, btb, rect.X + (rect.Width - mts.Width) / 2f - 16, rect.Y + (BTN_H - mts.Height) / 2f);
        }

        // ── Footer ────────────────────────────────────────────────────────────
        private static void DrawFooter(Graphics g)
        {
            float y = FORM_H - 32f;
            using var f = new Font("Segoe UI", 7.5f);
            using var b = new SolidBrush(Color.FromArgb(45, 50, 70));
            const string txt = "MML v1.0.0  •  Open-source BepInEx mod  •  Not affiliated with NoodleForge";
            var ts = g.MeasureString(txt, f);
            g.DrawString(txt, f, b, (W - ts.Width) / 2f, y);
        }

        // ── Mouse ─────────────────────────────────────────────────────────────
        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            var btnRect = new RectangleF(14, BTN_Y, W - 28f, BTN_H);
            _btnHovered = btnRect.Contains(e.Location);
            Cursor = _btnHovered ? Cursors.Hand : Cursors.Default;
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _btnHovered = false;
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            if (new RectangleF(14, BTN_Y, W - 28f, BTN_H).Contains(e.Location))
                OpenInstaller();
        }

        private void OpenInstaller()
        {
            var form = new InstallerForm();
            form.Show();
            Hide();
            form.FormClosed += (_, _) => System.Windows.Forms.Application.Exit();
        }

        // ── Utilities ─────────────────────────────────────────────────────────
        private static void DrawBadge(Graphics g, string text, float x, float y, Color bg, Color fg)
        {
            using var f  = new Font("Segoe UI", 8f, FontStyle.Bold);
            var ts       = g.MeasureString(text, f);
            var r        = new RectangleF(x, y, ts.Width + 14, ts.Height + 4);
            using var bb = new SolidBrush(Color.FromArgb(150, bg));  g.FillRectangle(bb, r);
            using var bp = new Pen(Color.FromArgb(170, fg), 1f);     g.DrawRectangle(bp, r.X, r.Y, r.Width, r.Height);
            using var tb = new SolidBrush(fg);                        g.DrawString(text, f, tb, x + 7, y + 2);
        }

        private static GraphicsPath BuildEllipse(float cx, float cy, float rx, float ry)
        {
            var p = new GraphicsPath();
            p.AddEllipse(cx - rx, cy - ry, rx * 2, ry * 2);
            return p;
        }
    }
}
