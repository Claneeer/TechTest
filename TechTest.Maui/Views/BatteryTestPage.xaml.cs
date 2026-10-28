using System;
using System.IO;
using System.Threading.Tasks;
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
            TechTest.Maui.Controls.ApprovalBar.Attach(this, TechTest.Maui.Services.TestId.Battery);

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

            // 1. Atualiza imediatamente os dados em tempo real
            _viewModel?.RefreshStatus();

            // 2. Dispara automaticamente em segundo plano a leitura detalhada/geração de relatório
            if (_viewModel != null && !_viewModel.IsReportReady && !_viewModel.IsGeneratingReport)
            {
                Task.Run(async () =>
                {
                    await Task.Delay(300); // Dá tempo à interface para renderizar suavemente
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        if (_viewModel.GenerateReportCommand.CanExecute(null))
                        {
                            _viewModel.GenerateReportCommand.Execute(null);
                        }
                    });
                });
            }
        }
    }
}