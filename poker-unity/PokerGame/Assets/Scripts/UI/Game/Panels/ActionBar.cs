using System;
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
    /// Fold / Check-Call / Raise / All-in buttons plus raise sizing (slider, typed amount,
    /// half-pot and pot shortcuts). Buttons the server doesn't allow are disabled.
    /// The server re-checks everything; this only makes illegal moves impossible to click.
    /// </summary>
    public class ActionBar : MonoBehaviour
    {
        [SerializeField] private CanvasGroup group;

        [Header("Main actions")]
        [SerializeField] private Button foldButton;
        [SerializeField] private Button checkCallButton;
        [SerializeField] private TMP_Text checkCallLabel;
        [SerializeField] private Button raiseButton;
        [SerializeField] private TMP_Text raiseLabel;
        [SerializeField] private Button allInButton;
        [SerializeField] private TMP_Text allInLabel;

        [Header("Raise sizing")]
        [SerializeField] private GameObject raiseRow;
        [SerializeField] private Slider raiseSlider;
        [SerializeField] private TMP_InputField raiseInput;
        [SerializeField] private Button halfPotButton;
        [SerializeField] private Button potButton;

        /// <summary>(action, amount) - amount is the "raise to" total for raises, otherwise 0.</summary>
        public event Action<string, int> ActionChosen;

        private LegalActions legal;
        private int pot;
        private int myStreetBet;
        private bool visible = true;

        private void Awake()
        {
            raiseSlider.wholeNumbers = true;
            foldButton.onClick.AddListener(() => Choose(PokerActions.Fold, 0));
            checkCallButton.onClick.AddListener(OnCheckCall);
            raiseButton.onClick.AddListener(() => Choose(PokerActions.Raise, RaiseAmount));
            allInButton.onClick.AddListener(() => Choose(PokerActions.AllIn, 0));
            halfPotButton.onClick.AddListener(() => SetRaise(PotSizedRaise(0.5f)));
            potButton.onClick.AddListener(() => SetRaise(PotSizedRaise(1f)));
            raiseSlider.onValueChanged.AddListener(v => SetRaise((int)v));
            raiseInput.onValueChanged.AddListener(OnRaiseTyping); // button and slider follow every keystroke
            raiseInput.onEndEdit.AddListener(OnRaiseTyped);
        }

        private int RaiseAmount => (int)raiseSlider.value;

        /// <summary>Show the bar when it is our turn in this snapshot, hide it otherwise.</summary>
        public void Render(TableState state)
        {
            HandState hand = state.hand;
            int mySeat = state.you?.seat ?? -1;
            bool myTurn = hand != null && hand.legal != null && hand.result == null && hand.currentSeat == mySeat;
            if (!myTurn) { Hide(); return; }

            int myBet = hand.seats.Find(s => s.seat == mySeat)?.bet ?? 0;
            Show(hand.legal, hand.pot, myBet);
        }

        /// <summary>Show the bar for our turn. pot = everything in the middle, myStreetBet = what we already put in this street.</summary>
        public void Show(LegalActions legalActions, int potSize, int alreadyBet)
        {
            legal = legalActions;
            pot = potSize;
            myStreetBet = alreadyBet;

            bool canCheck = Allowed(PokerActions.Check);
            bool canCall = Allowed(PokerActions.Call);
            bool canRaise = Allowed(PokerActions.Raise) && legal.maxRaiseTo >= legal.minRaiseTo;

            foldButton.interactable = Allowed(PokerActions.Fold);
            checkCallButton.interactable = canCheck || canCall;
            checkCallLabel.text = canCheck ? "Check" : $"Call {CardFormat.Chips(legal.toCall)}";
            allInButton.interactable = Allowed(PokerActions.AllIn);
            allInLabel.text = "All-in";

            raiseButton.interactable = canRaise;
            raiseRow.SetActive(canRaise);
            if (canRaise)
            {
                raiseSlider.minValue = legal.minRaiseTo;
                raiseSlider.maxValue = legal.maxRaiseTo;
                SetRaise(legal.minRaiseTo);
            }
            else
            {
                raiseLabel.text = "Raise";
            }

            if (!visible) { UIAnim.FadeIn(group); UIAnim.PopIn(transform, 0.25f); }
            visible = true;
        }

        public void Hide()
        {
            if (!visible) return;
            visible = false;
            UIAnim.FadeOut(group);
        }

        private bool Allowed(string action) => legal != null && legal.actions.Contains(action);

        private void OnCheckCall()
        {
            if (Allowed(PokerActions.Check)) Choose(PokerActions.Check, 0);
            else Choose(PokerActions.Call, 0);
        }

        private void Choose(string action, int amount)
        {
            Hide(); // avoid double clicks; the next state update shows it again if needed
            ActionChosen?.Invoke(action, amount);
        }

        /// <summary>
        /// Pot-sized raise: call first, then raise by a fraction of the pot after calling.
        /// raiseTo = (our bet + toCall) + fraction * (pot + toCall)
        /// </summary>
        private int PotSizedRaise(float fraction)
        {
            int callTotal = myStreetBet + legal.toCall;
            return callTotal + Mathf.RoundToInt(fraction * (pot + legal.toCall));
        }

        /// <summary>While typing: keep slider and button in step, but leave the text box alone.</summary>
        private void OnRaiseTyping(string text)
        {
            if (legal == null || !int.TryParse(text, out int amount)) return;
            amount = Mathf.Clamp(amount, legal.minRaiseTo, legal.maxRaiseTo);
            raiseSlider.SetValueWithoutNotify(amount);
            bool isOpeningBet = myStreetBet + legal.toCall == 0;
            raiseLabel.text = (isOpeningBet ? "Bet " : "Raise to ") + CardFormat.Chips(amount);
        }

        private void OnRaiseTyped(string text)
        {
            if (int.TryParse(text, out int amount)) SetRaise(amount);
            else SetRaise(RaiseAmount); // restore the last valid value
        }

        /// <summary>Clamp to the allowed range and update slider, text box and button label together.</summary>
        private void SetRaise(int amount)
        {
            if (legal == null) return;
            amount = Mathf.Clamp(amount, legal.minRaiseTo, legal.maxRaiseTo);
            raiseSlider.SetValueWithoutNotify(amount);
            raiseInput.SetTextWithoutNotify(amount.ToString());

            bool isOpeningBet = myStreetBet + legal.toCall == 0;
            raiseLabel.text = (isOpeningBet ? "Bet " : "Raise to ") + CardFormat.Chips(amount);
        }
    }
}
