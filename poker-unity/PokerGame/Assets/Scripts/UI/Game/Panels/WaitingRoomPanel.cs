using System;
using Poker.Platform;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static Poker.Core.Constants;
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

        /// <summary>Open, update or close for this snapshot. Returns true when it just closed (game started).</summary>
        public bool Render(TableState table)
        {
            if (table.started)
            {
                bool wasOpen = IsOpen;
                Hide();
                return wasOpen;
            }

            int seated = table.seats.FindAll(s => s != null).Count;
            int missing = Mathf.Max(0, table.maxSeats - seated);
            bool isHost = table.hostId == table.you?.playerId;

            statusText.text = string.Format(GameTexts.RoomStatus, seated, table.maxSeats);
            codeText.text = table.code;
            footerText.text = FooterText(isHost, seated, missing);

            startButton.gameObject.SetActive(isHost);
            startButton.interactable = seated >= 2;
            copyLinkButton.gameObject.SetActive(InviteLinkService.BuildLink(table.code) != null);

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
            return false;
        }

        public void Hide()
        {
            if (!IsOpen) return;
            IsOpen = false;
            shownPlayers = -1;
            UIAnim.FadeOut(group, 0.3f);
        }

        private static string FooterText(bool isHost, int seated, int missing)
        {
            string waitFor = missing == 1 ? GameTexts.OneMorePlayer : string.Format(GameTexts.MorePlayers, missing);
            if (!isHost) return string.Format(GameTexts.RoomGuestWaits, waitFor);
            return string.Format(seated >= 2 ? GameTexts.RoomHostCanStart : GameTexts.RoomHostWaits, waitFor);
        }
    }
}
