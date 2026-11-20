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
    /// Lobby: create a table (vs bots or with friends, 2-6 seats), join one with a code,
    /// or pick from the open tables list. Invite links land here too and join directly.
    /// As soon as the server seats us (tableState arrives) we switch to the Game scene.
    /// </summary>
    public class LobbyController : MonoBehaviour
    {
        private static readonly string[] Modes = { TableModes.Bots, TableModes.Friends }; // order of modeGroup's buttons

        [Header("Header")]
        [SerializeField] private TMP_Text welcomeText;
        [SerializeField] private Button logoutButton;

        [Header("Create / join")]
        [SerializeField] private OptionGroup modeGroup;  // 0 = bots, 1 = friends
        [SerializeField] private OptionGroup seatGroup;  // button i = GameRules.MinSeats + i seats
        [SerializeField] private TMP_Text modeHintText;
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
        private string inviteToJoin; // room code from an invite link, joined once connected
        private NetworkManager Net => NetworkManager.Instance;
        private AuthManager Auth => AuthManager.Instance;

        private string SelectedMode => Modes[Mathf.Clamp(modeGroup.Selected, 0, Modes.Length - 1)];
        private int SelectedSeats => GameRules.MinSeats + Mathf.Max(0, seatGroup.Selected);

        private void Awake() => AppServices.EnsureExists();

        private void Start()
        {
            // Opened directly without logging in (e.g. pressing Play on this scene).
            // An invite code stays pending through the login screen.
            if (!Auth.IsLoggedIn)
            {
                SceneManager.LoadScene(SceneNames.Splash);
                return;
            }

            inviteToJoin = InviteLink.TakePending();

            // Reconnected while still seated at a table: go straight back to it,
            // unless an invite link asks for a different table.
            if (Net.CurrentTable != null && (inviteToJoin == null || inviteToJoin == Net.CurrentTable.code))
            {
                SceneManager.LoadScene(SceneNames.Game);
                return;
            }

            welcomeText.text = $"Hi, <b>{Auth.Username}</b>";
            codeInput.characterLimit = GameRules.TableCodeLength;
            codeInput.onValueChanged.AddListener(v => codeInput.SetTextWithoutNotify(v.ToUpperInvariant()));
            codeInput.onSubmit.AddListener(_ => JoinByCode());

            SetUpCreateOptions();
            createButton.onClick.AddListener(CreateTable);
            joinButton.onClick.AddListener(JoinByCode);
            refreshButton.onClick.AddListener(() => Net.RequestTableList());
            logoutButton.onClick.AddListener(Logout);

            Net.SessionStarted += OnSessionStarted;
            Net.TableListReceived += ShowTables;
            Net.TableStateReceived += OnSeated;
            Net.ServerErrorReceived += toast.Show;
            Net.AuthRejected += BackToSplash;
            InviteLink.Received += OnInviteReceived;

            ShowTables(new List<TableSummary>());
            if (Net.IsConnected)
            {
                Net.RequestTableList();
                JoinInvite();
            }
            else if (!Net.HasConnectedOnce) Net.Connect(); // otherwise auto-reconnect is already running
        }

        private void OnDestroy()
        {
            InviteLink.Received -= OnInviteReceived;
            if (Net == null) return;
            Net.SessionStarted -= OnSessionStarted;
            Net.TableListReceived -= ShowTables;
            Net.TableStateReceived -= OnSeated;
            if (toast != null) Net.ServerErrorReceived -= toast.Show;
            Net.AuthRejected -= BackToSplash;
        }

        private void OnSessionStarted(SessionInfo _)
        {
            Net.RequestTableList();
            JoinInvite();
        }

        // ---------- Invite links ----------

        private void OnInviteReceived(string code)
        {
            inviteToJoin = InviteLink.TakePending() ?? code;
            if (Net.IsConnected) JoinInvite();
        }

        private void JoinInvite()
        {
            if (inviteToJoin == null) return;
            toast.Show($"Joining table {inviteToJoin}...");
            Net.JoinTable(inviteToJoin);
            inviteToJoin = null;
        }

        // ---------- Create ----------

        private void SetUpCreateOptions()
        {
            int savedMode = System.Array.IndexOf(Modes, PlayerPrefs.GetString(PrefsKeys.CreateMode, TableModes.Friends));
            int savedSeats = PlayerPrefs.GetInt(PrefsKeys.CreateSeats, 4);
            modeGroup.Select(Mathf.Max(0, savedMode), notify: false);
            seatGroup.Select(Mathf.Clamp(savedSeats, GameRules.MinSeats, GameRules.MaxSeats) - GameRules.MinSeats, notify: false);
            UpdateModeHint();

            modeGroup.Changed += _ => OnCreateOptionsChanged();
            seatGroup.Changed += _ => OnCreateOptionsChanged();
        }

        private void OnCreateOptionsChanged()
        {
            PlayerPrefs.SetString(PrefsKeys.CreateMode, SelectedMode);
            PlayerPrefs.SetInt(PrefsKeys.CreateSeats, SelectedSeats);
            UpdateModeHint();
        }

        private void UpdateModeHint()
        {
            int seats = SelectedSeats;
            int bots = seats - 1;
            modeHintText.text = SelectedMode == TableModes.Bots
                ? $"You + {bots} bot{(bots == 1 ? "" : "s")}. The game starts right away."
                : $"You get a room code to share. The game starts when all {seats} seats are filled.";
        }

        private void CreateTable()
        {
            Net.CreateTable(tableNameInput.text.Trim(), SelectedMode, SelectedSeats);
        }

        // ---------- Join ----------

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
