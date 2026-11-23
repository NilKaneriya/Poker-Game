using System;
using Poker.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static Poker.Core.Constants;
using static Poker.Network.DataModel;

namespace Poker.UI.Lobby
{
    /// <summary>One row of the shop: what it is, how many chips, the (demo) price and a Get button.</summary>
    public class ShopItemView : MonoBehaviour
    {
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text chipsText;
        [SerializeField] private TMP_Text priceText;
        [SerializeField] private Button buyButton;

        public void Bind(ShopItem item, Action<string> onBuy)
        {
            nameText.text = item.name;
            chipsText.text = string.Format(ShopTexts.ItemChips, CardFormat.Chips(item.chips));
            priceText.text = item.price;
            buyButton.onClick.RemoveAllListeners();
            buyButton.onClick.AddListener(() => onBuy(item.id));
        }

        public void SetInteractable(bool value) => buyButton.interactable = value;
    }
}
