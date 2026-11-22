using Poker.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static Poker.Core.Constants;
using static Poker.EditorTools.UIFactory;

namespace Poker.EditorTools
{
    /// <summary>
    /// Builds the hand history pop-up (list of hands + the selected hand street by street)
    /// and the buttons that open it. Called by PokerSceneBuilder for the Lobby and Game.
    /// </summary>
    public static class HandHistoryUIBuilder
    {
        private static readonly Vector2 TopLeft = new Vector2(0f, 1f);
        private static readonly Vector2 Left = new Vector2(0f, 0.5f);
        private static readonly Vector2 Right = new Vector2(1f, 0.5f);

        public static Button BuildOpenButton(Transform parent, string label, Vector2 anchor, float x, float y, float w, float h, float fontSize)
        {
            var button = MakeButton(parent, "HandHistoryButton", label, Theme.Dark.WithAlpha(0.85f), Theme.Gold, fontSize, 16f);
            button.GetComponent<RectTransform>().Pin(anchor, x, y, w, h);
            return button;
        }

        public static HandHistoryPanel BuildPanel(Transform canvas, Button openButton)
        {
            var root = ProfileUIBuilder.ModalShell(canvas, "HandHistoryPanel", HistoryTexts.Title, 1500, 900,
                out RectTransform card, out Button close, out Button backdrop, out CanvasGroup group);
            LabelOf(close).text = "X";

            // ----- Left: the list of hands -----
            var listBg = Panel(card, "ListBackground", Theme.Input.WithAlpha(0.8f), 18f);
            listBg.rectTransform.Pin(TopLeft, 40, -112, 500, 750);
            var listContent = ScrollList(listBg.transform, "Hands", out ScrollRect listScroll);
            listScroll.GetComponent<RectTransform>().Fill(10, 10, 10, 10);
            listContent.GetComponent<VerticalLayoutGroup>().spacing = 8f;
            var listStatus = MakeText(listBg.transform, "Status", HistoryTexts.Loading, 22, Theme.Muted);
            listStatus.rectTransform.Fill(30, 30, 30, 30);

            var row = MakeButton(listContent, "RowTemplate", "", new Color(1f, 1f, 1f, 0.05f), Theme.Text, 20, 16f);
            Object.DestroyImmediate(LabelOf(row).gameObject);
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = 92;
            MakeText(row.transform, "Title", "Hand #1  •  Table", 22, Theme.Text, TextAlignmentOptions.Left, bold: true)
                .rectTransform.Pin(TopLeft, 18, -12, 300, 32);
            MakeText(row.transform, "Sub", "Today 14:32", 18, Theme.Muted, TextAlignmentOptions.Left)
                .rectTransform.Pin(new Vector2(0f, 0f), 18, 14, 300, 28);
            MakeText(row.transform, "Net", "+180", 26, Theme.Text, TextAlignmentOptions.Right, bold: true)
                .rectTransform.Pin(new Vector2(1f, 1f), -16, -8, 140, 34);
            CardRow(row.transform, "Cards", new Vector2(1f, 0f), -16, 6, 90, 46, TextAnchor.MiddleRight, 0.8f);

            // ----- Right: the selected hand -----
            var detailArea = Node("Detail", card).Pin(TopLeft, 570, -112, 890, 750);
            var empty = MakeText(detailArea, "Empty", HistoryTexts.PickHand, 24, Theme.Muted);
            empty.rectTransform.Fill(40, 40, 40, 40);
            var detail = Node("Hand", detailArea).Fill();

            var title = MakeText(detail, "Title", "Hand #1  •  Table", 32, Theme.Text, TextAlignmentOptions.Left, bold: true);
            title.rectTransform.Pin(TopLeft, 0, 0, 890, 44);
            var info = MakeText(detail, "Info", "Blinds 10/20", 20, Theme.Muted, TextAlignmentOptions.Left);
            info.rectTransform.Pin(TopLeft, 0, -46, 890, 30);

            SmallLabel(detail, HistoryTexts.YourCards, 0, -90);
            var yourCards = CardRow(detail, "YourCards", TopLeft, 0, -118, 200, 70, TextAnchor.MiddleLeft, 1.25f);
            SmallLabel(detail, HistoryTexts.Board, 220, -90);
            var boardCards = CardRow(detail, "BoardCards", TopLeft, 220, -118, 460, 70, TextAnchor.MiddleLeft, 1.25f);
            var noBoard = MakeText(detail, "NoBoard", HistoryTexts.NoBoard, 20, Theme.Muted, TextAlignmentOptions.Left);
            noBoard.rectTransform.Pin(TopLeft, 220, -132, 560, 40);

            var result = MakeText(detail, "Result", "You won 340", 28, Theme.Text, TextAlignmentOptions.Left);
            result.rectTransform.Pin(TopLeft, 0, -200, 890, 40);

            var logBg = Panel(detail, "LogBackground", Theme.Input.WithAlpha(0.8f), 18f);
            logBg.rectTransform.Pin(TopLeft, 0, -254, 890, 496);
            var logContent = ScrollList(logBg.transform, "Log", out ScrollRect logScroll);
            logScroll.GetComponent<RectTransform>().Fill(16, 16, 12, 12);
            var logLayout = logContent.GetComponent<VerticalLayoutGroup>();
            logLayout.spacing = 4f;
            logLayout.padding = new RectOffset(6, 6, 4, 10);

            // ----- Templates (hidden; copied at runtime) -----
            var templates = Node("Templates", card).Fill();

            var section = Node("SectionTemplate", templates).At(0, 0, 840, 54);
            section.gameObject.AddComponent<LayoutElement>().preferredHeight = 58;
            var sectionLabel = MakeText(section, "Label", "FLOP", 22, Theme.Gold, TextAlignmentOptions.Left, bold: true);
            sectionLabel.rectTransform.Pin(Left, 0, 0, 180, 40);
            sectionLabel.characterSpacing = 4;
            CardRow(section, "Cards", Left, 180, 0, 400, 54, TextAnchor.MiddleLeft, 0.95f);

            var line = MakeText(templates, "LineTemplate", "Ann raises to 60", 22, Theme.Text, TextAlignmentOptions.Left);
            line.rectTransform.At(0, 0, 840, 32);
            line.enableWordWrapping = true;
            line.richText = true;
            line.margin = new Vector4(16, 0, 0, 0);

            var cardLine = Node("CardLineTemplate", templates).At(0, 0, 840, 58);
            cardLine.gameObject.AddComponent<LayoutElement>().preferredHeight = 60;
            MakeText(cardLine, "Label", "Bob", 22, Theme.Text, TextAlignmentOptions.Left).rectTransform.Pin(Left, 16, 0, 220, 40);
            CardRow(cardLine, "Cards", Left, 240, 0, 110, 56, TextAnchor.MiddleLeft, 0.95f);
            MakeText(cardLine, "Info", "Two Pair", 22, Theme.Gold, TextAlignmentOptions.Left, bold: true).rectTransform.Pin(Left, 370, 0, 460, 40);

            var mini = Img(templates, "MiniCardTemplate", UISprites.Rounded, Color.white, 8f);
            mini.rectTransform.At(0, 0, 40, 56);
            var miniText = MakeText(mini.transform, "Text", "A♠", 24, Color.black, bold: true);
            miniText.rectTransform.Fill(2, 2, 2, 2);
            miniText.enableAutoSizing = true;
            miniText.fontSizeMin = 12;
            miniText.fontSizeMax = 24;

            foreach (var t in new Component[] { row, section, line, cardLine, mini }) t.gameObject.SetActive(false);

            var panel = root.gameObject.AddComponent<HandHistoryPanel>();
            ProfileUIBuilder.WireModal(panel, group, card, openButton, close, backdrop);
            Wire(panel, "listContent", listContent);
            Wire(panel, "rowTemplate", row);
            Wire(panel, "listStatusText", listStatus);
            WireColor(panel, "rowSelectedColor", Theme.Gold.WithAlpha(0.22f));
            Wire(panel, "detailRoot", detail.gameObject);
            Wire(panel, "detailEmptyText", empty);
            Wire(panel, "detailTitle", title);
            Wire(panel, "detailInfo", info);
            Wire(panel, "yourCards", yourCards);
            Wire(panel, "boardCards", boardCards);
            Wire(panel, "noBoardText", noBoard);
            Wire(panel, "resultText", result);
            Wire(panel, "detailScroll", logScroll);
            Wire(panel, "detailContent", logContent);
            Wire(panel, "sectionTemplate", section);
            Wire(panel, "lineTemplate", line);
            Wire(panel, "cardLineTemplate", cardLine);
            Wire(panel, "miniCardTemplate", mini.rectTransform);
            return panel;
        }

        /// <summary>Row of mini cards (filled at runtime). Scale makes the same card template bigger or smaller.</summary>
        private static RectTransform CardRow(Transform parent, string name, Vector2 anchor, float x, float y, float w, float h,
            TextAnchor align, float scale)
        {
            var rowRect = Node(name, parent).Pin(anchor, x, y, w / scale, h / scale);
            rowRect.localScale = Vector3.one * scale;
            var layout = rowRect.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 6f;
            layout.childAlignment = align;
            layout.childControlWidth = layout.childControlHeight = false;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            return rowRect;
        }

        private static void SmallLabel(Transform parent, string text, float x, float y)
        {
            var label = MakeText(parent, "Label", text, 18, Theme.Gold, TextAlignmentOptions.Left, bold: true);
            label.rectTransform.Pin(TopLeft, x, y, 200, 26);
            label.characterSpacing = 4;
        }
    }
}
