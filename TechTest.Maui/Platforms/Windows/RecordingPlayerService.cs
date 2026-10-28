#if WINDOWS
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NAudio.Wave;

namespace TechTest.Maui.Services;

/// <summary>
/// Reproduz um arquivo WAV com um <see cref="WaveOutEvent"/> PRÓPRIO.
/// Garante: uma única reprodução por vez, sem loop, liberação determinística dos handles
/// (arquivo + dispositivo de saída) e parada segura via Stop()/Dispose()/CancellationToken.
/// </summary>
public sealed class RecordingPlayerService : IRecordingPlayerService
{
    private readonly object _gate = new();
    private WaveOutEvent? _output;
    private AudioFileReader? _reader;
    private CancellationTokenRegistration _ctReg;
    private bool _disposed;

    public bool IsPlaying
    {
        get { lock (_gate) return _output?.PlaybackState == PlaybackState.Playing; }
    }

    public event EventHandler? PlaybackFinished;

    public Task PlayAsync(string filePath, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            throw new FileNotFoundException("Arquivo de gravação não encontrado.", filePath);

        lock (_gate)
        {
            ReleaseUnsafe(raiseEvent: false); // garante instância única

            AudioFileReader? reader = null;
            WaveOutEvent? output = null;
            try
            {
                reader = new AudioFileReader(filePath);
                output = new WaveOutEvent { DesiredLatency = 150 };
                output.PlaybackStopped += OnPlaybackStopped;
                output.Init(reader);

                _reader = reader;
                _output = output;
                _ctReg = cancellationToken.Register(Stop);

                output.Play();
            }
            catch
            {
                if (output != null) output.PlaybackStopped -= OnPlaybackStopped;
                output?.Dispose();
                reader?.Dispose();
                _output = null;
                _reader = null;
                throw;
            }
        }

        return Task.CompletedTask;
    }

    public void Stop()
    {
        lock (_gate) ReleaseUnsafe(raiseEvent: true);
    }

    private void OnPlaybackStopped(object? sender, StoppedEventArgs e)
    {
        // Fim natural do arquivo (ou erro do driver): libera tudo e avisa a UI.
        lock (_gate)
        {
            if (!ReferenceEquals(sender, _output)) return; // evento de uma instância antiga
            ReleaseUnsafe(raiseEvent: true);
        }
    }

    private void ReleaseUnsafe(bool raiseEvent)
    {
        var output = _output;
        var reader = _reader;
        if (output == null && reader == null) return;

        _output = null;
        _reader = null;

        try { _ctReg.Dispose(); } catch { /* ignorar */ }

        if (output != null)
        {
            try
            {
                output.PlaybackStopped -= OnPlaybackStopped; // evita reentrância
                output.Stop();
            }
            catch { /* dispositivo pode já ter sido removido */ }
            finally
            {
                try { output.Dispose(); } catch { }
            }
        }

        try { reader?.Dispose(); } catch { }

        if (raiseEvent)
        {
            try { PlaybackFinished?.Invoke(this, EventArgs.Empty); } catch { }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
        GC.SuppressFinalize(this);
    }
}
#endif
