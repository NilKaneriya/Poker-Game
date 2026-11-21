using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static Poker.Core.Constants;

namespace Poker.UI
{
    /// <summary>A player's avatar: a coloured circle with a card-suit symbol (Constants.Avatars).</summary>
    public class AvatarView : MonoBehaviour
    {
        [SerializeField] private Image background;
        [SerializeField] private TMP_Text symbol;
        [SerializeField] private GameObject selectedRing; // optional: shown when picked in the profile editor

        public void Show(int avatarId)
        {
            int id = Avatars.Clamp(avatarId);
            background.color = Avatars.Colors[id];
            symbol.text = Avatars.Symbols[id];
        }

        public void SetSelected(bool selected)
        {
            if (selectedRing != null) selectedRing.SetActive(selected);
        }
    }
}
