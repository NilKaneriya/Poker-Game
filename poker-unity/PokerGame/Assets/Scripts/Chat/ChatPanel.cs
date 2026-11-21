using System.Collections.Generic;
using Poker.Network;
using Poker.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static Poker.Chat.ChatConstants;
using static Poker.Chat.ChatModels;

namespace Poker.Chat
{
    /// <summary>
    /// The chat window: a "Chat" button with an unread badge, and a panel with the
    /// message list, quick phrases and a text box. Talks only to ChatClient.
    /// Built by Editor/Chat/ChatPanelBuilder; drop it into any scene that has a table.
    /// </summary>
    public class ChatPanel : MonoBehaviour
    {
        [Header("Open / close")]
        [SerializeField] private Button toggleButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private CanvasGroup panelGroup;
        [SerializeField] private GameObject unreadBadge;
        [SerializeField] private TMP_Text unreadText;

        [Header("Messages")]
        [SerializeField] private ScrollRect scroll;
        [SerializeField] private RectTransform content;
        [SerializeField] private TMP_Text rowTemplate;
        [SerializeField] private TMP_Text emptyText;

        [Header("Writing")]
        [SerializeField] private TMP_InputField input;
        [SerializeField] private Button sendButton;
        [SerializeField] private Button[] quickPhraseButtons;
        [SerializeField] private TMP_Text statusText;

        private readonly Queue<GameObject> rows = new Queue<GameObject>();
        private readonly HashSet<long> shownIds = new HashSet<long>();
        private ChatClient chat;
        private bool open;
        private int unread;
        private bool switchedOn = true;

        private static string MyPlayerId => AuthManager.Instance != null ? AuthManager.Instance.PlayerId : null;

        private void Start()
        {
            rowTemplate.gameObject.SetActive(false);
            input.characterLimit = Limits.MaxLength;
            SetOpen(false);
            SetUnread(0);

            chat = ChatClient.Instance;
            if (chat == null) { toggleButton.gameObject.SetActive(false); return; } // no network (offline preview)
            toggleButton.gameObject.SetActive(switchedOn);

            toggleButton.onClick.AddListener(() => SetOpen(!open));
            closeButton.onClick.AddListener(() => SetOpen(false));
            sendButton.onClick.AddListener(SendTyped);
            input.onSubmit.AddListener(_ => SendTyped());
            for (int i = 0; i < quickPhraseButtons.Length && i < Texts.QuickPhrases.Length; i++)
            {
                string phrase = Texts.QuickPhrases[i];
                quickPhraseButtons[i].onClick.AddListener(() => chat.Send(phrase));
            }

            chat.MessageReceived += OnMessage;
            chat.HistoryReceived += OnHistory;
            chat.ErrorReceived += OnError;
            chat.RequestHistory();
        }

        private void OnDestroy()
        {
            if (chat == null) return;
            chat.MessageReceived -= OnMessage;
            chat.HistoryReceived -= OnHistory;
            chat.ErrorReceived -= OnError;
        }

        // ---------- Incoming ----------

        private void OnHistory(ChatHistory history)
        {
            foreach (var row in rows) Destroy(row);
            rows.Clear();
            shownIds.Clear();
            foreach (var message in history.messages) AddRow(message);
            ScrollToBottom();
        }

        private void OnMessage(ChatMessage message)
        {
            if (!AddRow(message)) return;
            bool fromMe = message.playerId == MyPlayerId;
            if (!open && switchedOn && !fromMe && message.kind == Kinds.Player) SetUnread(unread + 1);
            if (fromMe) statusText.text = "";
            ScrollToBottom();
        }

        private void OnError(string message)
        {
            statusText.text = message;
            UIAnim.Punch(statusText.transform, 0.1f);
        }

        /// <summary>Adds one line; false if it was already shown (history and live messages can overlap).</summary>
        private bool AddRow(ChatMessage message)
        {
            if (message == null || !shownIds.Add(message.id)) return false;
            TMP_Text row = Instantiate(rowTemplate, content);
            row.text = ChatMessageFormat.Row(message, MyPlayerId);
            row.gameObject.SetActive(true);
            rows.Enqueue(row.gameObject);
            while (rows.Count > Limits.MaxRowsShown) Destroy(rows.Dequeue());
            emptyText.gameObject.SetActive(false);
            return true;
        }

        // ---------- Outgoing ----------

        private void SendTyped()
        {
            string text = input.text;
            if (string.IsNullOrWhiteSpace(text)) return;
            chat.Send(text);
            input.text = "";
            if (!Application.isMobilePlatform) input.ActivateInputField(); // keep typing on desktop
        }

        // ---------- Switch on / off ----------

        /// <summary>
        /// The player turned chat on or off (the scene passes in their setting; chat itself
        /// doesn't read game settings). Off: no button, no window, no unread badge.
        /// </summary>
        public void SetSwitchedOn(bool on)
        {
            switchedOn = on;
            if (!on) SetOpen(false);
            SetUnread(0);
            toggleButton.gameObject.SetActive(on && chat != null);
        }

        // ---------- View ----------

        private void SetOpen(bool value)
        {
            open = value;
            panelGroup.alpha = value ? 1f : 0f;
            panelGroup.interactable = panelGroup.blocksRaycasts = value;
            if (!value) return;
            UIAnim.PopIn(panelGroup.transform, 0.2f);
            SetUnread(0);
            statusText.text = "";
            emptyText.gameObject.SetActive(rows.Count == 0);
            ScrollToBottom();
        }

        private void SetUnread(int count)
        {
            unread = count;
            unreadBadge.SetActive(count > 0);
            unreadText.text = count > Limits.MaxUnreadShown ? Texts.UnreadMany : count.ToString();
            if (count > 0) UIAnim.Punch(unreadBadge.transform, 0.2f);
        }

        private void ScrollToBottom()
        {
            Canvas.ForceUpdateCanvases();
            scroll.verticalNormalizedPosition = 0f;
        }
    }
}
