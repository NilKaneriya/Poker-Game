using Poker.UI;
using Poker.UI.Lobby;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static Poker.Core.Constants;
using static Poker.EditorTools.UIFactory;

namespace Poker.EditorTools
{
    /// <summary>
    /// Builds the wallet button (shows your chips, opens the shop) and the dummy shop pop-up.
    /// Called by PokerSceneBuilder for the Lobby.
    /// </summary>
    public static class ShopUIBuilder
    {
        private static readonly Vector2 TopLeft = new Vector2(0f, 1f);

        /// <summary>A gold-edged button reading "10,000 chips  +"; the number is filled in at run time.</summary>
        public static Button BuildWalletButton(Transform parent, Vector2 anchor, float x, float y, float w, float h)
        {
            var button = MakeButton(parent, "WalletButton", ShopTexts.Loading, Theme.Dark.WithAlpha(0.85f), Theme.Gold, 24, 18f);
            button.GetComponent<RectTransform>().Pin(anchor, x, y, w, h);
            var view = button.gameObject.AddComponent<WalletView>();
            Wire(view, "balanceText", LabelOf(button));
            return button;
        }

        public static ShopPanel BuildPanel(Transform canvas, Button openButton)
        {
            var root = ProfileUIBuilder.ModalShell(canvas, "ShopPanel", ShopTexts.Title, 1000, 760,
                out RectTransform card, out Button close, out Button backdrop, out CanvasGroup group);
            LabelOf(close).text = "X";

            var wallet = MakeText(card, "Wallet", "", 28, Theme.Text, TextAlignmentOptions.Left);
            wallet.rectTransform.Pin(TopLeft, 40, -104, 600, 40);
            var demo = MakeText(card, "DemoNote", ShopTexts.Demo, 20, Theme.Muted, TextAlignmentOptions.Left);
            demo.rectTransform.Pin(TopLeft, 40, -146, 920, 30);

            var content = ScrollList(card, "Items", out ScrollRect scroll);
            scroll.GetComponent<RectTransform>().Fill(40, 40, 196, 96);
            content.GetComponent<VerticalLayoutGroup>().spacing = 14f;

            // Row template (hidden; copied once per item the server lists)
            var rowImage = Panel(content, "RowTemplate", Theme.Input.WithAlpha(0.8f), 18f);
            rowImage.gameObject.AddComponent<LayoutElement>().preferredHeight = 118;
            var row = rowImage.transform;
            var name = MakeText(row, "Name", "Handful of chips", 24, Theme.Muted, TextAlignmentOptions.Left);
            name.rectTransform.Pin(TopLeft, 28, -16, 520, 32);
            var chips = MakeText(row, "Chips", "5,000 chips", 38, Theme.Gold, TextAlignmentOptions.Left, bold: true);
            chips.rectTransform.Pin(TopLeft, 28, -52, 520, 52);
            var price = MakeText(row, "Price", "$0.99 (demo)", 22, Theme.Muted, TextAlignmentOptions.Right);
            price.rectTransform.Pin(new Vector2(1f, 0.5f), -214, 0, 200, 32);
            var get = MakeButton(row, "GetButton", ShopTexts.Get, Theme.Gold, Theme.Dark, 28, 18f);
            get.GetComponent<RectTransform>().Pin(new Vector2(1f, 0.5f), -28, 0, 170, 70);
            var item = rowImage.gameObject.AddComponent<ShopItemView>();
            Wire(item, "nameText", name);
            Wire(item, "chipsText", chips);
            Wire(item, "priceText", price);
            Wire(item, "buyButton", get);

            var status = MakeText(card, "Status", "", 24, Theme.Muted);
            status.rectTransform.Pin(new Vector2(0.5f, 0f), 0, 34, 900, 40);

            var panel = root.gameObject.AddComponent<ShopPanel>();
            ProfileUIBuilder.WireModal(panel, group, card, openButton, close, backdrop);
            Wire(panel, "rowTemplate", item);
            Wire(panel, "walletText", wallet);
            Wire(panel, "statusText", status);
            return panel;
        }
    }
}
