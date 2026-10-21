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
        services.AddSingleton<IAudioPlaybackService, AudioPlaybackService>();
        services.AddSingleton<IAudioCaptureService, AudioCaptureService>();
        services.AddSingleton<IBatteryService, BatteryService>();
#endif

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