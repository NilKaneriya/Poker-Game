/**
 * Runs async jobs one at a time per key (jobs for different keys still run in parallel).
 *
 * Services that read a player's document, change it and write it back (stats, hand
 * history) must not interleave two of those for the same player: with real database
 * round trips the second write would overwrite the first ("lost update"). Found by the
 * end-to-end test on MongoDB when two hands of one player finished close together.
 */
class KeyedQueue {
  constructor() {
    this.tails = new Map(); // key -> promise of the last queued job
  }

  /** Queue fn for this key; resolves/rejects with fn's result. */
  run(key, fn) {
    const previous = this.tails.get(key) || Promise.resolve();
    const job = previous.then(fn, fn); // run even if the previous job failed
    const tail = job.catch(() => {}); // keep the chain alive after a failure
    this.tails.set(key, tail);
    tail.then(() => {
      if (this.tails.get(key) === tail) this.tails.delete(key); // nothing queued after it
    });
    return job;
  }
}

module.exports = KeyedQueue;
