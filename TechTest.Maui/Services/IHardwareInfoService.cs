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
    public string OSVersion { get; set; } = "";
    public string MachineName { get; set; } = "";
}

public interface IHardwareInfoService
{
    Task<HardwareSpecs> GetHardwareSpecsAsync();

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
