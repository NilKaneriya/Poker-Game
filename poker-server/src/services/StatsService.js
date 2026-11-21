const fs = require('fs');
const path = require('path');
const config = require('../config');
const { HAND_NAMES } = require('../engine/constants');

/**
 * Per-player statistics: the game's single retention feature.
 * Hands played, hands won, biggest pot won, best hand shown at showdown.
 * Deliberately no streaks, daily rewards, timers or anything that pushes extra play.
 * Saved to a JSON file so the numbers survive a server restart.
 */
class StatsService {
  /** @param {string|null} filePath  where to save; null = memory only (tests) */
  constructor(filePath) {
    this.filePath = filePath;
    this.byPlayer = {}; // playerId -> { handsPlayed, handsWon, biggestPot, bestHandRank }
    this.load();
  }

  load() {
    if (!this.filePath) return;
    try {
      this.byPlayer = JSON.parse(fs.readFileSync(this.filePath, 'utf8'));
    } catch {
      // No file yet (first run) - start empty.
    }
  }

  save() {
    if (!this.filePath) return;
    fs.mkdirSync(path.dirname(this.filePath), { recursive: true });
    fs.writeFileSync(this.filePath, JSON.stringify(this.byPlayer, null, 2));
  }

  /** The player's stats, shaped for the client. */
  get(playerId) {
    const s = this.byPlayer[playerId] || {};
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
  recordHand(playerIds, result) {
    for (const playerId of playerIds) {
      const s = this.byPlayer[playerId]
        || (this.byPlayer[playerId] = { handsPlayed: 0, handsWon: 0, biggestPot: 0, bestHandRank: -1 });
      s.handsPlayed++;

      const win = result.winners.find((w) => w.playerId === playerId);
      if (win) {
        s.handsWon++;
        s.biggestPot = Math.max(s.biggestPot, win.amount);
      }

      // A hand is only known when it is shown at showdown (nobody shows when everyone else folds).
      const rank = HAND_NAMES.indexOf(result.handNames?.[playerId] ?? '');
      if (rank > s.bestHandRank) s.bestHandRank = rank;
    }
    if (playerIds.length > 0) this.save();
  }
}

module.exports = new StatsService(config.statsFile);
module.exports.StatsService = StatsService; // class, for tests
