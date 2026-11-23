using System.Collections.Generic;
using UnityEngine;
using static Poker.Network.DataModel;

namespace Poker.Core
{
    /// <summary>
    /// Every constant and every piece of static data used by the game, grouped by topic.
    /// Data shapes live in Poker.Network.DataModel; this file only holds values.
    /// Use with: using static Poker.Core.Constants; then write e.g. PokerEvents.Session.
    /// </summary>
    public static class Constants
    {
        // =====================================================================
        // App, network, storage
        // =====================================================================

        /// <summary>Scene names as listed in Build Settings.</summary>
        public static class SceneNames
        {
            public const string Splash = "Splash";
            public const string Lobby = "Lobby";
            public const string Game = "Game";
        }

        /// <summary>HTTP paths of the login endpoints (see poker-server/src/network/authRoutes.js).</summary>
        public static class ApiRoutes
        {
            public const string Login = "/auth/login";
            public const string Register = "/auth/register";
            public const string Me = "/auth/me";
            public const string Logout = "/auth/logout";

            // Profile and settings (see poker-server/src/network/profileRoutes.js)
            public const string MyProfile = "/profile/me";
            public const string Profile = "/profile";
            public const string Settings = "/profile/settings";
        }

        /// <summary>Socket.IO event names. Must match poker-server/src/network/events.js.</summary>
        public static class PokerEvents
        {
            // Client -> Server
            public const string CreateTable = "createTable";
            public const string JoinTable = "joinTable";
            public const string LeaveTable = "leaveTable";
            public const string StartGame = "startGame";
            public const string GetStats = "getStats";
            public const string GetHandHistory = "getHandHistory";
            public const string ListTables = "listTables";
            public const string PlayerAction = "playerAction";
            public const string Rebuy = "rebuy";

            // Server -> Client
            public const string Session = "session";
            public const string SignedInElsewhere = "signedInElsewhere";
            public const string TableList = "tableList";
            public const string TableState = "tableState";
            public const string Stats = "stats";
            public const string HandHistory = "handHistory";
            public const string ActionRejected = "actionRejected";
            public const string Error = "errorMessage";

            // Built-in Socket.IO events
            public const string SocketConnect = "connect";
            public const string SocketDisconnect = "disconnect";
            public const string SocketConnectError = "connect_error";
        }

        /// <summary>Local storage keys (PlayerPrefs).</summary>
        public static class PrefsKeys
        {
            public const string AuthToken = "poker.authToken";
            public const string CreateMode = "poker.create.mode";   // last picked TableModes value
            public const string CreateSeats = "poker.create.seats"; // last picked table size
            public const string MusicOn = "poker.audio.music";      // 1 = on (default), 0 = off
            public const string SoundOn = "poker.audio.sound";
            public const string BreakReminders = "poker.wellbeing.breakReminders"; // 1 = on (default), 0 = off
            public const string MusicVolume = "poker.audio.musicVolume";       // 0..1
            public const string SoundVolume = "poker.audio.soundVolume";       // 0..1
            public const string ChatEnabled = "poker.chat.enabled";
            public const string ShowHandStrength = "poker.table.showHandStrength";
            public const string FourColorDeck = "poker.table.fourColorDeck";
        }

        /// <summary>Settings sync with the server.</summary>
        public static class SettingsSync
        {
            public const float SaveDelaySeconds = 1f; // dragging a slider sends one request, not fifty
        }

        /// <summary>
        /// Player avatars: a colour and a card-suit symbol. Index = avatarId saved in the profile
        /// (poker-server/src/services/profileRules.js AVATAR_COUNT must match Count).
        /// </summary>
        public static class Avatars
        {
            public static readonly Color[] Colors =
            {
                new Color32(239, 108, 87, 255), new Color32(76, 154, 242, 255), new Color32(155, 117, 232, 255),
                new Color32(62, 196, 145, 255), new Color32(246, 182, 62, 255), new Color32(230, 104, 168, 255),
                new Color32(43, 182, 196, 255), new Color32(214, 84, 84, 255), new Color32(120, 150, 70, 255),
                new Color32(232, 140, 50, 255), new Color32(96, 110, 220, 255), new Color32(160, 160, 170, 255),
            };
            public static readonly string[] Symbols =
            {
                "\u2660", "\u2665", "\u2666", "\u2663", "\u2660", "\u2665",
                "\u2666", "\u2663", "\u2660", "\u2665", "\u2666", "\u2663",
            };
            public static int Count => Colors.Length;
            public static int Clamp(int avatarId) => avatarId >= 0 && avatarId < Count ? avatarId : 0;
        }

        /// <summary>Four-colour deck (setting): each suit its own colour, easier to read at a glance.</summary>
        public static class SuitColors
        {
            public static readonly Color Red = new Color(0.86f, 0.15f, 0.2f);
            public static readonly Color Black = new Color(0.1f, 0.12f, 0.16f);
            public static readonly Color Blue = new Color32(37, 99, 235, 255);   // diamonds
            public static readonly Color Green = new Color32(22, 140, 70, 255);  // clubs
        }

        /// <summary>Responsible play: a gentle reminder after a long session (can be turned off).</summary>
        public static class Wellbeing
        {
            public const float FirstReminderMinutes = 60f;
            public const float RepeatReminderMinutes = 60f;
        }

        /// <summary>Network timeouts.</summary>
        public static class Timeouts
        {
            public const int HttpSeconds = 10;
            public const float SocketConnectSeconds = 10f;
        }

        /// <summary>Invite links: https://your-site/poker/?room=ABCDE</summary>
        public static class InviteLinks
        {
            public const string QueryKey = "room";
        }

        // =====================================================================
        // Poker rules (must match the server)
        // =====================================================================

        /// <summary>Action ids sent in playerAction. Must match engine/constants.js.</summary>
        public static class PokerActions
        {
            public const string Fold = "fold";
            public const string Check = "check";
            public const string Call = "call";
            public const string Raise = "raise";
            public const string AllIn = "allIn";
        }

        /// <summary>Who fills the table. Must match TABLE_MODES in engine/constants.js.</summary>
        public static class TableModes
        {
            public const string Bots = "bots";       // you vs computer players, starts at once
            public const string Friends = "friends"; // humans only, waits until every seat is taken

            /// <summary>Order of the lobby's mode buttons.</summary>
            public static readonly string[] LobbyOrder = { Bots, Friends };
        }

        /// <summary>Hand stages. Must match engine/constants.js.</summary>
        public static class PokerStages
        {
            public const string Preflop = "preflop";
            public const string Flop = "flop";
            public const string Turn = "turn";
            public const string River = "river";
            public const string Showdown = "showdown";

            /// <summary>Small label under the board.</summary>
            public static readonly IReadOnlyDictionary<string, string> Labels = new Dictionary<string, string>
            {
                { Preflop, "PRE-FLOP" }, { Flop, "FLOP" }, { Turn, "TURN" }, { River, "RIVER" }, { Showdown, "SHOWDOWN" },
            };

            /// <summary>Banner shown for a moment when the street starts.</summary>
            public static readonly IReadOnlyDictionary<string, string> Announcements = new Dictionary<string, string>
            {
                { Preflop, "PRE-FLOP" }, { Flop, "THE FLOP" }, { Turn, "THE TURN" }, { River, "THE RIVER" }, { Showdown, "SHOWDOWN" },
            };
        }

        /// <summary>Hand ranks, weakest first. Must match HAND_RANKS / HAND_NAMES in engine/constants.js.</summary>
        public static class HandRanks
        {
            public const int HighCard = 0, OnePair = 1, TwoPair = 2, ThreeOfAKind = 3, Straight = 4,
                Flush = 5, FullHouse = 6, FourOfAKind = 7, StraightFlush = 8, RoyalFlush = 9;

            public static readonly string[] Names =
            {
                "High Card", "One Pair", "Two Pair", "Three of a Kind", "Straight",
                "Flush", "Full House", "Four of a Kind", "Straight Flush", "Royal Flush",
            };
        }

        /// <summary>Gameplay values shared by the UI.</summary>
        public static class GameRules
        {
            public const int StartingChips = 1000;
            public const int TableCodeLength = 5;
            public const int MinSeats = 2; // table sizes the host can pick (server: config.table)
            public const int MaxSeats = 6;
            public const int DefaultSeats = 4;
        }

        // =====================================================================
        // Hint chart (Hand rankings panel)
        // =====================================================================

        public static class HandChart
        {
            /// <summary>The 10 hands, best first. Example cards list the cards that make the hand first.</summary>
            public static readonly HandChartEntry[] Entries =
            {
                new HandChartEntry(HandRanks.RoyalFlush, "A, K, Q, J, 10, all the same suit.", 5, "As", "Ks", "Qs", "Js", "Ts"),
                new HandChartEntry(HandRanks.StraightFlush, "Five cards in a row, all the same suit.", 5, "9h", "8h", "7h", "6h", "5h"),
                new HandChartEntry(HandRanks.FourOfAKind, "Four cards of the same rank.", 4, "Qc", "Qd", "Qh", "Qs", "7d"),
                new HandChartEntry(HandRanks.FullHouse, "Three of a kind plus a pair.", 5, "Kh", "Kd", "Kc", "4s", "4h"),
                new HandChartEntry(HandRanks.Flush, "Any five cards of the same suit.", 5, "Ad", "Jd", "8d", "6d", "2d"),
                new HandChartEntry(HandRanks.Straight, "Five in a row, any suits. A-2-3-4-5 counts too.", 5, "Tc", "9d", "8s", "7h", "6c"),
                new HandChartEntry(HandRanks.ThreeOfAKind, "Three cards of the same rank.", 3, "7s", "7h", "7d", "Kc", "2s"),
                new HandChartEntry(HandRanks.TwoPair, "Two different pairs.", 4, "Jh", "Js", "5c", "5d", "Ah"),
                new HandChartEntry(HandRanks.OnePair, "Two cards of the same rank.", 2, "Ah", "Ad", "Kc", "9s", "4h"),
                new HandChartEntry(HandRanks.HighCard, "No combination: your highest card plays.", 1, "Ac", "Jd", "8h", "5s", "3c"),
            };

            public const int CardsPerEntry = 5;

            /// <summary>Row in Entries for a hand rank (rows are best first).</summary>
            public static int RowOf(int handRank) => Entries.Length - 1 - handRank;

            public const string HowToPlay =
                "<b>A hand of Texas Hold'em</b>\n" +
                "•  You get 2 private cards. Up to 5 shared cards are dealt face up on the table.\n" +
                "•  Make your best 5-card hand from any of those 7 cards.\n" +
                "•  Betting rounds: Pre-flop, Flop (3 cards), Turn (1 card), River (1 card).\n" +
                "•  On your turn: Fold, Check, Call, Raise or All-in. You have 30 seconds.\n" +
                "•  Win the pot with the best hand at showdown, or by being the last player in.\n\n" +
                "<b>Ties</b>\n" +
                "•  Same hand type? Higher cards win: a pair of Kings beats a pair of Queens.\n" +
                "•  Still equal? The next highest card (the kicker) decides.\n" +
                "•  Exactly equal hands split the pot. Suits never break a tie.";
        }

        // =====================================================================
        // Table layout and animation
        // =====================================================================

        public static class TableLayout
        {
            /// <summary>Seat views on screen, clockwise from the bottom (you).</summary>
            public const int ScreenSlots = 6;

            /// <summary>
            /// Screen slots used for each table size (index = seat count). Slots:
            /// 0 bottom, 1 lower left, 2 upper left, 3 top, 4 upper right, 5 lower right.
            /// </summary>
            public static readonly int[][] SlotsBySeatCount =
            {
                new[] { 0 },
                new[] { 0 },
                new[] { 0, 3 },             // heads-up: face to face
                new[] { 0, 2, 4 },
                new[] { 0, 1, 3, 5 },
                new[] { 0, 1, 2, 4, 5 },
                new[] { 0, 1, 2, 3, 4, 5 },
            };
        }

        /// <summary>Durations in seconds and sizes in reference pixels (1920x1080).</summary>
        public static class AnimationTimings
        {
            // Dealing hole cards
            public const float DealFirstSeatDelay = 0.3f;
            public const float DealSeatStagger = 0.12f;
            public const float DealCardStagger = 0.18f;
            public const float DealFlight = 0.35f;

            // Flying chips
            public const float ChipStagger = 0.05f;
            public const float ChipAppear = 0.1f;
            public const float ChipFlight = 0.45f;
            public const float ChipVanish = 0.12f;
            public const float ChipJumpHeight = 50f;
            public const float ChipStartJitter = 10f;
            public const float ChipEndJitter = 6f;
            public const float CollectToPotTime = 0.6f;  // later chip moves wait for bets to reach the pot
            public const float PotToWinnerDelay = 0.15f;

            // Street banner
            public const float BannerFadeIn = 0.2f;
            public const float BannerPop = 0.35f;
            public const float BannerHold = 0.9f;
            public const float BannerFadeOut = 0.35f;
        }

        /// <summary>How many chips fly for an amount.</summary>
        public static class ChipFlights
        {
            public const int MinChips = 2;
            public const int MaxChips = 7;
            public const int PotToWinnerChips = 8;
        }

        // =====================================================================
        // Audio
        // =====================================================================

        public static class AudioConfig
        {
            public const string LibraryResource = "SoundLibrary"; // Resources/SoundLibrary.asset
            public const int EffectVoices = 16;                   // sound effects that can play at once
            public const float MusicFadeSeconds = 1.2f;            // crossfade between scene tracks
        }

        /// <summary>When sounds play, in seconds (lined up with AnimationTimings).</summary>
        public static class SoundCueTimings
        {
            public const int TimerAlertSeconds = 5;   // ticking starts with this many seconds left on your turn
            public const int TimerUrgentSeconds = 3;  // ...and turns urgent here
            public const float BoardFlipDelay = 0.35f;
            public const float BoardFlipStagger = 0.12f;
            public const float YourTurnDelay = 0.2f;
            public const float CollectDelay = 0.25f;
            public const float WinChipsDelay = AnimationTimings.CollectToPotTime + AnimationTimings.PotToWinnerDelay + AnimationTimings.ChipFlight;
            public const float ResultJingleDelay = 0.9f;
            public const int MaxDealSounds = 12;
        }

        /// <summary>Default mix used when the SoundLibrary is generated (adjustable in the asset afterwards).</summary>
        public static class SoundDefaults
        {
            public static readonly IReadOnlyDictionary<SoundId, float> Volume = new Dictionary<SoundId, float>
            {
                { SoundId.MusicLobby, 0.35f }, { SoundId.MusicTable, 0.28f },
                { SoundId.CardDeal, 0.55f }, { SoundId.CardFlip, 0.6f },
                { SoundId.ChipBet, 0.7f }, { SoundId.ChipRaise, 0.75f }, { SoundId.ChipAllIn, 0.85f },
                { SoundId.ChipCollect, 0.6f }, { SoundId.ChipsWin, 0.75f },
                { SoundId.Check, 0.7f }, { SoundId.Fold, 0.5f },
                { SoundId.YourTurn, 0.6f }, { SoundId.TimerTick, 0.45f }, { SoundId.TimerUrgent, 0.6f },
                { SoundId.Win, 0.6f }, { SoundId.Lose, 0.45f }, { SoundId.PlayerJoined, 0.55f }, { SoundId.GameStart, 0.6f },
                { SoundId.ButtonClick, 0.35f }, { SoundId.Error, 0.5f },
            };

            /// <summary>Sounds that repeat a lot get a little random pitch so they don't sound robotic.</summary>
            public static readonly IReadOnlyDictionary<SoundId, float> PitchJitter = new Dictionary<SoundId, float>
            {
                { SoundId.CardDeal, 0.08f }, { SoundId.CardFlip, 0.06f }, { SoundId.ChipBet, 0.07f },
                { SoundId.ChipRaise, 0.05f }, { SoundId.ChipCollect, 0.05f }, { SoundId.Check, 0.05f },
                { SoundId.ButtonClick, 0.05f },
            };

            public static bool IsMusic(SoundId id) => id == SoundId.MusicLobby || id == SoundId.MusicTable;
        }

        /// <summary>Labels of the settings toggle buttons; {0} = On / Off.</summary>
        public static class SettingTexts
        {
            public const string Music = "♪ Music";
            public const string Sound = "Sound";
            public const string BreakReminders = "Break reminders: {0}";
            public const string On = "On";
            public const string Off = "Off";

            // Settings panel
            public const string Button = "Settings";
            public const string Title = "Settings";
            public const string AudioSection = "Audio";
            public const string TableSection = "Table";
            public const string PlaySection = "Responsible play";
            public const string MusicVolume = "Music";
            public const string SoundVolume = "Sound effects";
            public const string ShowHandStrength = "Show my hand strength";
            public const string FourColorDeck = "Four-colour deck";
            public const string ChatEnabled = "Table chat";
            public const string BreakReminderToggle = "Remind me to take a break";
            public const string SyncedHint = "Saved to your account, so they follow you to any device.";
            public const string LocalHint = "Saved on this device. Log in to keep them on your account.";
            public const string Close = "Done";
        }

        public static class ProfileTexts
        {
            public const string Title = "Your profile";
            public const string Edit = "Edit profile";
            public const string DisplayName = "Display name";
            public const string DisplayNameHint = "How others see you at the table";
            public const string Bio = "About you";
            public const string BioHint = "A short line, e.g. favourite hand (optional)";
            public const string Avatar = "Avatar";
            public const string Save = "Save";
            public const string Saving = "Saving...";
            public const string Saved = "Profile saved";
            public const string Loading = "Loading...";
            public const string Username = "Login name: <b>{0}</b>";
            public const string MemberSince = "Playing since {0:MMMM yyyy}";
            public const int DisplayNameMax = 16;
            public const int BioMax = 80;
        }

        public static class WellbeingTexts
        {
            public const string Title = "Time for a break?";
            public const string Body = "You've been playing for {0}. Your chips will be right here when you come back.";
            public const string KeepPlaying = "Keep playing";
            public const string TurnOff = "Don't remind me";
            public const string Hours = "{0}h {1:00}m";
            public const string Minutes = "{0} minutes";
        }

        // =====================================================================
        // Colours (the Editor's Theme uses these too)
        // =====================================================================

        public static class UIColors
        {
            public static readonly Color Gold = new Color32(246, 199, 68, 255);
            public static readonly Color PanelLight = new Color32(38, 53, 90, 255);
            public static readonly Color Dark = new Color32(11, 18, 32, 255);
            public static readonly Color Text = new Color32(243, 246, 251, 255);
            public static readonly Color Red = new Color32(229, 72, 77, 255);
            public static readonly Color Blue = new Color32(59, 130, 246, 255);
            public static readonly Color Green = new Color32(34, 197, 94, 255);

            /// <summary>Hand-chart row of the player's current hand.</summary>
            public static readonly Color MarkedRow = new Color32(246, 199, 68, 70);

            public static readonly Color[] ChipPalette = { Red, Blue, Green, Gold, new Color32(30, 30, 36, 255) };
        }

        // =====================================================================
        // Texts
        // =====================================================================

        /// <summary>Messages shown in the Lobby.</summary>
        public static class LobbyTexts
        {
            public const string BotsHint = "You + {0} bot{1}. The game starts right away.";
            public const string FriendsHint = "You get a room code to share. The game starts when all {0} seats are filled.";
            public const string JoiningInvite = "Joining table {0}...";
            public const string BadCodeLength = "Table codes have {0} letters.";
            public const string TableWaiting = "  •  <color=#22C55E>Waiting for players</color>";
            public const string TableInHand = "  •  Hand in progress";
            public const string FreeToPlay = "Virtual chips only  •  No real money  •  No purchases";

            // Your stats
            public const string StatsTitle = "Your stats";
            public const string HandsPlayed = "Hands played";
            public const string HandsWon = "Hands won";
            public const string BiggestPot = "Biggest pot";
            public const string BestHand = "Best hand";
            public const string NoValue = "-";
        }

        /// <summary>Hand history panel (Lobby and Game).</summary>
        public static class HistoryTexts
        {
            public const string Button = "Hand history";
            public const string ButtonShort = "History";
            public const string Title = "Hand history";
            public const string Loading = "Loading your hands...";
            public const string Empty = "No hands yet.\nPlay a hand and it shows up here.";
            public const string PickHand = "Pick a hand on the left to see how it played out.";
            public const string Offline = "Connect to see your hand history.";

            // List rows
            public const string RowTitle = "Hand #{0}  •  {1}";
            public const string RowFolded = "Folded";

            // Detail
            public const string DetailTitle = "Hand #{0}  •  {1}";
            public const string DetailInfo = "Blinds {0}/{1}  •  {2}  •  Pot {3}";
            public const string Board = "BOARD";
            public const string YourCards = "YOUR CARDS";
            public const string NoBoard = "No board: the hand ended pre-flop";
            public const string Showdown = "SHOWDOWN";
            public const string Result = "RESULT";
            public const string Today = "Today {0}";

            // Your result
            public const string YouWon = "<color=#22C55E><b>You won {0}</b></color>";
            public const string YouLost = "<color=#E5484D><b>You lost {0}</b></color>";
            public const string YouFolded = "<color=#94A3B8><b>You folded</b></color>";
            public const string YouEven = "<b>You broke even</b>";
            public const string WithHand = " with {0}";

            public static string Street(string stage) => stage switch
            {
                "preflop" => "PRE-FLOP",
                "flop" => "FLOP",
                "turn" => "TURN",
                "river" => "RIVER",
                _ => (stage ?? "").ToUpperInvariant(),
            };

            // Actions: {0} = name, {1} = amount, {2} = total
            public const string Sb = "{0} posts small blind {1}";
            public const string Bb = "{0} posts big blind {1}";
            public const string Fold = "{0} folds";
            public const string Check = "{0} checks";
            public const string Call = "{0} calls {1}";
            public const string Bet = "{0} bets {2}";
            public const string Raise = "{0} raises to {2}";
            public const string AllIn = "{0} is all-in for {2}";
            public const string Returned = "{1} uncalled returned to {0}";
            public const string TimedOut = " <color=#94A3B8>(time ran out)</color>";
            public const string Left = " <color=#94A3B8>(left the table)</color>";
            public const string Won = "{0} wins {1}";
            public const string WonWith = "{0} wins {1} with {2}";
            public const string You = "<color=#F6C744><b>You</b></color>";
            public const string Name = "<color=#7CB4FF><b>{0}</b></color>";
        }

        /// <summary>Messages shown at the table.</summary>
        public static class GameTexts
        {
            // Middle of the table between hands
            public const string WaitingForPlayers = "Waiting for players...\nShare code <b>{0}</b> with friends";
            public const string FirstHandComing = "Everyone's here!\nDealing the first hand...";
            public const string NextHandComing = "Next hand starting...";

            // Your current hand (hand chart + pill under your chips)
            public const string HandNotDealt = "Your hand shows here once cards are dealt";
            public const string HandWatching = "You're watching this hand";
            public const string HandFolded = "You folded this hand";
            public const string YourHand = "Your hand: <color=#F6C744><b>{0}</b></color>";
            public const string DealtInNextHand = "Dealt in next hand";

            // Joined a game against bots mid-hand: watch, then take a bot's seat
            public const string WaitingForSeat = "Hand in progress - you'll take a seat when it ends";
            public const string HandWaitingForSeat = "You'll take a bot's seat when this hand ends";
            public const string TookBotSeat = "You're in! A bot gave up its seat for you";
            public const float WaitingForSeatToastSeconds = 8f;

            // End of hand with more than one winner (split pot or side pots)
            public const string SplitPot = "Split pot";
            public const string PotsWon = "Pots won";
            public const string WinnerShare = "{0} +{1}";
            public const string WinnerShareWithHand = "{0} +{1} ({2})";

            // Connection
            public const string ConnectionLost = "Connection lost. Reconnecting... your seat and chips are kept for 2 minutes.";
            public const string TableClosed = "The server restarted, so your table was closed. The hand in progress was not saved. Create or join a table to keep playing.";

            // Waiting room
            public const string RoomStatus = "<color=#F6C744><b>{0}</b></color> of {1} players at the table";
            public const string RoomHostCanStart = "Starts when {0}, or start now.";
            public const string RoomHostWaits = "Starts when {0}.";
            public const string RoomGuestWaits = "Starts when {0}, or when the host starts it.";
            public const string OneMorePlayer = "1 more player joins";
            public const string MorePlayers = "{0} more players join";

            // Invites
            public const string InviteTitle = "Play poker with me";
            public const string InviteMessage = "Join my poker table \"{0}\"! Room code: {1}";
            public const string CodeCopied = "Code {0} copied - share it with friends";
            public const string LinkCopied = "Invite link copied - friends who open it join this table";
            public const string InviteCopied = "Invite copied - paste it to your friends";
        }
    }
}
