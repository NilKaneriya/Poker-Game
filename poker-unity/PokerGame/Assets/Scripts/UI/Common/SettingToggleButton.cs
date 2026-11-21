using Poker.Audio;
using Poker.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static Poker.Core.Constants;

namespace Poker.UI
{
    /// <summary>
    /// Button that switches one player setting on/off: music, sound effects or break
    /// reminders. Faded when off; a label with {0} also shows "On" / "Off".
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class SettingToggleButton : MonoBehaviour
    {
        public enum Setting { Music, Sound, BreakReminders }

        [SerializeField] private Setting setting;
        [SerializeField] private TMP_Text label;
        [SerializeField] private CanvasGroup group;
        [SerializeField] private float offAlpha = 0.45f;

        private static AudioManager Audio => AudioManager.Instance;
        private static SessionClock Clock => SessionClock.Instance;

        private void Awake() => AppServices.EnsureExists();

        private void Start()
        {
            GetComponent<Button>().onClick.AddListener(Toggle);
            Audio.SettingsChanged += Refresh;
            Clock.SettingsChanged += Refresh;
            Refresh();
        }

        private void OnDestroy()
        {
            if (Audio != null) Audio.SettingsChanged -= Refresh;
            if (Clock != null) Clock.SettingsChanged -= Refresh;
        }

        private bool IsOn => setting switch
        {
            Setting.Music => Audio.MusicOn,
            Setting.Sound => Audio.SoundOn,
            _ => Clock.RemindersOn,
        };

        private void Toggle()
        {
            switch (setting)
            {
                case Setting.Music: Audio.SetMusicOn(!Audio.MusicOn); break;
                case Setting.Sound: Audio.SetSoundOn(!Audio.SoundOn); break;
                default: Clock.SetRemindersOn(!Clock.RemindersOn); break;
            }
        }

        private void Refresh()
        {
            string format = setting switch
            {
                Setting.Music => SettingTexts.Music,
                Setting.Sound => SettingTexts.Sound,
                _ => SettingTexts.BreakReminders,
            };
            label.text = string.Format(format, IsOn ? SettingTexts.On : SettingTexts.Off);
            group.alpha = IsOn ? 1f : offAlpha;
        }
    }
}
