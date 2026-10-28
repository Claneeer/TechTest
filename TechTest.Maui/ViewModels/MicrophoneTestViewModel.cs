using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using TechTest.Maui.Services;

namespace TechTest.Maui.ViewModels;

/// <summary>
/// Teste de microfone. Responsabilidades de ciclo de vida:
///  - captura (IAudioCaptureService) e gravação de 5 s cancelável;
///  - reprodução da gravação feita por <see cref="IRecordingPlayerService"/>, que é uma instância
///    ISOLADA do gerador de tons da aba Áudio (antes o ViewModel disparava um "bip" de 440 Hz no
///    serviço compartilhado, que nunca parava sozinho).
/// </summary>
public sealed class MicrophoneTestViewModel : BaseViewModel, IDisposable
{
    private static readonly TimeSpan RecordingDuration = TimeSpan.FromSeconds(5);

    private readonly IAudioCaptureService? _captureService;
    private readonly IRecordingPlayerService? _recordingPlayer;

    private CancellationTokenSource? _recordingCts;
    private bool _disposed;

    private string _statusText = "Pronto para testar";
    private Color _statusColor = Color.FromArgb("#A0A0B0");
    private double _volumeLevel = 0.0;
    private bool _isCapturing;
    private bool _isRecording;
    private bool _isPlayingRecording;
    private float[] _waveformSamples = Array.Empty<float>();
    private List<string> _devices = new();
    private float _sensitivity = 1.0f;
    private readonly string _outputPath;

    public string StatusText { get => _statusText; set => SetProperty(ref _statusText, value); }
    public Color StatusColor { get => _statusColor; set => SetProperty(ref _statusColor, value); }
    public double VolumeLevel { get => _volumeLevel; set => SetProperty(ref _volumeLevel, value); }

    public bool IsCapturing
    {
        get => _isCapturing;
        set { if (SetProperty(ref _isCapturing, value)) OnPropertyChanged(nameof(IsRecording)); }
    }

    public bool IsRecording { get => _isRecording; set => SetProperty(ref _isRecording, value); }
    public bool IsPlayingRecording { get => _isPlayingRecording; private set => SetProperty(ref _isPlayingRecording, value); }
    public float[] WaveformSamples { get => _waveformSamples; set => SetProperty(ref _waveformSamples, value); }
    public List<string> Devices { get => _devices; set => SetProperty(ref _devices, value); }

    public float Sensitivity
    {
        get => _sensitivity;
        set
        {
            if (SetProperty(ref _sensitivity, value) && _captureService != null)
                _captureService.SensitivityMultiplier = value;
        }
    }

    public MicrophoneTestViewModel(IAudioCaptureService? captureService, IRecordingPlayerService? recordingPlayer)
    {
        _captureService = captureService;
        _recordingPlayer = recordingPlayer;

        if (_captureService != null) _captureService.DataAvailable += OnDataAvailable;
        if (_recordingPlayer != null) _recordingPlayer.PlaybackFinished += OnRecordingPlaybackFinished;

        string docsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        _outputPath = Path.Combine(docsPath, "TechTest", "mic_test.wav");
    }

    private void OnDataAvailable(object? sender, AudioDataEventArgs e)
    {
        VolumeLevel = e.Peak;
        WaveformSamples = e.WaveformSamples;
    }

    private void OnRecordingPlaybackFinished(object? sender, EventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (_disposed) return;
            IsPlayingRecording = false;
            if (!IsRecording)
            {
                StatusText = IsCapturing ? "Escutando..." : "Reprodução concluída";
                StatusColor = IsCapturing ? Color.FromArgb("#22C55E") : Color.FromArgb("#3B82F6");
            }
        });
    }

    public async Task LoadDevicesAsync()
    {
        if (_captureService == null) return;
        try
        {
            var deviceList = await _captureService.GetDevicesAsync().ConfigureAwait(false);
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
            StopPlayback(); // nunca capturar enquanto reproduz a gravação anterior (feedback/eco)
            _captureService.StartCapture(deviceIndex, _sensitivity);
            IsCapturing = true;
            StatusText = "Escutando...";
            StatusColor = Color.FromArgb("#22C55E");
        }
        catch (Exception ex)
        {
            IsCapturing = false;
            StatusText = $"Erro: {ex.Message}";
            StatusColor = Color.FromArgb("#EF4444");
        }
    }

    public async Task StartRecordingAsync()
    {
        if (_captureService == null || !IsCapturing || IsRecording) return;

        // Nova gravação => interrompe qualquer reprodução anterior e libera o arquivo (será sobrescrito).
        StopPlayback();

        var cts = new CancellationTokenSource();
        _recordingCts = cts;

        try
        {
            StatusText = "Gravando áudio (5s)...";
            StatusColor = Color.FromArgb("#EF4444");
            IsRecording = true;

            await _captureService.StartRecording(_outputPath).ConfigureAwait(true);
            await Task.Delay(RecordingDuration, cts.Token).ConfigureAwait(true);

            FinishRecording(playBack: true);
        }
        catch (OperationCanceledException)
        {
            // StopCapture()/Dispose() cancelou: o próprio chamador já finalizou o writer.
        }
        catch (Exception ex)
        {
            try { _captureService.StopRecording(); } catch { }
            IsRecording = false;
            StatusText = $"Erro de gravação: {ex.Message}";
            StatusColor = Color.FromArgb("#EF4444");
        }
        finally
        {
            if (ReferenceEquals(_recordingCts, cts)) _recordingCts = null;
            cts.Dispose();
        }
    }

    private void FinishRecording(bool playBack)
    {
        if (_captureService == null || !IsRecording) return;

        try
        {
            _captureService.StopRecording(); // fecha e libera o arquivo antes de reproduzir
            IsRecording = false;
            StatusText = "Gravação concluída!";
            StatusColor = Color.FromArgb("#3B82F6");

            if (playBack) _ = PlayRecordingAsync();
        }
        catch (Exception ex)
        {
            IsRecording = false;
            StatusText = $"Erro ao salvar: {ex.Message}";
            StatusColor = Color.FromArgb("#EF4444");
        }
    }

    private async Task PlayRecordingAsync()
    {
        if (_recordingPlayer == null || !File.Exists(_outputPath)) return;

        try
        {
            IsPlayingRecording = true;
            StatusText = "Reproduzindo gravação...";
            StatusColor = Color.FromArgb("#10B981");
            await _recordingPlayer.PlayAsync(_outputPath).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            IsPlayingRecording = false;
            StatusText = $"Não foi possível reproduzir: {ex.Message}";
            StatusColor = Color.FromArgb("#EF4444");
        }
    }

    /// <summary>Interrompe a reprodução da gravação (troca de aba, nova gravação, saída da página).</summary>
    public void StopPlayback()
    {
        try { _recordingPlayer?.Stop(); } catch { }
        IsPlayingRecording = false;
    }

    /// <summary>Encerra TUDO: gravação em andamento, captura e reprodução. Idempotente.</summary>
    public void StopCapture()
    {
        try { _recordingCts?.Cancel(); } catch (ObjectDisposedException) { }

        StopPlayback();

        if (_captureService == null) return;

        try
        {
            if (IsRecording)
            {
                _captureService.StopRecording();
                IsRecording = false;
            }

            _captureService.StopCapture();
        }
        catch
        {
            // Dispositivo pode ter sido removido; o estado de UI é restaurado abaixo.
        }
        finally
        {
            IsCapturing = false;
            VolumeLevel = 0.0;
            WaveformSamples = Array.Empty<float>();
            StatusText = "Pronto para testar";
            StatusColor = Color.FromArgb("#A0A0B0");
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        StopCapture();
        _disposed = true;

        if (_captureService != null) _captureService.DataAvailable -= OnDataAvailable;
        if (_recordingPlayer != null) _recordingPlayer.PlaybackFinished -= OnRecordingPlaybackFinished;
        _recordingPlayer?.Dispose();
    }
}
