using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace TechTest.Maui.Services;

public class AudioDataEventArgs : EventArgs
{
    public float Peak { get; set; }
    public float RMS { get; set; }
    public float[] WaveformSamples { get; set; } = Array.Empty<float>();
}

public interface IAudioCaptureService : IDisposable
{
    bool IsCapturing { get; }
    bool IsRecording { get; }
    double SensitivityMultiplier { get; set; }

    event EventHandler<AudioDataEventArgs>? DataAvailable;

    Task<List<string>> GetDevicesAsync();
    void StartCapture(int deviceIndex, double sensitivityMultiplier);
    void StopCapture();
    Task StartRecording(string outputPath);
    void StopRecording();
}
