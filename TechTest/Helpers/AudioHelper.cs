using System;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace TechTest.Helpers
{
    public enum AudioChannel { Left, Right, Both }

    public enum SoundType
    {
        Sine,
        Square,
        WhiteNoise,
        Sweep,
        Beep
    }

    public static class AudioHelper
    {
        public static float CalculateRMS(byte[] buffer, int bytesRecorded)
        {
            float sum = 0;
            int sampleCount = bytesRecorded / 2;
            for (int i = 0; i < bytesRecorded; i += 2)
            {
                short sample = (short)(buffer[i] | (buffer[i + 1] << 8));
                float normalized = sample / 32768f;
                sum += normalized * normalized;
            }
            return sampleCount > 0 ? (float)Math.Sqrt(sum / sampleCount) : 0;
        }

        public static float CalculatePeak(byte[] buffer, int bytesRecorded)
        {
            float max = 0;
            for (int i = 0; i < bytesRecorded; i += 2)
            {
                short sample = (short)(buffer[i] | (buffer[i + 1] << 8));
                float abs = Math.Abs(sample / 32768f);
                if (abs > max) max = abs;
            }
            return max;
        }

        /// <summary>
        /// Creates a tone with the specified frequency, channel, sound type, and volume gain.
        /// </summary>
        /// <param name="frequency">Frequency in Hz (50 to 15000)</param>
        /// <param name="channel">Left, Right, or Both speaker channels</param>
        /// <param name="soundType">Type of waveform to generate</param>
        /// <param name="gain">Volume gain (0.0 to 2.0, where 1.0 is 100% and 2.0 is 200%)</param>
        /// <returns>A stereo ISampleProvider ready for playback</returns>
        public static ISampleProvider CreateTone(float frequency, AudioChannel channel,
            SoundType soundType = SoundType.Sine, float gain = 0.5f)
        {
            ISampleProvider source;

            switch (soundType)
            {
                case SoundType.Square:
                    source = new SignalGenerator(44100, 1)
                    {
                        Frequency = frequency,
                        Type = SignalGeneratorType.Square,
                        Gain = gain
                    };
                    break;

                case SoundType.WhiteNoise:
                    source = new SignalGenerator(44100, 1)
                    {
                        Frequency = frequency,
                        Type = SignalGeneratorType.White,
                        Gain = gain
                    };
                    break;

                case SoundType.Sweep:
                    source = new SignalGenerator(44100, 1)
                    {
                        Frequency = 100,
                        FrequencyEnd = 10000,
                        Type = SignalGeneratorType.Sweep,
                        SweepLengthSecs = 5,
                        Gain = gain
                    };
                    break;

                case SoundType.Beep:
                    // Beep is a sine wave that will be pulsed on/off by the caller
                    source = new SignalGenerator(44100, 1)
                    {
                        Frequency = frequency,
                        Type = SignalGeneratorType.Sin,
                        Gain = gain
                    };
                    break;

                case SoundType.Sine:
                default:
                    source = new SignalGenerator(44100, 1)
                    {
                        Frequency = frequency,
                        Type = SignalGeneratorType.Sin,
                        Gain = gain
                    };
                    break;
            }

            var stereo = new MonoToStereoSampleProvider(source);
            switch (channel)
            {
                case AudioChannel.Left:
                    stereo.RightVolume = 0f;
                    stereo.LeftVolume = 1f;
                    break;
                case AudioChannel.Right:
                    stereo.LeftVolume = 0f;
                    stereo.RightVolume = 1f;
                    break;
                case AudioChannel.Both:
                    stereo.LeftVolume = 1f;
                    stereo.RightVolume = 1f;
                    break;
            }
            return stereo;
        }

        /// <summary>
        /// Legacy overload — creates a Sine tone at default gain for backward compatibility.
        /// </summary>
        public static ISampleProvider CreateTone(float frequency, AudioChannel channel)
        {
            return CreateTone(frequency, channel, SoundType.Sine, 0.5f);
        }

        public static float[] ExtractWaveformSamples(byte[] buffer, int bytesRecorded, int maxSamples)
        {
            int totalSamples = bytesRecorded / 2;
            int step = Math.Max(1, totalSamples / maxSamples);
            int outputCount = Math.Min(maxSamples, totalSamples);
            float[] result = new float[outputCount];

            for (int i = 0; i < outputCount; i++)
            {
                int idx = i * step * 2;
                if (idx + 1 < bytesRecorded)
                {
                    short sample = (short)(buffer[idx] | (buffer[idx + 1] << 8));
                    result[i] = sample / 32768f;
                }
            }
            return result;
        }
    }
}
