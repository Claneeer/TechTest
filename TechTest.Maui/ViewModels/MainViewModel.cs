using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using Microsoft.Maui.Controls;
using TechTest.Maui.Services;

namespace TechTest.Maui.ViewModels;

public class MainViewModel : BaseViewModel
{
    private readonly IWindowResolutionService _resolutionService;
    private ResolutionOption? _selectedResolution;
    private string _resolutionStatusText = "";

    public MainViewModel(IWindowResolutionService? resolutionService = null)
    {
        _resolutionService = resolutionService
            ?? IPlatformApplication.Current?.Services?.GetService<IWindowResolutionService>()
            ?? new WindowResolutionService();

        ResolutionPresets = _resolutionService.Presets;
        SetResolutionCommand = new Command<object?>(OnSetResolutionExecuted);
    }

    public IReadOnlyList<ResolutionOption> ResolutionPresets { get; }

    public ResolutionOption? SelectedResolution
    {
        get => _selectedResolution;
        set
        {
            if (SetProperty(ref _selectedResolution, value) && value != null)
            {
                var result = _resolutionService.ApplyPreset(value);
                ResolutionStatusText = result.Message;
            }
        }
    }

    public string ResolutionStatusText
    {
        get => _resolutionStatusText;
        set => SetProperty(ref _resolutionStatusText, value);
    }

    public ICommand SetResolutionCommand { get; }

    private void OnSetResolutionExecuted(object? parameter)
    {
        if (parameter is ResolutionOption preset)
        {
            SelectedResolution = preset;
            return;
        }

        if (parameter is string spec && !string.IsNullOrWhiteSpace(spec))
        {
            // Aceita formato "1920x1080" ou "1920x1200"
            var parts = spec.Split(new[] { 'x', 'X' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 2 &&
                int.TryParse(parts[0].Trim(), out int w) &&
                int.TryParse(parts[1].Trim(), out int h))
            {
                var matched = ResolutionPresets.FirstOrDefault(p => p.Width == w && p.Height == h);
                if (matched != null)
                {
                    SelectedResolution = matched;
                }
                else
                {
                    var res = _resolutionService.ApplyResolution(w, h);
                    ResolutionStatusText = res.Message;
                }
            }
        }
    }
}
