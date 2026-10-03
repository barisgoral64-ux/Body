using System;
using System.Collections.Generic;
using MinikDuello.Core;
using MinikDuello.Services;
using MinikDuello.Services.Managers;
using MinikDuello.Services.Save;
using UnityEngine;

namespace MinikDuello.Infra
{
    public enum Sfx
    {
        Tap,
        Correct,
        Oops,
        Star,
        Win,
        Pop,
        Whoosh,
        Tick,
        Go
    }

    /// <summary>
    /// Ses yöneticisi. Efektler kod ile üretilen yumuşak, korkutmayan tonlardır (sanatçı sesleri gelene kadar yer tutucu).
    /// Ebeveyn "ses kapalı" derse ve yerel ses seviyesi 0 ise hiçbir şey çalmaz.
    /// </summary>
    public sealed class AudioManager : MonoBehaviour, IVoiceService
    {
        private const int SampleRate = 22050;
        private const float AttackSeconds = 0.01f;
        private const float MasterGain = 0.5f;
        private const float MusicGain = 0.35f;
        private const string VoiceFolder = "Voice/";

        private readonly Dictionary<Sfx, AudioClip> clips = new Dictionary<Sfx, AudioClip>();
        private AudioSource sfxSource;
        private AudioSource voiceSource;
        private AudioSource musicSource;
        private SaveManager save;
        private ParentControlManager parent;

        public void Initialize(SaveManager saveManager, ParentControlManager parentManager)
        {
            save = saveManager;
            parent = parentManager;
            sfxSource = gameObject.AddComponent<AudioSource>();
            voiceSource = gameObject.AddComponent<AudioSource>();
            musicSource = gameObject.AddComponent<AudioSource>();
            sfxSource.playOnAwake = voiceSource.playOnAwake = musicSource.playOnAwake = false;

            clips[Sfx.Tap] = Notes("tap", 0.08f, 660f);
            clips[Sfx.Pop] = Notes("pop", 0.1f, 880f);
            clips[Sfx.Correct] = Notes("correct", 0.12f, 523.25f, 659.25f, 783.99f);
            clips[Sfx.Star] = Notes("star", 0.1f, 783.99f, 987.77f, 1174.66f, 1567.98f);
            clips[Sfx.Win] = Notes("win", 0.14f, 523.25f, 659.25f, 783.99f, 1046.5f);
            // Yanlış denemede keskin "hata" sesi yok: yumuşak, aşağı inen iki nota.
            clips[Sfx.Oops] = Notes("oops", 0.14f, 392f, 349.23f);
            clips[Sfx.Whoosh] = Notes("whoosh", 0.06f, 440f, 523.25f);
            clips[Sfx.Tick] = Notes("tick", 0.1f, 587.33f);
            clips[Sfx.Go] = Notes("go", 0.18f, 783.99f, 1046.5f);

            musicSource.clip = BuildMusic();
            musicSource.loop = true;
            RefreshVolumes();
        }

        public void RefreshVolumes()
        {
            if (musicSource == null) return;
            musicSource.volume = Enabled ? MusicVolume * MusicGain : 0f;
            if (Enabled && MusicVolume > 0f && !musicSource.isPlaying) musicSource.Play();
            if ((!Enabled || MusicVolume <= 0f) && musicSource.isPlaying) musicSource.Pause();
        }

        public void Play(Sfx sfx)
        {
            if (sfxSource == null || !Enabled || !clips.TryGetValue(sfx, out AudioClip clip)) return;
            sfxSource.PlayOneShot(clip, SfxVolume * MasterGain);
        }

        /// <summary>Sesli yönerge. Resources/Voice/{anahtar} klibi varsa çalar; yoksa sessiz geçer (yer tutucu).</summary>
        public void Speak(string clipKey)
        {
            if (voiceSource == null || !Enabled || !save.Data.VoiceEnabled || string.IsNullOrEmpty(clipKey)) return;
            var clip = Resources.Load<AudioClip>(VoiceFolder + clipKey);
            if (clip == null) return;
            voiceSource.Stop();
            voiceSource.PlayOneShot(clip, SfxVolume);
        }

        private bool Enabled => parent == null || parent.Settings.SoundEnabled;
        private float SfxVolume => save.Data.SfxVolume / 100f;
        private float MusicVolume => save.Data.MusicVolume / 100f;

        private static AudioClip Notes(string name, float noteSeconds, params float[] frequencies)
        {
            int perNote = Mathf.RoundToInt(noteSeconds * SampleRate);
            var data = new float[perNote * frequencies.Length];
            for (int n = 0; n < frequencies.Length; n++)
            {
                for (int i = 0; i < perNote; i++)
                {
                    float t = i / (float)SampleRate;
                    float attack = Mathf.Clamp01(t / AttackSeconds);
                    float decay = Mathf.Exp(-6f * t / noteSeconds);
                    float wave = Mathf.Sin(2f * Mathf.PI * frequencies[n] * t) * 0.8f + Mathf.Sin(4f * Mathf.PI * frequencies[n] * t) * 0.2f;
                    data[n * perNote + i] = wave * attack * decay * 0.6f;
                }
            }
            return Create(name, data);
        }

        /// <summary>Çok yumuşak, döngüsel pentatonik melodi (yer tutucu müzik).</summary>
        private static AudioClip BuildMusic()
        {
            float[] scale = { 261.63f, 293.66f, 329.63f, 392f, 440f, 523.25f };
            int[] melody = { 0, 2, 4, 5, 4, 2, 3, 1, 0, 2, 3, 4, 3, 2, 1, 0 };
            const float noteSeconds = 0.5f;
            int perNote = Mathf.RoundToInt(noteSeconds * SampleRate);
            var data = new float[perNote * melody.Length];
            for (int n = 0; n < melody.Length; n++)
            {
                float f = scale[melody[n]];
                for (int i = 0; i < perNote; i++)
                {
                    float t = i / (float)SampleRate;
                    float env = Mathf.Clamp01(t / 0.03f) * Mathf.Exp(-3f * t / noteSeconds);
                    data[n * perNote + i] = Mathf.Sin(2f * Mathf.PI * f * t) * env * 0.4f;
                }
            }
            return Create("music", data);
        }

        private static AudioClip Create(string name, float[] data)
        {
            AudioClip clip = AudioClip.Create(name, data.Length, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
