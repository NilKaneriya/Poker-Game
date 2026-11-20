using Poker.Network;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Poker.UI
{
    /// <summary>
    /// Small online/offline indicator, plus an optional banner that explains a lost
    /// connection ("your seat is kept") instead of failing silently.
    /// </summary>
    public class ConnectionStatusView : MonoBehaviour
    {
        [SerializeField] private Image dot;
        [SerializeField] private TMP_Text label;
        [SerializeField] private GameObject lostBanner; // optional

        [SerializeField] private Color onlineColor = new Color(0.18f, 0.8f, 0.44f);
        [SerializeField] private Color offlineColor = new Color(0.9f, 0.28f, 0.3f);

        private enum State { Unknown, Connecting, Online, Reconnecting }
        private State shown = State.Unknown;

        private void Update()
        {
            var net = NetworkManager.Instance;
            State now = net == null ? State.Connecting
                : net.IsConnected ? State.Online
                : net.HasConnectedOnce ? State.Reconnecting
                : State.Connecting;

            if (now == shown) return; // only touch the UI when the state changes
            shown = now;

            bool online = now == State.Online;
            dot.color = online ? onlineColor : offlineColor;
            label.text = now switch
            {
                State.Online => "Online",
                State.Reconnecting => "Reconnecting...",
                _ => "Connecting...",
            };
            if (lostBanner != null) lostBanner.SetActive(now == State.Reconnecting);
        }
    }
}
