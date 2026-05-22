using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using TechTest.Maui.Services;
using TechTest.Maui.ViewModels;

namespace TechTest.Maui.Views
{
    public partial class SpecsPage : ContentPage
    {
        private readonly IHardwareInfoService _hardwareInfoService;
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

                if (_hardwareInfoService != null)
                {
                    var specs = await _hardwareInfoService.GetHardwareSpecsAsync();

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
                await DisplayAlert("Erro", $"Erro ao carregar especificações:\n{ex.Message}", "OK");
            }
        }

        private async void OnCopyAllClicked(object sender, EventArgs e)
        {
            try
            {
                string text =
                    "--- ESPECIFICAÇÕES DO HARDWARE ---\n" +
                    $"Fabricante: {LblManufacturer.Text}\n" +
                    $"Modelo: {LblModel.Text}\n" +
                    $"SO: {LblOS.Text}\n" +
                    $"Nome da Máquina: {LblMachineName.Text}\n" +
                    $"CPU: {LblCPU.Text}\n" +
                    $"RAM: {LblRAM.Text}\n" +
                    $"Resolução: {LblResolution.Text}\n" +
                    $"Taxa de Atualização: {LblRefreshRate.Text}\n" +
                    $"Service Tag: {LblServiceTag.Text}\n" +
                    "-------------------------------";

                await Clipboard.Default.SetTextAsync(text);
                await DisplayAlert("Sucesso", "Informações de hardware copiadas com sucesso para a área de transferência!", "OK");
            }
            catch (Exception ex)
            {
                await DisplayAlert("Erro", $"Erro ao copiar para a área de transferência:\n{ex.Message}", "OK");
            }
        }

        private async void OnBackClicked(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("//MainPage");
        }
    }

    public class DriveDisplayInfo
    {
        public string Name { get; set; }
        public string DriveType { get; set; }
        public double UsagePercent { get; set; }
        public string UsageText { get; set; }
    }

    public class GpuDisplayInfo
    {
        public string Name { get; set; }
        public string VRAM { get; set; }
    }
}
