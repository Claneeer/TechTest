using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Management;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using TechTest.Helpers;

namespace TechTest.Forms
{
    public class BatteryTestForm : Form
    {
        private Panel _batteryPanel, _infoPanel;
        private Label _lblChargePercent, _lblStatus, _lblTimeLeft;
        private Label _lblDesignCap, _lblFullCap, _lblHealth, _lblCycles;
        private Label _lblManufacturer, _lblPlugged;
        private Label _lblReportStatus;
        private Button _btnReport;
        private System.Windows.Forms.Timer _updateTimer;

        // Data from powercfg
        private string _reportDesignCap = "—";
        private string _reportFullCap = "—";
        private string _reportCycles = "—";
        private string _reportHealth = "—";
        private string _reportManufacturer = "—";
        private string _reportPath;
        private bool _reportLoaded = false;

        public BatteryTestForm()
        {
            Theme.StyleForm(this, "🔋 Teste de Bateria", 750, 560);
            
            // Allow manual resizing and maximizing
            this.FormBorderStyle = FormBorderStyle.Sizable;
            this.MaximizeBox = true;
            this.MinimumSize = this.Size;

            BuildUI();
            UpdateBatteryInfo();
            LoadBatteryReport();

            _updateTimer = new System.Windows.Forms.Timer { Interval = 2000 };
            _updateTimer.Tick += (s, e) => UpdateBatteryInfo();
            _updateTimer.Start();
        }

        private void BuildUI()
        {
            var lblTitle = Theme.CreateLabel("Teste de Bateria", 30, 15, Theme.FontHeader);
            Controls.Add(lblTitle);

            var lblDesc = Theme.CreateLabel(
                "Informações sobre a saúde e status da bateria do notebook.",
                30, 48, Theme.FontSmall, Theme.TextSecondary);
            Controls.Add(lblDesc);

            // Battery visual panel
            _batteryPanel = new Panel
            {
                BackColor = Color.Transparent
            };
            typeof(Panel).GetProperty("DoubleBuffered",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                ?.SetValue(_batteryPanel, true);
            _batteryPanel.Paint += BatteryPanel_Paint;
            _batteryPanel.Resize += (s, e) => _batteryPanel.Invalidate();
            Controls.Add(_batteryPanel);

            // Info panel
            _infoPanel = new Panel
            {
                BackColor = Color.Transparent,
                AutoScroll = true
            };
            Controls.Add(_infoPanel);

            // Build Info controls inside _infoPanel using unscaled relative coordinates
            int infoX = 0;
            int infoY = 5;

            _infoPanel.Controls.Add(Theme.CreateLabel("Status", infoX, infoY, Theme.FontButton));
            infoY += 30;

            _lblPlugged = CreateInfoRow(_infoPanel, "Fonte de energia:", "—", infoX, ref infoY);
            _lblStatus = CreateInfoRow(_infoPanel, "Status:", "—", infoX, ref infoY);
            _lblChargePercent = CreateInfoRow(_infoPanel, "Nível de carga:", "—", infoX, ref infoY);
            _lblTimeLeft = CreateInfoRow(_infoPanel, "Tempo restante:", "—", infoX, ref infoY);

            infoY += 15;
            _infoPanel.Controls.Add(Theme.CreateLabel("Saúde da Bateria (powercfg)", infoX, infoY, Theme.FontButton));
            infoY += 30;

            _lblDesignCap = CreateInfoRow(_infoPanel, "Capacidade projetada:", "—", infoX, ref infoY);
            _lblFullCap = CreateInfoRow(_infoPanel, "Capacidade atual:", "—", infoX, ref infoY);
            _lblHealth = CreateInfoRow(_infoPanel, "Saúde:", "—", infoX, ref infoY);
            _lblCycles = CreateInfoRow(_infoPanel, "Ciclos:", "—", infoX, ref infoY);
            _lblManufacturer = CreateInfoRow(_infoPanel, "Fabricante:", "—", infoX, ref infoY);

            infoY += 10;
            _lblReportStatus = Theme.CreateLabel("⏳ Gerando relatório...", infoX, infoY, Theme.FontSmall, Theme.TextMuted);
            _infoPanel.Controls.Add(_lblReportStatus);

            _btnReport = Theme.CreateSecondaryButton("📄 Abrir Relatório", infoX + 220, infoY - 5, 150, 32);
            _btnReport.Enabled = false;
            _btnReport.Click += (s, e) =>
            {
                if (_reportPath != null && File.Exists(_reportPath))
                {
                    Process.Start(new ProcessStartInfo(_reportPath) { UseShellExecute = true });
                }
            };
            _infoPanel.Controls.Add(_btnReport);

            // Execute initial layout placement
            OnResize(EventArgs.Empty);
        }

        private Label CreateInfoRow(Panel parent, string label, string value, int x, ref int y)
        {
            parent.Controls.Add(Theme.CreateLabel(label, x, y, Theme.FontSmall, Theme.TextSecondary));
            var lblValue = Theme.CreateLabel(value, x + 160, y, Theme.FontBody, Theme.TextPrimary);
            lblValue.MaximumSize = new Size(Theme.S(250), 0);
            parent.Controls.Add(lblValue);
            y += 28;
            return lblValue;
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (_batteryPanel == null || _infoPanel == null) return;

            int padX = Theme.S(30);
            int padY = Theme.S(90);

            int availW = this.ClientSize.Width - padX * 3;
            int availH = this.ClientSize.Height - padY - Theme.S(30);

            if (availW < Theme.S(200)) availW = Theme.S(200);
            if (availH < Theme.S(200)) availH = Theme.S(200);

            // Allocate 40% of horizontal space to the visual battery panel, and 60% to the details panel
            int batW = (int)(availW * 0.40f);
            int infoW = availW - batW;

            _batteryPanel.Location = new Point(padX, padY);
            _batteryPanel.Size = new Size(batW, availH);

            _infoPanel.Location = new Point(padX * 2 + batW, padY);
            _infoPanel.Size = new Size(infoW, availH);
        }

        private void UpdateBatteryInfo()
        {
            try
            {
                var ps = SystemInformation.PowerStatus;

                // Charge
                float charge = ps.BatteryLifePercent;
                int pct = (int)(charge * 100);
                if (pct > 100) pct = 100;
                if (pct < 0) pct = 0;
                _lblChargePercent.Text = $"{pct}%";
                _lblChargePercent.ForeColor = pct > 50 ? Theme.Success : pct > 20 ? Theme.Warning : Theme.Error;

                // Status
                string status;
                switch (ps.BatteryChargeStatus)
                {
                    case BatteryChargeStatus.Charging:
                        status = "⚡ Carregando";
                        break;
                    case BatteryChargeStatus.NoSystemBattery:
                        status = "❌ Sem bateria";
                        break;
                    case BatteryChargeStatus.High:
                        status = "🟢 Alta";
                        break;
                    case BatteryChargeStatus.Low:
                        status = "🟡 Baixa";
                        break;
                    case BatteryChargeStatus.Critical:
                        status = "🔴 Crítica";
                        break;
                    default:
                        status = "🔋 Normal";
                        break;
                }
                _lblStatus.Text = status;

                // Plugged in
                _lblPlugged.Text = ps.PowerLineStatus == PowerLineStatus.Online ? "🔌 Na tomada" : "🔋 Bateria";

                // Time remaining
                int seconds = ps.BatteryLifeRemaining;
                if (seconds > 0)
                {
                    var ts = TimeSpan.FromSeconds(seconds);
                    _lblTimeLeft.Text = $"{(int)ts.TotalHours}h {ts.Minutes}min";
                }
                else if (ps.PowerLineStatus == PowerLineStatus.Online)
                {
                    _lblTimeLeft.Text = "Carregando...";
                }
                else
                {
                    _lblTimeLeft.Text = "Calculando...";
                }

                // Update powercfg data if loaded
                if (_reportLoaded)
                {
                    _lblDesignCap.Text = $"{_reportDesignCap} (100%)";
                    _lblFullCap.Text = $"{_reportFullCap} ({_reportHealth})";
                    _lblHealth.Text = _reportHealth;
                    _lblCycles.Text = _reportCycles;
                    _lblManufacturer.Text = _reportManufacturer;
                }
            }
            catch (Exception ex)
            {
                _lblStatus.Text = $"Erro: {ex.Message}";
            }

            _batteryPanel.Invalidate();
        }

        private async void LoadBatteryReport()
        {
            try
            {
                string tempDir = Path.Combine(Path.GetTempPath(), "TechTest");
                Directory.CreateDirectory(tempDir);
                _reportPath = Path.Combine(tempDir, "battery-report.html");

                // Run powercfg in background
                await Task.Run(() =>
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = "powercfg",
                        Arguments = $"/batteryreport /output \"{_reportPath}\"",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    };
                    using (var proc = Process.Start(psi))
                    {
                        proc?.WaitForExit(); // Aguarda sem timeout até completar
                    }
                });

                // Parse the HTML report
                if (File.Exists(_reportPath))
                {
                    string html = await Task.Run(() => File.ReadAllText(_reportPath));
                    ParseBatteryReport(html);
                    _reportLoaded = true;

                    this.Invoke((Action)(() =>
                    {
                        _lblReportStatus.Text = "✅ Relatório gerado com sucesso";
                        _lblReportStatus.ForeColor = Theme.Success;
                        _btnReport.Enabled = true;
                        UpdateBatteryInfo();
                    }));
                }
                else
                {
                    this.Invoke((Action)(() =>
                    {
                        _lblReportStatus.Text = "❌ Falha ao gerar relatório";
                        _lblReportStatus.ForeColor = Theme.Error;
                    }));
                }
            }
            catch (Exception ex)
            {
                try
                {
                    this.Invoke((Action)(() =>
                    {
                        _lblReportStatus.Text = $"❌ Erro: {ex.Message}";
                        _lblReportStatus.ForeColor = Theme.Error;
                    }));
                }
                catch { }
            }
        }

        private void ParseBatteryReport(string html)
        {
            try
            {
                // Extract DESIGN CAPACITY
                var designMatch = Regex.Match(html, @"DESIGN\s+CAPACITY.*?(\d[\d,\.]+)\s*mWh", RegexOptions.IgnoreCase | RegexOptions.Singleline);
                if (designMatch.Success)
                    _reportDesignCap = designMatch.Groups[1].Value.Replace(",", ".") + " mWh";

                // Extract FULL CHARGE CAPACITY
                var fullMatch = Regex.Match(html, @"FULL\s+CHARGE\s+CAPACITY.*?(\d[\d,\.]+)\s*mWh", RegexOptions.IgnoreCase | RegexOptions.Singleline);
                if (fullMatch.Success)
                    _reportFullCap = fullMatch.Groups[1].Value.Replace(",", ".") + " mWh";

                // Calculate health
                if (designMatch.Success && fullMatch.Success)
                {
                    double design = double.Parse(designMatch.Groups[1].Value.Replace(",", "").Replace(".", ""));
                    double full = double.Parse(fullMatch.Groups[1].Value.Replace(",", "").Replace(".", ""));
                    if (design > 0)
                    {
                        double health = full / design * 100;
                        _reportHealth = $"{health:F1}%";
                    }
                }

                // Extract CYCLE COUNT
                var cycleMatch = Regex.Match(html, @"CYCLE\s+COUNT.*?(\d+)", RegexOptions.IgnoreCase | RegexOptions.Singleline);
                if (cycleMatch.Success)
                    _reportCycles = cycleMatch.Groups[1].Value;

                // Extract manufacturer
                var mfgMatch = Regex.Match(html, @"MANUFACTURER.*?<td[^>]*>\s*([^<]+?)\s*</td>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
                if (mfgMatch.Success)
                {
                    string mfg = mfgMatch.Groups[1].Value.Trim();
                    if (!string.IsNullOrWhiteSpace(mfg) && mfg != "&nbsp;")
                        _reportManufacturer = mfg;
                }
            }
            catch { }
        }

        private void BatteryPanel_Paint(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var rect = _batteryPanel.ClientRectangle;

            // Draw background
            using (var path = Theme.RoundedRect(rect, 12))
            using (var brush = new SolidBrush(Theme.BgCard))
                g.FillPath(brush, path);

            var ps = SystemInformation.PowerStatus;
            int pct = Math.Max(0, Math.Min(100, (int)(ps.BatteryLifePercent * 100)));

            // Calculate responsive bounds keeping a nice aspect ratio (approx 0.65)
            float padX = rect.Width * 0.15f;
            float padY = rect.Height * 0.15f;
            float availW = rect.Width - 2 * padX;
            float availH = rect.Height - 2 * padY;

            float aspect = 0.65f;
            float batW_f, batH_f;
            if (availW / availH > aspect)
            {
                batH_f = availH;
                batW_f = batH_f * aspect;
            }
            else
            {
                batW_f = availW;
                batH_f = batW_f / aspect;
            }

            int batW = (int)batW_f;
            int batH = (int)batH_f;

            // Center the battery body horizontally and vertically
            int batX = (rect.Width - batW) / 2;
            int batY = (rect.Height - batH) / 2 + (int)(batH * 0.02f);

            // Battery terminal (top nub) - scaled proportionally
            int nubW = (int)(batW * 0.3f);
            int nubH = (int)(batH * 0.08f);
            var nubRect = new Rectangle(batX + (batW - nubW) / 2, batY - nubH, nubW, nubH);
            int nubR = Math.Max(2, (int)(nubW * 0.15f));
            using (var path = Theme.RoundedRect(nubRect, nubR))
            using (var brush = new SolidBrush(Theme.Border))
                g.FillPath(brush, path);

            // Battery body - scaled
            var bodyRect = new Rectangle(batX, batY, batW, batH);
            int bodyR = Math.Max(4, (int)(batW * 0.08f));
            float penWidth = Math.Max(1.5f, batW * 0.025f);
            using (var path = Theme.RoundedRect(bodyRect, bodyR))
            {
                using (var brush = new SolidBrush(Color.FromArgb(25, 25, 50)))
                    g.FillPath(brush, path);
                using (var pen = new Pen(Theme.Border, penWidth))
                    g.DrawPath(pen, path);
            }

            // Fill level - scaled inside the battery borders
            int fillPadding = Math.Max(2, (int)(batW * 0.04f));
            int maxFillH = batH - 2 * fillPadding;
            int fillH = (int)(maxFillH * pct / 100.0);
            if (fillH > 0)
            {
                int fillY = batY + batH - fillPadding - fillH;
                var fillRect = new Rectangle(batX + fillPadding, fillY, batW - 2 * fillPadding, fillH);
                Color fillColor = pct > 50 ? Theme.Success : pct > 20 ? Theme.Warning : Theme.Error;
                int fillR = Math.Max(2, (int)((batW - 2 * fillPadding) * 0.08f));

                using (var path = Theme.RoundedRect(fillRect, fillR))
                using (var brush = new LinearGradientBrush(fillRect,
                    Color.FromArgb(200, fillColor), fillColor, 90f))
                {
                    g.FillPath(brush, path);
                }
            }

            // Percentage text in center — dynamically scaled font
            float fontSize = Math.Max(8f, batW * 0.20f);
            using (var font = new Font("Segoe UI", fontSize, FontStyle.Bold))
            {
                string pctText = $"{pct}%";
                var sz = g.MeasureString(pctText, font);
                float tx = batX + (batW - sz.Width) / 2;
                float ty = batY + (batH - sz.Height) / 2;
                g.DrawString(pctText, font, new SolidBrush(Theme.TextPrimary), tx, ty);
            }

            // Charging icon — dynamically scaled below the battery
            if (ps.PowerLineStatus == PowerLineStatus.Online)
            {
                float boltFontSize = Math.Max(10f, batW * 0.16f);
                using (var boltFont = new Font("Segoe UI Emoji", boltFontSize))
                {
                    string boltText = "⚡";
                    var boltSz = g.MeasureString(boltText, boltFont);
                    float bx = batX + (batW - boltSz.Width) / 2;
                    float by = batY + batH + Math.Max(2f, batH * 0.03f);
                    g.DrawString(boltText, boltFont, Brushes.Yellow, bx, by);
                }
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _updateTimer?.Stop();
            _updateTimer?.Dispose();
            base.OnFormClosing(e);
        }
    }
}
