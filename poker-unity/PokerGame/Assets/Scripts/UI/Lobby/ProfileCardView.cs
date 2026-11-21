using Poker.Network;
using TMPro;
using UnityEngine;
using static Poker.Core.Constants;
using static Poker.Network.DataModel;

namespace Poker.UI.Lobby
{
    /// <summary>
    /// The player's avatar and name in the lobby's top bar. The whole card is a button that
    /// opens the profile editor (wired to ProfilePanel's open button).
    /// </summary>
    public class ProfileCardView : MonoBehaviour
    {
        [SerializeField] private AvatarView avatar;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text hintText;

        private static ProfileManager Profiles => ProfileManager.Instance;

        private void Start()
        {
            hintText.text = ProfileTexts.Edit;
            Profiles.ProfileChanged += Show;
            if (Profiles.Current != null) Show(Profiles.Current);
            else ShowLoginName();
        }

        private void OnDestroy()
        {
            if (Profiles != null) Profiles.ProfileChanged -= Show;
        }

        private void Show(UserProfile profile)
        {
            avatar.Show(profile.avatarId);
            nameText.text = profile.displayName;
        }

        /// <summary>Until the profile arrives: the login name and a neutral avatar.</summary>
        private void ShowLoginName()
        {
            avatar.Show(0);
            nameText.text = AuthManager.Instance != null ? AuthManager.Instance.Username : "";
        }
    }
}
