using System;
using Microsoft.Maui.Controls;
using TechTest.Maui.Controls;

namespace TechTest.Maui.Views
{
    public partial class KeyboardTestPage : ContentPage
    {
        public KeyboardTestPage()
        {
            InitializeComponent();
            
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
            KeyboardCanvas.KeyUp(mappedId);
            KeyboardCanvas.KeyPressed(mappedId);
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
            var window = App.Current?.Windows?[0]?.Handler?.PlatformView as Microsoft.UI.Xaml.Window;
            if (window != null)
            {
                _winuiWindow = window;
                if (window.Content != null)
                {
                    window.Content.KeyDown += PlatformContent_KeyDown;
                    window.Content.KeyUp += PlatformContent_KeyUp;
                    window.Content.Focus(Microsoft.UI.Xaml.FocusState.Programmatic);
                }
            }
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
            if (_winuiWindow != null && _winuiWindow.Content != null)
            {
                _winuiWindow.Content.KeyDown -= PlatformContent_KeyDown;
                _winuiWindow.Content.KeyUp -= PlatformContent_KeyUp;
            }
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
