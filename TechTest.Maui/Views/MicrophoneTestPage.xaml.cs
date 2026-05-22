using System;
using System.Diagnostics;
using System.IO;
using Microsoft.Maui.Controls;
using TechTest.Maui.Services;
using TechTest.Maui.ViewModels;

namespace TechTest.Maui.Views
{
    public partial class MicrophoneTestPage : ContentPage
    {
        private readonly MicrophoneTestViewModel _viewModel;

        public MicrophoneTestPage()
        {
            InitializeComponent();

            var audioService = IPlatformApplication.Current?.Services?.GetService<IAudioCaptureService>();
            var audioPlayback = IPlatformApplication.Current?.Services?.GetService<IAudioPlaybackService>();
            _viewModel = new MicrophoneTestViewModel(audioService, audioPlayback);
            BindingContext = _viewModel;

            _viewModel.PropertyChanged += (s, e) =>
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    switch (e.PropertyName)
                    {
                        case nameof(_viewModel.StatusText):
                            LblStatus.Text = _viewModel.StatusText;
                            LblStatus.TextColor = _viewModel.StatusColor;
                            break;
                        case nameof(_viewModel.VolumeLevel):
                            VolumeMeter.Progress = _viewModel.VolumeLevel;
                            LblVolume.Text = $"{(int)(_viewModel.VolumeLevel * 100)}%";
                            UpdateVolumeMeterColor(_viewModel.VolumeLevel);
                            WaveformView.Peak = (float)_viewModel.VolumeLevel;
                            break;
                        case nameof(_viewModel.IsCapturing):
                            BtnStart.IsEnabled = !_viewModel.IsCapturing;
                            BtnRecord.IsEnabled = _viewModel.IsCapturing && !_viewModel.IsRecording;
                            BtnStop.IsEnabled = _viewModel.IsCapturing;
                            WaveformView.IsActive = _viewModel.IsCapturing;
                            break;
                        case nameof(_viewModel.IsRecording):
                            BtnRecord.IsEnabled = _viewModel.IsCapturing && !_viewModel.IsRecording;
                            break;
                        case nameof(_viewModel.WaveformSamples):
                            WaveformView.UpdateSamples(_viewModel.WaveformSamples);
                            break;
                        case nameof(_viewModel.Devices):
                            PickerDevice.ItemsSource = _viewModel.Devices;
                            if (_viewModel.Devices?.Count > 0)
                                PickerDevice.SelectedIndex = 0;
                            break;
                    }
                });
            };

            _viewModel.LoadDevices();
        }

        private void UpdateVolumeMeterColor(double volume)
        {
            if (volume > 0.7)
                VolumeMeter.ProgressColor = Color.FromArgb("#EF4444");
            else if (volume > 0.3)
                VolumeMeter.ProgressColor = Color.FromArgb("#EAB308");
            else
                VolumeMeter.ProgressColor = Color.FromArgb("#22C55E");
        }

        private void OnSensitivityChanged(object sender, ValueChangedEventArgs e)
        {
            double rounded = Math.Round(e.NewValue, 1);
            LblSensitivity.Text = $"{rounded:F1}x";
            _viewModel.Sensitivity = (float)rounded;
        }

        private void OnStartClicked(object sender, EventArgs e)
        {
            int deviceIndex = PickerDevice.SelectedIndex;
            if (deviceIndex < 0) return;
            _viewModel.StartCapture(deviceIndex);
        }

        private void OnRecordClicked(object sender, EventArgs e)
        {
            _viewModel.StartRecording();
        }

        private void OnStopClicked(object sender, EventArgs e)
        {
            _viewModel.StopCapture();
        }

        private void OnOpenFolderClicked(object sender, EventArgs e)
        {
#if WINDOWS
            try
            {
                string docsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                string techTestDir = Path.Combine(docsPath, "TechTest");
                if (!Directory.Exists(techTestDir)) Directory.CreateDirectory(techTestDir);
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = techTestDir,
                    UseShellExecute = true
                });
            }
            catch { }
#elif MACCATALYST
            try
            {
                string docsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                string techTestDir = Path.Combine(docsPath, "TechTest");
                if (!Directory.Exists(techTestDir)) Directory.CreateDirectory(techTestDir);
                Process.Start("open", techTestDir);
            }
            catch { }
#endif
        }

        private async void OnBackClicked(object sender, EventArgs e)
        {
            _viewModel.StopCapture();
            await Shell.Current.GoToAsync("//MainPage");
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            _viewModel.StopCapture();
        }
    }
}
