using System;
using System.IO;
using Microsoft.Maui.Controls;
using TechTest.Maui.Services;
using TechTest.Maui.ViewModels;

namespace TechTest.Maui.Views
{
    public partial class BatteryTestPage : ContentPage
    {
        private readonly BatteryTestViewModel _viewModel;

        public BatteryTestPage()
        {
            InitializeComponent();

            var batteryService = IPlatformApplication.Current?.Services?.GetService<IBatteryService>();
            _viewModel = new BatteryTestViewModel(batteryService);
            BindingContext = _viewModel;
        }

        private async void OnOpenReportClicked(object sender, EventArgs e)
        {
            string path = _viewModel.ReportFilePath;
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;

            try
            {
                await Launcher.Default.OpenAsync(new OpenFileRequest
                {
                    File = new ReadOnlyFile(path)
                });
            }
            catch
            {
                // Fallback for direct shell execute
                try
                {
#if WINDOWS
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "explorer.exe",
                        Arguments = $"\"{path}\"",
                        UseShellExecute = true
                    });
#elif MACCATALYST
                    System.Diagnostics.Process.Start("open", $"\"{path}\"");
#endif
                }
                catch
                {
                    await DisplayAlert("Erro", "Não foi possível abrir o relatório automaticamente.", "OK");
                }
            }
        }

        private async void OnBackClicked(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("//MainPage");
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            _viewModel?.RefreshStatus();
        }
    }
}
