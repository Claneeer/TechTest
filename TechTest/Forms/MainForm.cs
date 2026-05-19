using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Management;
using TechTest.Helpers;

namespace TechTest.Forms
{
    public class MainForm : Form
    {
        private readonly (string icon, string title, string desc, Type formType)[] _cards = new[]
        {
            ("🎤", "Microfone", "Teste o microfone integrado", typeof(MicrophoneTestForm)),
            ("🔊", "Alto-falantes", "Teste áudio esquerdo e direito", typeof(AudioTestForm)),
            ("📷", "Webcam", "Teste a câmera integrada", typeof(CameraTestForm)),
            ("🖥", "Pixel Morto", "Verifique pixels defeituosos", typeof(DeadPixelTestForm)),
            ("⌨", "Teclado", "Teste todas as teclas", typeof(KeyboardTestForm)),
            ("🖱", "Touchpad", "Teste movimento e cliques", typeof(TouchpadTestForm)),
            ("🔋", "Bateria", "Verifique saúde da bateria", typeof(BatteryTestForm)),
            ("🔥", "FurMark", "Teste de estresse da GPU", (Type)null),
        };

        private Panel[] _cardPanels;
        private int _hoveredIndex = -1;
        private Label _lblSysInfo;

        public MainForm()
        {
            Theme.StyleForm(this, "TechTest Notebook — Diagnóstico de Hardware", 1060, 720);
            this.FormBorderStyle = FormBorderStyle.Sizable;
            this.MaximizeBox = true;
            this.MinimumSize = this.Size;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            BuildUI();
        }

        private void BuildUI()
        {
            _cardPanels = new Panel[_cards.Length];

            for (int i = 0; i < _cards.Length; i++)
            {
                var panel = new Panel
                {
                    BackColor = Color.Transparent,
                    Cursor = Cursors.Hand,
                    Tag = i
                };

                SetDoubleBuffered(panel);
                int index = i;
                panel.Paint += (s, e) => PaintCard(e.Graphics, panel.ClientRectangle, index);
                panel.MouseEnter += (s, e) => { _hoveredIndex = index; panel.Invalidate(); };
                panel.MouseLeave += (s, e) => { _hoveredIndex = -1; panel.Invalidate(); };
                panel.Click += (s, e) =>
                {
                    if (_cards[index].formType != null)
                        OpenTest(_cards[index].formType);
                    else
                        LaunchFurMark();
                };

                _cardPanels[i] = panel;
                this.Controls.Add(panel);
            }

            // Arrange the card panels initially
            RearrangeCards();

            // Bottom bar panel
            var bottomBar = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = Theme.S(45),
                BackColor = Theme.BgDark,
                Padding = new Padding(Theme.S(15), Theme.S(5), Theme.S(15), Theme.S(5))
            };
            this.Controls.Add(bottomBar);

            // Scale ComboBox (Right)
            var cmbScale = new ComboBox
            {
                Width = Theme.S(130),
                Dock = DockStyle.Right,
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Theme.BgInput,
                ForeColor = Theme.TextPrimary,
                FlatStyle = FlatStyle.Flat,
                Font = Theme.FontSmall
            };
            cmbScale.Items.AddRange(new object[] { "Auto (DPI)", "75% (Pequeno)", "100% (Padrão)", "125% (Médio)", "150% (Grande)" });
            
            if (!Theme.IsCustomScale)
                cmbScale.SelectedIndex = 0;
            else if (Math.Abs(Theme.ScaleFactor - 0.75f) < 0.01f)
                cmbScale.SelectedIndex = 1;
            else if (Math.Abs(Theme.ScaleFactor - 1.0f) < 0.01f)
                cmbScale.SelectedIndex = 2;
            else if (Math.Abs(Theme.ScaleFactor - 1.25f) < 0.01f)
                cmbScale.SelectedIndex = 3;
            else if (Math.Abs(Theme.ScaleFactor - 1.5f) < 0.01f)
                cmbScale.SelectedIndex = 4;
            else
                cmbScale.SelectedIndex = 0;

            cmbScale.SelectedIndexChanged += (s, e) =>
            {
                float newScale = -1f;
                switch (cmbScale.SelectedIndex)
                {
                    case 0: newScale = -1f; break; // Auto
                    case 1: newScale = 0.75f; break; // 75%
                    case 2: newScale = 1.0f; break; // 100%
                    case 3: newScale = 1.25f; break; // 125%
                    case 4: newScale = 1.5f; break; // 150%
                }
                ChangeScale(newScale);
            };

            var lblScaleTitle = new Label
            {
                Text = "🖥 Ajustar Tela:  ",
                Dock = DockStyle.Right,
                AutoSize = true,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = Theme.FontSmall,
                ForeColor = Theme.TextSecondary,
                BackColor = Color.Transparent
            };

            // System info label (Left/Fill)
            _lblSysInfo = new Label
            {
                Text = "💻 Carregando informações do sistema...",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = Theme.FontSmall,
                ForeColor = Theme.TextMuted,
                BackColor = Color.Transparent
            };

            bottomBar.Controls.Add(_lblSysInfo);
            bottomBar.Controls.Add(lblScaleTitle);
            bottomBar.Controls.Add(cmbScale);

            LoadSystemInfoAsync();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            RearrangeCards();
            this.Invalidate(); // Force background gradient and title text to repaint at the new size!
        }

        private void RearrangeCards()
        {
            if (_cardPanels == null) return;

            int cardW = Theme.S(220);
            int cardH = Theme.S(175);
            int gap = Theme.S(20);

            // Determine optimal number of columns based on current client width
            int availableW = this.ClientSize.Width - gap * 2;
            int cols = Math.Max(1, availableW / (cardW + gap));
            if (cols > _cards.Length) cols = _cards.Length;

            int gridW = cols * cardW + (cols - 1) * gap;
            int startX = (this.ClientSize.Width - gridW) / 2;
            int startY = Theme.S(130);

            for (int i = 0; i < _cards.Length; i++)
            {
                if (_cardPanels[i] == null) continue;

                int row = i / cols;
                int col = i % cols;
                int x = startX + col * (cardW + gap);
                int y = startY + row * (cardH + gap);

                _cardPanels[i].Location = new Point(x, y);
                _cardPanels[i].Size = new Size(cardW, cardH);
            }
        }

        private void ChangeScale(float scale)
        {
            // Avoid redundant scaling if already at that scale
            if (scale < 0 && !Theme.IsCustomScale) return;
            if (scale > 0 && Theme.IsCustomScale && Math.Abs(Theme.ScaleFactor - scale) < 0.01f) return;

            if (scale < 0)
            {
                Theme.ResetScale();
            }
            else
            {
                Theme.ScaleFactor = scale;
            }

            // Save system info text to restore it without reloading async
            string currentInfo = _lblSysInfo?.Text ?? "Carregando...";

            // Remove all controls
            this.Controls.Clear();

            // Reapply styling using the new ScaleFactor!
            Theme.StyleForm(this, "TechTest Notebook — Diagnóstico de Hardware", 1060, 720);

            // Rebuild UI
            BuildUI();

            // Restore system info
            _lblSysInfo.Text = currentInfo;

            // Apply size changes and center on screen
            this.StartPosition = FormStartPosition.CenterScreen;
            var screen = Screen.PrimaryScreen.WorkingArea;
            int x = (screen.Width - this.Width) / 2;
            int y = (screen.Height - this.Height) / 2;
            this.Location = new Point(x, y);

            this.Invalidate();
        }

        private void SetDoubleBuffered(Panel panel)
        {
            typeof(Panel).GetProperty("DoubleBuffered",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                ?.SetValue(panel, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            // Background gradient
            using (var brush = new LinearGradientBrush(
                this.ClientRectangle, Theme.BgDark, Theme.BgPrimary, 90f))
            {
                g.FillRectangle(brush, this.ClientRectangle);
            }

            // Title
            string title = "TechTest Notebook";
            using (var font = Theme.FontTitle)
            {
                var titleSize = g.MeasureString(title, font);
                float titleX = (this.ClientSize.Width - titleSize.Width) / 2;
                g.DrawString(title, font, new SolidBrush(Theme.TextPrimary), titleX, Theme.S(25));
            }

            // Subtitle
            string subtitle = "Diagnóstico de Hardware para Notebooks";
            using (var font = Theme.FontSubtitle)
            {
                var subSize = g.MeasureString(subtitle, font);
                float subX = (this.ClientSize.Width - subSize.Width) / 2;
                g.DrawString(subtitle, font, new SolidBrush(Theme.TextSecondary), subX, Theme.S(68));
            }

            // Accent line
            int lineW = Theme.S(120);
            int lineX = (this.ClientSize.Width - lineW) / 2;
            using (var pen = new Pen(Theme.Accent, 3))
            {
                g.DrawLine(pen, lineX, Theme.S(100), lineX + lineW, Theme.S(100));
            }
        }

        private void PaintCard(Graphics g, Rectangle rect, int index)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            bool hovered = (_hoveredIndex == index);
            var bounds = new Rectangle(1, 1, rect.Width - 2, rect.Height - 2);

            using (var path = Theme.RoundedRect(bounds, 14))
            {
                // Card background
                Color bgColor = hovered ? Theme.BgCardHover : Theme.BgCard;
                using (var brush = new SolidBrush(bgColor))
                    g.FillPath(brush, path);

                // Border
                Color borderColor = hovered ? Theme.Accent : Theme.Border;
                using (var pen = new Pen(borderColor, hovered ? 2f : 1f))
                    g.DrawPath(pen, path);

                // Glow on hover
                if (hovered)
                {
                    using (var glowBrush = new SolidBrush(Color.FromArgb(15, Theme.Accent)))
                        g.FillPath(glowBrush, path);
                }
            }

            var card = _cards[index];

            // Icon
            using (var iconFont = Theme.FontIcon)
            {
                var iconSize = g.MeasureString(card.icon, iconFont);
                float iconX = (rect.Width - iconSize.Width) / 2;
                g.DrawString(card.icon, iconFont, Brushes.White, iconX, Theme.S(20));
            }

            // Title
            using (var titleFont = Theme.FontCardTitle)
            {
                var titleSize = g.MeasureString(card.title, titleFont);
                float titleX = (rect.Width - titleSize.Width) / 2;
                g.DrawString(card.title, titleFont, new SolidBrush(Theme.TextPrimary), titleX, Theme.S(85));
            }

            // Description
            using (var descFont = Theme.FontCardDesc)
            {
                var descSize = g.MeasureString(card.desc, descFont);
                float descX = (rect.Width - descSize.Width) / 2;
                g.DrawString(card.desc, descFont, new SolidBrush(Theme.TextSecondary), descX, Theme.S(115));
            }

            // Bottom accent bar on hover
            if (hovered)
            {
                using (var accentBrush = new LinearGradientBrush(
                    new Rectangle(Theme.S(20), rect.Height - Theme.S(6), rect.Width - Theme.S(40), Theme.S(3)),
                    Theme.Accent, Theme.AccentLight, 0f))
                {
                    g.FillRectangle(accentBrush, Theme.S(20), rect.Height - Theme.S(6), rect.Width - Theme.S(40), Theme.S(3));
                }
            }
        }

        private void OpenTest(Type formType)
        {
            using (var form = (Form)Activator.CreateInstance(formType))
            {
                form.ShowDialog(this);
            }
        }

        private string GetSystemInfo()
        {
            try
            {
                string pcName = Environment.MachineName;
                string os = Environment.OSVersion.ToString();
                string cpu = "N/A";
                try
                {
                    using (var searcher = new ManagementObjectSearcher("SELECT Name FROM Win32_Processor"))
                    {
                        foreach (ManagementObject obj in searcher.Get())
                        {
                            cpu = obj["Name"]?.ToString()?.Trim() ?? "N/A";
                            break;
                        }
                    }
                }
                catch { }
                long ramMB = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / (1024 * 1024);
                return $"💻 {pcName}  |  🖥 {os}  |  ⚡ {cpu}  |  🧠 {ramMB:N0} MB RAM";
            }
            catch
            {
                return "Informações do sistema indisponíveis";
            }
        }

        private async void LoadSystemInfoAsync()
        {
            string info = await Task.Run(() => GetSystemInfo());
            try
            {
                this.Invoke((Action)(() => _lblSysInfo.Text = info));
            }
            catch { }
        }

        private void LaunchFurMark()
        {
            try
            {
                // Look for FurMark in the same directory as the executable
                string appDir = AppDomain.CurrentDomain.BaseDirectory;
                string furmarkPath = Path.Combine(appDir, "FurMark.exe");

                // Also check common alternative names
                if (!File.Exists(furmarkPath))
                    furmarkPath = Path.Combine(appDir, "furmark.exe");
                if (!File.Exists(furmarkPath))
                    furmarkPath = Path.Combine(appDir, "FurMark_GUI.exe");

                if (File.Exists(furmarkPath))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = furmarkPath,
                        UseShellExecute = true,
                        WorkingDirectory = appDir
                    });
                }
                else
                {
                    MessageBox.Show(
                        $"FurMark não encontrado na pasta do programa.\n\n" +
                        $"Coloque o arquivo 'FurMark.exe' na mesma pasta do TechTest:\n" +
                        $"{appDir}",
                        "FurMark não encontrado",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro ao abrir FurMark:\n{ex.Message}",
                    "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
