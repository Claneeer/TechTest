using System;
using System.Diagnostics;
using System.Linq;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;

namespace TechTest.Maui.Services;

public static class WindowSizeService
{
#if WINDOWS
    public static void SetResolution(int width, int height)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            try
            {
                var nativeWindow = App.Current?.Windows.FirstOrDefault()?.Handler?.PlatformView as Microsoft.UI.Xaml.Window;
                if (nativeWindow == null) return;

                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(nativeWindow);
                var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
                var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);

                if (appWindow != null)
                {
                    if (appWindow.Presenter is Microsoft.UI.Windowing.OverlappedPresenter presenter &&
                        presenter.State == Microsoft.UI.Windowing.OverlappedPresenterState.Maximized)
                    {
                        presenter.Restore();
                    }

                    appWindow.ResizeClient(new Windows.Graphics.SizeInt32(width, height));

                    // Centralização no monitor ativo
                    var displayArea = Microsoft.UI.Windowing.DisplayArea.GetFromWindowId(windowId, Microsoft.UI.Windowing.DisplayAreaFallback.Primary);
                    if (displayArea != null)
                    {
                        var centeredX = displayArea.WorkArea.X + Math.Max(0, (displayArea.WorkArea.Width - width) / 2);
                        var centeredY = displayArea.WorkArea.Y + Math.Max(0, (displayArea.WorkArea.Height - height) / 2);
                        appWindow.Move(new Windows.Graphics.PointInt32(centeredX, centeredY));
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Falha ao redimensionar janela: {ex}");
            }
        });
    }
#else
    public static void SetResolution(int width, int height)
    {
        var window = App.Current?.Windows.FirstOrDefault();
        if (window != null)
        {
            window.Width = width;
            window.Height = height;
        }
    }
#endif

    public static bool TrySetResolutionFromParameter(string? parameter)
    {
        if (string.IsNullOrWhiteSpace(parameter)) return false;

        var parts = parameter.Split(new[] { 'x', 'X', ',' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 2 &&
            int.TryParse(parts[0], out int width) &&
            int.TryParse(parts[1], out int height) &&
            width > 0 && height > 0)
        {
            SetResolution(width, height);
            return true;
        }

        return false;
    }
}

