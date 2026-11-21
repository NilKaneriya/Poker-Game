using System;
using System.Collections.Generic;
using Poker.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Poker.UI.Game
{
    /// <summary>
    /// End-of-hand overlay in three flavours:
    ///   Win     - you won the pot (shows amount and winning hand)
    ///   Loss    - someone else won (shows who and with what)
    ///   Busted  - you have no chips left: watch the table or restart with 1,000 chips
    /// </summary>
    public class ResultPanel : MonoBehaviour
    {
        [SerializeField] private CanvasGroup group;
        [SerializeField] private RectTransform card; // the panel that pops in
        [SerializeField] private Image headerBar;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text subtitleText;
        [SerializeField] private TMP_Text amountText;
        [SerializeField] private CardView[] bestCards = new CardView[5];

        [SerializeField] private Button continueButton;
        [SerializeField] private Button rebuyButton;
        [SerializeField] private Button lobbyButton;

        [SerializeField] private float fullHeight = 520f;    // with the row of winning cards
        [SerializeField] private float compactHeight = 360f; // no cards to show (fold win, out of chips)

        [SerializeField] private Color winColor = new Color(0.97f, 0.76f, 0.2f);
        [SerializeField] private Color lossColor = new Color(0.35f, 0.45f, 0.6f);
        [SerializeField] private Color bustedColor = new Color(0.9f, 0.3f, 0.32f);

        public event Action ContinueClicked;
        public event Action RebuyClicked;
        public event Action LobbyClicked;

        public bool IsShowingBusted { get; private set; }

        private void Awake()
        {
            continueButton.onClick.AddListener(() => { Hide(); ContinueClicked?.Invoke(); });
            rebuyButton.onClick.AddListener(() => RebuyClicked?.Invoke());
            lobbyButton.onClick.AddListener(() => LobbyClicked?.Invoke());
        }

        public void ShowWin(int amount, string handName, List<string> cards)
        {
            Open(winColor, "You win!", HandLine(handName), "+" + CardFormat.Chips(amount), cards, busted: false);
        }

        public void ShowLoss(string winnerName, int amount, string handName, List<string> cards)
        {
            string how = string.IsNullOrEmpty(handName) ? "takes the pot" : $"wins with {handName}";
            Open(lossColor, "Not this time", $"{winnerName} {how}", CardFormat.Chips(amount) + " chips", cards, busted: false);
        }

        public void ShowBusted(int startingChips)
        {
            Open(bustedColor, "Out of chips",
                "You can keep watching this table, or start fresh. Chips are just for fun.",
                "", null, busted: true);
            rebuyButton.GetComponentInChildren<TMP_Text>().text = $"Restart with {CardFormat.Chips(startingChips)}";
        }

        public void Hide()
        {
            IsShowingBusted = false;
            if (group.gameObject.activeSelf) UIAnim.FadeOut(group);
        }

        private void Open(Color accent, string title, string subtitle, string amount, List<string> cards, bool busted)
        {
            IsShowingBusted = busted;
            headerBar.color = accent;
            titleText.text = title;
            titleText.color = accent;
            subtitleText.text = subtitle;
            amountText.text = amount;
            amountText.gameObject.SetActive(!string.IsNullOrEmpty(amount));

            bool hasCards = cards != null && cards.Count > 0;
            for (int i = 0; i < bestCards.Length; i++)
            {
                if (hasCards && i < cards.Count) bestCards[i].ShowFace(cards[i]);
                else bestCards[i].Clear();
            }
            card.sizeDelta = new Vector2(card.sizeDelta.x, hasCards ? fullHeight : compactHeight);

            continueButton.gameObject.SetActive(!busted);
            rebuyButton.gameObject.SetActive(busted);
            lobbyButton.gameObject.SetActive(busted);

            UIAnim.FadeIn(group, 0.25f);
            UIAnim.PopIn(card, 0.4f);
        }

        private static string HandLine(string handName) =>
            string.IsNullOrEmpty(handName) ? "Everyone else folded" : $"with {handName}";
    }
}
