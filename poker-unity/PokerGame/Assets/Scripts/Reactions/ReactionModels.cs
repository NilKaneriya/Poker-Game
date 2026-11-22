using System;

namespace Poker.Reactions
{
    /// <summary>Payloads of the reaction events (JSON). Same shape as the server's.</summary>
    public static class ReactionModels
    {
        [Serializable]
        public class ReactionSendRequest
        {
            public string emoteId;
            public string targetId; // null = everyone
        }

        [Serializable]
        public class Reaction
        {
            public long id;
            public string emoteId;
            public string fromId;
            public string fromName;
            public string targetId; // null = everyone
            public long time;

            public bool HasTarget => !string.IsNullOrEmpty(targetId);
        }

        [Serializable]
        public class ReactionError
        {
            public string message;
        }

        /// <summary>Someone the local player can aim a reaction at.</summary>
        [Serializable]
        public struct ReactionTarget
        {
            public string id;
            public string name;

            public ReactionTarget(string id, string name)
            {
                this.id = id;
                this.name = name;
            }
        }
    }
}
