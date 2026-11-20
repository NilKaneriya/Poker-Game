using System;
using System.Collections.Generic;

namespace Poker.Network
{
    /// <summary>
    /// Every serialized payload exchanged with the server, grouped in one place.
    /// Field names are lowercase on purpose so they map 1:1 to the server's JSON
    /// (serialized with Newtonsoft.Json). Use with: using static Poker.Network.DataModel;
    /// </summary>
    public static class DataModel
    {
        // ---------- HTTP (login) ----------

        [Serializable]
        public class AuthRequest
        {
            public string username;
            public string password;
            public bool remember;
        }

        [Serializable]
        public class AuthResponse
        {
            public string token; // private login token, never shown to other players
            public string playerId;
            public string username;
        }

        [Serializable]
        public class UserInfo
        {
            public string playerId;
            public string username;
        }

        [Serializable]
        public class ErrorBody
        {
            public string error;
        }

        // ---------- Socket: Server -> Client ----------

        [Serializable]
        public class SessionInfo
        {
            public string playerId;
            public string username;
        }

        [Serializable]
        public class TableSummary
        {
            public string code;
            public string name;
            public string mode; // see TableModes
            public int players;
            public int maxSeats;
            public string blinds;
            public bool started; // false = still in the waiting room
            public bool inHand;
        }

        [Serializable]
        public class SeatInfo
        {
            public int seat;
            public string playerId;
            public string name;
            public int chips;
            public bool isBot;
            public bool connected;
        }

        [Serializable]
        public class YouInfo
        {
            public string playerId;
            public int seat;
        }

        [Serializable]
        public class TableState
        {
            public string code;
            public string name;
            public string mode; // see TableModes
            public bool started; // false = waiting room: hands begin once every seat is taken
            public string hostId; // the player who may start the game early
            public int maxSeats;
            public int smallBlind;
            public int bigBlind;
            public int buttonSeat;
            public int handNumber;
            public List<SeatInfo> seats = new List<SeatInfo>(); // null entries are empty seats
            public YouInfo you;
            public HandState hand; // null between hands
        }

        /// <summary>The running hand as seen by this player (opponents' cards hidden until showdown).</summary>
        [Serializable]
        public class HandState
        {
            public string stage; // see PokerStages
            public List<string> board = new List<string>();
            public int pot; // everything in the middle, including this street's bets
            public int currentSeat = -1; // whose turn it is, -1 if nobody
            public int turnTimeLeftMs;
            public int turnTimeTotalMs;
            public List<string> yourCards = new List<string>();
            public LegalActions legal; // only set when it is your turn
            public List<SeatHandInfo> seats = new List<SeatHandInfo>();
            public HandResult result; // set once the hand is over
        }

        [Serializable]
        public class SeatHandInfo
        {
            public int seat;
            public int bet; // chips put in on the current street
            public string lastAction;
            public bool folded;
            public bool allIn;
            public List<string> cards; // only filled at showdown for players who show
        }

        [Serializable]
        public class LegalActions
        {
            public List<string> actions = new List<string>(); // see PokerActions
            public int toCall;
            public int minRaiseTo; // raise amounts are "raise to" totals for this street
            public int maxRaiseTo;
        }

        [Serializable]
        public class HandResult
        {
            public List<WinnerInfo> winners = new List<WinnerInfo>();
        }

        [Serializable]
        public class WinnerInfo
        {
            public int seat;
            public string playerId;
            public string name;
            public int amount;
            public string handName; // e.g. "Full House"; empty if everyone else folded
            public List<string> bestCards = new List<string>();
        }

        [Serializable]
        public class ActionRejectedInfo
        {
            public string reason;
        }

        [Serializable]
        public class ServerError
        {
            public string message;
        }

        // ---------- Socket: Client -> Server ----------

        [Serializable]
        public class CreateTableRequest
        {
            public string tableName;
            public string mode; // see TableModes
            public int maxSeats; // GameRules.MinSeats..MaxSeats
        }

        [Serializable]
        public class JoinTableRequest
        {
            public string code;
        }

        [Serializable]
        public class PlayerActionRequest
        {
            public string action;
            public int amount;
        }
    }
}
