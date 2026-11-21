using Poker.Core;
using TMPro;
using UnityEngine;
using static Poker.Core.Constants;
using static Poker.Network.DataModel;

namespace Poker.UI.Lobby
{
    /// <summary>"Your stats" card: hands played, hands won, biggest pot, best hand. Facts only, no goals or streaks.</summary>
    public class PlayerStatsView : MonoBehaviour
    {
        [SerializeField] private TMP_Text handsPlayedText;
        [SerializeField] private TMP_Text handsWonText;
        [SerializeField] private TMP_Text biggestPotText;
        [SerializeField] private TMP_Text bestHandText;

        public void Show(PlayerStats stats)
        {
            handsPlayedText.text = stats.handsPlayed.ToString("N0");
            handsWonText.text = stats.handsWon.ToString("N0");
            biggestPotText.text = stats.biggestPot > 0 ? CardFormat.Chips(stats.biggestPot) : LobbyTexts.NoValue;
            bestHandText.text = string.IsNullOrEmpty(stats.bestHand) ? LobbyTexts.NoValue : stats.bestHand;
        }
    }
}
