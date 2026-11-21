using System;
using DG.Tweening;
using UnityEngine;
using static Poker.Core.Constants;
using static Poker.Network.DataModel;
using Random = UnityEngine.Random;

namespace Poker.Audio
{
    /// <summary>
    /// Plays sound effects and music. Created once by AppServices and kept across scenes.
    ///   Play(id)       one-shot effect from a pool of voices (optionally delayed)
    ///   PlayMusic(id)  looping track with a crossfade when the scene changes
    /// Music and sound effects can be switched off separately; the choice is saved.
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        /// <summary>Music or sound switched on/off.</summary>
        public event Action SettingsChanged;

        public bool MusicOn { get; private set; }
        public bool SoundOn { get; private set; }

        private SoundLibrary library;
        private AudioSource[] voices;
        private int nextVoice;
        private AudioSource musicA, musicB; // two sources so tracks can crossfade
        private SoundId? currentMusic;

        private void Awake()
        {
            Instance = this;
            library = SoundLibrary.Load();
            MusicOn = PlayerPrefs.GetInt(PrefsKeys.MusicOn, 1) == 1;
            SoundOn = PlayerPrefs.GetInt(PrefsKeys.SoundOn, 1) == 1;

            voices = new AudioSource[AudioConfig.EffectVoices];
            for (int i = 0; i < voices.Length; i++) voices[i] = NewSource(loop: false);
            musicA = NewSource(loop: true);
            musicB = NewSource(loop: true);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            DOTween.Kill(this);
        }

        // ---------- Sound effects ----------

        public void Play(SoundId id, float delay = 0f)
        {
            if (!SoundOn) return;
            if (delay <= 0f) PlayNow(id);
            else DOVirtual.DelayedCall(delay, () => PlayNow(id)).SetTarget(this);
        }

        private void PlayNow(SoundId id)
        {
            SoundEntry entry = library.Get(id);
            if (entry == null || !SoundOn) return;

            AudioSource voice = FreeVoice();
            voice.pitch = 1f + Random.Range(-entry.pitchJitter, entry.pitchJitter);
            voice.PlayOneShot(entry.clips[Random.Range(0, entry.clips.Length)], entry.volume);
        }

        /// <summary>A voice that is silent, or the next one in turn if all are busy.</summary>
        private AudioSource FreeVoice()
        {
            for (int i = 0; i < voices.Length; i++)
            {
                AudioSource v = voices[(nextVoice + i) % voices.Length];
                if (!v.isPlaying) { nextVoice = (nextVoice + i + 1) % voices.Length; return v; }
            }
            AudioSource oldest = voices[nextVoice];
            nextVoice = (nextVoice + 1) % voices.Length;
            oldest.Stop();
            return oldest;
        }

        // ---------- Music ----------

        /// <summary>Switch to this track (crossfade). Same track again: keeps playing.</summary>
        public void PlayMusic(SoundId id)
        {
            if (currentMusic == id && (musicA.isPlaying || !MusicOn)) return;
            currentMusic = id;
            if (MusicOn) CrossfadeTo(id);
        }

        private void CrossfadeTo(SoundId id)
        {
            SoundEntry entry = library.Get(id);
            AudioSource fadingOut = musicA;
            (musicA, musicB) = (musicB, musicA); // musicA is always the current track

            fadingOut.DOKill();
            fadingOut.DOFade(0f, AudioConfig.MusicFadeSeconds).OnComplete(fadingOut.Stop);

            if (entry == null) return;
            musicA.DOKill();
            musicA.clip = entry.clips[0];
            musicA.volume = 0f;
            musicA.Play();
            musicA.DOFade(entry.volume, AudioConfig.MusicFadeSeconds);
        }

        // ---------- Settings ----------

        public void SetMusicOn(bool on)
        {
            if (MusicOn == on) return;
            MusicOn = on;
            Save(PrefsKeys.MusicOn, on);
            if (on && currentMusic.HasValue) CrossfadeTo(currentMusic.Value);
            if (!on)
            {
                musicA.DOKill();
                musicA.DOFade(0f, 0.3f).OnComplete(musicA.Stop);
            }
            SettingsChanged?.Invoke();
        }

        public void SetSoundOn(bool on)
        {
            if (SoundOn == on) return;
            SoundOn = on;
            Save(PrefsKeys.SoundOn, on);
            if (!on) foreach (AudioSource v in voices) v.Stop();
            SettingsChanged?.Invoke();
        }

        private static void Save(string key, bool on)
        {
            PlayerPrefs.SetInt(key, on ? 1 : 0);
            PlayerPrefs.Save();
        }

        private AudioSource NewSource(bool loop)
        {
            var source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = loop;
            source.spatialBlend = 0f; // 2D
            return source;
        }
    }
}
