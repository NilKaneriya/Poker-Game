const REACTION_EVENTS = require('./reactionEvents');
const ReactionService = require('./ReactionService');
const { EMOTE_IDS } = require('./emotes');

const DEFAULTS = Object.freeze({
  rateMax: 4, //          reactions per player...
  rateWindowMs: 6000, //  ...per this many ms
});

/**
 * Reactions (emotes thrown at the table or at another player): a stand-alone module.
 * It imports nothing from the game or chat, so it can be dropped into any Socket.IO
 * server. Everything it needs is passed in:
 *
 *   groupOf(userId)           -> group key or null    (e.g. the table the player is at)
 *   roomOf(group)             -> Socket.IO room to broadcast to
 *   isInGroup(group, userId)  -> true if userId may be targeted in that group
 *   whoIs(socket)             -> { id, name } of the sender (default: socket.data.playerId / name)
 *
 * Reactions are not stored: they are a moment at the table, not history.
 * Removing reactions = deleting this folder and the attachReactions call in src/index.js.
 */
function attachReactions(io, { groupOf, roomOf, isInGroup, whoIs = defaultWhoIs }, settings = {}) {
  const service = new ReactionService({ groupOf, isInGroup }, { ...DEFAULTS, ...settings });

  io.on('connection', (socket) => {
    socket.on(REACTION_EVENTS.SEND, (payload) => {
      // Read the sender now, not at connect time: their display name may have changed.
      const result = service.send(whoIs(socket), payload);
      if (result.error) socket.emit(REACTION_EVENTS.ERROR, { message: result.error });
      else if (result.reaction) io.to(roomOf(result.group)).emit(REACTION_EVENTS.REACTION, result.reaction);
    });
  });

  return { service };
}

function defaultWhoIs(socket) {
  return { id: socket.data.playerId, name: socket.data.name };
}

module.exports = { attachReactions, REACTION_EVENTS, EMOTE_IDS, ReactionService };
