using System.Collections;
using System.Collections.Generic;
using Poker.Core;
using Poker.Network;
using TMPro;
using UnityEngine;
using static Poker.Core.Constants;
using static Poker.Network.DataModel;

namespace Poker.UI.Lobby
{
    /// <summary>
    /// The dummy chip shop. It lists what the server offers and sends "buy item X" (just the id).
    /// The server decides the amount, enforces a cooldown and answers with the new wallet, so
    /// nothing here can create chips. No real money is involved.
    /// </summary>
    public class ShopPanel : ModalPanel
    {
        private const float ButtonLockSeconds = 1.1f; // a little longer than the server's cooldown

        [SerializeField] private ShopItemView rowTemplate;
        [SerializeField] private TMP_Text walletText;
        [SerializeField] private TMP_Text statusText;

        private readonly List<ShopItemView> rows = new List<ShopItemView>();
        private NetworkManager Net => NetworkManager.Instance;

        private void Awake() => AppServices.EnsureExists();

        protected override void Start()
        {
            base.Start();
            rowTemplate.gameObject.SetActive(false);
            statusText.text = "";
            Net.ShopCatalogReceived += BuildRows;
            Net.ShopResultReceived += ShowResult;
            Net.WalletReceived += ShowWallet;
            if (Net.Catalog != null) BuildRows(Net.Catalog);
            if (Net.Wallet >= 0) ShowWallet(Net.Wallet);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (Net == null) return;
            Net.ShopCatalogReceived -= BuildRows;
            Net.ShopResultReceived -= ShowResult;
            Net.WalletReceived -= ShowWallet;
        }

        protected override void OnOpened()
        {
            statusText.text = "";
            if (Net.IsConnected) Net.RequestWallet(); // fresh balance and list
        }

        private void BuildRows(ShopCatalog catalog)
        {
            foreach (var row in rows) Destroy(row.gameObject);
            rows.Clear();
            if (catalog?.items == null) return;
            foreach (ShopItem item in catalog.items)
            {
                var row = Instantiate(rowTemplate, rowTemplate.transform.parent);
                row.gameObject.SetActive(true);
                row.Bind(item, Buy);
                rows.Add(row);
            }
        }

        private void Buy(string itemId)
        {
            if (!Net.IsConnected) { SetStatus("Not connected to the server.", false); return; }
            Net.BuyItem(itemId);
            StopAllCoroutines();
            StartCoroutine(LockButtons());
        }

        private IEnumerator LockButtons()
        {
            foreach (var row in rows) row.SetInteractable(false);
            yield return new WaitForSecondsRealtime(ButtonLockSeconds);
            foreach (var row in rows) row.SetInteractable(true);
        }

        private void ShowResult(ShopResult result) => SetStatus(result.message, result.ok);

        private void SetStatus(string message, bool ok)
        {
            statusText.text = message;
            statusText.color = ok ? new Color(0.13f, 0.77f, 0.37f) : new Color(0.9f, 0.28f, 0.3f);
        }

        private void ShowWallet(int balance) => walletText.text = string.Format(ShopTexts.Wallet, CardFormat.Chips(balance));
    }
}
