#if WINDOWS
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NAudio.Wave;

namespace TechTest.Maui.Services;

public class AudioCaptureService : IAudioCaptureService, IDisposable
{
    private WaveInEvent? _waveIn;
    private WaveFileWriter? _waveFileWriter;
    private bool _isCapturing;
    private bool _isRecording;
    private double _sensitivityMultiplier = 1.0;
    private bool _disposed;
    private readonly object _writerLock = new();

    private const int SampleRate = 44100;
    private const int BitsPerSample = 16;
    private const int Channels = 1;
    private const int MaxWaveformSamples = 340;

    public bool IsCapturing => _isCapturing;
    public bool IsRecording => _isRecording;

    public double SensitivityMultiplier
    {
        get => _sensitivityMultiplier;
        set => _sensitivityMultiplier = Math.Max(0.1, value);
    }

    public event EventHandler<AudioDataEventArgs>? DataAvailable;

    public Task<List<string>> GetDevicesAsync()
    {
        return Task.Run(() =>
        {
            var devices = new List<string>();

            int deviceCount = WaveInEvent.DeviceCount;
            for (int i = 0; i < deviceCount; i++)
            {
                var capabilities = WaveInEvent.GetCapabilities(i);
                devices.Add(capabilities.ProductName);
            }

            return devices;
        });
    }

    public void StartCapture(int deviceIndex, double sensitivityMultiplier)
    {
        StopCapture();

        _sensitivityMultiplier = Math.Max(0.1, sensitivityMultiplier);

        _waveIn = new WaveInEvent
        {
            DeviceNumber = deviceIndex,
            WaveFormat = new WaveFormat(SampleRate, BitsPerSample, Channels),
            BufferMilliseconds = 50
        };

        _waveIn.DataAvailable += OnDataAvailable;
        _waveIn.RecordingStopped += OnRecordingStopped;

        _waveIn.StartRecording();
        _isCapturing = true;
    }

    public void StopCapture()
    {
        if (_isRecording)
        {
            StopRecording();
        }

        if (_waveIn != null)
        {
            try
            {
                _waveIn.DataAvailable -= OnDataAvailable;
                _waveIn.RecordingStopped -= OnRecordingStopped;
                _waveIn.StopRecording();
                _waveIn.Dispose();
            }
            catch
            {
                // Ignorar erros ao parar
            }

            _waveIn = null;
        }

        _isCapturing = false;
    }

    public Task StartRecording(string outputPath)
    {
        return Task.Run(() =>
        {
            lock (_writerLock)
            {
                if (_isRecording) return;

                try
                {
                    string? directory = Path.GetDirectoryName(outputPath);
                    if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }

                    _waveFileWriter = new WaveFileWriter(outputPath,
                        new WaveFormat(SampleRate, BitsPerSample, Channels));
                    _isRecording = true;
                }
                catch
                {
                    _waveFileWriter?.Dispose();
                    _waveFileWriter = null;
                    _isRecording = false;
                    throw;
                }
            }
        });
    }

    public void StopRecording()
    {
        lock (_writerLock)
        {
            _isRecording = false;

            if (_waveFileWriter != null)
            {
                try
                {
                    _waveFileWriter.Flush();
                    _waveFileWriter.Dispose();
                }
                catch
                {
                    // Ignorar erros ao finalizar
                }

                _waveFileWriter = null;
            }
        }
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        if (e.BytesRecorded == 0) return;

        lock (_writerLock)
        {
            if (_isRecording && _waveFileWriter != null)
            {
                try
                {
                    _waveFileWriter.Write(e.Buffer, 0, e.BytesRecorded);
                }
                catch
                {
                    // Ignorar erros de escrita
                }
            }
        }

        int sampleCount = e.BytesRecorded / 2;
        if (sampleCount == 0) return;

        float peak = 0f;
        float sumSquares = 0f;

        var allSamples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            short sample = BitConverter.ToInt16(e.Buffer, i * 2);
            float normalized = sample / 32768f;
            allSamples[i] = normalized;

            float absSample = Math.Abs(normalized);
            if (absSample > peak)
                peak = absSample;

            sumSquares += normalized * normalized;
        }

        float rms = (float)Math.Sqrt(sumSquares / sampleCount);

        peak = (float)Math.Min(peak * _sensitivityMultiplier, 1.0);
        rms = (float)Math.Min(rms * _sensitivityMultiplier, 1.0);

        float[] waveformSamples;
        if (sampleCount <= MaxWaveformSamples)
        {
            waveformSamples = allSamples;
        }
        else
        {
            waveformSamples = new float[MaxWaveformSamples];
            double step = (double)sampleCount / MaxWaveformSamples;

            for (int i = 0; i < MaxWaveformSamples; i++)
            {
                int sourceIndex = (int)(i * step);
                if (sourceIndex >= sampleCount)
                    sourceIndex = sampleCount - 1;

                waveformSamples[i] = (float)Math.Clamp(
                    allSamples[sourceIndex] * _sensitivityMultiplier, -1.0, 1.0);
            }
        }

        var args = new AudioDataEventArgs
        {
            Peak = peak,
            RMS = rms,
            WaveformSamples = waveformSamples
        };

        DataAvailable?.Invoke(this, args);
    }

    private void OnRecordingStopped(object? sender, StoppedEventArgs e)
    {
        _isCapturing = false;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            StopCapture();
            _disposed = true;
        }

        GC.SuppressFinalize(this);
    }
}
#endif
