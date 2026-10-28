using System;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using TechTest.Maui.Services;

namespace TechTest.Maui.ViewModels;

public class SpecsViewModel : BaseViewModel
{
    private readonly IHardwareInfoService? _hardwareInfoService;
    private readonly IBatteryService? _batteryService;
    private readonly IClipboardService _clipboardService;

    private HardwareSpecs? _specs;
    private BatteryStatus? _batteryStatus;
    private BatteryReport? _batteryReport;

    private string _serialNumber = "—";
    private string _copySnButtonText = "📋 Copiar SN";
    private string _copyAllButtonText = "📋 Copiar Tudo";
    private bool _isCopySnEnabled = true;
    private bool _isCopyAllEnabled = true;

    public SpecsViewModel(
        IHardwareInfoService? hardwareInfoService = null,
        IBatteryService? batteryService = null,
        IClipboardService? clipboardService = null)
    {
        var services = IPlatformApplication.Current?.Services;
        _hardwareInfoService = hardwareInfoService ?? services?.GetService<IHardwareInfoService>();
        _batteryService = batteryService ?? services?.GetService<IBatteryService>();
        _clipboardService = clipboardService ?? services?.GetService<IClipboardService>() ?? new ClipboardService();

        CopySerialNumberCommand = new Command(async () => await ExecuteCopySerialNumberAsync(), () => IsCopySnEnabled);
        CopyAllInfoCommand = new Command(async () => await ExecuteCopyAllInfoAsync(), () => IsCopyAllEnabled);
    }

    public HardwareSpecs? Specs
    {
        get => _specs;
        set
        {
            if (SetProperty(ref _specs, value) && value != null)
            {
                SerialNumber = value.ServiceTag;
            }
        }
    }

    public BatteryStatus? BatteryStatus
    {
        get => _batteryStatus;
        set => SetProperty(ref _batteryStatus, value);
    }

    public BatteryReport? BatteryReport
    {
        get => _batteryReport;
        set => SetProperty(ref _batteryReport, value);
    }

    public string SerialNumber
    {
        get => _serialNumber;
        set => SetProperty(ref _serialNumber, value);
    }

    public string CopySnButtonText
    {
        get => _copySnButtonText;
        set => SetProperty(ref _copySnButtonText, value);
    }

    public string CopyAllButtonText
    {
        get => _copyAllButtonText;
        set => SetProperty(ref _copyAllButtonText, value);
    }

    public bool IsCopySnEnabled
    {
        get => _isCopySnEnabled;
        set
        {
            if (SetProperty(ref _isCopySnEnabled, value))
            {
                (CopySerialNumberCommand as Command)?.ChangeCanExecute();
            }
        }
    }

    public bool IsCopyAllEnabled
    {
        get => _isCopyAllEnabled;
        set
        {
            if (SetProperty(ref _isCopyAllEnabled, value))
            {
                (CopyAllInfoCommand as Command)?.ChangeCanExecute();
            }
        }
    }

    public ICommand CopySerialNumberCommand { get; }
    public ICommand CopyAllInfoCommand { get; }

    public async Task ExecuteCopySerialNumberAsync()
    {
        if (!IsCopySnEnabled) return;

        string originalText = CopySnButtonText;
        IsCopySnEnabled = false;

        try
        {
            string rawSn = _specs?.ServiceTag ?? SerialNumber;
            string cleanSn = HardwareReportFormatter.CleanSerialNumber(rawSn);

            if (string.IsNullOrEmpty(cleanSn))
            {
                // Se ainda não carregou ou veio placeholder, tenta buscar diretamente
                if (_hardwareInfoService != null && _specs == null)
                {
                    _specs = await _hardwareInfoService.GetHardwareSpecsAsync();
                    cleanSn = HardwareReportFormatter.CleanSerialNumber(_specs.ServiceTag);
                }
            }

            if (string.IsNullOrEmpty(cleanSn))
            {
                cleanSn = string.IsNullOrWhiteSpace(rawSn) ? "N/D" : rawSn.Trim();
            }

            await _clipboardService.SetTextAsync(cleanSn);

            CopySnButtonText = "✅ SN Copiado!";
            await Task.Delay(1800);
        }
        catch (Exception ex)
        {
            await ShowErrorAlertAsync("Erro ao copiar o Número de Série (SN) para a área de transferência:", ex);
        }
        finally
        {
            CopySnButtonText = originalText;
            IsCopySnEnabled = true;
        }
    }

    public async Task ExecuteCopyAllInfoAsync(HardwareSpecs? fallbackSpecs = null)
    {
        if (!IsCopyAllEnabled) return;

        string originalText = CopyAllButtonText;
        IsCopyAllEnabled = false;

        try
        {
            var specs = _specs ?? fallbackSpecs;
            if (specs == null && _hardwareInfoService != null)
            {
                specs = await _hardwareInfoService.GetHardwareSpecsAsync();
                _specs = specs;
            }

            if (_batteryService != null)
            {
                try
                {
                    _batteryStatus ??= await Task.Run(() => _batteryService.GetCurrentStatus());
                    if (_batteryStatus.HasBattery && _batteryReport == null)
                    {
                        var reportTask = _batteryService.GenerateReportAsync();
                        if (await Task.WhenAny(reportTask, Task.Delay(TimeSpan.FromSeconds(15))) == reportTask)
                        {
                            _batteryReport = await reportTask;
                        }
                    }
                }
                catch
                {
                    // Se o relatório de bateria falhar ou expirar, prossegue com os dados disponíveis
                }
            }

            string formattedReport = HardwareReportFormatter.ToText(specs, _batteryStatus, _batteryReport);
            await _clipboardService.SetTextAsync(formattedReport);

            CopyAllButtonText = "✅ Copiado!";
            await Task.Delay(1800);
        }
        catch (Exception ex)
        {
            await ShowErrorAlertAsync("Erro ao copiar para a área de transferência:", ex);
        }
        finally
        {
            CopyAllButtonText = originalText;
            IsCopyAllEnabled = true;
        }
    }

    private static async Task ShowErrorAlertAsync(string prefix, Exception ex)
    {
        string details = string.IsNullOrWhiteSpace(ex.Message)
            ? ex.ToString()
            : $"{ex.Message}\n\nDetalhes técnicos:\n{ex}";

        var page = Application.Current?.MainPage;
        if (page != null)
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
                page.DisplayAlert("Erro", $"{prefix}\n{details}", "OK"));
        }
    }
}
