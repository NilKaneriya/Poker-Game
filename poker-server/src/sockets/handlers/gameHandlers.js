const EVENTS = require('../events');
const { ACTIONS } = require('../../engine/constants');
const tables = require('../../services/TableManager');
const { runnerFor } = require('../../game/TableRunner');
const wallet = require('../../services/WalletService');
const { sendWallet } = require('../broadcast');
const { tryLock } = require('../playerLocks');

const VALID_ACTIONS = new Set(Object.values(ACTIONS));

module.exports = (io, socket) => {
  const { playerId } = socket.data;
  const reject = (reason) => socket.emit(EVENTS.ACTION_REJECTED, { reason });

  socket.on(EVENTS.PLAYER_ACTION, (payload) => {
    const { action, amount } = payload || {};
    if (!VALID_ACTIONS.has(action)) return reject('Unknown action');
    if (amount !== undefined && (!Number.isInteger(amount) || amount < 0)) return reject('Invalid amount');

    const table = tables.findByPlayer(playerId);
    if (!table) return reject('You are not at a table');

    // Turn order, bet sizes and every other rule are checked inside the engine.
    const result = runnerFor(io, table).handleAction(playerId, action, amount);
    if (!result.ok) reject(result.error);
  });

  // Out of chips at the table: buy back in from the wallet (the chips are not free).
  socket.on(EVENTS.REBUY, async () => {
    const table = tables.findByPlayer(playerId);
    if (!table) return reject('You are not at a table');
    if (!table.canRebuy(playerId).ok) return reject(table.canRebuy(playerId).error);

    const release = tryLock(playerId);
    if (!release) return reject('One moment');
    try {
      const paid = await wallet.buyIn(playerId, table.startingChips, `rebuy ${table.code}`);
      if (!paid.ok) return reject(paid.error);

      // Things may have changed while the wallet was busy: check again, refund if no longer possible.
      const again =
        tables.findByPlayer(playerId) === table ? table.canRebuy(playerId) : { ok: false, error: 'You are not at a table' };
      if (!again.ok || !socket.connected) {
        await wallet.cashOut(playerId, paid.amount, 'refund', paid.amount);
        sendWallet(io, playerId);
        return reject(again.error || 'Could not buy in');
      }
      table.rebuy(playerId, paid.amount);
      sendWallet(io, playerId);
      runnerFor(io, table).update(); // may start a hand now that we have chips again
    } finally {
      release();
    }
  });
};
