using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Management;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using TechTest.Helpers;

namespace TechTest.Forms
{
    public class SpecsForm : Form
    {
        private Label _lblLoading;
        private Panel _contentPanel;
        private Button _btnCopy;
        private FullHardwareSpecs _specs;

        [DllImport("dxgi.dll", SetLastError = true)]
        private static extern int CreateDXGIFactory(ref Guid riid, out IDXGIFactory ppFactory);

        public SpecsForm()
        {
            Theme.StyleForm(this, "💻 Especificações do Sistema", 750, 650);
            this.FormBorderStyle = FormBorderStyle.Sizable;
            this.MaximizeBox = true;
            this.MinimumSize = new Size(Theme.S(600), Theme.S(500));
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            BuildUI();
            LoadSpecsAsync();
        }

        private void BuildUI()
        {
            // Header
            var lblTitle = Theme.CreateLabel("Especificações do Sistema", 30, 20, Theme.FontHeader);
            Controls.Add(lblTitle);

            var lblDesc = Theme.CreateLabel(
                "Informações detalhadas sobre todos os componentes de hardware do notebook.",
                30, 55, Theme.FontSmall, Theme.TextSecondary);
            Controls.Add(lblDesc);

            // Copy button
            _btnCopy = Theme.CreateButton("📋 Copiar Tudo", 560, 20, 150, 38);
            _btnCopy.Click += (s, e) => CopyToClipboard();
            _btnCopy.Enabled = false;
            Controls.Add(_btnCopy);

            // Loading indicator
            _lblLoading = Theme.CreateLabel("⏳ Carregando informações do hardware...", 30, 100, Theme.FontBody, Theme.TextMuted);
            Controls.Add(_lblLoading);

            // Scrollable content panel
            _contentPanel = new Panel
            {
                Location = new Point(Theme.S(15), Theme.S(90)),
                Size = new Size(this.ClientSize.Width - Theme.S(30), this.ClientSize.Height - Theme.S(100)),
                AutoScroll = true,
                BackColor = Color.Transparent,
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };
            Controls.Add(_contentPanel);
        }

        private async void LoadSpecsAsync()
        {
            _specs = await Task.Run(() => CollectAllSpecs());

            if (this.IsDisposed || !this.IsHandleCreated) return;

            this.Invoke((Action)(() =>
            {
                _lblLoading.Visible = false;
                _btnCopy.Enabled = true;
                PopulateContent();
            }));
        }

        private void PopulateContent()
        {
            _contentPanel.Controls.Clear();
            int y = 10;

            // === SISTEMA ===
            AddSectionHeader("🖥 Sistema", ref y);
            AddSpecRow("Fabricante / Modelo:", _specs.Manufacturer, ref y);
            AddSpecRow("Nome do PC:", _specs.MachineName, ref y);
            AddSpecRow("Sistema Operacional:", _specs.OSVersion, ref y);
            AddSpecRow("Service Tag / SA/ST:", _specs.ServiceTag, ref y);
            y += Theme.S(12);

            // === PROCESSADOR ===
            AddSectionHeader("⚡ Processador", ref y);
            AddSpecRow("CPU:", _specs.CPU, ref y);
            y += Theme.S(12);

            // === MEMÓRIA ===
            AddSectionHeader("🧠 Memória RAM", ref y);
            AddSpecRow("Total:", _specs.RAM, ref y);
            if (_specs.RamTotalSlots > 0)
            {
                AddSpecRow("Slots:", $"{_specs.RamOccupiedSlots} / {_specs.RamTotalSlots} ({_specs.RamOccupiedSlots} usados)", ref y);
            }
            if (_specs.RamSlots.Count >= 2)
            {
                for (int i = 0; i < _specs.RamSlots.Count; i++)
                {
                    var slot = _specs.RamSlots[i];
                    double gb = slot.CapacityBytes / (1024.0 * 1024.0 * 1024.0);
                    string speedText = slot.SpeedMhz > 0 ? $" ({slot.SpeedMhz} MHz)" : "";
                    string mfrText = !string.IsNullOrEmpty(slot.Manufacturer) && slot.Manufacturer != "Unknown" && slot.Manufacturer != "0x0000" ? $" [{slot.Manufacturer}]" : "";
                    AddSpecRow($"    Slot {slot.Locator}:", $"{Math.Round(gb)} GB{speedText}{mfrText}", ref y);
                }
            }
            y += Theme.S(12);

            // === PLACAS DE VÍDEO ===
            AddSectionHeader("🎮 Placas de Vídeo (GPU)", ref y);
            if (_specs.GPUs.Count == 0)
            {
                AddSpecRow("GPU:", "Não detectada", ref y);
            }
            else
            {
                for (int i = 0; i < _specs.GPUs.Count; i++)
                {
                    var gpu = _specs.GPUs[i];
                    AddSpecRow($"GPU {i + 1}:", gpu.Name, ref y);
                    if (!string.IsNullOrEmpty(gpu.VRAM))
                        AddSpecRow("    VRAM:", gpu.VRAM, ref y);
                    if (!string.IsNullOrEmpty(gpu.DriverVersion))
                        AddSpecRow("    Driver:", gpu.DriverVersion, ref y);
                }
            }
            y += Theme.S(12);

            // === TELA ===
            AddSectionHeader("📺 Tela", ref y);
            AddSpecRow("Resolução:", _specs.ScreenResolution, ref y);
            AddSpecRow("Taxa de Atualização:", _specs.ScreenRefreshRate, ref y);
            y += Theme.S(12);

            // === ARMAZENAMENTO ===
            AddSectionHeader("💾 Armazenamento", ref y);
            if (_specs.Disks.Count == 0)
            {
                AddSpecRow("Disco:", "Não detectado", ref y);
            }
            else
            {
                for (int i = 0; i < _specs.Disks.Count; i++)
                {
                    var disk = _specs.Disks[i];
                    AddSpecRow($"Disco {i + 1}:", $"{disk.Name} ({disk.Type})", ref y);
                    AddSpecRow("    Capacidade:", disk.TotalSize, ref y);
                    
                    if (disk.Partitions.Count > 0)
                    {
                        for (int j = 0; j < disk.Partitions.Count; j++)
                        {
                            var part = disk.Partitions[j];
                            string labelText = !string.IsNullOrEmpty(part.Label) ? $" [{part.Label}]" : "";
                            AddSpecRow($"    Volume {part.DriveLetter}{labelText}:", $"{part.FreeSpace} livres de {part.TotalSize}", ref y);
                        }
                    }
                }
            }
        }

        private void AddSectionHeader(string text, ref int y)
        {
            var lbl = new Label
            {
                Text = text,
                Location = new Point(Theme.S(15), y),
                AutoSize = true,
                Font = Theme.FontButton,
                ForeColor = Theme.Accent,
                BackColor = Color.Transparent
            };
            _contentPanel.Controls.Add(lbl);
            y += Theme.S(26);

            // Separator line
            var line = new Panel
            {
                Location = new Point(Theme.S(15), y),
                Size = new Size(_contentPanel.Width - Theme.S(50), 1),
                BackColor = Theme.Border,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            _contentPanel.Controls.Add(line);
            y += Theme.S(8);
        }

        private void AddSpecRow(string label, string value, ref int y)
        {
            var lblLabel = new Label
            {
                Text = label,
                Location = new Point(Theme.S(25), y),
                Size = new Size(Theme.S(180), Theme.S(22)),
                Font = Theme.FontSmall,
                ForeColor = Theme.TextSecondary,
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.MiddleLeft
            };
            _contentPanel.Controls.Add(lblLabel);

            var lblValue = new Label
            {
                Text = value ?? "—",
                Location = new Point(Theme.S(210), y),
                Size = new Size(_contentPanel.Width - Theme.S(240), Theme.S(22)),
                Font = Theme.FontBody,
                ForeColor = Theme.TextPrimary,
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.MiddleLeft,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            _contentPanel.Controls.Add(lblValue);
            y += Theme.S(24);
        }

        private FullHardwareSpecs CollectAllSpecs()
        {
            var specs = new FullHardwareSpecs();

            try
            {
                // CPU
                using (var searcher = new ManagementObjectSearcher("SELECT Name FROM Win32_Processor"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        string name = obj["Name"]?.ToString() ?? "N/A";
                        name = name.Replace("(R)", "").Replace("(TM)", "").Replace("CPU", "").Trim();
                        // Keep manufacturer prefix for CPU (it's useful info)
                        int atIdx = name.IndexOf('@');
                        if (atIdx > 0)
                        {
                            string freq = name.Substring(atIdx).Trim();
                            name = name.Substring(0, atIdx).Trim() + " " + freq;
                        }
                        // Clean up extra whitespace
                        while (name.Contains("  ")) name = name.Replace("  ", " ");
                        specs.CPU = name;
                        break;
                    }
                }

                // Manufacturer + Model
                using (var searcher = new ManagementObjectSearcher("SELECT Manufacturer, Model FROM Win32_ComputerSystem"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        string manufacturer = obj["Manufacturer"]?.ToString() ?? "N/A";
                        string model = obj["Model"]?.ToString() ?? "N/A";
                        manufacturer = CleanManufacturer(manufacturer);
                        if (!string.IsNullOrEmpty(model) && model != "N/A")
                        {
                            if (model.ToUpper().StartsWith(manufacturer.ToUpper()))
                                model = model.Substring(manufacturer.Length).Trim();
                            specs.Manufacturer = $"{manufacturer} {model}";
                        }
                        else
                        {
                            specs.Manufacturer = manufacturer;
                        }
                        break;
                    }
                }

                // RAM Slots and Details
                int totalSlots = 0;
                try
                {
                    using (var searcher = new ManagementObjectSearcher("SELECT MemoryDevices FROM Win32_PhysicalMemoryArray"))
                    {
                        foreach (ManagementObject obj in searcher.Get())
                        {
                            totalSlots = Convert.ToInt32(obj["MemoryDevices"] ?? 0);
                            break;
                        }
                    }
                }
                catch { }

                // Correct RAM slots using Motherboard (BaseBoard) heuristic to bypass BIOS/SMBIOS generic firmware limits
                try
                {
                    string boardMfr = "";
                    string boardModel = "";
                    using (var searcher = new ManagementObjectSearcher("SELECT Manufacturer, Product FROM Win32_BaseBoard"))
                    {
                        foreach (ManagementObject obj in searcher.Get())
                        {
                            boardMfr = obj["Manufacturer"]?.ToString() ?? "";
                            boardModel = obj["Product"]?.ToString() ?? "";
                            break;
                        }
                    }

                    if (!string.IsNullOrEmpty(boardModel) && totalSlots > 2)
                    {
                        string boardModelUpper = boardModel.ToUpperInvariant();
                        
                        // Chipsets that physically ONLY support 2 slots max:
                        // H610, H510, H410, H310, H110, H81, H61, A320
                        string[] twoSlotChipsets = new string[]
                        {
                            "H610", "H510", "H410", "H310", "H110", "H81", "H61", "A320"
                        };

                        bool matchFound = false;
                        foreach (var chipset in twoSlotChipsets)
                        {
                            if (boardModelUpper.Contains(chipset))
                            {
                                totalSlots = 2;
                                matchFound = true;
                                break;
                            }
                        }

                        if (!matchFound)
                        {
                            // Specific popular 2-slot models/keywords for chipsets that can have 4 slots (like A520, B450, B550, etc.)
                            string[] twoSlotKeywords = new string[]
                            {
                                "-K", "-HDV", "-HVS", "-DX", "DXV4", "A PRO", "-A PRO", "PRO-VH", "MCR-A520M"
                            };

                            foreach (var keyword in twoSlotKeywords)
                            {
                                if (boardModelUpper.Contains(keyword))
                                {
                                    totalSlots = 2;
                                    break;
                                }
                            }
                        }
                    }
                }
                catch { }

                long ramBytes = 0;
                try
                {
                    using (var searcher = new ManagementObjectSearcher("SELECT Capacity, Speed, DeviceLocator, BankLabel, Manufacturer FROM Win32_PhysicalMemory"))
                    {
                        foreach (ManagementObject obj in searcher.Get())
                        {
                            long cap = Convert.ToInt64(obj["Capacity"] ?? 0);
                            ramBytes += cap;

                            int speed = 0;
                            try { speed = Convert.ToInt32(obj["Speed"] ?? 0); } catch { }

                            string locator = obj["DeviceLocator"]?.ToString()?.Trim() ?? "";
                            string bank = obj["BankLabel"]?.ToString()?.Trim() ?? "";
                            string mfr = obj["Manufacturer"]?.ToString()?.Trim() ?? "";

                            specs.RamSlots.Add(new RamSlotDetail
                            {
                                Locator = string.IsNullOrEmpty(locator) ? (string.IsNullOrEmpty(bank) ? $"Slot {specs.RamSlots.Count + 1}" : bank) : locator,
                                CapacityBytes = cap,
                                SpeedMhz = speed,
                                Manufacturer = mfr
                            });
                        }
                    }
                }
                catch { }

                specs.RamOccupiedSlots = specs.RamSlots.Count;
                specs.RamTotalSlots = Math.Max(totalSlots, specs.RamOccupiedSlots);

                if (ramBytes > 0)
                {
                    double ramGb = ramBytes / (1024.0 * 1024.0 * 1024.0);
                    specs.RAM = $"{Math.Round(ramGb)} GB";
                }
                else
                {
                    long ramMB = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / (1024 * 1024);
                    specs.RAM = $"{Math.Round(ramMB / 1024.0)} GB";
                }

                // ALL GPUs (iterate without break!)
                using (var searcher = new ManagementObjectSearcher(
                    "SELECT Name, AdapterRAM, DriverVersion, CurrentRefreshRate, " +
                    "CurrentHorizontalResolution, CurrentVerticalResolution " +
                    "FROM Win32_VideoController"))
                {
                    bool gotScreenInfo = false;
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        var gpu = new GpuDetail();
                        gpu.Name = obj["Name"]?.ToString() ?? "N/A";
                        gpu.DriverVersion = obj["DriverVersion"]?.ToString() ?? "";

                        // Bypasses the 4GB cap using DXGI DedicatedVideoMemory
                        gpu.VRAM = GetGpuVram(gpu.Name);

                        specs.GPUs.Add(gpu);

                        // Get screen info from the first active controller
                        if (!gotScreenInfo)
                        {
                            try
                            {
                                int hz = Convert.ToInt32(obj["CurrentRefreshRate"] ?? 0);
                                if (hz > 0)
                                    specs.ScreenRefreshRate = $"{hz} Hz";

                                int hRes = Convert.ToInt32(obj["CurrentHorizontalResolution"] ?? 0);
                                int vRes = Convert.ToInt32(obj["CurrentVerticalResolution"] ?? 0);
                                if (hRes > 0 && vRes > 0)
                                    specs.ScreenResolution = $"{hRes} x {vRes}";

                                gotScreenInfo = true;
                            }
                            catch { }
                        }
                    }
                }

                // Service Tag
                using (var searcher = new ManagementObjectSearcher("SELECT SerialNumber FROM Win32_Bios"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        specs.ServiceTag = obj["SerialNumber"]?.ToString()?.Trim() ?? "N/A";
                        break;
                    }
                }

                // OS + Machine name
                specs.OSVersion = $"{Environment.OSVersion}";
                specs.MachineName = Environment.MachineName;

                // Storage disks (Physical mapped to Partition mapped to Logical Drive)
                try
                {
                    var mappedDriveLetters = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                    // 1. Get physical media types from MSFT_PhysicalDisk (modern namespace)
                    var diskMediaTypes = new Dictionary<string, string>(); // Model or FriendlyName -> MediaType
                    try
                    {
                        var scope = new ManagementScope(@"\\.\root\Microsoft\Windows\Storage");
                        scope.Connect();
                        using (var searcher = new ManagementObjectSearcher(scope,
                            new ObjectQuery("SELECT FriendlyName, MediaType FROM MSFT_PhysicalDisk")))
                        {
                            foreach (ManagementObject obj in searcher.Get())
                            {
                                string friendlyName = obj["FriendlyName"]?.ToString() ?? "";
                                int mediaType = Convert.ToInt32(obj["MediaType"] ?? 0);
                                string typeStr;
                                switch (mediaType)
                                {
                                    case 3: typeStr = "HDD"; break;
                                    case 4: typeStr = "SSD"; break;
                                    case 5: typeStr = "SCM"; break;
                                    default: typeStr = "SSD"; break; // Modern default is SSD
                                }
                                if (!string.IsNullOrEmpty(friendlyName))
                                {
                                    diskMediaTypes[friendlyName] = typeStr;
                                }
                            }
                        }
                    }
                    catch { }

                    // 2. Query all physical disks from Win32_DiskDrive
                    using (var diskSearcher = new ManagementObjectSearcher("SELECT Model, Size, DeviceID FROM Win32_DiskDrive"))
                    {
                        foreach (ManagementObject diskObj in diskSearcher.Get())
                        {
                            string model = diskObj["Model"]?.ToString() ?? "Disco Físico";
                            long physSize = 0;
                            try { physSize = Convert.ToInt64(diskObj["Size"] ?? 0); } catch { }
                            string deviceId = diskObj["DeviceID"]?.ToString() ?? "";

                            // Determine type (SSD/HDD/NVMe)
                            string type = "SSD"; // default
                            if (model.ToUpper().Contains("NVME")) type = "NVMe (SSD)";
                            else if (model.ToUpper().Contains("SSD")) type = "SSD";
                            else if (model.ToUpper().Contains("HDD") || model.ToUpper().Contains("SATA")) type = "HDD";
                            else
                            {
                                // Match with MSFT_PhysicalDisk media type
                                foreach (var kv in diskMediaTypes)
                                {
                                    if (model.Contains(kv.Key) || kv.Key.Contains(model))
                                    {
                                        type = kv.Value;
                                        break;
                                    }
                                }
                            }

                            var diskDetail = new DiskDetail
                            {
                                Name = model,
                                Type = type,
                                TotalSize = FormatBytes(physSize),
                                FreeSpace = ""
                            };

                            // 3. Map partitions associated with this physical disk
                            if (!string.IsNullOrEmpty(deviceId))
                            {
                                string diskPath = diskObj.Path.RelativePath;
                                if (string.IsNullOrEmpty(diskPath))
                                {
                                    string escapedPath = deviceId.Replace("\\", "\\\\");
                                    diskPath = $"Win32_DiskDrive.DeviceID='{escapedPath}'";
                                }
                                var partitionQuery = $"ASSOCIATORS OF {{{diskPath}}} WHERE AssocClass = Win32_DiskDriveToDiskPartition";
                                using (var partitionSearcher = new ManagementObjectSearcher(partitionQuery))
                                {
                                    foreach (ManagementObject partitionObj in partitionSearcher.Get())
                                    {
                                        string partId = partitionObj["DeviceID"]?.ToString() ?? "";
                                        if (!string.IsNullOrEmpty(partId))
                                        {
                                            string partPath = partitionObj.Path.RelativePath;
                                            if (string.IsNullOrEmpty(partPath))
                                            {
                                                string escapedPart = partId.Replace("\\", "\\\\");
                                                partPath = $"Win32_DiskPartition.DeviceID='{escapedPart}'";
                                            }
                                            var logicalQuery = $"ASSOCIATORS OF {{{partPath}}} WHERE AssocClass = Win32_LogicalDiskToPartition";
                                            using (var logicalSearcher = new ManagementObjectSearcher(logicalQuery))
                                            {
                                                foreach (ManagementObject logicalObj in logicalSearcher.Get())
                                                {
                                                    string driveLetter = logicalObj["DeviceID"]?.ToString() ?? "";
                                                    if (!string.IsNullOrEmpty(driveLetter))
                                                    {
                                                        string cleanLetter = driveLetter.Contains(":") ? driveLetter.Substring(0, driveLetter.IndexOf(':') + 1).ToUpper() : driveLetter.ToUpper();
                                                        mappedDriveLetters.Add(cleanLetter);

                                                        // Get real-time space from DriveInfo
                                                        try
                                                        {
                                                            var driveInfo = new DriveInfo(driveLetter);
                                                            if (driveInfo.IsReady)
                                                            {
                                                                diskDetail.Partitions.Add(new PartitionDetail
                                                                {
                                                                    DriveLetter = driveLetter,
                                                                    Label = driveInfo.VolumeLabel,
                                                                    TotalSize = FormatBytes(driveInfo.TotalSize),
                                                                    FreeSpace = FormatBytes(driveInfo.AvailableFreeSpace)
                                                                });
                                                            }
                                                        }
                                                        catch
                                                        {
                                                            // Fallback WMI space
                                                            long sizeVal = 0, freeVal = 0;
                                                            try { sizeVal = Convert.ToInt64(logicalObj["Size"] ?? 0); } catch { }
                                                            try { freeVal = Convert.ToInt64(logicalObj["FreeSpace"] ?? 0); } catch { }
                                                            diskDetail.Partitions.Add(new PartitionDetail
                                                            {
                                                                DriveLetter = driveLetter,
                                                                Label = logicalObj["VolumeName"]?.ToString() ?? "",
                                                                TotalSize = FormatBytes(sizeVal),
                                                                FreeSpace = FormatBytes(freeVal)
                                                            });
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }

                            specs.Disks.Add(diskDetail);
                        }
                    }

                    // 4. Fallback for unmapped drives (Fixed or Removable drives that were not mapped by physical association)
                    try
                    {
                        var allDrives = DriveInfo.GetDrives();
                        foreach (var d in allDrives)
                        {
                            if (!d.IsReady) continue;
                            if (d.DriveType != DriveType.Fixed && d.DriveType != DriveType.Removable) continue;

                            string letter = d.Name.Contains(":") ? d.Name.Substring(0, d.Name.IndexOf(':') + 1).ToUpper() : d.Name.ToUpper();
                            if (!mappedDriveLetters.Contains(letter))
                            {
                                var diskDetail = new DiskDetail
                                {
                                    Name = $"Volume Local ({letter})",
                                    Type = d.DriveType == DriveType.Removable ? "Removível" : "SSD/HDD",
                                    TotalSize = FormatBytes(d.TotalSize),
                                    FreeSpace = ""
                                };
                                diskDetail.Partitions.Add(new PartitionDetail
                                {
                                    DriveLetter = letter,
                                    Label = d.VolumeLabel,
                                    TotalSize = FormatBytes(d.TotalSize),
                                    FreeSpace = FormatBytes(d.AvailableFreeSpace)
                                });
                                specs.Disks.Add(diskDetail);
                                mappedDriveLetters.Add(letter);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Erro ao carregar discos não mapeados: {ex.Message}");
                    }

                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Erro ao carregar informações de disco: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Erro ao coletar especificações: {ex.Message}");
            }

            return specs;
        }

        private string CleanManufacturer(string manufacturer)
        {
            if (string.IsNullOrEmpty(manufacturer)) return "N/A";
            string m = manufacturer.ToUpper();
            if (m.Contains("DELL")) return "Dell";
            if (m.Contains("LENOVO")) return "Lenovo";
            if (m.Contains("HP") || m.Contains("HEWLETT-PACKARD")) return "HP";
            if (m.Contains("ASUS")) return "ASUS";
            if (m.Contains("ACER")) return "Acer";
            if (m.Contains("APPLE")) return "Apple";
            if (m.Contains("SAMSUNG")) return "Samsung";
            if (m.Contains("POSITIVO")) return "Positivo";
            if (m.Contains("GIGABYTE")) return "Gigabyte";
            if (m.Contains("MSI")) return "MSI";
            return manufacturer;
        }

        private static string GetGpuVram(string gpuName)
        {
            try
            {
                Guid factoryGuid = new Guid("7b7166ec-21c7-44ae-b21a-c9ae321ae369");
                IDXGIFactory factory;
                if (CreateDXGIFactory(ref factoryGuid, out factory) == 0 && factory != null)
                {
                    uint i = 0;
                    IDXGIAdapter adapter;
                    while (factory.EnumAdapters(i, out adapter) == 0)
                    {
                        DXGI_ADAPTER_DESC desc;
                        if (adapter.GetDesc(out desc) == 0)
                        {
                            string descName = desc.Description?.Trim();
                            if (!string.IsNullOrEmpty(descName))
                            {
                                string cleanDesc = CleanGpuNameForMatching(descName);
                                string cleanGpu = CleanGpuNameForMatching(gpuName);
                                if (cleanDesc.Contains(cleanGpu) || cleanGpu.Contains(cleanDesc))
                                {
                                    long bytes = desc.DedicatedVideoMemory.ToInt64();
                                    if (bytes > 0)
                                    {
                                        Marshal.ReleaseComObject(adapter);
                                        Marshal.ReleaseComObject(factory);
                                        return FormatGpuVram(bytes);
                                    }
                                }
                            }
                        }
                        Marshal.ReleaseComObject(adapter);
                        i++;
                    }
                    Marshal.ReleaseComObject(factory);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"DXGI Error: {ex.Message}");
            }

            // Fallback: Try registry
            long regBytes = GetGpuVramFromRegistry(gpuName);
            if (regBytes > 0)
            {
                return FormatGpuVram(regBytes);
            }

            return "N/A";
        }

        private static string CleanGpuNameForMatching(string name)
        {
            if (string.IsNullOrEmpty(name)) return "";
            return name.Replace("(R)", "").Replace("(TM)", "").Replace(" ", "").ToLower();
        }

        private static string FormatGpuVram(long bytes)
        {
            double vramMb = bytes / (1024.0 * 1024.0);
            if (vramMb >= 1024)
                return $"{vramMb / 1024.0:F1} GB";
            return $"{(int)vramMb} MB";
        }

        private static long GetGpuVramFromRegistry(string gpuName)
        {
            try
            {
                using (var baseKey = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}"))
                {
                    if (baseKey != null)
                    {
                        foreach (var subkeyName in baseKey.GetSubKeyNames())
                        {
                            using (var subKey = baseKey.OpenSubKey(subkeyName))
                            {
                                if (subKey != null)
                                {
                                    var desc = subKey.GetValue("DriverDesc")?.ToString();
                                    if (!string.IsNullOrEmpty(desc))
                                    {
                                        string cleanDesc = desc.Replace(" ", "").ToLower();
                                        string cleanGpuName = gpuName.Replace(" ", "").ToLower();
                                        if (cleanDesc.Contains(cleanGpuName) || cleanGpuName.Contains(cleanDesc))
                                        {
                                            var memVal = subKey.GetValue("HardwareInformation.MemorySize");
                                            if (memVal != null)
                                            {
                                                long bytes = 0;
                                                if (memVal is byte[] bytesArr)
                                                {
                                                    if (bytesArr.Length == 4)
                                                        bytes = BitConverter.ToUInt32(bytesArr, 0);
                                                    else if (bytesArr.Length == 8)
                                                        bytes = BitConverter.ToInt64(bytesArr, 0);
                                                }
                                                else
                                                {
                                                    bytes = Convert.ToInt64(memVal);
                                                }
                                                return Math.Abs(bytes);
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch { }
            return 0;
        }



        private string FormatBytes(long bytes)
        {
            if (bytes <= 0) return "—";
            double gb = bytes / (1024.0 * 1024.0 * 1024.0);
            if (gb >= 1000)
                return $"{gb / 1024.0:F2} TB";
            return $"{gb:F1} GB";
        }

        private void CopyToClipboard()
        {
            if (_specs == null) return;
            try
            {
                var lines = new List<string>();
                lines.Add("═══ ESPECIFICAÇÕES DO SISTEMA ═══");
                lines.Add("");
                lines.Add($"Fabricante/Modelo: {_specs.Manufacturer}");
                lines.Add($"Nome do PC: {_specs.MachineName}");
                lines.Add($"SO: {_specs.OSVersion}");
                lines.Add($"Service Tag: {_specs.ServiceTag}");
                lines.Add("");
                lines.Add($"CPU: {_specs.CPU}");
                lines.Add($"RAM: {_specs.RAM}");
                if (_specs.RamTotalSlots > 0)
                {
                    lines.Add($"RAM Slots: {_specs.RamOccupiedSlots} / {_specs.RamTotalSlots} ({_specs.RamOccupiedSlots} usados)");
                    if (_specs.RamSlots.Count >= 2)
                    {
                        for (int i = 0; i < _specs.RamSlots.Count; i++)
                        {
                            var slot = _specs.RamSlots[i];
                            double gb = slot.CapacityBytes / (1024.0 * 1024.0 * 1024.0);
                            string speedText = slot.SpeedMhz > 0 ? $" ({slot.SpeedMhz} MHz)" : "";
                            string mfrText = !string.IsNullOrEmpty(slot.Manufacturer) && slot.Manufacturer != "Unknown" && slot.Manufacturer != "0x0000" ? $" [{slot.Manufacturer}]" : "";
                            lines.Add($"  Slot {slot.Locator}: {Math.Round(gb)} GB{speedText}{mfrText}");
                        }
                    }
                }
                lines.Add("");
                lines.Add("── Placas de Vídeo ──");
                for (int i = 0; i < _specs.GPUs.Count; i++)
                {
                    var gpu = _specs.GPUs[i];
                    lines.Add($"  GPU {i + 1}: {gpu.Name}");
                    if (!string.IsNullOrEmpty(gpu.VRAM)) lines.Add($"    VRAM: {gpu.VRAM}");
                    if (!string.IsNullOrEmpty(gpu.DriverVersion)) lines.Add($"    Driver: {gpu.DriverVersion}");
                }
                lines.Add("");
                lines.Add("── Tela ──");
                lines.Add($"  Resolução: {_specs.ScreenResolution}");
                lines.Add($"  Taxa de Atualização: {_specs.ScreenRefreshRate}");
                lines.Add("");
                lines.Add("── Armazenamento ──");
                for (int i = 0; i < _specs.Disks.Count; i++)
                {
                    var disk = _specs.Disks[i];
                    lines.Add($"  Disco {i + 1}: {disk.Name} ({disk.Type})");
                    lines.Add($"    Capacidade: {disk.TotalSize}");
                    if (disk.Partitions.Count > 0)
                    {
                        for (int j = 0; j < disk.Partitions.Count; j++)
                        {
                            var part = disk.Partitions[j];
                            string labelText = !string.IsNullOrEmpty(part.Label) ? $" [{part.Label}]" : "";
                            lines.Add($"    Volume {part.DriveLetter}{labelText}: {part.FreeSpace} livres de {part.TotalSize}");
                        }
                    }
                }
                lines.Add("");
                lines.Add("═══════════════════════════════════");

                Clipboard.SetText(string.Join(Environment.NewLine, lines));
                MessageBox.Show(
                    "Informações de hardware copiadas com sucesso para a área de transferência!",
                    "Sucesso",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro ao copiar:\n{ex.Message}", "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // Background gradient
            using (var brush = new LinearGradientBrush(
                this.ClientRectangle, Theme.BgDark, Theme.BgPrimary, 90f))
            {
                g.FillRectangle(brush, this.ClientRectangle);
            }
        }
    }

    // === Data classes for SpecsForm ===

    public class FullHardwareSpecs
    {
        public string CPU { get; set; } = "Carregando...";
        public string Manufacturer { get; set; } = "Carregando...";
        public string RAM { get; set; } = "Carregando...";
        public int RamTotalSlots { get; set; }
        public int RamOccupiedSlots { get; set; }
        public List<RamSlotDetail> RamSlots { get; set; } = new List<RamSlotDetail>();
        public string ServiceTag { get; set; } = "Carregando...";
        public string ScreenRefreshRate { get; set; } = "—";
        public string ScreenResolution { get; set; } = "—";
        public string OSVersion { get; set; } = "—";
        public string MachineName { get; set; } = "—";
        public List<GpuDetail> GPUs { get; set; } = new List<GpuDetail>();
        public List<DiskDetail> Disks { get; set; } = new List<DiskDetail>();
    }

    public class RamSlotDetail
    {
        public string Locator { get; set; } = "";
        public long CapacityBytes { get; set; }
        public int SpeedMhz { get; set; }
        public string Manufacturer { get; set; } = "";
    }

    public class GpuDetail
    {
        public string Name { get; set; } = "N/A";
        public string VRAM { get; set; } = "";
        public string DriverVersion { get; set; } = "";
    }

    public class DiskDetail
    {
        public string Name { get; set; } = "N/A";
        public string Type { get; set; } = "—"; // SSD, HDD, NVMe
        public string TotalSize { get; set; } = "—";
        public string FreeSpace { get; set; } = "—";
        public List<PartitionDetail> Partitions { get; set; } = new List<PartitionDetail>();
    }

    public class PartitionDetail
    {
        public string DriveLetter { get; set; } = "";
        public string Label { get; set; } = "";
        public string TotalSize { get; set; } = "—";
        public string FreeSpace { get; set; } = "—";
    }

    // === DXGI COM Interop for accurate VRAM detection ===

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct DXGI_ADAPTER_DESC
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string Description;
        public uint VendorId;
        public uint DeviceId;
        public uint SubSysId;
        public uint Revision;
        public IntPtr DedicatedVideoMemory;
        public IntPtr DedicatedSystemMemory;
        public IntPtr SharedSystemMemory;
        public long AdapterLuid;
    }

    [ComImport]
    [Guid("7b7166ec-21c7-44ae-b21a-c9ae321ae369")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IDXGIFactory
    {
        [PreserveSig] int SetPrivateData(ref Guid Name, uint DataSize, IntPtr pData);
        [PreserveSig] int SetPrivateDataInterface(ref Guid Name, [MarshalAs(UnmanagedType.IUnknown)] object pUnknown);
        [PreserveSig] int GetPrivateData(ref Guid Name, ref uint pDataSize, IntPtr pData);
        [PreserveSig] int GetParent(ref Guid riid, out IntPtr ppParent);
        [PreserveSig] int EnumAdapters(uint Adapter, out IDXGIAdapter ppAdapter);
    }

    [ComImport]
    [Guid("2411e7e1-12ac-4ccf-bd14-9798e8534dc0")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IDXGIAdapter
    {
        [PreserveSig] int SetPrivateData(ref Guid Name, uint DataSize, IntPtr pData);
        [PreserveSig] int SetPrivateDataInterface(ref Guid Name, [MarshalAs(UnmanagedType.IUnknown)] object pUnknown);
        [PreserveSig] int GetPrivateData(ref Guid Name, ref uint pDataSize, IntPtr pData);
        [PreserveSig] int GetParent(ref Guid riid, out IntPtr ppParent);
        [PreserveSig] int EnumOutputs(uint Output, out IntPtr ppOutput);
        [PreserveSig] int GetDesc(out DXGI_ADAPTER_DESC pDesc);
        [PreserveSig] int CheckInterfaceSupport(ref Guid InterfaceName, out long pUMDVersion);
    }
}
