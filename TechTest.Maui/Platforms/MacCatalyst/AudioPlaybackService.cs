#if MACCATALYST
using System.Diagnostics;

namespace TechTest.Maui.Services;

public class AudioPlaybackService : IAudioPlaybackService, IDisposable
{
    private Process? _playProcess;
    private bool _isPlaying;
    private double _currentFrequency = 440.0;
    private double _currentVolume = 1.0;
    private AudioChannel _currentChannel = AudioChannel.Both;
    private SoundType _currentSoundType = SoundType.Sine;
    private string? _tempFilePath;
    private bool _disposed;

    private const int SampleRate = 44100;
    private const int BitsPerSample = 16;
    private const double DefaultDuration = 30.0;

    public bool IsPlaying => _isPlaying;
    public event EventHandler? PlaybackStopped;

    public void PlayTone(double frequency, AudioChannel channel, SoundType type, double volume)
    {
        StopTone();

        _currentFrequency = frequency;
        _currentVolume = Math.Clamp(volume, 0.0, 2.0);
        _currentChannel = channel;
        _currentSoundType = type;

        double duration = type == SoundType.Sweep ? 5.0 : DefaultDuration;
        byte[] wavData = GenerateWavData(frequency, channel, type, _currentVolume, duration);

        string tempDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TechTest", "Audio");

        if (!Directory.Exists(tempDir))
            Directory.CreateDirectory(tempDir);

        _tempFilePath = Path.Combine(tempDir, $"tone_{Guid.NewGuid():N}.wav");
        File.WriteAllBytes(_tempFilePath, wavData);

        var processInfo = new ProcessStartInfo
        {
            FileName = "/usr/bin/afplay",
            Arguments = $"\"{_tempFilePath}\"",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        _playProcess = new Process { StartInfo = processInfo };
        _playProcess.EnableRaisingEvents = true;
        _playProcess.Exited += OnProcessExited;
        _playProcess.Start();
        _isPlaying = true;
    }

    public void StopTone()
    {
        if (_playProcess != null)
        {
            try
            {
                _playProcess.Exited -= OnProcessExited;

                if (!_playProcess.HasExited)
                {
                    _playProcess.Kill();
                    _playProcess.WaitForExit(2000);
                }

                _playProcess.Dispose();
            }
            catch
            {
                // Ignorar erros ao parar
            }

            _playProcess = null;
        }

        CleanupTempFile();
        _isPlaying = false;
    }

    public void SetFrequency(double frequency)
    {
        _currentFrequency = frequency;

        if (_isPlaying)
        {
            PlayTone(_currentFrequency, _currentChannel, _currentSoundType, _currentVolume);
        }
    }

    public void SetVolume(double volume)
    {
        _currentVolume = Math.Clamp(volume, 0.0, 2.0);

        if (_isPlaying)
        {
            PlayTone(_currentFrequency, _currentChannel, _currentSoundType, _currentVolume);
        }
    }

    private byte[] GenerateWavData(double frequency, AudioChannel channel,
        SoundType type, double volume, double durationSeconds)
    {
        int numChannels = 2;
        int totalSamples = (int)(SampleRate * durationSeconds);
        int dataSize = totalSamples * numChannels * (BitsPerSample / 8);

        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);

        writer.Write(new[] { 'R', 'I', 'F', 'F' });
        writer.Write(36 + dataSize);
        writer.Write(new[] { 'W', 'A', 'V', 'E' });

        writer.Write(new[] { 'f', 'm', 't', ' ' });
        writer.Write(16);
        writer.Write((short)1);
        writer.Write((short)numChannels);
        writer.Write(SampleRate);
        writer.Write(SampleRate * numChannels * (BitsPerSample / 8));
        writer.Write((short)(numChannels * (BitsPerSample / 8)));
        writer.Write((short)BitsPerSample);

        writer.Write(new[] { 'd', 'a', 't', 'a' });
        writer.Write(dataSize);

        var random = new Random(42);
        double sweepStart = 100.0;
        double sweepEnd = 10000.0;

        for (int i = 0; i < totalSamples; i++)
        {
            double t = (double)i / SampleRate;
            double sample = 0.0;

            switch (type)
            {
                case SoundType.Sine:
                    sample = Math.Sin(2.0 * Math.PI * frequency * t);
                    break;

                case SoundType.Square:
                    sample = Math.Sin(2.0 * Math.PI * frequency * t) >= 0 ? 1.0 : -1.0;
                    break;

                case SoundType.WhiteNoise:
                    sample = (random.NextDouble() * 2.0) - 1.0;
                    break;

                case SoundType.Sweep:
                    double progress = t / durationSeconds;
                    double sweepFreq = sweepStart * Math.Pow(sweepEnd / sweepStart, progress);
                    sample = Math.Sin(2.0 * Math.PI * sweepFreq * t);
                    break;

                case SoundType.Beep:
                    double beepPeriod = 0.5;
                    double beepDuty = 0.3;
                    double beepPhase = t % beepPeriod;
                    sample = beepPhase < beepDuty
                        ? Math.Sin(2.0 * Math.PI * frequency * t)
                        : 0.0;
                    break;
            }

            sample *= volume * 0.5;
            sample = Math.Clamp(sample, -1.0, 1.0);

            short sampleValue = (short)(sample * 32767);

            short leftSample = channel == AudioChannel.Right ? (short)0 : sampleValue;
            short rightSample = channel == AudioChannel.Left ? (short)0 : sampleValue;

            writer.Write(leftSample);
            writer.Write(rightSample);
        }

        return stream.ToArray();
    }

    private void OnProcessExited(object? sender, EventArgs e)
    {
        _isPlaying = false;
        CleanupTempFile();
        PlaybackStopped?.Invoke(this, EventArgs.Empty);
    }

    private void CleanupTempFile()
    {
        if (!string.IsNullOrEmpty(_tempFilePath) && File.Exists(_tempFilePath))
        {
            try { File.Delete(_tempFilePath); } catch { }
            _tempFilePath = null;
        }
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
