const config = require('../config');
const ChatStore = require('./ChatStore');
const RateLimiter = require('./RateLimiter');
const CHAT_EVENTS = require('./chatEvents');
const { registerChatHandlers } = require('./chatHandlers');
const { SYSTEM_TEXT, winnerLines } = require('./systemMessages');

/**
 * Table chat: a separate module. It never imports game code. Everything it needs
 * from the game is passed in:
 *
 *   tableOf(playerId) -> table code or null   (who may chat where)
 *   roomOf(code)      -> Socket.IO room of that table's players
 *   gameEvents        -> EventEmitter of game events (for system messages)
 *
 * Removing chat = deleting this folder and the attachChat call in src/index.js.
 */
function attachChat(io, { tableOf, roomOf, gameEvents }, settings = config.chat) {
  const store = new ChatStore(settings.historySize);
  // One limiter for all sockets, so a second tab doesn't double the allowance.
  const limiter = new RateLimiter({ max: settings.rateMax, windowMs: settings.rateWindowMs });

  io.on('connection', (socket) => registerChatHandlers(socket, { io, store, limiter, settings, tableOf, roomOf }));

  const post = (code, text) => io.to(roomOf(code)).emit(CHAT_EVENTS.MESSAGE, store.addSystemMessage(code, text));

  const { GAME_EVENTS } = gameEvents;
  for (const [event, toText] of Object.entries(SYSTEM_TEXT)) gameEvents.on(event, (e) => post(e.code, toText(e)));
  gameEvents.on(GAME_EVENTS.HAND_FINISHED, (e) => winnerLines(e).forEach((line) => post(e.code, line)));
  gameEvents.on(GAME_EVENTS.TABLE_CLOSED, ({ code }) => store.clear(code));

  return { store };
}

module.exports = { attachChat, CHAT_EVENTS };
