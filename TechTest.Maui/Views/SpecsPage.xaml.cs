using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        private readonly SpecsViewModel _viewModel;

        public SpecsPage()
        {
            InitializeComponent();

            _hardwareInfoService = IPlatformApplication.Current?.Services?.GetService<IHardwareInfoService>();
            _viewModel = new SpecsViewModel(_hardwareInfoService);
            BindingContext = _viewModel;

            LoadSpecsAsync();
        }

        private async void LoadSpecsAsync()
        {
            try
            {
                LoadingIndicator.IsRunning = true;
                LoadingIndicator.IsVisible = true;

                var specs = await _viewModel.LoadSpecsAsync();

                if (specs != null)
                {
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        LblManufacturer.Text = specs.Manufacturer;
                        LblModel.Text = specs.Model;
                        LblOS.Text = specs.OSVersion;
                        LblMachineName.Text = specs.MachineName;
                        LblBattery.Text = specs.BatterySummary;
                        LblCPU.Text = specs.CPU;
                        LblRAM.Text = specs.RAM;
                        LblResolution.Text = string.IsNullOrWhiteSpace(specs.ScreenResolution) ? "Desconhecido" : specs.ScreenResolution;
                        LblRefreshRate.Text = specs.ScreenRefreshRate > 0 ? $"{specs.ScreenRefreshRate} Hz" : "Desconhecido";
                        LblServiceTag.Text = specs.ServiceTag;

                        var gpusList = new List<GpuDisplayInfo>();
                        foreach (var gpu in specs.GPUs)
                        {
                            double vramGb = gpu.VideoMemoryBytes / (1024.0 * 1024 * 1024);
                            string vramText = gpu.VideoMemoryBytes > 0 ? $"{vramGb:F1} GB GDDR" : "Compartilhada";
                            gpusList.Add(new GpuDisplayInfo
                            {
                                Name = gpu.Name,
                                VRAM = vramText
                            });
                        }
                        GpuList.ItemsSource = gpusList;

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
                    LblOS.Text = DeviceInfo.Current.Platform.ToString() + " " + DeviceInfo.Current.VersionString;
                    LblManufacturer.Text = string.IsNullOrWhiteSpace(DeviceInfo.Current.Manufacturer) ? "Desconhecido" : DeviceInfo.Current.Manufacturer;
                    LblModel.Text = string.IsNullOrWhiteSpace(DeviceInfo.Current.Model) ? "Desconhecido" : DeviceInfo.Current.Model;
                    LblBattery.Text = "Bateria não detectada (Computador de Mesa / Sem Bateria)";

                    long ramMb = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / (1024 * 1024);
                    LblRAM.Text = $"{ramMb / 1024.0:F0} GB";

                    var mainDisplay = DeviceDisplay.Current.MainDisplayInfo;
                    LblResolution.Text = $"{mainDisplay.Width} x {mainDisplay.Height}";
                    LblRefreshRate.Text = $"{mainDisplay.RefreshRate} Hz";

                    LblCPU.Text = "Desconhecido";
                    LblServiceTag.Text = "Desconhecido";

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
