// Socket.IO event names for reactions (emotes). Separate from the game's and chat's events.
// Mirrored in Unity: Assets/Scripts/Reactions/ReactionConstants.cs
module.exports = Object.freeze({
  // Client -> Server
  SEND: 'reactionSend', //    { emoteId, targetId? }  targetId = another player in your group, or none for everyone

  // Server -> Client
  REACTION: 'reaction', //    { id, emoteId, fromId, fromName, targetId, time }  to everyone in the group
  ERROR: 'reactionError', //  { message }  e.g. sending too fast
});
