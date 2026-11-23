const EVENTS = require('../events');
const config = require('../../config');
const { TABLE_MODES } = require('../../engine/constants');
const tables = require('../../services/TableManager');
const log = require('../../utils/logger');
const wallet = require('../../services/WalletService');
const { tryLock } = require('../playerLocks');
const handHistory = require('../../services/HandHistoryService');
const { tableRoom, sendTableList, sendStats, sendWallet } = require('../broadcast');
const gameEvents = require('../../game/gameEvents');
const { GAME_EVENTS } = gameEvents;
const { runnerFor } = require('../../game/TableRunner');
const { removeFromTable } = require('../../game/seating');

const MAX_TABLE_NAME_LENGTH = 24;
const VALID_MODES = new Set(Object.values(TABLE_MODES));

function cleanTableName(value) {
  const name = typeof value === 'string' ? value.trim().slice(0, MAX_TABLE_NAME_LENGTH) : '';
  return name || null;
}

const cleanMode = (value) => (VALID_MODES.has(value) ? value : TABLE_MODES.FRIENDS);

function cleanSeatCount(value) {
  const { minSeats, maxSeats } = config.table;
  return Number.isInteger(value) && value >= minSeats && value <= maxSeats ? value : maxSeats;
}

module.exports = (io, socket) => {
  const { playerId } = socket.data; // set from the login token, not from client input
  // Name and avatar can change while connected (profile edits), so they are read when used.
  const me = () => ({ playerId, name: socket.data.name, avatarId: socket.data.avatarId });
  const fail = (message) => socket.emit(EVENTS.ERROR, { message });

  function leaveCurrentTable() {
    const table = removeFromTable(io, playerId, socket.data.name); // folds them if a hand is running
    if (table) socket.leave(tableRoom(table.code));
  }

  /**
   * Pay the buy-in from the wallet, then sit (or wait for a seat on a bots table).
   * The wallet is charged asynchronously, so everything is checked again afterwards and the
   * buy-in is refunded if the seat is no longer possible. Returns true when seated.
   */
  async function takeSeat(table, { waiting = false } = {}) {
    const { name } = me();
    const paid = await wallet.buyIn(playerId, table.startingChips, `table ${table.code}`);
    if (!paid.ok) {
      fail(paid.error);
      return false;
    }
    const refund = async (message) => {
      await wallet.cashOut(playerId, paid.amount, 'refund', paid.amount);
      sendWallet(io, playerId);
      if (message) fail(message);
      return false;
    };

    if (!socket.connected) return refund(null); // left while we were waiting for the wallet
    if (tables.get(table.code) !== table) return refund('That table was closed');
    if (tables.findByPlayer(playerId)) return refund('You are already at a table');

    if (waiting) {
      table.addWaiting({ ...me(), chips: paid.amount });
    } else {
      const seatIndex = table.addPlayer({ ...me(), chips: paid.amount });
      if (seatIndex === -1) return refund('Table is full');
      log.info(`${name} sat at ${table.code} seat ${seatIndex} with ${paid.amount} chips`);
    }
    socket.join(tableRoom(table.code));
    sendWallet(io, playerId);
    return true;
  }

  function announceSeat(table) {
    gameEvents.emit(GAME_EVENTS.PLAYER_JOINED, { code: table.code, playerId, name: socket.data.name });
    runnerFor(io, table).update(); // adds/removes bots and starts a hand when possible
    sendTableList(io);
  }

  socket.on(EVENTS.LIST_TABLES, () => socket.emit(EVENTS.TABLE_LIST, tables.list()));
  socket.on(EVENTS.GET_STATS, () => sendStats(io, playerId));
  socket.on(EVENTS.GET_HAND_HISTORY, async () => {
    try {
      socket.emit(EVENTS.HAND_HISTORY, { hands: await handHistory.list(playerId) });
    } catch (err) {
      log.error(`Could not load hand history for ${playerId}: ${err.message}`);
      socket.emit(EVENTS.ERROR, { message: 'Could not load your hand history' });
    }
  });

  socket.on(EVENTS.CREATE_TABLE, async (payload) => {
    const { tableName, mode, maxSeats } = payload || {};
    const release = tryLock(playerId); // one sit-down at a time: two clicks must not charge twice
    if (!release) return fail('One moment, you are already joining a table');
    try {
      leaveCurrentTable();
      const table = tables.create({
        name: cleanTableName(tableName),
        mode: cleanMode(mode),
        maxSeats: cleanSeatCount(maxSeats),
        hostId: playerId,
      });
      log.info(`${socket.data.name} created ${table.mode} table ${table.code} (${table.maxSeats} seats)`);
      if (await takeSeat(table)) announceSeat(table);
      else tables.remove(table.code); // nobody sat: don't leave an empty table behind
    } finally {
      release();
    }
  });

  socket.on(EVENTS.JOIN_TABLE, async (payload) => {
    const { code } = payload || {};
    const table = tables.get(code);
    if (!table) return fail('No table with that code');

    // Already seated here (e.g. second tab or reconnect): just resync.
    if (table.findSeat(playerId) !== -1 || (table.mode === TABLE_MODES.BOTS && table.isWaiting(playerId))) {
      socket.join(tableRoom(table.code));
      return runnerFor(io, table).update();
    }

    // Bots game joined by code: mid-hand a friend watches from the waiting list and takes a bot's seat later.
    const isBots = table.mode === TABLE_MODES.BOTS;
    if (isBots ? table.humanCount >= table.maxSeats : table.isFull) return fail('Table is full');

    const release = tryLock(playerId);
    if (!release) return fail('One moment, you are already joining a table');
    try {
      leaveCurrentTable();
      if (!(await takeSeat(table, { waiting: isBots }))) return;
      if (isBots) {
        log.info(`${socket.data.name} joined bots table ${table.code}; seated when the current hand ends`);
        gameEvents.emit(GAME_EVENTS.PLAYER_WAITING, { code: table.code, playerId, name: socket.data.name });
        runnerFor(io, table).update();
      } else {
        announceSeat(table);
      }
    } finally {
      release();
    }
  });

  socket.on(EVENTS.START_GAME, () => {
    const table = tables.findByPlayer(playerId);
    if (!table) return fail('You are not at a table');
    const result = runnerFor(io, table).startEarly(playerId);
    if (!result.ok) fail(result.error);
  });

  socket.on(EVENTS.LEAVE_TABLE, leaveCurrentTable);
};
