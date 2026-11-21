/**
 * Turns game events into chat lines. Pure functions: event payload in, text out
 * (null = nothing to say). Keeps all chat wording in one place.
 */
const SYSTEM_TEXT = {
  playerJoined: ({ name }) => `${name} joined the table`,
  playerWaiting: ({ name }) => `${name} is watching and will sit down after this hand`,
  playerSeated: ({ name }) => `${name} took a seat`,
  playerLeft: ({ name }) => `${name} left the table`,
  botReplaced: ({ name }) => `${name} left to make room`,
};

/** "Ava won 300 with Flush", one line per winner (a short hand log). */
function winnerLines({ winners }) {
  return winners.map((w) => `${w.name ?? 'A player who left'} won ${w.amount}${w.handName ? ` with ${w.handName}` : ''}`);
}

module.exports = { SYSTEM_TEXT, winnerLines };
