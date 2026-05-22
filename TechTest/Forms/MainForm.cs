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
            ("💻", "Especificações", "Hardware e Service Tag (SA/ST)", (Type)null),
        };

        private Panel[] _cardPanels;
        private int _hoveredIndex = -1;
        private Label _lblSysInfo;
        private HardwareSpecs _specs = new HardwareSpecs();

        public MainForm()
        {
            Theme.StyleForm(this, "TechTest Notebook — Diagnóstico de Hardware", 1060, 720);
            this.FormBorderStyle = FormBorderStyle.Sizable;
            this.MaximizeBox = true;
            this.MinimumSize = this.Size;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            BuildUI();
            LoadHardwareSpecsAsync();
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
                    else if (index == 7)
                        OpenSpecsForm();
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

            var spacer = new Label
            {
                Width = Theme.S(25),
                Dock = DockStyle.Right,
                BackColor = Color.Transparent
            };

            var btnFurmark = new Button
            {
                Text = "🔥 FurMark Stress",
                Width = Theme.S(145),
                Dock = DockStyle.Right,
                FlatStyle = FlatStyle.Flat,
                BackColor = Theme.BgCard,
                ForeColor = Theme.TextPrimary,
                Font = Theme.FontSmall,
                Cursor = Cursors.Hand
            };
            btnFurmark.FlatAppearance.BorderSize = 1;
            btnFurmark.FlatAppearance.BorderColor = Theme.Border;
            btnFurmark.FlatAppearance.MouseOverBackColor = Theme.BgCardHover;
            btnFurmark.Click += (s, e) => LaunchFurMark();

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

            // Order of adding controls determines docking layout (first added with Dock = Right is rightmost)
            bottomBar.Controls.Add(cmbScale);
            bottomBar.Controls.Add(lblScaleTitle);
            bottomBar.Controls.Add(spacer);
            bottomBar.Controls.Add(btnFurmark);
            bottomBar.Controls.Add(_lblSysInfo);

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

            // Reset MinimumSize to Size.Empty so the window can shrink when scaling down
            this.MinimumSize = Size.Empty;

            // Reapply styling using the new ScaleFactor!
            Theme.StyleForm(this, "TechTest Notebook — Diagnóstico de Hardware", 1060, 720);

            // Restore sizable/maximizable properties that StyleForm overwrites
            this.FormBorderStyle = FormBorderStyle.Sizable;
            this.MaximizeBox = true;
            this.MinimumSize = this.Size;

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

            if (index == 7)
            {
                // Draw a beautiful "Especificações" card
                using (var titleFont = Theme.FontCardTitle)
                {
                    var titleSize = g.MeasureString("Especificações", titleFont);
                    float titleX = (rect.Width - titleSize.Width) / 2;
                    g.DrawString("Especificações", titleFont, new SolidBrush(Theme.TextPrimary), titleX, Theme.S(12));
                }

                // Draw each detail row inside the card
                int startY = Theme.S(38);
                int lineH = Theme.S(24);

                DrawSpecRow(g, "💻 Empresa:", _specs.Manufacturer, startY, rect.Width); startY += lineH;
                DrawSpecRow(g, "⚡ CPU:", _specs.CPU, startY, rect.Width); startY += lineH;
                DrawSpecRow(g, "🧠 RAM:", _specs.RAM, startY, rect.Width); startY += lineH;
                DrawSpecRow(g, "🎮 GPU:", _specs.GPU, startY, rect.Width); startY += lineH;
                DrawSpecRow(g, "🏷️ SA/ST:", _specs.ServiceTag, startY, rect.Width);

                // Bottom hint on hover
                if (hovered)
                {
                    using (var hintFont = Theme.FontSmall)
                    {
                        string hint = "Clique para copiar tudo";
                        var hintSize = g.MeasureString(hint, hintFont);
                        float hintX = (rect.Width - hintSize.Width) / 2;
                        g.DrawString(hint, hintFont, new SolidBrush(Theme.AccentLight), hintX, rect.Height - Theme.S(22));
                    }
                }
                return;
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

        private void OpenSpecsForm()
        {
            using (var form = new SpecsForm())
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

        private async void LoadHardwareSpecsAsync()
        {
            await Task.Run(() =>
            {
                try
                {
                    // 1. CPU
                    using (var searcher = new ManagementObjectSearcher("SELECT Name FROM Win32_Processor"))
                    {
                        foreach (ManagementObject obj in searcher.Get())
                        {
                            _specs.CPU = CleanCpuName(obj["Name"]?.ToString() ?? "N/A");
                            break;
                        }
                    }

                    // 2. Manufacturer / Model (Empresa)
                    using (var searcher = new ManagementObjectSearcher("SELECT Manufacturer, Model FROM Win32_ComputerSystem"))
                    {
                        foreach (ManagementObject obj in searcher.Get())
                        {
                            string manufacturer = obj["Manufacturer"]?.ToString() ?? "N/A";
                            string model = obj["Model"]?.ToString() ?? "N/A";
                            _specs.Manufacturer = CleanManufacturerName(manufacturer, model);
                            break;
                        }
                    }

                    // 3. RAM
                    long ramBytes = 0;
                    using (var searcher = new ManagementObjectSearcher("SELECT Capacity FROM Win32_PhysicalMemory"))
                    {
                        foreach (ManagementObject obj in searcher.Get())
                        {
                            ramBytes += Convert.ToInt64(obj["Capacity"] ?? 0);
                        }
                    }
                    if (ramBytes > 0)
                    {
                        double ramGb = ramBytes / (1024.0 * 1024.0 * 1024.0);
                        _specs.RAM = $"{Math.Round(ramGb)} GB";
                    }
                    else
                    {
                        // Fallback
                        long ramMB = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / (1024 * 1024);
                        _specs.RAM = $"{Math.Round(ramMB / 1024.0)} GB";
                    }

                    // 4. GPU (Video Controller)
                    using (var searcher = new ManagementObjectSearcher("SELECT Name FROM Win32_VideoController"))
                    {
                        foreach (ManagementObject obj in searcher.Get())
                        {
                            _specs.GPU = CleanGpuName(obj["Name"]?.ToString() ?? "N/A");
                            break;
                        }
                    }

                    // 5. Service Tag (SA/ST) / Serial Number
                    using (var searcher = new ManagementObjectSearcher("SELECT SerialNumber FROM Win32_Bios"))
                    {
                        foreach (ManagementObject obj in searcher.Get())
                        {
                            _specs.ServiceTag = obj["SerialNumber"]?.ToString()?.Trim() ?? "N/A";
                            break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Erro ao carregar especificações de hardware: {ex.Message}");
                }
            });

            // Refresh UI once loaded
            try
            {
                if (!this.IsDisposed && this.IsHandleCreated)
                {
                    this.Invoke((Action)(() =>
                    {
                        if (_cardPanels != null && _cardPanels.Length > 7 && _cardPanels[7] != null)
                        {
                            _cardPanels[7].Invalidate();
                        }
                    }));
                }
            }
            catch { }
        }

        private string CleanCpuName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "N/A";
            name = name.Replace("(R)", "").Replace("(TM)", "").Replace("CPU", "").Trim();
            name = name.Replace("Intel ", "").Replace("AMD ", "");
            int indexAt = name.IndexOf('@');
            if (indexAt > 0) name = name.Substring(0, indexAt).Trim();
            return name;
        }

        private string CleanManufacturerName(string manufacturer, string model)
        {
            if (string.IsNullOrEmpty(manufacturer) || manufacturer == "N/A") return model;
            string m = manufacturer.ToUpper();
            if (m.Contains("DELL")) manufacturer = "Dell";
            else if (m.Contains("LENOVO")) manufacturer = "Lenovo";
            else if (m.Contains("HP") || m.Contains("HEWLETT-PACKARD")) manufacturer = "HP";
            else if (m.Contains("ASUS")) manufacturer = "ASUS";
            else if (m.Contains("ACER")) manufacturer = "Acer";
            else if (m.Contains("APPLE")) manufacturer = "Apple";
            else if (m.Contains("SAMSUNG")) manufacturer = "Samsung";
            else if (m.Contains("POSITIVO")) manufacturer = "Positivo";
            else if (m.Contains("GIGABYTE")) manufacturer = "Gigabyte";
            else if (m.Contains("MSI")) manufacturer = "MSI";

            if (!string.IsNullOrEmpty(model) && model != "N/A")
            {
                if (model.ToUpper().StartsWith(manufacturer.ToUpper()))
                {
                    model = model.Substring(manufacturer.Length).Trim();
                }
                return $"{manufacturer} {model}";
            }
            return manufacturer;
        }

        private string CleanGpuName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "N/A";
            name = name.Replace("(R)", "").Replace("(TM)", "").Replace("Graphics", "Graph.").Trim();
            name = name.Replace("Intel ", "").Replace("NVIDIA ", "").Replace("AMD ", "");
            return name;
        }

        private void DrawSpecRow(Graphics g, string label, string value, int y, int cardWidth)
        {
            using (var labelFont = Theme.FontSmall)
            using (var valueFont = Theme.FontSmall)
            {
                g.DrawString(label, labelFont, new SolidBrush(Theme.TextSecondary), Theme.S(15), y);

                var labelSize = g.MeasureString(label, labelFont);
                float valueX = Theme.S(15) + labelSize.Width + Theme.S(5);
                float availableWidth = cardWidth - valueX - Theme.S(15);

                string truncatedValue = value;
                var valueSize = g.MeasureString(truncatedValue, valueFont);
                if (valueSize.Width > availableWidth)
                {
                    while (truncatedValue.Length > 3 && g.MeasureString(truncatedValue + "...", valueFont).Width > availableWidth)
                    {
                        truncatedValue = truncatedValue.Substring(0, truncatedValue.Length - 1);
                    }
                    truncatedValue += "...";
                }

                g.DrawString(truncatedValue, valueFont, new SolidBrush(Theme.TextPrimary), valueX, y);
            }
        }

        private void CopySpecsToClipboard()
        {
            try
            {
                string text = $"--- INFORMAÇÕES DO HARDWARE ---\n" +
                              $"Empresa/Modelo: {_specs.Manufacturer}\n" +
                              $"Processador: {_specs.CPU}\n" +
                              $"Memória RAM: {_specs.RAM}\n" +
                              $"Placa de Vídeo: {_specs.GPU}\n" +
                              $"Service Tag / SA/ST: {_specs.ServiceTag}\n" +
                              $"-------------------------------";
                Clipboard.SetText(text);
                MessageBox.Show(
                    "Informações de hardware copiadas com sucesso para a área de transferência!",
                    "Sucesso",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro ao copiar para a área de transferência:\n{ex.Message}",
                    "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    public class HardwareSpecs
    {
        public string CPU { get; set; } = "Carregando...";
        public string Manufacturer { get; set; } = "Carregando...";
        public string RAM { get; set; } = "Carregando...";
        public string GPU { get; set; } = "Carregando...";
        public string ServiceTag { get; set; } = "Carregando...";
    }
}
