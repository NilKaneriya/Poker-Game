using Poker.Audio;
using TMPro;
using UnityEngine;
using static Poker.Core.Constants;
using static Poker.Network.DataModel;

namespace Poker.UI.Game
{
    /// <summary>
    /// Ticks once per second during the last seconds of *your* turn (urgent ticks at the
    /// very end), so a player looking away hears that time is running out.
    /// </summary>
    public class TurnTimerAlert : MonoBehaviour
    {
        [Header("Countdown (optional, always on top of pop-ups)")]
        [SerializeField] private GameObject countdownRoot;
        [SerializeField] private TMP_Text countdownText;

        private bool myTurn;
        private float turnEndsAt;
        private int lastTicked = int.MaxValue;

        /// <summary>Called with every snapshot.</summary>
        public void Track(TableState state)
        {
            HandState hand = state.hand;
            int mySeat = state.you?.seat ?? -1;
            bool nowMyTurn = hand != null && hand.result == null && hand.legal != null && mySeat >= 0 && hand.currentSeat == mySeat;

            if (nowMyTurn) turnEndsAt = Time.time + hand.turnTimeLeftMs / 1000f;
            if (nowMyTurn && !myTurn) lastTicked = int.MaxValue; // a new turn: tick again
            myTurn = nowMyTurn;
        }

        private void Update()
        {
            if (countdownRoot != null) countdownRoot.SetActive(myTurn);
            if (!myTurn) return;

            int left = Mathf.Max(0, Mathf.CeilToInt(turnEndsAt - Time.time));
            if (countdownText != null)
            {
                countdownText.text = $"Your turn  {left}s";
                countdownText.color = left <= SoundCueTimings.TimerAlertSeconds ? new Color(0.95f, 0.3f, 0.3f) : new Color(0.97f, 0.76f, 0.2f);
            }

            int secondsLeft = Mathf.CeilToInt(turnEndsAt - Time.time);
            if (secondsLeft <= 0 || secondsLeft > SoundCueTimings.TimerAlertSeconds || secondsLeft >= lastTicked) return;

            lastTicked = secondsLeft;
            SoundId tick = secondsLeft <= SoundCueTimings.TimerUrgentSeconds ? SoundId.TimerUrgent : SoundId.TimerTick;
            if (AudioManager.Instance != null) AudioManager.Instance.Play(tick);
        }
    }
}
