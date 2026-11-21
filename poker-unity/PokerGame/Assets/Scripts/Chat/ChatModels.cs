using System;
using System.Collections.Generic;

namespace Poker.Chat
{
    /// <summary>Chat payloads. Field names match the server JSON (poker-server/src/chat).</summary>
    public static class ChatModels
    {
        [Serializable]
        public class ChatMessage
        {
            public long id;
            public string kind;     // ChatConstants.Kinds
            public string playerId; // null for system messages
            public string name;     // sender's username (from their login, not chosen by the client)
            public string text;     // already cleaned by the server
            public long time;       // server time, ms since 1970
        }

        [Serializable]
        public class ChatHistory
        {
            public string code; // table the history belongs to; null = not at a table
            public List<ChatMessage> messages = new List<ChatMessage>();
        }

        [Serializable]
        public class ChatSendRequest
        {
            public string text;
        }

        [Serializable]
        public class ChatError
        {
            public string message;
        }
    }
}
