using System;
using Microsoft.Maui.Controls;

namespace TechTest.Maui.Views
{
    public partial class DeadPixelTestPage : ContentPage
    {
        private readonly Color[] _testColors = new[]
        {
            Colors.Red,
            Colors.Green,
            Colors.Blue,
            Colors.White,
            Colors.Black,
            Colors.Yellow,
            Colors.Magenta,
            Colors.Cyan
        };

        private readonly string[] _colorNames = new[]
        {
            "Vermelho",
            "Verde",
            "Azul",
            "Branco",
            "Preto",
            "Amarelo",
            "Magenta",
            "Ciano"
        };

        private int _colorIndex = 0;

        public DeadPixelTestPage()
        {
            InitializeComponent();
        }

        private void OnStartTestClicked(object sender, EventArgs e)
        {
            StartTest();
        }

        private void StartTest()
        {
            _colorIndex = 0;
            LayoutNormal.IsVisible = false;
            LayoutFullscreen.IsVisible = true;
            UpdateColor();

#if WINDOWS
            // Hook key events to intercept Escape key
            var window = App.Current?.Windows?[0]?.Handler?.PlatformView as Microsoft.UI.Xaml.Window;
            if (window != null && window.Content != null)
            {
                window.Content.KeyDown += PlatformContent_KeyDown;
                window.Content.Focus(Microsoft.UI.Xaml.FocusState.Programmatic);
            }
#endif
        }

        private void StopTest()
        {
            LayoutFullscreen.IsVisible = false;
            LayoutNormal.IsVisible = true;

#if WINDOWS
            var window = App.Current?.Windows?[0]?.Handler?.PlatformView as Microsoft.UI.Xaml.Window;
            if (window != null && window.Content != null)
            {
                window.Content.KeyDown -= PlatformContent_KeyDown;
            }
#endif
        }

        private void UpdateColor()
        {
            if (_colorIndex >= 0 && _colorIndex < _testColors.Length)
            {
                BoxFill.Color = _testColors[_colorIndex];
                LblFullscreenInstruction.Text = $"Cor Atual: {_colorNames[_colorIndex]}. Clique para avançar. Pressione ESC para sair.";
            }
        }

        private void OnFullscreenTapped(object sender, EventArgs e)
        {
            _colorIndex++;
            if (_colorIndex >= _testColors.Length)
            {
                StopTest();
            }
            else
            {
                UpdateColor();
            }
        }

#if WINDOWS
        private void PlatformContent_KeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Escape)
            {
                e.Handled = true;
                StopTest();
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
