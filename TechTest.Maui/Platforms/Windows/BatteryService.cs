#if WINDOWS
using System;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using BatteryPower = Microsoft.Maui.Devices.Battery;

namespace TechTest.Maui.Services;

public class BatteryService : IBatteryService
{
    public const string DesktopNoBatteryMessage = "Bateria não detectada (Computador de Mesa / Sem Bateria)";

    public BatteryStatus GetCurrentStatus()
    {
        var status = new BatteryStatus();
        bool wmiBatteryDetected = false;

        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT EstimatedChargeRemaining, BatteryStatus, EstimatedRunTime FROM Win32_Battery");

            foreach (ManagementBaseObject obj in searcher.Get())
            {
                using (obj)
                {
                    wmiBatteryDetected = true;

                    if (obj["EstimatedChargeRemaining"] != null &&
                        int.TryParse(obj["EstimatedChargeRemaining"]?.ToString(), out int pct))
                    {
                        status.ChargePercent = Math.Clamp(pct, 0, 100);
                    }

                    int batteryStatus = 0;
                    if (obj["BatteryStatus"] != null)
                        int.TryParse(obj["BatteryStatus"]?.ToString(), out batteryStatus);

                    // Win32_Battery.BatteryStatus:
                    // 1 = Discharging, 2 = AC (Not charging/Full), 3 = Fully Charged, 4 = Low, 5 = Critical,
                    // 6 = Charging, 7 = Charging High, 8 = Charging Low, 9 = Charging Critical
                    status.IsCharging = batteryStatus is 2 or 6 or 7 or 8 or 9;
                    status.PowerSource = batteryStatus is 1 or 4 or 5 ? "Bateria (DC)" : "Energia AC";
                    status.StatusText = batteryStatus switch
                    {
                        1 => "Descarregando",
                        2 => "Conectada à Tomada (AC)",
                        3 => "Completa",
                        4 => "Carga Baixa",
                        5 => "Carga Crítica",
                        6 or 7 or 8 or 9 => "Carregando",
                        _ => "Presente"
                    };

                    break;
                }
            }
        }
        catch
        {
            // Fallback para API MAUI se WMI falhar
        }

        try
        {
            var batteryState = BatteryPower.Default.State;
            var powerSource = BatteryPower.Default.PowerSource;

            if (!wmiBatteryDetected && batteryState == Microsoft.Maui.Devices.BatteryState.NotPresent)
            {
                status.HasBattery = false;
                status.ChargePercent = 0;
                status.IsCharging = false;
                status.PowerSource = "Energia AC (Desktop)";
                status.StatusText = DesktopNoBatteryMessage;
                status.TimeRemaining = "N/A";
                return status;
            }

            if (!wmiBatteryDetected)
            {
                double chargeLevel = BatteryPower.Default.ChargeLevel;
                status.ChargePercent = chargeLevel >= 0 ? (int)Math.Round(chargeLevel * 100) : 0;
                status.IsCharging = batteryState == Microsoft.Maui.Devices.BatteryState.Charging;
                status.PowerSource = powerSource switch
                {
                    Microsoft.Maui.Devices.BatteryPowerSource.AC => "Energia AC",
                    Microsoft.Maui.Devices.BatteryPowerSource.Battery => "Bateria (DC)",
                    Microsoft.Maui.Devices.BatteryPowerSource.Usb => "USB",
                    _ => "Desconhecido"
                };
                status.StatusText = batteryState switch
                {
                    Microsoft.Maui.Devices.BatteryState.Charging => "Carregando",
                    Microsoft.Maui.Devices.BatteryState.Discharging => "Descarregando",
                    Microsoft.Maui.Devices.BatteryState.Full => "Completa",
                    Microsoft.Maui.Devices.BatteryState.NotCharging => "Não carregando (AC)",
                    Microsoft.Maui.Devices.BatteryState.NotPresent => DesktopNoBatteryMessage,
                    _ => "Desconhecido"
                };
            }

            status.HasBattery = true;
            status.TimeRemaining = EstimateTimeRemaining(status);
        }
        catch
        {
            if (!wmiBatteryDetected)
            {
                status.HasBattery = false;
                status.ChargePercent = 0;
                status.PowerSource = "Energia AC";
                status.StatusText = DesktopNoBatteryMessage;
                status.TimeRemaining = "N/A";
            }
        }

        return status;
    }

    private static string EstimateTimeRemaining(BatteryStatus status)
    {
        if (!status.HasBattery)
            return "N/A";

        if (status.IsCharging)
            return "Carregando...";

        if (status.ChargePercent >= 100)
            return "Completa";

        return "Em uso";
    }

    public async Task<BatteryReport> GenerateReportAsync()
    {
        return await Task.Run(async () =>
        {
            var report = new BatteryReport();
            var currentStatus = GetCurrentStatus();
            if (!currentStatus.HasBattery)
            {
                report.HasBattery = false;
                report.Health = DesktopNoBatteryMessage;
                report.DesignCapacity = "N/A (Sem Bateria)";
                report.FullChargeCapacity = "N/A (Sem Bateria)";
                report.CycleCount = "N/A";
                report.Manufacturer = "N/A";
                report.IsReportReady = false;
                return report;
            }

            // 1. Leitura rápida via WMI para preenchimento imediato
            TryReadWmiBattery(report);

            try
            {
                // Caminho exigido: %TEMP%\battery_report.html
                string reportPath = Path.Combine(Path.GetTempPath(), "battery_report.html");

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
                    CreateNoWindow = true
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
                    report.IsReportReady = !string.IsNullOrEmpty(report.Health) && report.Health != "Não disponível";
                }
            }
            catch
            {
                report.IsReportReady = false;
            }

            return report;
        });
    }

    private static void TryReadWmiBattery(BatteryReport report)
    {
        try
        {
            var scope = new ManagementScope(@"\\.\root\wmi");
            scope.Connect();

            long designCap = 0;
            long fullCap = 0;

            using (var s = new ManagementObjectSearcher(scope, new ObjectQuery("SELECT DesignedCapacity, CycleCount FROM BatteryStaticData")))
            {
                foreach (ManagementObject obj in s.Get())
                {
                    using (obj)
                    {
                        if (obj["DesignedCapacity"] != null && long.TryParse(obj["DesignedCapacity"]?.ToString(), out long dc) && dc > 0)
                            designCap = dc;

                        if (obj["CycleCount"] != null && int.TryParse(obj["CycleCount"]?.ToString(), out int cc) && cc > 0)
                            report.CycleCount = cc.ToString();

                        break;
                    }
                }
            }

            using (var s = new ManagementObjectSearcher(scope, new ObjectQuery("SELECT FullChargedCapacity FROM BatteryFullChargedCapacity")))
            {
                foreach (ManagementObject obj in s.Get())
                {
                    using (obj)
                    {
                        if (obj["FullChargedCapacity"] != null && long.TryParse(obj["FullChargedCapacity"]?.ToString(), out long fc) && fc > 0)
                            fullCap = fc;

                        break;
                    }
                }
            }

            if (designCap > 0 && fullCap > 0)
            {
                report.DesignCapacity = $"{designCap} mWh";
                report.FullChargeCapacity = $"{fullCap} mWh";
                double health = ((double)fullCap / designCap) * 100.0;
                report.Health = $"{Math.Min(100.0, health):F1}%";
            }
        }
        catch
        {
            // WMI Battery não suportado nesta máquina
        }
    }

    private static void ParseBatteryReport(string html, BatteryReport report)
    {
        try
        {
            var designMatch = Regex.Match(html,
                @"DESIGN\s*CAPACITY.*?(\d[\d\s,\.]*)\s*mWh",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);

            if (designMatch.Success)
            {
                string value = CleanNumber(designMatch.Groups[1].Value);
                report.DesignCapacity = $"{value} mWh";
            }

            var fullChargeMatch = Regex.Match(html,
                @"FULL\s*CHARGE\s*CAPACITY.*?(\d[\d\s,\.]*)\s*mWh",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);

            if (fullChargeMatch.Success)
            {
                string value = CleanNumber(fullChargeMatch.Groups[1].Value);
                report.FullChargeCapacity = $"{value} mWh";
            }

            if (designMatch.Success && fullChargeMatch.Success)
            {
                string designStr = CleanNumber(designMatch.Groups[1].Value);
                string fullStr = CleanNumber(fullChargeMatch.Groups[1].Value);

                if (double.TryParse(designStr, out double design) &&
                    double.TryParse(fullStr, out double full) && design > 0)
                {
                    double health = (full / design) * 100.0;
                    report.Health = $"{Math.Min(100.0, health):F1}%";
                }
            }

            var cycleMatch = Regex.Match(html,
                @"CYCLE\s*COUNT.*?(\d+)",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);

            if (cycleMatch.Success)
            {
                report.CycleCount = cycleMatch.Groups[1].Value;
            }
            else if (string.IsNullOrEmpty(report.CycleCount))
            {
                report.CycleCount = "Não informado";
            }

            var manufacturerMatch = Regex.Match(html,
                @"MANUFACTURER.*?<td[^>]*>(.*?)</td>",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);

            if (manufacturerMatch.Success)
            {
                string manufacturer = Regex.Replace(manufacturerMatch.Groups[1].Value, @"<[^>]+>", "").Trim();
                report.Manufacturer = string.IsNullOrWhiteSpace(manufacturer) ? "Integrada" : manufacturer;
            }
        }
        catch
        {
            if (string.IsNullOrEmpty(report.Health)) report.Health = "Erro na leitura";
        }
    }

    private static string CleanNumber(string raw)
    {
        return Regex.Replace(raw, @"[^\d]", "").Trim();
    }
}
#endif
