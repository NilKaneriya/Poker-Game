using System.Collections.Generic;
using static Poker.Core.Constants;

namespace Poker.Gameplay
{
    /// <summary>
    /// Tells which kind of hand a set of 2-7 cards makes right now (a HandRanks value),
    /// for the "your hand" hints. Display only: the server decides who wins.
    /// </summary>
    public static class HandEvaluator
    {
        private const int CardsInHand = 5;

        /// <summary>Best HandRanks value made by these cards ("Ah", "Td", ...), or -1 if none are valid.</summary>
        public static int Evaluate(IEnumerable<string> cards)
        {
            var rankCounts = new int[15];                 // index 2..14 (14 = ace)
            var suitRanks = new Dictionary<char, HashSet<int>>();
            var allRanks = new HashSet<int>();
            int valid = 0;

            foreach (string code in cards)
            {
                if (code == null || code.Length != 2) continue;
                int rank = RankValue(code[0]);
                char suit = code[1];
                if (rank == 0 || "hdcs".IndexOf(suit) < 0) continue;

                valid++;
                rankCounts[rank]++;
                allRanks.Add(rank);
                if (!suitRanks.TryGetValue(suit, out var set)) suitRanks[suit] = set = new HashSet<int>();
                set.Add(rank);
            }
            if (valid == 0) return -1;

            // Flush (and straight flush) need 5 cards of one suit.
            foreach (var suited in suitRanks.Values)
            {
                if (suited.Count < CardsInHand) continue;
                int high = StraightHigh(suited);
                if (high == 14) return HandRanks.RoyalFlush;
                if (high > 0) return HandRanks.StraightFlush;
            }

            int quads = 0, trips = 0, pairs = 0;
            for (int r = 2; r <= 14; r++)
            {
                if (rankCounts[r] == 4) quads++;
                else if (rankCounts[r] == 3) trips++;
                else if (rankCounts[r] == 2) pairs++;
            }

            if (quads > 0) return HandRanks.FourOfAKind;
            if (trips > 0 && (trips > 1 || pairs > 0)) return HandRanks.FullHouse;
            foreach (var suited in suitRanks.Values) if (suited.Count >= CardsInHand) return HandRanks.Flush;
            if (StraightHigh(allRanks) > 0) return HandRanks.Straight;
            if (trips > 0) return HandRanks.ThreeOfAKind;
            if (pairs > 1) return HandRanks.TwoPair;
            if (pairs == 1) return HandRanks.OnePair;
            return HandRanks.HighCard;
        }

        /// <summary>Highest card of the best 5-in-a-row, 0 if none. The ace also plays low (A-2-3-4-5).</summary>
        private static int StraightHigh(HashSet<int> ranks)
        {
            int run = 0;
            for (int r = 14; r >= 1; r--)
            {
                bool has = ranks.Contains(r == 1 ? 14 : r);
                run = has ? run + 1 : 0;
                if (run == CardsInHand) return r + CardsInHand - 1;
            }
            return 0;
        }

        private static int RankValue(char c)
        {
            if (c >= '2' && c <= '9') return c - '0';
            switch (c)
            {
                case 'T': return 10;
                case 'J': return 11;
                case 'Q': return 12;
                case 'K': return 13;
                case 'A': return 14;
                default: return 0;
            }
        }
    }
}
