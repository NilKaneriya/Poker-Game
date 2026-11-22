namespace Poker.Reactions
{
    /// <summary>
    /// Everything fixed about reactions, kept inside the module so it stands alone.
    /// Event names and emote ids must match poker-server/src/reactions.
    /// </summary>
    public static class ReactionConstants
    {
        public static class Events
        {
            // Client -> Server
            public const string Send = "reactionSend";

            // Server -> Client
            public const string Reaction = "reaction";
            public const string Error = "reactionError";
        }

        public static class Emotes
        {
            /// <summary>Same ids, same order as the server's emotes.js. Art: Assets/Art/Reactions/emote_{id}.png</summary>
            public static readonly string[] Ids =
            {
                "laugh", "wow", "cool", "love", "angry", "cry",
                "wink", "sleepy", "heart", "fire", "coin", "gg",
            };
        }

        public static class Timings
        {
            public const float PopSeconds = 0.25f;     // grow in
            public const float FlySeconds = 0.65f;     // from sender to target
            public const float HoldSeconds = 1.4f;     // stays visible
            public const float FadeSeconds = 0.35f;    // fades out while drifting up
            public const float FlyArcHeight = 160f;    // how high a thrown emote arcs (canvas units)
            public const float DriftUp = 50f;
            public const int MaxOnScreen = 10;         // more at once and the oldest go first
        }

        public static class Texts
        {
            public const string Button = "Emotes";
            public const string Title = "Send a reaction";
            public const string Everyone = "Everyone";
            public const string ToPlayer = "Send to {0}";
        }
    }
}
