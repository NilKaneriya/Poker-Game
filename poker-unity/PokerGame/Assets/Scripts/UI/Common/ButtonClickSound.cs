using Poker.Audio;
using UnityEngine;
using UnityEngine.UI;
using static Poker.Network.DataModel;

namespace Poker.UI
{
    /// <summary>Plays the click sound when this button is pressed. Added to every button by the scene builder.</summary>
    [RequireComponent(typeof(Button))]
    public class ButtonClickSound : MonoBehaviour
    {
        [SerializeField] private SoundId sound = SoundId.ButtonClick;

        private void Awake() => GetComponent<Button>().onClick.AddListener(Play);

        private void Play()
        {
            if (AudioManager.Instance != null) AudioManager.Instance.Play(sound);
        }
    }
}
