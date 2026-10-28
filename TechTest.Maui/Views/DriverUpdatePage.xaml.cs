using System;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using TechTest.Maui.Services;

namespace TechTest.Maui.Views;

public partial class DriverUpdatePage : ContentPage
{
    private readonly IHardwareInfoService? _hardware;
    private readonly IClipboardService _clipboardService;
    private HardwareSpecs? _specs;
    private OemVendor _vendor = OemVendor.Unknown;

    public DriverUpdatePage()
    {
        InitializeComponent();
        var services = IPlatformApplication.Current?.Services;
        _hardware = services?.GetService<IHardwareInfoService>();
        _clipboardService = services?.GetService<IClipboardService>() ?? new ClipboardService();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        LoadingIndicator.IsRunning = true;
        LoadingIndicator.IsVisible = true;
        try
        {
            if (_hardware == null)
            {
                await DisplayAlert("Indisponível", "Serviço de hardware não disponível nesta plataforma.", "OK");
                return;
            }

            _specs = await _hardware.GetHardwareSpecsAsync();
            _vendor = OemDriverUpdater.Detect(_specs.Manufacturer);

            LblManufacturer.Text = _specs.Manufacturer;
            LblModel.Text = _specs.Model;
            LblFamilySku.Text = $"{Dash(_specs.SystemFamily)} / {Dash(_specs.SystemSku)}";
            LblBaseBoard.Text = $"{Dash(_specs.BaseBoardManufacturer)} {_specs.BaseBoardProduct}".Trim();
            LblSerial.Text = _specs.ServiceTag;
            LblTool.Text = OemDriverUpdater.GetToolDescription(_vendor);

            bool native = OemDriverUpdater.HasNativeOemTool(_vendor);
            BtnOemScan.IsEnabled = native && _vendor == OemVendor.Dell; // /scan só existe no Dell CLI
            BtnOemApply.IsEnabled = native;
            BtnOemApply.Text = _vendor switch
            {
                OemVendor.Dell => "⬇️ Aplicar Atualizações (Dell)",
                OemVendor.Lenovo => "⬇️ Abrir/Instalar Lenovo System Update",
                OemVendor.HP => "⬇️ Abrir/Instalar HP Support Assistant",
                _ => "⬇️ Sem utilitário OEM"
            };

            SectionId.IsVisible = true;
            SectionActions.IsVisible = true;
            SectionProblems.IsVisible = true;

            await LoadProblemsAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Erro", $"Erro ao carregar informações:\n{ex}", "OK");
        }
        finally
        {
            LoadingIndicator.IsRunning = false;
            LoadingIndicator.IsVisible = false;
        }
    }

    private async Task LoadProblemsAsync()
    {
        if (_hardware == null) return;
        var list = await _hardware.GetProblemDevicesAsync();
        ProblemList.ItemsSource = list;
        LblNoProblems.IsVisible = list.Count == 0;
        LblProblemsTitle.Text = list.Count == 0
            ? "⚠️ Dispositivos com Falha de Driver"
            : $"⚠️ Dispositivos com Falha de Driver ({list.Count})";
    }

    private static string Dash(string? s) => string.IsNullOrWhiteSpace(s) ? "—" : s;

    private void ShowStatus(OemActionResult r)
    {
        LblStatus.IsVisible = true;
        LblStatus.Text = (r.Success ? "✅ " : "❌ ") + r.Message;
        LblStatus.TextColor = (Color)Application.Current!.Resources[r.Success ? "Success" : "Error"];
    }

    private async void OnOemScanClicked(object? sender, EventArgs e)
        => ShowStatus(await OemDriverUpdater.RunOemToolAsync(_vendor, applyUpdates: false));

    private async void OnOemApplyClicked(object? sender, EventArgs e)
        => ShowStatus(await OemDriverUpdater.RunOemToolAsync(_vendor, applyUpdates: true));

    private async void OnCopySerialClicked(object? sender, EventArgs e)
    {
        try
        {
            string rawSn = _specs?.ServiceTag ?? "";
            string cleanSn = HardwareReportFormatter.CleanSerialNumber(rawSn);
            if (string.IsNullOrEmpty(cleanSn))
                cleanSn = rawSn.Trim();

            await _clipboardService.SetTextAsync(cleanSn);
            ShowStatus(new OemActionResult { Success = true, Message = $"Serial/ID copiado: {cleanSn}" });
        }
        catch (Exception ex)
        {
            ShowStatus(new OemActionResult { Success = false, Message = $"Erro ao copiar serial: {ex.Message}" });
            await DisplayAlert("Erro", $"Erro ao copiar para a área de transferência:\n{ex}", "OK");
        }
    }

    private async void OnOpenSupportClicked(object? sender, EventArgs e)
    {
        if (_specs == null) return;

        try
        {
            // Copia o serial limpo automaticamente para colar na página do fabricante
            string cleanSn = HardwareReportFormatter.CleanSerialNumber(_specs.ServiceTag);
            if (!string.IsNullOrEmpty(cleanSn))
            {
                await _clipboardService.SetTextAsync(cleanSn);
            }

            string url = OemDriverUpdater.GetSupportUrl(_vendor, _specs.Manufacturer, _specs.Model, _specs.ServiceTag);
            bool ok = await OemDriverUpdater.OpenSupportPageAsync(url);
            ShowStatus(new OemActionResult
            {
                Success = ok,
                Message = ok ? "Página de suporte aberta (serial copiado para a área de transferência)." : "Não foi possível abrir o navegador."
            });
        }
        catch (Exception ex)
        {
            await DisplayAlert("Erro", $"Erro ao abrir página de suporte:\n{ex}", "OK");
        }
    }

    private void OnWindowsUpdateClicked(object? sender, EventArgs e)
        => ShowStatus(OemDriverUpdater.RunWindowsUpdateDriverSearch());

    private async void OnRefreshProblemsClicked(object? sender, EventArgs e) => await LoadProblemsAsync();

    private async void OnBackClicked(object? sender, EventArgs e)
        => await Shell.Current.GoToAsync("//MainPage");
}
