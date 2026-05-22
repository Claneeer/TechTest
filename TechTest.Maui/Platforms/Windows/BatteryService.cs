#if WINDOWS
using System;
using System.IO;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using BatteryPower = Microsoft.Maui.Devices.Battery;

namespace TechTest.Maui.Services;

public class BatteryService : IBatteryService
{
    public BatteryStatus GetCurrentStatus()
    {
        var status = new BatteryStatus();

        try
        {
            double chargeLevel = BatteryPower.Default.ChargeLevel;
            status.ChargePercent = (int)Math.Round(chargeLevel * 100);

            var batteryState = BatteryPower.Default.State;
            status.IsCharging = batteryState == BatteryState.Charging;

            var powerSource = BatteryPower.Default.PowerSource;
            status.PowerSource = powerSource switch
            {
                BatteryPowerSource.AC => "Energia AC",
                BatteryPowerSource.Battery => "Bateria",
                BatteryPowerSource.Usb => "USB",
                _ => "Desconhecido"
            };

            status.StatusText = batteryState switch
            {
                BatteryState.Charging => "Carregando",
                BatteryState.Discharging => "Descarregando",
                BatteryState.Full => "Completa",
                BatteryState.NotCharging => "Não carregando",
                BatteryState.NotPresent => "Não presente",
                _ => "Desconhecido"
            };

            status.TimeRemaining = EstimateTimeRemaining(status);
        }
        catch
        {
            status.ChargePercent = 0;
            status.PowerSource = "Desconhecido";
            status.StatusText = "Erro ao obter status";
            status.TimeRemaining = "--:--";
        }

        return status;
    }

    private string EstimateTimeRemaining(BatteryStatus status)
    {
        if (status.IsCharging)
            return "Calculando...";

        if (status.ChargePercent >= 100)
            return "Completa";

        return "Estimando...";
    }

    public async Task<BatteryReport> GenerateReportAsync()
    {
        var report = new BatteryReport();

        try
        {
            string reportPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "TechTest", "battery-report.html");

            string? reportDir = Path.GetDirectoryName(reportPath);
            if (!string.IsNullOrEmpty(reportDir) && !Directory.Exists(reportDir))
            {
                Directory.CreateDirectory(reportDir);
            }

            if (File.Exists(reportPath))
            {
                try { File.Delete(reportPath); } catch { }
            }

            var processInfo = new ProcessStartInfo
            {
                FileName = "powercfg",
                Arguments = $"/batteryreport /output \"{reportPath}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                Verb = ""
            };

            using var process = new Process { StartInfo = processInfo };
            process.Start();

            await process.WaitForExitAsync();

            if (File.Exists(reportPath))
            {
                report.ReportFilePath = reportPath;
                report.IsReportReady = true;

                string htmlContent = await File.ReadAllTextAsync(reportPath);
                ParseBatteryReport(htmlContent, report);
            }
            else
            {
                report.IsReportReady = false;
                report.ReportFilePath = "";
            }
        }
        catch
        {
            report.IsReportReady = false;
        }

        return report;
    }

    private void ParseBatteryReport(string html, BatteryReport report)
    {
        try
        {
            var designMatch = Regex.Match(html,
                @"DESIGN\s*CAPACITY.*?(\d[\d,\.]*)\s*mWh",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);

            if (designMatch.Success)
            {
                string value = designMatch.Groups[1].Value.Replace(",", "");
                report.DesignCapacity = $"{value} mWh";
            }
            else
            {
                report.DesignCapacity = "Não disponível";
            }

            var fullChargeMatch = Regex.Match(html,
                @"FULL\s*CHARGE\s*CAPACITY.*?(\d[\d,\.]*)\s*mWh",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);

            if (fullChargeMatch.Success)
            {
                string value = fullChargeMatch.Groups[1].Value.Replace(",", "");
                report.FullChargeCapacity = $"{value} mWh";
            }
            else
            {
                report.FullChargeCapacity = "Não disponível";
            }

            if (designMatch.Success && fullChargeMatch.Success)
            {
                string designStr = designMatch.Groups[1].Value.Replace(",", "").Replace(".", "");
                string fullStr = fullChargeMatch.Groups[1].Value.Replace(",", "").Replace(".", "");

                if (double.TryParse(designStr, out double design) &&
                    double.TryParse(fullStr, out double full) && design > 0)
                {
                    double health = (full / design) * 100.0;
                    report.Health = $"{health:F1}%";
                }
                else
                {
                    report.Health = "Não calculável";
                }
            }
            else
            {
                report.Health = "Não disponível";
            }

            var cycleMatch = Regex.Match(html,
                @"CYCLE\s*COUNT.*?(\d+)",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);

            if (cycleMatch.Success)
            {
                report.CycleCount = cycleMatch.Groups[1].Value;
            }
            else
            {
                report.CycleCount = "Não disponível";
            }

            var manufacturerMatch = Regex.Match(html,
                @"MANUFACTURER.*?<td[^>]*>(.*?)</td>",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);

            if (manufacturerMatch.Success)
            {
                string manufacturer = manufacturerMatch.Groups[1].Value.Trim();
                manufacturer = Regex.Replace(manufacturer, @"<[^>]+>", "").Trim();
                report.Manufacturer = string.IsNullOrWhiteSpace(manufacturer) ? "Não disponível" : manufacturer;
            }
            else
            {
                report.Manufacturer = "Não disponível";
            }
        }
        catch
        {
            if (string.IsNullOrEmpty(report.DesignCapacity)) report.DesignCapacity = "Erro na leitura";
            if (string.IsNullOrEmpty(report.FullChargeCapacity)) report.FullChargeCapacity = "Erro na leitura";
            if (string.IsNullOrEmpty(report.Health)) report.Health = "Erro na leitura";
            if (string.IsNullOrEmpty(report.CycleCount)) report.CycleCount = "Erro na leitura";
            if (string.IsNullOrEmpty(report.Manufacturer)) report.Manufacturer = "Erro na leitura";
        }
    }
}
#endif
