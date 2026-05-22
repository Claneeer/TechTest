using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using TechTest.Maui.Services;

namespace TechTest.Maui.ViewModels;

public class MicrophoneTestViewModel : BaseViewModel
{
    private readonly IAudioCaptureService _captureService;
    private readonly IAudioPlaybackService _playbackService;

    private string _statusText = "Pronto para testar";
    private Color _statusColor = Color.FromArgb("#A0A0B0");
    private double _volumeLevel = 0.0;
    private bool _isCapturing;
    private bool _isRecording;
    private float[] _waveformSamples = Array.Empty<float>();
    private List<string> _devices = new();
    private float _sensitivity = 1.0f;
    private string _outputPath = "";

    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    public Color StatusColor
    {
        get => _statusColor;
        set => SetProperty(ref _statusColor, value);
    }

    public double VolumeLevel
    {
        get => _volumeLevel;
        set => SetProperty(ref _volumeLevel, value);
    }

    public bool IsCapturing
    {
        get => _isCapturing;
        set
        {
            if (SetProperty(ref _isCapturing, value))
            {
                OnPropertyChanged(nameof(IsRecording));
            }
        }
    }

    public bool IsRecording
    {
        get => _isRecording;
        set => SetProperty(ref _isRecording, value);
    }

    public float[] WaveformSamples
    {
        get => _waveformSamples;
        set => SetProperty(ref _waveformSamples, value);
    }

    public List<string> Devices
    {
        get => _devices;
        set => SetProperty(ref _devices, value);
    }

    public float Sensitivity
    {
        get => _sensitivity;
        set
        {
            if (SetProperty(ref _sensitivity, value))
            {
                if (_captureService != null)
                {
                    _captureService.SensitivityMultiplier = value;
                }
            }
        }
    }

    public MicrophoneTestViewModel(IAudioCaptureService captureService, IAudioPlaybackService playbackService)
    {
        _captureService = captureService;
        _playbackService = playbackService;

        if (_captureService != null)
        {
            _captureService.DataAvailable += OnDataAvailable;
        }

        string docsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        string techTestDir = Path.Combine(docsPath, "TechTest");
        _outputPath = Path.Combine(techTestDir, "mic_test.wav");
    }

    private void OnDataAvailable(object sender, AudioDataEventArgs e)
    {
        VolumeLevel = e.Peak;
        WaveformSamples = e.WaveformSamples;
    }

    public async void LoadDevices()
    {
        if (_captureService == null) return;
        try
        {
            var deviceList = await _captureService.GetDevicesAsync();
            Devices = deviceList ?? new List<string>();
        }
        catch
        {
            Devices = new List<string> { "Microfone Padrão" };
        }
    }

    public void StartCapture(int deviceIndex)
    {
        if (_captureService == null) return;

        try
        {
            _captureService.StartCapture(deviceIndex, _sensitivity);
            IsCapturing = true;
            StatusText = "Escutando...";
            StatusColor = Color.FromArgb("#22C55E"); // Green
        }
        catch (Exception ex)
        {
            StatusText = $"Erro: {ex.Message}";
            StatusColor = Color.FromArgb("#EF4444"); // Red
        }
    }

    public async void StartRecording()
    {
        if (_captureService == null || !IsCapturing || IsRecording) return;

        try
        {
            StatusText = "Gravando áudio (5s)...";
            StatusColor = Color.FromArgb("#EF4444"); // Red
            IsRecording = true;

            await _captureService.StartRecording(_outputPath);

            // Wait 5 seconds to complete recording
            await Task.Delay(5000);

            if (IsRecording)
            {
                StopRecording();
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Erro de gravação: {ex.Message}";
            StatusColor = Color.FromArgb("#EF4444"); // Red
            IsRecording = false;
        }
    }

    private void StopRecording()
    {
        if (_captureService == null || !IsRecording) return;

        try
        {
            _captureService.StopRecording();
            IsRecording = false;
            StatusText = "Gravação concluída!";
            StatusColor = Color.FromArgb("#3B82F6"); // Blue

            // Optional: playback recorded audio automatically or notify the user
            PlayRecording();
        }
        catch (Exception ex)
        {
            StatusText = $"Erro ao salvar: {ex.Message}";
            StatusColor = Color.FromArgb("#EF4444");
        }
    }

    private void PlayRecording()
    {
        if (_playbackService == null || !File.Exists(_outputPath)) return;

        try
        {
            // If the playback service supports playing file paths, we can call it.
            // Since IAudioPlaybackService is designed for Tone generator, we can play a notification beep or tone,
            // or if they have implemented PlayTone. Let's make sure we do not crash if the tone generator is simple.
            // Let's notify that audio is saved.
            StatusText = $"Gravação salva em: mic_test.wav! Tocando bip...";
            StatusColor = Color.FromArgb("#10B981");
            _playbackService.PlayTone(440, AudioChannel.Both, SoundType.Beep, 0.5);
        }
        catch
        {
            // Silencioso
        }
    }

    public void StopCapture()
    {
        if (_captureService == null) return;

        try
        {
            if (IsRecording)
            {
                _captureService.StopRecording();
                IsRecording = false;
            }

            _captureService.StopCapture();
            IsCapturing = false;
            VolumeLevel = 0.0;
            WaveformSamples = Array.Empty<float>();
            StatusText = "Pronto para testar";
            StatusColor = Color.FromArgb("#A0A0B0");
        }
        catch
        {
            // Silencioso
        }
    }
}
