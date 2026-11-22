using System;
using Poker.Network;
using Poker.Reactions;

namespace Poker.UI.Game
{
    /// <summary>
    /// Adapter: lets the reactions module use this game's socket (NetworkManager) and login.
    /// This is the only place that knows both sides; the module itself has no game code.
    /// </summary>
    public sealed class NetworkReactionTransport : IReactionTransport
    {
        private static NetworkReactionTransport current;
        private readonly NetworkManager net;

        /// <summary>The transport over the app's socket, or null before the network exists (offline preview).</summary>
        public static NetworkReactionTransport Instance
        {
            get
            {
                var net = NetworkManager.Instance;
                if (net == null) return null;
                if (current == null || current.net != net) current = new NetworkReactionTransport(net);
                return current;
            }
        }

        private NetworkReactionTransport(NetworkManager net) => this.net = net;

        public bool IsConnected => net != null && net.IsConnected;
        public string LocalUserId => AuthManager.Instance != null ? AuthManager.Instance.PlayerId : null;
        public void Send(string eventName, object payload) => net.Send(eventName, payload);
        public void Listen<T>(string eventName, Action<T> handler) => net.Listen(eventName, handler);
    }
}
