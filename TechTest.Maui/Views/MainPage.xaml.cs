using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using TechTest.Maui.Helpers;
using TechTest.Maui.Services;
using TechTest.Maui.ViewModels;

namespace TechTest.Maui.Views
{
    public partial class MainPage : ContentPage
    {
        private readonly IHardwareInfoService? _hardwareInfoService;
        private readonly MainViewModel _viewModel;
        private readonly Dictionary<TestId, Label> _badges = new();

        public MainPage()
        {
            InitializeComponent();

            _hardwareInfoService = IPlatformApplication.Current?.Services?.GetService<IHardwareInfoService>();
            _viewModel = new MainViewModel(_hardwareInfoService);
            BindingContext = _viewModel;

            PickerScale.SelectedIndex = 0;
            LoadSystemInfoAsync();
            BuildStatusBadges();
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            TestSession.Current.Changed += RefreshBadges;
            RefreshBadges();
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            TestSession.Current.Changed -= RefreshBadges;
        }

        private void BuildStatusBadges()
        {
            var map = new (Frame Card, TestId Id)[]
            {
                (CardMicrofone, TestId.Microphone),
                (CardAudio, TestId.Audio),
                (CardWebcam, TestId.Camera),
                (CardDeadPixel, TestId.DeadPixel),
                (CardTeclado, TestId.Keyboard),
                (CardTouchpad, TestId.Touchpad),
                (CardBateria, TestId.Battery),
            };

            foreach (var (card, id) in map)
            {
                if (card.Content is not Grid grid) continue;

                grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
                var badge = new Label
                {
                    FontSize = 10,
                    FontAttributes = FontAttributes.Bold,
                    HorizontalOptions = LayoutOptions.Center,
                    Padding = new Thickness(10, 2)
                };
                grid.Add(badge, 0, 3);
                _badges[id] = badge;
            }
        }

        private void RefreshBadges()
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                foreach (var (id, label) in _badges)
                {
                    (label.Text, label.TextColor) = TestSession.Current.GetStatus(id) switch
                    {
                        TestStatus.Approved => ("✅ Aprovado", Color.FromArgb("#22C55E")),
                        TestStatus.Rejected => ("❌ Reprovado", Color.FromArgb("#EF4444")),
                        _ => ("⏳ Pendente", Color.FromArgb("#9494B8"))
                    };
                }

                BtnSequential.Text = TestSession.Current.SequentialMode
                    ? "⏹ Parar Diagnóstico Sequencial"
                    : "▶️ Iniciar Diagnóstico Sequencial";
            });
        }

        private async void OnDriversCardTapped(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("//DriverUpdatePage");
        }

        private async void OnStartSequentialClicked(object sender, EventArgs e)
        {
            var session = TestSession.Current;
            if (session.SequentialMode)
            {
                session.StopSequential();
                return;
            }

            session.StartSequential();
            var first = session.FirstNotApproved() ?? TestSession.Order[0];
            await Shell.Current.GoToAsync($"//{TestSession.RouteOf(first)}");
        }

        private async void OnExportReportClicked(object sender, EventArgs e)
        {
            try
            {
                string? tech = await DisplayPromptAsync("Laudo Técnico", "Nome do técnico responsável:",
                    "Gerar", "Cancelar", Environment.UserName, -1, null, Environment.UserName);
                if (tech == null) return;

                string? notes = await DisplayPromptAsync("Laudo Técnico", "Observações (opcional):",
                    "OK", "Pular", "", -1, null, "");

                BtnReport.IsEnabled = false;
                BtnReport.Text = "⏳ Gerando laudo...";

                var result = await new ReportService().GenerateAsync(tech, notes ?? "");

                bool open = await DisplayAlert("Laudo gerado",
                    $"Veredito: {result.Verdict}\n\nHTML: {result.HtmlPath}\nJSON: {result.JsonPath}",
                    "Abrir HTML", "Fechar");
                if (open)
                {
#if WINDOWS
                    Process.Start(new ProcessStartInfo { FileName = result.HtmlPath, UseShellExecute = true });
#else
                    await Launcher.Default.OpenAsync(new Uri(result.HtmlPath));
#endif
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Erro", $"Não foi possível gerar o laudo:\n{ex}", "OK");
            }
            finally
            {
                BtnReport.IsEnabled = true;
                BtnReport.Text = "📄 Exportar Laudo Técnico";
            }
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
                Frame? frame = FindParentFrame(view);
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
                Frame? frame = FindParentFrame(view);
                if (frame != null)
                {
                    frame.BackgroundColor = Color.FromArgb("#1C1C38");
                    frame.BorderColor = Color.FromArgb("#32325A");
                }
            }
        }

        private Frame? FindParentFrame(View view)
        {
            if (view is Frame frame) return frame;
            Element? parent = view.Parent;
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

        private async void OnResolutionMenuClicked(object sender, EventArgs e)
        {
            string? choice = await DisplayActionSheet(
                "Selecionar Resolução da Janela (16:9 / 16:10)",
                "Cancelar",
                null,
                "16:9 — 1366 x 768 (HD / WXGA)",
                "16:9 — 1920 x 1080 (Full HD)",
                "16:9 — 2560 x 1440 (QHD / 2K)",
                "16:9 — 3840 x 2160 (UHD / 4K)",
                "16:10 — 1920 x 1200 (WUXGA)");

            switch (choice)
            {
                case "16:9 — 1366 x 768 (HD / WXGA)":
                    WindowSizeService.SetResolution(1366, 768);
                    break;
                case "16:9 — 1920 x 1080 (Full HD)":
                    WindowSizeService.SetResolution(1920, 1080);
                    break;
                case "16:9 — 2560 x 1440 (QHD / 2K)":
                    WindowSizeService.SetResolution(2560, 1440);
                    break;
                case "16:9 — 3840 x 2160 (UHD / 4K)":
                    WindowSizeService.SetResolution(3840, 2160);
                    break;
                case "16:10 — 1920 x 1200 (WUXGA)":
                    WindowSizeService.SetResolution(1920, 1200);
                    break;
            }
        }

        private async void OnFurMarkClicked(object sender, EventArgs e)
        {
            await _viewModel.OnLaunchFurMarkAsync();
        }
    }
}
