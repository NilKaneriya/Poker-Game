const config = require('../config');
const KeyedQueue = require('../utils/KeyedQueue');
const { findItem } = require('./shopCatalog');

const isChips = (n) => Number.isSafeInteger(n) && n >= 0;

/**
 * Every player's chips outside a table. The ONLY place where chip balances change.
 *
 * Rules that keep the economy honest:
 *  - Chips come from two places only: the starting grant and the (dummy) shop.
 *    Everything else is a transfer: wallet -> table seat on sitting down, seat -> wallet
 *    on leaving. So wallet + chips on tables never changes except by those two sources.
 *  - Amounts are always decided here or by the game engine, never taken from a client.
 *    Clients only send a shop item id.
 *  - One change at a time per player (KeyedQueue), so two quick clicks or two sockets can
 *    never both spend the same chips (the "double spend" race).
 *  - Whole, non-negative, safe integers only; the balance can never go below 0 or past
 *    config.economy.maxBalance.
 *  - `inPlay` records the chips handed to a table. If the server restarts (tables live in
 *    memory) the player gets them back the next time they connect (see reconcile).
 */
class WalletService {
  queue = new KeyedQueue();
  lastPurchaseAt = new Map(); // playerId -> time of the last shop purchase

  /** @param {{ wallets }} storage */
  attach(storage) {
    this.wallets = storage.wallets;
    return this;
  }

  /** Run fn(wallet) as the player's only wallet job right now; saves when fn changed it. */
  update(playerId, fn) {
    return this.queue.run(playerId, async () => {
      const stored = await this.wallets.get(playerId);
      const wallet = stored || { balance: config.economy.startingBalance, inPlay: 0, recent: [] };
      const before = JSON.stringify(wallet);
      const result = fn(wallet);
      if (!stored || JSON.stringify(wallet) !== before) await this.wallets.set(playerId, wallet);
      return result;
    });
  }

  note(wallet, type, amount, ref = '') {
    wallet.recent.push({ type, amount, balanceAfter: wallet.balance, at: Date.now(), ref });
    if (wallet.recent.length > config.economy.recentLedger) wallet.recent.shift();
  }

  /** @returns {Promise<{ balance: number, inPlay: number }>} */
  async get(playerId) {
    const r = await this.update(playerId, (w) => ({ balance: w.balance, inPlay: w.inPlay }));
    return { balance: r.balance, inPlay: r.inPlay };
  }

  /**
   * Take chips out of the wallet for a table seat: up to `wanted`, or everything if the
   * wallet holds less (but never less than the minimum buy-in).
   * @returns {Promise<{ ok: true, amount: number, balance: number } | { ok: false, error: string }>}
   */
  buyIn(playerId, wanted, ref = '') {
    if (!isChips(wanted) || wanted === 0) return Promise.resolve({ ok: false, error: 'Invalid buy-in' });
    return this.update(playerId, (w) => {
      const amount = Math.min(wanted, w.balance);
      if (amount < config.economy.minBuyIn) {
        return { ok: false, error: `You need at least ${config.economy.minBuyIn} chips to sit down. Visit the shop.` };
      }
      w.balance -= amount;
      w.inPlay += amount;
      this.note(w, 'buyIn', -amount, ref);
      return { ok: true, amount, balance: w.balance };
    });
  }

  /**
   * Chips come back from a table seat (the stack when leaving). `release` is how much of
   * the table money this closes: everything by default, or just an unused buy-in.
   */
  cashOut(playerId, chips, ref = '', release = Infinity) {
    if (!isChips(chips)) return Promise.resolve({ ok: false, error: 'Invalid amount' });
    return this.update(playerId, (w) => {
      w.balance = Math.min(w.balance + chips, config.economy.maxBalance);
      w.inPlay = release === Infinity ? 0 : Math.max(0, w.inPlay - release);
      if (chips > 0) this.note(w, 'cashOut', chips, ref);
      return { ok: true, balance: w.balance };
    });
  }

  /**
   * Called when a player connects. If the wallet says chips are at a table but the player
   * has no seat any more (the server restarted, so the table is gone), give them back.
   */
  reconcile(playerId, isSeated) {
    return this.update(playerId, (w) => {
      if (w.inPlay > 0 && !isSeated) {
        w.balance = Math.min(w.balance + w.inPlay, config.economy.maxBalance);
        this.note(w, 'refund', w.inPlay, 'table closed');
        w.inPlay = 0;
      }
      return { ok: true, balance: w.balance };
    });
  }

  /**
   * Dummy purchase: adds the item's chips, charges nothing. Only a catalog id is accepted.
   * @returns {Promise<{ ok: true, balance: number, item: object } | { ok: false, error: string }>}
   */
  purchase(playerId, itemId) {
    const item = findItem(itemId);
    if (!item) return Promise.resolve({ ok: false, error: 'That item does not exist' });

    // Checked before queueing so a flood of requests is dropped, not queued up.
    const now = Date.now();
    if (now - (this.lastPurchaseAt.get(playerId) || 0) < config.economy.purchaseCooldownMs) {
      return Promise.resolve({ ok: false, error: 'Please wait a moment before buying again' });
    }
    this.lastPurchaseAt.set(playerId, now);

    return this.update(playerId, (w) => {
      if (w.balance + item.chips > config.economy.maxBalance) return { ok: false, error: 'Your wallet is full' };
      w.balance += item.chips;
      this.note(w, 'purchase', item.chips, item.id);
      return { ok: true, balance: w.balance, item };
    });
  }
}

module.exports = new WalletService();
module.exports.WalletService = WalletService; // class, for tests
