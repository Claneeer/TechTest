using System;
using Microsoft.Maui.Controls;
using TechTest.Maui.Services;
using TechTest.Maui.ViewModels;

namespace TechTest.Maui.Views
{
    public partial class AudioTestPage : ContentPage
    {
        private readonly AudioTestViewModel _viewModel;

        public AudioTestPage()
        {
            InitializeComponent();

            var audioPlayback = IPlatformApplication.Current?.Services?.GetService<IAudioPlaybackService>();
            _viewModel = new AudioTestViewModel(audioPlayback);
            BindingContext = _viewModel;

            PickerSoundType.SelectedIndex = 0;

            _viewModel.PropertyChanged += (s, e) =>
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    switch (e.PropertyName)
                    {
                        case nameof(_viewModel.StatusText):
                            LblStatus.Text = _viewModel.StatusText;
                            LblStatus.TextColor = _viewModel.StatusColor;
                            break;
                        case nameof(_viewModel.IsPlaying):
                            SpeakerView.SetPlayingState(
                                _viewModel.IsPlaying,
                                _viewModel.ActiveChannel);
                            break;
                    }
                });
            };
        }

        private void OnFrequencyChanged(object sender, ValueChangedEventArgs e)
        {
            int freq = (int)e.NewValue;
            if (freq >= 1000)
                LblFrequency.Text = $"{freq / 1000.0:F1} kHz";
            else
                LblFrequency.Text = $"{freq} Hz";
            _viewModel.Frequency = freq;
        }

        private void OnVolumeChanged(object sender, ValueChangedEventArgs e)
        {
            int pct = (int)(e.NewValue * 100);
            LblVolume.Text = $"{pct}%";
            _viewModel.Volume = (float)e.NewValue;
        }

        private void OnSoundTypeChanged(object sender, EventArgs e)
        {
            if (PickerSoundType.SelectedIndex >= 0)
                _viewModel.SoundType = PickerSoundType.SelectedIndex;
        }

        private void OnPreset100Hz(object sender, EventArgs e)
        {
            SliderFrequency.Value = 100;
        }

        private void OnPreset440Hz(object sender, EventArgs e)
        {
            SliderFrequency.Value = 440;
        }

        private void OnPreset1kHz(object sender, EventArgs e)
        {
            SliderFrequency.Value = 1000;
        }

        private void OnPreset4kHz(object sender, EventArgs e)
        {
            SliderFrequency.Value = 4000;
        }

        private void OnPreset10kHz(object sender, EventArgs e)
        {
            SliderFrequency.Value = 10000;
        }

        private void OnLeftClicked(object sender, EventArgs e)
        {
            _viewModel.PlayTone(AudioChannel.Left);
        }

        private void OnRightClicked(object sender, EventArgs e)
        {
            _viewModel.PlayTone(AudioChannel.Right);
        }

        private void OnBothClicked(object sender, EventArgs e)
        {
            _viewModel.PlayTone(AudioChannel.Both);
        }

        private void OnStopClicked(object sender, EventArgs e)
        {
            _viewModel.StopTone();
            SpeakerView.SetPlayingState(false, AudioChannel.Both);
        }

        private async void OnBackClicked(object sender, EventArgs e)
        {
            _viewModel.StopTone();
            await Shell.Current.GoToAsync("//MainPage");
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            _viewModel.StopTone();
        }
    }
}
