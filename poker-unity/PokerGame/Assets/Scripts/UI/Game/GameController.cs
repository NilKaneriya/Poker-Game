using System.Collections.Generic;
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
    /// Game scene: draws the table from the latest server snapshot (TableState).
    /// It holds no game rules - every update simply re-renders what the server sent,
    /// and player input is forwarded to the server as an intention.
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
        [Tooltip("Seat views in screen order, clockwise. Index 0 is the bottom seat (you).")]
        [SerializeField] private SeatView[] seats = new SeatView[6];
        [SerializeField] private CardView[] boardCards = new CardView[5];
        [SerializeField] private TMP_Text potText;
        [SerializeField] private GameObject potRoot;
        [SerializeField] private TMP_Text stageText;
        [SerializeField] private TMP_Text waitingText;

        [Header("Panels")]
        [SerializeField] private ActionBar actionBar;
        [SerializeField] private ResultPanel resultPanel;
        [SerializeField] private ToastView toast;

        [Header("Editor")]
        [Tooltip("When this scene is played without a server connection, show sample data so the layout can be checked.")]
        [SerializeField] private bool previewWhenOffline = true;

        private NetworkManager Net => NetworkManager.Instance;
        private TableState table;
        private int shownPot = -1;
        private int resultShownForHand = -1;

        private void Awake() => AppServices.EnsureExists();

        private void Start()
        {
            leaveButton.onClick.AddListener(LeaveTable);
            copyCodeButton.onClick.AddListener(CopyCode);
            actionBar.ActionChosen += (action, amount) => Net.SendAction(action, amount);
            resultPanel.RebuyClicked += () => Net.Rebuy();
            resultPanel.LobbyClicked += LeaveTable;

            Net.TableStateReceived += Render;
            Net.ActionRejected += OnActionRejected;
            Net.ServerErrorReceived += toast.Show;
            Net.AuthRejected += OnAuthRejected;

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
            if (toast != null) Net.ServerErrorReceived -= toast.Show;
            Net.AuthRejected -= OnAuthRejected;
        }

        // ---------- Rendering ----------

        /// <summary>Redraw everything from a server snapshot.</summary>
        public void Render(TableState state)
        {
            table = state;
            HandState hand = state.hand;

            tableNameText.text = state.name;
            codeText.text = state.code;
            blindsText.text = $"Blinds {state.smallBlind}/{state.bigBlind}";

            RenderSeats(state, hand);
            RenderBoard(hand);
            RenderActionBar(state, hand);
            RenderResult(state, hand);
        }

        private void RenderSeats(TableState state, HandState hand)
        {
            int mySeat = Mathf.Max(0, state.you?.seat ?? 0);
            int seatCount = state.seats.Count;

            for (int i = 0; i < seats.Length; i++) seats[i].gameObject.SetActive(i < seatCount);

            for (int serverSeat = 0; serverSeat < seatCount; serverSeat++)
            {
                // Rotate the table so we always sit at the bottom (screen index 0).
                SeatView view = seats[ScreenIndex(serverSeat, mySeat, seatCount)];
                SeatInfo seat = state.seats[serverSeat];
                if (seat == null) { view.ShowEmpty(); continue; }

                SeatHandInfo seatHand = hand?.seats.Find(s => s.seat == serverSeat);
                bool isMe = serverSeat == state.you?.seat;
                List<string> faceUp = isMe ? hand?.yourCards : seatHand?.cards;

                view.Show(seat, seatHand, faceUp, serverSeat == state.buttonSeat);

                bool isTurn = hand != null && hand.currentSeat == serverSeat && hand.result == null;
                view.SetTurn(isTurn, hand?.turnTimeLeftMs / 1000f ?? 0f, hand?.turnTimeTotalMs / 1000f ?? 0f);
            }
        }

        private void RenderBoard(HandState hand)
        {
            for (int i = 0; i < boardCards.Length; i++)
            {
                if (hand != null && i < hand.board.Count) boardCards[i].ShowFace(hand.board[i]);
                else boardCards[i].Clear();
            }

            int pot = hand?.pot ?? 0;
            potRoot.SetActive(pot > 0);
            if (pot != shownPot)
            {
                UIAnim.CountTo(potText, Mathf.Max(0, shownPot), pot, p => $"Pot {CardFormat.Chips(p)}");
                UIAnim.Punch(potRoot.transform, 0.12f);
                shownPot = pot;
            }

            stageText.gameObject.SetActive(hand != null);
            if (hand != null) stageText.text = StageLabel(hand.stage);

            waitingText.gameObject.SetActive(hand == null);
            if (hand == null)
            {
                int players = table.seats.FindAll(s => s != null).Count;
                waitingText.text = players < 2
                    ? $"Waiting for players...\nShare code <b>{table.code}</b> with friends"
                    : "Next hand starting...";
            }
        }

        private void RenderActionBar(TableState state, HandState hand)
        {
            bool myTurn = hand != null && hand.legal != null && hand.currentSeat == state.you?.seat && hand.result == null;
            if (!myTurn) { actionBar.Hide(); return; }

            int myBet = hand.seats.Find(s => s.seat == state.you.seat)?.bet ?? 0;
            actionBar.Show(hand.legal, hand.pot, myBet);
        }

        private void RenderResult(TableState state, HandState hand)
        {
            SeatInfo me = state.you != null && state.you.seat >= 0 ? state.seats[state.you.seat] : null;

            // Out of chips and not playing the current hand: watch, or restart (no purchase, ever).
            bool playingThisHand = hand != null && hand.result == null && hand.seats.Exists(s => s.seat == state.you.seat);
            if (me != null && me.chips == 0 && !playingThisHand)
            {
                if (!resultPanel.IsShowingBusted) resultPanel.ShowBusted(GameRules.StartingChips);
                return;
            }
            if (resultPanel.IsShowingBusted) resultPanel.Hide(); // e.g. after restarting

            // A new hand has started: close the previous hand's result screen.
            if (hand != null && hand.result == null) resultPanel.Hide();

            if (hand?.result == null || resultShownForHand == state.handNumber) return;
            resultShownForHand = state.handNumber;

            WinnerInfo myWin = hand.result.winners.Find(w => w.playerId == state.you?.playerId);
            WinnerInfo topWinner = hand.result.winners.Count > 0 ? hand.result.winners[0] : null;
            if (myWin != null) resultPanel.ShowWin(myWin.amount, myWin.handName, myWin.bestCards);
            else if (topWinner != null) resultPanel.ShowLoss(topWinner.name, topWinner.amount, topWinner.handName, topWinner.bestCards);

            HighlightWinningCards(state, hand, topWinner);
        }

        private void HighlightWinningCards(TableState state, HandState hand, WinnerInfo winner)
        {
            if (winner?.bestCards == null || winner.bestCards.Count == 0) return;
            var best = new HashSet<string>(winner.bestCards);

            for (int i = 0; i < boardCards.Length && i < hand.board.Count; i++)
            {
                bool used = best.Contains(hand.board[i]);
                boardCards[i].SetHighlighted(used);
                boardCards[i].SetDimmed(!used);
            }

            int mySeat = Mathf.Max(0, state.you?.seat ?? 0);
            SeatHandInfo winnerHand = hand.seats.Find(s => s.seat == winner.seat);
            List<string> holeCards = winner.playerId == state.you?.playerId ? hand.yourCards : winnerHand?.cards;
            seats[ScreenIndex(winner.seat, mySeat, state.seats.Count)].HighlightCards(best, holeCards);
        }

        // ---------- Input ----------

        private void LeaveTable()
        {
            if (Net.IsConnected) Net.LeaveTable();
            SceneManager.LoadScene(SceneNames.Lobby);
        }

        private void CopyCode()
        {
            if (table == null) return;
            GUIUtility.systemCopyBuffer = table.code;
            toast.Show($"Code {table.code} copied - share it with friends");
        }

        private void OnActionRejected(string reason) => toast.Show(reason);

        private void OnAuthRejected()
        {
            AuthManager.Instance.ClearSession();
            SceneManager.LoadScene(SceneNames.Splash);
        }

        // ---------- Helpers ----------

        private static int ScreenIndex(int serverSeat, int mySeat, int seatCount) =>
            (serverSeat - mySeat + seatCount) % seatCount;

        private static string StageLabel(string stage) => stage switch
        {
            PokerStages.Preflop => "PRE-FLOP",
            PokerStages.Flop => "FLOP",
            PokerStages.Turn => "TURN",
            PokerStages.River => "RIVER",
            PokerStages.Showdown => "SHOWDOWN",
            _ => "",
        };

        // Right-click the component header in the Inspector (Play mode) to preview the overlays.
        [ContextMenu("Preview/Sample table")] private void PreviewTable() => Render(GamePreviewData.Create());
        [ContextMenu("Preview/Win screen")] private void PreviewWin() => resultPanel.ShowWin(640, "Full House", new List<string> { "Kh", "Kd", "Ks", "7c", "7h" });
        [ContextMenu("Preview/Lose screen")] private void PreviewLoss() => resultPanel.ShowLoss("Maya", 640, "Flush", new List<string> { "Ah", "Jh", "9h", "5h", "2h" });
        [ContextMenu("Preview/Out of chips")] private void PreviewBusted() => resultPanel.ShowBusted(GameRules.StartingChips);
    }
}
