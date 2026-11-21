const EVENTS = require('../events');
const config = require('../../config');
const { TABLE_MODES } = require('../../engine/constants');
const tables = require('../../services/TableManager');
const log = require('../../utils/logger');
const { tableRoom, sendTableList, sendStats } = require('../broadcast');
const gameEvents = require('../gameEvents');
const { GAME_EVENTS } = gameEvents;
const { runnerFor } = require('../TableRunner');
const { removeFromTable } = require('../seating');

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

  function seat(table) {
    const { name } = me();
    const seatIndex = table.addPlayer(me());
    if (seatIndex === -1) return fail('Table is full');
    socket.join(tableRoom(table.code));
    log.info(`${name} sat at ${table.code} seat ${seatIndex}`);
    gameEvents.emit(GAME_EVENTS.PLAYER_JOINED, { code: table.code, playerId, name });
    runnerFor(io, table).update(); // adds/removes bots and starts a hand when possible
    sendTableList(io);
  }

  socket.on(EVENTS.LIST_TABLES, () => socket.emit(EVENTS.TABLE_LIST, tables.list()));
  socket.on(EVENTS.GET_STATS, () => sendStats(io, playerId));

  socket.on(EVENTS.CREATE_TABLE, (payload) => {
    const { tableName, mode, maxSeats } = payload || {};
    leaveCurrentTable();
    const table = tables.create({
      name: cleanTableName(tableName),
      mode: cleanMode(mode),
      maxSeats: cleanSeatCount(maxSeats),
      hostId: playerId,
    });
    log.info(`${socket.data.name} created ${table.mode} table ${table.code} (${table.maxSeats} seats)`);
    seat(table);
  });

  socket.on(EVENTS.JOIN_TABLE, (payload) => {
    const { code } = payload || {};
    const table = tables.get(code);
    if (!table) return fail('No table with that code');

    // Already seated here (e.g. second tab or reconnect): just resync.
    if (table.findSeat(playerId) !== -1) {
      socket.join(tableRoom(table.code));
      return runnerFor(io, table).update();
    }
    if (table.mode === TABLE_MODES.BOTS) return joinBotsTable(table);
    if (table.isFull) return fail('Table is full');

    leaveCurrentTable();
    seat(table);
  });

  /**
   * A friend joins a game against bots by code. Mid-hand they watch from the waiting
   * list; when the hand ends a bot stands up and they take its seat. Between hands
   * the swap happens straight away (the runner does it on update).
   */
  function joinBotsTable(table) {
    if (table.isWaiting(playerId)) {
      socket.join(tableRoom(table.code));
      return runnerFor(io, table).update();
    }
    if (table.humanCount >= table.maxSeats) return fail('Table is full');

    leaveCurrentTable();
    const { name } = me();
    table.addWaiting(me());
    socket.join(tableRoom(table.code));
    log.info(`${name} joined bots table ${table.code}; seated when the current hand ends`);
    gameEvents.emit(GAME_EVENTS.PLAYER_WAITING, { code: table.code, playerId, name });
    runnerFor(io, table).update();
  }

  socket.on(EVENTS.START_GAME, () => {
    const table = tables.findByPlayer(playerId);
    if (!table) return fail('You are not at a table');
    const result = runnerFor(io, table).startEarly(playerId);
    if (!result.ok) fail(result.error);
  });

  socket.on(EVENTS.LEAVE_TABLE, leaveCurrentTable);
};
