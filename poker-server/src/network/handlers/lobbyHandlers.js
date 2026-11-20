const EVENTS = require('../events');
const tables = require('../../services/TableManager');
const log = require('../../utils/logger');
const { tableRoom, sendTableList } = require('../broadcast');
const { runnerFor, removeRunner } = require('../TableRunner');

const MAX_TABLE_NAME_LENGTH = 24;

function cleanTableName(value) {
  const name = typeof value === 'string' ? value.trim().slice(0, MAX_TABLE_NAME_LENGTH) : '';
  return name || null;
}

module.exports = (io, socket) => {
  const { playerId, name } = socket.data; // set from the login token, not from client input
  const fail = (message) => socket.emit(EVENTS.ERROR, { message });

  function leaveCurrentTable() {
    const table = tables.findByPlayer(playerId);
    if (!table) return;
    table.removePlayer(playerId); // folds them if a hand is running
    socket.leave(tableRoom(table.code));
    log.info(`${name} left table ${table.code}`);

    if (table.players.every((p) => p.isBot)) {
      removeRunner(table.code);
      tables.remove(table.code);
      log.info(`Table ${table.code} closed`);
    } else {
      runnerFor(io, table).update();
    }
    sendTableList(io);
  }

  function seat(table) {
    const seatIndex = table.addPlayer({ playerId, name });
    if (seatIndex === -1) return fail('Table is full');
    socket.join(tableRoom(table.code));
    log.info(`${name} sat at ${table.code} seat ${seatIndex}`);
    runnerFor(io, table).update(); // adds/removes bots and starts a hand when possible
    sendTableList(io);
  }

  socket.on(EVENTS.LIST_TABLES, () => socket.emit(EVENTS.TABLE_LIST, tables.list()));

  socket.on(EVENTS.CREATE_TABLE, (payload) => {
    const { tableName } = payload || {};
    leaveCurrentTable();
    const table = tables.create(cleanTableName(tableName));
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
    if (table.isFull) return fail('Table is full');

    leaveCurrentTable();
    seat(table);
  });

  socket.on(EVENTS.LEAVE_TABLE, leaveCurrentTable);
};
