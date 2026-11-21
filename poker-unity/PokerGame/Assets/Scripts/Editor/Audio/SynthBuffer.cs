using System;

namespace Poker.EditorTools.Audio
{
    /// <summary>
    /// A tiny synthesizer used to make placeholder sounds: decaying tones (with harmonics,
    /// pitch slides, vibrato and tremolo) and filtered noise bursts, mixed into one buffer.
    /// A looping buffer wraps sounds that run past the end back to the start, so music
    /// loops without a gap.
    /// </summary>
    public class SynthBuffer
    {
        public readonly int SampleRate;
        public readonly float[] Samples;
        private readonly bool loop;
        private const float ReleaseSeconds = 0.006f; // short fade at the end of every sound: no clicks

        public SynthBuffer(float seconds, int sampleRate, bool loop = false)
        {
            SampleRate = sampleRate;
            Samples = new float[Math.Max(1, (int)(seconds * sampleRate))];
            this.loop = loop;
        }

        /// <param name="harmonics">Extra partials: harmonics[0] = 2x frequency, [1] = 3x...</param>
        /// <param name="decay">Exponential decay rate per second (bigger = shorter).</param>
        public void Tone(float start, float duration, float freq, float amp, float decay,
            float freqEnd = -1f, float[] harmonics = null, float attack = 0.003f,
            float vibratoHz = 0f, float vibratoDepth = 0f, float tremoloHz = 0f, float tremoloDepth = 0f)
        {
            int first = (int)(start * SampleRate);
            int length = (int)(duration * SampleRate);
            double phase = 0;

            for (int i = 0; i < length; i++)
            {
                float t = (float)i / SampleRate;
                float f = freqEnd > 0f ? Lerp(freq, freqEnd, (float)i / length) : freq;
                if (vibratoHz > 0f) f *= 1f + vibratoDepth * (float)Math.Sin(2 * Math.PI * vibratoHz * t);
                phase += 2 * Math.PI * f / SampleRate;

                float v = (float)Math.Sin(phase);
                if (harmonics != null)
                    for (int h = 0; h < harmonics.Length; h++) v += harmonics[h] * (float)Math.Sin((h + 2) * phase);

                float trem = tremoloHz > 0f ? 1f - tremoloDepth * 0.5f * (1f + (float)Math.Sin(2 * Math.PI * tremoloHz * t)) : 1f;
                Add(first + i, amp * Envelope(t, duration, attack, decay) * trem * v);
            }
        }

        /// <summary>Noise through a low-pass that slides from lowpassStart to lowpassEnd Hz, optionally high-passed.</summary>
        public void Noise(float start, float duration, float amp, float decay, float lowpassStart, float lowpassEnd,
            float highpass, Random rng, float attack = 0.002f)
        {
            int first = (int)(start * SampleRate);
            int length = (int)(duration * SampleRate);
            float lp = 0f, hpState = 0f;
            float hpAlpha = highpass > 0f ? Alpha(highpass) : 0f;

            for (int i = 0; i < length; i++)
            {
                float t = (float)i / SampleRate;
                float x = (float)(rng.NextDouble() * 2 - 1);
                lp += Alpha(Lerp(lowpassStart, lowpassEnd, (float)i / length)) * (x - lp);
                hpState += hpAlpha * (lp - hpState);
                float y = highpass > 0f ? lp - hpState : lp;
                Add(first + i, amp * Envelope(t, duration, attack, decay) * y);
            }
        }

        /// <summary>Scale so the loudest sample hits 'peak'.</summary>
        public float[] Normalized(float peak)
        {
            float max = 0f;
            foreach (float s in Samples) max = Math.Max(max, Math.Abs(s));
            if (max > 0f) for (int i = 0; i < Samples.Length; i++) Samples[i] *= peak / max;
            return Samples;
        }

        private float Envelope(float t, float duration, float attack, float decay)
        {
            float env = (t < attack ? t / attack : 1f) * (float)Math.Exp(-decay * t);
            float untilEnd = duration - t;
            return untilEnd < ReleaseSeconds ? env * untilEnd / ReleaseSeconds : env;
        }

        private void Add(int index, float value)
        {
            int n = Samples.Length;
            if (loop) index = ((index % n) + n) % n;
            else if (index < 0 || index >= n) return;
            Samples[index] += value;
        }

        private float Alpha(float cutoffHz) => 1f - (float)Math.Exp(-2 * Math.PI * cutoffHz / SampleRate);

        private static float Lerp(float a, float b, float t) => a + (b - a) * t;
    }
}
