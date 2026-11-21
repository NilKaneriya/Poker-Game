using UnityEngine;
using static Poker.Core.Constants;
using static Poker.Network.DataModel;

namespace Poker.UI.Game
{
    /// <summary>
    /// Decides which animations to play by comparing the previous table snapshot with
    /// the new one: new bets fly from the player to the table, bets that disappear slide
    /// into the pot, a new street is announced, and the pot flies to the winner(s).
    /// Visual only; every number on screen still comes from the server.
    /// </summary>
    public class TableChangeAnimator : MonoBehaviour
    {
        [SerializeField] private ChipAnimator chips;
        [SerializeField] private StageBannerView banner;
        [SerializeField] private SeatsView seats;
        [SerializeField] private BoardView board;

        public void Play(TableState before, TableState now)
        {
            HandState was = before?.hand;
            HandState hand = now.hand;
            if (hand == null || before == null || before.code != now.code) return;

            bool sameHand = was != null && before.handNumber == now.handNumber;
            bool newStreet = !sameHand || was.stage != hand.stage;
            bool handJustEnded = hand.result != null && (!sameHand || was.result == null);

            AnnounceStreet(was, hand, newStreet, handJustEnded);
            float collectTime = sameHand ? CollectBetsIntoPot(was, hand, newStreet, now.bigBlind) : 0f;
            if (hand.result == null) PlaceNewBets(was, hand, sameHand && !newStreet, now.bigBlind, collectTime);
            if (handJustEnded) PayWinners(hand, collectTime);
        }

        private void AnnounceStreet(HandState was, HandState hand, bool newStreet, bool handJustEnded)
        {
            if (newStreet && hand.result == null) banner.Show(hand.stage);
            bool reachedShowdown = hand.stage == PokerStages.Showdown && was?.stage != PokerStages.Showdown;
            if (handJustEnded && reachedShowdown) banner.Show(PokerStages.Showdown);
        }

        /// <summary>Bets that left the table went into the pot. Returns how long that takes.</summary>
        private float CollectBetsIntoPot(HandState was, HandState hand, bool newStreet, int bigBlind)
        {
            float collectTime = 0f;
            foreach (SeatHandInfo old in was.seats)
            {
                if (old.bet <= 0) continue;
                int stillBet = hand.result != null ? 0 : hand.seats.Find(s => s.seat == old.seat)?.bet ?? 0;
                if (stillBet >= old.bet && !newStreet) continue;

                chips.Fly(seats.ForServerSeat(old.seat).BetPosition, board.PotPosition, ChipAnimator.ChipsFor(old.bet, bigBlind));
                collectTime = AnimationTimings.CollectToPotTime;
            }
            return collectTime;
        }

        /// <summary>New bets and blinds: chips go from the player to the front of their seat.</summary>
        private void PlaceNewBets(HandState was, HandState hand, bool sameStreet, int bigBlind, float delay)
        {
            foreach (SeatHandInfo s in hand.seats)
            {
                int had = sameStreet ? was.seats.Find(o => o.seat == s.seat)?.bet ?? 0 : 0;
                if (s.bet <= had) continue;
                SeatView view = seats.ForServerSeat(s.seat);
                chips.Fly(view.ChipPosition, view.BetPosition, ChipAnimator.ChipsFor(s.bet - had, bigBlind), delay);
            }
        }

        private void PayWinners(HandState hand, float delay)
        {
            foreach (WinnerInfo w in hand.result.winners)
            {
                if (w.seat < 0 || w.amount <= 0) continue;
                SeatView winner = seats.ForServerSeat(w.seat);
                chips.Fly(board.PotPosition, winner.ChipPosition, ChipFlights.PotToWinnerChips,
                    delay + AnimationTimings.PotToWinnerDelay, winner.PunchPlate);
            }
        }
    }
}
