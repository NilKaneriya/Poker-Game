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

        private static SettingsManager Settings => SettingsManager.Instance;

        private void Awake() => AppServices.EnsureExists();

        private void Start()
        {
            GetComponent<Button>().onClick.AddListener(Toggle);
            Settings.Changed += Refresh;
            Refresh(Settings.Current);
        }

        private void OnDestroy()
        {
            if (Settings != null) Settings.Changed -= Refresh;
        }

        private bool IsOn(Network.DataModel.UserSettings s) => setting switch
        {
            Setting.Music => s.musicOn,
            Setting.Sound => s.soundOn,
            _ => s.breakReminders,
        };

        private void Toggle()
        {
            Settings.Change(s =>
            {
                switch (setting)
                {
                    case Setting.Music: s.musicOn = !s.musicOn; break;
                    case Setting.Sound: s.soundOn = !s.soundOn; break;
                    default: s.breakReminders = !s.breakReminders; break;
                }
            });
        }

        private void Refresh(Network.DataModel.UserSettings s)
        {
            string format = setting switch
            {
                Setting.Music => SettingTexts.Music,
                Setting.Sound => SettingTexts.Sound,
                _ => SettingTexts.BreakReminders,
            };
            bool on = IsOn(s);
            label.text = string.Format(format, on ? SettingTexts.On : SettingTexts.Off);
            group.alpha = on ? 1f : offAlpha;
        }
    }
}
