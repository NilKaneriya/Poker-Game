using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static Poker.Network.DataModel;

namespace Poker.UI.Game
{
    /// <summary>
    /// Shown in the middle of the table while a play-with-friends table waits for players.
    /// The seats around it fill up as friends join; when the last seat is taken the
    /// server starts the game and this panel closes for everyone at once.
    /// The host can also start early with 2+ players.
    /// </summary>
    public class WaitingRoomPanel : MonoBehaviour
    {
        [SerializeField] private CanvasGroup group;
        [SerializeField] private RectTransform card;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private TMP_Text codeText;
        [SerializeField] private TMP_Text footerText;
        [SerializeField] private Button copyCodeButton;
        [SerializeField] private Button copyLinkButton;
        [SerializeField] private Button shareButton;
        [SerializeField] private Button startButton;

        public event Action CopyCodeClicked;
        public event Action CopyLinkClicked;
        public event Action ShareClicked;
        public event Action StartClicked;

        public bool IsOpen { get; private set; }
        private int shownPlayers = -1;

        private void Awake()
        {
            copyCodeButton.onClick.AddListener(() => CopyCodeClicked?.Invoke());
            copyLinkButton.onClick.AddListener(() => CopyLinkClicked?.Invoke());
            shareButton.onClick.AddListener(() => ShareClicked?.Invoke());
            startButton.onClick.AddListener(() =>
            {
                startButton.interactable = false; // until the server answers (state or error)
                StartClicked?.Invoke();
            });
        }

        public void Show(TableState table, bool isHost, bool hasInviteLink)
        {
            int seated = table.seats.FindAll(s => s != null).Count;
            int missing = Mathf.Max(0, table.maxSeats - seated);

            statusText.text = $"<color=#F6C744><b>{seated}</b></color> of {table.maxSeats} players at the table";
            codeText.text = table.code;

            string waitFor = missing == 1 ? "1 more player joins" : $"{missing} more players join";
            footerText.text = isHost
                ? (seated >= 2 ? $"Starts when {waitFor}, or start now." : $"Starts when {waitFor}.")
                : $"Starts when {waitFor}, or when the host starts it.";

            startButton.gameObject.SetActive(isHost);
            startButton.interactable = seated >= 2;
            copyLinkButton.gameObject.SetActive(hasInviteLink);

            if (!IsOpen)
            {
                IsOpen = true;
                UIAnim.FadeIn(group, 0.25f);
                UIAnim.PopIn(card, 0.35f);
            }
            else if (seated != shownPlayers)
            {
                UIAnim.Punch(statusText.transform, 0.12f); // someone joined or left
            }
            shownPlayers = seated;
        }

        public void Hide()
        {
            if (!IsOpen) return;
            IsOpen = false;
            shownPlayers = -1;
            UIAnim.FadeOut(group, 0.3f);
        }
    }
}
