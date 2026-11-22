const { buildHandRecord } = require('./handRecord');
const KeyedQueue = require('../utils/KeyedQueue');

const KEEP = 30; // hands kept per player

/**
 * Each player's last hands, as they saw them (see handRecord.js), newest first.
 * Kept in storage (MongoDB or JSON files), one document per player: { hands: [...] }.
 */
class HandHistoryService {
  queue = new KeyedQueue();

  /** @param {{ handHistory }} storage */
  attach(storage) {
    this.collection = storage.handHistory;
    return this;
  }

  /** Save a finished hand for every human who was dealt in. */
  async recordHand(table, hand, playerIds) {
    await Promise.all(
      playerIds.map((playerId) =>
        // One save at a time per player: see utils/KeyedQueue.js
        this.queue.run(playerId, async () => {
          const record = buildHandRecord(table, hand, playerId);
          const doc = (await this.collection.get(playerId)) || { hands: [] };
          const hands = [record, ...(doc.hands || []).filter((h) => h.id !== record.id)].slice(0, KEEP);
          await this.collection.set(playerId, { hands });
        }),
      ),
    );
  }

  /** The player's hands, newest first. */
  async list(playerId) {
    const doc = await this.collection.get(playerId);
    return doc?.hands || [];
  }
}

module.exports = new HandHistoryService();
module.exports.HandHistoryService = HandHistoryService; // class, for tests
module.exports.KEEP = KEEP;
