using System;
using System.Collections.Generic;
using static Poker.Reactions.ReactionConstants;
using static Poker.Reactions.ReactionModels;

namespace Poker.Reactions
{
    /// <summary>
    /// Sends reactions and raises an event for each one the server broadcasts.
    /// One client per transport: listeners are registered once, so scenes can come and go.
    /// </summary>
    public sealed class ReactionClient
    {
        public event Action<Reaction> Received;
        public event Action<string> ErrorReceived;

        private static readonly Dictionary<IReactionTransport, ReactionClient> clients = new Dictionary<IReactionTransport, ReactionClient>();
        private readonly IReactionTransport transport;

        /// <summary>The client for this transport (created on first use).</summary>
        public static ReactionClient For(IReactionTransport transport)
        {
            if (transport == null) return null;
            if (!clients.TryGetValue(transport, out var client))
            {
                client = new ReactionClient(transport);
                clients[transport] = client;
            }
            return client;
        }

        private ReactionClient(IReactionTransport transport)
        {
            this.transport = transport;
            transport.Listen<Reaction>(Events.Reaction, r => { if (r != null) Received?.Invoke(r); });
            transport.Listen<ReactionError>(Events.Error, e => ErrorReceived?.Invoke(e?.message));
        }

        public bool IsConnected => transport.IsConnected;
        public string LocalUserId => transport.LocalUserId;

        /// <param name="targetId">Another user, or null to react to everyone.</param>
        public void Send(string emoteId, string targetId = null)
        {
            if (string.IsNullOrEmpty(emoteId) || Array.IndexOf(Emotes.Ids, emoteId) < 0) return;
            transport.Send(Events.Send, new ReactionSendRequest { emoteId = emoteId, targetId = targetId });
        }
    }
}
