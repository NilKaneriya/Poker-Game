using System.Collections.Generic;
using Poker.Core;
using TMPro;
using UnityEngine;
using static Poker.Core.Constants;
using static Poker.Network.DataModel;

namespace Poker.UI.Game
{
    /// <summary>
    /// The middle of the table: the 5 community cards, the pot, the street label and
    /// the "waiting / next hand" message between hands.
    /// </summary>
    public class BoardView : MonoBehaviour
    {
        [SerializeField] private CardView[] boardCards = new CardView[5];
        [SerializeField] private GameObject potRoot;
        [SerializeField] private TMP_Text potText;
        [SerializeField] private TMP_Text stageText;
        [SerializeField] private TMP_Text waitingText;

        private int shownPot = -1;

        /// <summary>Where chips go into / come out of the pot.</summary>
        public Vector3 PotPosition => potRoot.transform.position;

        public void Render(TableState table)
        {
            HandState hand = table.hand;

            for (int i = 0; i < boardCards.Length; i++)
            {
                if (hand != null && i < hand.board.Count) boardCards[i].ShowFace(hand.board[i]);
                else boardCards[i].Clear();
            }

            RenderPot(hand?.pot ?? 0);

            stageText.gameObject.SetActive(hand != null);
            if (hand != null) stageText.text = PokerStages.Labels.TryGetValue(hand.stage, out string label) ? label : "";

            // Before the game starts the waiting room panel covers the middle instead.
            bool showWaiting = hand == null && table.started;
            waitingText.gameObject.SetActive(showWaiting);
            if (showWaiting) waitingText.text = WaitingMessage(table);
        }

        /// <summary>Gold outline on the board cards that make the winning hand, the rest fade.</summary>
        public void HighlightBestCards(HashSet<string> best, List<string> board)
        {
            for (int i = 0; i < boardCards.Length && i < board.Count; i++)
            {
                bool used = best.Contains(board[i]);
                boardCards[i].SetHighlighted(used);
                boardCards[i].SetDimmed(!used);
            }
        }

        /// <summary>Little bump on the middle message, e.g. when the waiting room closes.</summary>
        public void PunchMessage() => UIAnim.Punch(waitingText.transform, 0.2f);

        private void RenderPot(int pot)
        {
            potRoot.SetActive(pot > 0);
            if (pot == shownPot) return;
            UIAnim.CountTo(potText, Mathf.Max(0, shownPot), pot, p => $"Pot {CardFormat.Chips(p)}");
            UIAnim.Punch(potRoot.transform, 0.12f);
            shownPot = pot;
        }

        private static string WaitingMessage(TableState table)
        {
            int players = table.seats.FindAll(s => s != null).Count;
            if (players < 2) return string.Format(GameTexts.WaitingForPlayers, table.code);
            return table.handNumber == 0 ? GameTexts.FirstHandComing : GameTexts.NextHandComing;
        }
    }
}
