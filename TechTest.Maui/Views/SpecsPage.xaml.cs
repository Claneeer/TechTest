using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Devices;
using TechTest.Maui.Services;
using TechTest.Maui.ViewModels;

namespace TechTest.Maui.Views
{
    public partial class SpecsPage : ContentPage
    {
        private readonly IHardwareInfoService? _hardwareInfoService;
        private readonly IBatteryService? _batteryService;
        private readonly IClipboardService _clipboardService;
        private readonly SpecsViewModel _viewModel;
        private HardwareSpecs? _specs;

        public SpecsPage()
        {
            InitializeComponent();

            var services = IPlatformApplication.Current?.Services;
            _hardwareInfoService = services?.GetService<IHardwareInfoService>();
            _batteryService = services?.GetService<IBatteryService>();
            _clipboardService = services?.GetService<IClipboardService>() ?? new ClipboardService();

            _viewModel = new SpecsViewModel(_hardwareInfoService, _batteryService, _clipboardService);
            BindingContext = _viewModel;

            LoadSpecsAsync();
        }

        private async void LoadSpecsAsync()
        {
            try
            {
                LoadingIndicator.IsRunning = true;
                LoadingIndicator.IsVisible = true;

                if (_hardwareInfoService != null)
                {
                    var specs = await _hardwareInfoService.GetHardwareSpecsAsync();
                    _specs = specs;
                    _viewModel.Specs = specs;

                    BatteryStatus? batteryStatus = null;
                    BatteryReport? batteryReport = null;

                    if (_batteryService != null)
                    {
                        try
                        {
                            batteryStatus = await Task.Run(() => _batteryService.GetCurrentStatus());
                            _viewModel.BatteryStatus = batteryStatus;

                            if (batteryStatus.HasBattery)
                            {
                                var reportTask = _batteryService.GenerateReportAsync();
                                if (await Task.WhenAny(reportTask, Task.Delay(TimeSpan.FromSeconds(15))) == reportTask)
                                {
                                    batteryReport = await reportTask;
                                    _viewModel.BatteryReport = batteryReport;
                                }
                            }
                        }
                        catch
                        {
                            // Continua exibindo as demais especificações caso a leitura da bateria falhe
                        }
                    }

                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        LblManufacturer.Text = specs.Manufacturer;
                        LblModel.Text = specs.Model;
                        LblOS.Text = specs.OSVersion;
                        LblMachineName.Text = specs.MachineName;
                        LblCPU.Text = specs.CPU;
                        LblRAM.Text = specs.RAM;
                        LblResolution.Text = specs.ScreenResolution;
                        LblRefreshRate.Text = specs.ScreenRefreshRate > 0 ? $"{specs.ScreenRefreshRate} Hz" : "Desconhecido";
                        LblServiceTag.Text = specs.ServiceTag;

                        // Map GPUs
                        var gpusList = new List<GpuDisplayInfo>();
                        foreach (var gpu in specs.GPUs)
                        {
                            double vramGb = gpu.VideoMemoryBytes / (1024.0 * 1024 * 1024);
                            string vramText = gpu.VideoMemoryBytes > 0 ? $"{vramGb:F1} GB GDDR" : "Desconhecido";
                            gpusList.Add(new GpuDisplayInfo
                            {
                                Name = gpu.Name,
                                VRAM = vramText
                            });
                        }
                        GpuList.ItemsSource = gpusList;

                        // Map Drives
                        var drivesList = new List<DriveDisplayInfo>();
                        foreach (var drive in specs.StorageDevices)
                        {
                            drivesList.Add(new DriveDisplayInfo
                            {
                                Name = $"{drive.Name} ({drive.DriveFormat})",
                                DriveType = drive.Type,
                                UsagePercent = drive.UsedPercentage / 100.0,
                                UsageText = $"{drive.FormattedUsedSpace} / {drive.FormattedTotalSize} ({drive.FormattedFreeSpace} livre)"
                            });
                        }
                        DriveList.ItemsSource = drivesList;

                        SmartList.ItemsSource = specs.DiskHealth;
                        SectionSmart.IsVisible = specs.DiskHealth != null && specs.DiskHealth.Count > 0;
                        LblSmartNote.IsVisible = specs.DiskHealth != null && specs.DiskHealth.Any(d => !d.HealthPercent.HasValue || !d.TemperatureC.HasValue);

                        // Map Battery
                        if (batteryStatus != null)
                        {
                            if (!batteryStatus.HasBattery)
                            {
                                LblBatteryStatus.Text = BatteryStatus.NoBatteryMessage;
                                LblBatteryPower.Text = batteryStatus.PowerSource;
                                LblBatteryHealth.Text = "N/A";
                                LblBatteryCapacity.Text = "N/A";
                            }
                            else
                            {
                                LblBatteryStatus.Text = batteryStatus.StatusText;
                                LblBatteryPower.Text = $"{batteryStatus.PowerSource} ({batteryStatus.ChargePercent}%)";
                                LblBatteryHealth.Text = batteryReport?.Health ?? "Não disponível";
                                LblBatteryCapacity.Text = batteryReport != null
                                    ? $"{batteryReport.FullChargeCapacity} / {batteryReport.DesignCapacity} (Ciclos: {batteryReport.CycleCount})"
                                    : "Não disponível";
                            }
                            SectionBattery.IsVisible = true;
                        }

                        SectionSistema.IsVisible = true;
                        SectionCPU.IsVisible = true;
                        SectionRAM.IsVisible = true;
                        SectionGPU.IsVisible = specs.GPUs != null && specs.GPUs.Count > 0;
                        SectionDisplay.IsVisible = true;
                        SectionStorage.IsVisible = true;
                        SectionServiceTag.IsVisible = true;
                        BtnCopyAll.IsVisible = true;

                        LoadingIndicator.IsRunning = false;
                        LoadingIndicator.IsVisible = false;
                    });
                }
                else
                {
                    LblMachineName.Text = Environment.MachineName;
                    LblOS.Text = DeviceInfo.Current.Platform + " " + DeviceInfo.Current.VersionString;
                    LblManufacturer.Text = DeviceInfo.Current.Manufacturer;
                    LblModel.Text = DeviceInfo.Current.Model;

                    long ramMb = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / (1024 * 1024);
                    LblRAM.Text = $"{ramMb / 1024.0:F0} GB";

                    var mainDisplay = DeviceDisplay.Current.MainDisplayInfo;
                    LblResolution.Text = $"{mainDisplay.Width} x {mainDisplay.Height}";
                    LblRefreshRate.Text = $"{mainDisplay.RefreshRate} Hz";

                    LblCPU.Text = "Requer serviço nativo";
                    LblServiceTag.Text = "Requer serviço nativo";

                    var drives = new List<DriveDisplayInfo>();
                    foreach (var drive in DriveInfo.GetDrives().Where(d => d.IsReady))
                    {
                        double totalGb = drive.TotalSize / (1024.0 * 1024 * 1024);
                        double freeGb = drive.AvailableFreeSpace / (1024.0 * 1024 * 1024);
                        double usedGb = totalGb - freeGb;
                        drives.Add(new DriveDisplayInfo
                        {
                            Name = drive.Name,
                            DriveType = drive.DriveType.ToString(),
                            UsagePercent = totalGb > 0 ? usedGb / totalGb : 0,
                            UsageText = $"{usedGb:F1} GB / {totalGb:F1} GB ({freeGb:F1} GB livre)"
                        });
                    }
                    DriveList.ItemsSource = drives;

                    var fallbackSpecs = new HardwareSpecs
                    {
                        Manufacturer = LblManufacturer.Text,
                        Model = LblModel.Text,
                        ServiceTag = LblServiceTag.Text,
                        OSVersion = LblOS.Text,
                        MachineName = LblMachineName.Text,
                        CPU = LblCPU.Text,
                        RAM = LblRAM.Text,
                        ScreenResolution = LblResolution.Text
                    };
                    _specs = fallbackSpecs;
                    _viewModel.Specs = fallbackSpecs;

                    SectionSistema.IsVisible = true;
                    SectionCPU.IsVisible = true;
                    SectionRAM.IsVisible = true;
                    SectionDisplay.IsVisible = true;
                    SectionStorage.IsVisible = true;
                    SectionServiceTag.IsVisible = true;
                    BtnCopyAll.IsVisible = true;

                    LoadingIndicator.IsRunning = false;
                    LoadingIndicator.IsVisible = false;
                }
            }
            catch (Exception ex)
            {
                LoadingIndicator.IsRunning = false;
                LoadingIndicator.IsVisible = false;
                await DisplayAlert("Erro", $"Erro ao carregar especificações:\n{ex}", "OK");
            }
        }

        private async void OnCopyAllClicked(object sender, EventArgs e)
        {
            await _viewModel.ExecuteCopyAllInfoAsync(_specs);
        }

        private async void OnFurMarkClicked(object sender, EventArgs e)
        {
            BtnFurMark.IsEnabled = false;
            try
            {
                var furMark = IPlatformApplication.Current?.Services?.GetService<IFurMarkService>();
                if (furMark == null) { ShowToolStatus("Serviço do FurMark indisponível.", false); return; }

                var result = await furMark.LaunchAsync();
                ShowToolStatus(result.Message, result.Success);
            }
            catch (Exception ex)
            {
                ShowToolStatus($"Erro: {ex}", false);
            }
            finally
            {
                BtnFurMark.IsEnabled = true;
            }
        }

        private async void OnNetworkConfigClicked(object sender, EventArgs e)
        {
            BtnNetworkXml.IsEnabled = false;
            try
            {
                var network = IPlatformApplication.Current?.Services?.GetService<INetworkConfigService>();
                if (network == null) { ShowToolStatus("Serviço de rede indisponível.", false); return; }

                string path = await network.EnsureConfigAsync();
                var cfg = await network.LoadAsync(); // valida o XML (XDocument) antes de informar sucesso
                ShowToolStatus($"XML válido: {path}\nPing: {string.Join(", ", cfg.PingHosts)} | HTTP: {cfg.HttpEndpoint}", true);
            }
            catch (InvalidDataException ex)
            {
                ShowToolStatus($"XML inválido:\n{ex.Message}", false);
            }
            catch (Exception ex)
            {
                ShowToolStatus($"Erro: {ex}", false);
            }
            finally
            {
                BtnNetworkXml.IsEnabled = true;
            }
        }

        private void ShowToolStatus(string message, bool success)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                LblToolStatus.Text = message;
                LblToolStatus.TextColor = Color.FromArgb(success ? "#22C55E" : "#EF4444");
                LblToolStatus.IsVisible = true;
            });
        }

        private async void OnDriversClicked(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("//DriverUpdatePage");
        }

        private async void OnBackClicked(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("//MainPage");
        }
    }

    public class DriveDisplayInfo
    {
        public string Name { get; set; } = "";
        public string DriveType { get; set; } = "";
        public double UsagePercent { get; set; }
        public string UsageText { get; set; } = "";
    }

    public class GpuDisplayInfo
    {
        public string Name { get; set; } = "";
        public string VRAM { get; set; } = "";
    }
}
