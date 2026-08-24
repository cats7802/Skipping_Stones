using System;
using UnityEngine;

public static class SoundSynthesizer
{
    public const int SampleRate = 44100;

    public static float[] GenerateWhoosh(float duration)
    {
        int numSamples = (int)(SampleRate * duration);
        float[] samples = new float[numSamples];
        System.Random rnd = new System.Random(42);

        for (int i = 0; i < numSamples; i++)
        {
            float t = (float)i / numSamples;
            float envelope = Mathf.Sin(t * Mathf.PI);
            envelope = Mathf.Pow(envelope, 2f);

            float whiteNoise = (float)(rnd.NextDouble() * 2.0 - 1.0);
            float freq = Mathf.Lerp(180f, 650f, Mathf.Sin(t * Mathf.PI));
            float tonal = Mathf.Sin(2f * Mathf.PI * freq * (i / (float)SampleRate));

            samples[i] = (whiteNoise * 0.65f + tonal * 0.35f) * envelope * 0.8f;
        }
        return samples;
    }

    public static float[] GenerateWaterBounce(float duration)
    {
        int numSamples = (int)(SampleRate * duration);
        float[] samples = new float[numSamples];

        for (int i = 0; i < numSamples; i++)
        {
            float t = (float)i / numSamples;
            float envelope = Mathf.Exp(-t * 12f);

            float freq = Mathf.Lerp(650f, 180f, Mathf.Pow(t, 0.4f));
            float phase = 2f * Mathf.PI * freq * (i / (float)SampleRate);
            float wave = Mathf.Sin(phase) + 0.3f * Mathf.Sin(phase * 2f);

            float bubbleMod = 1f + 0.2f * Mathf.Sin(2f * Mathf.PI * 45f * t);

            samples[i] = wave * bubbleMod * envelope * 0.85f;
        }
        return samples;
    }

    public static float[] GenerateGoodBounce(float duration)
    {
        int numSamples = (int)(SampleRate * duration);
        float[] samples = new float[numSamples];

        for (int i = 0; i < numSamples; i++)
        {
            float t = (float)i / numSamples;
            float envelope = Mathf.Exp(-t * 9f);

            float freq = Mathf.Lerp(300f, 750f, Mathf.Pow(t, 0.3f));
            float phase = 2f * Mathf.PI * freq * (i / (float)SampleRate);
            float mainWave = Mathf.Sin(phase);
            float harmonic = 0.4f * Mathf.Sin(phase * 1.5f);

            samples[i] = (mainWave + harmonic) * envelope * 0.9f;
        }
        return samples;
    }

    public static float[] GeneratePerfectChime(float duration)
    {
        int numSamples = (int)(SampleRate * duration);
        float[] samples = new float[numSamples];

        float[] freqs = { 1046.5f, 1318.5f, 1567.9f, 2093.0f };
        float[] weights = { 0.4f, 0.3f, 0.25f, 0.15f };

        for (int i = 0; i < numSamples; i++)
        {
            float t = (float)i / numSamples;
            float envelope = Mathf.Exp(-t * 5.5f);

            float chord = 0f;
            for (int f = 0; f < freqs.Length; f++)
            {
                float phase = 2f * Mathf.PI * freqs[f] * (i / (float)SampleRate);
                chord += Mathf.Sin(phase) * weights[f];
            }

            float shimmer = 1f + 0.1f * Mathf.Sin(2f * Mathf.PI * 18f * t);

            samples[i] = chord * shimmer * envelope * 0.95f;
        }
        return samples;
    }

    public static float[] GenerateSkimSlide(float duration)
    {
        int numSamples = (int)(SampleRate * duration);
        float[] samples = new float[numSamples];
        System.Random rnd = new System.Random(99);

        for (int i = 0; i < numSamples; i++)
        {
            float t = (float)i / numSamples;
            float envelope = Mathf.Sin(t * Mathf.PI) * Mathf.Exp(-t * 2.5f);

            float flutter = (Mathf.Sin(2f * Mathf.PI * 28f * t) > 0f ? 1f : 0.2f);
            float noise = (float)(rnd.NextDouble() * 2.0 - 1.0);
            float tone = Mathf.Sin(2f * Mathf.PI * 480f * (i / (float)SampleRate));

            samples[i] = (noise * 0.6f + tone * 0.4f) * flutter * envelope * 0.75f;
        }
        return samples;
    }

    public static float[] GenerateBoostPad(float duration)
    {
        int numSamples = (int)(SampleRate * duration);
        float[] samples = new float[numSamples];

        for (int i = 0; i < numSamples; i++)
        {
            float t = (float)i / numSamples;
            float envelope = Mathf.Sin(t * Mathf.PI * 0.5f) * Mathf.Exp(-t * 3f);

            float freq = Mathf.Lerp(350f, 1750f, Mathf.Pow(t, 0.7f));
            float phase = 2f * Mathf.PI * freq * (i / (float)SampleRate);
            float saw = (phase % (2f * Mathf.PI)) / Mathf.PI - 1f;
            float sine = Mathf.Sin(phase);

            samples[i] = (sine * 0.7f + saw * 0.3f) * envelope * 0.85f;
        }
        return samples;
    }

    public static float[] GenerateCoinJingle(float duration)
    {
        int numSamples = (int)(SampleRate * duration);
        float[] samples = new float[numSamples];

        float f1 = 987.77f;
        float f2 = 1318.51f;
        float splitPoint = 0.15f;

        for (int i = 0; i < numSamples; i++)
        {
            float t = (float)i / numSamples;
            float freq = (t < splitPoint) ? f1 : f2;
            float tLocal = (t < splitPoint) ? (t / splitPoint) : ((t - splitPoint) / (1f - splitPoint));
            float envelope = Mathf.Exp(-tLocal * 6f);

            float phase = 2f * Mathf.PI * freq * (i / (float)SampleRate);
            float sine = Mathf.Sin(phase);
            float harmonic = 0.3f * Mathf.Sin(phase * 2f);

            samples[i] = (sine + harmonic) * envelope * 0.8f;
        }
        return samples;
    }

    public static float[] GenerateStoneSink(float duration)
    {
        int numSamples = (int)(SampleRate * duration);
        float[] samples = new float[numSamples];

        for (int i = 0; i < numSamples; i++)
        {
            float t = (float)i / numSamples;
            float envelope = Mathf.Exp(-t * 4.5f);

            float freq = Mathf.Lerp(260f, 70f, Mathf.Pow(t, 0.5f));
            float phase = 2f * Mathf.PI * freq * (i / (float)SampleRate);
            float wave = Mathf.Sin(phase) + 0.4f * Mathf.Sin(phase * 0.5f);

            float bubble = 1f + 0.3f * Mathf.Sin(2f * Mathf.PI * 14f * t);

            samples[i] = wave * bubble * envelope * 0.85f;
        }
        return samples;
    }

    public static float[] GenerateButtonClick(float duration)
    {
        int numSamples = (int)(SampleRate * duration);
        float[] samples = new float[numSamples];

        for (int i = 0; i < numSamples; i++)
        {
            float t = (float)i / numSamples;
            float envelope = Mathf.Exp(-t * 45f);

            float freq = Mathf.Lerp(1200f, 400f, t);
            float wave = Mathf.Sin(2f * Mathf.PI * freq * (i / (float)SampleRate));

            samples[i] = wave * envelope * 0.7f;
        }
        return samples;
    }
}
