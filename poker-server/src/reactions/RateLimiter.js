/**
 * "At most N events per window" per key (sliding window). Stops emote spam.
 * The reactions module keeps its own copy (instead of sharing chat's) so it can be
 * copied into another project on its own.
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
    const allowed = recent.length < this.max;
    if (allowed) recent.push(now);
    this.hits.set(key, recent);
    return allowed;
  }
}

module.exports = RateLimiter;
