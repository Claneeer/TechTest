using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Maui.Views;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using TechTest.Maui.Services;

namespace TechTest.Maui.Views
{
    public partial class CameraTestPage : ContentPage
    {
        private static readonly TimeSpan HandlerTimeout = TimeSpan.FromSeconds(5);
        private static readonly TimeSpan PreviewTimeout = TimeSpan.FromSeconds(15);

        private readonly ICameraAccessService _access;
        private CameraView? _camView;
        private CancellationTokenSource? _cts;
        private bool _isStarting;

        public CameraTestPage(ICameraAccessService access)
        {
            InitializeComponent();
            _access = access;
            TechTest.Maui.Controls.ApprovalBar.Attach(this, TechTest.Maui.Services.TestId.Camera);
        }

        private async void OnStartCameraClicked(object sender, EventArgs e)
        {
            if (_isStarting) return;
            _isStarting = true;
            BtnStartCamera.IsEnabled = false;

            try
            {
                await StartCameraAsync();
            }
            catch (OperationCanceledException)
            {
                await StopCameraAsync();
                SetPlaceholder("Tempo esgotado", "A câmera demorou demais para responder. Verifique se outro app a está usando.");
                SetPermission("Timeout", "#EF4444");
            }
            catch (Exception ex)
            {
                await StopCameraAsync();
                SetPermission("Erro de Driver", "#EF4444");
                await DisplayAlert("Erro na Câmera", $"Não foi possível iniciar a câmera:\n{ex.Message}", "OK");
            }
            finally
            {
                _isStarting = false;
                if (_camView == null) BtnStartCamera.IsEnabled = true;
            }
        }

        private async Task StartCameraAsync()
        {
            // Sempre recomeça do zero: evita CameraView "meio inicializada" de execuções anteriores.
            await StopCameraAsync();

            SetPermission("Verificando...", "#EAB308");

            // 1) Permissões / hardware (prompt do SO precisa da thread principal).
            var access = await MainThread.InvokeOnMainThreadAsync(() => _access.EnsureAccessAsync());
            LblDevice.Text = access.DeviceName ?? "Desconectado";

            switch (access.State)
            {
                case CameraAccessState.NoDevice:
                    SetPermission("Sem Hardware", "#EF4444");
                    SetPlaceholder("Nenhuma webcam detectada", access.Message);
                    return;

                case CameraAccessState.Denied:
                    SetPermission("Negada", "#EF4444");
                    SetPlaceholder("Acesso à câmera negado", access.Message);
                    if (await DisplayAlert("Permissão Negada",
                            $"{access.Message}\n\nDeseja abrir as configurações de privacidade?", "Abrir", "Cancelar"))
                        await _access.OpenPrivacySettingsAsync();
                    return;

                case CameraAccessState.Error:
                    SetPermission("Erro", "#EF4444");
                    SetPlaceholder("Falha na verificação", access.Message);
                    return;
            }

            SetPermission("Concedida", "#22C55E");

            // 2) Cria o CameraView, insere na árvore visual e AGUARDA o handler nativo (sem Task.Delay "mágico").
            _cts = new CancellationTokenSource(PreviewTimeout);
            var token = _cts.Token;

            var camView = new CameraView
            {
                HorizontalOptions = LayoutOptions.Fill,
                VerticalOptions = LayoutOptions.Fill,
                MinimumHeightRequest = 300,
                MinimumWidthRequest = 400
            };

            var handlerReady = WaitForHandlerAsync(camView, token);

            // Container precisa estar visível ANTES de o handler ser criado, senão a superfície nativa tem tamanho 0.
            LayoutPlaceholder.IsVisible = false;
            CameraContainer.IsVisible = true;
            CameraContainer.Content = camView;
            _camView = camView;

            await handlerReady;

            // 3) Enumera câmeras e inicia o preview na thread principal.
            var cameras = await MainThread.InvokeOnMainThreadAsync<System.Collections.Generic.IReadOnlyList<CommunityToolkit.Maui.Core.CameraInfo>>(
                async () => await camView.GetAvailableCameras(token));
            var camera = cameras?.FirstOrDefault();

            if (camera == null)
            {
                await StopCameraAsync();
                SetPlaceholder("Câmera Ocupada ou Indisponível",
                    "A câmera foi detectada, mas não pôde ser aberta (verifique se outro app está usando).");
                return;
            }

            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                camView.SelectedCamera = camera;
                await camView.StartCameraPreview(token);
            });

            LblDevice.Text = camera.Name ?? "Câmera Padrão";
            var res = camera.SupportedResolutions?.FirstOrDefault();
            LblResolution.Text = res.HasValue ? $"{res.Value.Width}x{res.Value.Height}" : "Automática";
            BtnStopCamera.IsEnabled = true;
        }

        /// <summary>Completa quando o Handler nativo do controle existe (ou lança em caso de timeout/cancelamento).</summary>
        private static async Task WaitForHandlerAsync(VisualElement view, CancellationToken token)
        {
            if (view.Handler != null) return;

            var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            void OnChanged(object? s, EventArgs e)
            {
                if (view.Handler != null) tcs.TrySetResult();
            }

            view.HandlerChanged += OnChanged;
            try
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
                timeout.CancelAfter(HandlerTimeout);
                using var reg = timeout.Token.Register(() => tcs.TrySetCanceled(timeout.Token));
                await tcs.Task;
            }
            finally
            {
                view.HandlerChanged -= OnChanged;
            }
        }

        private async void OnStopCameraClicked(object sender, EventArgs e) => await StopCameraAsync();

        private async Task StopCameraAsync()
        {
            var cam = _camView;
            var cts = _cts;
            _camView = null;
            _cts = null;

            try { cts?.Cancel(); } catch { }

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                try
                {
                    if (cam != null)
                    {
                        try { cam.StopCameraPreview(); } catch { }
                        try { cam.SelectedCamera = null; } catch { }
                    }

                    CameraContainer.Content = null;   // remove da árvore visual
                    try { cam?.Handler?.DisconnectHandler(); } catch { } // libera a webcam nativa
                    CameraContainer.IsVisible = false;
                    LayoutPlaceholder.IsVisible = true;

                    BtnStartCamera.IsEnabled = true;
                    BtnStopCamera.IsEnabled = false;
                    LblDevice.Text = "Desconectado";
                    LblResolution.Text = "N/A";
                }
                catch { /* UI já descartada */ }
            });

            try { cts?.Dispose(); } catch { }
        }

        private void SetPermission(string text, string colorHex)
        {
            LblPermission.Text = text;
            LblPermission.TextColor = Color.FromArgb(colorHex);
        }

        private void SetPlaceholder(string title, string description)
        {
            LblPlaceholderTitle.Text = title;
            LblPlaceholderDesc.Text = description;
            LayoutPlaceholder.IsVisible = true;
            CameraContainer.IsVisible = false;
        }

        private async void OnBackClicked(object sender, EventArgs e)
        {
            await StopCameraAsync();
            await Shell.Current.GoToAsync("//MainPage");
        }

        protected override async void OnDisappearing()
        {
            base.OnDisappearing();
            await StopCameraAsync();
        }
    }
}