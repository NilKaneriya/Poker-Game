using System;
using Poker.Core;
using static Poker.Core.Constants;
using static Poker.Network.DataModel;

namespace Poker.UI
{
    /// <summary>
    /// Text for the hand history panel: one line per action, your result, times and chip
    /// changes. Pure functions over the server's hand record, so the panel stays layout only.
    /// </summary>
    public static class HandHistoryFormat
    {
        public static bool IsYou(HandRecord record, string playerId) =>
            record.players.Exists(p => p.isYou && p.playerId == playerId);

        /// <summary>"You" in gold for the viewer, other names in blue (names shown literally).</summary>
        public static string NameOf(HandRecord record, string playerId, string name) =>
            IsYou(record, playerId) ? HistoryTexts.You : string.Format(HistoryTexts.Name, Literal(name));

        public static string ActionLine(HandRecord record, HandAction a)
        {
            string who = NameOf(record, a.playerId, a.name);
            string amount = CardFormat.Chips(a.amount), total = CardFormat.Chips(a.total);
            string format = a.kind switch
            {
                "sb" => HistoryTexts.Sb,
                "bb" => HistoryTexts.Bb,
                "fold" => HistoryTexts.Fold,
                "check" => HistoryTexts.Check,
                "call" => HistoryTexts.Call,
                "bet" => HistoryTexts.Bet,
                "raise" => HistoryTexts.Raise,
                "allIn" => HistoryTexts.AllIn,
                "returned" => HistoryTexts.Returned,
                _ => "{0} " + a.kind,
            };
            string line = string.Format(format, who, amount, total);
            if (IsYou(record, a.playerId)) line = YouVerb(line);
            if (a.timeout) line += HistoryTexts.TimedOut;
            if (a.left) line += HistoryTexts.Left;
            return line;
        }

        /// <summary>"You posts" becomes "You post": the verb after the gold "You" loses its s.</summary>
        private static string YouVerb(string line)
        {
            if (!line.StartsWith(HistoryTexts.You)) return line;
            string rest = line.Substring(HistoryTexts.You.Length);
            foreach (var (from, to) in new[]
            {
                (" posts ", " post "), (" folds", " fold"), (" checks", " check"), (" calls ", " call "),
                (" bets ", " bet "), (" raises ", " raise "), (" wins ", " win "), (" is all-in", " are all-in"),
            })
            {
                if (rest.StartsWith(from)) return HistoryTexts.You + to + rest.Substring(from.Length);
            }
            return line;
        }

        public static string WinnerLine(HandRecord record, HandWinner w)
        {
            string who = NameOf(record, w.playerId, w.name);
            string line = string.IsNullOrEmpty(w.handName)
                ? string.Format(HistoryTexts.Won, who, CardFormat.Chips(w.amount))
                : string.Format(HistoryTexts.WonWith, who, CardFormat.Chips(w.amount), w.handName);
            return IsYou(record, w.playerId) ? YouVerb(line) : line;
        }

        /// <summary>"You won 340 with Three of a Kind", "You folded", "You lost 160".</summary>
        public static string YourResult(HandRecord record)
        {
            var you = record.you;
            if (you == null) return "";
            string line;
            if (you.net > 0) line = string.Format(HistoryTexts.YouWon, CardFormat.Chips(you.net));
            else if (you.folded) line = HistoryTexts.YouFolded;
            else if (you.net < 0) line = string.Format(HistoryTexts.YouLost, CardFormat.Chips(-you.net));
            else line = HistoryTexts.YouEven;
            if (!you.folded && !string.IsNullOrEmpty(you.handName)) line += string.Format(HistoryTexts.WithHand, you.handName);
            return line;
        }

        /// <summary>"+180" in green, "-160" in red, "0" plain.</summary>
        public static string Net(int net) =>
            net > 0 ? $"<color=#22C55E>+{CardFormat.Chips(net)}</color>"
            : net < 0 ? $"<color=#E5484D>-{CardFormat.Chips(-net)}</color>"
            : "0";

        /// <summary>"Today 14:32" or "21 Nov 14:32", in local time.</summary>
        public static string When(long unixMs)
        {
            DateTime local = DateTimeOffset.FromUnixTimeMilliseconds(unixMs).LocalDateTime;
            string time = local.ToString("HH:mm");
            return local.Date == DateTime.Now.Date ? string.Format(HistoryTexts.Today, time) : local.ToString("d MMM ") + time;
        }

        /// <summary>Player-chosen text is shown as typed: rich-text tags in it do nothing.</summary>
        public static string Literal(string text) => "<noparse>" + (text ?? "").Replace("</noparse>", "") + "</noparse>";
    }
}
