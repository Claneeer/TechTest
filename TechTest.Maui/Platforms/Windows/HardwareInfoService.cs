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
            specs.DiskHealth = GetDiskHealth();
            GetBaseBoardInfo(specs);
            specs.BiosSerial = specs.ServiceTag;

            // Se a BIOS não tem serial válido, usa o da placa-mãe como fallback
            if (IsInvalidSerial(specs.ServiceTag) && !IsInvalidSerial(specs.BaseBoardSerial))
                specs.ServiceTag = specs.BaseBoardSerial;

            ApplyIdentityFallbacks(specs);

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
                    string hwId = "";
                    if (obj["HardwareID"] is string[] ids && ids.Length > 0)
                        hwId = ids[0];

                    int code = 0;
                    if (obj["ConfigManagerErrorCode"] != null)
                        int.TryParse(obj["ConfigManagerErrorCode"].ToString(), out code);

                    list.Add(new ProblemDevice
                    {
                        Name = obj["Name"]?.ToString() ?? "Dispositivo desconhecido",
                        DeviceId = obj["DeviceID"]?.ToString() ?? "",
                        HardwareId = hwId,
                        ErrorCode = code
                    });
                    obj.Dispose();
                }
            }
            catch
            {
                // Fallback silencioso
            }
            return list;
        });
    }

    private static readonly string[] PlaceholderValues =
    {
        "To be filled by O.E.M.", "Default string", "System Product Name", "System manufacturer",
        "System Serial Number", "Not Applicable", "Not Specified", "None", "N/A", "O.E.M.",
        "Desconhecido", "Carregando...", "Não disponível", "Não informado"
    };

    private static bool IsPlaceholder(string? v)
    {
        if (string.IsNullOrWhiteSpace(v)) return true;
        foreach (var p in PlaceholderValues)
            if (v.Trim().Equals(p, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    private static string? QueryFirst(string wql, string property)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(wql);
            foreach (ManagementBaseObject obj in searcher.Get())
            {
                var value = obj[property]?.ToString()?.Trim();
                obj.Dispose();
                if (!IsPlaceholder(value)) return value;
            }
        }
        catch
        {
            // WMI indisponível/sem permissão: tenta o próximo fallback
        }
        return null;
    }

    /// <summary>
    /// Win32_ComputerSystem costuma devolver placeholders de OEM. Completa Fabricante, Modelo e Serial
    /// percorrendo: ComputerSystem → ComputerSystemProduct → BaseBoard → SystemEnclosure → BIOS → DeviceInfo (MAUI).
    /// </summary>
    private void ApplyIdentityFallbacks(HardwareSpecs specs)
    {
        if (IsPlaceholder(specs.Manufacturer))
        {
            var m = QueryFirst("SELECT Vendor FROM Win32_ComputerSystemProduct", "Vendor")
                    ?? (IsPlaceholder(specs.BaseBoardManufacturer) ? null : specs.BaseBoardManufacturer)
                    ?? (IsPlaceholder(DeviceInfo.Current.Manufacturer) ? null : DeviceInfo.Current.Manufacturer);
            specs.Manufacturer = m != null ? CleanManufacturerName(m) : "Desconhecido";
        }

        if (IsPlaceholder(specs.Model))
        {
            // Lenovo guarda o nome comercial em ComputerSystemProduct.Version
            var model = QueryFirst("SELECT Version FROM Win32_ComputerSystemProduct", "Version")
                        ?? QueryFirst("SELECT Name FROM Win32_ComputerSystemProduct", "Name")
                        ?? (IsPlaceholder(specs.SystemSku) ? null : specs.SystemSku)
                        ?? (IsPlaceholder(specs.BaseBoardProduct) ? null : specs.BaseBoardProduct)
                        ?? (IsPlaceholder(DeviceInfo.Current.Model) ? null : DeviceInfo.Current.Model);
            specs.Model = model ?? "Desconhecido";
        }

        if (IsPlaceholder(specs.ServiceTag))
        {
            specs.ServiceTag =
                QueryFirst("SELECT IdentifyingNumber FROM Win32_ComputerSystemProduct", "IdentifyingNumber")
                ?? QueryFirst("SELECT SerialNumber FROM Win32_SystemEnclosure", "SerialNumber")
                ?? (IsPlaceholder(specs.BaseBoardSerial) ? null : specs.BaseBoardSerial)
                ?? "Não informado";
        }

        if (IsPlaceholder(specs.BiosSerial))
            specs.BiosSerial = QueryFirst("SELECT SerialNumber FROM Win32_BIOS", "SerialNumber") ?? "Não informado";
    }

    /// <summary>
    /// Saúde S.M.A.R.T.: MSFT_PhysicalDisk (status/tipo) + MSFT_StorageReliabilityCounter (desgaste, temperatura, horas ligado).
    /// Alguns drives só expõem os contadores quando o app roda como administrador.
    /// </summary>
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
                        if (TryGetInt(rel["Wear"], out int wear) && wear >= 0 && wear <= 100)
                            info.HealthPercent = 100 - wear;

                        // 0 °C costuma significar "não informado"
                        if (TryGetInt(rel["Temperature"], out int temp) && temp > 0 && temp < 150)
                            info.TemperatureC = temp;

                        if (TryGetLong(rel["PowerOnHours"], out long hours) && hours > 0)
                            info.PowerOnHours = hours;

                        rel.Dispose();
                        break;
                    }
                }
                catch
                {
                    // Contadores indisponíveis (sem privilégio ou drive não suporta)
                }

                list.Add(info);
                disk.Dispose();
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
        // BusType 17 = NVMe
        if (TryGetInt(bus, out int b) && b == 17) return "NVMe";
        return TryGetInt(media, out int m)
            ? m switch { 3 => "HDD", 4 => "SSD", 5 => "SCM", _ => "Desconhecido" }
            : "Desconhecido";
    }

    private static bool IsInvalidSerial(string? serial)
    {
        if (string.IsNullOrWhiteSpace(serial)) return true;
        string[] invalid = { "To be filled by O.E.M.", "Default string", "System Serial Number", "None",
                             "Não disponível", "Desconhecido", "Carregando..." };
        foreach (var s in invalid)
            if (serial.Equals(s, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    private void GetBaseBoardInfo(HardwareSpecs specs)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT Manufacturer, Product, SerialNumber FROM Win32_BaseBoard");
            foreach (var obj in searcher.Get())
            {
                specs.BaseBoardManufacturer = obj["Manufacturer"]?.ToString()?.Trim() ?? "";
                specs.BaseBoardProduct = obj["Product"]?.ToString()?.Trim() ?? "";
                specs.BaseBoardSerial = obj["SerialNumber"]?.ToString()?.Trim() ?? "";
                obj.Dispose();
                return;
            }
        }
        catch
        {
            // Fallback silencioso
        }
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
        string? rawManufacturer = null;
        string? rawModel = null;

        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT Manufacturer, Model, SystemFamily, SystemSKUNumber FROM Win32_ComputerSystem");
            foreach (var obj in searcher.Get())
            {
                rawManufacturer = obj["Manufacturer"]?.ToString();
                rawModel = obj["Model"]?.ToString();
                specs.SystemFamily = obj["SystemFamily"]?.ToString()?.Trim() ?? "";
                specs.SystemSku = obj["SystemSKUNumber"]?.ToString()?.Trim() ?? "";
                obj.Dispose();
                break;
            }
        }
        catch
        {
            // WMI falhou: segue para o fallback da BIOS abaixo
        }

        // Fallback: Win32_BIOS.Manufacturer quando ComputerSystem vem vazio/genérico
        if (IsPlaceholder(rawManufacturer) || CleanManufacturerName(rawManufacturer) == "Desconhecido")
        {
            var biosVendor = QueryFirst("SELECT Manufacturer FROM Win32_BIOS", "Manufacturer");
            if (!string.IsNullOrWhiteSpace(biosVendor) && CleanManufacturerName(biosVendor) != "Desconhecido")
                rawManufacturer = biosVendor;
        }

        specs.Manufacturer = CleanManufacturerName(rawManufacturer);
        specs.Model = string.IsNullOrWhiteSpace(rawModel) ? "Desconhecido" : rawModel.Trim();
    }

    /// <summary>
    /// Normaliza o fabricante com verificações explícitas (sem dicionário => sem risco de chave duplicada).
    /// Qualquer valor nulo/em branco/não reconhecido resulta em "Desconhecido".
    /// </summary>
    private static string CleanManufacturerName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "Desconhecido";

        string n = name.Trim();

        if (Has(n, "LENOVO")) return "Lenovo";
        else if (Has(n, "Dell")) return "Dell";
        else if (Has(n, "Hewlett") || Has(n, "Hewlett-Packard") || n.Equals("HP", StringComparison.OrdinalIgnoreCase)
                 || n.StartsWith("HP ", StringComparison.OrdinalIgnoreCase) || n.StartsWith("HP,", StringComparison.OrdinalIgnoreCase)
                 || n.StartsWith("HP.", StringComparison.OrdinalIgnoreCase))
            return "HP";
        else if (Has(n, "Acer")) return "Acer";
        else if (Has(n, "ASUS")) return "ASUS";
        else if (Has(n, "SAMSUNG")) return "Samsung";
        else if (Has(n, "Positivo")) return "Positivo";
        else if (Has(n, "Avell")) return "Avell";
        else if (Has(n, "Apple")) return "Apple";
        else if (Has(n, "Micro-Star") || n.Equals("MSI", StringComparison.OrdinalIgnoreCase)) return "MSI";
        else if (Has(n, "GIGABYTE")) return "Gigabyte";

        return "Desconhecido";
    }

    private static bool Has(string value, string token) =>
        value.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;

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
