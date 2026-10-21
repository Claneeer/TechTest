using System;
using Microsoft.Maui.Controls;

namespace TechTest.Maui.Views
{
    public partial class DeadPixelTestPage : ContentPage
    {
        private TechTest.Maui.Controls.ApprovalBar? _approvalBar;

        // Ordem obrigatória: Preto, Branco e depois as demais cores
        private readonly Color[] _testColors = new[]
        {
            Color.FromArgb("#000000"),
            Color.FromArgb("#FFFFFF"),
            Colors.Red,
            Colors.Green,
            Colors.Blue,
            Colors.Cyan,
            Colors.Magenta,
            Colors.Yellow
        };

        private readonly string[] _colorNames = new[]
        {
            "Preto",
            "Branco",
            "Vermelho",
            "Verde",
            "Azul",
            "Ciano",
            "Magenta",
            "Amarelo"
        };

        private int _colorIndex = 0;
        private bool _testActive;
        private IDispatcherTimer? _hideCloseTimer;

        public DeadPixelTestPage()
        {
            InitializeComponent();
            _approvalBar = TechTest.Maui.Controls.ApprovalBar.Attach(this, TechTest.Maui.Services.TestId.DeadPixel);
        }

        private void OnStartTestClicked(object sender, EventArgs e)
        {
            StartTest();
        }

        private void StartTest()
        {
            _testActive = true;
            _colorIndex = 0;

            // Esconde cabeçalho/instruções e a ApprovalBar: o fundo ocupa 100% da página
            LayoutNormal.IsVisible = false;
            if (_approvalBar != null) _approvalBar.IsVisible = false;
            Shell.SetTabBarIsVisible(this, false);
            LayoutFullscreen.IsVisible = true;
            UpdateColor();
            ShowCloseButtonTemporarily();

#if WINDOWS
            try
            {
                var window = this.Window?.Handler?.PlatformView as Microsoft.UI.Xaml.Window;
                if (window != null)
                {
                    SetWindowFullScreen(window, true);
                    if (window.Content != null)
                    {
                        // Preview (tunneling): teclas Space/Enter não são consumidas por botões focados
                        window.Content.PreviewKeyDown -= PlatformContent_KeyDown;
                        window.Content.PreviewKeyDown += PlatformContent_KeyDown;
                        window.Content.Focus(Microsoft.UI.Xaml.FocusState.Programmatic);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Fullscreen failed: {ex.Message}");
            }
#endif
        }

        private void StopTest()
        {
            if (!_testActive) return;
            _testActive = false;

            _hideCloseTimer?.Stop();
            LayoutFullscreen.IsVisible = false;
            LayoutNormal.IsVisible = true;
            if (_approvalBar != null) _approvalBar.IsVisible = true;
            Shell.SetTabBarIsVisible(this, true);

#if WINDOWS
            try
            {
                var window = this.Window?.Handler?.PlatformView as Microsoft.UI.Xaml.Window;
                if (window != null)
                {
                    if (window.Content != null)
                        window.Content.PreviewKeyDown -= PlatformContent_KeyDown;
                    SetWindowFullScreen(window, false);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Exit fullscreen failed: {ex.Message}");
            }
#endif
        }

#if WINDOWS
        private static void SetWindowFullScreen(Microsoft.UI.Xaml.Window window, bool fullScreen)
        {
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
            var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
            appWindow.SetPresenter(fullScreen
                ? Microsoft.UI.Windowing.AppWindowPresenterKind.FullScreen
                : Microsoft.UI.Windowing.AppWindowPresenterKind.Overlapped);
        }
#endif

        private void UpdateColor()
        {
            if (_colorIndex >= 0 && _colorIndex < _testColors.Length)
            {
                BoxFill.Color = _testColors[_colorIndex];
                LblFullscreenInstruction.Text =
                    $"Cor {_colorIndex + 1}/{_testColors.Length}: {_colorNames[_colorIndex]}. Clique/Espaço/→ avança, ← volta. ESC ou ✕ para sair.";
            }
        }

        private void NextColor()
        {
            _colorIndex = (_colorIndex + 1) % _testColors.Length;
            UpdateColor();
        }

        private void PreviousColor()
        {
            _colorIndex = (_colorIndex - 1 + _testColors.Length) % _testColors.Length;
            UpdateColor();
        }

        // Clicar apenas troca de cor; nunca sai do teste
        private void OnFullscreenTapped(object sender, EventArgs e)
        {
            NextColor();
        }

        private void OnCloseClicked(object sender, EventArgs e)
        {
            StopTest();
        }

        // Mexer o mouse mostra o botão ✕ por 2.5s
        private void OnFullscreenPointerMoved(object sender, PointerEventArgs e)
        {
            if (_testActive) ShowCloseButtonTemporarily();
        }

        private void ShowCloseButtonTemporarily()
        {
            BtnClose.IsVisible = true;
            BtnClose.Opacity = 1;

            _hideCloseTimer ??= CreateHideTimer();
            _hideCloseTimer.Stop();
            _hideCloseTimer.Start();
        }

        private IDispatcherTimer CreateHideTimer()
        {
            var timer = Dispatcher.CreateTimer();
            timer.Interval = TimeSpan.FromMilliseconds(2500);
            timer.IsRepeating = false;
            timer.Tick += (_, _) =>
            {
                BtnClose.Opacity = 0;
                BtnClose.IsVisible = false;
            };
            return timer;
        }

#if WINDOWS
        private void PlatformContent_KeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            if (!_testActive) return;

            switch (e.Key)
            {
                case Windows.System.VirtualKey.Escape:
                    e.Handled = true;
                    StopTest();
                    break;
                case Windows.System.VirtualKey.Space:
                case Windows.System.VirtualKey.Enter:
                case Windows.System.VirtualKey.Right:
                    e.Handled = true;
                    NextColor();
                    break;
                case Windows.System.VirtualKey.Left:
                    e.Handled = true;
                    PreviousColor();
                    break;
            }
        }
#endif

        private async void OnBackClicked(object sender, EventArgs e)
        {
            StopTest();
            await Shell.Current.GoToAsync("//MainPage");
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            StopTest();
        }
    }
}
