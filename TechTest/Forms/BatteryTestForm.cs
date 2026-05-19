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
        private Panel _batteryPanel;
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
                Location = new Point(Theme.S(30), Theme.S(90)),
                Size = new Size(Theme.S(280), Theme.S(340)),
                BackColor = Color.Transparent
            };
            typeof(Panel).GetProperty("DoubleBuffered",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                ?.SetValue(_batteryPanel, true);
            _batteryPanel.Paint += BatteryPanel_Paint;
            Controls.Add(_batteryPanel);

            // Info section
            int infoX = Theme.S(340), infoY = Theme.S(95);

            Controls.Add(Theme.CreateLabel("Status", infoX, infoY, Theme.FontButton));
            infoY += Theme.S(30);

            _lblPlugged = CreateInfoRow("Fonte de energia:", "—", infoX, ref infoY);
            _lblStatus = CreateInfoRow("Status:", "—", infoX, ref infoY);
            _lblChargePercent = CreateInfoRow("Nível de carga:", "—", infoX, ref infoY);
            _lblTimeLeft = CreateInfoRow("Tempo restante:", "—", infoX, ref infoY);

            infoY += Theme.S(15);
            Controls.Add(Theme.CreateLabel("Saúde da Bateria (powercfg)", infoX, infoY, Theme.FontButton));
            infoY += Theme.S(30);

            _lblDesignCap = CreateInfoRow("Capacidade projetada:", "—", infoX, ref infoY);
            _lblFullCap = CreateInfoRow("Capacidade atual:", "—", infoX, ref infoY);
            _lblHealth = CreateInfoRow("Saúde:", "—", infoX, ref infoY);
            _lblCycles = CreateInfoRow("Ciclos:", "—", infoX, ref infoY);
            _lblManufacturer = CreateInfoRow("Fabricante:", "—", infoX, ref infoY);

            infoY += Theme.S(10);
            _lblReportStatus = Theme.CreateLabel("⏳ Gerando relatório...", infoX, infoY, Theme.FontSmall, Theme.TextMuted);
            Controls.Add(_lblReportStatus);

            _btnReport = Theme.CreateSecondaryButton("📄 Abrir Relatório", infoX + Theme.S(250), infoY - Theme.S(5), 160, 32);
            _btnReport.Enabled = false;
            _btnReport.Click += (s, e) =>
            {
                if (_reportPath != null && File.Exists(_reportPath))
                {
                    Process.Start(new ProcessStartInfo(_reportPath) { UseShellExecute = true });
                }
            };
            Controls.Add(_btnReport);
        }

        private Label CreateInfoRow(string label, string value, int x, ref int y)
        {
            Controls.Add(Theme.CreateLabel(label, x, y, Theme.FontSmall, Theme.TextSecondary));
            var lblValue = Theme.CreateLabel(value, x + Theme.S(180), y, Theme.FontBody, Theme.TextPrimary);
            Controls.Add(lblValue);
            y += Theme.S(28);
            return lblValue;
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
                    _lblDesignCap.Text = _reportDesignCap;
                    _lblFullCap.Text = _reportFullCap;
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
                        proc?.WaitForExit(10000);
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

            // Battery outline
            int batW = 140, batH = 200;
            int batX = (rect.Width - batW) / 2;
            int batY = (rect.Height - batH) / 2 - 10;

            // Battery terminal (top nub)
            var nubRect = new Rectangle(batX + batW / 2 - 20, batY - 12, 40, 16);
            using (var path = Theme.RoundedRect(nubRect, 5))
            using (var brush = new SolidBrush(Theme.Border))
                g.FillPath(brush, path);

            // Battery body
            var bodyRect = new Rectangle(batX, batY, batW, batH);
            using (var path = Theme.RoundedRect(bodyRect, 12))
            {
                using (var brush = new SolidBrush(Color.FromArgb(25, 25, 50)))
                    g.FillPath(brush, path);
                using (var pen = new Pen(Theme.Border, 3f))
                    g.DrawPath(pen, path);
            }

            // Fill level
            int fillH = (int)(batH * pct / 100.0) - 8;
            if (fillH > 0)
            {
                int fillY = batY + batH - fillH - 4;
                var fillRect = new Rectangle(batX + 4, fillY, batW - 8, fillH);
                Color fillColor = pct > 50 ? Theme.Success : pct > 20 ? Theme.Warning : Theme.Error;

                using (var path = Theme.RoundedRect(fillRect, 8))
                using (var brush = new LinearGradientBrush(fillRect,
                    Color.FromArgb(200, fillColor), fillColor, 90f))
                {
                    g.FillPath(brush, path);
                }
            }

            // Percentage text in center
            using (var font = new Font("Segoe UI", 28, FontStyle.Bold))
            {
                string pctText = $"{pct}%";
                var sz = g.MeasureString(pctText, font);
                float tx = batX + (batW - sz.Width) / 2;
                float ty = batY + (batH - sz.Height) / 2;
                g.DrawString(pctText, font, new SolidBrush(Theme.TextPrimary), tx, ty);
            }

            // Charging icon
            if (ps.PowerLineStatus == PowerLineStatus.Online)
            {
                using (var font = new Font("Segoe UI Emoji", 18))
                {
                    g.DrawString("⚡", font, Brushes.Yellow, batX + batW / 2 - 14, batY + batH + 8);
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
