using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Maui.Views;
using Microsoft.Maui.Controls;
using Microsoft.Maui.ApplicationModel;

namespace TechTest.Maui.Views
{
    public partial class CameraTestPage : ContentPage
    {
        private CameraView? _camView;
        private CancellationTokenSource? _cts;

        public CameraTestPage()
        {
            InitializeComponent();
            TechTest.Maui.Controls.ApprovalBar.Attach(this, TechTest.Maui.Services.TestId.Camera);
        }

        private async void OnStartCameraClicked(object sender, EventArgs e)
        {
            BtnStartCamera.IsEnabled = false;

            try
            {
                LblPermission.Text = "Verificando...";
                LblPermission.TextColor = Color.FromArgb("#EAB308");

#if WINDOWS
                // 1. No Windows Unpackaged, verifica via WinRT se existe webcam fisica antes de criar o CameraView
                var videoDevices = await Windows.Devices.Enumeration.DeviceInformation.FindAllAsync(
                    Windows.Devices.Enumeration.DeviceClass.VideoCapture);

                if (videoDevices == null || videoDevices.Count == 0)
                {
                    LblPermission.Text = "Sem Hardware";
                    LblPermission.TextColor = Color.FromArgb("#EF4444");
                    LblDevice.Text = "Nenhuma Webcam";
                    LblPlaceholderTitle.Text = "Nenhuma webcam detectada";
                    LblPlaceholderDesc.Text = "Não há webcam conectada ou reconhecida pelo Windows neste computador.";
                    BtnStartCamera.IsEnabled = true;
                    return;
                }

                LblPermission.Text = "Concedida (Win32)";
                LblPermission.TextColor = Color.FromArgb("#22C55E");
#else
                var status = await Permissions.CheckStatusAsync<Permissions.Camera>();
                if (status != PermissionStatus.Granted)
                {
                    status = await Permissions.RequestAsync<Permissions.Camera>();
                }

                if (status != PermissionStatus.Granted)
                {
                    LblPermission.Text = "Negada";
                    LblPermission.TextColor = Color.FromArgb("#EF4444");
                    BtnStartCamera.IsEnabled = true;
                    await DisplayAlert("Permissão Negada", "O acesso à câmera é necessário para executar este teste.", "OK");
                    return;
                }

                LblPermission.Text = "Concedida";
                LblPermission.TextColor = Color.FromArgb("#22C55E");
#endif

                // 2. Instancia o CameraView somente agora que sabemos que existe câmera!
                _cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

                _camView = new CameraView
                {
                    HorizontalOptions = LayoutOptions.Fill,
                    VerticalOptions = LayoutOptions.Fill
                };

                CameraContainer.Content = _camView;
                LayoutPlaceholder.IsVisible = false;
                CameraContainer.IsVisible = true;

                // Aguarda o controle nativo carregar na árvore visual
                await Task.Delay(250);

                var cameras = await _camView.GetAvailableCameras(_cts.Token);
                if (cameras != null && cameras.Count > 0)
                {
                    var camera = cameras.FirstOrDefault();
                    _camView.SelectedCamera = camera;

                    LblDevice.Text = camera?.Name ?? "Câmera Padrão";
                    var res = camera?.SupportedResolutions?.FirstOrDefault();
                    LblResolution.Text = res.HasValue ? $"{res.Value.Width}x{res.Value.Height}" : "Automática";

                    await _camView.StartCameraPreview(_cts.Token);
                    BtnStopCamera.IsEnabled = true;
                }
                else
                {
                    StopCamera();
                    LblPlaceholderTitle.Text = "Câmera Ocupada ou Indisponível";
                    LblPlaceholderDesc.Text = "A câmera foi detectada, mas não pôde ser aberta (verifique se outro app está usando).";
                }
            }
            catch (Exception ex)
            {
                StopCamera();
                LblPermission.Text = "Erro de Driver";
                LblPermission.TextColor = Color.FromArgb("#EF4444");
                await DisplayAlert("Erro na Câmera", $"Não foi possível iniciar a câmera:\n{ex.Message}", "OK");
            }
        }

        private void OnStopCameraClicked(object sender, EventArgs e)
        {
            StopCamera();
        }

        private void StopCamera()
        {
            try
            {
                _cts?.Cancel();
                _cts?.Dispose();
                _cts = null;

                if (_camView != null)
                {
                    _camView.StopCameraPreview();
                    _camView.SelectedCamera = null;
                    _camView.Handler?.DisconnectHandler();
                    _camView = null;
                }

                CameraContainer.Content = null;
                CameraContainer.IsVisible = false;
                LayoutPlaceholder.IsVisible = true;

                BtnStartCamera.IsEnabled = true;
                BtnStopCamera.IsEnabled = false;

                LblDevice.Text = "Desconectado";
                LblResolution.Text = "N/A";
            }
            catch { }
        }

        private async void OnBackClicked(object sender, EventArgs e)
        {
            StopCamera();
            await Shell.Current.GoToAsync("//MainPage");
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            StopCamera();
        }
    }
}