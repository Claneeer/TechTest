using System.Windows.Input;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace TechTest.Maui.ViewModels;

public class KeyboardTestViewModel : BaseViewModel
{
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
