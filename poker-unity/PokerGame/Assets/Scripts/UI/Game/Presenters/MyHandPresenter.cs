using System.Collections.Generic;
using Poker.Gameplay;
using static Poker.Core.Constants;
using static Poker.Network.DataModel;

namespace Poker.UI.Game
{
    /// <summary>
    /// "What do I have right now?" Works out the local player's best hand from their own
    /// 2 cards + the board, and shows it in two places: the pill under their chips and the
    /// marked row in the hand-rankings chart. Other players' cards are never involved.
    /// </summary>
    public class MyHandPresenter
    {
        private readonly SeatsView seats;
        private readonly HandRankingsPanel chart; // optional

        public MyHandPresenter(SeatsView seats, HandRankingsPanel chart)
        {
            this.seats = seats;
            this.chart = chart;
        }

        public void Render(TableState state)
        {
            HandState hand = state.hand;
            int rank = -1;
            string status;

            if (hand == null) status = GameTexts.HandNotDealt;
            else if (hand.yourCards == null || hand.yourCards.Count == 0) status = GameTexts.HandWatching;
            else if (hand.seats.Find(s => s.seat == state.you?.seat)?.folded == true) status = GameTexts.HandFolded;
            else
            {
                var cards = new List<string>(hand.yourCards);
                cards.AddRange(hand.board);
                rank = HandEvaluator.Evaluate(cards);
                status = rank < 0 ? "" : string.Format(GameTexts.YourHand, HandRanks.Names[rank]);
            }

            bool seated = state.you != null && state.you.seat >= 0;
            seats.Mine.SetHandRank(seated && rank >= 0 ? HandRanks.Names[rank] : null);

            if (chart != null) chart.ShowCurrentHand(rank < 0 ? -1 : HandChart.RowOf(rank), status);
        }
    }
}
