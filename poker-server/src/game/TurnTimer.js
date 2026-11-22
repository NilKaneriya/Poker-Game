/**
 * One countdown per table for the player whose turn it is.
 * Exposes the deadline so clients can draw their own countdown.
 */
class TurnTimer {
  constructor() {
    this.handle = null;
    this.deadline = 0;
  }

  start(ms, onExpire) {
    this.clear();
    this.deadline = Date.now() + ms;
    this.handle = setTimeout(() => {
      this.handle = null;
      this.deadline = 0;
      onExpire();
    }, ms);
  }

  clear() {
    if (this.handle) clearTimeout(this.handle);
    this.handle = null;
    this.deadline = 0;
  }

  get remainingMs() {
    return this.deadline ? Math.max(0, this.deadline - Date.now()) : 0;
  }
}

module.exports = TurnTimer;
