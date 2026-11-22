using System.Collections.Generic;
using Poker.Core;
using Poker.Network;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static Poker.Core.Constants;
using static Poker.Network.DataModel;

namespace Poker.UI
{
    /// <summary>
    /// Your last hands, like a poker client's hand history: a list on the left (hand number,
    /// table, your cards, chips won or lost) and the selected hand on the right, street by
    /// street, with the board, every action, the showdown and who won.
    /// Used in the Lobby and at the table; the server keeps the last 30 hands per player.
    /// </summary>
    public class HandHistoryPanel : ModalPanel
    {
        [Header("List")]
        [SerializeField] private RectTransform listContent;
        [Tooltip("Hidden row: Button with 'Title', 'Sub', 'Net' texts and a 'Cards' container.")]
        [SerializeField] private Button rowTemplate;
        [SerializeField] private TMP_Text listStatusText;
        [SerializeField] private Color rowColor = new Color(1f, 1f, 1f, 0.05f);
        [SerializeField] private Color rowSelectedColor = new Color(0.96f, 0.78f, 0.27f, 0.25f);

        [Header("Selected hand")]
        [SerializeField] private GameObject detailRoot;
        [SerializeField] private TMP_Text detailEmptyText;
        [SerializeField] private TMP_Text detailTitle;
        [SerializeField] private TMP_Text detailInfo;
        [SerializeField] private RectTransform yourCards;
        [SerializeField] private RectTransform boardCards;
        [SerializeField] private TMP_Text noBoardText;
        [SerializeField] private TMP_Text resultText;
        [SerializeField] private ScrollRect detailScroll;
        [SerializeField] private RectTransform detailContent;

        [Header("Templates (hidden)")]
        [Tooltip("Street header: 'Label' text + 'Cards' container.")]
        [SerializeField] private RectTransform sectionTemplate;
        [SerializeField] private TMP_Text lineTemplate;
        [Tooltip("Showdown row: 'Label' text, 'Cards' container, 'Info' text.")]
        [SerializeField] private RectTransform cardLineTemplate;
        [Tooltip("Small card: background Image with a 'Text' child.")]
        [SerializeField] private RectTransform miniCardTemplate;

        private static NetworkManager Net => NetworkManager.Instance;

        private readonly List<(HandRecord hand, Button row)> rows = new List<(HandRecord, Button)>();
        private readonly List<GameObject> detailItems = new List<GameObject>();
        private string selectedId;

        protected override void Start()
        {
            base.Start();
            foreach (var template in new Component[] { rowTemplate, sectionTemplate, lineTemplate, cardLineTemplate, miniCardTemplate })
                template.gameObject.SetActive(false);
            ShowDetail(null);
            if (Net != null) Net.HandHistoryReceived += OnHistory;
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (Net != null) Net.HandHistoryReceived -= OnHistory;
        }

        protected override void OnOpened()
        {
            if (Net == null || !Net.IsConnected)
            {
                SetListStatus(HistoryTexts.Offline);
                return;
            }
            if (rows.Count == 0) SetListStatus(HistoryTexts.Loading);
            Net.RequestHandHistory(); // always fresh: a hand may have just finished
        }

        // ---------- List ----------

        private void OnHistory(HandHistoryList list)
        {
            foreach (var (_, row) in rows) Destroy(row.gameObject);
            rows.Clear();

            var hands = list?.hands ?? new List<HandRecord>();
            foreach (var hand in hands) rows.Add((hand, BuildRow(hand)));
            SetListStatus(hands.Count == 0 ? HistoryTexts.Empty : null);

            // Keep the selection if it is still there; otherwise show the newest hand.
            var selected = hands.Find(h => h.id == selectedId) ?? (hands.Count > 0 ? hands[0] : null);
            Select(selected);
        }

        private Button BuildRow(HandRecord hand)
        {
            var row = Instantiate(rowTemplate, listContent);
            row.name = "Hand_" + hand.handNumber;
            row.gameObject.SetActive(true);
            Text(row.transform, "Title").text = string.Format(HistoryTexts.RowTitle, hand.handNumber, HandHistoryFormat.Literal(hand.tableName));
            Text(row.transform, "Sub").text = HandHistoryFormat.When(hand.startedAt)
                + (hand.you != null && hand.you.folded ? "  •  " + HistoryTexts.RowFolded : "");
            Text(row.transform, "Net").text = HandHistoryFormat.Net(hand.you?.net ?? 0);
            FillCards((RectTransform)row.transform.Find("Cards"), hand.you?.cards, keepExisting: false);
            row.onClick.AddListener(() => Select(hand));
            return row;
        }

        private void Select(HandRecord hand)
        {
            selectedId = hand?.id;
            foreach (var (h, row) in rows) row.targetGraphic.color = h == hand ? rowSelectedColor : rowColor;
            ShowDetail(hand);
        }

        private void SetListStatus(string text)
        {
            listStatusText.gameObject.SetActive(!string.IsNullOrEmpty(text));
            listStatusText.text = text ?? "";
        }

        // ---------- Selected hand ----------

        private void ShowDetail(HandRecord hand)
        {
            detailRoot.SetActive(hand != null);
            detailEmptyText.gameObject.SetActive(hand == null);
            detailEmptyText.text = HistoryTexts.PickHand;
            foreach (var item in detailItems) Destroy(item);
            detailItems.Clear();
            if (hand == null) return;

            detailTitle.text = string.Format(HistoryTexts.DetailTitle, hand.handNumber, HandHistoryFormat.Literal(hand.tableName));
            detailInfo.text = string.Format(HistoryTexts.DetailInfo, CardFormat.Chips(hand.smallBlind), CardFormat.Chips(hand.bigBlind),
                HandHistoryFormat.When(hand.startedAt), CardFormat.Chips(hand.pot));
            FillCards(yourCards, hand.you?.cards, keepExisting: false);
            FillCards(boardCards, hand.board, keepExisting: false);
            noBoardText.gameObject.SetActive(hand.board.Count == 0);
            noBoardText.text = HistoryTexts.NoBoard;
            resultText.text = HandHistoryFormat.YourResult(hand);

            // Street by street
            foreach (var street in hand.streets)
            {
                AddSection(HistoryTexts.Street(street.stage), street.cards);
                foreach (var action in street.actions) AddLine(HandHistoryFormat.ActionLine(hand, action));
            }

            // Showdown: everyone whose cards were shown (other than you, they're at the top)
            var shown = hand.players.FindAll(p => !p.isYou && p.cards.Count > 0);
            if (shown.Count > 0)
            {
                AddSection(HistoryTexts.Showdown, null);
                foreach (var p in shown) AddCardLine(HandHistoryFormat.NameOf(hand, p.playerId, p.name), p.cards, p.handName);
            }

            AddSection(HistoryTexts.Result, null);
            foreach (var w in hand.winners) AddLine(HandHistoryFormat.WinnerLine(hand, w));

            Canvas.ForceUpdateCanvases();
            detailScroll.verticalNormalizedPosition = 1f; // start at the top
        }

        private void AddSection(string title, List<string> cards)
        {
            var section = Instantiate(sectionTemplate, detailContent);
            section.gameObject.SetActive(true);
            Text(section, "Label").text = title;
            FillCards((RectTransform)section.Find("Cards"), cards, keepExisting: false);
            detailItems.Add(section.gameObject);
        }

        private void AddLine(string text)
        {
            var line = Instantiate(lineTemplate, detailContent);
            line.gameObject.SetActive(true);
            line.text = text;
            detailItems.Add(line.gameObject);
        }

        private void AddCardLine(string label, List<string> cards, string info)
        {
            var line = Instantiate(cardLineTemplate, detailContent);
            line.gameObject.SetActive(true);
            Text(line, "Label").text = label;
            Text(line, "Info").text = info ?? "";
            FillCards((RectTransform)line.Find("Cards"), cards, keepExisting: false);
            detailItems.Add(line.gameObject);
        }

        // ---------- Cards ----------

        private void FillCards(RectTransform container, List<string> cards, bool keepExisting)
        {
            if (container == null) return;
            if (!keepExisting)
                for (int i = container.childCount - 1; i >= 0; i--) Destroy(container.GetChild(i).gameObject);
            if (cards == null) return;

            bool fourColor = SettingsManager.Instance != null && SettingsManager.Instance.Current.fourColorDeck;
            foreach (string code in cards)
            {
                if (!CardFormat.TryParse(code, out string rank, out string suit, out _)) continue;
                var card = Instantiate(miniCardTemplate, container);
                card.gameObject.SetActive(true);
                var text = Text(card, "Text");
                text.text = $"{rank}<size=80%>{suit}</size>";
                text.color = CardFormat.SuitColor(code[1], fourColor);
            }
        }

        private static TMP_Text Text(Transform parent, string child) => parent.Find(child).GetComponent<TMP_Text>();
    }
}
