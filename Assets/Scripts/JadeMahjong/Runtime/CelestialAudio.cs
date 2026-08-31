using System;
using UnityEngine;

namespace JadeMahjong.Runtime
{
    public sealed class CelestialAudio : MonoBehaviour
    {
        private const int SampleRate = 44100;
        private AudioSource _music;
        private AudioSource _effects;
        private AudioClip _pair;
        private AudioClip _error;
        private AudioClip _gong;
        private AudioClip _victory;

        private void Awake()
        {
            _music = gameObject.AddComponent<AudioSource>();
            _effects = gameObject.AddComponent<AudioSource>();
            _music.loop = true;
            _music.volume = 0.34f;
            _effects.volume = 0.72f;
            _music.clip = CreateTheme();
            _pair = CreateChime(new[] { 0, 7, 12 }, 0.34f, 0.33f);
            _error = CreateChime(new[] { 0, -2 }, 0.28f, 0.2f);
            _gong = CreateGong(1.3f);
            _victory = CreateChime(new[] { 0, 4, 7, 12, 16, 19 }, 1.6f, 0.48f);
            _music.Play();
        }

        public void Pair()
        {
            _effects.PlayOneShot(_pair);
        }

        public void Error()
        {
            _effects.PlayOneShot(_error);
        }

        public void Gong()
        {
            _effects.PlayOneShot(_gong);
        }

        public void Victory()
        {
            _effects.PlayOneShot(_victory);
        }

        public void SetMuted(bool muted)
        {
            _music.mute = muted;
            _effects.mute = muted;
        }

        private static AudioClip CreateTheme()
        {
            const float duration = 24f;
            var samples = new float[Mathf.CeilToInt(duration * SampleRate)];
            var melody = new[]
            {
                4, 7, 9, 12, 9, 7, 2, 4, 7, 9, 9, 12, 14, 16, 14, 12,
                7, 9, 12, 14, 16, 14, 12, 9, 7, 4, 7, 9, 7, 4, 2, 0
            };
            var bass = new[] { 0, 7, 9, 4, 5, 0, 2, 7 };
            var beatLength = duration / melody.Length;

            for (var note = 0; note < melody.Length; note++)
            {
                var start = Mathf.RoundToInt(note * beatLength * SampleRate);
                var length = Mathf.RoundToInt(beatLength * SampleRate * 0.9f);
                AddPluck(samples, start, length, Frequency(60 + melody[note]), 0.22f);
                if (note % 2 == 0)
                    AddBell(samples, start, length * 2, Frequency(72 + melody[note]), 0.08f);
            }

            for (var bar = 0; bar < bass.Length; bar++)
            {
                var start = Mathf.RoundToInt(bar * duration / bass.Length * SampleRate);
                var length = Mathf.RoundToInt(duration / bass.Length * SampleRate);
                AddPad(samples, start, length, Frequency(36 + bass[bar]), 0.11f);
                AddGong(samples, start, Mathf.Min(length, SampleRate), 0.055f);
            }

            for (var beat = 0; beat < melody.Length; beat += 2)
            {
                var start = Mathf.RoundToInt(beat * beatLength * SampleRate);
                AddNoise(samples, start, Mathf.RoundToInt(0.07f * SampleRate), 0.028f);
            }

            Normalize(samples, 0.82f);
            var clip = AudioClip.Create("Corte de Jade", samples.Length, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static AudioClip CreateChime(int[] intervals, float duration, float volume)
        {
            var samples = new float[Mathf.CeilToInt(duration * SampleRate)];
            for (var index = 0; index < intervals.Length; index++)
            {
                var start = Mathf.RoundToInt(index * duration / (intervals.Length + 1) * SampleRate);
                AddBell(samples, start, samples.Length - start, Frequency(72 + intervals[index]), volume);
            }
            Normalize(samples, 0.9f);
            var clip = AudioClip.Create("Jade Chime", samples.Length, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static AudioClip CreateGong(float duration)
        {
            var samples = new float[Mathf.CeilToInt(duration * SampleRate)];
            AddGong(samples, 0, samples.Length, 0.65f);
            Normalize(samples, 0.9f);
            var clip = AudioClip.Create("Palace Gong", samples.Length, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static void AddPluck(float[] target, int start, int length, float frequency, float volume)
        {
            for (var index = 0; index < length && start + index < target.Length; index++)
            {
                var time = index / (float)SampleRate;
                var envelope = Mathf.Exp(-time * 5.5f) * Mathf.Min(1f, time * 80f);
                var wave = Mathf.Sin(2f * Mathf.PI * frequency * time) +
                           0.38f * Mathf.Sin(2f * Mathf.PI * frequency * 2.01f * time);
                target[start + index] += wave * envelope * volume;
            }
        }

        private static void AddBell(float[] target, int start, int length, float frequency, float volume)
        {
            for (var index = 0; index < length && start + index < target.Length; index++)
            {
                var time = index / (float)SampleRate;
                var envelope = Mathf.Exp(-time * 4.2f);
                var wave = Mathf.Sin(2f * Mathf.PI * frequency * time) +
                           0.5f * Mathf.Sin(2f * Mathf.PI * frequency * 2.71f * time) +
                           0.22f * Mathf.Sin(2f * Mathf.PI * frequency * 4.13f * time);
                target[start + index] += wave * envelope * volume;
            }
        }

        private static void AddPad(float[] target, int start, int length, float frequency, float volume)
        {
            for (var index = 0; index < length && start + index < target.Length; index++)
            {
                var time = index / (float)SampleRate;
                var phase = index / (float)Mathf.Max(1, length);
                var envelope = Mathf.Sin(Mathf.PI * Mathf.Clamp01(phase));
                var wave = Mathf.Sin(2f * Mathf.PI * frequency * time) +
                           0.45f * Mathf.Sin(2f * Mathf.PI * frequency * 1.5f * time);
                target[start + index] += wave * envelope * volume;
            }
        }

        private static void AddGong(float[] target, int start, int length, float volume)
        {
            for (var index = 0; index < length && start + index < target.Length; index++)
            {
                var time = index / (float)SampleRate;
                var envelope = Mathf.Exp(-time * 2.7f);
                var wave = Mathf.Sin(2f * Mathf.PI * 110f * time) +
                           0.7f * Mathf.Sin(2f * Mathf.PI * 173f * time) +
                           0.35f * Mathf.Sin(2f * Mathf.PI * 281f * time);
                target[start + index] += wave * envelope * volume;
            }
        }

        private static void AddNoise(float[] target, int start, int length, float volume)
        {
            var state = 0xA341316Cu + (uint)start;
            for (var index = 0; index < length && start + index < target.Length; index++)
            {
                state ^= state << 13;
                state ^= state >> 17;
                state ^= state << 5;
                var noise = (state / (float)uint.MaxValue) * 2f - 1f;
                var envelope = 1f - index / (float)Mathf.Max(1, length);
                target[start + index] += noise * envelope * volume;
            }
        }

        private static float Frequency(int midi)
        {
            return 440f * Mathf.Pow(2f, (midi - 69) / 12f);
        }

        private static void Normalize(float[] samples, float ceiling)
        {
            var maximum = 0f;
            foreach (var sample in samples)
                maximum = Mathf.Max(maximum, Mathf.Abs(sample));
            if (maximum <= ceiling)
                return;
            var scale = ceiling / maximum;
            for (var index = 0; index < samples.Length; index++)
                samples[index] *= scale;
        }
    }
}
