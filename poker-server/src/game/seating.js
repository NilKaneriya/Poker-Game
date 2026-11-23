const tables = require('../services/TableManager');
const log = require('../utils/logger');
const wallet = require('../services/WalletService');
const { sendTableList, sendWallet } = require('../sockets/broadcast');
const gameEvents = require('./gameEvents');
const { GAME_EVENTS } = gameEvents;
const { runnerFor, removeRunner } = require('./TableRunner');

/**
 * Move the chips a player walked away with back into their wallet. Runs in the background
 * (the wallet is saved asynchronously) but is queued per player, so a quick re-join always
 * sees the chips. A failed save is retried; if it still fails the amount is logged so the
 * chips can be restored by hand instead of vanishing silently.
 */
async function payBack(io, playerId, chips, code) {
  for (let attempt = 1; attempt <= 3; attempt++) {
    try {
      const result = await wallet.cashOut(playerId, chips, `table ${code}`);
      if (result.ok) return sendWallet(io, playerId);
    } catch (err) {
      log.error(`Wallet cash-out failed for ${playerId} (${chips} chips, attempt ${attempt}): ${err.message}`);
    }
  }
  log.error(`LOST CHIPS: could not return ${chips} chips to ${playerId} from table ${code}`);
}

/**
 * Take a player off their table: folds them if a hand is running, passes on the host
 * role, and closes the table when no humans are left. Used when a player leaves and
 * when a disconnected player doesn't come back in time.
 * @returns {Table|null} the table they left, or null if they weren't seated
 */
function removeFromTable(io, playerId, name) {
  const table = tables.findByPlayer(playerId);
  if (!table) return null;

  const chips = table.removePlayer(playerId);
  if (chips !== null) payBack(io, playerId, chips, table.code);
  log.info(`${name} left table ${table.code}`);

  if (table.humanCount === 0) {
    // only bots left (nobody seated or waiting)
    removeRunner(table.code);
    tables.remove(table.code);
    gameEvents.emit(GAME_EVENTS.TABLE_CLOSED, { code: table.code });
    log.info(`Table ${table.code} closed`);
  } else {
    gameEvents.emit(GAME_EVENTS.PLAYER_LEFT, { code: table.code, playerId, name });
    runnerFor(io, table).update();
  }
  sendTableList(io);
  return table;
}

module.exports = { removeFromTable };
