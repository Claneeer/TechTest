using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Xunit;
using TechTest.Forms;

namespace TechTest.Tests
{
    public class HardwareDiagnosticsTests
    {
        private void RunInStaThread(Action action)
        {
            var tcs = new TaskCompletionSource<object?>();
            var thread = new Thread(() =>
            {
                try
                {
                    action();
                    tcs.SetResult(null);
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            tcs.Task.GetAwaiter().GetResult();
        }

        [Fact]
        public void MainForm_ShouldInitializeCorrectly()
        {
            RunInStaThread(() =>
            {
                using (var form = new MainForm())
                {
                    Assert.NotNull(form);
                    Assert.Contains("TechTest", form.Text);
                    Assert.True(form.Controls.Count > 0);
                }
            });
        }

        [Fact]
        public void MicrophoneTestForm_ShouldInitializeCorrectly()
        {
            RunInStaThread(() =>
            {
                using (var form = new MicrophoneTestForm())
                {
                    Assert.NotNull(form);
                    Assert.Contains("Microfone", form.Text);
                    Assert.True(form.Controls.Count > 0);
                }
            });
        }

        [Fact]
        public void AudioTestForm_ShouldInitializeCorrectly()
        {
            RunInStaThread(() =>
            {
                using (var form = new AudioTestForm())
                {
                    Assert.NotNull(form);
                    Assert.Contains("Alto-falantes", form.Text);
                    Assert.True(form.Controls.Count > 0);
                }
            });
        }

        [Fact]
        public void CameraTestForm_ShouldInitializeCorrectly()
        {
            RunInStaThread(() =>
            {
                using (var form = new CameraTestForm())
                {
                    Assert.NotNull(form);
                    Assert.Contains("Webcam", form.Text);
                    Assert.True(form.Controls.Count > 0);
                }
            });
        }

        [Fact]
        public void DeadPixelTestForm_ShouldInitializeCorrectly()
        {
            RunInStaThread(() =>
            {
                using (var form = new DeadPixelTestForm())
                {
                    Assert.NotNull(form);
                    Assert.Equal(FormBorderStyle.None, form.FormBorderStyle);
                    Assert.True(form.Controls.Count > 0);
                }
            });
        }

        [Fact]
        public void KeyboardTestForm_ShouldInitializeCorrectly()
        {
            RunInStaThread(() =>
            {
                using (var form = new KeyboardTestForm())
                {
                    Assert.NotNull(form);
                    Assert.Contains("Teclado", form.Text);
                    Assert.True(form.Controls.Count > 0);
                }
            });
        }

        [Fact]
        public void TouchpadTestForm_ShouldInitializeCorrectly()
        {
            RunInStaThread(() =>
            {
                using (var form = new TouchpadTestForm())
                {
                    Assert.NotNull(form);
                    Assert.Contains("Touchpad", form.Text);
                    Assert.True(form.Controls.Count > 0);
                }
            });
        }

        [Fact]
        public void BatteryTestForm_ShouldInitializeCorrectly()
        {
            RunInStaThread(() =>
            {
                using (var form = new BatteryTestForm())
                {
                    Assert.NotNull(form);
                    Assert.Contains("Bateria", form.Text);
                    Assert.True(form.Controls.Count > 0);
                }
            });
        }
    }
}