const { STAGES, ACTIONS } = require('./constants');
const { createDeck, shuffle } = require('./cards');
const { evaluate } = require('./handEvaluator');
const { buildPots, distributePots } = require('./pots');

const BETTING_STAGES = [STAGES.PREFLOP, STAGES.FLOP, STAGES.TURN, STAGES.RIVER];
const NEXT_STAGE = { [STAGES.PREFLOP]: STAGES.FLOP, [STAGES.FLOP]: STAGES.TURN, [STAGES.TURN]: STAGES.RIVER };
const CARDS_FOR_STAGE = { [STAGES.FLOP]: 3, [STAGES.TURN]: 1, [STAGES.RIVER]: 1 };

/**
 * One hand of Texas Hold'em, from posting blinds to showdown.
 * Pure game state: no timers, sockets or I/O. The game layer (src/game) calls
 * applyAction() and reads the state back; every rule check happens here.
 *
 * Betting terms used below:
 *   bet         chips a player put in on the current street
 *   currentBet  the highest bet on this street (what everyone must match)
 *   minRaise    size of the last full raise; the next raise must be at least this big
 *   hasActed    acted since betting was last (re)opened by a full raise
 *
 * Every blind, action and returned bet is also written to `log` (for hand histories):
 *   { stage, playerId, action, amount, total, timeout? }
 *   amount = chips put in by this action, total = the player's bet on this street after it.
 */
class HoldemHand {
  /**
   * @param {object} opts
   * @param {{ playerId: string, stack: number }[]} opts.players  players dealt in, clockwise
   * @param {number} opts.buttonIndex  index into players of the dealer button
   * @param {number} opts.smallBlind
   * @param {number} opts.bigBlind
   * @param {string[]} [opts.deck]     cards in dealing order (tests); a shuffled deck otherwise
   */
  constructor({ players, buttonIndex, smallBlind, bigBlind, deck }) {
    if (players.length < 2) throw new Error('A hand needs at least 2 players');
    this.players = players.map((p) => ({
      playerId: p.playerId,
      stack: p.stack,
      startStack: p.stack,
      holeCards: [],
      bet: 0,
      totalBet: 0,
      folded: false,
      allIn: p.stack === 0,
      hasActed: false,
      lastAction: null,
    }));
    this.buttonIndex = buttonIndex;
    this.smallBlind = smallBlind;
    this.bigBlind = bigBlind;
    this.deck = deck ? deck.slice() : shuffle(createDeck());
    this.board = [];
    this.stage = STAGES.WAITING;
    this.currentIndex = -1;
    this.currentBet = 0;
    this.minRaise = bigBlind;
    this.result = null;
    this.log = [];
  }

  // =====================================================================
  // Reading state
  // =====================================================================

  get currentPlayer() {
    return this.players[this.currentIndex] || null;
  }

  get currentPlayerId() {
    return this.currentPlayer?.playerId ?? null;
  }

  get isComplete() {
    return this.result !== null;
  }

  /** Everything in the middle, including bets on the current street. */
  get pot() {
    return this.players.reduce((sum, p) => sum + p.totalBet, 0);
  }

  getPlayer(playerId) {
    return this.players.find((p) => p.playerId === playerId) || null;
  }

  /** Player ids clockwise starting left of the button (used for odd chips). */
  seatOrderFromButton() {
    const n = this.players.length;
    return Array.from({ length: n }, (_, k) => this.players[(this.buttonIndex + 1 + k) % n].playerId);
  }

  /**
   * What the given player may do right now.
   * minRaiseTo / maxRaiseTo are "raise to" totals for this street (0 when raising isn't allowed).
   */
  getLegalActions(playerId) {
    const none = { canAct: false, actions: [], toCall: 0, minRaiseTo: 0, maxRaiseTo: 0 };
    const p = this.getPlayer(playerId);
    if (!p || this.isComplete || this.currentPlayer !== p) return none;

    const owed = this.currentBet - p.bet;
    const toCall = Math.min(owed, p.stack);
    const actions = [ACTIONS.FOLD];

    if (owed <= 0) actions.push(ACTIONS.CHECK);
    else if (p.stack > owed) actions.push(ACTIONS.CALL);
    // (stack <= owed: calling means going all-in, offered as ALL_IN below)

    // Raising needs: betting open to this player, someone left to respond, and chips beyond a call.
    const othersCanRespond = this.players.some((o) => o !== p && this.canBet(o));
    const maxRaiseTo = p.bet + p.stack;
    const minRaiseTo = this.currentBet + this.minRaise;
    const canRaise = !p.hasActed && othersCanRespond && maxRaiseTo > this.currentBet;

    const fullRaisePossible = canRaise && maxRaiseTo >= minRaiseTo;
    if (fullRaisePossible) actions.push(ACTIONS.RAISE);
    if (canRaise || (owed > 0 && p.stack <= owed)) actions.push(ACTIONS.ALL_IN);

    return {
      canAct: true,
      actions,
      toCall,
      minRaiseTo: fullRaisePossible ? minRaiseTo : 0,
      maxRaiseTo: fullRaisePossible ? maxRaiseTo : 0,
    };
  }

  // =====================================================================
  // Running the hand
  // =====================================================================

  /** Deal hole cards, post blinds and give the turn to the first player. */
  start() {
    if (this.stage !== STAGES.WAITING) throw new Error('Hand already started');
    const n = this.players.length;

    // Two rounds of one card each, starting left of the button.
    for (let round = 0; round < 2; round++) {
      for (let k = 1; k <= n; k++) this.players[(this.buttonIndex + k) % n].holeCards.push(this.draw());
    }

    // Heads-up the button posts the small blind; otherwise it's the next seat.
    const sbIndex = n === 2 ? this.buttonIndex : this.nextIndex(this.buttonIndex);
    const bbIndex = this.nextIndex(sbIndex);
    this.postBlind(this.players[sbIndex], this.smallBlind, 'small blind');
    this.postBlind(this.players[bbIndex], this.bigBlind, 'big blind');

    this.stage = STAGES.PREFLOP;
    this.currentBet = this.bigBlind;
    this.minRaise = this.bigBlind;

    // Pre-flop the player after the big blind starts (the big blind gets the last word).
    this.currentIndex = bbIndex;
    if (!this.moveToNextPlayer()) this.endBettingRound();
  }

  /**
   * Validate and apply one action. Never throws for bad client input.
   * @param {string} action  one of ACTIONS
   * @param {number} [amount] for RAISE: total bet to raise to on this street
   * @returns {{ ok: true } | { ok: false, error: string }}
   */
  applyAction(playerId, action, amount) {
    if (this.isComplete || !BETTING_STAGES.includes(this.stage)) return fail('No betting right now');
    const p = this.currentPlayer;
    if (!p || p.playerId !== playerId) return fail('Not your turn');

    const legal = this.getLegalActions(playerId);
    // Not enough chips to call: the call becomes an all-in automatically.
    if (
      action === ACTIONS.CALL &&
      !legal.actions.includes(ACTIONS.CALL) &&
      legal.actions.includes(ACTIONS.ALL_IN) &&
      p.stack <= this.currentBet - p.bet
    ) {
      action = ACTIONS.ALL_IN;
    }
    if (!legal.actions.includes(action)) return fail(`You can't ${action} right now`);

    const stage = this.stage;
    const before = p.totalBet;
    const isOpeningBet = action === ACTIONS.RAISE && this.currentBet === 0; // shown as BET, not RAISE
    switch (action) {
      case ACTIONS.FOLD:
        p.folded = true;
        break;
      case ACTIONS.CHECK:
        break;
      case ACTIONS.CALL:
        this.putChips(p, legal.toCall);
        break;
      case ACTIONS.RAISE:
        if (!Number.isInteger(amount)) return fail('Raise needs a whole-number amount');
        if (amount < legal.minRaiseTo || amount > legal.maxRaiseTo) {
          return fail(`Raise must be between ${legal.minRaiseTo} and ${legal.maxRaiseTo}`);
        }
        this.raiseTo(p, amount);
        break;
      case ACTIONS.ALL_IN:
        this.raiseTo(p, p.bet + p.stack);
        break;
      default:
        return fail('Unknown action');
    }

    const loggedAction = p.allIn && action !== ACTIONS.FOLD ? ACTIONS.ALL_IN : action;
    p.lastAction = isOpeningBet && loggedAction === ACTIONS.RAISE ? 'bet' : loggedAction;
    p.hasActed = true;
    this.log.push({ stage, playerId, action: loggedAction, amount: p.totalBet - before, total: p.bet });
    this.afterAction();
    return { ok: true };
  }

  /** Turn timer ran out: check if that's free, otherwise fold. */
  actOnTimeout(playerId) {
    const legal = this.getLegalActions(playerId);
    if (!legal.canAct) return fail('Not your turn');
    const stage = this.stage;
    const result = this.applyAction(playerId, legal.actions.includes(ACTIONS.CHECK) ? ACTIONS.CHECK : ACTIONS.FOLD);
    const entry = this.log.findLast((e) => e.playerId === playerId && e.stage === stage);
    if (result.ok && entry) entry.timeout = true;
    return result;
  }

  /** Player left the table mid-hand: fold them now, even if it isn't their turn. */
  foldOutOfTurn(playerId) {
    const p = this.getPlayer(playerId);
    if (!p || p.folded || this.isComplete) return;
    p.folded = true;
    p.lastAction = ACTIONS.FOLD;
    this.log.push({ stage: this.stage, playerId, action: ACTIONS.FOLD, amount: 0, total: p.bet, left: true });

    if (this.activePlayers().length === 1) return this.finishByFold();
    if (p === this.currentPlayer) this.afterAction();
  }

  // =====================================================================
  // Internals
  // =====================================================================

  draw() {
    const card = this.deck.shift();
    if (!card) throw new Error('Deck is empty');
    return card;
  }

  nextIndex(i) {
    return (i + 1) % this.players.length;
  }

  activePlayers() {
    return this.players.filter((p) => !p.folded);
  }

  /** Still in the hand with chips behind. */
  canBet(p) {
    return !p.folded && !p.allIn;
  }

  needsToAct(p) {
    if (!this.canBet(p)) return false;
    if (p.bet < this.currentBet) return true; // must call, raise or fold
    if (p.hasActed) return false; // already matched and acted
    // Hasn't acted yet with nothing to call: only matters if someone could still respond.
    return this.players.some((o) => o !== p && this.canBet(o));
  }

  /** Pass the turn clockwise to the next player who needs to act. False if nobody does. */
  moveToNextPlayer() {
    for (let k = 1; k <= this.players.length; k++) {
      const i = (this.currentIndex + k) % this.players.length;
      if (this.needsToAct(this.players[i])) {
        this.currentIndex = i;
        return true;
      }
    }
    this.currentIndex = -1;
    return false;
  }

  putChips(p, amount) {
    const chips = Math.min(amount, p.stack);
    p.stack -= chips;
    p.bet += chips;
    p.totalBet += chips;
    if (p.stack === 0) p.allIn = true;
  }

  postBlind(p, amount, label) {
    this.putChips(p, amount);
    p.lastAction = label;
    this.log.push({ stage: STAGES.PREFLOP, playerId: p.playerId, action: label, amount: p.totalBet, total: p.bet });
  }

  /**
   * Bring the player's street bet up to `target`.
   * A full raise (at least minRaise) reopens betting for everyone else.
   * A smaller all-in raise only makes others call the difference; it doesn't let
   * players who already acted raise again.
   */
  raiseTo(p, target) {
    const raiseSize = target - this.currentBet;
    this.putChips(p, target - p.bet);
    if (raiseSize <= 0) return; // all-in for a call or less

    if (raiseSize >= this.minRaise) {
      this.minRaise = raiseSize;
      for (const other of this.players) if (other !== p) other.hasActed = false;
    }
    this.currentBet = target;
  }

  afterAction() {
    if (this.activePlayers().length === 1) return this.finishByFold();
    if (!this.moveToNextPlayer()) this.endBettingRound();
  }

  /** The part of the biggest bet nobody could match goes back to its owner. */
  returnUncalledBet() {
    const bets = this.players.map((p) => p.bet).sort((a, b) => b - a);
    const excess = bets[0] - (bets[1] ?? 0);
    if (excess <= 0) return;
    const top = this.players.find((p) => p.bet === bets[0]);
    if (top.folded) return; // the biggest bettor left: their chips stay in the pot as dead money
    top.stack += excess;
    top.bet -= excess;
    top.totalBet -= excess;
    if (top.stack > 0) top.allIn = false;
    this.log.push({ stage: this.stage, playerId: top.playerId, action: 'returned', amount: excess, total: top.bet });
  }

  endBettingRound() {
    this.returnUncalledBet();
    for (const p of this.players) {
      p.bet = 0;
      p.hasActed = false;
    }
    this.currentBet = 0;
    this.minRaise = this.bigBlind;
    this.currentIndex = -1;

    if (this.stage === STAGES.RIVER) return this.showdown();

    this.stage = NEXT_STAGE[this.stage];
    this.draw(); // burn card
    for (let i = 0; i < CARDS_FOR_STAGE[this.stage]; i++) this.board.push(this.draw());

    // Fewer than two players with chips: no more betting, deal the rest of the board.
    if (this.players.filter((p) => this.canBet(p)).length < 2) return this.endBettingRound();

    // After the flop, the first player left of the button starts.
    this.currentIndex = this.buttonIndex;
    if (!this.moveToNextPlayer()) this.endBettingRound();
  }

  finishByFold() {
    this.returnUncalledBet();
    const winner = this.activePlayers()[0];
    const amount = this.pot;
    winner.stack += amount;
    this.currentIndex = -1;
    this.result = {
      winners: [{ playerId: winner.playerId, amount, handName: '', bestCards: [] }],
      pots: [{ amount, winners: [{ playerId: winner.playerId, share: amount }] }],
      shownHands: {}, // nobody has to show cards when everyone else folds
    };
  }

  showdown() {
    this.stage = STAGES.SHOWDOWN;
    this.currentIndex = -1;

    const contenders = this.activePlayers();
    const hands = new Map(contenders.map((p) => [p.playerId, evaluate([...p.holeCards, ...this.board])]));
    const pots = buildPots(this.players.map((p) => ({ playerId: p.playerId, amount: p.totalBet, folded: p.folded })));
    const payouts = distributePots(pots, hands, this.seatOrderFromButton());

    const won = new Map();
    for (const pot of payouts) {
      for (const { playerId, share } of pot.winners) {
        this.getPlayer(playerId).stack += share;
        won.set(playerId, (won.get(playerId) || 0) + share);
      }
    }

    this.result = {
      winners: [...won.entries()]
        .map(([playerId, amount]) => ({
          playerId,
          amount,
          handName: hands.get(playerId)?.name ?? '',
          bestCards: hands.get(playerId)?.cards ?? [],
        }))
        .sort((a, b) => b.amount - a.amount),
      pots: payouts,
      shownHands: Object.fromEntries(contenders.map((p) => [p.playerId, p.holeCards])),
      handNames: Object.fromEntries([...hands.entries()].map(([id, h]) => [id, h.name])),
    };
  }
}

function fail(error) {
  return { ok: false, error };
}

module.exports = HoldemHand;
