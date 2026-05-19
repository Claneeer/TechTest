using System;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace TechTest.Helpers
{
    public enum AudioChannel { Left, Right, Both }

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

        public static ISampleProvider CreateTone(float frequency, AudioChannel channel)
        {
            var sine = new SignalGenerator(44100, 1)
            {
                Frequency = frequency,
                Type = SignalGeneratorType.Sin,
                Gain = 0.5
            };

            var stereo = new MonoToStereoSampleProvider(sine);
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
