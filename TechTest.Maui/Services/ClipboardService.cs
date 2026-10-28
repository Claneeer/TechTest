using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.ApplicationModel.DataTransfer;
using Microsoft.Maui.Controls;

namespace TechTest.Maui.Services;

public static class ClipboardService
{
#if WINDOWS
    private const uint CF_UNICODETEXT = 13;
    private const uint GMEM_MOVEABLE = 0x0002;

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool OpenClipboard(IntPtr hWndNewOwner);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EmptyClipboard();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetClipboardData(uint uFormat, IntPtr hMem);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseClipboard();

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalAlloc(uint uFlags, UIntPtr dwBytes);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalLock(IntPtr hMem);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalUnlock(IntPtr hMem);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalFree(IntPtr hMem);

    [DllImport("ole32.dll")]
    private static extern int OleInitialize(IntPtr pvReserved);
#endif

    public static async Task CopyTextAsync(string text)
    {
        if (string.IsNullOrEmpty(text)) return;

#if WINDOWS
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        var nativeWindow = Application.Current?.Windows.FirstOrDefault()?.Handler?.PlatformView as Microsoft.UI.Xaml.Window;
        var dispatcherQueue = nativeWindow?.DispatcherQueue ?? Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();

        void ExecuteClipboardWrite()
        {
            try
            {
                IntPtr hwnd = IntPtr.Zero;
                if (nativeWindow != null)
                {
                    try { hwnd = WinRT.Interop.WindowNative.GetWindowHandle(nativeWindow); } catch { }
                }

                // 1. Método primário: Win32 Nativo (OpenClipboard / SetClipboardData) - imune a COMException 0x800401F0
                if (TrySetWin32ClipboardText(hwnd, text))
                {
                    tcs.TrySetResult(true);
                    return;
                }

                // 2. Fallback: Inicializa OLE STA explicitamente e usa DataPackage do WinRT
                OleInitialize(IntPtr.Zero);
                var package = new Windows.ApplicationModel.DataTransfer.DataPackage();
                package.SetText(text);
                Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(package);
                Windows.ApplicationModel.DataTransfer.Clipboard.Flush();
                tcs.TrySetResult(true);
            }
            catch (Exception ex)
            {
                // 3. Fallback de última instância em Thread STA dedicada
                try
                {
                    SetClipboardOnDedicatedStaThread(text);
                    tcs.TrySetResult(true);
                }
                catch (Exception innerEx)
                {
                    Debug.WriteLine($"Falha Clipboard: {ex} | Fallback STA: {innerEx}");
                    tcs.TrySetException(ex);
                }
            }
        }

        if (dispatcherQueue != null)
        {
            if (dispatcherQueue.HasThreadAccess)
            {
                ExecuteClipboardWrite();
            }
            else if (!dispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, ExecuteClipboardWrite))
            {
                await MainThread.InvokeOnMainThreadAsync(ExecuteClipboardWrite);
            }
        }
        else
        {
            await MainThread.InvokeOnMainThreadAsync(ExecuteClipboardWrite);
        }

        await tcs.Task;
#else
        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            await Clipboard.Default.SetTextAsync(text);
        });
#endif
    }

#if WINDOWS
    private static bool TrySetWin32ClipboardText(IntPtr hwnd, string text)
    {
        // Tenta abrir o clipboard com pequenas retentativas caso outro processo do Windows esteja inspecionando a área de transferência
        bool opened = false;
        for (int attempt = 0; attempt < 5; attempt++)
        {
            if (OpenClipboard(hwnd))
            {
                opened = true;
                break;
            }
            Thread.Sleep(20);
        }

        if (!opened && hwnd != IntPtr.Zero && OpenClipboard(IntPtr.Zero))
        {
            opened = true;
        }

        if (!opened) return false;

        IntPtr hGlobal = IntPtr.Zero;
        try
        {
            EmptyClipboard();

            int charCount = text.Length;
            int byteCount = (charCount + 1) * sizeof(char);
            hGlobal = GlobalAlloc(GMEM_MOVEABLE, (UIntPtr)byteCount);
            if (hGlobal == IntPtr.Zero) return false;

            IntPtr targetPtr = GlobalLock(hGlobal);
            if (targetPtr == IntPtr.Zero)
            {
                GlobalFree(hGlobal);
                hGlobal = IntPtr.Zero;
                return false;
            }

            try
            {
                Marshal.Copy(text.ToCharArray(), 0, targetPtr, charCount);
                Marshal.WriteInt16(targetPtr, charCount * sizeof(char), 0); // Terminador nulo UTF-16
            }
            finally
            {
                GlobalUnlock(hGlobal);
            }

            IntPtr result = SetClipboardData(CF_UNICODETEXT, hGlobal);
            if (result != IntPtr.Zero)
            {
                // Após SetClipboardData ter sucesso, o sistema operacional passa a ser dono de hGlobal
                hGlobal = IntPtr.Zero;
                return true;
            }

            return false;
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

    private static void SetClipboardOnDedicatedStaThread(string text)
    {
        Exception? threadEx = null;
        var thread = new Thread(() =>
        {
            try
            {
                OleInitialize(IntPtr.Zero);
                if (!TrySetWin32ClipboardText(IntPtr.Zero, text))
                {
                    var package = new Windows.ApplicationModel.DataTransfer.DataPackage();
                    package.SetText(text);
                    Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(package);
                    Windows.ApplicationModel.DataTransfer.Clipboard.Flush();
                }
            }
            catch (Exception ex)
            {
                threadEx = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
        thread.Join(3000);

        if (threadEx != null)
            throw threadEx;
    }
#endif
}
