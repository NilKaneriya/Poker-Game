const config = require('../config');
const log = require('../utils/logger');
const TurnTimer = require('./TurnTimer');
const { sendTableState, sendTableList, sendStats } = require('../sockets/broadcast');
const gameEvents = require('./gameEvents');
const { GAME_EVENTS } = gameEvents;
const stats = require('../services/StatsService');
const history = require('../services/HandHistoryService');
const { buildPlayerView } = require('./playerView');
const { BotPlayer, LEVELS } = require('../bots/BotPlayer');

const { TABLE_MODES } = require('../engine/constants');
const { AVATAR_COUNT } = require('../services/profileRules');

const BOT_NAMES = ['Ava', 'Max', 'Zoe', 'Rex', 'Mia', 'Kai'];

/**
 * Runs one table over time: deals hands, runs the turn timer, lets bots act,
 * shows results and starts the next hand. The engine (Table/HoldemHand) holds the
 * rules; this class only decides *when* things happen and tells clients.
 *
 * Call update() after anything changes. It broadcasts the new state and
 * schedules whatever comes next.
 */
class TableRunner {
  constructor(io, table) {
    this.io = io;
    this.table = table;
    this.turnTimer = new TurnTimer();
    this.turnKey = null; // identifies the current turn so stale timers/bot moves are ignored
    this.actionCount = 0;
    this.pending = null; // the scheduled "start hand" or "finish hand"
    this.botTimer = null;
    this.bots = new Map(); // playerId -> BotPlayer
    this.botsAdded = 0;
  }

  // ---------- Main loop ----------

  update() {
    const hand = this.table.hand;

    if (hand && !hand.isComplete) {
      this.armTurn();
    } else {
      this.turnTimer.clear();
      this.turnKey = null;
    }

    if (hand && hand.isComplete) {
      this.schedule(config.timing.resultShowMs, () => this.finishHand());
    }

    if (!hand) {
      this.manageBots();
      this.startWhenReady();
      if (this.table.started && this.table.canStartHand()) {
        this.schedule(config.timing.nextHandDelayMs, () => this.startHand());
      }
    }

    this.broadcast();
  }

  /**
   * Waiting room: a friends table opens once every seat is taken, so everyone
   * moves to the table together. A bots table is full of bots, so it opens at once.
   */
  startWhenReady() {
    if (this.table.started || !this.table.isFull) return;
    this.table.started = true;
    log.info(`Table ${this.table.code}: all ${this.table.maxSeats} seats taken, game starting`);
    sendTableList(this.io);
  }

  /** The host starts a friends table before it is full. Returns { ok, error }. */
  startEarly(playerId) {
    const table = this.table;
    if (table.started) return { ok: false, error: 'The game has already started' };
    if (table.hostId !== playerId) return { ok: false, error: 'Only the host can start the game' };
    if (table.eligiblePlayers().length < 2) return { ok: false, error: 'You need at least 2 players to start' };
    table.started = true;
    log.info(`Table ${table.code}: host started the game with ${table.players.length}/${table.maxSeats} players`);
    this.update();
    sendTableList(this.io);
    return { ok: true };
  }

  startHand() {
    if (!this.table.started || !this.table.canStartHand()) return this.update();
    this.table.startHand();
    log.info(`Table ${this.table.code}: hand #${this.table.handNumber} started`);
    this.update();
    sendTableList(this.io);
  }

  finishHand() {
    const result = this.table.finishHand();
    const names = result.winners.map((w) => `${this.table.getSeat(w.playerId)?.name ?? w.playerId} +${w.amount}`);
    log.info(`Table ${this.table.code}: hand #${this.table.handNumber} won by ${names.join(', ')}`);
    this.publishResult(result);
    this.recordStats(result);
    this.recordHistory();
    this.update();
    sendTableList(this.io);
  }

  /** Tell other modules (e.g. chat's hand log) who won. The game itself doesn't care who listens. */
  publishResult(result) {
    gameEvents.emit(GAME_EVENTS.HAND_FINISHED, {
      code: this.table.code,
      handNumber: this.table.handNumber,
      winners: result.winners.map((w) => ({
        playerId: w.playerId,
        name: this.table.getSeat(w.playerId)?.name ?? null, // null = left during the hand
        amount: w.amount,
        handName: w.handName,
      })),
    });
  }

  /** Update the stats of every human dealt into the hand that just ended and send them theirs. */
  recordStats(result) {
    const humans = this.table.lastHand.players.map((p) => p.playerId).filter((id) => !this.bots.has(id));
    // Saving must never hold up the table, so it runs in the background.
    stats
      .recordHand(humans, result)
      .then(() => humans.forEach((playerId) => sendStats(this.io, playerId)))
      .catch((err) => log.error(`Table ${this.table.code}: could not save stats: ${err.message}`));
  }

  /** Save the hand that just ended to the hand history of every human dealt in (in the background). */
  recordHistory() {
    const hand = this.table.lastHand;
    const humans = hand.players.map((p) => p.playerId).filter((id) => !this.bots.has(id));
    history
      .recordHand(this.table, hand, humans)
      .catch((err) => log.error(`Table ${this.table.code}: could not save hand history: ${err.message}`));
  }

  /** A human's action from the socket. Returns the engine's { ok, error }. */
  handleAction(playerId, action, amount) {
    const hand = this.table.hand;
    if (!hand) return { ok: false, error: 'No hand in progress' };
    const result = hand.applyAction(playerId, action, amount);
    if (result.ok) this.afterAction();
    return result;
  }

  // ---------- Turns, timer, bots ----------

  /** Start the countdown for the player whose turn it is (once per turn). */
  armTurn() {
    const hand = this.table.hand;
    const key = `${this.table.handNumber}:${this.actionCount}:${hand.currentPlayerId}`;
    if (key === this.turnKey) return; // same turn, timer already running
    this.turnKey = key;

    this.turnTimer.start(config.turnTimeoutMs, () => this.onTimeout(key));

    const bot = this.bots.get(hand.currentPlayerId);
    if (bot) {
      clearTimeout(this.botTimer);
      const think = config.timing.botThinkMs * (1 + Math.random()); // feel less robotic
      this.botTimer = setTimeout(() => this.botAct(key, bot), think);
    }
  }

  /** Time ran out: check if free, otherwise fold. Disconnected players end up here too. */
  onTimeout(key) {
    if (key !== this.turnKey) return;
    const playerId = this.table.hand.currentPlayerId;
    this.table.hand.actOnTimeout(playerId);
    log.info(`Table ${this.table.code}: ${this.table.getSeat(playerId)?.name} timed out`);
    this.afterAction();
  }

  botAct(key, bot) {
    if (key !== this.turnKey) return;
    // The bot sees exactly what a human in its seat would see.
    const view = buildPlayerView(this.table, bot.playerId);
    const choice = bot.decide(view, view.hand.legal);
    const result = this.table.hand.applyAction(bot.playerId, choice.action, choice.amount);
    if (!result.ok) this.table.hand.actOnTimeout(bot.playerId); // should not happen; never stall the table
    this.afterAction();
  }

  afterAction() {
    this.actionCount++;
    this.table.syncChips();
    this.update();
  }

  // ---------- Bots ----------

  /**
   * Bots tables: humans waiting for a seat sit down first (a bot stands up for each),
   * then every empty seat gets a bot. Friends tables never have bots.
   * Only runs between hands, so nobody is removed from a hand in progress.
   */
  manageBots() {
    if (this.table.mode !== TABLE_MODES.BOTS) return;

    this.seatWaitingPlayers();

    const humans = this.table.players.filter((p) => !p.isBot);
    if (humans.length > 0) {
      while (!this.table.isFull) {
        if (!this.addBot()) break;
      }
    }
    // Bots never get stuck spectating: give a broke bot a fresh stack.
    for (const id of this.bots.keys()) {
      if (this.table.getSeat(id)?.chips === 0) this.table.rebuy(id);
    }
  }

  /** Give each waiting human a seat, freeing a bot's seat when the table is full. */
  seatWaitingPlayers() {
    const table = this.table;
    while (table.waiting.length > 0) {
      if (table.isFull && !this.removeBot()) break; // only humans left; they keep waiting
      const { playerId, name, avatarId, connected } = table.waiting.shift();
      const seat = table.addPlayer({ playerId, name, avatarId });
      table.getSeat(playerId).connected = connected;
      log.info(`Table ${table.code}: ${name} took seat ${seat} after waiting for the hand to end`);
      gameEvents.emit(GAME_EVENTS.PLAYER_SEATED, { code: table.code, playerId, name });
    }
  }

  /** Stand up one bot (the one with the fewest chips, so the least play is disturbed). */
  removeBot() {
    const bot = this.table.players.filter((p) => p.isBot).sort((a, b) => a.chips - b.chips)[0];
    if (!bot) return false;
    this.table.removePlayer(bot.playerId);
    this.bots.delete(bot.playerId);
    log.info(`Table ${this.table.code}: bot ${bot.name} left to make room for a player`);
    gameEvents.emit(GAME_EVENTS.BOT_REPLACED, { code: this.table.code, name: bot.name });
    return true;
  }

  addBot() {
    const index = this.botsAdded++; // count every bot ever added, so ids are never reused
    // Vary names and levels per table, so different personalities show up.
    const offset = this.table.code.charCodeAt(0) + this.table.handNumber;
    const level = LEVELS[(offset + index) % LEVELS.length];
    const name = `${BOT_NAMES[(offset + index) % BOT_NAMES.length]} (${level})`;
    const bot = new BotPlayer({ playerId: `bot-${this.table.code}-${Date.now()}-${index}`, name, level });
    const avatarId = (offset + index) % AVATAR_COUNT;
    if (this.table.addPlayer({ playerId: bot.playerId, name, avatarId, isBot: true }) === -1) return false;
    this.bots.set(bot.playerId, bot);
    log.info(`Table ${this.table.code}: bot ${name} sat down`);
    return true;
  }

  // ---------- Helpers ----------

  schedule(ms, fn) {
    if (this.pending) return; // something is already planned
    this.pending = setTimeout(() => {
      this.pending = null;
      fn();
    }, ms);
  }

  broadcast() {
    sendTableState(this.io, this.table, {
      turnTimeLeftMs: this.turnTimer.remainingMs,
      turnTimeTotalMs: config.turnTimeoutMs,
    });
  }

  destroy() {
    this.turnTimer.clear();
    clearTimeout(this.pending);
    clearTimeout(this.botTimer);
  }
}

// One runner per table, created on first use.
const runners = new Map();

function runnerFor(io, table) {
  if (!runners.has(table.code)) runners.set(table.code, new TableRunner(io, table));
  return runners.get(table.code);
}

function removeRunner(code) {
  runners.get(code)?.destroy();
  runners.delete(code);
}

module.exports = { runnerFor, removeRunner };
