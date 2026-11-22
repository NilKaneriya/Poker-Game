const { HAND_NAMES } = require('../engine/constants');
const KeyedQueue = require('../utils/KeyedQueue');

const EMPTY = () => ({ handsPlayed: 0, handsWon: 0, biggestPot: 0, bestHandRank: -1 });

/**
 * Per-player statistics: the game's single retention feature.
 * Hands played, hands won, biggest pot won, best hand shown at showdown.
 * Deliberately no streaks, daily rewards, timers or anything that pushes extra play.
 * Kept in storage (MongoDB or JSON files) so the numbers survive restarts.
 */
class StatsService {
  queue = new KeyedQueue();

  /** @param {{ stats }} storage */
  attach(storage) {
    this.stats = storage.stats;
    return this;
  }

  /** The player's stats, shaped for the client. */
  async get(playerId) {
    const s = (await this.stats.get(playerId)) || EMPTY();
    const bestHandRank = Number.isInteger(s.bestHandRank) ? s.bestHandRank : -1;
    return {
      handsPlayed: s.handsPlayed || 0,
      handsWon: s.handsWon || 0,
      biggestPot: s.biggestPot || 0,
      bestHand: bestHandRank >= 0 ? HAND_NAMES[bestHandRank] : '',
      bestHandRank,
    };
  }

  /**
   * Record one finished hand for every human who was dealt in.
   * @param {string[]} playerIds  humans dealt into the hand
   * @param {object} result       HoldemHand result ({ winners, handNames? })
   */
  async recordHand(playerIds, result) {
    await Promise.all(
      playerIds.map((playerId) =>
        // One save at a time per player: see utils/KeyedQueue.js
        this.queue.run(playerId, async () => {
          const s = { ...EMPTY(), ...((await this.stats.get(playerId)) || {}) };
          s.handsPlayed++;

          const win = result.winners.find((w) => w.playerId === playerId);
          if (win) {
            s.handsWon++;
            s.biggestPot = Math.max(s.biggestPot, win.amount);
          }

          // A hand is only known when it is shown at showdown (nobody shows when everyone else folds).
          const rank = HAND_NAMES.indexOf(result.handNames?.[playerId] ?? '');
          if (rank > s.bestHandRank) s.bestHandRank = rank;

          await this.stats.set(playerId, s);
        }),
      ),
    );
  }
}

module.exports = new StatsService();
module.exports.StatsService = StatsService; // class, for tests
