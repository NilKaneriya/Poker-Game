// The emotes a player can send. Clients draw each id with their own art; the server only
// checks the id is on this list. Same list, same order in Unity: ReactionConstants.Emotes.Ids
const EMOTE_IDS = Object.freeze([
  'laugh', 'wow', 'cool', 'love', 'angry', 'cry',
  'wink', 'sleepy', 'heart', 'fire', 'coin', 'gg',
]);

const isEmote = (id) => typeof id === 'string' && EMOTE_IDS.includes(id);

module.exports = { EMOTE_IDS, isEmote };
