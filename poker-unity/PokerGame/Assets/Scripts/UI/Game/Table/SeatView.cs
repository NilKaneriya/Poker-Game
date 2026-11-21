using System.Collections.Generic;
using Poker.Core;
using Poker.Network;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static Poker.Core.Constants;
using static Poker.Network.DataModel;

namespace Poker.UI.Game
{
    /// <summary>
    /// One seat around the table: avatar, name, chips, hole cards, current bet,
    /// last action, dealer button and the turn timer ring.
    /// </summary>
    public class SeatView : MonoBehaviour
    {
        [Header("States")]
        [SerializeField] private GameObject occupiedRoot;
        [SerializeField] private GameObject emptyRoot;
        [SerializeField] private CanvasGroup occupiedGroup; // faded when folded

        [Header("Player")]
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text chipsText;
        [SerializeField] private TMP_Text initialText;
        [SerializeField] private Image avatarBackground;
        [SerializeField] private GameObject offlineBadge;
        [SerializeField] private GameObject dealerButton;

        [Header("Hand")]
        [SerializeField] private CardView[] cards = new CardView[2];
        [SerializeField] private GameObject actionBadge;
        [SerializeField] private TMP_Text actionText;
        [SerializeField] private GameObject betRoot;
        [SerializeField] private TMP_Text betText;

        [Header("Turn")]
        [SerializeField] private GameObject turnGlow;
        [SerializeField] private Image timerRing; // Image type Filled (radial)
        [SerializeField] private Transform pulseTarget;

        [Header("Your hand (bottom seat only)")]
        [Tooltip("Pill under the chips showing your current hand, e.g. 'One Pair'. Optional.")]
        [SerializeField] private GameObject handRankRoot;
        [SerializeField] private TMP_Text handRankText;

        [Header("Animation")]
        [Tooltip("Where hole cards are dealt from (the middle of the table).")]
        [SerializeField] private Transform dealOrigin;

        /// <summary>Wait before this seat's cards fly in, so seats are dealt one after another.</summary>
        public float DealDelay { get; set; }
        /// <summary>Where this player's chips leave from / arrive at (the name plate).</summary>
        public Vector3 ChipPosition => pulseTarget.position;
        /// <summary>Where this player's bet sits on the table.</summary>
        public Vector3 BetPosition => betRoot.transform.position;

        private int shownChips = -1;
        private bool isTurn;
        private float turnEndsAt;
        private float turnDuration;

        public void ShowEmpty()
        {
            occupiedRoot.SetActive(false);
            emptyRoot.SetActive(true);
            shownChips = -1;
            SetTurn(false, 0, 0);
            SetHandRank(null);
            foreach (var card in cards) card.Clear();
        }

        /// <summary>Little bounce on the name plate, e.g. when the pot lands on a winner.</summary>
        public void PunchPlate() => UIAnim.Punch(pulseTarget, 0.12f);

        /// <summary>Show (or hide, with null) the current-hand pill under the chips.</summary>
        public void SetHandRank(string handName)
        {
            if (handRankRoot == null) return;
            bool show = !string.IsNullOrEmpty(handName);
            if (!show) { handRankRoot.SetActive(false); return; }

            bool changed = !handRankRoot.activeSelf || handRankText.text != handName;
            handRankRoot.SetActive(true);
            handRankText.text = handName;
            if (changed) UIAnim.PopIn(handRankRoot.transform, 0.3f); // e.g. the flop just made your pair
        }

        /// <param name="seat">Public seat info.</param>
        /// <param name="hand">This seat's state in the running hand, or null.</param>
        /// <param name="holeCards">Cards to show face up (ours, or revealed at showdown); null = backs if in hand.</param>
        /// <param name="hideBet">The hand is over: bets have gone to the pot, so don't draw them.</param>
        public void Show(SeatInfo seat, SeatHandInfo hand, List<string> holeCards, bool isDealer, bool hideBet = false)
        {
            emptyRoot.SetActive(false);
            occupiedRoot.SetActive(true);

            nameText.text = seat.name;
            initialText.text = string.IsNullOrEmpty(seat.name) ? "?" : seat.name.Substring(0, 1).ToUpperInvariant();
            avatarBackground.color = UIColors.AvatarPalette[ColorIndex(seat.playerId)];
            offlineBadge.SetActive(!seat.connected);
            dealerButton.SetActive(isDealer);

            if (shownChips != seat.chips)
            {
                UIAnim.CountTo(chipsText, shownChips < 0 ? seat.chips : shownChips, seat.chips, CardFormat.Chips);
                shownChips = seat.chips;
            }

            ShowHand(hand, holeCards, hideBet);
        }

        private void ShowHand(SeatHandInfo hand, List<string> holeCards, bool hideBet)
        {
            bool inHand = hand != null;
            bool folded = inHand && hand.folded;
            occupiedGroup.alpha = folded ? 0.5f : 1f;

            // No cards showing yet and now in a hand: they are being dealt.
            bool dealing = inHand && !folded && !cards[0].gameObject.activeSelf;

            for (int i = 0; i < cards.Length; i++)
            {
                if (!inHand || folded) cards[i].Clear();
                else if (holeCards != null && i < holeCards.Count) cards[i].ShowFace(holeCards[i]);
                else cards[i].ShowBack();
            }

            if (dealing && dealOrigin != null)
            {
                for (int i = 0; i < cards.Length; i++) cards[i].FlyIn(dealOrigin.position, DealDelay + i * AnimationTimings.DealCardStagger);
            }

            string action = !inHand ? null : hand.allIn ? "ALL-IN" : hand.lastAction?.ToUpperInvariant();
            actionBadge.SetActive(!string.IsNullOrEmpty(action));
            if (!string.IsNullOrEmpty(action) && actionText.text != action)
            {
                actionText.text = action;
                UIAnim.PopIn(actionBadge.transform, 0.25f);
            }

            bool hasBet = inHand && hand.bet > 0 && !hideBet;
            betRoot.SetActive(hasBet);
            if (hasBet && betText.text != CardFormat.Chips(hand.bet))
            {
                betText.text = CardFormat.Chips(hand.bet);
                UIAnim.Punch(betRoot.transform);
            }
        }

        /// <summary>Highlight this seat and run its countdown ring.</summary>
        public void SetTurn(bool active, float secondsLeft, float totalSeconds)
        {
            if (active && !isTurn) UIAnim.StartPulse(pulseTarget, 1.04f);
            if (!active && isTurn) UIAnim.StopPulse(pulseTarget);

            isTurn = active;
            turnGlow.SetActive(active);
            timerRing.gameObject.SetActive(active);
            turnDuration = Mathf.Max(0.01f, totalSeconds);
            turnEndsAt = Time.time + secondsLeft;
            UpdateTimerRing();
        }

        /// <summary>Mark the winner's cards at the end of a hand.</summary>
        public void HighlightCards(ICollection<string> winningCards, List<string> holeCards)
        {
            for (int i = 0; i < cards.Length; i++)
            {
                bool isWinning = holeCards != null && i < holeCards.Count && winningCards.Contains(holeCards[i]);
                cards[i].SetHighlighted(isWinning);
            }
        }

        private void Update()
        {
            if (isTurn) UpdateTimerRing();
        }

        /// <summary>Same id always gives the same colour (sum of character codes).</summary>
        private static int ColorIndex(string playerId)
        {
            int sum = 0;
            if (playerId != null) foreach (char c in playerId) sum += c;
            return sum % UIColors.AvatarPalette.Length;
        }

        private void UpdateTimerRing()
        {
            float left01 = Mathf.Clamp01((turnEndsAt - Time.time) / turnDuration);
            timerRing.fillAmount = left01;
            // Green -> yellow -> red as time runs out.
            timerRing.color = left01 > 0.5f
                ? Color.Lerp(new Color(1f, 0.8f, 0.2f), new Color(0.2f, 0.85f, 0.45f), (left01 - 0.5f) * 2f)
                : Color.Lerp(new Color(0.95f, 0.25f, 0.25f), new Color(1f, 0.8f, 0.2f), left01 * 2f);
        }
    }
}
