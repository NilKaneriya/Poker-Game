/**
 * Remembers each player's chips between tables, so leaving and re-joining does not
 * refill the stack. Out of chips (0) means a fresh starting stack, same as "Restart".
 * Kept in memory only: a server restart gives everyone a fresh stack.
 */
const stacks = new Map(); // playerId -> chips

function save(playerId, chips) {
  stacks.set(playerId, chips);
}

function stackFor(playerId, startingChips) {
  const chips = stacks.get(playerId);
  return chips > 0 ? chips : startingChips;
}

function clear() {
  stacks.clear();
}

module.exports = { save, stackFor, clear };
