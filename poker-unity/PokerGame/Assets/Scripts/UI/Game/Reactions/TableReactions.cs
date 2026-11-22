using System.Collections.Generic;
using Poker.Audio;
using Poker.Reactions;
using UnityEngine;
using UnityEngine.UI;
using static Poker.Network.DataModel;
using static Poker.Reactions.ReactionModels;

namespace Poker.UI.Game
{
    /// <summary>
    /// Plugs the reactions module into the poker table: players are anchored at their seat's
    /// name plate, a click on a seat opens the picker aimed at that player, and the "Emotes"
    /// buttons (table and chat window) open it for everyone.
    /// </summary>
    public class TableReactions : MonoBehaviour, IReactionScene
    {
        [SerializeField] private SeatsView seats;
        [SerializeField] private ReactionLayer layer;
        [SerializeField] private EmotePicker picker;
        [Tooltip("Buttons that open the picker (e.g. on the table and in the chat window).")]
        [SerializeField] private Button[] openButtons;
        [SerializeField] private ToastView toast;

        private readonly List<ReactionTarget> targets = new List<ReactionTarget>();
        private ReactionClient client;

        public IReadOnlyList<ReactionTarget> Targets
        {
            get
            {
                targets.Clear();
                foreach (SeatInfo seat in seats.Players) targets.Add(new ReactionTarget(seat.playerId, seat.name));
                return targets;
            }
        }

        private void Start()
        {
            client = ReactionClient.For(NetworkReactionTransport.Instance);
            layer.Bind(client, this);
            picker.Bind(client, this);

            if (client == null) // offline preview: nothing to send to
            {
                foreach (var button in openButtons) if (button != null) button.gameObject.SetActive(false);
                return;
            }

            foreach (var button in openButtons) if (button != null) button.onClick.AddListener(() => picker.Toggle());
            seats.PlayerClicked += OnPlayerClicked;
            client.ErrorReceived += OnError;
            layer.Played += OnPlayed;
        }

        private void OnDestroy()
        {
            if (seats != null) seats.PlayerClicked -= OnPlayerClicked;
            if (layer != null) layer.Played -= OnPlayed;
            if (client != null) client.ErrorReceived -= OnError;
        }

        public bool TryGetAnchor(string userId, out RectTransform anchor)
        {
            anchor = seats.TryGetSeat(userId, out SeatView seat) ? seat.Plate : null;
            return anchor != null;
        }

        /// <summary>Your own seat opens the picker for everyone; another seat aims at that player.</summary>
        private void OnPlayerClicked(string playerId) => picker.Open(playerId == client.LocalUserId ? null : playerId);

        private void OnError(string message)
        {
            if (toast != null && !string.IsNullOrEmpty(message)) toast.Show(message);
        }

        private void OnPlayed(Reaction reaction)
        {
            if (AudioManager.Instance != null) AudioManager.Instance.Play(SoundId.ButtonClick);
        }
    }
}
