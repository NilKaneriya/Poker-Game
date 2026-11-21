using System;
using System.Collections.Generic;

namespace Poker.Network
{
    /// <summary>
    /// Every data shape used by the game, grouped in one place: the payloads exchanged
    /// with the server, plus a few client-only models at the end.
    /// Payload field names are lowercase on purpose so they map 1:1 to the server's JSON
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
            public string displayName;
            public int avatarId;
        }

        /// <summary>Every saved setting. Defaults match poker-server/src/services/profileRules.js.</summary>
        [Serializable]
        public class UserSettings
        {
            public bool musicOn = true;
            public bool soundOn = true;
            public float musicVolume = 0.6f;
            public float soundVolume = 0.8f;
            public bool breakReminders = true;
            public bool chatEnabled = true;
            public bool showHandStrength = true;
            public bool fourColorDeck;

            public UserSettings Copy() => (UserSettings)MemberwiseClone();
        }

        /// <summary>The logged-in player's own profile (GET /profile/me).</summary>
        [Serializable]
        public class UserProfile
        {
            public string playerId;
            public string username;     // login name, can't change
            public string displayName;  // shown at tables and in chat
            public int avatarId;
            public string bio;
            public long createdAt;      // ms since 1970
            public bool settingsSaved;  // false until settings were saved to the account once
            public UserSettings settings = new UserSettings();
        }

        /// <summary>PUT /profile. Null fields are left unchanged.</summary>
        [Serializable]
        public class ProfileUpdate
        {
            public string displayName;
            public int? avatarId;
            public string bio;
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
            public string tableCode; // the table you are seated at; null if none (e.g. the server restarted)
        }

        /// <summary>Your own statistics (the game's one retention feature).</summary>
        [Serializable]
        public class PlayerStats
        {
            public int handsPlayed;
            public int handsWon;
            public int biggestPot;
            public string bestHand;   // e.g. "Full House"; empty until a hand is shown at showdown
            public int bestHandRank = -1;
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
            public int avatarId;
            public int chips;
            public bool isBot;
            public bool connected;
        }

        [Serializable]
        public class YouInfo
        {
            public string playerId;
            public int seat;     // -1 while watching without a seat
            public bool waiting; // joined a bots table mid-hand; takes a bot's seat when the hand ends
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

        // ---------- Client-only models (never sent over the network) ----------

        /// <summary>One row of the hand-rankings hint chart (values in Constants.HandChart).</summary>
        public class HandChartEntry
        {
            public readonly int Rank;            // Constants.HandRanks value; name = HandRanks.Names[Rank]
            public readonly string Description;
            public readonly int MadeBy;          // how many of Cards form the hand (shown in gold)
            public readonly string[] Cards;      // example cards, server codes ("Ah", "Td")

            public HandChartEntry(int rank, string description, int madeBy, params string[] cards)
            {
                Rank = rank;
                Description = description;
                MadeBy = madeBy;
                Cards = cards;
            }
        }

        /// <summary>Invite to a table, as shared with friends.</summary>
        public class TableInvite
        {
            public string Code;
            public string Link;      // null when there is no web address to share
            public string Message;   // text without the link
        }

        // ---------- Audio ----------

        /// <summary>Every sound the game can play. Clips are assigned in the SoundLibrary asset.</summary>
        public enum SoundId
        {
            // Music (looping)
            MusicLobby,
            MusicTable,

            // Cards
            CardDeal,
            CardFlip,

            // Chips
            ChipBet,      // call / blinds
            ChipRaise,
            ChipAllIn,
            ChipCollect,  // bets slide into the pot
            ChipsWin,     // pot goes to the winner

            // Actions without chips
            Check,
            Fold,

            // Turn
            YourTurn,
            TimerTick,
            TimerUrgent,

            // Results and table events
            Win,
            Lose,
            PlayerJoined,
            GameStart,

            // UI
            ButtonClick,
            Error,
        }

        /// <summary>One sound in the SoundLibrary: one or more clips (picked at random) and how loud.</summary>
        [Serializable]
        public class SoundEntry
        {
            public SoundId id;
            public UnityEngine.AudioClip[] clips = new UnityEngine.AudioClip[0];
            [UnityEngine.Range(0f, 1f)] public float volume = 1f;
            [UnityEngine.Range(0f, 0.3f)] public float pitchJitter; // random pitch +/- this, so repeats don't sound robotic
        }
    }
}
