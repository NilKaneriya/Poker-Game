/**
 * "At most N events per window" per key (sliding window). Used to stop chat spam.
 */
class RateLimiter {
  constructor({ max, windowMs, now = Date.now }) {
    this.max = max;
    this.windowMs = windowMs;
    this.now = now;
    this.hits = new Map(); // key -> timestamps inside the window
  }

  /** Records the attempt and returns true if it is allowed. */
  tryHit(key) {
    const now = this.now();
    const recent = (this.hits.get(key) || []).filter((t) => now - t < this.windowMs);
    if (recent.length >= this.max) {
      this.hits.set(key, recent);
      return false;
    }
    recent.push(now);
    this.hits.set(key, recent);
    return true;
  }

  forget(key) {
    this.hits.delete(key);
  }
}

module.exports = RateLimiter;
