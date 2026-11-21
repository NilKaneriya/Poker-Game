using System;
using UnityEngine;
using static Poker.Core.Constants;

namespace Poker.Core
{
    /// <summary>
    /// Responsible play: measures how long this session has lasted and raises ReminderDue
    /// after Wellbeing.FirstReminderMinutes (then every RepeatReminderMinutes) so the game can
    /// suggest a break. Players can switch reminders off; the choice is saved.
    /// Created once by AppServices and kept across scenes.
    /// </summary>
    public class SessionClock : MonoBehaviour
    {
        public static SessionClock Instance { get; private set; }

        /// <summary>A break reminder should be shown now.</summary>
        public event Action ReminderDue;
        public event Action SettingsChanged;

        public bool RemindersOn { get; private set; }
        /// <summary>A reminder is waiting to be shown (e.g. it fell due during a scene change).</summary>
        public bool IsDue { get; private set; }
        public float PlayedMinutes => Time.realtimeSinceStartup / 60f;

        private float nextReminderAt; // seconds since startup

        private void Awake()
        {
            Instance = this;
            RemindersOn = PlayerPrefs.GetInt(PrefsKeys.BreakReminders, 1) == 1;
            nextReminderAt = Wellbeing.FirstReminderMinutes * 60f;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (!RemindersOn || IsDue || Time.realtimeSinceStartup < nextReminderAt) return;
            IsDue = true;
            ReminderDue?.Invoke();
        }

        /// <summary>The player saw the reminder and chose to keep playing.</summary>
        public void Acknowledge()
        {
            IsDue = false;
            nextReminderAt = Time.realtimeSinceStartup + Wellbeing.RepeatReminderMinutes * 60f;
        }

        public void SetRemindersOn(bool on)
        {
            RemindersOn = on;
            if (!on) IsDue = false;
            else nextReminderAt = Mathf.Max(nextReminderAt, Time.realtimeSinceStartup + Wellbeing.RepeatReminderMinutes * 60f);
            PlayerPrefs.SetInt(PrefsKeys.BreakReminders, on ? 1 : 0);
            PlayerPrefs.Save();
            SettingsChanged?.Invoke();
        }

        /// <summary>Editor/testing: make the reminder appear now.</summary>
        public void ShowReminderNow()
        {
            IsDue = true;
            ReminderDue?.Invoke();
        }
    }
}
