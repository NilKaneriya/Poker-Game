using System.Collections.Generic;
using Poker.Network;
using static Poker.Core.Constants;
using static Poker.Network.DataModel;

namespace Poker.UI.Game
{
    /// <summary>
    /// Sample table used only to look at the Game scene layout without a server.
    /// Same shape as a real tableState message.
    /// </summary>
    public static class GamePreviewData
    {
        public static TableState Create()
        {
            var seats = new List<SeatInfo>
            {
                new SeatInfo { seat = 0, playerId = "p-maya", name = "Maya", chips = 1240, connected = true },
                new SeatInfo { seat = 1, playerId = "p-leo", name = "Leo", chips = 860, connected = true },
                new SeatInfo { seat = 2, playerId = "p-you", name = "You", chips = 955, connected = true },
                null, // empty seat
                new SeatInfo { seat = 4, playerId = "p-sam", name = "Sam", chips = 0, connected = false },
                new SeatInfo { seat = 5, playerId = "p-bot", name = "Bot Ava", chips = 1500, isBot = true, connected = true },
            };

            var hand = new HandState
            {
                stage = PokerStages.Flop,
                board = new List<string> { "Ah", "Td", "7c" },
                pot = 230,
                currentSeat = 2,
                turnTimeLeftMs = 21000,
                turnTimeTotalMs = 30000,
                yourCards = new List<string> { "As", "Kd" },
                legal = new LegalActions
                {
                    actions = new List<string> { PokerActions.Fold, PokerActions.Call, PokerActions.Raise, PokerActions.AllIn },
                    toCall = 40,
                    minRaiseTo = 80,
                    maxRaiseTo = 955,
                },
                seats = new List<SeatHandInfo>
                {
                    new SeatHandInfo { seat = 0, bet = 40, lastAction = PokerActions.Raise },
                    new SeatHandInfo { seat = 1, bet = 40, lastAction = PokerActions.Call },
                    new SeatHandInfo { seat = 2, bet = 0, lastAction = PokerActions.Check },
                    new SeatHandInfo { seat = 4, folded = true, lastAction = PokerActions.Fold },
                    new SeatHandInfo { seat = 5, bet = 0, lastAction = PokerActions.Check },
                },
            };

            return new TableState
            {
                code = "K7QXP",
                name = "Friday Night",
                mode = TableModes.Friends,
                started = true,
                hostId = "p-maya",
                maxSeats = 6,
                smallBlind = 10,
                bigBlind = 20,
                buttonSeat = 1,
                handNumber = 7,
                seats = seats,
                you = new YouInfo { playerId = "p-you", seat = 2 },
                hand = hand,
            };
        }

        /// <summary>A play-with-friends table that is not full yet: you (the host) and one friend.</summary>
        public static TableState CreateWaitingRoom(int maxSeats, bool started = false)
        {
            var seats = new List<SeatInfo>();
            for (int i = 0; i < maxSeats; i++) seats.Add(null);
            seats[0] = new SeatInfo { seat = 0, playerId = "p-you", name = "You", chips = 1000, connected = true };
            seats[1] = new SeatInfo { seat = 1, playerId = "p-leo", name = "Leo", chips = 1000, connected = true };

            return new TableState
            {
                code = "K7QXP",
                name = "Friday Night",
                mode = TableModes.Friends,
                started = started,
                hostId = "p-you",
                maxSeats = maxSeats,
                smallBlind = 10,
                bigBlind = 20,
                buttonSeat = -1,
                seats = seats,
                you = new YouInfo { playerId = "p-you", seat = 0 },
            };
        }
    }
}
