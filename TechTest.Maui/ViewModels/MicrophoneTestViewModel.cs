using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Graphics;
using TechTest.Maui.Services;

namespace TechTest.Maui.ViewModels;

public class MicrophoneTestViewModel : BaseViewModel, IDisposable
{
    private readonly IAudioCaptureService? _captureService;
    private CancellationTokenSource? _recordingCts;
    private bool _disposed;

    private string _statusText = "Pronto para testar";
    private Color _statusColor = Color.FromArgb("#A0A0B0");
    private double _volumeLevel = 0.0;
    private bool _isCapturing;
    private bool _isRecording;
    private float[] _waveformSamples = Array.Empty<float>();
    private List<string> _devices = new();
    private float _sensitivity = 1.0f;
    private readonly string _outputPath;

    public string OutputPath => _outputPath;

    public event EventHandler<string>? PrivacyBlockedError;

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

    public MicrophoneTestViewModel(IAudioCaptureService? captureService, IAudioPlaybackService? _ = null)
    {
        _captureService = captureService;

        if (_captureService != null)
        {
            _captureService.DataAvailable += OnDataAvailable;
        }

        string docsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        string techTestDir = Path.Combine(docsPath, "TechTest");
        _outputPath = Path.Combine(techTestDir, "mic_test.wav");
    }

    private void OnDataAvailable(object? sender, AudioDataEventArgs e)
    {
        VolumeLevel = e.Peak;
        WaveformSamples = e.WaveformSamples;
    }

    public async Task LoadDevicesAsync()
    {
        if (_captureService == null) return;
        try
        {
            var deviceList = await _captureService.GetDevicesAsync();
            if (deviceList != null && deviceList.Count > 0)
            {
                Devices = deviceList;
                StatusText = $"{deviceList.Count} microfone(s) detectado(s). Pronto para testar.";
                StatusColor = Color.FromArgb("#A0A0B0");
            }
            else
            {
                Devices = new List<string> { "Nenhum microfone detectado" };
                StatusText = "⚠️ Nenhum dispositivo de entrada de áudio encontrado.";
                StatusColor = Color.FromArgb("#EAB308");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Erro ao listar microfones: {ex}");
            Devices = new List<string> { "Microfone Padrão" };
        }
    }

    public async void LoadDevices()
    {
        await LoadDevicesAsync();
    }

    public async void StartCapture(int deviceIndex)
    {
        if (_captureService == null) return;

        try
        {
            // Valida permissão de microfone (com fallback seguro para modo WindowsPackageType=None)
            try
            {
                var status = await Permissions.CheckStatusAsync<Permissions.Microphone>();
                if (status != PermissionStatus.Granted)
                {
                    status = await Permissions.RequestAsync<Permissions.Microphone>();
                }

                if (status == PermissionStatus.Denied || status == PermissionStatus.Restricted)
                {
                    throw new UnauthorizedAccessException(
                        "Permissão de acesso ao microfone negada nas Configurações de Privacidade do Windows.");
                }
            }
            catch (UnauthorizedAccessException)
            {
                throw;
            }
            catch (Exception permEx)
            {
                Debug.WriteLine($"Aviso Permissions.Microphone (Unpackaged): {permEx.Message}");
            }

            CancelRecordingToken();
            int safeIndex = Math.Max(0, deviceIndex);
            _captureService.StartCapture(safeIndex, _sensitivity);
            IsCapturing = true;
            StatusText = "🎤 Escutando microfone...";
            StatusColor = Color.FromArgb("#22C55E");
        }
        catch (UnauthorizedAccessException uaEx)
        {
            IsCapturing = false;
            StatusText = $"🔒 Microfone bloqueado: {uaEx.Message}";
            StatusColor = Color.FromArgb("#EF4444");
            PrivacyBlockedError?.Invoke(this, uaEx.Message);
        }
        catch (Exception ex)
        {
            IsCapturing = false;
            StatusText = $"Erro: {ex.Message}";
            StatusColor = Color.FromArgb("#EF4444");
        }
    }

    public async void StartRecording()
    {
        if (_captureService == null || !IsCapturing) return;

        CancelRecordingToken();
        if (IsRecording)
        {
            try { _captureService.StopRecording(); } catch { }
            IsRecording = false;
        }

        _recordingCts = new CancellationTokenSource();
        var token = _recordingCts.Token;

        try
        {
            StatusText = "⏺ Gravando áudio (5s)...";
            StatusColor = Color.FromArgb("#EF4444");
            IsRecording = true;

            await _captureService.StartRecording(_outputPath);

            await Task.Delay(5000, token);

            if (!token.IsCancellationRequested && IsRecording)
            {
                StopRecording(completedNormally: true);
            }
        }
        catch (OperationCanceledException)
        {
            if (IsRecording)
            {
                try { _captureService.StopRecording(); } catch { }
                IsRecording = false;
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Erro de gravação: {ex.Message}";
            StatusColor = Color.FromArgb("#EF4444");
            IsRecording = false;
        }
    }

    private void StopRecording(bool completedNormally)
    {
        if (_captureService == null || !IsRecording) return;

        try
        {
            _captureService.StopRecording();
            IsRecording = false;

            if (completedNormally)
            {
                StatusText = "✅ Gravação concluída! Clique em '🔊 Ouvir' para reproduzir.";
                StatusColor = Color.FromArgb("#10B981");
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Erro ao salvar: {ex.Message}";
            StatusColor = Color.FromArgb("#EF4444");
        }
    }

    public void CancelRecordingToken()
    {
        if (_recordingCts != null)
        {
            try
            {
                if (!_recordingCts.IsCancellationRequested)
                    _recordingCts.Cancel();
                _recordingCts.Dispose();
            }
            catch { }
            finally
            {
                _recordingCts = null;
            }
        }
    }

    public void StopCapture()
    {
        CancelRecordingToken();

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
        catch { }
    }

    public void Dispose()
    {
        if (_disposed) return;
        StopCapture();
        if (_captureService != null)
        {
            _captureService.DataAvailable -= OnDataAvailable;
        }
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
