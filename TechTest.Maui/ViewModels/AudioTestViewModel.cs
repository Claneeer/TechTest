using System;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using TechTest.Maui.Services;

namespace TechTest.Maui.ViewModels;

public class AudioTestViewModel : BaseViewModel
{
    private readonly IAudioPlaybackService _playbackService;

    private string _statusText = "Pronto para testar";
    private Color _statusColor = Color.FromArgb("#A0A0B0");
    private bool _isPlaying;
    private AudioChannel _activeChannel = AudioChannel.Both;
    private double _frequency = 440;
    private double _volume = 1.0;
    private int _soundType = 0; // 0 = Sine, 1 = Square, 2 = WhiteNoise, 3 = Sweep, 4 = Beep

    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    public Color StatusColor
    {
        get => _statusColor;
        set => SetProperty(ref _statusColor, value);
    }

    public bool IsPlaying
    {
        get => _isPlaying;
        set => SetProperty(ref _isPlaying, value);
    }

    public AudioChannel ActiveChannel
    {
        get => _activeChannel;
        set => SetProperty(ref _activeChannel, value);
    }

    public double Frequency
    {
        get => _frequency;
        set
        {
            if (SetProperty(ref _frequency, value))
            {
                if (IsPlaying && _playbackService != null)
                {
                    _playbackService.SetFrequency(value);
                }
            }
        }
    }

    public double Volume
    {
        get => _volume;
        set
        {
            if (SetProperty(ref _volume, value))
            {
                if (IsPlaying && _playbackService != null)
                {
                    _playbackService.SetVolume(value);
                }
            }
        }
    }

    public int SoundType
    {
        get => _soundType;
        set => SetProperty(ref _soundType, value);
    }

    public AudioTestViewModel(IAudioPlaybackService playbackService)
    {
        _playbackService = playbackService;

        if (_playbackService != null)
        {
            _playbackService.PlaybackStopped += OnPlaybackStopped;
        }
    }

    private void OnPlaybackStopped(object sender, EventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            IsPlaying = false;
            StatusText = "Pronto para testar";
            StatusColor = Color.FromArgb("#A0A0B0");
        });
    }

    public void PlayTone(AudioChannel channel)
    {
        if (_playbackService == null) return;

        try
        {
            ActiveChannel = channel;
            var type = (SoundType)SoundType;

            _playbackService.PlayTone(Frequency, channel, type, Volume);
            IsPlaying = true;

            string chText = channel switch
            {
                AudioChannel.Left => "Esquerdo",
                AudioChannel.Right => "Direito",
                _ => "Ambos"
            };

            string soundText = type switch
            {
                Services.SoundType.Sine => "Tom Senoidal",
                Services.SoundType.Square => "Tom Quadrado",
                Services.SoundType.WhiteNoise => "Ruído Branco",
                Services.SoundType.Sweep => "Varredura (100Hz - 10kHz)",
                Services.SoundType.Beep => "Bip pulsante",
                _ => "Tom"
            };

            StatusText = $"Tocando {soundText} no canal {chText}...";
            StatusColor = Color.FromArgb("#22C55E"); // Green
        }
        catch (Exception ex)
        {
            StatusText = $"Erro: {ex.Message}";
            StatusColor = Color.FromArgb("#EF4444"); // Red
        }
    }

    public void StopTone()
    {
        if (_playbackService == null) return;

        try
        {
            _playbackService.StopTone();
            IsPlaying = false;
            StatusText = "Pronto para testar";
            StatusColor = Color.FromArgb("#A0A0B0");
        }
        catch
        {
            // Silencioso
        }
    }
}
