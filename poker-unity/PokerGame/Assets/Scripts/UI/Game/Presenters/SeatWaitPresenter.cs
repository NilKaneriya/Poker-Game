using static Poker.Core.Constants;
using static Poker.Network.DataModel;

namespace Poker.UI.Game
{
    /// <summary>
    /// A friend who joins a game against bots while a hand is running watches it first;
    /// when the hand ends a bot stands up and they take its seat (the server decides this).
    /// This tells them what is happening: once when they start waiting, once when seated.
    /// </summary>
    public class SeatWaitPresenter
    {
        private readonly ToastView toast;
        private bool wasWaiting;

        public SeatWaitPresenter(ToastView toast)
        {
            this.toast = toast;
        }

        public void Render(TableState state)
        {
            bool waiting = state.you != null && state.you.waiting;
            bool seated = state.you != null && state.you.seat >= 0;

            if (waiting && !wasWaiting) toast.Show(GameTexts.WaitingForSeat, GameTexts.WaitingForSeatToastSeconds);
            else if (wasWaiting && seated) toast.Show(GameTexts.TookBotSeat);

            wasWaiting = waiting;
        }
    }
}
