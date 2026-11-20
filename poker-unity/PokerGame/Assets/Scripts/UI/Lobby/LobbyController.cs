using System.Collections.Generic;
using Poker.Core;
using Poker.Network;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static Poker.Core.Constants;
using static Poker.Network.DataModel;

namespace Poker.UI.Lobby
{
    /// <summary>
    /// Lobby: create a table, join one with a code, or pick from the open tables list.
    /// As soon as the server seats us (tableState arrives) we switch to the Game scene.
    /// </summary>
    public class LobbyController : MonoBehaviour
    {

        [Header("Header")]
        [SerializeField] private TMP_Text welcomeText;
        [SerializeField] private Button logoutButton;

        [Header("Create / join")]
        [SerializeField] private TMP_InputField tableNameInput;
        [SerializeField] private Button createButton;
        [SerializeField] private TMP_InputField codeInput;
        [SerializeField] private Button joinButton;

        [Header("Open tables")]
        [SerializeField] private Button refreshButton;
        [SerializeField] private Transform listContent;
        [SerializeField] private TableListItemView listItemPrefab;
        [SerializeField] private GameObject emptyState;

        [SerializeField] private ToastView toast;

        private readonly List<TableListItemView> rows = new List<TableListItemView>();
        private NetworkManager Net => NetworkManager.Instance;
        private AuthManager Auth => AuthManager.Instance;

        private void Awake() => AppServices.EnsureExists();

        private void Start()
        {
            // Opened directly without logging in (e.g. pressing Play on this scene).
            if (!Auth.IsLoggedIn)
            {
                SceneManager.LoadScene(SceneNames.Splash);
                return;
            }

            // Reconnected while still seated at a table: go straight back to it.
            if (Net.CurrentTable != null)
            {
                SceneManager.LoadScene(SceneNames.Game);
                return;
            }

            welcomeText.text = $"Hi, <b>{Auth.Username}</b>";
            codeInput.characterLimit = GameRules.TableCodeLength;
            codeInput.onValueChanged.AddListener(v => codeInput.SetTextWithoutNotify(v.ToUpperInvariant()));
            codeInput.onSubmit.AddListener(_ => JoinByCode());

            createButton.onClick.AddListener(CreateTable);
            joinButton.onClick.AddListener(JoinByCode);
            refreshButton.onClick.AddListener(() => Net.RequestTableList());
            logoutButton.onClick.AddListener(Logout);

            Net.SessionStarted += OnSessionStarted;
            Net.TableListReceived += ShowTables;
            Net.TableStateReceived += OnSeated;
            Net.ServerErrorReceived += toast.Show;
            Net.AuthRejected += BackToSplash;

            ShowTables(new List<TableSummary>());
            if (Net.IsConnected) Net.RequestTableList();
            else if (!Net.HasConnectedOnce) Net.Connect(); // otherwise auto-reconnect is already running
        }

        private void OnDestroy()
        {
            if (Net == null) return;
            Net.SessionStarted -= OnSessionStarted;
            Net.TableListReceived -= ShowTables;
            Net.TableStateReceived -= OnSeated;
            if (toast != null) Net.ServerErrorReceived -= toast.Show;
            Net.AuthRejected -= BackToSplash;
        }

        private void OnSessionStarted(SessionInfo _) => Net.RequestTableList();

        private void CreateTable()
        {
            Net.CreateTable(tableNameInput.text.Trim());
        }

        private void JoinByCode()
        {
            string code = codeInput.text.Trim();
            if (code.Length != GameRules.TableCodeLength)
            {
                toast.Show($"Table codes have {GameRules.TableCodeLength} letters.");
                return;
            }
            Net.JoinTable(code);
        }

        private void ShowTables(List<TableSummary> tables)
        {
            // Reuse existing rows, add more if needed, hide the rest.
            for (int i = 0; i < tables.Count; i++)
            {
                if (i >= rows.Count) rows.Add(Instantiate(listItemPrefab, listContent));
                rows[i].gameObject.SetActive(true);
                rows[i].Bind(tables[i], code => Net.JoinTable(code));
            }
            for (int i = tables.Count; i < rows.Count; i++) rows[i].gameObject.SetActive(false);

            emptyState.SetActive(tables.Count == 0);
        }

        private void OnSeated(TableState _) => SceneManager.LoadScene(SceneNames.Game);

        private void Logout()
        {
            Net.Disconnect();
            Auth.Logout();
            BackToSplash();
        }

        private void BackToSplash() => SceneManager.LoadScene(SceneNames.Splash);
    }
}
