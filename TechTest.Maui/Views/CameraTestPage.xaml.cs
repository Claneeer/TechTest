using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using Microsoft.Maui.ApplicationModel;

namespace TechTest.Maui.Views
{
    public partial class CameraTestPage : ContentPage
    {
        public CameraTestPage()
        {
            InitializeComponent();
        }

        private async void OnStartCameraClicked(object sender, EventArgs e)
        {
            try
            {
                LblPermission.Text = "Verificando...";
                LblPermission.TextColor = Color.FromArgb("#EAB308"); // Yellow

                var status = await Permissions.CheckStatusAsync<Permissions.Camera>();
                if (status != PermissionStatus.Granted)
                {
                    status = await Permissions.RequestAsync<Permissions.Camera>();
                }

                if (status == PermissionStatus.Granted)
                {
                    LblPermission.Text = "Concedida";
                    LblPermission.TextColor = Color.FromArgb("#22C55E"); // Green

                    // Load camera
                    LayoutPlaceholder.IsVisible = false;
                    CamView.IsVisible = true;

                    // Let's get the default camera and bind it
                    var cameras = await CamView.GetAvailableCameras(System.Threading.CancellationToken.None);
                    if (cameras != null && cameras.Count > 0)
                    {
                        var camera = cameras.FirstOrDefault();
                        CamView.SelectedCamera = camera;
                        
                        LblDevice.Text = camera?.Name ?? "Câmera Padrão";
                        LblResolution.Text = "Carregando...";
                    }
                    else
                    {
                        LblDevice.Text = "Câmera Padrão";
                        LblResolution.Text = "Auto";
                    }

                    BtnStartCamera.IsEnabled = false;
                    BtnStopCamera.IsEnabled = true;
                }
                else
                {
                    LblPermission.Text = "Negada";
                    LblPermission.TextColor = Color.FromArgb("#EF4444"); // Red
                    await DisplayAlert("Permissão Negada", "O acesso à câmera é necessário para executar este teste.", "OK");
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Erro na Câmera", $"Não foi possível iniciar a câmera: {ex.Message}", "OK");
                LayoutPlaceholder.IsVisible = true;
                CamView.IsVisible = false;
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
                CamView.SelectedCamera = null;
                CamView.IsVisible = false;
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
