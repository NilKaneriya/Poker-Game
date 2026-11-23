using System.Collections.Generic;
using Poker.Core;
using static Poker.Core.Constants;
using static Poker.Network.DataModel;

namespace Poker.UI.Game
{
    /// <summary>
    /// End-of-hand screens: "You win" / "Not this time" once per hand, the winning cards
    /// highlighted on the board and in the winner's seat, and the "Out of chips" screen.
    /// </summary>
    public class HandResultPresenter
    {
        private readonly ResultPanel panel;
        private readonly BoardView board;
        private readonly SeatsView seats;
        private int resultShownForHand = -1;
        private string tableCode;

        public HandResultPresenter(ResultPanel panel, BoardView board, SeatsView seats)
        {
            this.panel = panel;
            this.board = board;
            this.seats = seats;
        }

        public void Render(TableState state)
        {
            // A different table: forget the old table's result so its pop-up can never show here.
            if (state.code != tableCode)
            {
                tableCode = state.code;
                resultShownForHand = -1;
                panel.HideNow();
            }

            HandState hand = state.hand;
            int mySeat = state.you?.seat ?? -1;
            SeatInfo me = mySeat >= 0 ? state.seats[mySeat] : null;

            // Out of chips and not playing the current hand: watch, or buy in again from the wallet.
            bool playingThisHand = hand != null && hand.result == null && hand.seats.Exists(s => s.seat == mySeat);
            if (me != null && me.chips == 0 && !playingThisHand)
            {
                if (!panel.IsShowingBusted) panel.ShowBusted(GameRules.StartingChips);
                return;
            }
            if (panel.IsShowingBusted) panel.Hide(); // e.g. after restarting

            // A new hand has started: close the previous hand's result screen.
            if (hand != null && hand.result == null) panel.Hide();

            if (hand?.result == null || resultShownForHand == state.handNumber) return;
            resultShownForHand = state.handNumber;

            List<WinnerInfo> winners = hand.result.winners;
            WinnerInfo myWin = winners.Find(w => w.playerId == state.you?.playerId);
            WinnerInfo topWinner = winners.Count > 0 ? winners[0] : null;

            if (winners.Count > 1) panel.ShowShared(SharedTitle(winners), WinnersLine(winners), myWin?.amount ?? 0, topWinner.bestCards);
            else if (myWin != null) panel.ShowWin(myWin.amount, myWin.handName, myWin.bestCards);
            else if (topWinner != null) panel.ShowLoss(topWinner.name, topWinner.amount, topWinner.handName, topWinner.bestCards);

            HighlightWinningCards(state, hand, topWinner);
        }

        /// <summary>Same hand for everyone = a tie ("Split pot"); different hands = side pots won by different players.</summary>
        private static string SharedTitle(List<WinnerInfo> winners) =>
            winners.TrueForAll(w => w.handName == winners[0].handName) ? GameTexts.SplitPot : GameTexts.PotsWon;

        /// <summary>"Maya +300 (Flush)  •  Leo +150 (Straight)"</summary>
        private static string WinnersLine(List<WinnerInfo> winners) =>
            string.Join("  •  ", winners.ConvertAll(w => string.IsNullOrEmpty(w.handName)
                ? string.Format(GameTexts.WinnerShare, w.name, CardFormat.Chips(w.amount))
                : string.Format(GameTexts.WinnerShareWithHand, w.name, CardFormat.Chips(w.amount), w.handName)));

        private void HighlightWinningCards(TableState state, HandState hand, WinnerInfo winner)
        {
            if (winner?.bestCards == null || winner.bestCards.Count == 0) return;
            var best = new HashSet<string>(winner.bestCards);
            board.HighlightBestCards(best, hand.board);

            SeatHandInfo winnerHand = hand.seats.Find(s => s.seat == winner.seat);
            List<string> holeCards = winner.playerId == state.you?.playerId ? hand.yourCards : winnerHand?.cards;
            seats.ForServerSeat(winner.seat).HighlightCards(best, holeCards);
        }
    }
}
