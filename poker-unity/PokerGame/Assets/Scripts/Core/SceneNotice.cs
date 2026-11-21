namespace Poker.Core
{
    /// <summary>
    /// A message to show on the next scene, e.g. "your table was closed" when the Game
    /// scene sends the player back to the Lobby.
    /// </summary>
    public static class SceneNotice
    {
        private static string pending;

        public static void Set(string message) => pending = message;

        /// <summary>The waiting message (or null), cleared once taken.</summary>
        public static string Take()
        {
            string message = pending;
            pending = null;
            return message;
        }
    }
}
