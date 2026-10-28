#if WINDOWS
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace TechTest.Maui.Services;

public class HardwareInfoService : IHardwareInfoService
{
    public const string DesktopNoBatteryMessage = "Bateria não detectada (Computador de Mesa / Sem Bateria)";

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
            GetBaseBoardInfo(specs);
            GetManufacturerAndModel(specs);
            specs.RAM = GetTotalRam();
            specs.GPUs = GetGpuList();
            GetScreenInfo(specs);
            specs.StorageDevices = GetStorageDevices();
            specs.ServiceTag = GetServiceTag();
            specs.DiskHealth = GetDiskHealth();
            specs.BiosSerial = specs.ServiceTag;

            if (IsInvalidSerial(specs.ServiceTag) && !IsInvalidSerial(specs.BaseBoardSerial))
            {
                specs.ServiceTag = specs.BaseBoardSerial;
            }

            PopulateBatteryInfoAndTriggerReport(specs);

            return specs;
        });
    }

    public async Task<List<ProblemDevice>> GetProblemDevicesAsync()
    {
        return await Task.Run(() =>
        {
            var list = new List<ProblemDevice>();
            try
            {
                using var searcher = new ManagementObjectSearcher(
                    "SELECT Name, DeviceID, HardwareID, ConfigManagerErrorCode FROM Win32_PnPEntity WHERE ConfigManagerErrorCode <> 0");
                foreach (ManagementBaseObject obj in searcher.Get())
                {
                    using (obj)
                    {
                        string hwId = "";
                        if (obj["HardwareID"] is string[] ids && ids.Length > 0)
                            hwId = ids[0] ?? "";

                        int code = 0;
                        if (obj["ConfigManagerErrorCode"] != null)
                            int.TryParse(obj["ConfigManagerErrorCode"]?.ToString(), out code);

                        list.Add(new ProblemDevice
                        {
                            Name = obj["Name"]?.ToString() ?? "Dispositivo desconhecido",
                            DeviceId = obj["DeviceID"]?.ToString() ?? "",
                            HardwareId = hwId,
                            ErrorCode = code
                        });
                    }
                }
            }
            catch
            {
                // Fallback silencioso
            }
            return list;
        });
    }

    private static bool IsGenericOrEmpty(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return true;
        string[] invalid =
        {
            "To be filled by O.E.M.", "Default string", "System manufacturer",
            "System Product Name", "None", "Desconhecido", "All Series",
            "Type2 - Board Vendor Name1", "Type2 - Board Asset Tag"
        };

        foreach (var s in invalid)
        {
            if (value.Equals(s, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    private static bool IsInvalidSerial(string? serial)
    {
        if (string.IsNullOrWhiteSpace(serial)) return true;
        string[] invalid =
        {
            "To be filled by O.E.M.", "Default string", "System Serial Number", "None",
            "Não disponível", "Desconhecido", "Carregando..."
        };

        foreach (var s in invalid)
        {
            if (serial.Equals(s, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    /// <summary>
    /// Normaliza o nome do fabricante usando cadeia estruturada if/else if com IndexOf(..., StringComparison.OrdinalIgnoreCase).
    /// Caso nulo, vazio ou não reconhecido, retorna obrigatoriamente "Desconhecido".
    /// </summary>
    public static string NormalizeManufacturer(string? rawManufacturer)
    {
        if (string.IsNullOrWhiteSpace(rawManufacturer) || IsGenericOrEmpty(rawManufacturer))
            return "Desconhecido";

        string raw = rawManufacturer.Trim();

        if (raw.IndexOf("LENOVO", StringComparison.OrdinalIgnoreCase) >= 0)
            return "Lenovo";
        else if (raw.IndexOf("Dell Inc.", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 raw.IndexOf("Dell", StringComparison.OrdinalIgnoreCase) >= 0)
            return "Dell";
        else if (raw.IndexOf("Hewlett-Packard", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 raw.IndexOf("Hewlett Packard", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 raw.IndexOf("HP", StringComparison.OrdinalIgnoreCase) >= 0)
            return "HP";
        else if (raw.IndexOf("Acer", StringComparison.OrdinalIgnoreCase) >= 0)
            return "Acer";
        else if (raw.IndexOf("ASUSTeK COMPUTER INC.", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 raw.IndexOf("ASUS", StringComparison.OrdinalIgnoreCase) >= 0)
            return "ASUS";
        else if (raw.IndexOf("SAMSUNG", StringComparison.OrdinalIgnoreCase) >= 0)
            return "Samsung";
        else if (raw.IndexOf("Positivo", StringComparison.OrdinalIgnoreCase) >= 0)
            return "Positivo";
        else if (raw.IndexOf("Avell", StringComparison.OrdinalIgnoreCase) >= 0)
            return "Avell";
        else if (raw.IndexOf("Apple", StringComparison.OrdinalIgnoreCase) >= 0)
            return "Apple";
        else
            return "Desconhecido";
    }

    private void GetManufacturerAndModel(HardwareSpecs specs)
    {
        string? csManufacturer = null;
        string? csModel = null;
        string? family = null;
        string? sku = null;

        // 1. Extração via Win32_ComputerSystem
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT Manufacturer, Model, SystemFamily, SystemSKUNumber FROM Win32_ComputerSystem");
            foreach (ManagementBaseObject obj in searcher.Get())
            {
                using (obj)
                {
                    csManufacturer = obj["Manufacturer"]?.ToString()?.Trim();
                    csModel = obj["Model"]?.ToString()?.Trim();
                    family = obj["SystemFamily"]?.ToString()?.Trim();
                    sku = obj["SystemSKUNumber"]?.ToString()?.Trim();
                    break;
                }
            }
        }
        catch { }

        string normalizedManufacturer = NormalizeManufacturer(csManufacturer);

        // 2. Fallback via Win32_BIOS.Manufacturer caso Win32_ComputerSystem.Manufacturer não identifique
        if (normalizedManufacturer == "Desconhecido")
        {
            try
            {
                using var biosSearcher = new ManagementObjectSearcher("SELECT Manufacturer FROM Win32_BIOS");
                foreach (ManagementBaseObject obj in biosSearcher.Get())
                {
                    using (obj)
                    {
                        string? biosManufacturer = obj["Manufacturer"]?.ToString()?.Trim();
                        normalizedManufacturer = NormalizeManufacturer(biosManufacturer);
                        if (normalizedManufacturer != "Desconhecido")
                            break;
                    }
                }
            }
            catch { }
        }

        // 3. Fallback adicional via Registro ou BaseBoard se ainda "Desconhecido"
        if (normalizedManufacturer == "Desconhecido")
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\BIOS");
                if (key != null)
                {
                    string? regMan = key.GetValue("SystemManufacturer")?.ToString()?.Trim();
                    normalizedManufacturer = NormalizeManufacturer(regMan);

                    if (IsGenericOrEmpty(csModel))
                    {
                        string? regMod = key.GetValue("SystemProductName")?.ToString()?.Trim();
                        if (!IsGenericOrEmpty(regMod)) csModel = regMod;
                    }
                }
            }
            catch { }
        }

        if (normalizedManufacturer == "Desconhecido" && !IsGenericOrEmpty(specs.BaseBoardManufacturer))
        {
            normalizedManufacturer = NormalizeManufacturer(specs.BaseBoardManufacturer);
        }

        if (IsGenericOrEmpty(csModel) && !IsGenericOrEmpty(specs.BaseBoardProduct))
        {
            csModel = specs.BaseBoardProduct;
        }

        specs.Manufacturer = normalizedManufacturer;
        specs.Model = !IsGenericOrEmpty(csModel) ? csModel! : "Desconhecido";
        specs.SystemFamily = family ?? "";
        specs.SystemSku = sku ?? "";
    }

    private void GetBaseBoardInfo(HardwareSpecs specs)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT Manufacturer, Product, SerialNumber FROM Win32_BaseBoard");
            foreach (ManagementBaseObject obj in searcher.Get())
            {
                using (obj)
                {
                    specs.BaseBoardManufacturer = obj["Manufacturer"]?.ToString()?.Trim() ?? "";
                    specs.BaseBoardProduct = obj["Product"]?.ToString()?.Trim() ?? "";
                    specs.BaseBoardSerial = obj["SerialNumber"]?.ToString()?.Trim() ?? "";
                    return;
                }
            }
        }
        catch
        {
            // Fallback silencioso
        }
    }

    private string GetServiceTag()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT SerialNumber FROM Win32_BIOS");
            foreach (ManagementBaseObject obj in searcher.Get())
            {
                using (obj)
                {
                    string? serial = obj["SerialNumber"]?.ToString()?.Trim();
                    if (!string.IsNullOrWhiteSpace(serial) && !IsInvalidSerial(serial))
                    {
                        return serial;
                    }
                }
            }
        }
        catch
        {
            // Fallback silencioso
        }

        return "Desconhecido";
    }

    private void PopulateBatteryInfoAndTriggerReport(HardwareSpecs specs)
    {
        bool batteryFound = false;

        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT EstimatedChargeRemaining, BatteryStatus FROM Win32_Battery");
            foreach (ManagementBaseObject obj in searcher.Get())
            {
                using (obj)
                {
                    batteryFound = true;
                    int charge = 0;
                    if (obj["EstimatedChargeRemaining"] != null)
                        int.TryParse(obj["EstimatedChargeRemaining"]?.ToString(), out charge);

                    int statusCode = 0;
                    if (obj["BatteryStatus"] != null)
                        int.TryParse(obj["BatteryStatus"]?.ToString(), out statusCode);

                    string statusDesc = statusCode switch
                    {
                        1 => "Descarregando (Bateria)",
                        2 => "Conectado na Tomada (AC)",
                        3 => "Carga Completa",
                        4 => "Carga Baixa",
                        5 => "Carga Crítica",
                        6 => "Carregando",
                        7 => "Carregando (Alta)",
                        8 => "Carregando (Baixa)",
                        9 => "Carregando (Crítica)",
                        _ => "Detectada"
                    };

                    specs.BatterySummary = $"{charge}% - {statusDesc}";
                    break;
                }
            }
        }
        catch
        {
            // Tentativa de fallback via MAUI Battery API
            try
            {
                var state = Microsoft.Maui.Devices.Battery.Default.State;
                if (state != Microsoft.Maui.Devices.BatteryState.NotPresent &&
                    state != Microsoft.Maui.Devices.BatteryState.Unknown)
                {
                    batteryFound = true;
                    int pct = (int)Math.Round(Microsoft.Maui.Devices.Battery.Default.ChargeLevel * 100);
                    specs.BatterySummary = $"{pct}% ({state})";
                }
            }
            catch { }
        }

        if (!batteryFound)
        {
            specs.BatterySummary = DesktopNoBatteryMessage;
            specs.BatteryReportPath = "";
            return;
        }

        // Executa silenciosamente o utilitário nativo para gerar o relatório detalhado em %TEMP%\battery_report.html
        string reportPath = Path.Combine(Path.GetTempPath(), "battery_report.html");
        specs.BatteryReportPath = reportPath;

        _ = Task.Run(() =>
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "powercfg",
                    Arguments = $"/batteryreport /output \"{reportPath}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var proc = Process.Start(psi);
                proc?.WaitForExit(15000);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Falha ao executar powercfg /batteryreport: {ex}");
            }
        });
    }

    private string GetCpuName()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT Name FROM Win32_Processor");
            foreach (ManagementBaseObject obj in searcher.Get())
            {
                using (obj)
                {
                    string name = obj["Name"]?.ToString() ?? "Desconhecido";
                    return CleanCpuName(name);
                }
            }
        }
        catch
        {
            // Fallback silencioso
        }

        return "Desconhecido";
    }

    private static string CleanCpuName(string name)
    {
        name = Regex.Replace(name, @"\(R\)", "", RegexOptions.IgnoreCase);
        name = Regex.Replace(name, @"\(TM\)", "", RegexOptions.IgnoreCase);
        name = Regex.Replace(name, @"\bCPU\b", "", RegexOptions.IgnoreCase);
        name = Regex.Replace(name, @"^\s*(Intel|AMD)\s+", "", RegexOptions.IgnoreCase);
        name = Regex.Replace(name, @"\s{2,}", " ");
        name = Regex.Replace(name, @"\s+@", " @");
        return string.IsNullOrWhiteSpace(name) ? "Desconhecido" : name.Trim();
    }

    private string GetTotalRam()
    {
        try
        {
            long totalBytes = 0;
            using var searcher = new ManagementObjectSearcher("SELECT Capacity FROM Win32_PhysicalMemory");
            foreach (ManagementBaseObject obj in searcher.Get())
            {
                using (obj)
                {
                    if (obj["Capacity"] != null && long.TryParse(obj["Capacity"]?.ToString(), out long capacity))
                    {
                        totalBytes += capacity;
                    }
                }
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

            foreach (ManagementBaseObject obj in searcher.Get())
            {
                using (obj)
                {
                    var gpu = new GpuInfo
                    {
                        Name = obj["Name"]?.ToString() ?? "Desconhecido",
                        DriverVersion = obj["DriverVersion"]?.ToString() ?? ""
                    };

                    if (obj["AdapterRAM"] != null && long.TryParse(obj["AdapterRAM"]?.ToString(), out long vram))
                    {
                        gpu.VideoMemoryBytes = vram;
                    }

                    gpus.Add(gpu);
                }
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

            foreach (ManagementBaseObject obj in searcher.Get())
            {
                using (obj)
                {
                    if (obj["CurrentRefreshRate"] != null &&
                        int.TryParse(obj["CurrentRefreshRate"]?.ToString(), out int hz) && hz > 0)
                    {
                        specs.ScreenRefreshRate = hz;
                    }

                    if (obj["CurrentHorizontalResolution"] != null && obj["CurrentVerticalResolution"] != null)
                    {
                        string? hRes = obj["CurrentHorizontalResolution"]?.ToString();
                        string? vRes = obj["CurrentVerticalResolution"]?.ToString();
                        if (!string.IsNullOrEmpty(hRes) && !string.IsNullOrEmpty(vRes))
                        {
                            specs.ScreenResolution = $"{hRes} x {vRes}";
                        }
                    }

                    if (specs.ScreenRefreshRate > 0 && !string.IsNullOrEmpty(specs.ScreenResolution))
                        break;
                }
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
            foreach (ManagementBaseObject obj in searcher.Get())
            {
                using (obj)
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

                    // Indexação direta segura (sem Dictionary.Add)
                    types[index] = type;
                    index++;
                }
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

            foreach (ManagementBaseObject disk in searcher.Get())
            {
                using (disk)
                {
                    string model = disk["Model"]?.ToString() ?? "";
                    string mediaType = disk["MediaType"]?.ToString() ?? "";

                    if (model.Contains("NVMe", StringComparison.OrdinalIgnoreCase))
                    {
                        return "NVMe";
                    }

                    if (model.Contains("SSD", StringComparison.OrdinalIgnoreCase) ||
                        mediaType.Contains("SSD", StringComparison.OrdinalIgnoreCase) ||
                        mediaType.Contains("Solid", StringComparison.OrdinalIgnoreCase))
                    {
                        return "SSD";
                    }
                }
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

    private List<DiskHealthInfo> GetDiskHealth()
    {
        var list = new List<DiskHealthInfo>();
        try
        {
            var scope = new ManagementScope(@"\\.\root\Microsoft\Windows\Storage");
            scope.Connect();

            using var searcher = new ManagementObjectSearcher(scope,
                new ObjectQuery("SELECT FriendlyName, MediaType, HealthStatus, BusType FROM MSFT_PhysicalDisk"));

            foreach (ManagementObject disk in searcher.Get())
            {
                using (disk)
                {
                    var info = new DiskHealthInfo
                    {
                        Name = disk["FriendlyName"]?.ToString()?.Trim() ?? "Disco",
                        MediaType = MapMediaType(disk["MediaType"], disk["BusType"]),
                        HealthStatus = MapHealthStatus(disk["HealthStatus"])
                    };

                    try
                    {
                        foreach (ManagementObject rel in disk.GetRelated("MSFT_StorageReliabilityCounter"))
                        {
                            using (rel)
                            {
                                if (TryGetInt(rel["Wear"], out int wear) && wear >= 0 && wear <= 100)
                                    info.HealthPercent = 100 - wear;

                                if (TryGetInt(rel["Temperature"], out int temp) && temp > 0 && temp < 150)
                                    info.TemperatureC = temp;

                                if (TryGetLong(rel["PowerOnHours"], out long hours) && hours > 0)
                                    info.PowerOnHours = hours;

                                break;
                            }
                        }
                    }
                    catch
                    {
                        // Contadores indisponíveis (sem privilégio ou drive não suporta)
                    }

                    list.Add(info);
                }
            }
        }
        catch
        {
            // Namespace de Storage indisponível
        }

        return list;
    }

    private static bool TryGetInt(object? value, out int result)
    {
        result = 0;
        return value != null && int.TryParse(value.ToString(), out result);
    }

    private static bool TryGetLong(object? value, out long result)
    {
        result = 0;
        return value != null && long.TryParse(value.ToString(), out result);
    }

    private static string MapHealthStatus(object? value) =>
        TryGetInt(value, out int s)
            ? s switch { 0 => "Saudável", 1 => "Atenção", 2 => "Crítico", _ => "Desconhecido" }
            : "Desconhecido";

    private static string MapMediaType(object? media, object? bus)
    {
        if (TryGetInt(bus, out int b) && b == 17) return "NVMe";
        return TryGetInt(media, out int m)
            ? m switch { 3 => "HDD", 4 => "SSD", 5 => "SCM", _ => "Desconhecido" }
            : "Desconhecido";
    }
}
#endif
