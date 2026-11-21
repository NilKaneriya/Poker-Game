using static Poker.Chat.ChatConstants;
using static Poker.Chat.ChatModels;

namespace Poker.Chat
{
    /// <summary>
    /// Turns a chat message into one line of TextMeshPro rich text. Player text is
    /// shown literally: tags like &lt;size=500&gt; typed by a player are not applied.
    /// </summary>
    public static class ChatMessageFormat
    {
        public static string Row(ChatMessage message, string myPlayerId)
        {
            string text = Literal(message.text);
            if (message.kind == Kinds.System) return string.Format(Texts.SystemRow, "", text);
            bool mine = !string.IsNullOrEmpty(myPlayerId) && message.playerId == myPlayerId;
            return string.Format(mine ? Texts.MineRow : Texts.OtherRow, Literal(message.name), text);
        }

        /// <summary>Wrap in &lt;noparse&gt; so TMP shows tags as plain text; strip anything that could close it early.</summary>
        public static string Literal(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            string safe = System.Text.RegularExpressions.Regex.Replace(value, "</?noparse>", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            return "<noparse>" + safe + "</noparse>";
        }
    }
}
