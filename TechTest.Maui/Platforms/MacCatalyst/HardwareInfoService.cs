#if MACCATALYST
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace TechTest.Maui.Services;

public class HardwareInfoService : IHardwareInfoService
{
    public async Task<HardwareSpecs> GetHardwareSpecsAsync()
    {
        return await Task.Run(async () =>
        {
            var specs = new HardwareSpecs
            {
                MachineName = Environment.MachineName,
                OSVersion = $"macOS {Environment.OSVersion.Version}",
                Manufacturer = "Apple"
            };

            specs.CPU = await GetCpuNameAsync();
            specs.Model = await GetModelAsync();
            specs.RAM = await GetRamAsync();
            specs.GPUs = await GetGpuListAsync();
            specs.ScreenRefreshRate = await GetScreenRefreshRateAsync();
            specs.ScreenResolution = await GetScreenResolutionAsync();
            specs.StorageDevices = await GetStorageDevicesAsync();
            specs.ServiceTag = await GetServiceTagAsync();

            return specs;
        });
    }

    private async Task<string> GetCpuNameAsync()
    {
        try
        {
            string output = await RunCommandAsync("sysctl -n machdep.cpu.brand_string");
            return string.IsNullOrWhiteSpace(output) ? "Desconhecido" : output.Trim();
        }
        catch
        {
            return "Desconhecido";
        }
    }

    private async Task<string> GetModelAsync()
    {
        try
        {
            string output = await RunCommandAsync("sysctl -n hw.model");
            return string.IsNullOrWhiteSpace(output) ? "Desconhecido" : output.Trim();
        }
        catch
        {
            return "Desconhecido";
        }
    }

    private async Task<string> GetRamAsync()
    {
        try
        {
            string output = await RunCommandAsync("sysctl -n hw.memsize");
            if (long.TryParse(output.Trim(), out long bytes))
            {
                double gb = bytes / (1024.0 * 1024.0 * 1024.0);
                return $"{gb:F1} GB";
            }
        }
        catch
        {
            // Fallback silencioso
        }

        return "Desconhecido";
    }

    private async Task<List<GpuInfo>> GetGpuListAsync()
    {
        var gpus = new List<GpuInfo>();

        try
        {
            string output = await RunCommandAsync("system_profiler SPDisplaysDataType");
            if (string.IsNullOrWhiteSpace(output))
                return gpus;

            var lines = output.Split('\n');

            foreach (string line in lines)
            {
                string trimmed = line.Trim();

                if (trimmed.StartsWith("Chipset Model:", StringComparison.OrdinalIgnoreCase))
                {
                    string gpuName = trimmed.Substring("Chipset Model:".Length).Trim();
                    if (!string.IsNullOrEmpty(gpuName))
                    {
                        gpus.Add(new GpuInfo
                        {
                            Name = gpuName,
                            DriverVersion = "",
                            VideoMemoryBytes = 0
                        });
                    }
                }
                else if (trimmed.StartsWith("VRAM", StringComparison.OrdinalIgnoreCase) && gpus.Count > 0)
                {
                    var vramMatch = Regex.Match(trimmed, @"(\d+)\s*(MB|GB)", RegexOptions.IgnoreCase);
                    if (vramMatch.Success)
                    {
                        long value = long.Parse(vramMatch.Groups[1].Value);
                        string unit = vramMatch.Groups[2].Value.ToUpperInvariant();
                        long bytes = unit == "GB"
                            ? value * 1024L * 1024L * 1024L
                            : value * 1024L * 1024L;
                        gpus[^1].VideoMemoryBytes = bytes;
                    }
                }
            }

            if (gpus.Count == 0)
            {
                string chipLine = lines.FirstOrDefault(l =>
                    l.Contains("Chip", StringComparison.OrdinalIgnoreCase) &&
                    !l.Contains("Chipset", StringComparison.OrdinalIgnoreCase)) ?? "";

                if (!string.IsNullOrWhiteSpace(chipLine))
                {
                    string chipName = chipLine.Split(':').LastOrDefault()?.Trim() ?? "";
                    if (!string.IsNullOrEmpty(chipName))
                    {
                        gpus.Add(new GpuInfo { Name = chipName });
                    }
                }
            }
        }
        catch
        {
            // Fallback silencioso
        }

        return gpus;
    }

    private async Task<int> GetScreenRefreshRateAsync()
    {
        try
        {
            string output = await RunCommandAsync("system_profiler SPDisplaysDataType");
            var hzMatch = Regex.Match(output, @"(\d+)\s*Hz", RegexOptions.IgnoreCase);
            if (hzMatch.Success && int.TryParse(hzMatch.Groups[1].Value, out int hz))
            {
                return hz;
            }
        }
        catch
        {
            // Fallback silencioso
        }

        return 60;
    }

    private async Task<string> GetScreenResolutionAsync()
    {
        try
        {
            string output = await RunCommandAsync("system_profiler SPDisplaysDataType");

            var resMatch = Regex.Match(output, @"Resolution:\s*(\d+)\s*x\s*(\d+)", RegexOptions.IgnoreCase);
            if (resMatch.Success)
            {
                return $"{resMatch.Groups[1].Value} x {resMatch.Groups[2].Value}";
            }

            var uiMatch = Regex.Match(output, @"UI Looks like:\s*(\d+)\s*x\s*(\d+)", RegexOptions.IgnoreCase);
            if (uiMatch.Success)
            {
                return $"{uiMatch.Groups[1].Value} x {uiMatch.Groups[2].Value}";
            }
        }
        catch
        {
            // Fallback silencioso
        }

        return "";
    }

    private async Task<List<StorageInfo>> GetStorageDevicesAsync()
    {
        var devices = new List<StorageInfo>();

        try
        {
            string dfOutput = await RunCommandAsync("df -k /");
            var dfLines = dfOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries);

            if (dfLines.Length >= 2)
            {
                var parts = Regex.Split(dfLines[1].Trim(), @"\s+");
                if (parts.Length >= 4)
                {
                    long totalKb = long.TryParse(parts[1], out long t) ? t : 0;
                    long availKb = long.TryParse(parts[3], out long a) ? a : 0;

                    string diskName = "Macintosh HD";
                    string diskFormat = "APFS";
                    string diskType = "SSD";

                    try
                    {
                        string diskutilOutput = await RunCommandAsync("diskutil info /");
                        var nameMatch = Regex.Match(diskutilOutput, @"Volume Name:\s*(.+)",
                            RegexOptions.IgnoreCase);
                        if (nameMatch.Success)
                            diskName = nameMatch.Groups[1].Value.Trim();

                        var fsMatch = Regex.Match(diskutilOutput,
                            @"Type \(Bundle\):\s*(.+)", RegexOptions.IgnoreCase);
                        if (fsMatch.Success)
                            diskFormat = fsMatch.Groups[1].Value.Trim();

                        var solidMatch = Regex.Match(diskutilOutput,
                            @"Solid State:\s*(Yes|No)", RegexOptions.IgnoreCase);
                        if (solidMatch.Success)
                            diskType = solidMatch.Groups[1].Value.Equals("Yes",
                                StringComparison.OrdinalIgnoreCase) ? "SSD" : "HDD";

                        var protoMatch = Regex.Match(diskutilOutput,
                            @"Protocol:\s*(.+)", RegexOptions.IgnoreCase);
                        if (protoMatch.Success &&
                            protoMatch.Groups[1].Value.Contains("NVMe", StringComparison.OrdinalIgnoreCase))
                        {
                            diskType = "NVMe";
                        }
                    }
                    catch
                    {
                        // Usar valores padrão
                    }

                    devices.Add(new StorageInfo
                    {
                        Name = diskName,
                        Type = diskType,
                        TotalBytes = totalKb * 1024L,
                        FreeBytes = availKb * 1024L,
                        DriveFormat = diskFormat
                    });
                }
            }
        }
        catch
        {
            // Fallback silencioso
        }

        return devices;
    }

    private async Task<string> GetServiceTagAsync()
    {
        try
        {
            string output = await RunCommandAsync(
                "system_profiler SPHardwareDataType | grep 'Serial Number'");

            var match = Regex.Match(output, @"Serial Number.*?:\s*(.+)", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                string serial = match.Groups[1].Value.Trim();
                return string.IsNullOrWhiteSpace(serial) ? "Não disponível" : serial;
            }
        }
        catch
        {
            // Fallback silencioso
        }

        return "Não disponível";
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
