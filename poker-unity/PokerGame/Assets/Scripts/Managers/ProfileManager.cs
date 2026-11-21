using System;
using System.Collections;
using Newtonsoft.Json;
using Poker.Core;
using UnityEngine;
using static Poker.Core.Constants;
using static Poker.Network.DataModel;

namespace Poker.Network
{
    /// <summary>
    /// The logged-in player's profile (display name, avatar, bio) and the link between
    /// their settings and their account (saved by the server in MongoDB):
    ///   - after every login: load the profile; account settings replace this device's
    ///     (or, the first time, this device's settings are uploaded)
    ///   - the player changes a setting: saved to the account a moment later
    ///   - Save(): edit the profile
    /// Created once by AppServices.
    /// </summary>
    public class ProfileManager : MonoBehaviour
    {
        public static ProfileManager Instance { get; private set; }

        public event Action<UserProfile> ProfileChanged;

        /// <summary>Null until loaded after login.</summary>
        public UserProfile Current { get; private set; }

        private ServerConfig config;
        private Coroutine pendingSettingsSave;

        private static AuthManager Auth => AuthManager.Instance;

        private void Awake()
        {
            Instance = this;
            config = ServerConfig.Load();
        }

        private void Start()
        {
            // Load the profile whenever a session starts (login, auto-login, reconnect).
            NetworkManager.Instance.SessionStarted += _ => Refresh();
            SettingsManager.Instance.ChangedByPlayer += QueueSettingsSave;
            if (Auth.IsLoggedIn) Refresh();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>Load the profile from the server.</summary>
        public void Refresh()
        {
            if (!Auth.IsLoggedIn) return;
            StartCoroutine(ApiClient.Get<UserProfile>(config.Url(ApiRoutes.MyProfile), Auth.Token, profile =>
            {
                SetCurrent(profile);
                if (profile.settingsSaved) SettingsManager.Instance.ApplyFromAccount(profile.settings);
                else SaveSettingsNow(SettingsManager.Instance.Current); // first login: keep this device's choices
            }, failure => Debug.LogWarning($"[Profile] Could not load the profile: {failure.Message}")));
        }

        /// <summary>Save a profile edit. Null fields in the update stay as they are.</summary>
        public void Save(ProfileUpdate update, Action<UserProfile> onSaved, Action<string> onError)
        {
            if (!Auth.IsLoggedIn) { onError?.Invoke("Log in to edit your profile."); return; }
            StartCoroutine(ApiClient.Put<UserProfile>(config.Url(ApiRoutes.Profile), WithoutNulls(update), Auth.Token, profile =>
            {
                SetCurrent(profile);
                onSaved?.Invoke(profile);
            }, failure => onError?.Invoke(failure.Message)));
        }

        private void SetCurrent(UserProfile profile)
        {
            Current = profile;
            ProfileChanged?.Invoke(profile);
        }

        // ---------- Settings -> account ----------

        /// <summary>Wait a moment before saving, so dragging a slider sends one request.</summary>
        private void QueueSettingsSave(UserSettings settings)
        {
            if (!Auth.IsLoggedIn) return; // kept on this device; uploaded at the next login
            if (pendingSettingsSave != null) StopCoroutine(pendingSettingsSave);
            pendingSettingsSave = StartCoroutine(SaveSettingsLater());
        }

        private IEnumerator SaveSettingsLater()
        {
            yield return new WaitForSecondsRealtime(SettingsSync.SaveDelaySeconds);
            pendingSettingsSave = null;
            SaveSettingsNow(SettingsManager.Instance.Current);
        }

        private void SaveSettingsNow(UserSettings settings)
        {
            StartCoroutine(ApiClient.Put<UserSettings>(config.Url(ApiRoutes.Settings), settings, Auth.Token, saved =>
            {
                if (Current != null) { Current.settings = saved; Current.settingsSaved = true; }
            }, failure => Debug.LogWarning($"[Profile] Settings not saved to the account: {failure.Message}")));
        }

        /// <summary>Send only the fields being changed ({ "bio": "..." }, not "displayName": null).</summary>
        private static object WithoutNulls(ProfileUpdate update) =>
            JsonConvert.DeserializeObject(JsonConvert.SerializeObject(update,
                new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore }));
    }
}
