using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
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

        static readonly Color BG          = Color.FromArgb(10, 11, 16);
        static readonly Color HeaderTop   = Color.FromArgb(12, 14, 22);
        static readonly Color HeaderBot   = Color.FromArgb(8,  10, 16);
        static readonly Color Accent      = Color.FromArgb(51, 181, 229);
        static readonly Color AccentGreen = Color.FromArgb(41, 210, 110);
        static readonly Color CardBG      = Color.FromArgb(17, 19, 28);
        static readonly Color CardBorder  = Color.FromArgb(32, 36, 52);
        static readonly Color TextPrimary = Color.FromArgb(230, 233, 245);
        static readonly Color TextMuted   = Color.FromArgb(100, 110, 140);

        private struct Particle { public float X, Y, Speed, Size, Alpha; }
        private readonly List<Particle> _particles = new();
        private readonly Random _rng = new();

        private float  _fadeOpacity   = 0f;
        private float  _glowPhase     = 0f;
        private float  _shimmerOffset = -800f;
        private int    _titleReveal   = 0;
        private bool   _titleDone     = false;
        private float  _btnHover      = 0f;
        private bool   _btnHovered    = false;
        private Image? _banner;

        private readonly System.Windows.Forms.Timer _animTimer = new();
        private const int W = 660;
        private const int HEADER_H = 220;

        private static readonly (string Icon, string Title, string Desc, string Tag, Color TagCol)[] Features = {
            ("📊", "Factory HUD",          "Live overlay with Machines, Belts, Sales & Settings tabs — toggle with F5.",                  "F5",  Color.FromArgb(51,181,229)),
            ("🔧", "Splitter Wrench",       "Buyable tool: LMB configures the splitter you aim at, RMB shows all splitters on the map.", "NEW", Color.FromArgb(41,210,110)),
            ("⚠️",  "Bottleneck Detector",  "Auto-flags the slowest conveyor belts so you can fix the weakest link fast.",               "HUD", Color.FromArgb(230,160,30)),
            ("🏷️", "Belt Item Counter",     "Floating items-per-minute labels on nearby belts, color-coded by saturation.  (F6)",        "F6",  Color.FromArgb(51,181,229)),
            ("💰", "Sales Tracker",         "Session revenue, top-selling resources, and live earnings rate per minute.",                 "HUD", Color.FromArgb(51,181,229)),
            ("🖥️", "Terminal Integration",  "Open the Factory HUD from any Computer Terminal via the Factory Overview option.",          "NEW", Color.FromArgb(41,210,110)),
        };

        public LandingForm()
        {
            SuspendLayout();
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode  = AutoScaleMode.Font;
            ClientSize     = new Size(W, 630);
            FormBorderStyle= FormBorderStyle.FixedSingle;
            MaximizeBox    = false;
            StartPosition  = FormStartPosition.CenterScreen;
            Text           = "MML  —  Mine Mogul Logic  v1.0.0";
            BackColor      = BG;
            DoubleBuffered = true;
            Opacity        = 0;
            ResumeLayout(false);

            try { int p = DWMWCP_ROUND; DwmSetWindowAttribute(Handle, DWMWA_WINDOW_CORNER_PREFERENCE, ref p, sizeof(int)); } catch { }
            Icon    = LoadAppIcon() ?? SystemIcons.Application;
            _banner = LoadBannerImage();

            for (int i = 0; i < 60; i++) _particles.Add(RandomParticle(true));

            _animTimer.Interval = 16;
            _animTimer.Tick += OnAnimTick;
            _animTimer.Start();
        }

        public static Icon? LoadAppIcon()
        {
            try
            {
                var s = Assembly.GetExecutingAssembly().GetManifestResourceStream("MineMogulModInstaller.app.ico");
                if (s != null) return new Icon(s);
            }
            catch { }
            return null;
        }

        private static Image? LoadBannerImage()
        {
            try
            {
                var s = Assembly.GetExecutingAssembly().GetManifestResourceStream("MineMogulModInstaller.banner.png");
                if (s != null) return Image.FromStream(new MemoryStream(new System.IO.BinaryReader(s).ReadBytes((int)s.Length)));
            }
            catch { }
            return null;
        }

        private void OnAnimTick(object? sender, EventArgs e)
        {
            if (_fadeOpacity < 1f) { _fadeOpacity = Math.Min(1f, _fadeOpacity + 0.05f); Opacity = _fadeOpacity; }
            _glowPhase     = (_glowPhase + 0.025f) % (MathF.PI * 2f);
            _shimmerOffset = (_shimmerOffset + 2.5f);
            if (_shimmerOffset > W + 400) _shimmerOffset = -400;
            if (!_titleDone) { _titleReveal++; if (_titleReveal >= "Mine Mogul Logic".Length) _titleDone = true; }
            _btnHover = _btnHovered ? Math.Min(1f, _btnHover + 0.08f) : Math.Max(0f, _btnHover - 0.06f);
            for (int i = 0; i < _particles.Count; i++)
            {
                var p = _particles[i]; p.Y -= p.Speed;
                if (p.Y < -10) p = RandomParticle(false);
                _particles[i] = p;
            }
            Invalidate();
        }

        private Particle RandomParticle(bool randomY) => new Particle
        {
            X = (float)_rng.NextDouble() * W,
            Y = randomY ? (float)_rng.NextDouble() * HEADER_H : HEADER_H + 5,
            Speed = 0.3f + (float)_rng.NextDouble() * 0.8f,
            Size  = 1f + (float)_rng.NextDouble() * 2.5f,
            Alpha = 30 + (float)_rng.NextDouble() * 140f,
        };

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode     = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            DrawHeader(g); DrawFeatures(g); DrawInstallButton(g); DrawFooter(g);
        }

        private void DrawHeader(Graphics g)
        {
            var r = new Rectangle(0, 0, W, HEADER_H);
            using var bgBrush = new LinearGradientBrush(r, HeaderTop, HeaderBot, 90f);
            g.FillRectangle(bgBrush, r);

            if (_banner != null)
            {
                var ia = new ImageAttributes();
                ia.SetColorMatrix(new ColorMatrix { Matrix33 = 0.18f });
                g.DrawImage(_banner, r, 0, 0, _banner.Width, _banner.Height, GraphicsUnit.Pixel, ia);
            }

            float glow = 0.4f + 0.15f * MathF.Sin(_glowPhase);
            using var radPath = BuildEllipsePath(W / 2f, 90, 260, 130);
            using var radBrush = new PathGradientBrush(radPath)
            { CenterColor = Color.FromArgb((int)(glow * 45), Accent), SurroundColors = new[] { Color.Transparent } };
            g.FillPath(radBrush, radPath);

            if (_shimmerOffset > -120 && _shimmerOffset < W + 120)
            {
                using var shimBrush = new LinearGradientBrush(
                    new RectangleF(_shimmerOffset - 120, 0, 240, HEADER_H), Color.Transparent, Color.Transparent, 0f)
                {
                    InterpolationColors = new ColorBlend
                    {
                        Positions = new[] { 0f, 0.5f, 1f },
                        Colors = new[] { Color.Transparent, Color.FromArgb(18, 255, 255, 255), Color.Transparent }
                    }
                };
                g.FillRectangle(shimBrush, _shimmerOffset - 120, 0, 240, HEADER_H);
            }

            foreach (var p in _particles)
            {
                using var pb = new SolidBrush(Color.FromArgb((int)Math.Clamp(p.Alpha, 0, 200), Accent));
                g.FillEllipse(pb, p.X - p.Size / 2, p.Y - p.Size / 2, p.Size, p.Size);
            }

            using var lp1 = new Pen(Color.FromArgb(60, Accent), 1f);
            using var lp2 = new Pen(Color.FromArgb(18, Accent), 4f);
            g.DrawLine(lp1, 0, HEADER_H - 1, W, HEADER_H - 1);
            g.DrawLine(lp2, 0, HEADER_H - 1, W, HEADER_H - 1);

            DrawBadge(g, "v1.0.0", W - 82, 18, Color.FromArgb(30, 80, 130), Accent);

            string titleFull  = "Mine Mogul Logic";
            string titleShown = titleFull[..Math.Min(_titleReveal, titleFull.Length)];
            using var titleFont = new Font("Segoe UI", 30f, FontStyle.Bold);
            var ts = g.MeasureString(titleShown, titleFont);
            float tx = (W - ts.Width) / 2f, ty = 52f;
            for (int s = 4; s >= 1; s--)
            {
                using var sb2 = new SolidBrush(Color.FromArgb(18 * s, Accent));
                g.DrawString(titleShown, titleFont, sb2, tx - s, ty - s);
                g.DrawString(titleShown, titleFont, sb2, tx + s, ty + s);
            }
            using var tb = new SolidBrush(TextPrimary);
            g.DrawString(titleShown, titleFont, tb, tx, ty);
            if (!_titleDone || DateTime.Now.Millisecond < 500)
            {
                using var cb = new SolidBrush(Accent);
                g.DrawString("|", titleFont, cb, tx + ts.Width, ty);
            }

            using var subFont  = new Font("Segoe UI", 10f);
            using var subBrush = new SolidBrush(TextMuted);
            string sub = "Satisfactory-style factory analytics for MineMogul";
            var ss = g.MeasureString(sub, subFont);
            g.DrawString(sub, subFont, subBrush, (W - ss.Width) / 2f, ty + 56f);

            DrawHeaderTags(g, ty + 82f);
        }

        private static void DrawHeaderTags(Graphics g, float y)
        {
            string[] tags = { "BepInEx 5.4.23", "MineMogul", "Free & Open-source" };
            using var font = new Font("Segoe UI", 8f);
            float totalW = 0;
            foreach (var t in tags) totalW += g.MeasureString(t, font).Width + 28;
            float x = (W - totalW) / 2f;
            foreach (var tag in tags)
            {
                var tw = g.MeasureString(tag, font).Width;
                var rect = new RectangleF(x, y, tw + 20, 22f);
                using var bgB = new SolidBrush(Color.FromArgb(45, Accent));
                FillRoundRect(g, bgB, rect, 6);
                using var bp = new Pen(Color.FromArgb(80, Accent), 1f);
                DrawRoundRect(g, bp, rect, 6);
                using var textB = new SolidBrush(Color.FromArgb(190, Accent));
                g.DrawString(tag, font, textB, x + 10, y + 4);
                x += tw + 28;
            }
        }

        private void DrawFeatures(Graphics g)
        {
            float startY = HEADER_H + 18, cardH = 60f, cardW = (W - 40f) / 2f, gap = 10f;
            using var titleFont = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            using var descFont  = new Font("Segoe UI", 8.5f);
            using var iconFont  = new Font("Segoe UI Emoji", 14f);
            using var tagFont   = new Font("Segoe UI", 7f, FontStyle.Bold);

            for (int i = 0; i < Features.Length; i++)
            {
                var (ico, title, desc, tag, tagCol) = Features[i];
                float x = 14f + (i % 2) * (cardW + gap);
                float y = startY + (i / 2) * (cardH + gap);
                var cardRect = new RectangleF(x, y, cardW, cardH);

                using var cardBrush  = new SolidBrush(CardBG);
                FillRoundRect(g, cardBrush, cardRect, 8);
                using var borderPen  = new Pen(CardBorder, 1f);
                DrawRoundRect(g, borderPen, cardRect, 8);
                using var accentBar  = new SolidBrush(Color.FromArgb(180, tagCol));
                g.FillRectangle(accentBar, new RectangleF(x, y + 10, 3, cardH - 20));

                g.DrawString(ico, iconFont, Brushes.White, x + 12, y + 12);

                using var titleBrush = new SolidBrush(TextPrimary);
                g.DrawString(title, titleFont, titleBrush, x + 42, y + 8);
                var titleW = g.MeasureString(title, titleFont).Width;
                var br = new RectangleF(x + 42 + titleW + 4, y + 10, g.MeasureString(tag, tagFont).Width + 10, 14);
                using var tagBG  = new SolidBrush(Color.FromArgb(50, tagCol));
                FillRoundRect(g, tagBG, br, 4);
                using var tagTxt = new SolidBrush(Color.FromArgb(220, tagCol));
                g.DrawString(tag, tagFont, tagTxt, br.X + 5, br.Y + 1);

                using var descBrush = new SolidBrush(TextMuted);
                g.DrawString(desc, descFont, descBrush, new RectangleF(x + 42, y + 27, cardW - 55, cardH - 32));
            }
        }

        private void DrawInstallButton(Graphics g)
        {
            float y = ClientSize.Height - 105f;
            var rect = new RectangleF(14, y, W - 28f, 52f);
            float pulse = 0.55f + 0.3f * MathF.Sin(_glowPhase * 1.4f);

            for (int s = 6; s >= 1; s--)
            {
                int a = (int)((pulse * 0.4f + _btnHover * 0.3f) * 255 / s);
                var er = new RectangleF(rect.X - s * 1.5f, rect.Y - s * 1.5f, rect.Width + s * 3f, rect.Height + s * 3f);
                using var gp = new Pen(Color.FromArgb(Math.Clamp(a, 0, 255), AccentGreen), 1.5f);
                DrawRoundRect(g, gp, er, 10 + s);
            }

            var col = BlendColor(Color.FromArgb(28, 100, 52), Color.FromArgb(35, 130, 68), _btnHover);
            using var bb = new SolidBrush(col); FillRoundRect(g, bb, rect, 10);
            using var bp = new Pen(Color.FromArgb(55, 170, 85), 1.5f); DrawRoundRect(g, bp, rect, 10);

            using var af = new Font("Segoe UI", 14f, FontStyle.Bold);
            using var ab = new SolidBrush(Color.FromArgb((int)(180 + 50 * _btnHover), AccentGreen));
            g.DrawString("→", af, ab, rect.Right - 46 + _btnHover * 6, rect.Y + 12);

            using var bf  = new Font("Segoe UI", 12f, FontStyle.Bold);
            string txt = "Get Started  —  Install / Uninstall";
            var ts = g.MeasureString(txt, bf);
            using var btb = new SolidBrush(Color.White);
            g.DrawString(txt, bf, btb, rect.X + (rect.Width - ts.Width) / 2f - 20, rect.Y + (rect.Height - ts.Height) / 2f);
        }

        private void DrawFooter(Graphics g)
        {
            float y = ClientSize.Height - 42f;
            using var f  = new Font("Segoe UI", 7.5f);
            using var b  = new SolidBrush(Color.FromArgb(45, 50, 70));
            string txt = "MML v1.0.0  •  Open-source BepInEx mod  •  Not affiliated with NoodleForge";
            var ts = g.MeasureString(txt, f);
            g.DrawString(txt, f, b, (W - ts.Width) / 2f, y);
            using var lf = new Font("Segoe UI", 7.5f, FontStyle.Underline);
            using var lb = new SolidBrush(Color.FromArgb(60, Accent));
            g.DrawString("📄 Documentation", lf, lb, 14, y);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            float y = ClientSize.Height - 105f;
            _btnHovered = new RectangleF(14, y, W - 28f, 52f).Contains(e.Location);
            Cursor = _btnHovered ? Cursors.Hand : Cursors.Default;
        }

        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); _btnHovered = false; }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            float y = ClientSize.Height - 105f;
            if (new RectangleF(14, y, W - 28f, 52f).Contains(e.Location)) OpenInstaller();
            if (new RectangleF(14, ClientSize.Height - 42f, 130, 18).Contains(e.Location))
                try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = "https://github.com", UseShellExecute = true }); } catch { }
        }

        private void OpenInstaller()
        {
            var form = new InstallerForm();
            form.Show(); this.Hide();
            form.FormClosed += (_, _) => Application.Exit();
        }

        private static void DrawBadge(Graphics g, string text, float x, float y, Color bg, Color fg)
        {
            using var f = new Font("Segoe UI", 8f, FontStyle.Bold);
            var ts = g.MeasureString(text, f);
            var r = new RectangleF(x, y, ts.Width + 16, ts.Height + 4);
            using var bb = new SolidBrush(Color.FromArgb(160, bg)); FillRoundRect(g, bb, r, 5);
            using var bp = new Pen(Color.FromArgb(180, fg), 1f); DrawRoundRect(g, bp, r, 5);
            using var tb = new SolidBrush(fg); g.DrawString(text, f, tb, x + 8, y + 2);
        }

        private static GraphicsPath BuildEllipsePath(float cx, float cy, float rx, float ry)
        { var p = new GraphicsPath(); p.AddEllipse(cx - rx, cy - ry, rx * 2, ry * 2); return p; }

        private static Color BlendColor(Color a, Color b, float t)
        {
            t = Math.Clamp(t, 0f, 1f);
            return Color.FromArgb((int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));
        }

        private static void FillRoundRect(Graphics g, Brush b, RectangleF r, int rad)
        { using var p = RoundRectPath(r, rad); g.FillPath(b, p); }

        private static void DrawRoundRect(Graphics g, Pen p, RectangleF r, int rad)
        { using var path = RoundRectPath(r, rad); g.DrawPath(p, path); }

        private static GraphicsPath RoundRectPath(RectangleF r, int rad)
        {
            var path = new GraphicsPath();
            float d = rad * 2f;
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}