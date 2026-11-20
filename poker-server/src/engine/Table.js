const HoldemHand = require('./HoldemHand');
const { TABLE_MODES } = require('./constants');

/**
 * A poker table: seats, chip stacks, the dealer button and the current hand.
 * Knows nothing about sockets or timers; the network layer drives it.
 *
 * Seat chips always show what a player has *behind* (not in the pot). While a
 * hand runs they are synced from the hand after every action.
 */
class Table {
  constructor({ code, name, maxSeats, smallBlind, bigBlind, startingChips, mode = TABLE_MODES.FRIENDS, hostId = null }) {
    this.code = code;
    this.name = name;
    this.mode = mode;
    this.hostId = hostId;  // who created the table; may start a friends game early
    this.started = false;  // false = waiting room (friends tables until all seats are taken)
    this.maxSeats = maxSeats;
    this.smallBlind = smallBlind;
    this.bigBlind = bigBlind;
    this.startingChips = startingChips;
    this.seats = new Array(maxSeats).fill(null); // { playerId, name, chips, isBot, connected, sittingOut }
    this.buttonSeat = -1;
    this.hand = null;     // the running hand
    this.lastHand = null; // the previous hand, kept for showing its result
    this.handNumber = 0;
  }

  // ---------- Seats ----------

  get players() {
    return this.seats.filter(Boolean);
  }

  get isFull() {
    return this.players.length >= this.maxSeats;
  }

  findSeat(playerId) {
    return this.seats.findIndex((s) => s && s.playerId === playerId);
  }

  getSeat(playerId) {
    return this.seats[this.findSeat(playerId)] || null;
  }

  /** Seat a player in the first free seat. Returns the seat index, or -1 if full. */
  addPlayer({ playerId, name, isBot = false }) {
    const existing = this.findSeat(playerId);
    if (existing !== -1) return existing;
    const seat = this.seats.indexOf(null);
    if (seat === -1) return -1;
    this.seats[seat] = { playerId, name, chips: this.startingChips, isBot, connected: true, sittingOut: false };
    return seat;
  }

  /** Leave the table. If a hand is running they fold; chips already bet stay in the pot. */
  removePlayer(playerId) {
    const seat = this.findSeat(playerId);
    if (seat === -1) return;
    if (this.hand && !this.hand.isComplete) {
      this.hand.foldOutOfTurn(playerId);
      this.syncChips();
    }
    this.seats[seat] = null;
    // The host left: hand the role to the next human still seated.
    if (this.hostId === playerId) this.hostId = this.players.find((p) => !p.isBot)?.playerId ?? null;
  }

  /** Players with chips who can be dealt into the next hand. Busted players watch. */
  eligiblePlayers() {
    return this.players.filter((p) => p.chips > 0 && !p.sittingOut);
  }

  isDealtIn(playerId) {
    return Boolean(this.hand && this.hand.getPlayer(playerId));
  }

  // ---------- Hands ----------

  canStartHand() {
    return !this.hand && this.eligiblePlayers().length >= 2;
  }

  /** Move the button, deal a new hand and post blinds. */
  startHand(deck) {
    if (!this.canStartHand()) throw new Error('Not enough players to start a hand');

    this.buttonSeat = this.nextEligibleSeat(this.buttonSeat);
    const dealtSeats = this.seats
      .map((s, i) => (s && s.chips > 0 && !s.sittingOut ? i : -1))
      .filter((i) => i !== -1); // clockwise by seat number

    this.hand = new HoldemHand({
      players: dealtSeats.map((i) => ({ playerId: this.seats[i].playerId, stack: this.seats[i].chips })),
      buttonIndex: dealtSeats.indexOf(this.buttonSeat),
      smallBlind: this.smallBlind,
      bigBlind: this.bigBlind,
      deck,
    });
    this.handNumber++;
    this.hand.start();
    this.syncChips();
    return this.hand;
  }

  /** Copy stacks from the hand back to the seats (after every action). */
  syncChips() {
    if (!this.hand) return;
    for (const p of this.hand.players) {
      const seat = this.getSeat(p.playerId);
      if (seat) seat.chips = p.stack;
    }
  }

  /** Close a finished hand. Returns its result. */
  finishHand() {
    if (!this.hand || !this.hand.isComplete) throw new Error('No finished hand to close');
    this.syncChips();
    this.lastHand = this.hand;
    this.hand = null;
    return this.lastHand.result;
  }

  /** Out of chips: start again with the starting stack (only between hands). */
  rebuy(playerId) {
    const seat = this.getSeat(playerId);
    if (!seat) return { ok: false, error: 'You are not at this table' };
    if (seat.chips > 0) return { ok: false, error: 'You still have chips' };
    if (this.isDealtIn(playerId) && !this.hand.isComplete) return { ok: false, error: 'Wait for the hand to finish' };
    seat.chips = this.startingChips;
    seat.sittingOut = false;
    return { ok: true };
  }

  /** Next seat clockwise after `fromSeat` holding a player who can be dealt in. */
  nextEligibleSeat(fromSeat) {
    for (let k = 1; k <= this.maxSeats; k++) {
      const i = (fromSeat + k + this.maxSeats) % this.maxSeats;
      const s = this.seats[i];
      if (s && s.chips > 0 && !s.sittingOut) return i;
    }
    return -1;
  }

  /** Public snapshot, safe to send to everyone (no hole cards). */
  toPublic() {
    return {
      code: this.code,
      name: this.name,
      mode: this.mode,
      started: this.started,
      hostId: this.hostId,
      maxSeats: this.maxSeats,
      smallBlind: this.smallBlind,
      bigBlind: this.bigBlind,
      buttonSeat: this.buttonSeat,
      handNumber: this.handNumber,
      seats: this.seats.map((s, i) =>
        s ? { seat: i, playerId: s.playerId, name: s.name, chips: s.chips, isBot: s.isBot, connected: s.connected } : null
      ),
    };
  }
}

module.exports = Table;
