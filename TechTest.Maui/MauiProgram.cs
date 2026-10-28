using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
using SkiaSharp.Views.Maui.Controls.Hosting;
using TechTest.Maui.Services;

namespace TechTest.Maui;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        // Escudo global para evitar que falhas de hardware/periféricos fechem o app
        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            System.Diagnostics.Debug.WriteLine($"[Erro Crítico]: {e.ExceptionObject}");
        };

        TaskScheduler.UnobservedTaskException += (s, e) =>
        {
            e.SetObserved(); // Impede que falhas assíncronas (como CameraView) derrubem o processo
        };

        var builder = MauiApp.CreateBuilder();

        builder
            .UseMauiApp<App>()
            .UseSkiaSharp()
            .UseMauiCommunityToolkit()
            .UseMauiCommunityToolkitCamera();

        RegisterServices(builder.Services);

        return builder.Build();
    }

    private static void RegisterServices(IServiceCollection services)
    {
#if WINDOWS || MACCATALYST
        services.AddSingleton<IHardwareInfoService, HardwareInfoService>();
        // Gerador de tons (aba Áudio): instância própria, nunca compartilhada com a gravação do microfone.
        services.AddTransient<IAudioPlaybackService, AudioPlaybackService>();
        services.AddSingleton<IAudioCaptureService, AudioCaptureService>();
        services.AddSingleton<IBatteryService, BatteryService>();
#endif
#if WINDOWS
        // Player de arquivo da gravação: dispositivo de saída ISOLADO do gerador de tons.
        services.AddTransient<IRecordingPlayerService, RecordingPlayerService>();
#else
        services.AddTransient<IRecordingPlayerService, NullRecordingPlayerService>();
#endif

        services.AddSingleton<IClipboardService, ClipboardService>();
        services.AddSingleton<IWindowResolutionService, WindowResolutionService>();
        services.AddSingleton<ICameraAccessService, CameraAccessService>();
        services.AddSingleton<IToolsPathProvider, ToolsPathProvider>();
        services.AddSingleton<INetworkConfigService, NetworkConfigService>();
        services.AddSingleton<IFurMarkService, FurMarkService>();

        services.AddTransient<ViewModels.MainViewModel>();
        services.AddTransient<ViewModels.SpecsViewModel>();
        services.AddTransient<ViewModels.MicrophoneTestViewModel>();
        services.AddTransient<ViewModels.AudioTestViewModel>();

        services.AddTransient<Views.MainPage>();
        services.AddTransient<Views.MicrophoneTestPage>();
        services.AddTransient<Views.AudioTestPage>();
        services.AddTransient<Views.KeyboardTestPage>();
        services.AddTransient<Views.BatteryTestPage>();
        services.AddTransient<Views.SpecsPage>();
        services.AddTransient<Views.DriverUpdatePage>();
        services.AddTransient<Views.CameraTestPage>();
        services.AddTransient<Views.DeadPixelTestPage>();
        services.AddTransient<Views.TouchpadTestPage>();
    }
}
