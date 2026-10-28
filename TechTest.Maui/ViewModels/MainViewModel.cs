using System;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using TechTest.Maui.Services;

namespace TechTest.Maui.ViewModels;

public class MainViewModel : BaseViewModel
{
    private readonly IHardwareInfoService? _hardwareInfoService;
    private HardwareSpecs? _cachedSpecs;

    public ICommand SetResolutionCommand { get; }
    public ICommand LaunchFurMarkCommand { get; }
    public ICommand RunInternetTestCommand { get; }
    public ICommand CopySerialNumberCommand { get; }
    public ICommand CopyAllInfoCommand { get; }

    public MainViewModel(IHardwareInfoService? hardwareInfoService = null)
    {
        _hardwareInfoService = hardwareInfoService ?? IPlatformApplication.Current?.Services?.GetService<IHardwareInfoService>();

        SetResolutionCommand = new Command<string>(OnSetResolution);
        LaunchFurMarkCommand = new Command(async () => await OnLaunchFurMarkAsync());
        RunInternetTestCommand = new Command(async () => await OnRunInternetTestAsync());
        CopySerialNumberCommand = new Command(async () => await OnCopySerialNumberAsync());
        CopyAllInfoCommand = new Command(async () => await OnCopyAllInfoAsync());

        // Garante diretório de ferramentas e arquivo XML de configuração de internet na inicialização
        _ = Task.Run(() =>
        {
            ExternalToolsService.EnsureToolsDirectory();
            ExternalToolsService.EnsureAndLoadInternetConfigXml();
        });
    }

    private void OnSetResolution(string? parameter)
    {
        WindowSizeService.TrySetResolutionFromParameter(parameter);
    }

    public async Task OnLaunchFurMarkAsync()
    {
        var page = Application.Current?.MainPage;
#if WINDOWS
        var (started, message, _) = ExternalToolsService.LaunchFurMark();
        if (!started && page != null)
        {
            await page.DisplayAlert("FurMark", message, "OK");
        }
#else
        if (page != null)
        {
            await page.DisplayAlert("Indisponível", "FurMark está disponível apenas no Windows.", "OK");
        }
#endif
    }

    public async Task OnRunInternetTestAsync()
    {
        var page = Application.Current?.MainPage;
        try
        {
            var result = await ExternalToolsService.RunInternetDiagnosticsAsync();
            if (page != null)
            {
                string title = result.Success ? "🌐 Conectividade Aprovada" : "⚠️ Falha de Conectividade";
                await page.DisplayAlert(title, result.Summary, "OK");
            }
        }
        catch (Exception ex)
        {
            if (page != null)
            {
                await page.DisplayAlert("Erro no Diagnóstico de Rede", ex.ToString(), "OK");
            }
        }
    }

    private async Task<HardwareSpecs?> EnsureSpecsLoadedAsync()
    {
        if (_cachedSpecs != null) return _cachedSpecs;
        if (_hardwareInfoService != null)
        {
            _cachedSpecs = await _hardwareInfoService.GetHardwareSpecsAsync();
        }
        return _cachedSpecs;
    }

    public async Task OnCopySerialNumberAsync()
    {
        var page = Application.Current?.MainPage;
        try
        {
            var specs = await EnsureSpecsLoadedAsync();
            string serial = specs?.ServiceTag ?? "Desconhecido";
            await ClipboardService.CopyTextAsync(serial);

            if (page != null)
            {
                await page.DisplayAlert("Sucesso", $"Número de Série copiado:\n{serial}", "OK");
            }
        }
        catch (Exception ex)
        {
            if (page != null)
            {
                await page.DisplayAlert("Erro ao copiar SN", ex.ToString(), "OK");
            }
        }
    }

    public async Task OnCopyAllInfoAsync()
    {
        var page = Application.Current?.MainPage;
        try
        {
            var specs = await EnsureSpecsLoadedAsync();
            string report = SpecsViewModel.BuildConsolidatedReport(specs);
            await ClipboardService.CopyTextAsync(report);

            if (page != null)
            {
                await page.DisplayAlert("Sucesso", "Relatório consolidado copiado para a área de transferência!", "OK");
            }
        }
        catch (Exception ex)
        {
            if (page != null)
            {
                await page.DisplayAlert("Erro ao copiar relatório", ex.ToString(), "OK");
            }
        }
    }
}
