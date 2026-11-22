const test = require('node:test');
const assert = require('node:assert/strict');
const KeyedQueue = require('../src/utils/KeyedQueue');
const { StatsService } = require('../src/services/StatsService');
const { HandHistoryService } = require('../src/services/HandHistoryService');
const { makeHand, play } = require('./helpers');

const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

/** A collection with real waiting on every call, like a database round trip. */
function slowCollection() {
  const docs = new Map();
  const jitter = () => sleep(1 + Math.random() * 5);
  return {
    async get(id) {
      await jitter();
      return docs.has(id) ? structuredClone(docs.get(id)) : null;
    },
    async set(id, doc) {
      await jitter();
      docs.set(id, structuredClone(doc));
    },
  };
}

test('keyed queue: one job at a time per key, in order; other keys run in parallel', async () => {
  const queue = new KeyedQueue();
  const log = [];
  const job = (key, i, ms) => () => sleep(ms).then(() => log.push(`${key}${i}`));
  await Promise.all([queue.run('a', job('a', 1, 20)), queue.run('a', job('a', 2, 1)), queue.run('b', job('b', 1, 5))]);
  assert.deepEqual(log, ['b1', 'a1', 'a2']); // a2 waited for a1; b did not wait for a
});

test('keyed queue: a failed job does not block the next one', async () => {
  const queue = new KeyedQueue();
  const failed = queue.run('a', async () => {
    throw new Error('boom');
  });
  const next = queue.run('a', async () => 'ok');
  await assert.rejects(failed, /boom/);
  assert.equal(await next, 'ok');
  await sleep(0);
  assert.equal(queue.tails.size, 0); // nothing left behind
});

test('stats: hands finishing at the same time are all counted (no lost update)', async () => {
  const stats = new StatsService().attach({ stats: slowCollection() });
  const result = { winners: [{ playerId: 'ann', amount: 100 }], handNames: {} };
  await Promise.all(Array.from({ length: 20 }, () => stats.recordHand(['ann', 'bob'], result)));
  const ann = await stats.get('ann');
  assert.equal(ann.handsPlayed, 20);
  assert.equal(ann.handsWon, 20);
  assert.equal((await stats.get('bob')).handsPlayed, 20);
});

test('hand history: hands finishing at the same time are all kept (no lost update)', async () => {
  const history = new HandHistoryService().attach({ handHistory: slowCollection() });
  const saves = Array.from({ length: 10 }, (_, n) => {
    const hand = makeHand({ stacks: [1000, 1000] });
    hand.info = { handNumber: n + 1, startedAt: n, buttonSeat: 0, seats: [] };
    play(hand, [['p0', 'fold']]);
    return history.recordHand({ code: 'T', name: 'T' }, hand, ['p0']);
  });
  await Promise.all(saves);
  const numbers = (await history.list('p0')).map((h) => h.handNumber).sort((a, b) => a - b);
  assert.deepEqual(numbers, [1, 2, 3, 4, 5, 6, 7, 8, 9, 10]);
});
