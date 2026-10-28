using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.ApplicationModel.DataTransfer;

namespace TechTest.Maui.Services;

public interface IClipboardService
{
    /// <summary>
    /// Copia o texto para a área de transferência garantindo execução em STA (UI Thread)
    /// e aplicando fallbacks nativos do Windows (WinRT DataPackage + Flush e Win32 User32 Clipboard)
    /// para suportar aplicações desempacotadas (WindowsPackageType=None).
    /// </summary>
    Task SetTextAsync(string? text);
}

public sealed class ClipboardService : IClipboardService
{
    private const int MaxRetryAttempts = 4;
    private const int BaseRetryDelayMs = 120;

    public async Task SetTextAsync(string? text)
    {
        string safeText = text ?? string.Empty;
        Exception? lastException = null;

        for (int attempt = 1; attempt <= MaxRetryAttempts; attempt++)
        {
            try
            {
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
#if WINDOWS
                    SetTextWindowsNative(safeText);
                    await Task.CompletedTask;
#else
                    await Clipboard.Default.SetTextAsync(safeText);
#endif
                });

                return;
            }
            catch (Exception ex)
            {
                lastException = ex;
                if (attempt < MaxRetryAttempts)
                {
                    await Task.Delay(BaseRetryDelayMs * attempt).ConfigureAwait(false);
                }
            }
        }

        throw new InvalidOperationException(
            $"Falha ao copiar texto para a área de transferência após {MaxRetryAttempts} tentativas. " +
            $"Detalhes: {lastException?.ToString() ?? "Erro desconhecido"}",
            lastException);
    }

#if WINDOWS
    private static void SetTextWindowsNative(string text)
    {
        Exception? winRtException = null;

        // 1) Tentativa primária via WinRT DataPackage + Clipboard.Flush() (na UI Thread / STA)
        try
        {
            var dataPackage = new Windows.ApplicationModel.DataTransfer.DataPackage
            {
                RequestedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Copy
            };
            dataPackage.SetText(text);
            Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dataPackage);
            Windows.ApplicationModel.DataTransfer.Clipboard.Flush();
            return;
        }
        catch (Exception ex)
        {
            winRtException = ex;
        }

        // 2) Fallback secundário via Win32 nativo (User32 OpenClipboard / SetClipboardData CF_UNICODETEXT)
        // Essencial quando WindowsPackageType=None não possui CoreWindow ativo ou o WinRT Clipboard falha com COMException vazia.
        try
        {
            SetTextWin32(text);
            return;
        }
        catch (Exception win32Ex)
        {
            // 3) Última tentativa via abstração MAUI
            try
            {
                Clipboard.Default.SetTextAsync(text).GetAwaiter().GetResult();
                return;
            }
            catch (Exception mauiEx)
            {
                throw new AggregateException(
                    "Falha em todos os provedores de área de transferência do Windows (WinRT DataPackage, Win32 User32 e MAUI Clipboard).",
                    new[] { winRtException!, win32Ex, mauiEx });
            }
        }
    }

    private const uint CF_UNICODETEXT = 13;
    private const uint GMEM_MOVEABLE = 0x0002;

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool OpenClipboard(IntPtr hWndNewOwner);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseClipboard();

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EmptyClipboard();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetClipboardData(uint uFormat, IntPtr hMem);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalAlloc(uint uFlags, UIntPtr dwBytes);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalLock(IntPtr hMem);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalUnlock(IntPtr hMem);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalFree(IntPtr hMem);

    private static void SetTextWin32(string text)
    {
        IntPtr hwnd = IntPtr.Zero;
        if (Application.Current?.Windows is { Count: > 0 } windows &&
            windows[0].Handler?.PlatformView is Microsoft.UI.Xaml.Window nativeWindow)
        {
            hwnd = WinRT.Interop.WindowNative.GetWindowHandle(nativeWindow);
        }

        if (!OpenClipboard(hwnd))
        {
            int err = Marshal.GetLastWin32Error();
            throw new System.ComponentModel.Win32Exception(err, $"OpenClipboard falhou (Win32 Error {err}).");
        }

        IntPtr hGlobal = IntPtr.Zero;
        try
        {
            EmptyClipboard();

            int byteCount = (text.Length + 1) * sizeof(char);
            hGlobal = GlobalAlloc(GMEM_MOVEABLE, (UIntPtr)byteCount);
            if (hGlobal == IntPtr.Zero)
            {
                int err = Marshal.GetLastWin32Error();
                throw new System.ComponentModel.Win32Exception(err, $"GlobalAlloc falhou (Win32 Error {err}).");
            }

            IntPtr targetPtr = GlobalLock(hGlobal);
            if (targetPtr == IntPtr.Zero)
            {
                int err = Marshal.GetLastWin32Error();
                throw new System.ComponentModel.Win32Exception(err, $"GlobalLock falhou (Win32 Error {err}).");
            }

            try
            {
                Marshal.Copy(text.ToCharArray(), 0, targetPtr, text.Length);
                Marshal.WriteInt16(targetPtr, text.Length * sizeof(char), 0); // Null terminator UTF-16
            }
            finally
            {
                GlobalUnlock(hGlobal);
            }

            if (SetClipboardData(CF_UNICODETEXT, hGlobal) == IntPtr.Zero)
            {
                int err = Marshal.GetLastWin32Error();
                throw new System.ComponentModel.Win32Exception(err, $"SetClipboardData falhou (Win32 Error {err}).");
            }

            // A partir de SetClipboardData bem-sucedido, o sistema operacional é dono do bloco hGlobal
            hGlobal = IntPtr.Zero;
        }
        finally
        {
            if (hGlobal != IntPtr.Zero)
            {
                GlobalFree(hGlobal);
            }
            CloseClipboard();
        }
    }
#endif
}

