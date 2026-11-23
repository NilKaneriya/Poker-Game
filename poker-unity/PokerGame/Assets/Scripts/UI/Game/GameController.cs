using System.Collections.Generic;
using Poker.Chat;
using Poker.Core;
using Poker.Network;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static Poker.Core.Constants;
using static Poker.Network.DataModel;

namespace Poker.UI.Game
{
    /// <summary>
    /// Game scene coordinator. Listens to the network and hands every new table snapshot
    /// to the part of the screen responsible for it; forwards player input to the server.
    /// It holds no game rules and draws nothing itself (except the header).
    ///
    ///   SeatsView            seats around the table
    ///   BoardView            community cards, pot, street label
    ///   WaitingRoomPanel     friends tables before the game starts
    ///   ActionBar            your buttons on your turn
    ///   HandResultPresenter  win / lose / out-of-chips screens
    ///   MyHandPresenter      your current hand (pill under your chips + chart mark)
    ///   SeatWaitPresenter    joined a bots game mid-hand: "you'll sit when it ends"
    ///   TableChangeAnimator  chips, dealing and street banners between snapshots
    ///   TableSoundCues       sounds between snapshots; TurnTimerAlert ticks on your turn
    ///   TableInviteActions   copy code / link, share invite
    /// </summary>
    public class GameController : MonoBehaviour
    {
        [Header("Header")]
        [SerializeField] private TMP_Text tableNameText;
        [SerializeField] private TMP_Text codeText;
        [SerializeField] private Button copyCodeButton;
        [SerializeField] private TMP_Text blindsText;
        [SerializeField] private Button leaveButton;

        [Header("Table")]
        [SerializeField] private SeatsView seats;
        [SerializeField] private BoardView board;
        [SerializeField] private TableChangeAnimator animator;
        [SerializeField] private TurnTimerAlert timerAlert;

        [Header("Panels")]
        [SerializeField] private WaitingRoomPanel waitingRoom;
        [SerializeField] private ActionBar actionBar;
        [SerializeField] private ResultPanel resultPanel;
        [SerializeField] private HandRankingsPanel handRankingsPanel;
        [SerializeField] private HandHistoryPanel handHistoryPanel; // closed when your turn starts, so the countdown stays in view
        [SerializeField] private Button handRankingsButton;
        [SerializeField] private ToastView toast;
        [SerializeField] private ChatPanel chat; // optional (chat module)

        [Header("Editor")]
        [Tooltip("When this scene is played without a server connection, show sample data so the layout can be checked.")]
        [SerializeField] private bool previewWhenOffline = true;

        private NetworkManager Net => NetworkManager.Instance;
        private TableState table;
        private HandResultPresenter results;
        private MyHandPresenter myHand;
        private SeatWaitPresenter seatWait;
        private TableInviteActions invites;
        private TableSoundCues sounds;

        private void Awake()
        {
            AppServices.EnsureExists();
            results = new HandResultPresenter(resultPanel, board, seats);
            myHand = new MyHandPresenter(seats, handRankingsPanel);
            seatWait = new SeatWaitPresenter(toast);
            invites = new TableInviteActions(toast);
            sounds = new TableSoundCues();
        }

        private void Start()
        {
            WireInput();

            Net.TableStateReceived += Render;
            Net.ActionRejected += OnActionRejected;
            Net.ServerErrorReceived += OnServerError;
            Net.AuthRejected += OnAuthRejected;
            Net.TableLost += OnTableLost;
            SettingsManager.Instance.Changed += OnSettingsChanged;
            OnSettingsChanged(SettingsManager.Instance.Current);

            actionBar.Hide();
            resultPanel.Hide();

            if (Net.CurrentTable != null) Render(Net.CurrentTable);
            else if (previewWhenOffline && Application.isEditor) Render(GamePreviewData.Create());
            else SceneManager.LoadScene(SceneNames.Lobby);
        }

        private void OnDestroy()
        {
            if (Net == null) return;
            Net.TableStateReceived -= Render;
            Net.ActionRejected -= OnActionRejected;
            Net.ServerErrorReceived -= OnServerError;
            Net.AuthRejected -= OnAuthRejected;
            Net.TableLost -= OnTableLost;
            if (SettingsManager.Instance != null) SettingsManager.Instance.Changed -= OnSettingsChanged;
        }

        /// <summary>Settings that change what the table shows: chat on/off, hand strength hint.</summary>
        private void OnSettingsChanged(UserSettings settings)
        {
            if (chat != null) chat.SetSwitchedOn(settings.chatEnabled);
            if (table != null) myHand.Render(table);
        }

        private void WireInput()
        {
            leaveButton.onClick.AddListener(LeaveTable);
            copyCodeButton.onClick.AddListener(() => invites.CopyCode(table));
            actionBar.ActionChosen += (action, amount) => Net.SendAction(action, amount);
            resultPanel.RebuyClicked += () => Net.Rebuy();
            resultPanel.LobbyClicked += LeaveTable;
            waitingRoom.CopyCodeClicked += () => invites.CopyCode(table);
            waitingRoom.CopyLinkClicked += () => invites.CopyLink(table);
            waitingRoom.ShareClicked += () => invites.Share(table);
            waitingRoom.StartClicked += () => Net.StartGame();

            if (handRankingsButton != null && handRankingsPanel != null)
                handRankingsButton.onClick.AddListener(handRankingsPanel.Toggle);
            else
                Debug.LogWarning("[Game] Hand rankings button/panel missing from this scene. Run Tools > Poker > Build Scenes.");
        }

        /// <summary>Redraw everything from a server snapshot.</summary>
        public void Render(TableState state)
        {
            TableState previous = table;
            table = state;

            tableNameText.text = state.name;
            codeText.text = state.code;
            blindsText.text = $"Blinds {state.smallBlind}/{state.bigBlind}";

            seats.Render(state);
            board.Render(state);
            if (waitingRoom.Render(state)) board.PunchMessage(); // the game just started
            actionBar.Render(state);
            results.Render(state);
            myHand.Render(state);
            seatWait.Render(state);
            if (animator != null) animator.Play(previous, state);
            sounds.Play(previous, state);
            bool turnStarted = timerAlert != null && timerAlert.Track(state);
            if (turnStarted && handHistoryPanel != null && handHistoryPanel.IsOpen) handHistoryPanel.Close();
        }

        private void LeaveTable()
        {
            if (Net.IsConnected) Net.LeaveTable();
            SceneManager.LoadScene(SceneNames.Lobby);
        }

        private void OnActionRejected(string reason)
        {
            toast.Show(reason);
            sounds.PlayError();
        }

        private void OnServerError(string message)
        {
            toast.Show(message);
            sounds.PlayError();
            if (table != null && !table.started) waitingRoom.Render(table); // re-enable "Start now" after a refusal
        }

        /// <summary>Reconnected, but we are no longer at the table: explain why and go back to the lobby.</summary>
        private void OnTableLost(bool serverRestarted)
        {
            SceneNotice.Set(serverRestarted ? GameTexts.TableClosed : GameTexts.SeatReleased);
            SceneManager.LoadScene(SceneNames.Lobby);
        }

        private void OnAuthRejected()
        {
            AuthManager.Instance.ClearSession();
            SceneManager.LoadScene(SceneNames.Splash);
        }

        // Right-click the component header in the Inspector (Play mode) to preview the overlays.
        [ContextMenu("Preview/Sample table")] private void PreviewTable() => Render(GamePreviewData.Create());
        [ContextMenu("Preview/Waiting room (4 seats)")] private void PreviewWaiting() => Render(GamePreviewData.CreateWaitingRoom(4));
        [ContextMenu("Preview/Heads-up (2 seats)")] private void PreviewHeadsUp() => Render(GamePreviewData.CreateWaitingRoom(2, started: true));
        [ContextMenu("Preview/Hand rankings")] private void PreviewRankings() { if (handRankingsPanel != null) handRankingsPanel.Open(); }
        [ContextMenu("Preview/Win screen")] private void PreviewWin() => resultPanel.ShowWin(640, "Full House", new List<string> { "Kh", "Kd", "Ks", "7c", "7h" });
        [ContextMenu("Preview/Lose screen")] private void PreviewLoss() => resultPanel.ShowLoss("Maya", 640, "Flush", new List<string> { "Ah", "Jh", "9h", "5h", "2h" });
        [ContextMenu("Preview/Out of chips")] private void PreviewBusted() => resultPanel.ShowBusted(GameRules.StartingChips);
    }
}
