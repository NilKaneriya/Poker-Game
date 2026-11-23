using Poker.Core;
using Poker.Network;
using TMPro;
using UnityEngine;
using static Poker.Core.Constants;

namespace Poker.UI
{
    /// <summary>
    /// Shows the player's wallet (chips outside any table) in the format "10,000 chips  +".
    /// The number only ever comes from the server; this view cannot change it.
    /// </summary>
    public class WalletView : MonoBehaviour
    {
        [SerializeField] private TMP_Text balanceText;

        private NetworkManager Net => NetworkManager.Instance;

        private void Awake() => AppServices.EnsureExists();

        private void Start()
        {
            Net.WalletReceived += Show;
            if (Net.Wallet >= 0) Show(Net.Wallet);
            else
            {
                balanceText.text = ShopTexts.Loading;
                if (Net.IsConnected) Net.RequestWallet();
            }
        }

        private void OnDestroy()
        {
            if (Net != null) Net.WalletReceived -= Show;
        }

        private void Show(int balance) => balanceText.text = string.Format(ShopTexts.WalletButton, CardFormat.Chips(balance));
    }
}
