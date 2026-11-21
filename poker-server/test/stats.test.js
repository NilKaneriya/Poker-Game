const test = require('node:test');
const assert = require('node:assert');
const { StatsService } = require('../src/services/StatsService');

test('new players start with empty stats', () => {
  const stats = new StatsService(null);
  assert.deepStrictEqual(stats.get('p1'), { handsPlayed: 0, handsWon: 0, biggestPot: 0, bestHand: '', bestHandRank: -1 });
});

test('records hands played, wins, biggest pot and best shown hand', () => {
  const stats = new StatsService(null);
  stats.recordHand(['a', 'b'], {
    winners: [{ playerId: 'a', amount: 300 }],
    handNames: { a: 'Flush', b: 'One Pair' },
  });
  stats.recordHand(['a', 'b'], {
    winners: [{ playerId: 'b', amount: 120 }], // everyone else folded: no hands shown
  });
  stats.recordHand(['a'], {
    winners: [{ playerId: 'a', amount: 80 }],
    handNames: { a: 'Two Pair' }, // worse than the Flush: best hand stays Flush
  });

  assert.deepStrictEqual(stats.get('a'), { handsPlayed: 3, handsWon: 2, biggestPot: 300, bestHand: 'Flush', bestHandRank: 5 });
  assert.deepStrictEqual(stats.get('b'), { handsPlayed: 2, handsWon: 1, biggestPot: 120, bestHand: 'One Pair', bestHandRank: 1 });
});

test('split pot counts as a win for every winner', () => {
  const stats = new StatsService(null);
  stats.recordHand(['a', 'b', 'c'], {
    winners: [{ playerId: 'a', amount: 150 }, { playerId: 'b', amount: 150 }],
    handNames: { a: 'Straight', b: 'Straight', c: 'High Card' },
  });
  assert.strictEqual(stats.get('a').handsWon, 1);
  assert.strictEqual(stats.get('b').handsWon, 1);
  assert.strictEqual(stats.get('c').handsWon, 0);
});
