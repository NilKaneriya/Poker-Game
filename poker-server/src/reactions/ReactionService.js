const RateLimiter = require('./RateLimiter');
const { isEmote } = require('./emotes');

/**
 * The rules of reactions, with no networking: who may send what to whom, and how often.
 * Socket.IO lives in index.js; this class can be used from any transport (or a test).
 *
 *   groupOf(userId)           -> group key (e.g. a table code) or null if not in a group
 *   isInGroup(group, userId)  -> may this user be the target of a reaction in that group?
 */
class ReactionService {
  constructor({ groupOf, isInGroup }, { rateMax, rateWindowMs }, now = Date.now) {
    this.groupOf = groupOf;
    this.isInGroup = isInGroup;
    this.now = now;
    this.limiter = new RateLimiter({ max: rateMax, windowMs: rateWindowMs, now });
    this.nextId = 1;
  }

  /**
   * Checks a reaction from `from` ({ id, name }) and returns either
   *   { group, reaction }  to send to everyone in the group, or
   *   { error }            to tell the sender (or { ignored: true } for junk payloads).
   */
  send(from, payload) {
    const group = this.groupOf(from.id);
    if (!group) return { error: 'Join a table to send reactions' };

    const emoteId = payload?.emoteId;
    if (!isEmote(emoteId)) return { ignored: true }; // unknown emote: an old or modified client

    let targetId = payload?.targetId ?? null;
    if (targetId === '' || targetId === from.id) targetId = null; // "to myself" = to everyone
    if (targetId !== null && (typeof targetId !== 'string' || !this.isInGroup(group, targetId))) {
      return { error: 'That player is not at your table' };
    }

    if (!this.limiter.tryHit(from.id)) return { error: 'Easy! Wait a moment before sending another reaction' };

    return {
      group,
      reaction: { id: this.nextId++, emoteId, fromId: from.id, fromName: from.name, targetId, time: this.now() },
    };
  }
}

module.exports = ReactionService;
