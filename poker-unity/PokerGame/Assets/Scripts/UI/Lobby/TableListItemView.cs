using System;
using Poker.Network;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static Poker.Core.Constants;
using static Poker.Network.DataModel;

namespace Poker.UI.Lobby
{
    /// <summary>One row in the lobby's "Open tables" list.</summary>
    public class TableListItemView : MonoBehaviour
    {
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text detailsText;
        [SerializeField] private TMP_Text playersText;
        [SerializeField] private Button joinButton;
        [SerializeField] private TMP_Text joinLabel;

        public void Bind(TableSummary table, Action<string> onJoin)
        {
            bool full = table.players >= table.maxSeats;

            nameText.text = table.name;
            string status = !table.started ? LobbyTexts.TableWaiting
                : table.inHand ? LobbyTexts.TableInHand : "";
            detailsText.text = $"Code {table.code}  •  Blinds {table.blinds}{status}";
            playersText.text = $"{table.players}/{table.maxSeats}";
            joinLabel.text = full ? "Full" : "Join";
            joinButton.interactable = !full;

            joinButton.onClick.RemoveAllListeners();
            joinButton.onClick.AddListener(() => onJoin(table.code));
        }
    }
}
