using Poker.Audio;
using static Poker.Core.Constants;
using static Poker.Network.DataModel;

namespace Poker.UI.Game
{
    /// <summary>
    /// Decides which sounds to play by comparing the previous table snapshot with the new
    /// one (the audio twin of TableChangeAnimator): dealing, each player's action, cards
    /// turned on the board, chips into the pot and to the winner, your turn, win / lose,
    /// and waiting-room joins. Delays line up with the animations.
    /// </summary>
    public class TableSoundCues
    {
        private static AudioManager Audio => AudioManager.Instance;

        public void Play(TableState before, TableState now)
        {
            if (Audio == null || before == null || before.code != now.code) return;

            WaitingRoomSounds(before, now);

            HandState was = before.hand;
            HandState hand = now.hand;
            if (hand == null) return;

            bool sameHand = was != null && before.handNumber == now.handNumber;
            if (!sameHand)
            {
                DealSounds(now);
            }
            else
            {
                ActionSound(was, hand);
                BoardSounds(was, hand);
                CollectSound(was, hand);
                ShowdownRevealSound(was, hand);
            }

            YourTurnSound(was, hand, now.you?.seat ?? -1, sameHand, now);
            ResultSounds(was, hand, now, sameHand);
        }

        /// <summary>The server refused something (action or request).</summary>
        public void PlayError() => Audio?.Play(SoundId.Error);

        // ---------- Pieces ----------

        private static void WaitingRoomSounds(TableState before, TableState now)
        {
            if (!now.started && Seated(now) > Seated(before)) Audio.Play(SoundId.PlayerJoined);
            if (!before.started && now.started) Audio.Play(SoundId.GameStart);
        }

        /// <summary>Blinds go in, then one card sound per card dealt, in the same order as the deal animation.</summary>
        private static void DealSounds(TableState now)
        {
            Audio.Play(SoundId.ChipBet);
            int played = 0;
            foreach (SeatHandInfo seat in now.hand.seats)
            {
                int order = SeatLayout.DealOrder(seat.seat, now.buttonSeat, now.seats.Count);
                for (int card = 0; card < 2 && played < SoundCueTimings.MaxDealSounds; card++, played++)
                {
                    float delay = AnimationTimings.DealFirstSeatDelay + order * AnimationTimings.DealSeatStagger
                                  + card * AnimationTimings.DealCardStagger;
                    Audio.Play(SoundId.CardDeal, delay);
                }
            }
        }

        /// <summary>The player whose turn it was has acted: check, call, raise, all-in or fold.</summary>
        private static void ActionSound(HandState was, HandState hand)
        {
            int actor = was.currentSeat;
            bool turnMoved = hand.currentSeat != actor || hand.stage != was.stage || hand.result != null;
            if (was.result != null || actor < 0 || !turnMoved) return;

            SeatHandInfo seat = hand.seats.Find(s => s.seat == actor);
            if (seat == null) return; // left the table

            SoundId sound = seat.folded ? SoundId.Fold
                : seat.allIn ? SoundId.ChipAllIn
                : seat.lastAction == PokerActions.Check ? SoundId.Check
                : seat.lastAction == PokerActions.Raise ? SoundId.ChipRaise
                : SoundId.ChipBet;
            Audio.Play(sound);
        }

        private static void BoardSounds(HandState was, HandState hand)
        {
            for (int i = was.board.Count; i < hand.board.Count; i++)
            {
                float delay = SoundCueTimings.BoardFlipDelay + (i - was.board.Count) * SoundCueTimings.BoardFlipStagger;
                Audio.Play(SoundId.CardFlip, delay);
            }
        }

        private static void CollectSound(HandState was, HandState hand)
        {
            bool streetClosed = hand.stage != was.stage || (hand.result != null && was.result == null);
            if (streetClosed && was.seats.Exists(s => s.bet > 0)) Audio.Play(SoundId.ChipCollect, SoundCueTimings.CollectDelay);
        }

        private static void ShowdownRevealSound(HandState was, HandState hand)
        {
            bool revealed = hand.seats.Exists(s => s.cards != null && was.seats.Find(o => o.seat == s.seat)?.cards == null);
            if (revealed) Audio.Play(SoundId.CardFlip);
        }

        private static void YourTurnSound(HandState was, HandState hand, int mySeat, bool sameHand, TableState now)
        {
            if (!IsTurnOf(hand, mySeat)) return;
            bool newTurn = !sameHand || !IsTurnOf(was, mySeat) || was.stage != hand.stage;
            if (!newTurn) return;

            float delay = sameHand ? SoundCueTimings.YourTurnDelay
                : AnimationTimings.DealFirstSeatDelay + now.seats.Count * AnimationTimings.DealSeatStagger + AnimationTimings.DealFlight;
            Audio.Play(SoundId.YourTurn, delay);
        }

        private static void ResultSounds(HandState was, HandState hand, TableState now, bool sameHand)
        {
            if (hand.result == null || (sameHand && was.result != null)) return;

            Audio.Play(SoundId.ChipsWin, SoundCueTimings.WinChipsDelay);
            string me = now.you?.playerId;
            bool iWon = hand.result.winners.Exists(w => w.playerId == me);
            bool iPlayed = hand.seats.Exists(s => s.seat == now.you?.seat);
            if (iWon) Audio.Play(SoundId.Win, SoundCueTimings.ResultJingleDelay);
            else if (iPlayed) Audio.Play(SoundId.Lose, SoundCueTimings.ResultJingleDelay);
        }

        // ---------- Helpers ----------

        private static bool IsTurnOf(HandState hand, int seat) =>
            hand != null && seat >= 0 && hand.result == null && hand.currentSeat == seat;

        private static int Seated(TableState table) => table.seats.FindAll(s => s != null).Count;
    }
}
