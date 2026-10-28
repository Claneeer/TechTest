using System;
using System.Threading.Tasks;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using TechTest.Maui.Services;

namespace TechTest.Maui.Views;

public partial class DriverUpdatePage : ContentPage
{
    private readonly IHardwareInfoService? _hardware;
    private HardwareSpecs? _specs;
    private OemVendor _vendor = OemVendor.Unknown;

    public DriverUpdatePage()
    {
        InitializeComponent();
        _hardware = IPlatformApplication.Current?.Services?.GetService<IHardwareInfoService>();
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

            LblManufacturer.Text = Dash(_specs.Manufacturer);
            LblModel.Text = Dash(_specs.Model);
            LblFamilySku.Text = $"{Dash(_specs.SystemFamily)} / {Dash(_specs.SystemSku)}";
            LblBaseBoard.Text = $"{Dash(_specs.BaseBoardManufacturer)} {Dash(_specs.BaseBoardProduct)}".Trim();
            LblSerial.Text = Dash(_specs.ServiceTag);
            LblTool.Text = OemDriverUpdater.GetToolDescription(_vendor);

            bool native = OemDriverUpdater.HasNativeOemTool(_vendor);
            BtnOemScan.IsEnabled = native && _vendor == OemVendor.Dell;
            BtnOemApply.IsEnabled = native;
            BtnOemApply.Text = _vendor switch
            {
                OemVendor.Dell => "⬇️ Aplicar Atualizações (Dell)",
                OemVendor.Lenovo => "⬇️ Abrir/Instalar Lenovo System Update",
                OemVendor.HP => "⬇️ Abrir/Instalar HP Support Assistant",
                _ => "⬇️ Sem utilitário OEM disponível"
            };

            SectionId.IsVisible = true;
            SectionActions.IsVisible = true;
            SectionProblems.IsVisible = true;

            await LoadProblemsAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Erro", $"Erro ao carregar informações de drivers:\n{ex}", "OK");
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
        try
        {
            var list = await _hardware.GetProblemDevicesAsync();
            ProblemList.ItemsSource = list;
            LblNoProblems.IsVisible = list.Count == 0;
            LblProblemsTitle.Text = list.Count == 0
                ? "⚠️ Dispositivos com Falha de Driver"
                : $"⚠️ Dispositivos com Falha de Driver ({list.Count})";
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Erro ao listar problemas de driver: {ex}");
        }
    }

    private static string Dash(string? s) => string.IsNullOrWhiteSpace(s) ? "—" : s;

    private void ShowStatus(OemActionResult r)
    {
        try
        {
            LblStatus.IsVisible = true;
            LblStatus.Text = (r.Success ? "✅ " : "❌ ") + r.Message;

            if (r.Success)
            {
                LblStatus.TextColor = Application.Current?.Resources.TryGetValue("Success", out var cSuccess) == true && cSuccess is Color colS
                    ? colS
                    : Color.FromArgb("#22C55E");
            }
            else
            {
                LblStatus.TextColor = Application.Current?.Resources.TryGetValue("Error", out var cErr) == true && cErr is Color colE
                    ? colE
                    : Color.FromArgb("#EF4444");
            }
        }
        catch
        {
            LblStatus.IsVisible = true;
            LblStatus.Text = r.Message;
        }
    }

    private async void OnOemScanClicked(object? sender, EventArgs e)
    {
        try
        {
            ShowStatus(new OemActionResult { Success = true, Message = "A iniciar verificação OEM..." });
            var result = await Task.Run(() => OemDriverUpdater.RunOemToolAsync(_vendor, applyUpdates: false));
            ShowStatus(result);
        }
        catch (Exception ex)
        {
            ShowStatus(new OemActionResult { Success = false, Message = $"Falha ao executar: {ex}" });
        }
    }

    private async void OnOemApplyClicked(object? sender, EventArgs e)
    {
        try
        {
            ShowStatus(new OemActionResult { Success = true, Message = "A processar utilitário OEM..." });
            var result = await Task.Run(() => OemDriverUpdater.RunOemToolAsync(_vendor, applyUpdates: true));
            ShowStatus(result);
        }
        catch (Exception ex)
        {
            ShowStatus(new OemActionResult { Success = false, Message = $"Falha ao executar: {ex}" });
        }
    }

    private async void OnCopySerialClicked(object? sender, EventArgs e)
    {
        try
        {
            string text = _specs?.ServiceTag ?? "";
            if (string.IsNullOrWhiteSpace(text) || text == "—" || text == "Desconhecido")
            {
                ShowStatus(new OemActionResult { Success = false, Message = "Nenhum Serial/Service Tag identificado para copiar." });
                return;
            }

            await ClipboardService.CopyTextAsync(text);
            ShowStatus(new OemActionResult { Success = true, Message = $"Serial/ID copiado: {text}" });
        }
        catch (Exception ex)
        {
            ShowStatus(new OemActionResult { Success = false, Message = $"Erro ao copiar para a área de transferência: {ex}" });
            await DisplayAlert("Erro", $"Erro ao copiar para a área de transferência:\n{ex}", "OK");
        }
    }

    private async void OnOpenSupportClicked(object? sender, EventArgs e)
    {
        try
        {
            if (_specs == null)
            {
                ShowStatus(new OemActionResult { Success = false, Message = "Especificações ainda não carregadas." });
                return;
            }

            if (!string.IsNullOrWhiteSpace(_specs.ServiceTag) && _specs.ServiceTag != "—" && _specs.ServiceTag != "Desconhecido")
            {
                try { await ClipboardService.CopyTextAsync(_specs.ServiceTag); } catch { }
            }

            string url = OemDriverUpdater.GetSupportUrl(_vendor, _specs.Manufacturer, _specs.Model, _specs.ServiceTag);
            bool ok = await OemDriverUpdater.OpenSupportPageAsync(url);
            ShowStatus(new OemActionResult
            {
                Success = ok,
                Message = ok ? "Página de suporte aberta (Serial copiado para a área de transferência)." : "Não foi possível abrir o navegador padrão."
            });
        }
        catch (Exception ex)
        {
            ShowStatus(new OemActionResult { Success = false, Message = $"Erro ao abrir suporte: {ex}" });
        }
    }

    private async void OnWindowsUpdateClicked(object? sender, EventArgs e)
    {
        try
        {
            ShowStatus(new OemActionResult { Success = true, Message = "A solicitar verificação no Windows Update..." });
            var result = await Task.Run(() => OemDriverUpdater.RunWindowsUpdateDriverSearch());
            ShowStatus(result);
        }
        catch (Exception ex)
        {
            ShowStatus(new OemActionResult { Success = false, Message = $"Erro ao invocar Windows Update: {ex}" });
        }
    }

    private async void OnRefreshProblemsClicked(object? sender, EventArgs e) => await LoadProblemsAsync();

    private async void OnBackClicked(object? sender, EventArgs e)
        => await Shell.Current.GoToAsync("//MainPage");
}
