using System.Collections.Generic;
using Poker.Core;
using Poker.Network;
using Poker.Platform;
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
        [Header("Header")]
        [SerializeField] private TMP_Text welcomeText;
        [SerializeField] private Button logoutButton;

        [Header("Create / join")]
        [SerializeField] private OptionButtonGroup modeGroup;  // buttons in TableModes.LobbyOrder
        [SerializeField] private OptionButtonGroup seatGroup;  // button i = GameRules.MinSeats + i seats
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

        [Header("Your stats")]
        [SerializeField] private PlayerStatsView statsView;

        [SerializeField] private ToastView toast;

        private readonly List<TableListItemView> rows = new List<TableListItemView>();
        private string inviteToJoin; // room code from an invite link, joined once connected
        private NetworkManager Net => NetworkManager.Instance;
        private AuthManager Auth => AuthManager.Instance;

        private string SelectedMode => TableModes.LobbyOrder[Mathf.Clamp(modeGroup.Selected, 0, TableModes.LobbyOrder.Length - 1)];
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

            inviteToJoin = InviteLinkService.TakePending();

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
            Net.StatsReceived += statsView.Show;
            Net.ServerErrorReceived += toast.Show;
            Net.AuthRejected += BackToSplash;
            InviteLinkService.Received += OnInviteReceived;

            ShowTables(new List<TableSummary>());
            string notice = SceneNotice.Take(); // e.g. "your table was closed"
            if (notice != null) toast.Show(notice, 6f);

            if (Net.IsConnected)
            {
                Net.RequestTableList();
                Net.RequestStats();
                JoinInvite();
            }
            else if (!Net.HasConnectedOnce) Net.Connect(); // otherwise auto-reconnect is already running
        }

        private void OnDestroy()
        {
            InviteLinkService.Received -= OnInviteReceived;
            if (Net == null) return;
            Net.SessionStarted -= OnSessionStarted;
            Net.TableListReceived -= ShowTables;
            Net.TableStateReceived -= OnSeated;
            if (statsView != null) Net.StatsReceived -= statsView.Show;
            if (toast != null) Net.ServerErrorReceived -= toast.Show;
            Net.AuthRejected -= BackToSplash;
        }

        private void OnSessionStarted(SessionInfo _)
        {
            Net.RequestTableList(); // stats arrive with every new session
            JoinInvite();
        }

        // ---------- Invite links ----------

        private void OnInviteReceived(string code)
        {
            inviteToJoin = InviteLinkService.TakePending() ?? code;
            if (Net.IsConnected) JoinInvite();
        }

        private void JoinInvite()
        {
            if (inviteToJoin == null) return;
            toast.Show(string.Format(LobbyTexts.JoiningInvite, inviteToJoin));
            Net.JoinTable(inviteToJoin);
            inviteToJoin = null;
        }

        // ---------- Create ----------

        private void SetUpCreateOptions()
        {
            int savedMode = System.Array.IndexOf(TableModes.LobbyOrder, PlayerPrefs.GetString(PrefsKeys.CreateMode, TableModes.Friends));
            int savedSeats = PlayerPrefs.GetInt(PrefsKeys.CreateSeats, GameRules.DefaultSeats);
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
                ? string.Format(LobbyTexts.BotsHint, bots, bots == 1 ? "" : "s")
                : string.Format(LobbyTexts.FriendsHint, seats);
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
                toast.Show(string.Format(LobbyTexts.BadCodeLength, GameRules.TableCodeLength));
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
