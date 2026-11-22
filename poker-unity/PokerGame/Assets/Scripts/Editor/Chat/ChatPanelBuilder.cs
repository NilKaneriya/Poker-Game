using Poker.Chat;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static Poker.Chat.ChatConstants;
using static Poker.EditorTools.UIFactory;

namespace Poker.EditorTools.Chat
{
    /// <summary>
    /// Builds the chat UI (button + window) into a scene. Part of the chat module: the scene
    /// builder only calls Build(canvas); everything about the chat layout lives here.
    /// </summary>
    public static class ChatPanelBuilder
    {
        private static readonly Vector2 BottomLeft = new Vector2(0f, 0f);

        /// <summary>"Chat" button at (buttonX, margin) from the bottom-left; the window opens above it.</summary>
        public static ChatPanel Build(Transform canvas, float buttonX, float margin)
        {
            var root = Node("Chat", canvas).Fill();

            // Toggle button with unread badge
            var toggle = MakeButton(root, "ChatButton", Texts.Button, Theme.Dark.WithAlpha(0.85f), Theme.Gold, 24);
            toggle.GetComponent<RectTransform>().Pin(BottomLeft, buttonX, margin, 170, 60);
            var badge = Img(toggle.transform, "UnreadBadge", UISprites.Circle, Colors.UnreadBadge);
            badge.rectTransform.Pin(new Vector2(1f, 1f), 10, 10, 34, 34);
            var unreadText = MakeText(badge.transform, "Count", "1", 18, Color.white, bold: true);
            unreadText.rectTransform.Fill();

            // Window
            var window = Card(root, "ChatWindow", Theme.Panel.WithAlpha(0.97f), 24f);
            window.raycastTarget = true; // clicks on the window must not reach the table
            window.rectTransform.Pin(BottomLeft, margin, margin + 72, 560, 500);
            var group = Group(window);

            MakeText(window.transform, "Title", Texts.Title, 28, Theme.Gold, TextAlignmentOptions.Left, bold: true)
                .rectTransform.Pin(new Vector2(0f, 1f), 22, -14, 360, 40);
            var close = MakeButton(window.transform, "CloseButton", "X", Theme.PanelLight, Theme.Text, 22, 20f);
            close.GetComponent<RectTransform>().Pin(new Vector2(1f, 1f), -14, -12, 46, 46);

            // Message list
            var listBg = Panel(window.transform, "ListBackground", Theme.Input.WithAlpha(0.8f), 16f);
            listBg.rectTransform.Fill(14, 14, 66, 176);
            var content = ScrollList(listBg.transform, "Messages", out ScrollRect scroll);
            scroll.GetComponent<RectTransform>().Fill(12, 12, 8, 8);
            content.GetComponent<VerticalLayoutGroup>().spacing = 6f;

            var row = MakeText(content, "RowTemplate", "<b>Name</b>  message", 21, Theme.Text, TextAlignmentOptions.TopLeft);
            row.overflowMode = TextOverflowModes.Overflow;
            row.enableWordWrapping = true;
            row.richText = true;

            var empty = MakeText(listBg.transform, "Empty", Texts.Empty, 22, Theme.Muted);
            empty.rectTransform.Fill(20, 20, 20, 20);

            var status = MakeText(window.transform, "Status", "", 18, Theme.Red, TextAlignmentOptions.Left);
            status.rectTransform.Pin(BottomLeft, 18, 140, 520, 28);

            // Quick phrases
            var quick = new Button[Texts.QuickPhrases.Length];
            float quickWidth = (530f - (quick.Length - 1) * 8f) / quick.Length;
            for (int i = 0; i < quick.Length; i++)
            {
                quick[i] = MakeButton(window.transform, "Quick" + i, Texts.QuickPhrases[i], Theme.PanelLight, Theme.Text, 18, 14f);
                quick[i].GetComponent<RectTransform>().Pin(BottomLeft, 15 + i * (quickWidth + 8f), 88, quickWidth, 44);
            }

            // Input + send
            var input = MakeInput(window.transform, "Input", Texts.Placeholder, 22);
            input.GetComponent<RectTransform>().Pin(BottomLeft, 15, 16, 400, 60);
            input.lineType = TMP_InputField.LineType.SingleLine;
            var send = MakeButton(window.transform, "SendButton", Texts.Send, Theme.Green, Theme.Dark, 24);
            send.GetComponent<RectTransform>().Pin(BottomLeft, 425, 16, 120, 60);

            var panel = root.gameObject.AddComponent<ChatPanel>();
            Wire(panel, "toggleButton", toggle);
            Wire(panel, "closeButton", close);
            Wire(panel, "panelGroup", group);
            Wire(panel, "unreadBadge", badge.gameObject);
            Wire(panel, "unreadText", unreadText);
            Wire(panel, "scroll", scroll);
            Wire(panel, "content", content);
            Wire(panel, "rowTemplate", row);
            Wire(panel, "emptyText", empty);
            Wire(panel, "input", input);
            Wire(panel, "sendButton", send);
            WireArray(panel, "quickPhraseButtons", quick);
            Wire(panel, "statusText", status);
            return panel;
        }
    }
}
