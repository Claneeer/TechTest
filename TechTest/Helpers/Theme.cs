using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace TechTest.Helpers
{
    public static class Theme
    {
        // Scale factor based on screen resolution (1.0 = 1920x1080 baseline)
        private static float _scaleFactor = -1f;
        private static bool _isCustomScale = false;

        public static float ScaleFactor
        {
            get
            {
                if (_scaleFactor < 0)
                    _scaleFactor = CalculateScaleFactor();
                return _scaleFactor;
            }
            set
            {
                _scaleFactor = value;
                _isCustomScale = true;
            }
        }

        public static bool IsCustomScale => _isCustomScale;

        public static void ResetScale()
        {
            _scaleFactor = CalculateScaleFactor();
            _isCustomScale = false;
        }

        private static float CalculateScaleFactor()
        {
            var screen = Screen.PrimaryScreen.WorkingArea;
            // Baseline: 1920x1080. Scale proportionally to the smaller dimension.
            float scaleW = screen.Width / 1920f;
            float scaleH = screen.Height / 1080f;
            float scale = Math.Min(scaleW, scaleH);
            // Clamp between 0.6 and 1.5 to avoid extremes
            return Math.Max(0.6f, Math.Min(1.5f, scale));
        }

        /// <summary>Scale an integer value by the screen factor.</summary>
        public static int S(int value) => (int)(value * ScaleFactor);

        /// <summary>Scale a float value by the screen factor.</summary>
        public static float S(float value) => value * ScaleFactor;

        // Backgrounds
        public static readonly Color BgDark = Color.FromArgb(10, 10, 20);
        public static readonly Color BgPrimary = Color.FromArgb(18, 18, 36);
        public static readonly Color BgCard = Color.FromArgb(28, 28, 56);
        public static readonly Color BgCardHover = Color.FromArgb(38, 38, 76);
        public static readonly Color BgInput = Color.FromArgb(22, 22, 44);

        // Accents
        public static readonly Color Accent = Color.FromArgb(99, 102, 241);
        public static readonly Color AccentLight = Color.FromArgb(129, 140, 248);
        public static readonly Color AccentDark = Color.FromArgb(67, 56, 202);

        // Text
        public static readonly Color TextPrimary = Color.FromArgb(240, 240, 255);
        public static readonly Color TextSecondary = Color.FromArgb(148, 148, 184);
        public static readonly Color TextMuted = Color.FromArgb(100, 100, 140);

        // Status
        public static readonly Color Success = Color.FromArgb(34, 197, 94);
        public static readonly Color Error = Color.FromArgb(239, 68, 68);
        public static readonly Color Warning = Color.FromArgb(234, 179, 8);

        // Border
        public static readonly Color Border = Color.FromArgb(50, 50, 90);

        // Scaled Fonts
        public static Font FontTitle => new Font("Segoe UI", S(22f), FontStyle.Bold);
        public static Font FontSubtitle => new Font("Segoe UI", S(12f), FontStyle.Regular);
        public static Font FontBody => new Font("Segoe UI", S(10f), FontStyle.Regular);
        public static Font FontSmall => new Font("Segoe UI", S(9f), FontStyle.Regular);
        public static Font FontIcon => new Font("Segoe UI Emoji", S(32f), FontStyle.Regular);
        public static Font FontCardTitle => new Font("Segoe UI Semibold", S(12f), FontStyle.Bold);
        public static Font FontCardDesc => new Font("Segoe UI", S(9f), FontStyle.Regular);
        public static Font FontButton => new Font("Segoe UI Semibold", S(10f), FontStyle.Bold);
        public static Font FontHeader => new Font("Segoe UI", S(16f), FontStyle.Bold);

        public static GraphicsPath RoundedRect(Rectangle bounds, int radius)
        {
            var path = new GraphicsPath();
            int d = radius * 2;
            if (d > bounds.Width) d = bounds.Width;
            if (d > bounds.Height) d = bounds.Height;
            path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
            path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
            path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        public static void StyleForm(Form form, string title, int width = 900, int height = 650)
        {
            form.Text = title;
            form.AutoScaleMode = AutoScaleMode.Dpi;

            // Scale requested size
            int scaledW = S(width);
            int scaledH = S(height);

            // Constrain to screen working area
            var screen = Screen.PrimaryScreen.WorkingArea;
            scaledW = Math.Min(scaledW, screen.Width - 20);
            scaledH = Math.Min(scaledH, screen.Height - 20);

            form.Size = new Size(scaledW, scaledH);
            form.StartPosition = FormStartPosition.CenterScreen;
            form.BackColor = BgPrimary;
            form.ForeColor = TextPrimary;
            form.Font = FontBody;
            form.FormBorderStyle = FormBorderStyle.FixedSingle;
            form.MaximizeBox = false;
            typeof(Form).GetProperty("DoubleBuffered",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                ?.SetValue(form, true);
        }

        public static Button CreateButton(string text, int x, int y, int w = 160, int h = 42)
        {
            var btn = new Button
            {
                Text = text,
                Location = new Point(S(x), S(y)),
                Size = new Size(S(w), S(h)),
                FlatStyle = FlatStyle.Flat,
                BackColor = Accent,
                ForeColor = TextPrimary,
                Font = FontButton,
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.FlatAppearance.MouseOverBackColor = AccentLight;
            return btn;
        }

        public static Button CreateSecondaryButton(string text, int x, int y, int w = 160, int h = 42)
        {
            var btn = new Button
            {
                Text = text,
                Location = new Point(S(x), S(y)),
                Size = new Size(S(w), S(h)),
                FlatStyle = FlatStyle.Flat,
                BackColor = BgCard,
                ForeColor = TextPrimary,
                Font = FontButton,
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderSize = 1;
            btn.FlatAppearance.BorderColor = Border;
            btn.FlatAppearance.MouseOverBackColor = BgCardHover;
            return btn;
        }

        public static Label CreateLabel(string text, int x, int y, Font font = null, Color? color = null)
        {
            return new Label
            {
                Text = text,
                Location = new Point(S(x), S(y)),
                AutoSize = true,
                Font = font ?? FontBody,
                ForeColor = color ?? TextPrimary,
                BackColor = Color.Transparent
            };
        }

        public static Panel CreatePanel(int x, int y, int w, int h)
        {
            var panel = new Panel
            {
                Location = new Point(S(x), S(y)),
                Size = new Size(S(w), S(h)),
                BackColor = BgCard,
                BorderStyle = BorderStyle.None
            };
            return panel;
        }
    }
}
