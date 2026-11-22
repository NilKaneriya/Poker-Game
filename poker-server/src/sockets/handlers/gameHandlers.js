const EVENTS = require('../events');
const { ACTIONS } = require('../../engine/constants');
const tables = require('../../services/TableManager');
const { runnerFor } = require('../../game/TableRunner');

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

  socket.on(EVENTS.REBUY, () => {
    const table = tables.findByPlayer(playerId);
    if (!table) return reject('You are not at a table');
    const result = table.rebuy(playerId);
    if (!result.ok) return reject(result.error);
    runnerFor(io, table).update(); // may start a hand now that we have chips again
  });
};
