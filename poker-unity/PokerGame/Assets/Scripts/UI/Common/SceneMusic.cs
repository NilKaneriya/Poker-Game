using Poker.Audio;
using Poker.Core;
using UnityEngine;
using static Poker.Network.DataModel;

namespace Poker.UI
{
    /// <summary>Starts this scene's music track (crossfades from the previous scene's).</summary>
    public class SceneMusic : MonoBehaviour
    {
        [SerializeField] private SoundId track = SoundId.MusicLobby;

        private void Awake() => AppServices.EnsureExists();

        private void Start() => AudioManager.Instance.PlayMusic(track);
    }
}
