#if MACCATALYST
using System.Diagnostics;
using System.Timers;
using Timer = System.Timers.Timer;

namespace TechTest.Maui.Services;

public class AudioCaptureService : IAudioCaptureService, IDisposable
{
    private Process? _captureProcess;
    private Process? _recordProcess;
    private Timer? _dataTimer;
    private bool _isCapturing;
    private bool _isRecording;
    private double _sensitivityMultiplier = 1.0;
    private bool _disposed;
    private readonly Random _noiseGenerator = new();
    private float _currentPeak;
    private readonly object _lock = new();

    private const int SampleRate = 44100;
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
        return Task.Run(async () =>
        {
            var devices = new List<string>();

            try
            {
                string output = await RunCommandAsync(
                    "system_profiler SPAudioDataType 2>/dev/null | grep -A2 'Input'");

                if (!string.IsNullOrWhiteSpace(output))
                {
                    var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                    foreach (string line in lines)
                    {
                        string trimmed = line.Trim();
                        if (!string.IsNullOrEmpty(trimmed) &&
                            !trimmed.StartsWith("Input", StringComparison.OrdinalIgnoreCase) &&
                            !trimmed.Contains(":", StringComparison.Ordinal))
                        {
                            devices.Add(trimmed);
                        }
                    }
                }

                if (devices.Count == 0)
                {
                    devices.Add("Microfone Integrado");
                }
            }
            catch
            {
                devices.Add("Microfone Integrado");
            }

            return devices;
        });
    }

    public void StartCapture(int deviceIndex, double sensitivityMultiplier)
    {
        StopCapture();

        _sensitivityMultiplier = Math.Max(0.1, sensitivityMultiplier);
        _isCapturing = true;

        try
        {
            var processInfo = new ProcessStartInfo
            {
                FileName = "/bin/bash",
                Arguments = "-c \"rec -q -t raw -r 44100 -b 16 -c 1 -e signed-integer - 2>/dev/null\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            _captureProcess = new Process { StartInfo = processInfo };

            if (_captureProcess.Start())
            {
                _ = Task.Run(() => ReadAudioStream(_captureProcess));
                return;
            }
        }
        catch
        {
            // sox/rec não disponível - usar simulação
        }

        StartSimulatedCapture();
    }

    private async Task ReadAudioStream(Process process)
    {
        var buffer = new byte[4096];
        var stream = process.StandardOutput.BaseStream;

        try
        {
            while (_isCapturing && !process.HasExited)
            {
                int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                if (bytesRead == 0) break;

                ProcessRawAudioData(buffer, bytesRead);
            }
        }
        catch
        {
            // Stream encerrada
        }
    }

    private void ProcessRawAudioData(byte[] buffer, int bytesRead)
    {
        int sampleCount = bytesRead / 2;
        if (sampleCount == 0) return;

        float peak = 0f;
        float sumSquares = 0f;
        var allSamples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            int offset = i * 2;
            if (offset + 1 >= bytesRead) break;

            short sample = BitConverter.ToInt16(buffer, offset);
            float normalized = sample / 32768f;
            allSamples[i] = normalized;

            float abs = Math.Abs(normalized);
            if (abs > peak) peak = abs;
            sumSquares += normalized * normalized;
        }

        float rms = (float)Math.Sqrt(sumSquares / sampleCount);

        peak = (float)Math.Min(peak * _sensitivityMultiplier, 1.0);
        rms = (float)Math.Min(rms * _sensitivityMultiplier, 1.0);

        float[] waveformSamples = DownsampleWaveform(allSamples, sampleCount);

        DataAvailable?.Invoke(this, new AudioDataEventArgs
        {
            Peak = peak,
            RMS = rms,
            WaveformSamples = waveformSamples
        });
    }

    private void StartSimulatedCapture()
    {
        _dataTimer = new Timer(50);
        _dataTimer.Elapsed += OnSimulatedDataTick;
        _dataTimer.AutoReset = true;
        _dataTimer.Start();
    }

    private void OnSimulatedDataTick(object? sender, ElapsedEventArgs e)
    {
        if (!_isCapturing) return;

        float basePeak = (float)(_noiseGenerator.NextDouble() * 0.05);
        float baseRms = basePeak * 0.7f;

        float peak = (float)Math.Min(basePeak * _sensitivityMultiplier, 1.0);
        float rms = (float)Math.Min(baseRms * _sensitivityMultiplier, 1.0);

        var waveformSamples = new float[MaxWaveformSamples];
        for (int i = 0; i < MaxWaveformSamples; i++)
        {
            float noise = (float)((_noiseGenerator.NextDouble() * 2.0 - 1.0) * basePeak);
            waveformSamples[i] = (float)Math.Clamp(noise * _sensitivityMultiplier, -1.0, 1.0);
        }

        _currentPeak = peak;

        DataAvailable?.Invoke(this, new AudioDataEventArgs
        {
            Peak = peak,
            RMS = rms,
            WaveformSamples = waveformSamples
        });
    }

    public void StopCapture()
    {
        _isCapturing = false;

        if (_isRecording)
        {
            StopRecording();
        }

        if (_dataTimer != null)
        {
            _dataTimer.Stop();
            _dataTimer.Elapsed -= OnSimulatedDataTick;
            _dataTimer.Dispose();
            _dataTimer = null;
        }

        if (_captureProcess != null)
        {
            try
            {
                if (!_captureProcess.HasExited)
                {
                    _captureProcess.Kill();
                    _captureProcess.WaitForExit(2000);
                }

                _captureProcess.Dispose();
            }
            catch
            {
                // Ignorar erros ao parar
            }

            _captureProcess = null;
        }
    }

    public async Task StartRecording(string outputPath)
    {
        if (_isRecording) return;

        string? directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        try
        {
            var processInfo = new ProcessStartInfo
            {
                FileName = "/bin/bash",
                Arguments = $"-c \"rec -q '{outputPath}' rate 44100 channels 1 2>/dev/null\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            _recordProcess = new Process { StartInfo = processInfo };

            if (_recordProcess.Start())
            {
                _isRecording = true;
                return;
            }
        }
        catch
        {
            // sox/rec não disponível
        }

        _isRecording = true;
        await Task.CompletedTask;
    }

    public void StopRecording()
    {
        _isRecording = false;

        if (_recordProcess != null)
        {
            try
            {
                if (!_recordProcess.HasExited)
                {
                    _recordProcess.Kill();
                    _recordProcess.WaitForExit(2000);
                }

                _recordProcess.Dispose();
            }
            catch
            {
                // Ignorar erros ao parar
            }

            _recordProcess = null;
        }
    }

    private float[] DownsampleWaveform(float[] allSamples, int sampleCount)
    {
        if (sampleCount <= MaxWaveformSamples)
        {
            var result = new float[sampleCount];
            for (int i = 0; i < sampleCount; i++)
            {
                result[i] = (float)Math.Clamp(allSamples[i] * _sensitivityMultiplier, -1.0, 1.0);
            }
            return result;
        }

        var waveform = new float[MaxWaveformSamples];
        double step = (double)sampleCount / MaxWaveformSamples;

        for (int i = 0; i < MaxWaveformSamples; i++)
        {
            int sourceIndex = Math.Min((int)(i * step), sampleCount - 1);
            waveform[i] = (float)Math.Clamp(allSamples[sourceIndex] * _sensitivityMultiplier, -1.0, 1.0);
        }

        return waveform;
    }

    private async Task<string> RunCommandAsync(string command)
    {
        var processInfo = new ProcessStartInfo
        {
            FileName = "/bin/bash",
            Arguments = $"-c \"{command.Replace("\"", "\\\"")}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = processInfo };
        process.Start();

        string output = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();

        return output;
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
