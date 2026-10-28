using System;
using System.Threading;
using System.Threading.Tasks;

namespace TechTest.Maui.Services;

/// <summary>
/// Reprodutor de ARQUIVOS gravados (ex.: mic_test.wav). É totalmente independente do
/// <see cref="IAudioPlaybackService"/> (gerador de tons da aba Áudio): cada um possui o seu
/// próprio dispositivo de saída, então parar/iniciar um nunca afeta o outro.
/// </summary>
public interface IRecordingPlayerService : IDisposable
{
    bool IsPlaying { get; }

    /// <summary>Disparado (em thread arbitrária) quando a reprodução termina, é interrompida ou falha.</summary>
    event EventHandler? PlaybackFinished;

    /// <summary>Toca o arquivo uma única vez (sem loop). Qualquer reprodução anterior é encerrada antes.</summary>
    Task PlayAsync(string filePath, CancellationToken cancellationToken = default);

    /// <summary>Idempotente e seguro de chamar de qualquer thread.</summary>
    void Stop();
}
