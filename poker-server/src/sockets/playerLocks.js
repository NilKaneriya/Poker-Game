/**
 * A tiny per-player lock for actions that move chips between the wallet and a table
 * (sit down, create a table, rebuy). Those do a slow wallet save in the middle, so without
 * a lock two quick clicks could both pass the checks and charge twice.
 *
 * tryLock(id) returns a release function, or null when the player is already busy.
 */
const locked = new Set();

function tryLock(playerId) {
  if (locked.has(playerId)) return null;
  locked.add(playerId);
  return () => locked.delete(playerId);
}

module.exports = { tryLock };
