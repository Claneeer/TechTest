using System;
using System.Threading;
using System.Threading.Tasks;

namespace TechTest.Maui.Services;

/// <summary>Implementação nula (plataformas sem player de arquivo): mantém o DI resolvível.</summary>
public sealed class NullRecordingPlayerService : IRecordingPlayerService
{
    public bool IsPlaying => false;
    public event EventHandler? PlaybackFinished { add { } remove { } }
    public Task PlayAsync(string filePath, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public void Stop() { }
    public void Dispose() { }
}
