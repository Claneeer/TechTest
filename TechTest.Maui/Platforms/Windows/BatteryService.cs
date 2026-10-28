#if WINDOWS
using System;
using System.IO;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Management;
using System.Threading;
using BatteryPower = Microsoft.Maui.Devices.Battery;

namespace TechTest.Maui.Services;

public class BatteryService : IBatteryService
{
    public BatteryStatus GetCurrentStatus()
    {
        var status = new BatteryStatus();

        try
        {
            // 1) Win32_Battery: sem instâncias => desktop / sem bateria instalada
            int? percent = null;
            int? wmiState = null;
            bool found = false;

            try
            {
                using var searcher = new ManagementObjectSearcher(
                    "SELECT EstimatedChargeRemaining, BatteryStatus FROM Win32_Battery");
                foreach (ManagementBaseObject obj in searcher.Get())
                {
                    found = true;
                    if (int.TryParse(obj["EstimatedChargeRemaining"]?.ToString(), out int p)) percent = p;
                    if (int.TryParse(obj["BatteryStatus"]?.ToString(), out int s)) wmiState = s;
                    obj.Dispose();
                    break;
                }
            }
            catch
            {
                // WMI indisponível: decide pela API do MAUI abaixo
            }

            var mauiState = SafeMauiState();
            if (!found && mauiState is BatteryState.NotPresent or BatteryState.Unknown or null)
            {
                status.HasBattery = false;
                status.StatusText = BatteryStatus.NoBatteryMessage;
                status.PowerSource = "Energia AC";
                status.PowerType = "AC";
                status.TimeRemaining = "--";
                return status;
            }

            status.HasBattery = true;
            status.ChargePercent = percent ?? (int)Math.Round(BatteryPower.Default.ChargeLevel * 100);

            // BatteryStatus (WMI): 1=descarregando 2=AC 3=completa 4=baixa 5=crítica 6-9=carregando 11=parcialmente carregada
            bool onAc;
            if (wmiState.HasValue)
            {
                onAc = wmiState is 2 or 3 or 6 or 7 or 8 or 9 or 11;
                status.IsCharging = wmiState is 6 or 7 or 8 or 9;
                status.StatusText = wmiState switch
                {
                    1 => "Descarregando",
                    2 => "Conectada à energia (não carregando)",
                    3 => "Completa",
                    4 => "Bateria baixa",
                    5 => "Bateria crítica",
                    6 or 7 or 8 or 9 => "Carregando",
                    11 => "Parcialmente carregada",
                    _ => "Desconhecido"
                };
            }
            else
            {
                onAc = BatteryPower.Default.PowerSource == BatteryPowerSource.AC;
                status.IsCharging = mauiState == BatteryState.Charging;
                status.StatusText = mauiState switch
                {
                    BatteryState.Charging => "Carregando",
                    BatteryState.Discharging => "Descarregando",
                    BatteryState.Full => "Completa",
                    BatteryState.NotCharging => "Não carregando",
                    _ => "Desconhecido"
                };
            }

            status.PowerType = onAc ? "AC" : "DC";
            status.PowerSource = onAc ? "Energia AC (tomada)" : "Bateria (DC)";
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

    private static BatteryState? SafeMauiState()
    {
        try { return BatteryPower.Default.State; }
        catch { return null; }
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

        // Desktop/sem bateria: não executa powercfg (geraria erro) e informa claramente.
        if (!GetCurrentStatus().HasBattery)
        {
            const string msg = BatteryStatus.NoBatteryMessage;
            report.DesignCapacity = report.FullChargeCapacity = report.Health =
                report.CycleCount = report.Manufacturer = msg;
            report.IsReportReady = false;
            return report;
        }

        try
        {
            // powercfg /batteryreport /output "%TEMP%\battery_report.html"
            string reportPath = Path.Combine(Path.GetTempPath(), "battery_report.html");

            if (File.Exists(reportPath))
            {
                try { File.Delete(reportPath); } catch { }
            }

            var processInfo = new ProcessStartInfo
            {
                FileName = "powercfg.exe",
                Arguments = $"/batteryreport /output \"{reportPath}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = processInfo };
            process.Start();

            // Drena as saídas para não bloquear o processo e aplica timeout
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            try
            {
                await process.WaitForExitAsync(cts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                try { process.Kill(entireProcessTree: true); } catch { }
                throw;
            }

            await Task.WhenAll(stdout, stderr).ConfigureAwait(false);

            if (File.Exists(reportPath))
            {
                report.ReportFilePath = reportPath;
                report.IsReportReady = true;

                string htmlContent = await File.ReadAllTextAsync(reportPath).ConfigureAwait(false);
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
