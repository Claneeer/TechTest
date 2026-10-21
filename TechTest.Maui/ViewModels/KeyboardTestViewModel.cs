using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows.Input;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace TechTest.Maui.ViewModels;

/// <summary>Chip exibido na barra de histórico de teclas.</summary>
public class KeyHistoryItem
{
    public string Label { get; init; } = "";
    public bool IsChatter { get; init; }
    public long ChatterMs { get; init; }
    public string Detail => IsChatter ? $"{ChatterMs} ms" : "";
    public bool HasDetail => IsChatter;
    public Color BackgroundColor => IsChatter ? Color.FromArgb("#EF4444") : Color.FromArgb("#1C1C38");
    public Color BorderColor => IsChatter ? Color.FromArgb("#FCA5A5") : Color.FromArgb("#32325A");
    public Color TextColor => IsChatter ? Colors.White : Color.FromArgb("#F0F0FF");
}

public class KeyboardTestViewModel : BaseViewModel
{
    /// <summary>Intervalo (ms) abaixo do qual um KeyUp->KeyDown da mesma tecla é considerado chattering.</summary>
    public const long ChatterThresholdMs = 35;
    public const int MaxHistory = 20;

    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly Dictionary<string, long> _lastKeyUpMs = new();
    private readonly HashSet<string> _held = new();
    private int _keysPressedCount;
    private int _peakKeysPressed;
    private int _chatterCount;
    private string _lastChatterText = "Nenhum chattering detectado";
    private Color _lastChatterColor = Color.FromArgb("#9494B8");

    public ObservableCollection<KeyHistoryItem> History { get; } = new();

    /// <summary>Quantidade de teclas pressionadas simultaneamente agora.</summary>
    public int KeysPressedCount { get => _keysPressedCount; private set => SetProperty(ref _keysPressedCount, value); }

    /// <summary>Pico máximo de teclas simultâneas (rollover).</summary>
    public int PeakKeysPressed { get => _peakKeysPressed; private set => SetProperty(ref _peakKeysPressed, value); }

    public int ChatterCount { get => _chatterCount; private set => SetProperty(ref _chatterCount, value); }
    public string LastChatterText { get => _lastChatterText; private set => SetProperty(ref _lastChatterText, value); }
    public Color LastChatterColor { get => _lastChatterColor; private set => SetProperty(ref _lastChatterColor, value); }

    /// <summary>
    /// Registra o KeyDown. Retorna o intervalo em ms se detectou chattering; caso contrário null.
    /// Repetições automáticas (tecla segurada) são ignoradas.
    /// </summary>
    public long? RegisterKeyDown(string keyId)
    {
        if (string.IsNullOrEmpty(keyId)) return null;
        if (!_held.Add(keyId)) return null; // auto-repeat de tecla segurada

        long now = _clock.ElapsedMilliseconds;
        long? chatter = null;

        if (_lastKeyUpMs.TryGetValue(keyId, out long upAt))
        {
            long delta = now - upAt;
            if (delta < ChatterThresholdMs)
                chatter = delta;
        }

        KeysPressedCount = _held.Count;
        if (_held.Count > PeakKeysPressed) PeakKeysPressed = _held.Count;

        History.Insert(0, new KeyHistoryItem { Label = keyId, IsChatter = chatter.HasValue, ChatterMs = chatter ?? 0 });
        while (History.Count > MaxHistory) History.RemoveAt(History.Count - 1);

        if (chatter.HasValue)
        {
            ChatterCount++;
            LastChatterText = $"⚠ Chattering em [{keyId}]: {chatter.Value} ms (< {ChatterThresholdMs} ms)";
            LastChatterColor = Color.FromArgb("#EF4444");
        }

        return chatter;
    }

    public void RegisterKeyUp(string keyId)
    {
        if (string.IsNullOrEmpty(keyId)) return;
        _held.Remove(keyId);
        _lastKeyUpMs[keyId] = _clock.ElapsedMilliseconds;
        KeysPressedCount = _held.Count;
    }

    public void ResetHistory()
    {
        History.Clear();
        _held.Clear();
        _lastKeyUpMs.Clear();
        KeysPressedCount = 0;
        PeakKeysPressed = 0;
        ChatterCount = 0;
        LastChatterText = "Nenhum chattering detectado";
        LastChatterColor = Color.FromArgb("#9494B8");
    }

    private int _testedCount = 0;
    private int _totalKeys = 1; // avoid divide by zero
    private double _testedPercent = 0.0;
    private bool _hasNumpad = true;
    private string _statusText = "Aperte qualquer tecla para iniciar o teste";
    private Color _statusColor = Color.FromArgb("#A0A0B0");

    public int TestedCount
    {
        get => _testedCount;
        set
        {
            if (SetProperty(ref _testedCount, value))
            {
                UpdatePercentageAndStatus();
            }
        }
    }

    public int TotalKeys
    {
        get => _totalKeys;
        set
        {
            if (SetProperty(ref _totalKeys, value))
            {
                UpdatePercentageAndStatus();
            }
        }
    }

    public double TestedPercent
    {
        get => _testedPercent;
        set => SetProperty(ref _testedPercent, value);
    }

    public bool HasNumpad
    {
        get => _hasNumpad;
        set => SetProperty(ref _hasNumpad, value);
    }

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

    public KeyboardTestViewModel()
    {
    }

    private void UpdatePercentageAndStatus()
    {
        if (TotalKeys > 0)
        {
            TestedPercent = (double)TestedCount / TotalKeys;
        }
        else
        {
            TestedPercent = 0.0;
        }

        if (TestedCount == 0)
        {
            StatusText = "Aperte qualquer tecla para iniciar o teste";
            StatusColor = Color.FromArgb("#A0A0B0");
        }
        else if (TestedCount < TotalKeys)
        {
            StatusText = $"Testando... {TestedCount} de {TotalKeys} teclas verificadas.";
            StatusColor = Color.FromArgb("#EAB308"); // Yellow/Warning
        }
        else
        {
            StatusText = "Excelente! Todas as teclas testadas com sucesso!";
            StatusColor = Color.FromArgb("#22C55E"); // Green
        }
    }

    public void Reset()
    {
        TestedCount = 0;
        UpdatePercentageAndStatus();
    }
}
