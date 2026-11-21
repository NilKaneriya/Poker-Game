const EVENTS = require('./events');
const tables = require('../services/TableManager');
const stats = require('../services/StatsService');
const { buildPlayerView } = require('./playerView');
const log = require('../utils/logger');

const playerRoom = (playerId) => `player:${playerId}`;
const tableRoom = (code) => `table:${code}`;

/** Send every seated human, and everyone waiting for a seat, their own view of the table. */
function sendTableState(io, table, timing) {
  const viewers = [...table.players.filter((p) => !p.isBot), ...table.waiting];
  for (const p of viewers) {
    io.to(playerRoom(p.playerId)).emit(EVENTS.TABLE_STATE, buildPlayerView(table, p.playerId, timing));
  }
}

function sendTableList(io) {
  io.emit(EVENTS.TABLE_LIST, tables.list());
}

/** A player's own statistics (only ever sent to that player). */
async function sendStats(io, playerId) {
  try {
    io.to(playerRoom(playerId)).emit(EVENTS.STATS, await stats.get(playerId));
  } catch (err) {
    log.error(`Could not load stats for ${playerId}: ${err.message}`);
  }
}

module.exports = { playerRoom, tableRoom, sendTableState, sendTableList, sendStats };
