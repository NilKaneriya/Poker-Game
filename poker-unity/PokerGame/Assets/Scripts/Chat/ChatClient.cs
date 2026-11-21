using System;
using Poker.Network;
using static Poker.Chat.ChatConstants;
using static Poker.Chat.ChatModels;

namespace Poker.Chat
{
    /// <summary>
    /// The chat connection. Uses the app's socket only as a transport (NetworkManager.Listen /
    /// Send); it knows nothing about poker. Created on first use, lives as long as the socket.
    /// </summary>
    public sealed class ChatClient
    {
        public event Action<ChatMessage> MessageReceived;
        public event Action<ChatHistory> HistoryReceived;
        public event Action<string> ErrorReceived;

        private static ChatClient instance;
        private readonly NetworkManager net;

        /// <summary>The chat client, or null before the network exists (e.g. a scene opened directly in the editor).</summary>
        public static ChatClient Instance
        {
            get
            {
                var current = NetworkManager.Instance;
                if (current == null) return null;
                if (instance == null || instance.net != current) instance = new ChatClient(current);
                return instance;
            }
        }

        private ChatClient(NetworkManager net)
        {
            this.net = net;
            net.Listen<ChatMessage>(Events.Message, m => MessageReceived?.Invoke(m));
            net.Listen<ChatHistory>(Events.History, h => HistoryReceived?.Invoke(h));
            net.Listen<ChatError>(Events.Error, e => ErrorReceived?.Invoke(e?.message));
        }

        public bool IsConnected => net != null && net.IsConnected;

        public void Send(string text)
        {
            text = text?.Trim();
            if (string.IsNullOrEmpty(text)) return;
            if (text.Length > Limits.MaxLength) text = text.Substring(0, Limits.MaxLength);
            net.Send(Events.Send, new ChatSendRequest { text = text });
        }

        public void RequestHistory() => net.Send(Events.GetHistory);
    }
}
