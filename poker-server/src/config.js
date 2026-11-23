require('dotenv').config({ quiet: true });
const path = require('path');

const num = (value, fallback) => (value !== undefined && value !== '' ? Number(value) : fallback);
const DAY_MS = 24 * 60 * 60 * 1000;

// "*" must stay the plain string "*": the cors library treats an array as a list of exact
// origins, so ["*"] never matches and browsers (WebGL) get no Access-Control-Allow-Origin.
function parseCorsOrigin(value) {
  const origins = (value || '')
    .split(',')
    .map((s) => s.trim())
    .filter(Boolean);
  return origins.length === 0 || origins.includes('*') ? '*' : origins;
}

module.exports = {
  port: num(process.env.PORT, 3000),
  corsOrigin: parseCorsOrigin(process.env.CORS_ORIGIN),

  table: {
    minSeats: 2, // the host picks the table size in this range
    maxSeats: 6,
    startingChips: 1000,
    smallBlind: num(process.env.SMALL_BLIND, 10),
    bigBlind: num(process.env.BIG_BLIND, 20),
  },

  turnTimeoutMs: num(process.env.TURN_TIMEOUT_MS, 30000),
  authRateMax: num(process.env.AUTH_RATE_MAX, 10), // login/register attempts per IP per minute
  timing: {
    nextHandDelayMs: num(process.env.NEXT_HAND_DELAY_MS, 2500), // pause before dealing the next hand
    resultShowMs: num(process.env.RESULT_SHOW_MS, 5000), //       how long the winner screen stays
    botThinkMs: num(process.env.BOT_THINK_MS, 900), //            bots wait this long (+ random) before acting
  },
  // How long a disconnected player's seat is kept before they are removed.
  reconnectGraceMs: num(process.env.RECONNECT_GRACE_MS, 120000),

  // Table chat (src/chat)
  chat: {
    maxLength: 140, //        characters per message
    historySize: 50, //       messages kept per table (sent to players who join later)
    rateMax: 5, //            at most this many messages...
    rateWindowMs: 10000, //   ...in this window, per player
  },

  // Where accounts, profiles, settings, login tokens and stats are saved.
  //   MONGODB_URI set -> MongoDB (e.g. a free MongoDB Atlas cluster)
  //   not set         -> JSON files in DATA_DIR (nothing to install)
  storage: {
    mongoUri: process.env.MONGODB_URI || '',
    mongoDb: process.env.MONGODB_DB || 'poker',
    dataDir: process.env.DATA_DIR || path.join(__dirname, '..', 'data'),
  },

  // Optional: folder with a Unity WebGL build to serve from this server (same address
  // for game and server, so a single deployment is enough). Empty = don't serve.
  webglDir: process.env.WEBGL_DIR || path.join(__dirname, '..', 'public'),

  auth: {
    sessionTokenTtlMs: 12 * 60 * 60 * 1000, // without "remember me"
    rememberTokenTtlMs: 30 * DAY_MS, //        with "remember me"
  },
};
