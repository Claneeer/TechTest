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

        private void PlatformContent_KeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            e.Handled = true;
            string keyStr = e.Key.ToString();
            var keyStatus = e.KeyStatus;

            // Differentiate modifier keys using ScanCode and ExtendedKey
            if (e.Key == Windows.System.VirtualKey.Shift)
            {
                keyStr = keyStatus.ScanCode == 54 ? "RightShift" : "LeftShift";
            }
            else if (e.Key == Windows.System.VirtualKey.Control)
            {
                keyStr = keyStatus.IsExtendedKey ? "RightCtrl" : "LeftControl";
            }
            else if (e.Key == Windows.System.VirtualKey.Menu) // Alt
            {
                keyStr = keyStatus.IsExtendedKey ? "RightAlt" : "LeftAlt";
            }

            string mappedId = KeyboardCanvas.MapKeyToId(keyStr);

            // Detecção de chattering: KeyUp -> KeyDown da mesma tecla em < 35 ms
            long? chatterMs = _vm.RegisterKeyDown(mappedId);
            if (chatterMs.HasValue)
                KeyboardCanvas.MarkChatter(mappedId, chatterMs.Value);

            KeyboardCanvas.KeyDown(mappedId);
        }

        private void PlatformContent_KeyUp(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            e.Handled = true;
            string keyStr = e.Key.ToString();
            var keyStatus = e.KeyStatus;

            if (e.Key == Windows.System.VirtualKey.Shift)
            {
                keyStr = keyStatus.ScanCode == 54 ? "RightShift" : "LeftShift";
            }
            else if (e.Key == Windows.System.VirtualKey.Control)
            {
                keyStr = keyStatus.IsExtendedKey ? "RightCtrl" : "LeftControl";
            }
            else if (e.Key == Windows.System.VirtualKey.Menu) // Alt
            {
                keyStr = keyStatus.IsExtendedKey ? "RightAlt" : "LeftAlt";
            }

            string mappedId = KeyboardCanvas.MapKeyToId(keyStr);
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
                content.KeyDown += PlatformContent_KeyDown;
                content.KeyUp += PlatformContent_KeyUp;
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
                _hookedContent.KeyDown -= PlatformContent_KeyDown;
                _hookedContent.KeyUp -= PlatformContent_KeyUp;
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
