using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using TechTest.Maui.Helpers;
using TechTest.Maui.Services;
using TechTest.Maui.ViewModels;

namespace TechTest.Maui.Views
{
    public partial class MainPage : ContentPage
    {
        private readonly IHardwareInfoService _hardwareInfoService;

        public MainPage()
        {
            InitializeComponent();

            _hardwareInfoService = IPlatformApplication.Current?.Services?.GetService<IHardwareInfoService>();
            BindingContext = new MainViewModel();

            PickerScale.SelectedIndex = 0;
            LoadSystemInfoAsync();
        }

        private async void LoadSystemInfoAsync()
        {
            try
            {
                if (_hardwareInfoService != null)
                {
                    string info = await Task.Run(() => _hardwareInfoService.GetSystemSummary());
                    LblSysInfo.Text = info;
                }
                else
                {
                    string machineName = Environment.MachineName;
                    long ramMb = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / (1024 * 1024);
                    LblSysInfo.Text = $"💻 {machineName}  |  🧠 {ramMb:N0} MB RAM";
                }
            }
            catch (Exception ex)
            {
                LblSysInfo.Text = $"💻 {Environment.MachineName} | Erro ao carregar info: {ex.Message}";
            }
        }

        private async void OnMicrofoneCardTapped(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("//MicrophoneTestPage");
        }

        private async void OnAudioCardTapped(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("//AudioTestPage");
        }

        private async void OnWebcamCardTapped(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("//CameraTestPage");
        }

        private async void OnDeadPixelCardTapped(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("//DeadPixelTestPage");
        }

        private async void OnKeyboardCardTapped(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("//KeyboardTestPage");
        }

        private async void OnTouchpadCardTapped(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("//TouchpadTestPage");
        }

        private async void OnBatteryCardTapped(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("//BatteryTestPage");
        }

        private async void OnSpecsCardTapped(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("//SpecsPage");
        }

        private void OnCardPointerEntered(object sender, PointerEventArgs e)
        {
            if (sender is View view)
            {
                Frame frame = FindParentFrame(view);
                if (frame != null)
                {
                    frame.BackgroundColor = Color.FromArgb("#26264C");
                    frame.BorderColor = Color.FromArgb("#6366F1");
                }
            }
        }

        private void OnCardPointerExited(object sender, PointerEventArgs e)
        {
            if (sender is View view)
            {
                Frame frame = FindParentFrame(view);
                if (frame != null)
                {
                    frame.BackgroundColor = Color.FromArgb("#1C1C38");
                    frame.BorderColor = Color.FromArgb("#32325A");
                }
            }
        }

        private Frame FindParentFrame(View view)
        {
            if (view is Frame frame) return frame;
            Element parent = view.Parent;
            while (parent != null)
            {
                if (parent is Frame f) return f;
                parent = parent.Parent;
            }
            return null;
        }

        private void OnScalePickerChanged(object sender, EventArgs e)
        {
            if (PickerScale.SelectedIndex < 0) return;

            double scale = PickerScale.SelectedIndex switch
            {
                0 => -1.0,
                1 => 0.75,
                2 => 1.0,
                3 => 1.25,
                4 => 1.5,
                _ => -1.0
            };

            ResponsiveHelper.SetScale(scale);
        }

        private async void OnFurMarkClicked(object sender, EventArgs e)
        {
#if WINDOWS
            try
            {
                string appDir = AppDomain.CurrentDomain.BaseDirectory;
                string furmarkPath = Path.Combine(appDir, "FurMark.exe");

                if (!File.Exists(furmarkPath))
                    furmarkPath = Path.Combine(appDir, "furmark.exe");
                if (!File.Exists(furmarkPath))
                    furmarkPath = Path.Combine(appDir, "FurMark_GUI.exe");

                if (File.Exists(furmarkPath))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = furmarkPath,
                        UseShellExecute = true,
                        WorkingDirectory = appDir
                    });
                }
                else
                {
                    await DisplayAlert("FurMark não encontrado",
                        $"FurMark não encontrado na pasta do programa.\n\n" +
                        $"Coloque o arquivo 'FurMark.exe' na mesma pasta do TechTest:\n{appDir}",
                        "OK");
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Erro", $"Erro ao abrir FurMark:\n{ex.Message}", "OK");
            }
#else
            await DisplayAlert("Indisponível", "FurMark está disponível apenas no Windows.", "OK");
#endif
        }
    }
}
