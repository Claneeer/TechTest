#if WINDOWS
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Microsoft.Maui.Controls;
using SkiaSharp;

namespace TechTest.Maui.Views
{
    /// <summary>
    /// Leitura das coordenadas físicas ABSOLUTAS do Precision Touchpad via Win32 Raw Input
    /// (UsagePage 0x0D / Usage 0x05) + parsing HID (hid.dll), independente do cursor do Windows.
    /// Também implementa o "Modo Cursor" legado (cursor centralizado e contido no canvas).
    /// </summary>
    public partial class TouchpadTestPage
    {
        #region Win32

        private const int WM_INPUT = 0x00FF;
        private const int GWLP_WNDPROC = -4;
        private const uint RID_INPUT = 0x10000003;
        private const uint RIDI_PREPARSEDDATA = 0x20000005;
        private const uint RIDEV_REMOVE = 0x00000001;
        private const uint RIDEV_INPUTSINK = 0x00000100;
        private const uint RIM_TYPEHID = 2;
        private const int HIDP_STATUS_SUCCESS = 0x00110000;

        [StructLayout(LayoutKind.Sequential)]
        private struct RAWINPUTDEVICE
        {
            public ushort UsagePage;
            public ushort Usage;
            public uint Flags;
            public IntPtr Target;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT { public int Left, Top, Right, Bottom; }

        [StructLayout(LayoutKind.Sequential)]
        private struct HIDP_CAPS
        {
            public ushort Usage;
            public ushort UsagePage;
            public ushort InputReportByteLength;
            public ushort OutputReportByteLength;
            public ushort FeatureReportByteLength;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 17)]
            public ushort[] Reserved;
            public ushort NumberLinkCollectionNodes;
            public ushort NumberInputButtonCaps;
            public ushort NumberInputValueCaps;
            public ushort NumberInputDataIndices;
            public ushort NumberOutputButtonCaps;
            public ushort NumberOutputValueCaps;
            public ushort NumberOutputDataIndices;
            public ushort NumberFeatureButtonCaps;
            public ushort NumberFeatureValueCaps;
            public ushort NumberFeatureDataIndices;
        }

        private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterRawInputDevices(RAWINPUTDEVICE[] devices, uint count, uint size);

        [DllImport("user32.dll")]
        private static extern uint GetRawInputData(IntPtr hRawInput, uint command, IntPtr data, ref uint size, uint headerSize);

        [DllImport("user32.dll")]
        private static extern uint GetRawInputDeviceInfo(IntPtr hDevice, uint command, IntPtr data, ref uint size);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
        private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int index, IntPtr newLong);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
        private static extern IntPtr SetWindowLongPtrProc(IntPtr hWnd, int index, WndProcDelegate newProc);

        [DllImport("user32.dll")]
        private static extern IntPtr CallWindowProc(IntPtr prev, IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool ClipCursor(ref RECT rect);

        [DllImport("user32.dll", EntryPoint = "ClipCursor")]
        private static extern bool ClipCursorRelease(IntPtr rect);

        [DllImport("user32.dll")]
        private static extern bool SetCursorPos(int x, int y);

        [DllImport("user32.dll")]
        private static extern bool ClientToScreen(IntPtr hWnd, ref POINT point);

        [DllImport("hid.dll")]
        private static extern int HidP_GetCaps(IntPtr preparsed, out HIDP_CAPS caps);

        [DllImport("hid.dll")]
        private static extern int HidP_GetValueCaps(int reportType, IntPtr valueCaps, ref ushort length, IntPtr preparsed);

        [DllImport("hid.dll")]
        private static extern int HidP_GetUsageValue(int reportType, ushort usagePage, ushort link, ushort usage,
            out uint value, IntPtr preparsed, byte[] report, uint reportLength);

        [DllImport("hid.dll")]
        private static extern int HidP_GetUsages(int reportType, ushort usagePage, ushort link, ushort[] usageList,
            ref uint usageLength, IntPtr preparsed, byte[] report, uint reportLength);

        #endregion

        #region State

        private sealed class ContactAxes
        {
            public int XMin, XMax, YMin, YMax;
            public bool HasX, HasY;
        }

        private sealed class TouchpadDevice
        {
            public IntPtr Preparsed;
            public bool IsTouchpad;
            public readonly Dictionary<ushort, ContactAxes> Contacts = new();
        }

        private IntPtr _hwnd;
        private IntPtr _oldWndProc;
        private WndProcDelegate? _wndProc; // mantém o delegate vivo (evita GC)
        private bool _rawRegistered;
        private readonly Dictionary<IntPtr, TouchpadDevice> _devices = new();
        private readonly Dictionary<int, List<SKPoint>> _rawPaths = new();
        private bool _rawSeen;

        private IDispatcherTimer? _legacyTimer;
        private bool _legacyCentered;

        #endregion

        private IntPtr ResolveHwnd()
        {
            try
            {
                if (_hwnd != IntPtr.Zero) return _hwnd;
                if (this.Window?.Handler?.PlatformView is Microsoft.UI.Xaml.Window w)
                    _hwnd = WinRT.Interop.WindowNative.GetWindowHandle(w);
            }
            catch { }
            return _hwnd;
        }

        partial void ApplyInputMode()
        {
            try
            {
                StopRawInput();
                StopLegacyCursorMode();

                if (_precisionMode) StartRawInput();
                else StartLegacyCursorMode();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ApplyInputMode: {ex.Message}");
            }
        }

        partial void ReleaseInputMode()
        {
            try
            {
                StopRawInput();
                StopLegacyCursorMode();
            }
            catch { }
        }

        #region Precision mode (Raw Input)

        private void StartRawInput()
        {
            var hwnd = ResolveHwnd();
            if (hwnd == IntPtr.Zero)
            {
                LblModeStatus.Text = "Aguardando a janela nativa...";
                return;
            }

            _rawSeen = false;
            LblModeStatus.TextColor = Color.FromArgb("#EAB308");
            LblModeStatus.Text = "Aguardando dados do sensor físico... toque no touchpad.";

            if (_oldWndProc == IntPtr.Zero)
            {
                _wndProc = NewWndProc;
                _oldWndProc = SetWindowLongPtrProc(hwnd, GWLP_WNDPROC, _wndProc);
            }

            var rid = new[]
            {
                new RAWINPUTDEVICE { UsagePage = 0x0D, Usage = 0x05, Flags = RIDEV_INPUTSINK, Target = hwnd }
            };
            _rawRegistered = RegisterRawInputDevices(rid, 1, (uint)Marshal.SizeOf<RAWINPUTDEVICE>());
            if (!_rawRegistered)
            {
                LblModeStatus.TextColor = Color.FromArgb("#EF4444");
                LblModeStatus.Text = "Falha ao registrar Raw Input. Use o Modo Cursor (legado).";
            }
        }

        private void StopRawInput()
        {
            _rawPaths.Clear();

            if (_rawRegistered && _hwnd != IntPtr.Zero)
            {
                var rid = new[]
                {
                    new RAWINPUTDEVICE { UsagePage = 0x0D, Usage = 0x05, Flags = RIDEV_REMOVE, Target = IntPtr.Zero }
                };
                RegisterRawInputDevices(rid, 1, (uint)Marshal.SizeOf<RAWINPUTDEVICE>());
                _rawRegistered = false;
            }

            if (_oldWndProc != IntPtr.Zero && _hwnd != IntPtr.Zero)
            {
                SetWindowLongPtr(_hwnd, GWLP_WNDPROC, _oldWndProc);
                _oldWndProc = IntPtr.Zero;
                _wndProc = null;
            }

            foreach (var d in _devices.Values)
                if (d.Preparsed != IntPtr.Zero) Marshal.FreeHGlobal(d.Preparsed);
            _devices.Clear();
        }

        private IntPtr NewWndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            if (msg == WM_INPUT)
            {
                try { HandleRawInput(lParam); }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"WM_INPUT: {ex.Message}"); }
            }
            return CallWindowProc(_oldWndProc, hWnd, msg, wParam, lParam);
        }

        private TouchpadDevice? GetDevice(IntPtr hDevice)
        {
            if (_devices.TryGetValue(hDevice, out var cached)) return cached;

            var dev = new TouchpadDevice();
            _devices[hDevice] = dev;

            uint size = 0;
            GetRawInputDeviceInfo(hDevice, RIDI_PREPARSEDDATA, IntPtr.Zero, ref size);
            if (size == 0) return dev;

            dev.Preparsed = Marshal.AllocHGlobal((int)size);
            if (GetRawInputDeviceInfo(hDevice, RIDI_PREPARSEDDATA, dev.Preparsed, ref size) == uint.MaxValue)
                return dev;

            if (HidP_GetCaps(dev.Preparsed, out var caps) != HIDP_STATUS_SUCCESS) return dev;
            dev.IsTouchpad = caps.UsagePage == 0x0D && caps.Usage == 0x05;

            ushort n = caps.NumberInputValueCaps;
            if (n == 0) return dev;

            const int CapSize = 72; // sizeof(HIDP_VALUE_CAPS)
            IntPtr buf = Marshal.AllocHGlobal(CapSize * n);
            try
            {
                ushort len = n;
                if (HidP_GetValueCaps(0 /*HidP_Input*/, buf, ref len, dev.Preparsed) != HIDP_STATUS_SUCCESS) return dev;

                for (int i = 0; i < len; i++)
                {
                    int off = i * CapSize;
                    ushort usagePage = (ushort)Marshal.ReadInt16(buf, off);
                    ushort link = (ushort)Marshal.ReadInt16(buf, off + 6);
                    bool isRange = Marshal.ReadByte(buf, off + 12) != 0;
                    int logMin = Marshal.ReadInt32(buf, off + 40);
                    int logMax = Marshal.ReadInt32(buf, off + 44);
                    ushort uMin = (ushort)Marshal.ReadInt16(buf, off + 56);
                    ushort uMax = isRange ? (ushort)Marshal.ReadInt16(buf, off + 58) : uMin;

                    if (usagePage != 0x01) continue; // Generic Desktop (X=0x30, Y=0x31)

                    if (!dev.Contacts.TryGetValue(link, out var axes))
                        dev.Contacts[link] = axes = new ContactAxes();

                    if (uMin <= 0x30 && 0x30 <= uMax) { axes.XMin = logMin; axes.XMax = logMax; axes.HasX = true; }
                    if (uMin <= 0x31 && 0x31 <= uMax) { axes.YMin = logMin; axes.YMax = logMax; axes.HasY = true; }
                }
            }
            finally
            {
                Marshal.FreeHGlobal(buf);
            }

            return dev;
        }

        private void HandleRawInput(IntPtr hRawInput)
        {
            uint size = 0;
            uint headerSize = (uint)(IntPtr.Size == 8 ? 24 : 16);
            GetRawInputData(hRawInput, RID_INPUT, IntPtr.Zero, ref size, headerSize);
            if (size == 0) return;

            IntPtr data = Marshal.AllocHGlobal((int)size);
            try
            {
                if (GetRawInputData(hRawInput, RID_INPUT, data, ref size, headerSize) == uint.MaxValue) return;

                uint type = (uint)Marshal.ReadInt32(data, 0);
                if (type != RIM_TYPEHID) return;

                IntPtr hDevice = Marshal.ReadIntPtr(data, 8);
                var dev = GetDevice(hDevice);
                if (dev == null || !dev.IsTouchpad || dev.Preparsed == IntPtr.Zero) return;

                int hidOff = (int)headerSize;
                uint sizeHid = (uint)Marshal.ReadInt32(data, hidOff);
                uint count = (uint)Marshal.ReadInt32(data, hidOff + 4);
                if (sizeHid == 0 || count == 0) return;

                for (uint r = 0; r < count; r++)
                {
                    var report = new byte[sizeHid];
                    Marshal.Copy(IntPtr.Add(data, hidOff + 8 + (int)(r * sizeHid)), report, 0, (int)sizeHid);
                    ProcessReport(dev, report);
                }
            }
            finally
            {
                Marshal.FreeHGlobal(data);
            }
        }

        private void ProcessReport(TouchpadDevice dev, byte[] report)
        {
            float canvasW = CanvasTouch.CanvasSize.Width;
            float canvasH = CanvasTouch.CanvasSize.Height;
            int active = 0;

            foreach (var kv in dev.Contacts)
            {
                ushort link = kv.Key;
                var axes = kv.Value;
                if (!axes.HasX || !axes.HasY || axes.XMax <= axes.XMin || axes.YMax <= axes.YMin) continue;

                // Tip Switch (UsagePage 0x0D, Usage 0x42): só considera contatos realmente tocando
                var usages = new ushort[16];
                uint ulen = (uint)usages.Length;
                bool tip = true;
                if (HidP_GetUsages(0, 0x0D, link, usages, ref ulen, dev.Preparsed, report, (uint)report.Length) == HIDP_STATUS_SUCCESS)
                {
                    tip = false;
                    for (int i = 0; i < ulen; i++)
                        if (usages[i] == 0x42) { tip = true; break; }
                }

                if (!tip)
                {
                    _rawPaths.Remove(link);
                    continue;
                }

                if (HidP_GetUsageValue(0, 0x01, link, 0x30, out uint rx, dev.Preparsed, report, (uint)report.Length) != HIDP_STATUS_SUCCESS) continue;
                if (HidP_GetUsageValue(0, 0x01, link, 0x31, out uint ry, dev.Preparsed, report, (uint)report.Length) != HIDP_STATUS_SUCCESS) continue;

                // Normaliza a posição física (LogicalMin..LogicalMax) para 0.0..1.0
                double nx = Math.Clamp((rx - axes.XMin) / (double)(axes.XMax - axes.XMin), 0.0, 1.0);
                double ny = Math.Clamp((ry - axes.YMin) / (double)(axes.YMax - axes.YMin), 0.0, 1.0);
                active++;

                if (_ghostRunning)
                {
                    if (_ghostClock.ElapsedMilliseconds > GhostWarmupMs) _ghostMoves++;
                    continue;
                }

                int col = Math.Min(GridCols - 1, (int)(nx * GridCols));
                int row = Math.Min(GridRows - 1, (int)(ny * GridRows));
                _gridCells[col, row] = true;

                if (canvasW > 0 && canvasH > 0)
                {
                    var pt = new SKPoint((float)(nx * canvasW), (float)(ny * canvasH));
                    if (!_rawPaths.TryGetValue(link, out var path))
                    {
                        path = new List<SKPoint>();
                        _rawPaths[link] = path;
                        _paths.Add(path);
                    }
                    path.Add(pt);
                }
            }

            if (_ghostRunning) return;
            if (active == 0) _rawPaths.Clear();
            if (active >= 2) _multi = true;

            if (!_rawSeen)
            {
                _rawSeen = true;
                LblModeStatus.TextColor = Color.FromArgb("#22C55E");
                LblModeStatus.Text = "✅ Sensor HID de precisão ativo: lendo coordenadas físicas absolutas.";
            }

            RefreshBadges();
            UpdateStats();
            CanvasTouch.InvalidateSurface();
        }

        #endregion

        #region Legacy cursor mode

        private void StartLegacyCursorMode()
        {
            LblModeStatus.TextColor = Color.FromArgb("#EAB308");
            LblModeStatus.Text = "Modo Cursor: o cursor fica preso ao canvas. Pressione ESC para soltar.";
            _legacyCentered = false;

            _legacyTimer ??= Dispatcher.CreateTimer();
            _legacyTimer.Interval = TimeSpan.FromMilliseconds(100);
            _legacyTimer.Tick -= OnLegacyTick;
            _legacyTimer.Tick += OnLegacyTick;
            _legacyTimer.Start();
            ConfineCursorToCanvas();
        }

        private void StopLegacyCursorMode()
        {
            _legacyTimer?.Stop();
            ClipCursorRelease(IntPtr.Zero);
        }

        private void OnLegacyTick(object? sender, EventArgs e)
        {
            // ESC = saída de emergência (o cursor está preso e não alcança o botão)
            if ((GetAsyncKeyState(0x1B) & 0x8000) != 0)
            {
                SwitchPrecision.IsToggled = _precisionMode = true;
                return;
            }

            if (_ghostRunning) { ClipCursorRelease(IntPtr.Zero); return; }
            ConfineCursorToCanvas();
        }

        private void ConfineCursorToCanvas()
        {
            try
            {
                var hwnd = ResolveHwnd();
                if (hwnd == IntPtr.Zero || _nativeCanvas == null || _nativeCanvas.XamlRoot == null) return;

                double scale = _nativeCanvas.XamlRoot.RasterizationScale;
                var origin = _nativeCanvas.TransformToVisual(null).TransformPoint(new Windows.Foundation.Point(0, 0));
                var el = (Microsoft.UI.Xaml.FrameworkElement)_nativeCanvas;

                var client = new POINT { X = 0, Y = 0 };
                if (!ClientToScreen(hwnd, ref client)) return;

                var rect = new RECT
                {
                    Left = client.X + (int)(origin.X * scale),
                    Top = client.Y + (int)(origin.Y * scale),
                };
                rect.Right = rect.Left + (int)(el.ActualWidth * scale);
                rect.Bottom = rect.Top + (int)(el.ActualHeight * scale);
                if (rect.Right <= rect.Left || rect.Bottom <= rect.Top) return;

                if (!_legacyCentered)
                {
                    SetCursorPos((rect.Left + rect.Right) / 2, (rect.Top + rect.Bottom) / 2);
                    _legacyCentered = true;
                }
                ClipCursor(ref rect);
            }
            catch { }
        }

        #endregion
    }
}
#endif
