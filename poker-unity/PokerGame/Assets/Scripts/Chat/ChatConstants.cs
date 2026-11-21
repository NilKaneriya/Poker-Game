using UnityEngine;

namespace Poker.Chat
{
    /// <summary>
    /// Everything fixed about chat, kept inside the chat module so the module stands alone.
    /// Event names must match poker-server/src/chat/chatEvents.js.
    /// </summary>
    public static class ChatConstants
    {
        public static class Events
        {
            // Client -> Server
            public const string Send = "chatSend";
            public const string GetHistory = "getChat";

            // Server -> Client
            public const string Message = "chatMessage";
            public const string History = "chatHistory";
            public const string Error = "chatError";
        }

        public static class Limits
        {
            public const int MaxLength = 140;      // same as the server (it cuts longer messages)
            public const int MaxRowsShown = 50;    // oldest rows are removed beyond this
            public const int MaxUnreadShown = 9;   // badge shows "9+" above this
        }

        public static class Kinds
        {
            public const string Player = "player";
            public const string System = "system";
        }

        public static class Texts
        {
            public const string Title = "Table chat";
            public const string Button = "Chat";
            public const string Placeholder = "Say something nice...";
            public const string Send = "Send";
            public const string Empty = "No messages yet. Say hi!";
            public const string UnreadMany = "9+";

            /// <summary>One-tap messages (quick chat), handy on phones.</summary>
            public static readonly string[] QuickPhrases = { "Good luck!", "Nice hand!", "Well played", "gg" };

            // Row formats: {0} = name, {1} = escaped message
            public const string MineRow = "<color=#F6C744><b>You</b></color>  {1}";
            public const string OtherRow = "<color=#7CB4FF><b>{0}</b></color>  {1}";
            public const string SystemRow = "<color=#94A3B8><i>{1}</i></color>";
        }

        public static class Colors
        {
            public static readonly Color UnreadBadge = new Color32(229, 72, 77, 255);
        }
    }
}
