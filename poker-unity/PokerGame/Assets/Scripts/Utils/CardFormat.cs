namespace Poker.Core
{
    /// <summary>
    /// Turns server card codes ("Ah", "Td") into what the UI shows ("A", "10", ♥).
    /// </summary>
    public static class CardFormat
    {
        public static bool TryParse(string code, out string rank, out string suit, out bool isRed)
        {
            rank = suit = "";
            isRed = false;
            if (string.IsNullOrEmpty(code) || code.Length != 2) return false;

            rank = code[0] == 'T' ? "10" : code[0].ToString();
            switch (code[1])
            {
                case 'h': suit = "♥"; isRed = true; break;  // ♥
                case 'd': suit = "♦"; isRed = true; break;  // ♦
                case 'c': suit = "♣"; break;                // ♣
                case 's': suit = "♠"; break;                // ♠
                default: return false;
            }
            return true;
        }

        /// <summary>Chip amounts with thousands separators: 12500 -> "12,500".</summary>
        public static string Chips(int amount) => amount.ToString("N0");
    }
}
