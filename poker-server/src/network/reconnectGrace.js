const config = require('../config');
const tables = require('../services/TableManager');
const log = require('../utils/logger');
const { removeFromTable } = require('./seating');

/**
 * A disconnected player keeps their seat and chips for config.reconnectGraceMs.
 * While away, their turns time out like anyone's (check if free, otherwise fold).
 * If they are not back in time, the seat is freed so the table isn't held up forever.
 */
const timers = new Map(); // playerId -> timeout

function startGrace(io, playerId, name) {
  cancelGrace(playerId);
  timers.set(playerId, setTimeout(() => {
    timers.delete(playerId);
    const table = tables.findByPlayer(playerId);
    if (!table || table.getSeat(playerId)?.connected) return; // already back
    log.info(`${name} did not reconnect within ${config.reconnectGraceMs / 1000}s; freeing their seat`);
    removeFromTable(io, playerId, name);
  }, config.reconnectGraceMs));
}

function cancelGrace(playerId) {
  clearTimeout(timers.get(playerId));
  timers.delete(playerId);
}

module.exports = { startGrace, cancelGrace };
