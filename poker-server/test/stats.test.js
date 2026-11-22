const test = require('node:test');
const assert = require('node:assert');
const { StatsService } = require('../src/services/StatsService');
const { createStorage } = require('../src/storage');

const newStats = async () => new StatsService().attach(await createStorage({ dataDir: null }));

test('new players start with empty stats', async () => {
  const stats = await newStats();
  assert.deepStrictEqual(await stats.get('p1'), { handsPlayed: 0, handsWon: 0, biggestPot: 0, bestHand: '', bestHandRank: -1 });
});

test('records hands played, wins, biggest pot and best shown hand', async () => {
  const stats = await newStats();
  await stats.recordHand(['a', 'b'], {
    winners: [{ playerId: 'a', amount: 300 }],
    handNames: { a: 'Flush', b: 'One Pair' },
  });
  await stats.recordHand(['a', 'b'], {
    winners: [{ playerId: 'b', amount: 120 }], // everyone else folded: no hands shown
  });
  await stats.recordHand(['a'], {
    winners: [{ playerId: 'a', amount: 80 }],
    handNames: { a: 'Two Pair' }, // worse than the Flush: best hand stays Flush
  });

  assert.deepStrictEqual(await stats.get('a'), {
    handsPlayed: 3,
    handsWon: 2,
    biggestPot: 300,
    bestHand: 'Flush',
    bestHandRank: 5,
  });
  assert.deepStrictEqual(await stats.get('b'), {
    handsPlayed: 2,
    handsWon: 1,
    biggestPot: 120,
    bestHand: 'One Pair',
    bestHandRank: 1,
  });
});

test('split pot counts as a win for every winner', async () => {
  const stats = await newStats();
  await stats.recordHand(['a', 'b', 'c'], {
    winners: [
      { playerId: 'a', amount: 150 },
      { playerId: 'b', amount: 150 },
    ],
    handNames: { a: 'Straight', b: 'Straight', c: 'High Card' },
  });
  assert.strictEqual((await stats.get('a')).handsWon, 1);
  assert.strictEqual((await stats.get('b')).handsWon, 1);
  assert.strictEqual((await stats.get('c')).handsWon, 0);
});
