using Poker.Core;
using Poker.Network;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static Poker.Core.Constants;
using static Poker.Network.DataModel;

namespace Poker.UI
{
    /// <summary>
    /// Settings window (Lobby and Game): music and sound with volume, table options, chat
    /// and break reminders. Every change goes through SettingsManager, which applies it at
    /// once, saves it on the device and (when logged in) to the player's account.
    /// </summary>
    public class SettingsPanel : ModalPanel
    {
        [Header("Audio")]
        [SerializeField] private Toggle musicToggle;
        [SerializeField] private Slider musicSlider;
        [SerializeField] private Toggle soundToggle;
        [SerializeField] private Slider soundSlider;

        [Header("Table")]
        [SerializeField] private Toggle handStrengthToggle;
        [SerializeField] private Toggle fourColorToggle;
        [SerializeField] private Toggle chatToggle;

        [Header("Responsible play")]
        [SerializeField] private Toggle breakRemindersToggle;

        [SerializeField] private TMP_Text syncHintText;

        private static SettingsManager Settings => SettingsManager.Instance;

        private void Awake() => AppServices.EnsureExists();

        protected override void Start()
        {
            base.Start();
            musicToggle.onValueChanged.AddListener(on => Settings.Change(s => s.musicOn = on));
            soundToggle.onValueChanged.AddListener(on => Settings.Change(s => s.soundOn = on));
            musicSlider.onValueChanged.AddListener(v => Settings.Change(s => s.musicVolume = v));
            soundSlider.onValueChanged.AddListener(v => Settings.Change(s => s.soundVolume = v));
            handStrengthToggle.onValueChanged.AddListener(on => Settings.Change(s => s.showHandStrength = on));
            fourColorToggle.onValueChanged.AddListener(on => Settings.Change(s => s.fourColorDeck = on));
            chatToggle.onValueChanged.AddListener(on => Settings.Change(s => s.chatEnabled = on));
            breakRemindersToggle.onValueChanged.AddListener(on => Settings.Change(s => s.breakReminders = on));

            Settings.Changed += Show; // e.g. account settings arrived, or a lobby toggle was used
            Show(Settings.Current);
        }

        private void OnDestroy()
        {
            if (Settings != null) Settings.Changed -= Show;
        }

        protected override void OnOpened()
        {
            Show(Settings.Current);
            bool loggedIn = AuthManager.Instance != null && AuthManager.Instance.IsLoggedIn;
            syncHintText.text = loggedIn ? SettingTexts.SyncedHint : SettingTexts.LocalHint;
        }

        /// <summary>Show values without firing the listeners (which would save them again).</summary>
        private void Show(UserSettings s)
        {
            musicToggle.SetIsOnWithoutNotify(s.musicOn);
            soundToggle.SetIsOnWithoutNotify(s.soundOn);
            musicSlider.SetValueWithoutNotify(s.musicVolume);
            soundSlider.SetValueWithoutNotify(s.soundVolume);
            musicSlider.interactable = s.musicOn;
            soundSlider.interactable = s.soundOn;
            handStrengthToggle.SetIsOnWithoutNotify(s.showHandStrength);
            fourColorToggle.SetIsOnWithoutNotify(s.fourColorDeck);
            chatToggle.SetIsOnWithoutNotify(s.chatEnabled);
            breakRemindersToggle.SetIsOnWithoutNotify(s.breakReminders);
        }
    }
}
