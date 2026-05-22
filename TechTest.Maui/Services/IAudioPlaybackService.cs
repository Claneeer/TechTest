using System;

namespace TechTest.Maui.Services;

public enum SoundType
{
    Sine,
    Square,
    WhiteNoise,
    Sweep,
    Beep
}

public enum AudioChannel
{
    Left,
    Right,
    Both
}

public interface IAudioPlaybackService : IDisposable
{
    bool IsPlaying { get; }
    event EventHandler? PlaybackStopped;

    void PlayTone(double frequency, AudioChannel channel, SoundType type, double volume);
    void StopTone();
    void SetFrequency(double frequency);
    void SetVolume(double volume);
}
