/**
 * Per-player statistics (the single retention feature): hands played,
 * hands won, biggest pot, best hand. Kept in memory per session.
 * Deliberately no streaks, timers or rewards that push extra play.
 */
class StatsService {
  constructor() {
    this.byPlayer = new Map(); // playerId -> stats
  }

  get(playerId) {
    if (!this.byPlayer.has(playerId)) {
      this.byPlayer.set(playerId, { handsPlayed: 0, handsWon: 0, biggestPot: 0, bestHand: null });
    }
    return this.byPlayer.get(playerId);
  }

  // TODO: recordHand(playerId, { won, potWon, hand }) once showdown results exist.
}

module.exports = new StatsService();
