using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static Poker.Core.Constants;
using static Poker.Network.DataModel;

namespace Poker.UI.Game
{
    /// <summary>
    /// Hint overlay: the poker hand chart (best to worst, with example cards), the basic
    /// rules, and a mark on the row of the player's current hand. Opened from the
    /// "Hand rankings" button; closes with the X button, a click outside the card, or Esc.
    /// The chart data lives in Constants.HandChart.
    /// </summary>
    public class HandRankingsPanel : MonoBehaviour
    {
        [SerializeField] private CanvasGroup group;
        [SerializeField] private RectTransform card;
        [SerializeField] private Button closeButton;
        [SerializeField] private Button backdropButton;
        [Tooltip("HandChart.Entries.Length x 5 example cards, row by row.")]
        [SerializeField] private CardView[] exampleCards = new CardView[0];

        [Header("Your current hand")]
        [SerializeField] private TMP_Text currentHandText;
        [Tooltip("Row backgrounds, best hand first (same order as HandChart.Entries).")]
        [SerializeField] private Image[] rowBackgrounds = new Image[0];
        [Tooltip("'YOUR HAND' tag on each row, same order.")]
        [SerializeField] private GameObject[] yourHandTags = new GameObject[0];

        public bool IsOpen { get; private set; }
        private bool examplesShown;
        private Color[] rowColors;   // original row colours, restored when unmarked
        private int markedRow = -1;

        private void Awake()
        {
            closeButton.onClick.AddListener(Close);
            backdropButton.onClick.AddListener(Close);
        }

        private void Update()
        {
            if (IsOpen && Input.GetKeyDown(KeyCode.Escape)) Close();
        }

        public void Toggle()
        {
            if (IsOpen) Close();
            else Open();
        }

        public void Open()
        {
            IsOpen = true;
            UIAnim.FadeIn(group, 0.2f);
            UIAnim.PopIn(card, 0.3f);
            ShowExamples();
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            UIAnim.FadeOut(group, 0.2f);
        }

        /// <summary>Mark the player's current hand (row in HandChart.Entries, -1 = none) and show a status line.</summary>
        public void ShowCurrentHand(int row, string status)
        {
            if (currentHandText != null) currentHandText.text = status;
            if (row == markedRow) return;

            if (rowColors == null)
            {
                rowColors = new Color[rowBackgrounds.Length];
                for (int i = 0; i < rowBackgrounds.Length; i++) rowColors[i] = rowBackgrounds[i].color;
            }

            for (int i = 0; i < rowBackgrounds.Length; i++)
                rowBackgrounds[i].color = i == row ? UIColors.MarkedRow : rowColors[i];
            for (int i = 0; i < yourHandTags.Length; i++)
                yourHandTags[i].SetActive(i == row);

            // The hand just changed: draw the eye to it if the chart is open.
            if (IsOpen && row >= 0 && row < rowBackgrounds.Length)
                UIAnim.Punch(rowBackgrounds[row].transform, 0.05f);
            markedRow = row;
        }

        private void ShowExamples()
        {
            if (examplesShown) return;
            examplesShown = true;

            for (int row = 0; row < HandChart.Entries.Length; row++)
            {
                HandChartEntry hand = HandChart.Entries[row];
                for (int i = 0; i < HandChart.CardsPerEntry; i++)
                {
                    int index = row * HandChart.CardsPerEntry + i;
                    if (index >= exampleCards.Length) return;
                    bool makesHand = i < hand.MadeBy;
                    exampleCards[index].ShowFace(hand.Cards[i]);
                    exampleCards[index].SetHighlighted(makesHand);
                    exampleCards[index].SetDimmed(!makesHand); // kickers fade back
                }
            }
        }
    }
}
