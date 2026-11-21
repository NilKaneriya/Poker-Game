const CHAT_EVENTS = require('./chatEvents');
const { cleanChatText } = require('./chatText');

/**
 * Socket handlers for one connection. Only players at a table (seated, or waiting
 * for a seat) can read or write its chat. The sender's name comes from their login
 * (socket.data), never from the payload.
 *
 * @param {object} deps  { io, store, limiter, settings, tableOf, roomOf }
 */
function registerChatHandlers(socket, { io, store, limiter, settings, tableOf, roomOf }) {
  const { playerId } = socket.data;
  const fail = (message) => socket.emit(CHAT_EVENTS.ERROR, { message });

  socket.on(CHAT_EVENTS.SEND, (payload) => {
    const code = tableOf(playerId);
    if (!code) return fail('Join a table to chat');

    const text = cleanChatText(payload?.text, settings.maxLength);
    if (!text) return; // nothing left after cleaning: nothing to send
    if (!limiter.tryHit(playerId)) return fail('Slow down - wait a few seconds before sending more messages');

    // Read the name now, not at connect time: the player may have changed it since.
    const name = socket.data.name;
    io.to(roomOf(code)).emit(CHAT_EVENTS.MESSAGE, store.addPlayerMessage(code, { playerId, name }, text));
  });

  socket.on(CHAT_EVENTS.GET_HISTORY, () => {
    const code = tableOf(playerId);
    socket.emit(CHAT_EVENTS.HISTORY, { code, messages: code ? store.history(code) : [] });
  });
}

module.exports = { registerChatHandlers };
