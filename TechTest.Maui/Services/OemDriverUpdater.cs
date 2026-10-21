using System.ComponentModel;
using System.Diagnostics;

namespace TechTest.Maui.Services;

public enum OemVendor
{
    Unknown,
    Dell,
    Lenovo,
    HP,
    Asus,
    Acer,
    Samsung,
    Positivo,
    Msi,
    Gigabyte,
    Apple
}

public class OemActionResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = "";
}

/// <summary>
/// Identifica o fabricante e dispara utilitários OEM via winget / CLI (com elevação UAC quando necessário).
/// </summary>
public static class OemDriverUpdater
{
    private const string DellCliPath = @"C:\Program Files\Dell\CommandUpdate\dcu-cli.exe";
    private const string DellCliPathX86 = @"C:\Program Files (x86)\Dell\CommandUpdate\dcu-cli.exe";

    public static OemVendor Detect(string? manufacturer)
    {
        string m = (manufacturer ?? "").ToLowerInvariant();
        if (m.Contains("dell")) return OemVendor.Dell;
        if (m.Contains("lenovo")) return OemVendor.Lenovo;
        if (m.Contains("hp") || m.Contains("hewlett")) return OemVendor.HP;
        if (m.Contains("asus")) return OemVendor.Asus;
        if (m.Contains("acer")) return OemVendor.Acer;
        if (m.Contains("samsung")) return OemVendor.Samsung;
        if (m.Contains("positivo")) return OemVendor.Positivo;
        if (m.Contains("msi") || m.Contains("micro-star")) return OemVendor.Msi;
        if (m.Contains("gigabyte")) return OemVendor.Gigabyte;
        if (m.Contains("apple")) return OemVendor.Apple;
        return OemVendor.Unknown;
    }

    public static bool HasNativeOemTool(OemVendor vendor) =>
        vendor is OemVendor.Dell or OemVendor.Lenovo or OemVendor.HP;

    public static string GetToolDescription(OemVendor vendor) => vendor switch
    {
        OemVendor.Dell => "Dell Command | Update (winget: Dell.CommandUpdate)",
        OemVendor.Lenovo => "Lenovo System Update (winget: Lenovo.SystemUpdate)",
        OemVendor.HP => "HP Support Assistant (winget: HP.SupportAssistant)",
        _ => "Sem utilitário automático — use o suporte oficial e o Windows Update"
    };

    public static string GetSupportUrl(OemVendor vendor, string manufacturer, string model, string serial)
    {
        string s = Uri.EscapeDataString(serial ?? "");
        string q = Uri.EscapeDataString($"{manufacturer} {model} drivers");
        bool hasSerial = !string.IsNullOrWhiteSpace(serial) && !serial.Contains("dispon") && !serial.Equals("Desconhecido", StringComparison.OrdinalIgnoreCase);

        return vendor switch
        {
            OemVendor.Dell => hasSerial
                ? $"https://www.dell.com/support/home/pt-br/product-support/servicetag/{s}/drivers"
                : "https://www.dell.com/support/home/pt-br",
            OemVendor.Lenovo => hasSerial
                ? $"https://pcsupport.lenovo.com/br/pt/search?query={s}"
                : "https://pcsupport.lenovo.com/br/pt",
            OemVendor.HP => "https://support.hp.com/br-pt/drivers",
            OemVendor.Asus => "https://www.asus.com/br/support/download-center/",
            OemVendor.Acer => "https://www.acer.com/br-pt/support/drivers-and-manuals",
            OemVendor.Samsung => "https://www.samsung.com/br/support/downloads/",
            OemVendor.Positivo => "https://www.meupositivo.com.br/suporte/",
            OemVendor.Msi => "https://www.msi.com/support/download/",
            OemVendor.Gigabyte => "https://www.gigabyte.com/Support",
            _ => $"https://www.google.com/search?q={q}"
        };
    }

    /// <summary>Executa a ação principal do fabricante (Dell/Lenovo/HP).</summary>
    public static async Task<OemActionResult> RunOemToolAsync(OemVendor vendor, bool applyUpdates)
    {
        if (!OperatingSystem.IsWindows())
            return Fail("Disponível apenas no Windows.");

        switch (vendor)
        {
            case OemVendor.Dell:
            {
                string? dcu = File.Exists(DellCliPath) ? DellCliPath : File.Exists(DellCliPathX86) ? DellCliPathX86 : null;
                if (dcu == null)
                    return await WingetInstallAsync("Dell.CommandUpdate", "Dell Command | Update");

                return StartElevated(dcu, applyUpdates ? "/applyUpdates" : "/scan",
                    applyUpdates ? "Dell Command Update aplicando atualizações..." : "Dell Command Update escaneando...");
            }
            case OemVendor.Lenovo:
            {
                string[] candidates =
                {
                    @"C:\Program Files (x86)\Lenovo\System Update\tvsu.exe",
                    @"C:\Program Files\Lenovo\System Update\tvsu.exe"
                };
                string? tvsu = candidates.FirstOrDefault(File.Exists);
                if (tvsu == null)
                    return await WingetInstallAsync("Lenovo.SystemUpdate", "Lenovo System Update");

                return StartElevated(tvsu, "", "Lenovo System Update aberto.");
            }
            case OemVendor.HP:
            {
                string[] candidates =
                {
                    @"C:\Program Files (x86)\HP\HP Support Framework\HPSF.exe",
                    @"C:\Program Files\HP\HP Support Framework\HPSF.exe"
                };
                string? hp = candidates.FirstOrDefault(File.Exists);
                if (hp == null)
                    return await WingetInstallAsync("HP.SupportAssistant", "HP Support Assistant");

                return StartElevated(hp, "", "HP Support Assistant aberto.");
            }
            default:
                return Fail("Este fabricante não possui utilitário automático. Use o suporte oficial.");
        }
    }

    /// <summary>Dispara busca de drivers via Windows Update (UsoClient) e re-escaneia dispositivos (pnputil).</summary>
    public static OemActionResult RunWindowsUpdateDriverSearch()
    {
        if (!OperatingSystem.IsWindows())
            return Fail("Disponível apenas no Windows.");

        // pnputil /scan-devices reinstala drivers de dispositivos pendentes; UsoClient abre a busca do Windows Update.
        var r1 = StartElevated("pnputil.exe", "/scan-devices", "");
        var r2 = StartElevated("UsoClient.exe", "StartInteractiveScan", "");

        if (!r1.Success && !r2.Success)
            return Fail(r1.Message);

        try { Process.Start(new ProcessStartInfo("ms-settings:windowsupdate") { UseShellExecute = true }); } catch { }
        return Ok("Busca de drivers iniciada (pnputil /scan-devices + Windows Update).");
    }

    public static async Task<bool> OpenSupportPageAsync(string url)
    {
        try { return await Launcher.Default.OpenAsync(new Uri(url)); }
        catch { return false; }
    }

    private static async Task<OemActionResult> WingetInstallAsync(string id, string friendlyName)
    {
        // winget install precisa de elevação para ferramentas OEM (instalação machine-wide)
        string args = $"install --id {id} -e --accept-package-agreements --accept-source-agreements";
        var result = StartElevated("winget.exe", args, $"Instalando {friendlyName} via winget (confirme o UAC)...");
        await Task.CompletedTask;
        return result;
    }

    private static OemActionResult StartElevated(string fileName, string arguments, string okMessage)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = true,
                Verb = "runas"
            });
            return Ok(okMessage);
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            return Fail("Elevação UAC cancelada pelo usuário.");
        }
        catch (Exception ex)
        {
            return Fail($"Falha ao executar {Path.GetFileName(fileName)}: {ex.Message}");
        }
    }

    private static OemActionResult Ok(string msg) => new() { Success = true, Message = msg };
    private static OemActionResult Fail(string msg) => new() { Success = false, Message = msg };
}
