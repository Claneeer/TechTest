using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using TechTest.Maui.Services;
using TechTest.Maui.ViewModels;

#if WINDOWS
using System.Media;
using NAudio.Wave;
#endif

namespace TechTest.Maui.Views
{
    public partial class MicrophoneTestPage : ContentPage
    {
        private readonly MicrophoneTestViewModel _viewModel;
        private string? _lastRecordedFile;

#if WINDOWS
        // Instâncias estritamente isoladas para reprodução da gravação (RecordedPlaybackPlayer)
        private WaveOutEvent? _recordedWaveOut;
        private AudioFileReader? _recordedAudioReader;
        private SoundPlayer? _recordedPlaybackPlayer;
#endif

        public MicrophoneTestPage()
        {
            InitializeComponent();
            TechTest.Maui.Controls.ApprovalBar.Attach(this, TechTest.Maui.Services.TestId.Microphone);

            var audioService = IPlatformApplication.Current?.Services?.GetService<IAudioCaptureService>();
            _viewModel = new MicrophoneTestViewModel(audioService);
            BindingContext = _viewModel;

            _viewModel.PrivacyBlockedError += OnPrivacyBlockedError;

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
                            if (!_viewModel.IsRecording)
                            {
                                CheckLastRecording();
                            }
                            break;
                        case nameof(_viewModel.WaveformSamples):
                            WaveformView.UpdateSamples(_viewModel.WaveformSamples);
                            break;
                        case nameof(_viewModel.Devices):
                            PickerDevice.ItemsSource = _viewModel.Devices;
                            if (_viewModel.Devices?.Count > 0 && PickerDevice.SelectedIndex < 0)
                            {
                                PickerDevice.SelectedIndex = 0;
                            }
                            break;
                    }
                });
            };
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            if (!_viewModel.IsCapturing)
            {
                await _viewModel.LoadDevicesAsync();
                if (_viewModel.Devices?.Count > 0 && PickerDevice.SelectedIndex < 0)
                {
                    PickerDevice.SelectedIndex = 0;
                }
            }
        }

        private void OnPrivacyBlockedError(object? sender, string message)
        {
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                bool openSettings = await DisplayAlert(
                    "Permissão de Microfone Bloqueada",
                    $"{message}\n\nDeseja abrir as Configurações de Privacidade do Windows para liberar o acesso ao microfone?",
                    "Abrir Configurações",
                    "Cancelar");

                if (openSettings)
                {
#if WINDOWS
                    try
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = "ms-settings:privacy-microphone",
                            UseShellExecute = true
                        });
                    }
                    catch { }
#endif
                }
            });
        }

        private void StopAndDisposeRecordedPlayer()
        {
#if WINDOWS
            if (_recordedWaveOut != null)
            {
                try
                {
                    _recordedWaveOut.Stop();
                    _recordedWaveOut.Dispose();
                }
                catch { }
                finally
                {
                    _recordedWaveOut = null;
                }
            }

            if (_recordedAudioReader != null)
            {
                try { _recordedAudioReader.Dispose(); } catch { }
                finally { _recordedAudioReader = null; }
            }

            if (_recordedPlaybackPlayer != null)
            {
                try
                {
                    _recordedPlaybackPlayer.Stop();
                    _recordedPlaybackPlayer.Dispose();
                }
                catch { }
                finally
                {
                    _recordedPlaybackPlayer = null;
                }
            }
#endif
        }

        private void CheckLastRecording()
        {
            try
            {
                string docsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                string techTestDir = Path.Combine(docsPath, "TechTest");
                if (Directory.Exists(techTestDir))
                {
                    var latest = new DirectoryInfo(techTestDir)
                        .GetFiles("*.wav")
                        .OrderByDescending(f => f.LastWriteTime)
                        .FirstOrDefault();

                    if (latest != null && latest.Length > 44)
                    {
                        _lastRecordedFile = latest.FullName;
                        BtnPlay.IsEnabled = true;
                    }
                }
            }
            catch { }
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
            StopAndDisposeRecordedPlayer();

            if (PickerDevice.SelectedIndex < 0 && _viewModel.Devices?.Count > 0)
            {
                PickerDevice.SelectedIndex = 0;
            }

            int deviceIndex = Math.Max(0, PickerDevice.SelectedIndex);
            _viewModel.StartCapture(deviceIndex);
        }

        private void OnRecordClicked(object sender, EventArgs e)
        {
            StopAndDisposeRecordedPlayer();
            BtnPlay.IsEnabled = false;
            _viewModel.StartRecording();
        }

        private void OnPlayRecordingClicked(object sender, EventArgs e)
        {
            // Garante que qualquer tom do AudioTestPlayer esteja parado antes de tocar a gravação
            try
            {
                var audioTestService = IPlatformApplication.Current?.Services?.GetService<IAudioPlaybackService>();
                audioTestService?.StopTone();
            }
            catch { }

            if (string.IsNullOrEmpty(_lastRecordedFile) || !File.Exists(_lastRecordedFile))
            {
                CheckLastRecording();
            }

            if (!string.IsNullOrEmpty(_lastRecordedFile) && File.Exists(_lastRecordedFile))
            {
#if WINDOWS
                try
                {
                    StopAndDisposeRecordedPlayer();

                    try
                    {
                        _recordedAudioReader = new AudioFileReader(_lastRecordedFile);
                        _recordedWaveOut = new WaveOutEvent { DesiredLatency = 120 };
                        _recordedWaveOut.Init(_recordedAudioReader);
                        _recordedWaveOut.Play();
                    }
                    catch
                    {
                        StopAndDisposeRecordedPlayer();
                        _recordedPlaybackPlayer = new SoundPlayer(_lastRecordedFile);
                        _recordedPlaybackPlayer.Play();
                    }

                    LblStatus.Text = "🔊 A reproduzir gravação...";
                    LblStatus.TextColor = Color.FromArgb("#22C55E");
                }
                catch (Exception ex)
                {
                    DisplayAlert("Erro de Reprodução", ex.ToString(), "OK");
                }
#else
                Process.Start("open", _lastRecordedFile);
#endif
            }
            else
            {
                DisplayAlert("Aviso", "Nenhuma gravação recente encontrada. Grave 5s primeiro.", "OK");
            }
        }

        private void OnStopClicked(object sender, EventArgs e)
        {
            StopAndDisposeRecordedPlayer();
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
            StopAndDisposeRecordedPlayer();
            _viewModel.StopCapture();
            await Shell.Current.GoToAsync("//MainPage");
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            StopAndDisposeRecordedPlayer();
            _viewModel.StopCapture();
        }
    }
}
