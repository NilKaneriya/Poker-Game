/**
 * Builds the state one specific player is allowed to see.
 * This is the only place table/hand state is turned into client payloads,
 * so hidden information (deck, other players' hole cards) can't leak by accident.
 *
 * @param {Table} table
 * @param {string} playerId  who this view is for
 * @param {{ turnTimeLeftMs?: number, turnTimeTotalMs?: number }} [timing]
 */
function buildPlayerView(table, playerId, timing = {}) {
  const view = table.toPublic();
  view.you = { playerId, seat: table.findSeat(playerId) };
  view.hand = table.hand ? buildHandView(table, playerId, timing) : null;
  return view;
}

function buildHandView(table, playerId, timing) {
  const hand = table.hand;
  const me = hand.getPlayer(playerId); // null if watching (joined mid-hand or out of chips)
  const shown = hand.result?.shownHands || {}; // only filled at showdown
  const seatOf = (id) => table.findSeat(id);
  const isMyTurn = !hand.isComplete && hand.currentPlayerId === playerId;

  return {
    stage: hand.stage,
    board: hand.board,
    pot: hand.pot,
    currentSeat: hand.isComplete || !hand.currentPlayerId ? -1 : seatOf(hand.currentPlayerId),
    turnTimeLeftMs: timing.turnTimeLeftMs || 0,
    turnTimeTotalMs: timing.turnTimeTotalMs || 0,
    yourCards: me ? me.holeCards : [],
    legal: isMyTurn ? hand.getLegalActions(playerId) : null,
    seats: hand.players
      .filter((p) => seatOf(p.playerId) !== -1) // players who left mid-hand have no seat any more
      .map((p) => ({
        seat: seatOf(p.playerId),
        bet: p.bet,
        lastAction: p.lastAction,
        folded: p.folded,
        allIn: p.allIn,
        cards: shown[p.playerId] || null, // opponents' cards only after showdown
      })),
    result: hand.result
      ? {
          winners: hand.result.winners.map((w) => ({
            ...w,
            seat: seatOf(w.playerId),
            name: table.getSeat(w.playerId)?.name ?? 'Player',
          })),
        }
      : null,
  };
}

module.exports = { buildPlayerView };
