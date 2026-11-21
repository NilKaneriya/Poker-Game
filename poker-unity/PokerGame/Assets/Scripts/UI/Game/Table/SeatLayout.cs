using UnityEngine;
using static Poker.Core.Constants;

namespace Poker.UI.Game
{
    /// <summary>
    /// Maps server seat numbers to screen slots. The table is rotated so the local
    /// player always sits at the bottom (slot 0), and smaller tables use evenly spread
    /// slots (TableLayout.SlotsBySeatCount).
    /// </summary>
    public static class SeatLayout
    {
        public const int MySlot = 0;

        public static int[] SlotsFor(int seatCount) =>
            TableLayout.SlotsBySeatCount[Mathf.Clamp(seatCount, 0, TableLayout.SlotsBySeatCount.Length - 1)];

        /// <summary>Screen slot of a server seat, with mySeat drawn at the bottom.</summary>
        public static int ScreenSlot(int serverSeat, int mySeat, int seatCount)
        {
            int[] slots = SlotsFor(seatCount);
            int relative = (serverSeat - mySeat + seatCount) % seatCount;
            return slots[Mathf.Min(relative, slots.Length - 1)];
        }

        /// <summary>Order seats are dealt in: clockwise starting left of the dealer button.</summary>
        public static int DealOrder(int serverSeat, int buttonSeat, int seatCount) =>
            (serverSeat - buttonSeat - 1 + seatCount * 2) % seatCount;
    }
}
