using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;

namespace TechTest.Maui.Services;

public sealed record ResolutionOption(
    int Width,
    int Height,
    string AspectRatio,
    string StandardName)
{
    public string ShortLabel => $"{Width}x{Height} ({AspectRatio})";
    public string DisplayLabel => $"{Width} x {Height} ({AspectRatio}) — {StandardName}";
    public override string ToString() => DisplayLabel;
}

public sealed record WindowResizeResult(
    bool Success,
    int RequestedWidth,
    int RequestedHeight,
    int AppliedWidth,
    int AppliedHeight,
    string AspectRatio,
    bool WasClampedToMonitor,
    string Message);

public interface IWindowResolutionService
{
    IReadOnlyList<ResolutionOption> Presets { get; }
    WindowResizeResult ApplyPreset(ResolutionOption preset);
    WindowResizeResult ApplyResolution(int width, int height, string? aspectRatio = null);
}

public sealed class WindowResolutionService : IWindowResolutionService
{
    public static readonly IReadOnlyList<ResolutionOption> DefaultPresets = new List<ResolutionOption>
    {
        // 16:9
        new(1366, 768,  "16:9",  "HD / WXGA"),
        new(1920, 1080, "16:9",  "Full HD"),
        new(2560, 1440, "16:9",  "QHD / 2K"),
        new(3840, 2160, "16:9",  "UHD / 4K"),
        // 16:10
        new(1920, 1200, "16:10", "WUXGA")
    };

    public IReadOnlyList<ResolutionOption> Presets => DefaultPresets;

    public WindowResizeResult ApplyPreset(ResolutionOption preset)
    {
        if (preset == null)
            throw new ArgumentNullException(nameof(preset));

        return ApplyResolution(preset.Width, preset.Height, preset.AspectRatio);
    }

    public WindowResizeResult ApplyResolution(int width, int height, string? aspectRatio = null)
    {
        string ratioLabel = string.IsNullOrWhiteSpace(aspectRatio)
            ? InferAspectRatio(width, height)
            : aspectRatio!;

        if (width <= 0 || height <= 0)
        {
            return new WindowResizeResult(
                false, width, height, 0, 0, ratioLabel, false,
                "Dimensões de resolução inválidas.");
        }

#if WINDOWS
        try
        {
            WindowResizeResult? result = null;

            void ExecuteResize()
            {
                var nativeWindow = Application.Current?.Windows.FirstOrDefault()?.Handler?.PlatformView as Microsoft.UI.Xaml.Window;
                if (nativeWindow == null)
                {
                    result = new WindowResizeResult(
                        false, width, height, 0, 0, ratioLabel, false,
                        "Janela nativa WinUI 3 não encontrada.");
                    return;
                }

                var windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(nativeWindow);
                var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(windowHandle);
                var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);

                if (appWindow == null)
                {
                    result = new WindowResizeResult(
                        false, width, height, 0, 0, ratioLabel, false,
                        "AppWindow não pôde ser obtida a partir do WindowId.");
                    return;
                }

                // Se a janela estiver maximizada ou em tela cheia, restaura para estado normal (Overlapped) antes de redimensionar
                if (appWindow.Presenter is Microsoft.UI.Windowing.OverlappedPresenter presenter &&
                    presenter.State != Microsoft.UI.Windowing.OverlappedPresenterState.Restored)
                {
                    presenter.Restore();
                }

                var displayArea = Microsoft.UI.Windowing.DisplayArea.GetFromWindowId(
                    windowId,
                    Microsoft.UI.Windowing.DisplayAreaFallback.Primary);
                var workArea = displayArea.WorkArea;

                // Calcula a diferença entre o tamanho total da janela (bordas/barra de título) e a área cliente
                int frameBorderWidth = Math.Max(0, appWindow.Size.Width - appWindow.ClientSize.Width);
                int frameBorderHeight = Math.Max(0, appWindow.Size.Height - appWindow.ClientSize.Height);
                if (frameBorderWidth == 0) frameBorderWidth = 16;
                if (frameBorderHeight == 0) frameBorderHeight = 39;

                int maxClientWidth = Math.Max(640, workArea.Width - frameBorderWidth);
                int maxClientHeight = Math.Max(480, workArea.Height - frameBorderHeight);

                int targetWidth = width;
                int targetHeight = height;
                bool clamped = false;

                // Trata os limites do monitor atual preservando o aspect ratio selecionado (16:9 ou 16:10)
                if (targetWidth > maxClientWidth || targetHeight > maxClientHeight)
                {
                    clamped = true;
                    double targetAspect = (double)width / height;

                    targetWidth = maxClientWidth;
                    targetHeight = (int)Math.Round(targetWidth / targetAspect);

                    if (targetHeight > maxClientHeight)
                    {
                        targetHeight = maxClientHeight;
                        targetWidth = (int)Math.Round(targetHeight * targetAspect);
                    }
                }

                appWindow.ResizeClient(new Windows.Graphics.SizeInt32(targetWidth, targetHeight));

                // Centraliza a janela na área útil (WorkArea) do monitor atual sem ultrapassar os limites do display
                var finalWindowSize = appWindow.Size;
                int posX = workArea.X + Math.Max(0, (workArea.Width - finalWindowSize.Width) / 2);
                int posY = workArea.Y + Math.Max(0, (workArea.Height - finalWindowSize.Height) / 2);
                appWindow.Move(new Windows.Graphics.PointInt32(posX, posY));

                string msg = clamped
                    ? $"Resolução {width}x{height} ({ratioLabel}) ajustada para {targetWidth}x{targetHeight} ({ratioLabel}) conforme limite do monitor ({workArea.Width}x{workArea.Height})."
                    : $"Resolução alterada para {targetWidth}x{targetHeight} ({ratioLabel}).";

                result = new WindowResizeResult(
                    true, width, height, targetWidth, targetHeight, ratioLabel, clamped, msg);
            }

            if (MainThread.IsMainThread)
            {
                ExecuteResize();
            }
            else
            {
                MainThread.BeginInvokeOnMainThread(ExecuteResize);
            }

            return result ?? new WindowResizeResult(
                true, width, height, width, height, ratioLabel, false,
                $"Solicitação de redimensionamento enviada ({width}x{height} {ratioLabel}).");
        }
        catch (Exception ex)
        {
            return new WindowResizeResult(
                false, width, height, 0, 0, ratioLabel, false,
                $"Erro ao alterar resolução da janela: {ex}");
        }
#else
        return new WindowResizeResult(
            false, width, height, 0, 0, ratioLabel, false,
            "O redimensionamento nativo de janela é suportado apenas no Windows (WinUI 3).");
#endif
    }

    private static string InferAspectRatio(int width, int height)
    {
        if (height <= 0) return "N/D";
        double ratio = (double)width / height;
        if (Math.Abs(ratio - (16.0 / 9.0)) < 0.03) return "16:9";
        if (Math.Abs(ratio - (16.0 / 10.0)) < 0.03) return "16:10";
        return $"{width}:{height}";
    }
}

