using System.Collections.Generic;
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

        public HandResultPresenter(ResultPanel panel, BoardView board, SeatsView seats)
        {
            this.panel = panel;
            this.board = board;
            this.seats = seats;
        }

        public void Render(TableState state)
        {
            HandState hand = state.hand;
            int mySeat = state.you?.seat ?? -1;
            SeatInfo me = mySeat >= 0 ? state.seats[mySeat] : null;

            // Out of chips and not playing the current hand: watch, or restart (no purchase, ever).
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

            WinnerInfo myWin = hand.result.winners.Find(w => w.playerId == state.you?.playerId);
            WinnerInfo topWinner = hand.result.winners.Count > 0 ? hand.result.winners[0] : null;
            if (myWin != null) panel.ShowWin(myWin.amount, myWin.handName, myWin.bestCards);
            else if (topWinner != null) panel.ShowLoss(topWinner.name, topWinner.amount, topWinner.handName, topWinner.bestCards);

            HighlightWinningCards(state, hand, topWinner);
        }

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
