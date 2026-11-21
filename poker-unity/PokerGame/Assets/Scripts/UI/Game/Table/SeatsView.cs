using System.Collections.Generic;
using UnityEngine;
using static Poker.Core.Constants;
using static Poker.Network.DataModel;

namespace Poker.UI.Game
{
    /// <summary>
    /// All the seats around the table: picks the slots for the table size, rotates the
    /// table so you sit at the bottom, and draws every seat from the server snapshot.
    /// </summary>
    public class SeatsView : MonoBehaviour
    {
        [Tooltip("Seat views in screen order, clockwise. Index 0 is the bottom seat (you).")]
        [SerializeField] private SeatView[] seats = new SeatView[TableLayout.ScreenSlots];

        private int mySeat;
        private int seatCount;

        /// <summary>The bottom seat, where the local player sits.</summary>
        public SeatView Mine => seats[SeatLayout.MySlot];

        /// <summary>The view drawing a server seat (valid after Render).</summary>
        public SeatView ForServerSeat(int serverSeat) => seats[SeatLayout.ScreenSlot(serverSeat, mySeat, seatCount)];

        public void Render(TableState state)
        {
            HandState hand = state.hand;
            mySeat = Mathf.Max(0, state.you?.seat ?? 0);
            seatCount = state.seats.Count;

            var used = new HashSet<int>(SeatLayout.SlotsFor(seatCount));
            for (int i = 0; i < seats.Length; i++) seats[i].gameObject.SetActive(used.Contains(i));

            for (int serverSeat = 0; serverSeat < seatCount; serverSeat++)
            {
                SeatView view = ForServerSeat(serverSeat);
                SeatInfo seat = state.seats[serverSeat];
                if (seat == null) { view.ShowEmpty(); continue; }

                SeatHandInfo seatHand = hand?.seats.Find(s => s.seat == serverSeat);
                bool isMe = serverSeat == state.you?.seat;
                List<string> faceUp = isMe ? hand?.yourCards : seatHand?.cards;

                int dealOrder = SeatLayout.DealOrder(serverSeat, state.buttonSeat, seatCount);
                view.DealDelay = AnimationTimings.DealFirstSeatDelay + dealOrder * AnimationTimings.DealSeatStagger;
                view.Show(seat, seatHand, faceUp, serverSeat == state.buttonSeat, hideBet: hand?.result != null);

                bool isTurn = hand != null && hand.currentSeat == serverSeat && hand.result == null;
                view.SetTurn(isTurn, hand?.turnTimeLeftMs / 1000f ?? 0f, hand?.turnTimeTotalMs / 1000f ?? 0f);
            }
        }
    }
}
