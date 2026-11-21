using Poker.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static Poker.Core.Constants;

namespace Poker.UI
{
    /// <summary>
    /// Gentle "time for a break?" message after a long session (responsible play).
    /// No pressure: "Keep playing" closes it until the next reminder, "Don't remind me"
    /// turns reminders off (they can be turned back on in the Lobby).
    /// </summary>
    public class BreakReminderPanel : MonoBehaviour
    {
        [SerializeField] private CanvasGroup group;
        [SerializeField] private RectTransform card;
        [SerializeField] private TMP_Text bodyText;
        [SerializeField] private Button keepPlayingButton;
        [SerializeField] private Button turnOffButton;

        private static SessionClock Clock => SessionClock.Instance;

        private void Awake() => AppServices.EnsureExists();

        private void Start()
        {
            keepPlayingButton.onClick.AddListener(() => { Clock.Acknowledge(); Hide(); });
            turnOffButton.onClick.AddListener(() => { Clock.SetRemindersOn(false); Hide(); });
            Clock.ReminderDue += Show;
            if (Clock.IsDue) Show(); // fell due while another scene was loading
        }

        private void OnDestroy()
        {
            if (Clock != null) Clock.ReminderDue -= Show;
        }

        public void Show()
        {
            bodyText.text = string.Format(WellbeingTexts.Body, Duration(Clock.PlayedMinutes));
            UIAnim.FadeIn(group, 0.3f);
            UIAnim.PopIn(card, 0.35f);
        }

        private void Hide() => UIAnim.FadeOut(group, 0.25f);

        private static string Duration(float minutes)
        {
            int total = Mathf.FloorToInt(minutes);
            return total >= 60 ? string.Format(WellbeingTexts.Hours, total / 60, total % 60) : string.Format(WellbeingTexts.Minutes, total);
        }
    }
}
