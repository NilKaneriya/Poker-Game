const EVENTS = require('./events');
const tables = require('../services/TableManager');
const { buildPlayerView } = require('./playerView');

const playerRoom = (playerId) => `player:${playerId}`;
const tableRoom = (code) => `table:${code}`;

/** Send every seated human their own view of the table. */
function sendTableState(io, table, timing) {
  for (const p of table.players) {
    if (!p.isBot) io.to(playerRoom(p.playerId)).emit(EVENTS.TABLE_STATE, buildPlayerView(table, p.playerId, timing));
  }
}

function sendTableList(io) {
  io.emit(EVENTS.TABLE_LIST, tables.list());
}

module.exports = { playerRoom, tableRoom, sendTableState, sendTableList };
