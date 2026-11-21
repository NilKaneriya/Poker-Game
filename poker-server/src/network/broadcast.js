const EVENTS = require('./events');
const tables = require('../services/TableManager');
const stats = require('../services/StatsService');
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

/** A player's own statistics (only ever sent to that player). */
function sendStats(io, playerId) {
  io.to(playerRoom(playerId)).emit(EVENTS.STATS, stats.get(playerId));
}

module.exports = { playerRoom, tableRoom, sendTableState, sendTableList, sendStats };
