using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using TechTest.Maui.Services;

namespace TechTest.Maui.ViewModels;

public class SpecsViewModel : BaseViewModel
{
    private readonly IHardwareInfoService? _hardwareInfoService;
    private HardwareSpecs? _specs;

    public HardwareSpecs? Specs
    {
        get => _specs;
        set => SetProperty(ref _specs, value);
    }

    public ICommand CopySerialNumberCommand { get; }
    public ICommand CopyAllInfoCommand { get; }

    public SpecsViewModel(IHardwareInfoService? hardwareInfoService)
    {
        _hardwareInfoService = hardwareInfoService;
        CopySerialNumberCommand = new Command(async () => await CopySerialNumberAsync());
        CopyAllInfoCommand = new Command(async () => await CopyAllInfoAsync());
    }

    public async Task<HardwareSpecs?> LoadSpecsAsync()
    {
        if (_hardwareInfoService == null) return null;
        Specs = await _hardwareInfoService.GetHardwareSpecsAsync();
        return Specs;
    }

    public async Task CopySerialNumberAsync()
    {
        try
        {
            string serial = Specs?.ServiceTag ?? "Desconhecido";
            await ClipboardService.CopyTextAsync(serial);

            var page = Application.Current?.MainPage;
            if (page != null)
            {
                await page.DisplayAlert("Sucesso", $"Número de Série copiado:\n{serial}", "OK");
            }
        }
        catch (Exception ex)
        {
            var page = Application.Current?.MainPage;
            if (page != null)
            {
                await page.DisplayAlert("Erro", $"Erro ao copiar Número de Série para a área de transferência:\n{ex}", "OK");
            }
        }
    }

    public async Task CopyAllInfoAsync()
    {
        try
        {
            string reportText = BuildConsolidatedReport(Specs);
            await ClipboardService.CopyTextAsync(reportText);

            var page = Application.Current?.MainPage;
            if (page != null)
            {
                await page.DisplayAlert("Sucesso", "Relatório consolidado copiado para a área de transferência!", "OK");
            }
        }
        catch (Exception ex)
        {
            var page = Application.Current?.MainPage;
            if (page != null)
            {
                await page.DisplayAlert("Erro", $"Erro ao copiar para a área de transferência:\n{ex}", "OK");
            }
        }
    }

    public static string BuildConsolidatedReport(HardwareSpecs? specs)
    {
        if (specs == null)
        {
            return "=== RELATÓRIO DE HARDWARE TECHTEST ===\nEspecificações ainda não carregadas.";
        }

        string storageSummary = specs.StorageDevices != null && specs.StorageDevices.Count > 0
            ? string.Join(" | ", specs.StorageDevices.Select(d => $"{d.Name} [{d.Type}] {d.FormattedTotalSize} ({d.FormattedFreeSpace} livre)"))
            : "Desconhecido";

        string gpuSummary = specs.GPUs != null && specs.GPUs.Count > 0
            ? string.Join(" | ", specs.GPUs.Select(g => g.Name))
            : "Integrada / Desconhecida";

        var sb = new StringBuilder();
        sb.AppendLine("=== RELATÓRIO DE HARDWARE TECHTEST ===");
        sb.AppendLine($"Fabricante: {specs.Manufacturer}");
        sb.AppendLine($"Modelo: {specs.Model}");
        sb.AppendLine($"SN (Service Tag): {specs.ServiceTag}");
        sb.AppendLine($"CPU: {specs.CPU}");
        sb.AppendLine($"RAM: {specs.RAM}");
        sb.AppendLine($"GPU: {gpuSummary}");
        sb.AppendLine($"Armazenamento: {storageSummary}");
        sb.AppendLine($"SO: {specs.OSVersion} ({specs.MachineName})");
        sb.AppendLine($"Bateria: {specs.BatterySummary}");
        sb.AppendLine("======================================");
        return sb.ToString();
    }
}
