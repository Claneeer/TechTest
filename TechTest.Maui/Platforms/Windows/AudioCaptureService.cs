#if WINDOWS
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.Win32;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using Windows.Devices.Enumeration;
using Windows.Media.Devices;

namespace TechTest.Maui.Services;

public class AudioCaptureService : IAudioCaptureService, IDisposable
{
    private sealed class AudioEndpointItem
    {
        public string Id { get; init; } = "";
        public string Name { get; init; } = "";
        public bool IsDefault { get; init; }
    }

    private readonly List<AudioEndpointItem> _endpoints = new();
    private readonly object _writerLock = new();

    private IWaveIn? _captureDevice;
    private WaveFormat? _captureFormat;
    private WaveFileWriter? _waveFileWriter;
    private bool _isCapturing;
    private bool _isRecording;
    private double _sensitivityMultiplier = 1.0;
    private bool _disposed;

    private const int DefaultSampleRate = 44100;
    private const int OutputBitsPerSample = 16;
    private const int OutputChannels = 1;
    private const int MaxWaveformSamples = 340;

    public bool IsCapturing => _isCapturing;
    public bool IsRecording => _isRecording;

    public double SensitivityMultiplier
    {
        get => _sensitivityMultiplier;
        set => _sensitivityMultiplier = Math.Max(0.1, value);
    }

    public event EventHandler<AudioDataEventArgs>? DataAvailable;

    public async Task<List<string>> GetDevicesAsync()
    {
        var names = new List<string>();
        var discovered = new List<AudioEndpointItem>();

        try
        {
            string? defaultCommId = null;
            string? defaultGenId = null;

            try { defaultCommId = MediaDevice.GetDefaultAudioCaptureId(AudioDeviceRole.Communications); } catch { }
            try { defaultGenId = MediaDevice.GetDefaultAudioCaptureId(AudioDeviceRole.Default); } catch { }

            var winrtDevices = await DeviceInformation.FindAllAsync(DeviceClass.AudioCapture);
            if (winrtDevices != null && winrtDevices.Count > 0)
            {
                foreach (var dev in winrtDevices)
                {
                    if (!dev.IsEnabled) continue;

                    bool isDefault = (!string.IsNullOrEmpty(defaultCommId) && string.Equals(dev.Id, defaultCommId, StringComparison.OrdinalIgnoreCase))
                                  || (!string.IsNullOrEmpty(defaultGenId) && string.Equals(dev.Id, defaultGenId, StringComparison.OrdinalIgnoreCase))
                                  || dev.IsDefault;

                    discovered.Add(new AudioEndpointItem
                    {
                        Id = dev.Id,
                        Name = string.IsNullOrWhiteSpace(dev.Name) ? "Microfone" : dev.Name.Trim(),
                        IsDefault = isDefault
                    });
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Erro na enumeração WinRT AudioCapture: {ex}");
        }

        // Fallback via MMDeviceEnumerator (CoreAudio WASAPI) caso DeviceInformation retorne vazio
        if (discovered.Count == 0)
        {
            try
            {
                using var enumerator = new MMDeviceEnumerator();
                string? defaultMmId = null;
                try
                {
                    using var defDev = enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Communications)
                                    ?? enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Multimedia);
                    defaultMmId = defDev?.ID;
                }
                catch { }

                var mmDevices = enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active);
                foreach (var mm in mmDevices)
                {
                    using (mm)
                    {
                        bool isDef = !string.IsNullOrEmpty(defaultMmId) &&
                                     string.Equals(mm.ID, defaultMmId, StringComparison.OrdinalIgnoreCase);

                        discovered.Add(new AudioEndpointItem
                        {
                            Id = mm.ID,
                            Name = string.IsNullOrWhiteSpace(mm.FriendlyName) ? "Microfone" : mm.FriendlyName.Trim(),
                            IsDefault = isDef
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Erro no fallback MMDeviceEnumerator: {ex}");
            }
        }

        // Fallback final via WaveInEvent se necessário
        if (discovered.Count == 0)
        {
            try
            {
                int count = WaveInEvent.DeviceCount;
                for (int i = 0; i < count; i++)
                {
                    var caps = WaveInEvent.GetCapabilities(i);
                    discovered.Add(new AudioEndpointItem
                    {
                        Id = $"wavein:{i}",
                        Name = caps.ProductName,
                        IsDefault = i == 0
                    });
                }
            }
            catch { }
        }

        // Ordena colocando o dispositivo padrão no topo (índice 0) para seleção automática
        var ordered = discovered
            .OrderByDescending(d => d.IsDefault)
            .ToList();

        lock (_endpoints)
        {
            _endpoints.Clear();
            _endpoints.AddRange(ordered);
        }

        foreach (var item in ordered)
        {
            names.Add(item.IsDefault ? $"⭐ {item.Name} (Padrão)" : item.Name);
        }

        return names;
    }

    public static bool IsMicrophonePrivacyBlocked()
    {
        try
        {
            using var hkcuNonPackaged = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\microphone\NonPackaged");
            if (hkcuNonPackaged != null)
            {
                string? val = hkcuNonPackaged.GetValue("Value")?.ToString();
                if (string.Equals(val, "Deny", StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            using var hkcuGlobal = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\microphone");
            if (hkcuGlobal != null)
            {
                string? val = hkcuGlobal.GetValue("Value")?.ToString();
                if (string.Equals(val, "Deny", StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }
        catch { }

        return false;
    }

    public void StartCapture(int deviceIndex, double sensitivityMultiplier)
    {
        StopCapture();

        if (IsMicrophonePrivacyBlocked())
        {
            throw new UnauthorizedAccessException(
                "O acesso ao microfone está bloqueado nas Configurações de Privacidade do Windows (Configurações > Privacidade e Segurança > Microfone).");
        }

        _sensitivityMultiplier = Math.Max(0.1, sensitivityMultiplier);

        AudioEndpointItem? selectedEndpoint = null;
        lock (_endpoints)
        {
            if (_endpoints.Count > 0)
            {
                int safeIndex = Math.Clamp(deviceIndex, 0, _endpoints.Count - 1);
                selectedEndpoint = _endpoints[safeIndex];
            }
        }

        try
        {
            // Inicialização primária via WASAPI (suporta taxas de amostragem nativas 48kHz/44.1kHz e arrays de microfone modernos)
            var mmDevice = ResolveMmDevice(selectedEndpoint);
            if (mmDevice != null)
            {
                var wasapi = new WasapiCapture(mmDevice, true, 50);
                _captureFormat = wasapi.WaveFormat;
                wasapi.DataAvailable += OnDataAvailable;
                wasapi.RecordingStopped += OnRecordingStopped;
                wasapi.StartRecording();
                _captureDevice = wasapi;
                _isCapturing = true;
                return;
            }
        }
        catch (COMException comEx) when ((uint)comEx.HResult == 0x80070005)
        {
            throw new UnauthorizedAccessException(
                "Permissão negada ao inicializar o microfone (0x80070005). Verifique Configurações > Privacidade e Segurança > Microfone.", comEx);
        }
        catch (UnauthorizedAccessException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Falha ao iniciar WasapiCapture, tentando fallback WaveInEvent: {ex.Message}");
        }

        // Fallback via WaveInEvent
        try
        {
            int waveInCount = WaveInEvent.DeviceCount;
            if (waveInCount <= 0)
            {
                throw new InvalidOperationException("Nenhum dispositivo de entrada de áudio ativo foi encontrado no Windows.");
            }

            int waveInIndex = Math.Clamp(deviceIndex, 0, waveInCount - 1);
            var waveIn = new WaveInEvent
            {
                DeviceNumber = waveInIndex,
                WaveFormat = new WaveFormat(DefaultSampleRate, OutputBitsPerSample, OutputChannels),
                BufferMilliseconds = 50
            };

            _captureFormat = waveIn.WaveFormat;
            waveIn.DataAvailable += OnDataAvailable;
            waveIn.RecordingStopped += OnRecordingStopped;
            waveIn.StartRecording();
            _captureDevice = waveIn;
            _isCapturing = true;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Não foi possível inicializar o microfone selecionado. Verifique se está conectado e permitido nas configurações de privacidade do Windows. Detalhe: {ex.Message}", ex);
        }
    }

    private static MMDevice? ResolveMmDevice(AudioEndpointItem? selectedEndpoint)
    {
        try
        {
            using var enumerator = new MMDeviceEnumerator();
            var activeDevices = enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active);

            if (selectedEndpoint != null && !string.IsNullOrEmpty(selectedEndpoint.Id))
            {
                // O Id do WinRT DeviceInformation contém o GUID do MMDevice (ex: \\?\SWD#MMDEVAPI#{0.0.1.00000000}.{guid}#{...})
                foreach (var dev in activeDevices)
                {
                    if (selectedEndpoint.Id.Contains(dev.ID, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(dev.ID, selectedEndpoint.Id, StringComparison.OrdinalIgnoreCase))
                    {
                        return dev;
                    }
                }

                foreach (var dev in activeDevices)
                {
                    if (string.Equals(dev.FriendlyName.Trim(), selectedEndpoint.Name.Trim(), StringComparison.OrdinalIgnoreCase) ||
                        dev.FriendlyName.Contains(selectedEndpoint.Name, StringComparison.OrdinalIgnoreCase))
                    {
                        return dev;
                    }
                }
            }

            // Fallback: Dispositivo padrão de comunicações ou multimídia
            try
            {
                return enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Communications);
            }
            catch
            {
                return enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Multimedia);
            }
        }
        catch
        {
            return null;
        }
    }

    public void StopCapture()
    {
        if (_isRecording)
        {
            StopRecording();
        }

        if (_captureDevice != null)
        {
            try
            {
                _captureDevice.DataAvailable -= OnDataAvailable;
                _captureDevice.RecordingStopped -= OnRecordingStopped;
                _captureDevice.StopRecording();
                _captureDevice.Dispose();
            }
            catch
            {
                // Ignorar erros ao parar
            }

            _captureDevice = null;
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

                    if (File.Exists(outputPath))
                    {
                        try { File.Delete(outputPath); } catch { }
                    }

                    int sampleRate = _captureFormat?.SampleRate > 0 ? _captureFormat.SampleRate : DefaultSampleRate;
                    // Salva sempre em PCM 16-bit Mono padrão para máxima compatibilidade de reprodução
                    _waveFileWriter = new WaveFileWriter(outputPath,
                        new WaveFormat(sampleRate, OutputBitsPerSample, OutputChannels));
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
        if (e.BytesRecorded <= 0) return;

        var format = _captureFormat ?? new WaveFormat(DefaultSampleRate, OutputBitsPerSample, OutputChannels);
        float[] monoSamples = ExtractNormalizedMonoSamples(e.Buffer, e.BytesRecorded, format);
        int sampleCount = monoSamples.Length;
        if (sampleCount == 0) return;

        float peak = 0f;
        float sumSquares = 0f;
        float sensitivity = (float)_sensitivityMultiplier;

        // Prepara buffer PCM 16-bit caso esteja gravando em arquivo
        byte[]? pcm16Bytes = _isRecording ? new byte[sampleCount * 2] : null;

        for (int i = 0; i < sampleCount; i++)
        {
            float amplified = Math.Clamp(monoSamples[i] * sensitivity, -1.0f, 1.0f);
            monoSamples[i] = amplified;

            float abs = Math.Abs(amplified);
            if (abs > peak) peak = abs;
            sumSquares += amplified * amplified;

            if (pcm16Bytes != null)
            {
                short pcmSample = (short)Math.Clamp(amplified * 32767f, short.MinValue, short.MaxValue);
                pcm16Bytes[i * 2] = (byte)(pcmSample & 0xFF);
                pcm16Bytes[i * 2 + 1] = (byte)((pcmSample >> 8) & 0xFF);
            }
        }

        if (pcm16Bytes != null)
        {
            lock (_writerLock)
            {
                if (_isRecording && _waveFileWriter != null)
                {
                    try
                    {
                        _waveFileWriter.Write(pcm16Bytes, 0, pcm16Bytes.Length);
                    }
                    catch { }
                }
            }
        }

        float rms = (float)Math.Min(Math.Sqrt(sumSquares / sampleCount), 1.0);
        peak = Math.Min(peak, 1.0f);

        float[] waveformSamples;
        if (sampleCount <= MaxWaveformSamples)
        {
            waveformSamples = monoSamples;
        }
        else
        {
            waveformSamples = new float[MaxWaveformSamples];
            double step = (double)sampleCount / MaxWaveformSamples;

            for (int i = 0; i < MaxWaveformSamples; i++)
            {
                int sourceIndex = Math.Min((int)(i * step), sampleCount - 1);
                waveformSamples[i] = monoSamples[sourceIndex];
            }
        }

        DataAvailable?.Invoke(this, new AudioDataEventArgs
        {
            Peak = peak,
            RMS = rms,
            WaveformSamples = waveformSamples
        });
    }

    private static float[] ExtractNormalizedMonoSamples(byte[] buffer, int bytesRecorded, WaveFormat format)
    {
        int channels = Math.Max(1, format.Channels);
        int bits = format.BitsPerSample;
        bool isFloat = format.Encoding == WaveFormatEncoding.IeeeFloat;

        if (format is WaveFormatExtensible ext)
        {
            // KSDATAFORMAT_SUBTYPE_IEEE_FLOAT: 00000003-0000-0010-8000-00aa00389b71
            if (ext.SubFormat == new Guid("00000003-0000-0010-8000-00aa00389b71"))
            {
                isFloat = true;
            }
        }

        if (isFloat && bits == 32)
        {
            int frameSize = 4 * channels;
            int frameCount = bytesRecorded / frameSize;
            if (frameCount <= 0) return Array.Empty<float>();

            var result = new float[frameCount];
            for (int f = 0; f < frameCount; f++)
            {
                float sum = 0f;
                int baseOffset = f * frameSize;
                for (int c = 0; c < channels; c++)
                {
                    sum += BitConverter.ToSingle(buffer, baseOffset + c * 4);
                }
                result[f] = sum / channels;
            }
            return result;
        }

        if (bits == 16)
        {
            int frameSize = 2 * channels;
            int frameCount = bytesRecorded / frameSize;
            if (frameCount <= 0) return Array.Empty<float>();

            var result = new float[frameCount];
            for (int f = 0; f < frameCount; f++)
            {
                float sum = 0f;
                int baseOffset = f * frameSize;
                for (int c = 0; c < channels; c++)
                {
                    short s = BitConverter.ToInt16(buffer, baseOffset + c * 2);
                    sum += s / 32768f;
                }
                result[f] = sum / channels;
            }
            return result;
        }

        if (bits == 24)
        {
            int frameSize = 3 * channels;
            int frameCount = bytesRecorded / frameSize;
            if (frameCount <= 0) return Array.Empty<float>();

            var result = new float[frameCount];
            for (int f = 0; f < frameCount; f++)
            {
                float sum = 0f;
                int baseOffset = f * frameSize;
                for (int c = 0; c < channels; c++)
                {
                    int offset = baseOffset + c * 3;
                    int sample24 = (buffer[offset] | (buffer[offset + 1] << 8) | ((sbyte)buffer[offset + 2] << 16));
                    sum += sample24 / 8388608f;
                }
                result[f] = sum / channels;
            }
            return result;
        }

        if (bits == 32)
        {
            int frameSize = 4 * channels;
            int frameCount = bytesRecorded / frameSize;
            if (frameCount <= 0) return Array.Empty<float>();

            var result = new float[frameCount];
            for (int f = 0; f < frameCount; f++)
            {
                float sum = 0f;
                int baseOffset = f * frameSize;
                for (int c = 0; c < channels; c++)
                {
                    int s = BitConverter.ToInt32(buffer, baseOffset + c * 4);
                    sum += s / 2147483648f;
                }
                result[f] = sum / channels;
            }
            return result;
        }

        return Array.Empty<float>();
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
