using System;
using Poker.Network;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static Poker.Core.Constants;
using static Poker.Network.DataModel;

namespace Poker.UI.Lobby
{
    /// <summary>
    /// "Your profile" editor: avatar, display name and a short bio. Saved to the player's
    /// account through ProfileManager; the server checks every value again.
    /// </summary>
    public class ProfilePanel : ModalPanel
    {
        [SerializeField] private AvatarView preview;
        [SerializeField] private TMP_Text usernameText;
        [SerializeField] private TMP_Text memberSinceText;
        [SerializeField] private TMP_InputField displayNameInput;
        [SerializeField] private TMP_InputField bioInput;
        [SerializeField] private Button[] avatarButtons; // each has an AvatarView; index = avatarId
        [SerializeField] private Button saveButton;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private ToastView toast;

        private static ProfileManager Profiles => ProfileManager.Instance;
        private int selectedAvatar;
        private bool saving;

        protected override void Start()
        {
            base.Start();
            displayNameInput.characterLimit = ProfileTexts.DisplayNameMax;
            bioInput.characterLimit = ProfileTexts.BioMax;
            for (int i = 0; i < avatarButtons.Length; i++)
            {
                int id = i;
                avatarButtons[i].GetComponent<AvatarView>().Show(id);
                avatarButtons[i].onClick.AddListener(() => SelectAvatar(id));
            }
            saveButton.onClick.AddListener(Save);
            Profiles.ProfileChanged += OnProfileLoaded;
        }

        private void OnDestroy()
        {
            if (Profiles != null) Profiles.ProfileChanged -= OnProfileLoaded;
        }

        protected override void OnOpened()
        {
            statusText.text = "";
            if (Profiles.Current == null)
            {
                statusText.text = ProfileTexts.Loading;
                saveButton.interactable = false;
                Profiles.Refresh();
                return;
            }
            Fill(Profiles.Current);
        }

        private void OnProfileLoaded(UserProfile profile)
        {
            if (IsOpen && !saving) Fill(profile);
        }

        private void Fill(UserProfile profile)
        {
            statusText.text = "";
            saveButton.interactable = true;
            usernameText.text = string.Format(ProfileTexts.Username, profile.username);
            memberSinceText.text = profile.createdAt > 0
                ? string.Format(ProfileTexts.MemberSince, DateTimeOffset.FromUnixTimeMilliseconds(profile.createdAt).LocalDateTime)
                : "";
            displayNameInput.text = profile.displayName;
            bioInput.text = profile.bio;
            SelectAvatar(profile.avatarId);
        }

        private void SelectAvatar(int id)
        {
            selectedAvatar = Avatars.Clamp(id);
            preview.Show(selectedAvatar);
            for (int i = 0; i < avatarButtons.Length; i++) avatarButtons[i].GetComponent<AvatarView>().SetSelected(i == selectedAvatar);
            UIAnim.Punch(preview.transform, 0.12f);
        }

        private void Save()
        {
            UserProfile current = Profiles.Current;
            if (current == null) return;

            // Send only what changed.
            var update = new ProfileUpdate();
            string name = displayNameInput.text.Trim();
            string bio = bioInput.text.Trim();
            if (name != current.displayName) update.displayName = name;
            if (bio != (current.bio ?? "")) update.bio = bio;
            if (selectedAvatar != current.avatarId) update.avatarId = selectedAvatar;
            if (update.displayName == null && update.bio == null && update.avatarId == null) { Close(); return; }

            saving = true;
            saveButton.interactable = false;
            statusText.color = UIColors.Text;
            statusText.text = ProfileTexts.Saving;
            Profiles.Save(update, saved =>
            {
                saving = false;
                saveButton.interactable = true;
                Close();
                if (toast != null) toast.Show(ProfileTexts.Saved);
            }, error =>
            {
                saving = false;
                saveButton.interactable = true;
                statusText.color = UIColors.Red;
                statusText.text = error; // e.g. "Display name must be 3-16 letters..."
                UIAnim.Punch(statusText.transform, 0.1f);
            });
        }
    }
}
