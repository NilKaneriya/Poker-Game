const tables = require('../services/TableManager');
const log = require('../utils/logger');
const { sendTableList } = require('./broadcast');
const { runnerFor, removeRunner } = require('./TableRunner');

/**
 * Take a player off their table: folds them if a hand is running, passes on the host
 * role, and closes the table when no humans are left. Used when a player leaves and
 * when a disconnected player doesn't come back in time.
 * @returns {Table|null} the table they left, or null if they weren't seated
 */
function removeFromTable(io, playerId, name) {
  const table = tables.findByPlayer(playerId);
  if (!table) return null;

  table.removePlayer(playerId);
  log.info(`${name} left table ${table.code}`);

  if (table.humanCount === 0) { // only bots left (nobody seated or waiting)
    removeRunner(table.code);
    tables.remove(table.code);
    log.info(`Table ${table.code} closed`);
  } else {
    runnerFor(io, table).update();
  }
  sendTableList(io);
  return table;
}

module.exports = { removeFromTable };
