namespace Poker.Core
{
    /// <summary>
    /// Every constant used by the game, grouped by topic.
    /// Use with: using static Poker.Core.Constants; then write e.g. PokerEvents.Session.
    /// </summary>
    public static class Constants
    {
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
        }

        /// <summary>Socket.IO event names. Must match poker-server/src/network/events.js.</summary>
        public static class PokerEvents
        {
            // Client -> Server
            public const string CreateTable = "createTable";
            public const string JoinTable = "joinTable";
            public const string LeaveTable = "leaveTable";
            public const string StartGame = "startGame";
            public const string ListTables = "listTables";
            public const string PlayerAction = "playerAction";
            public const string Rebuy = "rebuy";

            // Server -> Client
            public const string Session = "session";
            public const string TableList = "tableList";
            public const string TableState = "tableState";
            public const string ActionRejected = "actionRejected";
            public const string Error = "errorMessage";

            // Built-in Socket.IO events
            public const string SocketConnect = "connect";
            public const string SocketDisconnect = "disconnect";
            public const string SocketConnectError = "connect_error";
        }

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
        }

        /// <summary>Hand stages. Must match engine/constants.js.</summary>
        public static class PokerStages
        {
            public const string Preflop = "preflop";
            public const string Flop = "flop";
            public const string Turn = "turn";
            public const string River = "river";
            public const string Showdown = "showdown";
        }

        /// <summary>Local storage keys (PlayerPrefs).</summary>
        public static class PrefsKeys
        {
            public const string AuthToken = "poker.authToken";
            public const string CreateMode = "poker.create.mode";   // last picked TableModes value
            public const string CreateSeats = "poker.create.seats"; // last picked table size
        }

        /// <summary>Network timeouts.</summary>
        public static class Timeouts
        {
            public const int HttpSeconds = 10;
            public const float SocketConnectSeconds = 10f;
        }

        /// <summary>Gameplay values shared by the UI.</summary>
        public static class GameRules
        {
            public const int StartingChips = 1000;
            public const int TableCodeLength = 5;
            public const int MinSeats = 2; // table sizes the host can pick (server: config.table)
            public const int MaxSeats = 6;
        }
    }
}
