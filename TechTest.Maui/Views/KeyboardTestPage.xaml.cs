using System;
using Microsoft.Maui.Controls;
using TechTest.Maui.Controls;
using TechTest.Maui.ViewModels;

namespace TechTest.Maui.Views
{
    public partial class KeyboardTestPage : ContentPage
    {
        private readonly KeyboardTestViewModel _vm = new();

        public KeyboardTestPage()
        {
            InitializeComponent();
            TechTest.Maui.Controls.ApprovalBar.Attach(this, TechTest.Maui.Services.TestId.Keyboard);
            BindingContext = _vm;
            
            // Set initial switch value and numpad setting
            SwitchNumpad.IsToggled = true;
            KeyboardCanvas.HasNumpad = true;
        }

        private void KeyboardCanvas_ProgressChanged(object sender, int pressedCount)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                int total = KeyboardCanvas.TotalKeys;
                double pct = total > 0 ? (double)pressedCount / total : 0;
                
                LblProgress.Text = $"{pressedCount} / {total} ({(int)(pct * 100)}%)";
                ProgressBarKeys.Progress = pct;
            });
        }

        private void OnNumpadToggled(object sender, ToggledEventArgs e)
        {
            KeyboardCanvas.HasNumpad = e.Value;
            UpdateProgress(KeyboardCanvas.PressedCount);
        }

        private void OnResetClicked(object sender, EventArgs e)
        {
            KeyboardCanvas.ResetKeys();
            _vm.ResetHistory();
            UpdateProgress(0);
        }

        private async void OnBackClicked(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("//MainPage");
        }

        private void UpdateProgress(int pressedCount)
        {
            int total = KeyboardCanvas.TotalKeys;
            double pct = total > 0 ? (double)pressedCount / total : 0;
            
            LblProgress.Text = $"{pressedCount} / {total} ({(int)(pct * 100)}%)";
            ProgressBarKeys.Progress = pct;
        }

        #region Platform Keyboard Event Hooking

#if WINDOWS
        private Microsoft.UI.Xaml.Window _winuiWindow;

        private string MapWinKey(Windows.System.VirtualKey key, Windows.UI.Core.CorePhysicalKeyStatus st)
        {
            int vk = (int)key;
            bool ext = st.IsExtendedKey;

            if (vk >= 65 && vk <= 90) return ((char)vk).ToString();      // A-Z
            if (vk >= 48 && vk <= 57) return ((char)vk).ToString();      // 0-9
            if (vk >= 96 && vk <= 105) return "Np" + (vk - 96);           // Numpad 0-9
            if (vk >= 112 && vk <= 123) return "F" + (vk - 111);          // F1-F12

            // Teclas do bloco numérico com NumLock desligado (chegam como navegação, sem flag estendida)
            if (!ext)
            {
                switch (vk)
                {
                    case 36: return "Np7"; case 38: return "Np8"; case 33: return "Np9";
                    case 37: return "Np4"; case 12: return "Np5"; case 39: return "Np6";
                    case 35: return "Np1"; case 40: return "Np2"; case 34: return "Np3";
                    case 45: return "Np0"; case 46: return "Np.";
                }
            }

            switch (vk)
            {
                case 16: return st.ScanCode == 54 ? "RShift" : "LShift";
                case 160: return "LShift";
                case 161: return "RShift";
                case 17: return ext ? "RCtrl" : "LCtrl";
                case 162: return "LCtrl";
                case 163: return "RCtrl";
                case 18: return ext ? "AltGr" : "Alt";   // RightMenu (AltGr) é estendida
                case 164: return "Alt";
                case 165: return "AltGr";
                case 91: case 92: return "Win";
                case 13: return ext ? "NpEnt" : "Enter";
                case 8: return "Backspace";
                case 9: return "Tab";
                case 20: return "Caps";
                case 27: return "Esc";
                case 32: return "Space";
                case 37: return "Left";
                case 38: return "Up";
                case 39: return "Right";
                case 40: return "Down";
                case 44: return "PrtSc";
                case 46: return "Del";
                case 144: return "Num";
                case 106: return "Np*";
                case 107: return "Np+";
                case 109: return "Np-";
                case 110: return "Np.";
                case 111: return "Np/";
                // Pontuação ABNT2
                case 186: return "Ç";      // Oem1
                case 187: return "=";      // OemPlus
                case 188: return ",";      // OemComma
                case 189: return "-";      // OemMinus
                case 190: return ".";      // OemPeriod
                case 191: return "/";      // Oem2  (; :)
                case 192: return "'";      // Oem3  (' \")
                case 193: return "Abnt/";  // AbntC1 (/ ? °)
                case 194: return "Np.";    // AbntC2 (separador numérico)
                case 219: return "[";      // Oem4  (´ `)
                case 220: return "\\";     // Oem5  (] })
                case 221: return "]";      // Oem6  ([ {)
                case 222: return "~";      // Oem7  (~ ^)
                case 226: return "|";      // Oem102 (\ |)
            }

            return KeyboardCanvas.MapKeyToId(key.ToString());
        }

        private void PlatformContent_KeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            // Impede Tab/Space/Enter/Alt/F10/Setas de roubarem foco ou acionarem botões
            e.Handled = true;

            string mappedId = MapWinKey(e.Key, e.KeyStatus);

            // Auto-repeat: não conta como nova pressão (e não deve disparar chattering)
            if (e.KeyStatus.WasKeyDown)
            {
                KeyboardCanvas.KeyDown(mappedId);
                return;
            }

            // Detecção de chattering: KeyUp -> KeyDown da mesma tecla em < 35 ms
            long? chatterMs = _vm.RegisterKeyDown(mappedId);
            if (chatterMs.HasValue)
                KeyboardCanvas.MarkChatter(mappedId, chatterMs.Value);

            KeyboardCanvas.KeyDown(mappedId);
        }

        private void PlatformContent_KeyUp(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            e.Handled = true;

            string mappedId = MapWinKey(e.Key, e.KeyStatus);
            _vm.RegisterKeyUp(mappedId);
            KeyboardCanvas.KeyUp(mappedId);
            KeyboardCanvas.KeyPressed(mappedId);
        }

        private Microsoft.UI.Xaml.UIElement? _hookedContent;

        private void OnPageLoadedHook(object? sender, EventArgs e) => TryHookWindowsKeyboard();
        private void OnPageHandlerChangedHook(object? sender, EventArgs e) => TryHookWindowsKeyboard();

        private void TryHookWindowsKeyboard()
        {
            try
            {
                // Window.Handler / PlatformView can be null until the page is attached and loaded
                var window = this.Window?.Handler?.PlatformView as Microsoft.UI.Xaml.Window;
                var content = window?.Content;
                if (window == null || content == null) return;
                if (ReferenceEquals(_hookedContent, content)) return;

                UnhookWindowsKeyboard();
                _winuiWindow = window;
                _hookedContent = content;
                content.PreviewKeyDown += PlatformContent_KeyDown;
                content.PreviewKeyUp += PlatformContent_KeyUp;
                content.Focus(Microsoft.UI.Xaml.FocusState.Programmatic);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Keyboard hook failed: {ex.Message}");
            }
        }

        private void UnhookWindowsKeyboard()
        {
            if (_hookedContent != null)
            {
                _hookedContent.PreviewKeyDown -= PlatformContent_KeyDown;
                _hookedContent.PreviewKeyUp -= PlatformContent_KeyUp;
                _hookedContent = null;
            }
        }
#endif

#if MACCATALYST
        private Foundation.NSObject _keyboardObserverBegan;
        private Foundation.NSObject _keyboardObserverEnded;
#endif

        protected override void OnAppearing()
        {
            base.OnAppearing();
            
            KeyboardCanvas.ProgressChanged += KeyboardCanvas_ProgressChanged;
            UpdateProgress(KeyboardCanvas.PressedCount);

#if WINDOWS
            Loaded -= OnPageLoadedHook;
            Loaded += OnPageLoadedHook;
            HandlerChanged -= OnPageHandlerChangedHook;
            HandlerChanged += OnPageHandlerChangedHook;
            TryHookWindowsKeyboard();
#endif

#if MACCATALYST
            // In macOS Catalyst, we hook key presses via UIWindow presses notifications or standard Cocoa observers
            // Let's implement a clean observer if they exist, or notify that Catalyst is ready.
            // A bulletproof way to receive keyboard presses on MacCatalyst is registering custom UIKeyCommands on the root VC.
            var vc = GetMacViewController();
            if (vc != null)
            {
                RegisterMacKeyCommands(vc);
            }
#endif
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            
            KeyboardCanvas.ProgressChanged -= KeyboardCanvas_ProgressChanged;

#if WINDOWS
            Loaded -= OnPageLoadedHook;
            HandlerChanged -= OnPageHandlerChangedHook;
            UnhookWindowsKeyboard();
#endif

#if MACCATALYST
            var vc = GetMacViewController();
            if (vc != null)
            {
                UnregisterMacKeyCommands(vc);
            }
#endif
        }

#if MACCATALYST
        private UIKit.UIViewController GetMacViewController()
        {
            var view = Handler?.PlatformView as UIKit.UIView;
            if (view == null) return null;
            var responder = view.NextResponder;
            while (responder != null)
            {
                if (responder is UIKit.UIViewController vc) return vc;
                responder = responder.NextResponder;
            }
            return null;
        }

        private void RegisterMacKeyCommands(UIKit.UIViewController vc)
        {
            // Register UIKeyCommands for standard characters and functions
            // Since MacCatalyst requires specific UIKeyCommand setups, let's create a generic responder loop
            // for standard key entries to let Catalyst capture presses correctly.
            try
            {
                // Add key commands for alphabetic and common keys
                string alphabet = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789 `1234567890-=[]\\;',./~!@#$%^&*()_+{}|:\"<>?";
                foreach (char c in alphabet)
                {
                    var cmd = UIKit.UIKeyCommand.Create((Foundation.NSString)c.ToString(), (UIKit.UIKeyModifierFlags)0, new ObjCRuntime.Selector("handleMacKey:"));
                    vc.AddKeyCommand(cmd);
                }
                
                // Add special keys
                var specials = new[] { 
                    UIKit.UIKeyCommand.LimitInput, UIKit.UIKeyCommand.LimitInput,
                    UIKit.UIKeyCommand.LeftArrow, UIKit.UIKeyCommand.RightArrow, 
                    UIKit.UIKeyCommand.UpArrow, UIKit.UIKeyCommand.DownArrow,
                    UIKit.UIKeyCommand.Escape
                };
                foreach (var special in specials)
                {
                    var cmd = UIKit.UIKeyCommand.Create((Foundation.NSString)special, (UIKit.UIKeyModifierFlags)0, new ObjCRuntime.Selector("handleMacKey:"));
                    vc.AddKeyCommand(cmd);
                }
            }
            catch
            {
                // Silently skip if Selector is not fully wired
            }
        }

        private void UnregisterMacKeyCommands(UIKit.UIViewController vc)
        {
            // Clean up registered commands
        }
#endif

        #endregion
    }
}
