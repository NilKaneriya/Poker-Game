using System;
using Poker.Audio;
using UnityEngine;
using static Poker.Core.Constants;
using static Poker.Network.DataModel;

namespace Poker.Core
{
    /// <summary>
    /// The one place every player setting lives (audio, table options, chat, break reminders).
    ///   - loads them from this device at start (PlayerPrefs)
    ///   - applies them to the systems that use them (AudioManager, SessionClock)
    ///   - saves them on the device on every change
    /// ProfileManager also saves player changes to the account (MongoDB) and loads them on login,
    /// so settings follow the player to any device. Created once by AppServices.
    /// </summary>
    public class SettingsManager : MonoBehaviour
    {
        public static SettingsManager Instance { get; private set; }

        /// <summary>Any setting changed (by the player or from the server).</summary>
        public event Action<UserSettings> Changed;
        /// <summary>The player changed a setting: it should be saved to their account.</summary>
        public event Action<UserSettings> ChangedByPlayer;

        /// <summary>A copy: change settings through Change(), not by editing this.</summary>
        public UserSettings Current => current.Copy();

        private UserSettings current;

        private void Awake()
        {
            Instance = this;
            current = LoadLocal();
            Apply();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>The player changed something, e.g. Change(s => s.chatEnabled = false).</summary>
        public void Change(Action<UserSettings> edit)
        {
            var next = current.Copy();
            edit(next);
            next.musicVolume = Mathf.Clamp01(next.musicVolume);
            next.soundVolume = Mathf.Clamp01(next.soundVolume);
            current = next;
            SaveLocal(current);
            Apply();
            Changed?.Invoke(Current);
            ChangedByPlayer?.Invoke(Current);
        }

        /// <summary>Settings loaded from the player's account replace the ones on this device.</summary>
        public void ApplyFromAccount(UserSettings fromAccount)
        {
            if (fromAccount == null) return;
            current = fromAccount.Copy();
            SaveLocal(current);
            Apply();
            Changed?.Invoke(Current);
        }

        private void Apply()
        {
            if (AudioManager.Instance != null)
                AudioManager.Instance.ApplySettings(current.musicOn, current.soundOn, current.musicVolume, current.soundVolume);
            if (SessionClock.Instance != null) SessionClock.Instance.SetRemindersOn(current.breakReminders);
        }

        // ---------- This device ----------

        private static UserSettings LoadLocal()
        {
            var d = new UserSettings(); // defaults
            return new UserSettings
            {
                musicOn = GetBool(PrefsKeys.MusicOn, d.musicOn),
                soundOn = GetBool(PrefsKeys.SoundOn, d.soundOn),
                musicVolume = PlayerPrefs.GetFloat(PrefsKeys.MusicVolume, d.musicVolume),
                soundVolume = PlayerPrefs.GetFloat(PrefsKeys.SoundVolume, d.soundVolume),
                breakReminders = GetBool(PrefsKeys.BreakReminders, d.breakReminders),
                chatEnabled = GetBool(PrefsKeys.ChatEnabled, d.chatEnabled),
                showHandStrength = GetBool(PrefsKeys.ShowHandStrength, d.showHandStrength),
                fourColorDeck = GetBool(PrefsKeys.FourColorDeck, d.fourColorDeck),
            };
        }

        private static void SaveLocal(UserSettings s)
        {
            SetBool(PrefsKeys.MusicOn, s.musicOn);
            SetBool(PrefsKeys.SoundOn, s.soundOn);
            PlayerPrefs.SetFloat(PrefsKeys.MusicVolume, s.musicVolume);
            PlayerPrefs.SetFloat(PrefsKeys.SoundVolume, s.soundVolume);
            SetBool(PrefsKeys.BreakReminders, s.breakReminders);
            SetBool(PrefsKeys.ChatEnabled, s.chatEnabled);
            SetBool(PrefsKeys.ShowHandStrength, s.showHandStrength);
            SetBool(PrefsKeys.FourColorDeck, s.fourColorDeck);
            PlayerPrefs.Save();
        }

        private static bool GetBool(string key, bool fallback) => PlayerPrefs.GetInt(key, fallback ? 1 : 0) == 1;
        private static void SetBool(string key, bool value) => PlayerPrefs.SetInt(key, value ? 1 : 0);
    }
}
