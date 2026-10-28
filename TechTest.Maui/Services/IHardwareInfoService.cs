using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace TechTest.Maui.Services;

public class StorageInfo
{
    public string Name { get; set; } = "";
    public string Type { get; set; } = ""; // SSD, HDD, NVMe
    public long TotalBytes { get; set; }
    public long FreeBytes { get; set; }
    public string DriveFormat { get; set; } = "";

    public double UsedPercentage =>
        TotalBytes > 0 ? (double)(TotalBytes - FreeBytes) / TotalBytes * 100.0 : 0;

    public string FormattedTotalSize => FormatBytes(TotalBytes);
    public string FormattedFreeSpace => FormatBytes(FreeBytes);
    public string FormattedUsedSpace => FormatBytes(TotalBytes - FreeBytes);

    private static string FormatBytes(long bytes)
    {
        string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
        int index = 0;
        double size = bytes;

        while (size >= 1024 && index < suffixes.Length - 1)
        {
            size /= 1024;
            index++;
        }

        return $"{size:F1} {suffixes[index]}";
    }
}

public class GpuInfo
{
    public string Name { get; set; } = "";
    public string DriverVersion { get; set; } = "";
    public long VideoMemoryBytes { get; set; }
}

/// <summary>Saúde S.M.A.R.T. do disco (MSFT_PhysicalDisk + MSFT_StorageReliabilityCounter).</summary>
public class DiskHealthInfo
{
    public string Name { get; set; } = "";
    public string MediaType { get; set; } = "";
    public string HealthStatus { get; set; } = "Desconhecido";
    /// <summary>Saúde estimada em % (100 - desgaste). Null quando o disco não informa o desgaste.</summary>
    public int? HealthPercent { get; set; }
    public int? TemperatureC { get; set; }
    public long? PowerOnHours { get; set; }

    public string DisplayHealth => HealthPercent.HasValue ? $"{HealthPercent}%" : "N/D";
    public string DisplayTemp => TemperatureC.HasValue ? $"{TemperatureC} °C" : "N/D";
    public string DisplayHours => PowerOnHours.HasValue ? $"{PowerOnHours:N0} h (~{PowerOnHours / 24.0:N0} dias)" : "N/D";
    public double HealthFraction => HealthPercent.HasValue ? HealthPercent.Value / 100.0 : 0;
    public string Summary => $"{DisplayHealth} • {DisplayTemp} • {DisplayHours}";

    public Color HealthColor => HealthStatus switch
    {
        "Saudável" when !HealthPercent.HasValue || HealthPercent >= 70 => Color.FromArgb("#22C55E"),
        "Saudável" => Color.FromArgb("#EAB308"),
        "Atenção" => Color.FromArgb("#EAB308"),
        "Crítico" => Color.FromArgb("#EF4444"),
        _ => Color.FromArgb("#9494B8")
    };
}

public class HardwareSpecs
{
    public string CPU { get; set; } = "Carregando...";
    public string Manufacturer { get; set; } = "Carregando...";
    public string Model { get; set; } = "Carregando...";
    public string RAM { get; set; } = "Carregando...";
    public List<GpuInfo> GPUs { get; set; } = new();
    public string ServiceTag { get; set; } = "Carregando...";
    public int ScreenRefreshRate { get; set; } = 0;
    public string ScreenResolution { get; set; } = "";
    public List<StorageInfo> StorageDevices { get; set; } = new();
    public List<DiskHealthInfo> DiskHealth { get; set; } = new();
    public string OSVersion { get; set; } = "";
    public string MachineName { get; set; } = "";
    public string SystemFamily { get; set; } = "";
    public string SystemSku { get; set; } = "";
    public string BaseBoardManufacturer { get; set; } = "";
    public string BaseBoardProduct { get; set; } = "";
    public string BaseBoardSerial { get; set; } = "";
    public string BiosSerial { get; set; } = "";
    public string BatterySummary { get; set; } = "Bateria não detectada (Computador de Mesa / Sem Bateria)";
    public string BatteryReportPath { get; set; } = "";
}

/// <summary>Dispositivo PnP com falha de driver (Win32_PnPEntity.ConfigManagerErrorCode != 0).</summary>
public class ProblemDevice
{
    public string Name { get; set; } = "";
    public string HardwareId { get; set; } = "";
    public int ErrorCode { get; set; }
    public string DeviceId { get; set; } = "";
    public string Display => $"{Name}  (erro {ErrorCode})";
}

public interface IHardwareInfoService
{
    Task<HardwareSpecs> GetHardwareSpecsAsync();

    /// <summary>Lista dispositivos com falha de driver. Retorna vazio em plataformas sem suporte.</summary>
    Task<List<ProblemDevice>> GetProblemDevicesAsync();

    public string GetSystemSummary()
    {
        try
        {
            var specsTask = GetHardwareSpecsAsync();
            specsTask.Wait();
            var specs = specsTask.Result;
            string gpuText = specs.GPUs != null && specs.GPUs.Count > 0 ? $"  |  🎮 {specs.GPUs[0].Name}" : "";
            return $"💻 {specs.MachineName}  |  🧠 {specs.RAM}  |  ⚙️ {specs.CPU}{gpuText}";
        }
        catch
        {
            return $"💻 {System.Environment.MachineName}  |  🧠 {System.GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / (1024 * 1024):N0} MB RAM";
        }
    }
}
