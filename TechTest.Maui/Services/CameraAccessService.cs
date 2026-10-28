using System;
using System.Threading.Tasks;
using Microsoft.Maui.ApplicationModel;

namespace TechTest.Maui.Services;

public enum CameraAccessState
{
    Granted,
    Denied,          // negada pelo usuário / política de privacidade do Windows
    NoDevice,        // sem webcam física/reconhecida
    Error
}

public sealed record CameraAccessResult(CameraAccessState State, string Message, string? DeviceName = null)
{
    public bool IsGranted => State == CameraAccessState.Granted;
}

public interface ICameraAccessService
{
    /// <summary>Valida hardware + permissões. Deve ser chamado da thread principal (pode exibir prompt do SO).</summary>
    Task<CameraAccessResult> EnsureAccessAsync();

    /// <summary>Abre a página de privacidade de câmera do sistema (quando suportado).</summary>
    Task OpenPrivacySettingsAsync();
}

public sealed class CameraAccessService : ICameraAccessService
{
    public async Task<CameraAccessResult> EnsureAccessAsync()
    {
        try
        {
#if WINDOWS
            // 1) Hardware: enumera dispositivos de captura de vídeo via WinRT.
            var devices = await global::Windows.Devices.Enumeration.DeviceInformation.FindAllAsync(
                global::Windows.Devices.Enumeration.DeviceClass.VideoCapture);

            if (devices == null || devices.Count == 0)
                return new CameraAccessResult(CameraAccessState.NoDevice,
                    "Nenhuma webcam foi detectada ou reconhecida pelo Windows.");

            string name = devices[0].Name;

            // 2) Privacidade do Windows (Configurações > Privacidade > Câmera). Em app não empacotado
            //    o consentimento fica em ConsentStore\webcam\NonPackaged ou na chave global.
            if (IsWebcamBlockedByWindowsPrivacy())
                return new CameraAccessResult(CameraAccessState.Denied,
                    "O acesso à câmera está bloqueado nas configurações de privacidade do Windows.", name);

            return new CameraAccessResult(CameraAccessState.Granted, "Acesso concedido.", name);
#else
            var status = await Permissions.CheckStatusAsync<Permissions.Camera>();
            if (status != PermissionStatus.Granted)
                status = await Permissions.RequestAsync<Permissions.Camera>();

            return status == PermissionStatus.Granted
                ? new CameraAccessResult(CameraAccessState.Granted, "Acesso concedido.")
                : new CameraAccessResult(CameraAccessState.Denied, "Permissão de câmera negada pelo usuário.");
#endif
        }
        catch (Exception ex)
        {
            return new CameraAccessResult(CameraAccessState.Error, $"Falha ao validar a câmera: {ex.Message}");
        }
    }

    public async Task OpenPrivacySettingsAsync()
    {
        try
        {
#if WINDOWS
            await Launcher.Default.OpenAsync("ms-settings:privacy-webcam");
#else
            AppInfo.Current.ShowSettingsUI();
            await Task.CompletedTask;
#endif
        }
        catch
        {
            // Sem suporte: apenas ignora.
        }
    }

#if WINDOWS
    private static bool IsWebcamBlockedByWindowsPrivacy()
    {
        const string basePath = @"Software\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\webcam";
        try
        {
            foreach (var (hive, sub) in new[]
            {
                (Microsoft.Win32.Registry.LocalMachine, basePath),             // chave global (todos os usuários)
                (Microsoft.Win32.Registry.CurrentUser, basePath),              // preferência do usuário
                (Microsoft.Win32.Registry.CurrentUser, basePath + @"\NonPackaged") // apps desktop (Win32/unpackaged)
            })
            {
                using var key = hive.OpenSubKey(sub);
                if (key?.GetValue("Value") is string v && v.Equals("Deny", StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }
        catch
        {
            // Sem acesso ao registro: assume que não está bloqueado e deixa o driver responder.
        }
        return false;
    }
#endif
}
