using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
using SkiaSharp.Views.Maui.Controls.Hosting;
using TechTest.Maui.Services;

namespace TechTest.Maui;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();

        builder
            .UseMauiApp<App>()
            .UseSkiaSharp()
            .UseMauiCommunityToolkit()
            .UseMauiCommunityToolkitCamera()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        RegisterServices(builder.Services);

#if DEBUG
        // System debug logging
#endif

        return builder.Build();
    }

    private static void RegisterServices(IServiceCollection services)
    {
        services.AddSingleton<IHardwareInfoService, HardwareInfoService>();
        services.AddSingleton<IAudioPlaybackService, AudioPlaybackService>();
        services.AddSingleton<IAudioCaptureService, AudioCaptureService>();
        services.AddSingleton<IBatteryService, BatteryService>();

        services.AddTransient<Views.MainPage>();
        services.AddTransient<Views.MicrophoneTestPage>();
        services.AddTransient<Views.AudioTestPage>();
        services.AddTransient<Views.KeyboardTestPage>();
        services.AddTransient<Views.BatteryTestPage>();
        services.AddTransient<Views.SpecsPage>();
        services.AddTransient<Views.CameraTestPage>();
        services.AddTransient<Views.DeadPixelTestPage>();
        services.AddTransient<Views.TouchpadTestPage>();
    }
}
