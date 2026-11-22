using System;
using System.Collections.Generic;
using UnityEngine;
using static Poker.Reactions.ReactionModels;

namespace Poker.Reactions
{
    /// <summary>
    /// How the module talks to its server. The host app implements this over whatever
    /// socket it already has, so the module itself has no network code of its own.
    /// </summary>
    public interface IReactionTransport
    {
        bool IsConnected { get; }
        /// <summary>The local user's id (to tell "my" reactions apart).</summary>
        string LocalUserId { get; }
        void Send(string eventName, object payload);
        void Listen<T>(string eventName, Action<T> handler);
    }

    /// <summary>
    /// What the host screen offers the module: where each person is drawn, and who can be
    /// aimed at. A poker table answers with its seats; a lobby or a chat room could answer
    /// with avatars in a list.
    /// </summary>
    public interface IReactionScene
    {
        /// <summary>Where to show reactions from / to this user. False if they are not on screen.</summary>
        bool TryGetAnchor(string userId, out RectTransform anchor);

        /// <summary>Other people the local user can send a reaction to (not including themself).</summary>
        IReadOnlyList<ReactionTarget> Targets { get; }
    }
}
