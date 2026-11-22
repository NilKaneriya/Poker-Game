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
    /// Builds the account UI: avatar, lobby profile card, profile editor, settings button
    /// and settings panel. Called by PokerSceneBuilder; all layout for these lives here.
    /// </summary>
    public static class ProfileUIBuilder
    {
        // ---------- Avatar ----------

        public static AvatarView BuildAvatar(Transform parent, string name, float size, bool withSelectedRing = false)
        {
            var bg = Img(parent, name, UISprites.Circle, Avatars.Colors[0]);
            bg.rectTransform.sizeDelta = new Vector2(size, size);
            var symbol = MakeText(bg.transform, "Symbol", Avatars.Symbols[0], size * 0.5f, Color.white, bold: true);
            symbol.rectTransform.Fill();

            GameObject ring = null;
            if (withSelectedRing)
            {
                var r = Img(bg.transform, "SelectedRing", UISprites.Ring, Theme.Gold);
                r.rectTransform.Fill(-8, -8, -8, -8);
                ring = r.gameObject;
                ring.SetActive(false);
            }

            var view = bg.gameObject.AddComponent<AvatarView>();
            Wire(view, "background", bg);
            Wire(view, "symbol", symbol);
            if (ring != null) Wire(view, "selectedRing", ring);
            return view;
        }

        // ---------- Lobby profile card ----------

        /// <summary>Avatar + display name + "Edit profile" in the top bar. Returns the card's button.</summary>
        public static Button BuildProfileCard(Transform parent, Vector2 anchor, float x, float y)
        {
            var card = MakeButton(parent, "ProfileCard", "", Theme.PanelLight.WithAlpha(0.7f), Theme.Text, 20, 20f);
            card.GetComponent<RectTransform>().Pin(anchor, x, y, 380, 80);
            Object.DestroyImmediate(LabelOf(card).gameObject);

            var avatar = BuildAvatar(card.transform, "Avatar", 60);
            avatar.GetComponent<RectTransform>().Pin(new Vector2(0f, 0.5f), 12, 0, 60, 60);
            var name = MakeText(card.transform, "Name", "Player", 26, Theme.Text, TextAlignmentOptions.Left, bold: true);
            name.rectTransform.Pin(new Vector2(0f, 0.5f), 86, 12, 280, 34);
            var hint = MakeText(card.transform, "Hint", ProfileTexts.Edit, 18, Theme.Gold, TextAlignmentOptions.Left);
            hint.rectTransform.Pin(new Vector2(0f, 0.5f), 86, -18, 280, 26);

            var view = card.gameObject.AddComponent<ProfileCardView>();
            Wire(view, "avatar", avatar);
            Wire(view, "nameText", name);
            Wire(view, "hintText", hint);
            return card;
        }

        // ---------- Settings ----------

        public static Button BuildSettingsButton(Transform parent, Vector2 anchor, float x, float y)
        {
            var button = MakeButton(parent, "SettingsButton", SettingTexts.Button, Theme.Dark.WithAlpha(0.85f), Theme.Gold, 22, 16f);
            button.GetComponent<RectTransform>().Pin(anchor, x, y, 180, 50);
            return button;
        }

        public static SettingsPanel BuildSettingsPanel(Transform canvas, Button openButton)
        {
            var root = ModalShell(canvas, "SettingsPanel", SettingTexts.Title, 780, 760, out RectTransform card, out Button close, out Button backdrop, out CanvasGroup group);
            LabelOf(close).text = SettingTexts.Close;

            float y = 250;
            Section(card, SettingTexts.AudioSection, y);
            var musicToggle = ToggleRow(card, "MusicToggle", SettingTexts.MusicVolume, y -= 56, 300);
            var musicSlider = VolumeSlider(card, "MusicVolume", y);
            var soundToggle = ToggleRow(card, "SoundToggle", SettingTexts.SoundVolume, y -= 62, 300);
            var soundSlider = VolumeSlider(card, "SoundVolume", y);

            Section(card, SettingTexts.TableSection, y -= 78);
            var strength = ToggleRow(card, "HandStrengthToggle", SettingTexts.ShowHandStrength, y -= 56);
            var fourColor = ToggleRow(card, "FourColorToggle", SettingTexts.FourColorDeck, y -= 56);
            var chat = ToggleRow(card, "ChatToggle", SettingTexts.ChatEnabled, y -= 56);

            Section(card, SettingTexts.PlaySection, y -= 78);
            var breaks = ToggleRow(card, "BreakRemindersToggle", SettingTexts.BreakReminderToggle, y -= 56);

            var hint = MakeText(card, "SyncHint", SettingTexts.SyncedHint, 19, Theme.Muted, TextAlignmentOptions.Left);
            hint.rectTransform.Pin(new Vector2(0.5f, 0f), 0, 30, 700, 30);

            var panel = root.gameObject.AddComponent<SettingsPanel>();
            WireModal(panel, group, card, openButton, close, backdrop);
            Wire(panel, "musicToggle", musicToggle);
            Wire(panel, "musicSlider", musicSlider);
            Wire(panel, "soundToggle", soundToggle);
            Wire(panel, "soundSlider", soundSlider);
            Wire(panel, "handStrengthToggle", strength);
            Wire(panel, "fourColorToggle", fourColor);
            Wire(panel, "chatToggle", chat);
            Wire(panel, "breakRemindersToggle", breaks);
            Wire(panel, "syncHintText", hint);
            return panel;
        }

        // ---------- Profile editor ----------

        public static ProfilePanel BuildProfilePanel(Transform canvas, Button openButton, ToastView toast)
        {
            var root = ModalShell(canvas, "ProfilePanel", ProfileTexts.Title, 980, 700, out RectTransform card, out Button close, out Button backdrop, out CanvasGroup group);
            LabelOf(close).text = "X";

            // Left: big avatar, login name, member since
            var preview = BuildAvatar(card, "Preview", 180);
            preview.GetComponent<RectTransform>().anchoredPosition = new Vector2(-300, 110);
            var username = MakeText(card, "Username", "", 22, Theme.Muted);
            username.rectTransform.At(-300, -20, 320, 32);
            var since = MakeText(card, "MemberSince", "", 20, Theme.Muted);
            since.rectTransform.At(-300, -54, 320, 30);

            // Right: name, bio, avatar grid
            const float right = 150;
            Label(card, ProfileTexts.DisplayName, right, 230);
            var nameInput = MakeInput(card, "DisplayNameInput", ProfileTexts.DisplayNameHint, 26);
            nameInput.GetComponent<RectTransform>().At(right, 182, 560, 64);

            Label(card, ProfileTexts.Bio, right, 122);
            var bioInput = MakeInput(card, "BioInput", ProfileTexts.BioHint, 22);
            bioInput.GetComponent<RectTransform>().At(right, 74, 560, 64);

            Label(card, ProfileTexts.Avatar, right, 14);
            var avatarButtons = new Object[Avatars.Count];
            const int perRow = 6;
            for (int i = 0; i < Avatars.Count; i++)
            {
                var view = BuildAvatar(card, "Avatar" + i, 70, withSelectedRing: true);
                view.GetComponent<RectTransform>().anchoredPosition = new Vector2(
                    right - 230 + (i % perRow) * 92, -48 - (i / perRow) * 92);
                var button = view.gameObject.AddComponent<Button>();
                button.targetGraphic = view.GetComponent<Image>();
                view.gameObject.AddComponent<ButtonClickSound>();
                avatarButtons[i] = button;
            }

            var status = MakeText(card, "Status", "", 21, Theme.Muted, TextAlignmentOptions.Left);
            status.rectTransform.Pin(new Vector2(0f, 0f), 44, 46, 640, 56);
            var save = MakeButton(card, "SaveButton", ProfileTexts.Save, Theme.Gold, Theme.Dark, 28);
            save.GetComponent<RectTransform>().Pin(new Vector2(1f, 0f), -40, 36, 220, 70);

            var panel = root.gameObject.AddComponent<ProfilePanel>();
            WireModal(panel, group, card, openButton, close, backdrop);
            Wire(panel, "preview", preview);
            Wire(panel, "usernameText", username);
            Wire(panel, "memberSinceText", since);
            Wire(panel, "displayNameInput", nameInput);
            Wire(panel, "bioInput", bioInput);
            WireArray(panel, "avatarButtons", avatarButtons);
            Wire(panel, "saveButton", save);
            Wire(panel, "statusText", status);
            Wire(panel, "toast", toast);
            return panel;
        }

        // ---------- Pieces ----------

        /// <summary>Full-screen dim backdrop + centred card with a title and a close button.</summary>
        internal static RectTransform ModalShell(Transform canvas, string name, string title, float w, float h,
            out RectTransform card, out Button close, out Button backdrop, out CanvasGroup group)
        {
            var root = Node(name, canvas).Fill();
            group = Group(root, 0f);
            var dim = Img(root, "Dim", null, Color.black.WithAlpha(0.65f), raycast: true);
            dim.rectTransform.Fill();
            backdrop = dim.gameObject.AddComponent<Button>();
            backdrop.transition = Selectable.Transition.None;

            var panel = Card(root, "Card", Theme.Panel, 30f);
            panel.raycastTarget = true; // clicks on the card must not reach the backdrop
            panel.rectTransform.At(0, 0, w, h);
            card = panel.rectTransform;
            Panel(card, "HeaderBar", Theme.Gold, 7f).rectTransform.Pin(new Vector2(0.5f, 1f), 0, -16, 260, 12);
            MakeText(card, "Title", title, 40, Theme.Gold, TextAlignmentOptions.Left, bold: true)
                .rectTransform.Pin(new Vector2(0f, 1f), 40, -34, 600, 52);
            close = MakeButton(card, "CloseButton", "X", Theme.PanelLight, Theme.Text, 24, 20f);
            close.GetComponent<RectTransform>().Pin(new Vector2(1f, 1f), -28, -28, 120, 56);
            return root;
        }

        internal static void WireModal(ModalPanel panel, CanvasGroup group, RectTransform card, Button open, Button close, Button backdrop)
        {
            Wire(panel, "group", group);
            Wire(panel, "card", card);
            Wire(panel, "openButton", open);
            Wire(panel, "closeButton", close);
            Wire(panel, "backdropButton", backdrop);
        }

        private static void Section(RectTransform card, string text, float y) =>
            MakeText(card, "Section", text.ToUpperInvariant(), 20, Theme.Gold, TextAlignmentOptions.Left, bold: true)
                .rectTransform.At(0, y, 680, 30);

        private static void Label(RectTransform card, string text, float x, float y) =>
            MakeText(card, "Label", text, 22, Theme.Muted, TextAlignmentOptions.Left, bold: true).rectTransform.At(x, y, 560, 30);

        private static Toggle ToggleRow(RectTransform card, string name, string label, float y, float width = 680)
        {
            var toggle = MakeToggle(card, name, label, true);
            toggle.GetComponent<RectTransform>().Pin(new Vector2(0f, 0.5f), 50, y, width, 44);
            return toggle;
        }

        private static Slider VolumeSlider(RectTransform card, string name, float y)
        {
            var slider = MakeSlider(card, name);
            slider.wholeNumbers = false;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.GetComponent<RectTransform>().Pin(new Vector2(1f, 0.5f), -50, y, 340, 44);
            return slider;
        }
    }
}
