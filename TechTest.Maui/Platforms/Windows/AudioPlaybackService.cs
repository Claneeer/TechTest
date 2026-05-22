#if WINDOWS
using System;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace TechTest.Maui.Services;

public class AudioPlaybackService : IAudioPlaybackService, IDisposable
{
    private WaveOutEvent? _waveOut;
    private SignalGenerator? _signalGenerator;
    private MonoToStereoSampleProvider? _stereoProvider;
    private VolumeSampleProvider? _volumeProvider;
    private bool _isPlaying;
    private double _currentFrequency;
    private double _currentVolume = 1.0;
    private AudioChannel _currentChannel;
    private SoundType _currentSoundType;
    private bool _disposed;

    public bool IsPlaying => _isPlaying;
    public event EventHandler? PlaybackStopped;

    public void PlayTone(double frequency, AudioChannel channel, SoundType type, double volume)
    {
        StopTone();

        _currentFrequency = frequency;
        _currentVolume = volume;
        _currentChannel = channel;
        _currentSoundType = type;

        _signalGenerator = new SignalGenerator(44100, 1)
        {
            Frequency = frequency,
            Gain = 0.5,
            Type = MapSoundType(type)
        };

        if (type == SoundType.Sweep)
        {
            _signalGenerator.FrequencyEnd = 10000;
            _signalGenerator.Frequency = 100;
            _signalGenerator.SweepLengthSecs = 5.0;
        }

        ISampleProvider source = _signalGenerator;

        _stereoProvider = new MonoToStereoSampleProvider(source);
        ApplyChannelRouting(_stereoProvider, channel);

        _volumeProvider = new VolumeSampleProvider(_stereoProvider)
        {
            Volume = (float)Math.Clamp(volume, 0.0, 2.0)
        };

        _waveOut = new WaveOutEvent
        {
            DesiredLatency = 150
        };

        _waveOut.PlaybackStopped += OnPlaybackStopped;
        _waveOut.Init(_volumeProvider);
        _waveOut.Play();
        _isPlaying = true;
    }

    public void StopTone()
    {
        if (_waveOut != null)
        {
            try
            {
                _waveOut.PlaybackStopped -= OnPlaybackStopped;
                _waveOut.Stop();
                _waveOut.Dispose();
            }
            catch
            {
                // Ignorar erros ao parar
            }

            _waveOut = null;
        }

        _signalGenerator = null;
        _stereoProvider = null;
        _volumeProvider = null;
        _isPlaying = false;
    }

    public void SetFrequency(double frequency)
    {
        _currentFrequency = frequency;

        if (_signalGenerator != null && _currentSoundType != SoundType.Sweep)
        {
            _signalGenerator.Frequency = frequency;
        }
    }

    public void SetVolume(double volume)
    {
        _currentVolume = Math.Clamp(volume, 0.0, 2.0);

        if (_volumeProvider != null)
        {
            _volumeProvider.Volume = (float)_currentVolume;
        }
    }

    private SignalGeneratorType MapSoundType(SoundType type)
    {
        return type switch
        {
            SoundType.Sine => SignalGeneratorType.Sin,
            SoundType.Square => SignalGeneratorType.Square,
            SoundType.WhiteNoise => SignalGeneratorType.White,
            SoundType.Sweep => SignalGeneratorType.Sweep,
            SoundType.Beep => SignalGeneratorType.Sin,
            _ => SignalGeneratorType.Sin
        };
    }

    private void ApplyChannelRouting(MonoToStereoSampleProvider provider, AudioChannel channel)
    {
        switch (channel)
        {
            case AudioChannel.Left:
                provider.LeftVolume = 1.0f;
                provider.RightVolume = 0.0f;
                break;
            case AudioChannel.Right:
                provider.LeftVolume = 0.0f;
                provider.RightVolume = 1.0f;
                break;
            case AudioChannel.Both:
            default:
                provider.LeftVolume = 1.0f;
                provider.RightVolume = 1.0f;
                break;
        }
    }

    private void OnPlaybackStopped(object? sender, StoppedEventArgs e)
    {
        _isPlaying = false;
        PlaybackStopped?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            StopTone();
            _disposed = true;
        }

        GC.SuppressFinalize(this);
    }
}
#endif
