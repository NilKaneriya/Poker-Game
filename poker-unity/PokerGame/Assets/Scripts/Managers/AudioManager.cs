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
    /// On/off and volume come from SettingsManager (ApplySettings); this class does not save them.
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        public bool MusicOn { get; private set; } = true;
        public bool SoundOn { get; private set; } = true;
        public float MusicVolume { get; private set; } = 1f;
        public float SoundVolume { get; private set; } = 1f;

        private SoundLibrary library;
        private AudioSource[] voices;
        private int nextVoice;
        private AudioSource musicA, musicB; // two sources so tracks can crossfade
        private SoundId? currentMusic;
        private float currentTrackVolume;   // the track's own volume from the SoundLibrary

        private void Awake()
        {
            Instance = this;
            library = SoundLibrary.Load();

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
            voice.PlayOneShot(entry.clips[Random.Range(0, entry.clips.Length)], entry.volume * SoundVolume);
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
            currentTrackVolume = entry.volume;
            musicA.DOKill();
            musicA.clip = entry.clips[0];
            musicA.volume = 0f;
            musicA.Play();
            musicA.DOFade(currentTrackVolume * MusicVolume, AudioConfig.MusicFadeSeconds);
        }

        // ---------- Settings (from SettingsManager) ----------

        public void ApplySettings(bool musicOn, bool soundOn, float musicVolume, float soundVolume)
        {
            bool musicWasOn = MusicOn;
            MusicOn = musicOn;
            SoundOn = soundOn;
            MusicVolume = Mathf.Clamp01(musicVolume);
            SoundVolume = Mathf.Clamp01(soundVolume);

            if (!SoundOn) foreach (AudioSource v in voices) v.Stop();

            if (MusicOn && !musicWasOn && currentMusic.HasValue) CrossfadeTo(currentMusic.Value);
            else if (!MusicOn && musicWasOn)
            {
                musicA.DOKill();
                musicA.DOFade(0f, 0.3f).OnComplete(musicA.Stop);
            }
            else if (MusicOn && musicA.isPlaying)
            {
                musicA.DOKill();
                musicA.volume = currentTrackVolume * MusicVolume; // slider moves: follow it at once
            }
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
