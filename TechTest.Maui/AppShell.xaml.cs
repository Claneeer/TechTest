using TechTest.Maui.Views;

namespace TechTest.Maui;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

        Routing.RegisterRoute(nameof(SpecsPage), typeof(SpecsPage));
        Routing.RegisterRoute(nameof(CameraTestPage), typeof(CameraTestPage));
        Routing.RegisterRoute(nameof(DeadPixelTestPage), typeof(DeadPixelTestPage));
        Routing.RegisterRoute(nameof(TouchpadTestPage), typeof(TouchpadTestPage));
    }
}
