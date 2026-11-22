const { STAGES, ACTIONS } = require('../engine/constants');

const STREETS = [STAGES.PREFLOP, STAGES.FLOP, STAGES.TURN, STAGES.RIVER];
const BOARD_SLICE = { [STAGES.PREFLOP]: [0, 0], [STAGES.FLOP]: [0, 3], [STAGES.TURN]: [3, 4], [STAGES.RIVER]: [4, 5] };
const BLIND_KIND = { 'small blind': 'sb', 'big blind': 'bb' };

/**
 * Turns a finished hand into a hand-history record, as one player is allowed to see it:
 * their own hole cards always, other players' only if they were shown at showdown.
 * Pure function: the hand and table info in, a plain object out (saved and sent as-is).
 *
 * @param {object} table   { code, name }
 * @param {HoldemHand} hand  a complete hand (hand.info is set by Table.startHand)
 * @param {string} viewerId
 */
function buildHandRecord(table, hand, viewerId) {
  const info = hand.info || { handNumber: 0, startedAt: Date.now(), buttonSeat: -1, seats: [] };
  const seatOf = new Map(info.seats.map((s) => [s.playerId, s]));
  const nameOf = (id) => seatOf.get(id)?.name ?? 'Player';
  const result = hand.result || { winners: [], shownHands: {} };
  const shown = result.shownHands || {};
  const handNames = result.handNames || {};

  const players = hand.players.map((p) => {
    const seat = seatOf.get(p.playerId);
    const isYou = p.playerId === viewerId;
    const cards = isYou || shown[p.playerId] ? p.holeCards.slice() : [];
    return {
      playerId: p.playerId,
      name: nameOf(p.playerId),
      seat: seat?.seat ?? -1,
      isBot: !!seat?.isBot,
      isButton: seat?.seat === info.buttonSeat,
      isYou,
      startChips: p.startStack,
      endChips: p.stack,
      net: p.stack - p.startStack,
      folded: p.folded,
      cards,
      handName: cards.length ? handNames[p.playerId] ?? '' : '',
    };
  });

  const me = players.find((p) => p.isYou);
  return {
    id: `${table.code}-${info.handNumber}-${info.startedAt}`,
    tableCode: table.code,
    tableName: table.name,
    handNumber: info.handNumber,
    startedAt: info.startedAt,
    smallBlind: hand.smallBlind,
    bigBlind: hand.bigBlind,
    board: hand.board.slice(),
    pot: result.winners.reduce((sum, w) => sum + w.amount, 0),
    you: me ? { cards: me.cards, net: me.net, folded: me.folded, handName: me.handName } : null,
    players,
    streets: buildStreets(hand, nameOf),
    winners: result.winners.map((w) => ({ playerId: w.playerId, name: nameOf(w.playerId), amount: w.amount, handName: w.handName || '' })),
  };
}

/** One entry per street that was reached: the new board cards and the actions on it. */
function buildStreets(hand, nameOf) {
  const streets = [];
  for (const stage of STREETS) {
    const [from, to] = BOARD_SLICE[stage];
    const cards = hand.board.slice(from, to);
    const entries = (hand.log || []).filter((e) => e.stage === stage || (stage === STAGES.RIVER && e.stage === STAGES.SHOWDOWN));
    if (stage !== STAGES.PREFLOP && cards.length === 0) break; // the hand ended before this street

    let streetBet = 0; // highest bet so far on this street: tells "bet" from "raise"
    const actions = entries.map((e) => {
      const kind = kindOf(e, streetBet);
      if (e.action !== 'returned') streetBet = Math.max(streetBet, e.total);
      const action = { playerId: e.playerId, name: nameOf(e.playerId), kind, amount: e.amount, total: e.total };
      if (e.timeout) action.timeout = true;
      if (e.left) action.left = true;
      return action;
    });
    streets.push({ stage, cards, actions });
  }
  return streets;
}

/** sb, bb, fold, check, call, bet, raise, allIn, returned */
function kindOf(entry, streetBet) {
  if (BLIND_KIND[entry.action]) return BLIND_KIND[entry.action];
  if (entry.action === ACTIONS.RAISE) return streetBet === 0 ? 'bet' : 'raise';
  return entry.action;
}

module.exports = { buildHandRecord };
