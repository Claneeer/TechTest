#if WINDOWS
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Management;
using System.Text.RegularExpressions;

namespace TechTest.Maui.Services;

public class HardwareInfoService : IHardwareInfoService
{
    public async Task<HardwareSpecs> GetHardwareSpecsAsync()
    {
        return await Task.Run(() =>
        {
            var specs = new HardwareSpecs
            {
                MachineName = Environment.MachineName,
                OSVersion = $"Windows {Environment.OSVersion.Version}"
            };

            specs.CPU = GetCpuName();
            GetManufacturerAndModel(specs);
            specs.RAM = GetTotalRam();
            specs.GPUs = GetGpuList();
            GetScreenInfo(specs);
            specs.StorageDevices = GetStorageDevices();
            specs.ServiceTag = GetServiceTag();

            return specs;
        });
    }

    private string GetCpuName()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT Name FROM Win32_Processor");
            foreach (var obj in searcher.Get())
            {
                string name = obj["Name"]?.ToString() ?? "Desconhecido";
                name = CleanCpuName(name);
                obj.Dispose();
                return name;
            }
        }
        catch
        {
            // Fallback silencioso
        }

        return "Desconhecido";
    }

    private string CleanCpuName(string name)
    {
        name = Regex.Replace(name, @"\(R\)", "", RegexOptions.IgnoreCase);
        name = Regex.Replace(name, @"\(TM\)", "", RegexOptions.IgnoreCase);
        name = Regex.Replace(name, @"\bCPU\b", "", RegexOptions.IgnoreCase);
        name = Regex.Replace(name, @"^\s*(Intel|AMD)\s+", "", RegexOptions.IgnoreCase);
        name = Regex.Replace(name, @"\s{2,}", " ");
        name = Regex.Replace(name, @"\s+@", " @");
        return name.Trim();
    }

    private void GetManufacturerAndModel(HardwareSpecs specs)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT Manufacturer, Model FROM Win32_ComputerSystem");
            foreach (var obj in searcher.Get())
            {
                string rawManufacturer = obj["Manufacturer"]?.ToString() ?? "Desconhecido";
                string rawModel = obj["Model"]?.ToString() ?? "Desconhecido";

                specs.Manufacturer = CleanManufacturerName(rawManufacturer);
                specs.Model = rawModel.Trim();

                obj.Dispose();
                return;
            }
        }
        catch
        {
            specs.Manufacturer = "Desconhecido";
            specs.Model = "Desconhecido";
        }
    }

    private string CleanManufacturerName(string name)
    {
        var mappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "Dell Inc.", "Dell" },
            { "Dell Inc", "Dell" },
            { "DELL", "Dell" },
            { "Lenovo", "Lenovo" },
            { "LENOVO", "Lenovo" },
            { "Hewlett-Packard", "HP" },
            { "HP", "HP" },
            { "Hewlett Packard", "HP" },
            { "ASUSTeK Computer Inc.", "ASUS" },
            { "ASUSTeK COMPUTER INC.", "ASUS" },
            { "ASUS", "ASUS" },
            { "Acer", "Acer" },
            { "Acer, Inc.", "Acer" },
            { "ACER", "Acer" },
            { "Apple Inc.", "Apple" },
            { "Apple", "Apple" },
            { "Samsung Electronics", "Samsung" },
            { "SAMSUNG ELECTRONICS CO., LTD.", "Samsung" },
            { "Samsung", "Samsung" },
            { "Positivo Tecnologia SA", "Positivo" },
            { "Positivo", "Positivo" },
            { "POSITIVO", "Positivo" },
            { "Gigabyte Technology Co., Ltd.", "Gigabyte" },
            { "GIGABYTE", "Gigabyte" },
            { "Micro-Star International Co., Ltd.", "MSI" },
            { "Micro-Star International", "MSI" },
            { "MSI", "MSI" }
        };

        string trimmed = name.Trim();

        if (mappings.TryGetValue(trimmed, out string? clean))
            return clean;

        foreach (var kvp in mappings)
        {
            if (trimmed.Contains(kvp.Key, StringComparison.OrdinalIgnoreCase))
                return kvp.Value;
        }

        return trimmed;
    }

    private string GetTotalRam()
    {
        try
        {
            long totalBytes = 0;
            using var searcher = new ManagementObjectSearcher("SELECT Capacity FROM Win32_PhysicalMemory");
            foreach (var obj in searcher.Get())
            {
                if (obj["Capacity"] != null && long.TryParse(obj["Capacity"].ToString(), out long capacity))
                {
                    totalBytes += capacity;
                }
                obj.Dispose();
            }

            if (totalBytes > 0)
            {
                double gb = totalBytes / (1024.0 * 1024.0 * 1024.0);
                return $"{gb:F1} GB";
            }
        }
        catch
        {
            // Fallback silencioso
        }

        return "Desconhecido";
    }

    private List<GpuInfo> GetGpuList()
    {
        var gpus = new List<GpuInfo>();

        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT Name, DriverVersion, AdapterRAM FROM Win32_VideoController");

            foreach (var obj in searcher.Get())
            {
                var gpu = new GpuInfo
                {
                    Name = obj["Name"]?.ToString() ?? "Desconhecido",
                    DriverVersion = obj["DriverVersion"]?.ToString() ?? ""
                };

                if (obj["AdapterRAM"] != null && long.TryParse(obj["AdapterRAM"].ToString(), out long vram))
                {
                    gpu.VideoMemoryBytes = vram;
                }

                gpus.Add(gpu);
                obj.Dispose();
            }
        }
        catch
        {
            // Fallback silencioso
        }

        return gpus;
    }

    private void GetScreenInfo(HardwareSpecs specs)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT CurrentRefreshRate, CurrentHorizontalResolution, CurrentVerticalResolution FROM Win32_VideoController");

            foreach (var obj in searcher.Get())
            {
                if (obj["CurrentRefreshRate"] != null &&
                    int.TryParse(obj["CurrentRefreshRate"].ToString(), out int hz) && hz > 0)
                {
                    specs.ScreenRefreshRate = hz;
                }

                if (obj["CurrentHorizontalResolution"] != null && obj["CurrentVerticalResolution"] != null)
                {
                    string hRes = obj["CurrentHorizontalResolution"].ToString()!;
                    string vRes = obj["CurrentVerticalResolution"].ToString()!;
                    if (!string.IsNullOrEmpty(hRes) && !string.IsNullOrEmpty(vRes))
                    {
                        specs.ScreenResolution = $"{hRes} x {vRes}";
                    }
                }

                obj.Dispose();

                if (specs.ScreenRefreshRate > 0 && !string.IsNullOrEmpty(specs.ScreenResolution))
                    break;
            }
        }
        catch
        {
            // Fallback silencioso
        }
    }

    private List<StorageInfo> GetStorageDevices()
    {
        var devices = new List<StorageInfo>();

        try
        {
            var diskTypes = GetDiskMediaTypes();

            var drives = DriveInfo.GetDrives();
            foreach (var drive in drives)
            {
                if (!drive.IsReady) continue;

                try
                {
                    var storage = new StorageInfo
                    {
                        Name = $"{drive.Name.TrimEnd('\\')} {drive.VolumeLabel}".Trim(),
                        TotalBytes = drive.TotalSize,
                        FreeBytes = drive.TotalFreeSpace,
                        DriveFormat = drive.DriveFormat,
                        Type = DetermineStorageType(drive, diskTypes)
                    };

                    devices.Add(storage);
                }
                catch
                {
                    // Ignorar unidades inacessíveis
                }
            }
        }
        catch
        {
            // Fallback silencioso
        }

        return devices;
    }

    private Dictionary<int, string> GetDiskMediaTypes()
    {
        var types = new Dictionary<int, string>();

        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT DeviceID, MediaType FROM Win32_DiskDrive");
            int index = 0;
            foreach (var obj in searcher.Get())
            {
                string mediaType = obj["MediaType"]?.ToString() ?? "";

                string type;
                if (mediaType.Contains("SSD", StringComparison.OrdinalIgnoreCase) ||
                    mediaType.Contains("Solid", StringComparison.OrdinalIgnoreCase))
                {
                    type = "SSD";
                }
                else if (mediaType.Contains("Fixed", StringComparison.OrdinalIgnoreCase) ||
                         mediaType.Contains("HDD", StringComparison.OrdinalIgnoreCase))
                {
                    type = "HDD";
                }
                else if (mediaType.Contains("Removable", StringComparison.OrdinalIgnoreCase))
                {
                    type = "Removível";
                }
                else if (mediaType.Contains("External", StringComparison.OrdinalIgnoreCase))
                {
                    type = "Externo";
                }
                else
                {
                    type = string.IsNullOrEmpty(mediaType) ? "Desconhecido" : mediaType;
                }

                types[index] = type;
                index++;
                obj.Dispose();
            }
        }
        catch
        {
            // Fallback silencioso
        }

        return types;
    }

    private string DetermineStorageType(DriveInfo drive, Dictionary<int, string> diskTypes)
    {
        if (drive.DriveType == DriveType.Network)
            return "Rede";
        if (drive.DriveType == DriveType.CDRom)
            return "CD/DVD";
        if (drive.DriveType == DriveType.Removable)
            return "Removível";

        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT DeviceID, MediaType, Model FROM Win32_DiskDrive");

            foreach (var disk in searcher.Get())
            {
                string model = disk["Model"]?.ToString() ?? "";
                string mediaType = disk["MediaType"]?.ToString() ?? "";

                if (model.Contains("NVMe", StringComparison.OrdinalIgnoreCase))
                {
                    disk.Dispose();
                    return "NVMe";
                }

                if (model.Contains("SSD", StringComparison.OrdinalIgnoreCase) ||
                    mediaType.Contains("SSD", StringComparison.OrdinalIgnoreCase) ||
                    mediaType.Contains("Solid", StringComparison.OrdinalIgnoreCase))
                {
                    disk.Dispose();
                    return "SSD";
                }

                disk.Dispose();
            }
        }
        catch
        {
            // Fallback silencioso
        }

        if (diskTypes.Count > 0)
            return diskTypes.Values.FirstOrDefault() ?? "HDD";

        return "HDD";
    }

    private string GetServiceTag()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT SerialNumber FROM Win32_Bios");
            foreach (var obj in searcher.Get())
            {
                string serial = obj["SerialNumber"]?.ToString() ?? "Desconhecido";
                obj.Dispose();

                if (!string.IsNullOrWhiteSpace(serial) &&
                    !serial.Equals("To be filled by O.E.M.", StringComparison.OrdinalIgnoreCase) &&
                    !serial.Equals("Default string", StringComparison.OrdinalIgnoreCase) &&
                    !serial.Equals("System Serial Number", StringComparison.OrdinalIgnoreCase) &&
                    !serial.Equals("None", StringComparison.OrdinalIgnoreCase))
                {
                    return serial.Trim();
                }

                return "Não disponível";
            }
        }
        catch
        {
            // Fallback silencioso
        }

        return "Desconhecido";
    }
}
#endif
