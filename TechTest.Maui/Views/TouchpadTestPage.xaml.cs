using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Maui.Controls;
using SkiaSharp;
using SkiaSharp.Views.Maui;

namespace TechTest.Maui.Views
{
    public partial class TouchpadTestPage : ContentPage
    {
        // Grid scanner de zonas mortas
        private const int GridCols = 15;
        private const int GridRows = 10;
        private const int TotalGridCells = GridCols * GridRows;

        private readonly bool[,] _gridCells = new bool[GridCols, GridRows];
        private readonly List<List<SKPoint>> _paths = new();
        private List<SKPoint>? _currentPath;
        private int _clickCount = 0;

        // Checklist de eventos
        private bool _left, _right, _vScroll, _hScroll, _multi;
        private readonly HashSet<long> _activeTouchIds = new();

        // Ghost touch
        private const int GhostSeconds = 5;
        private const int GhostWarmupMs = 400;
        private bool _ghostRunning;
        private int _ghostMoves;
        private int _ghostClicks;
        private readonly Stopwatch _ghostClock = new();
        private IDispatcherTimer? _ghostTimer;

        private static readonly Color BadgeOffBg = Color.FromArgb("#1C1C38");
        private static readonly Color BadgeOffStroke = Color.FromArgb("#32325A");
        private static readonly Color BadgeOffText = Color.FromArgb("#9494B8");
        private static readonly Color BadgeOnBg = Color.FromArgb("#143D26");
        private static readonly Color BadgeOnStroke = Color.FromArgb("#22C55E");
        private static readonly Color BadgeOnText = Color.FromArgb("#4ADE80");

        // true  = Modo Precisão (coordenadas físicas absolutas via Raw Input / HID)
        // false = Modo Cursor (touchpad PS/2 legado: cursor preso ao canvas)
        private bool _precisionMode = true;

        partial void ApplyInputMode();
        partial void ReleaseInputMode();

        public TouchpadTestPage()
        {
            InitializeComponent();
            TechTest.Maui.Controls.ApprovalBar.Attach(this, TechTest.Maui.Services.TestId.Touchpad);
            ResetTest();
            SwitchPrecision.Toggled += OnModeToggled;

#if WINDOWS
            CanvasTouch.HandlerChanged += OnCanvasHandlerChanged;
#else
            _precisionMode = false;
            SwitchPrecision.IsToggled = false;
            SwitchPrecision.IsEnabled = false;
            LblModeStatus.Text = "Raw Input disponível apenas no Windows.";
#endif
        }

        private void OnModeToggled(object? sender, ToggledEventArgs e)
        {
            _precisionMode = e.Value;
            LblMode.Text = _precisionMode
                ? "Modo Precisão (Sensor Físico Absoluto)"
                : "Modo Cursor (Touchpad PS/2 Legado)";
            ResetTest();
            ApplyInputMode();
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            ApplyInputMode();
        }

        #region Reset / Stats

        private void ResetTest()
        {
            StopGhostTest(false);

            Array.Clear(_gridCells, 0, _gridCells.Length);
            _paths.Clear();
            _currentPath = null;
            _clickCount = 0;
            _left = _right = _vScroll = _hScroll = _multi = false;
            _activeTouchIds.Clear();
            LblGhostResult.IsVisible = false;

            RefreshBadges();
            UpdateStats();
            CanvasTouch.InvalidateSurface();
        }

        private void UpdateStats()
        {
            int covered = 0;
            for (int col = 0; col < GridCols; col++)
                for (int row = 0; row < GridRows; row++)
                    if (_gridCells[col, row]) covered++;

            double pct = (double)covered / TotalGridCells;

            LblCoverage.Text = $"{covered} / {TotalGridCells} ({(int)Math.Round(pct * 100)}%)";
            ProgressBarCoverage.Progress = pct;
            LblClicks.Text = _clickCount.ToString();
        }

        private void RefreshBadges()
        {
            SetBadge(BadgeLeft, LblBadgeLeft, _left);
            SetBadge(BadgeRight, LblBadgeRight, _right);
            SetBadge(BadgeVScroll, LblBadgeVScroll, _vScroll);
            SetBadge(BadgeHScroll, LblBadgeHScroll, _hScroll);
            SetBadge(BadgeMulti, LblBadgeMulti, _multi);
        }

        private static void SetBadge(Border border, Label label, bool active)
        {
            border.BackgroundColor = active ? BadgeOnBg : BadgeOffBg;
            border.Stroke = new SolidColorBrush(active ? BadgeOnStroke : BadgeOffStroke);
            label.TextColor = active ? BadgeOnText : BadgeOffText;
        }

        #endregion

        #region Touch / Pointer input

        private void OnCanvasTouch(object? sender, SKTouchEventArgs e)
        {
            var location = e.Location;
            float canvasW = CanvasTouch.CanvasSize.Width;
            float canvasH = CanvasTouch.CanvasSize.Height;

            if (canvasW <= 0 || canvasH <= 0) return;

            if (_ghostRunning)
            {
                // Qualquer evento durante o teste fantasma (sem o usuário tocar) é suspeito
                if (_ghostClock.ElapsedMilliseconds > GhostWarmupMs &&
                    (e.ActionType == SKTouchAction.Pressed || e.ActionType == SKTouchAction.Moved))
                {
                    if (e.ActionType == SKTouchAction.Pressed) _ghostClicks++;
                    else _ghostMoves++;
                }
                e.Handled = true;
                return;
            }

            switch (e.ActionType)
            {
                case SKTouchAction.Pressed:
                    _clickCount++;
                    if (e.DeviceType == SKTouchDeviceType.Touch)
                    {
                        _left = true; // toque na tela = clique primário
                        _activeTouchIds.Add(e.Id);
                        if (_activeTouchIds.Count >= 2) _multi = true;
                    }
                    else
                    {
                        if (e.MouseButton == SKMouseButton.Left) _left = true;
                        else if (e.MouseButton == SKMouseButton.Right) _right = true;
                    }

                    if (!_precisionMode)
                    {
                        _currentPath = new List<SKPoint> { location };
                        _paths.Add(_currentPath);
                        MarkGridCell(location.X, location.Y, canvasW, canvasH);
                    }
                    e.Handled = true;
                    break;

                case SKTouchAction.Moved:
                    // Hover (sem botão) também marca blocos: o objetivo é varrer a superfície
                    // Modo Precisão: ignora o cursor (e mouses USB); o grid vem do sensor físico
                    if (!_precisionMode)
                    {
                        MarkGridCell(location.X, location.Y, canvasW, canvasH);
                        if (_currentPath != null && e.InContact)
                            _currentPath.Add(location);
                    }
                    if (e.DeviceType == SKTouchDeviceType.Touch && _activeTouchIds.Count >= 2)
                        _multi = true;
                    e.Handled = true;
                    break;

                case SKTouchAction.Released:
                case SKTouchAction.Cancelled:
                    _activeTouchIds.Remove(e.Id);
                    _currentPath = null;
                    e.Handled = true;
                    break;

                case SKTouchAction.WheelChanged:
#if !WINDOWS
                    // No Windows o scroll é tratado pelo evento nativo (separa vertical/horizontal/pinça)
                    if (e.WheelDelta != 0) _vScroll = true;
#endif
                    break;
            }

            RefreshBadges();
            UpdateStats();
            CanvasTouch.InvalidateSurface();
        }

        private void MarkGridCell(float x, float y, float width, float height)
        {
            int col = (int)(x / (width / GridCols));
            int row = (int)(y / (height / GridRows));

            if (col >= 0 && col < GridCols && row >= 0 && row < GridRows)
                _gridCells[col, row] = true;
        }

        #endregion

        #region Ghost touch

        private async void OnGhostClicked(object? sender, EventArgs e)
        {
            if (_ghostRunning)
            {
                StopGhostTest(false);
                return;
            }

            bool ok = await DisplayAlert("Teste de Toque Fantasma",
                "Durante 5 segundos NÃO toque no touchpad nem no mouse.\n" +
                "O teste monitora se o cursor se move ou clica sozinho.",
                "Iniciar", "Cancelar");
            if (!ok) return;

            StartGhostTest();
        }

        private void StartGhostTest()
        {
            _ghostMoves = 0;
            _ghostClicks = 0;
            _ghostRunning = true;
            _ghostClock.Restart();
            _lastCursorValid = false;

            LblGhostResult.IsVisible = true;
            LblGhostResult.TextColor = Color.FromArgb("#EAB308");
            LblGhostResult.Text = "⏳ Monitorando... não toque no touchpad.";

            _ghostTimer ??= Dispatcher.CreateTimer();
            _ghostTimer.Interval = TimeSpan.FromMilliseconds(40);
            _ghostTimer.Tick -= OnGhostTick;
            _ghostTimer.Tick += OnGhostTick;
            _ghostTimer.Start();
        }

        private void OnGhostTick(object? sender, EventArgs e)
        {
            if (!_ghostRunning) return;

            long elapsed = _ghostClock.ElapsedMilliseconds;

            if (elapsed > GhostWarmupMs)
                PollGlobalCursor();

            double remaining = GhostSeconds - elapsed / 1000.0;
            BtnGhost.Text = $"👻 Monitorando... {Math.Max(0, Math.Ceiling(remaining))}s (cancelar)";

            if (elapsed >= GhostSeconds * 1000)
                StopGhostTest(true);
        }

        private void StopGhostTest(bool showResult)
        {
            _ghostTimer?.Stop();
            bool wasRunning = _ghostRunning;
            _ghostRunning = false;
            _ghostClock.Reset();
            BtnGhost.Text = "👻 Testar Toque Fantasma (5s)";

            if (!wasRunning) return;

            if (!showResult)
            {
                LblGhostResult.IsVisible = false;
                return;
            }

            int total = _ghostMoves + _ghostClicks;
            LblGhostResult.IsVisible = true;
            if (total == 0)
            {
                LblGhostResult.TextColor = Color.FromArgb("#22C55E");
                LblGhostResult.Text = "✅ Nenhum toque fantasma detectado em 5s.";
            }
            else
            {
                LblGhostResult.TextColor = Color.FromArgb("#EF4444");
                LblGhostResult.Text = $"❌ Toque fantasma! {_ghostMoves} movimento(s) e {_ghostClicks} clique(s) sem interação.";
            }
        }

        // Monitoramento global do cursor (Windows): detecta movimento/clique mesmo fora do canvas
        private bool _lastCursorValid;
        private int _lastX, _lastY;
        private bool _lastLeftDown, _lastRightDown;

        private void PollGlobalCursor()
        {
#if WINDOWS
            if (!GetCursorPos(out POINT p)) return;
            bool left = (GetAsyncKeyState(0x01) & 0x8000) != 0;
            bool right = (GetAsyncKeyState(0x02) & 0x8000) != 0;

            if (!_lastCursorValid)
            {
                _lastX = p.X; _lastY = p.Y;
                _lastLeftDown = left; _lastRightDown = right;
                _lastCursorValid = true;
                return;
            }

            if (Math.Abs(p.X - _lastX) > 1 || Math.Abs(p.Y - _lastY) > 1)
                _ghostMoves++;
            if ((left && !_lastLeftDown) || (right && !_lastRightDown))
                _ghostClicks++;

            _lastX = p.X; _lastY = p.Y;
            _lastLeftDown = left; _lastRightDown = right;
#endif
        }

#if WINDOWS
        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct POINT { public int X; public int Y; }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        private Microsoft.UI.Xaml.UIElement? _nativeCanvas;

        private void OnCanvasHandlerChanged(object? sender, EventArgs e)
        {
            // PlatformView só existe depois que o handler é criado
            if (_nativeCanvas != null)
            {
                _nativeCanvas.RemoveHandler(Microsoft.UI.Xaml.UIElement.PointerWheelChangedEvent,
                    new Microsoft.UI.Xaml.Input.PointerEventHandler(OnNativeWheel));
                _nativeCanvas = null;
            }

            if (CanvasTouch.Handler?.PlatformView is Microsoft.UI.Xaml.UIElement el)
            {
                _nativeCanvas = el;
                // handledEventsToo = true: o SkiaSharp pode marcar o evento como tratado
                el.AddHandler(Microsoft.UI.Xaml.UIElement.PointerWheelChangedEvent,
                    new Microsoft.UI.Xaml.Input.PointerEventHandler(OnNativeWheel), true);
            }
        }

        private void OnNativeWheel(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (_ghostRunning)
            {
                if (_ghostClock.ElapsedMilliseconds > GhostWarmupMs) _ghostMoves++;
                return;
            }

            var props = e.GetCurrentPoint(_nativeCanvas).Properties;
            bool ctrl = e.KeyModifiers.HasFlag(Windows.System.VirtualKeyModifiers.Control);

            if (ctrl)
            {
                // Touchpads de precisão enviam a pinça como Ctrl + roda
                _multi = true;
            }
            else if (props.IsHorizontalMouseWheel)
            {
                _hScroll = true;
            }
            else
            {
                _vScroll = true;
            }

            RefreshBadges();
        }
#endif

        #endregion

        #region Paint

        private void OnPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
        {
            var canvas = e.Surface.Canvas;
            var info = e.Info;

            canvas.Clear(SKColor.Parse("#0A0A14"));

            float w = info.Width;
            float h = info.Height;
            float cellW = w / GridCols;
            float cellH = h / GridRows;

            // Blocos visitados em verde
            using var cellPaint = new SKPaint
            {
                Color = SKColor.Parse("#22C55E").WithAlpha(110),
                Style = SKPaintStyle.Fill,
                IsAntialias = true
            };

            for (int col = 0; col < GridCols; col++)
            {
                for (int row = 0; row < GridRows; row++)
                {
                    if (_gridCells[col, row])
                    {
                        var cellRect = new SKRect(col * cellW + 1, row * cellH + 1, (col + 1) * cellW - 1, (row + 1) * cellH - 1);
                        canvas.DrawRect(cellRect, cellPaint);
                    }
                }
            }

            // Linhas do grid
            using var gridPaint = new SKPaint
            {
                Color = SKColor.Parse("#32325A"),
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 1f,
                IsAntialias = true
            };

            for (int i = 1; i < GridCols; i++)
                canvas.DrawLine(i * cellW, 0, i * cellW, h, gridPaint);
            for (int j = 1; j < GridRows; j++)
                canvas.DrawLine(0, j * cellH, w, j * cellH, gridPaint);

            // Traços do usuário
            using var brushPaint = new SKPaint
            {
                Color = SKColor.Parse("#3B82F6"),
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 4f,
                StrokeCap = SKStrokeCap.Round,
                StrokeJoin = SKStrokeJoin.Round,
                IsAntialias = true
            };

            foreach (var path in _paths)
            {
                if (path.Count < 2) continue;

                using var skPath = new SKPath();
                skPath.MoveTo(path[0]);
                for (int idx = 1; idx < path.Count; idx++)
                    skPath.LineTo(path[idx]);
                canvas.DrawPath(skPath, brushPaint);
            }
        }

        #endregion

        private void OnResetClicked(object? sender, EventArgs e) => ResetTest();

        private async void OnBackClicked(object? sender, EventArgs e)
        {
            ResetTest();
            await Shell.Current.GoToAsync("//MainPage");
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            ReleaseInputMode();
            ResetTest();
        }
    }
}
