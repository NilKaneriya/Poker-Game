const tables = require('../services/TableManager');
const { runnerFor } = require('./TableRunner');

/**
 * A player saved a new name or avatar. Show it right away everywhere they already are:
 * on their open sockets (used for chat and the next table they join) and on the seat
 * at their current table.
 */
function syncProfile(io, { playerId, displayName, avatarId }) {
  for (const socket of io.of('/').sockets.values()) {
    if (socket.data.playerId !== playerId) continue;
    socket.data.name = displayName;
    socket.data.avatarId = avatarId;
  }

  const table = tables.findByPlayer(playerId);
  if (table && table.updateMember(playerId, { name: displayName, avatarId })) {
    runnerFor(io, table).update(); // everyone at the table sees the change
  }
}

module.exports = { syncProfile };
