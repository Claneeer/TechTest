#if MACCATALYST
using System;
using System.IO;
using System.Collections.Generic;
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
            string ioregOutput = await RunCommandAsync(
                "ioreg -l -w0 | grep -E '\"(DesignCapacity|MaxCapacity|CycleCount|BatteryManufacturer)\"'");

            if (!string.IsNullOrWhiteSpace(ioregOutput))
            {
                ParseIoregOutput(ioregOutput, report);
            }

            string pmsetOutput = await RunCommandAsync("pmset -g batt");
            if (!string.IsNullOrWhiteSpace(pmsetOutput))
            {
                ParsePmsetOutput(pmsetOutput, report);
            }

            string reportDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "TechTest");

            if (!Directory.Exists(reportDir))
                Directory.CreateDirectory(reportDir);

            string reportPath = Path.Combine(reportDir, "battery-report.txt");
            await GenerateTextReport(report, reportPath);

            report.ReportFilePath = reportPath;
            report.IsReportReady = true;
        }
        catch
        {
            report.IsReportReady = false;
        }

        return report;
    }

    private void ParseIoregOutput(string output, BatteryReport report)
    {
        var designMatch = Regex.Match(output,
            @"""DesignCapacity""\s*=\s*(\d+)", RegexOptions.IgnoreCase);
        if (designMatch.Success)
        {
            report.DesignCapacity = $"{designMatch.Groups[1].Value} mAh";
        }
        else
        {
            report.DesignCapacity = "Não disponível";
        }

        var maxCapMatch = Regex.Match(output,
            @"""MaxCapacity""\s*=\s*(\d+)", RegexOptions.IgnoreCase);
        if (maxCapMatch.Success)
        {
            report.FullChargeCapacity = $"{maxCapMatch.Groups[1].Value} mAh";
        }
        else
        {
            report.FullChargeCapacity = "Não disponível";
        }

        if (designMatch.Success && maxCapMatch.Success)
        {
            if (double.TryParse(designMatch.Groups[1].Value, out double design) &&
                double.TryParse(maxCapMatch.Groups[1].Value, out double maxCap) &&
                design > 0)
            {
                double health = (maxCap / design) * 100.0;
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

        var cycleMatch = Regex.Match(output,
            @"""CycleCount""\s*=\s*(\d+)", RegexOptions.IgnoreCase);
        report.CycleCount = cycleMatch.Success
            ? cycleMatch.Groups[1].Value
            : "Não disponível";

        var mfgMatch = Regex.Match(output,
            @"""BatteryManufacturer""\s*=\s*""([^""]+)""", RegexOptions.IgnoreCase);
        report.Manufacturer = mfgMatch.Success
            ? mfgMatch.Groups[1].Value.Trim()
            : "Não disponível";
    }

    private void ParsePmsetOutput(string output, BatteryReport report)
    {
        if (string.IsNullOrEmpty(report.DesignCapacity) ||
            report.DesignCapacity == "Não disponível")
        {
            var percentMatch = Regex.Match(output, @"(\d+)%");
            if (percentMatch.Success)
            {
                // pmset só fornece percentual, não capacidade absoluta
            }
        }
    }

    private async Task GenerateTextReport(BatteryReport report, string outputPath)
    {
        var lines = new List<string>
        {
            "═══════════════════════════════════════════",
            "        Relatório de Bateria - macOS",
            "═══════════════════════════════════════════",
            "",
            $"  Data do Relatório: {DateTime.Now:dd/MM/yyyy HH:mm:ss}",
            $"  Dispositivo: {Environment.MachineName}",
            "",
            "───────────────────────────────────────────",
            "  Informações da Bateria",
            "───────────────────────────────────────────",
            "",
            $"  Fabricante:            {report.Manufacturer}",
            $"  Capacidade de Projeto: {report.DesignCapacity}",
            $"  Capacidade Atual:      {report.FullChargeCapacity}",
            $"  Saúde da Bateria:      {report.Health}",
            $"  Ciclos de Carga:       {report.CycleCount}",
            "",
            "═══════════════════════════════════════════"
        };

        await File.WriteAllLinesAsync(outputPath, lines);
    }

    private async Task<string> RunCommandAsync(string command)
    {
        var processInfo = new ProcessStartInfo
        {
            FileName = "/bin/bash",
            Arguments = $"-c \"{command.Replace("\"", "\\\"")}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = processInfo };
        process.Start();

        string output = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();

        return output;
    }
}
#endif
