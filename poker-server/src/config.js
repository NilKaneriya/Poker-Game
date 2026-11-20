require('dotenv').config({ quiet: true });
const path = require('path');

const num = (value, fallback) => (value !== undefined && value !== '' ? Number(value) : fallback);
const DAY_MS = 24 * 60 * 60 * 1000;

module.exports = {
  port: num(process.env.PORT, 3000),
  corsOrigin: process.env.CORS_ORIGIN
    ? process.env.CORS_ORIGIN.split(',').map((s) => s.trim())
    : '*',

  table: {
    maxSeats: 6,
    startingChips: 1000,
    smallBlind: num(process.env.SMALL_BLIND, 10),
    bigBlind: num(process.env.BIG_BLIND, 20),
  },

  turnTimeoutMs: num(process.env.TURN_TIMEOUT_MS, 30000),
  timing: {
    nextHandDelayMs: num(process.env.NEXT_HAND_DELAY_MS, 2500), // pause before dealing the next hand
    resultShowMs: num(process.env.RESULT_SHOW_MS, 5000), //       how long the winner screen stays
    botThinkMs: num(process.env.BOT_THINK_MS, 900), //            bots wait this long (+ random) before acting
  },
  // How long a disconnected player's seat is kept before they are removed.
  reconnectGraceMs: num(process.env.RECONNECT_GRACE_MS, 120000),

  auth: {
    accountsFile: process.env.ACCOUNTS_FILE || path.join(__dirname, '..', 'data', 'accounts.json'),
    sessionTokenTtlMs: 12 * 60 * 60 * 1000, // without "remember me"
    rememberTokenTtlMs: 30 * DAY_MS, //        with "remember me"
  },
};
