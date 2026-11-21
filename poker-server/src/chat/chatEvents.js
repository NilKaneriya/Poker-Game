// Socket.IO event names for chat. Separate from the game's events.js on purpose.
// Mirrored in Unity: Assets/Scripts/Chat/ChatConstants.cs
module.exports = Object.freeze({
  // Client -> Server
  SEND: 'chatSend', //        { text }  a message to everyone at your table
  GET_HISTORY: 'getChat', //  the chat history of your table (answered with HISTORY)

  // Server -> Client
  MESSAGE: 'chatMessage', //  { id, kind: 'player' | 'system', playerId, name, text, time }
  HISTORY: 'chatHistory', //  { code, messages: [...] }  oldest first
  ERROR: 'chatError', //      { message }  e.g. sending too fast
});
