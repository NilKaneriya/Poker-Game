const test = require('node:test');
const assert = require('node:assert/strict');
const { buildPots, distributePots } = require('../src/engine/pots');
const { evaluate } = require('../src/engine/handEvaluator');

const c = (playerId, amount, folded = false) => ({ playerId, amount, folded });
const hands = (obj) => new Map(Object.entries(obj).map(([id, cards]) => [id, evaluate(cards.split(' '))]));
const totalOf = (results) => results.reduce((sum, p) => sum + p.winners.reduce((s, w) => s + w.share, 0), 0);

test('everyone in for the same amount makes one pot', () => {
  assert.deepEqual(buildPots([c('a', 100), c('b', 100), c('c', 100)]), [{ amount: 300, eligible: ['a', 'b', 'c'] }]);
});

test('folded chips stay in the pot but folded players cannot win', () => {
  assert.deepEqual(buildPots([c('a', 100), c('b', 40, true), c('c', 100)]), [{ amount: 240, eligible: ['a', 'c'] }]);
});

test('three all-ins for different amounts make a main pot and two side pots', () => {
  const pots = buildPots([c('a', 50), c('b', 200), c('c', 500), c('d', 500)]);
  assert.deepEqual(pots, [
    { amount: 200, eligible: ['a', 'b', 'c', 'd'] }, // 50 x 4
    { amount: 450, eligible: ['b', 'c', 'd'] }, //      150 x 3
    { amount: 600, eligible: ['c', 'd'] }, //           300 x 2
  ]);
});

test('chips above every remaining player (from a folder) join the pot below', () => {
  // b folded after putting in more than the all-in players a and c.
  const pots = buildPots([c('a', 50), c('b', 80, true), c('c', 50)]);
  assert.deepEqual(pots, [{ amount: 180, eligible: ['a', 'c'] }]);
});

test('short all-in wins only the main pot; the side pot goes to the next best hand', () => {
  const board = 'Jd 8c 5h 3s 2c';
  const pots = buildPots([c('a', 50), c('b', 200), c('c', 200)]);
  const results = distributePots(pots, hands({
    a: `As Ah ${board}`, // best hand, but only covered for 50
    b: `Ks Kh ${board}`, // second best
    c: `Qs Qh ${board}`,
  }), ['a', 'b', 'c']);

  assert.deepEqual(results, [
    { amount: 150, winners: [{ playerId: 'a', share: 150 }] },
    { amount: 300, winners: [{ playerId: 'b', share: 300 }] },
  ]);
});

test('tied hands split the pot equally', () => {
  const board = 'Ah Kh Qh Jh Th'; // royal flush on board: everyone plays it
  const results = distributePots([{ amount: 300, eligible: ['a', 'b', 'c'] }],
    hands({ a: `2c 3d ${board}`, b: `4c 5d ${board}`, c: `6c 7d ${board}` }), ['a', 'b', 'c']);
  assert.deepEqual(results[0].winners.map((w) => w.share), [100, 100, 100]);
});

test('odd chip goes to the tied winner closest to the left of the button', () => {
  const board = 'Ah Kh Qh Jh Th';
  const pot = [{ amount: 101, eligible: ['a', 'b'] }];
  const h = hands({ a: `2c 3d ${board}`, b: `4c 5d ${board}` });

  // Seat order from the button: b is first, so b gets the extra chip.
  assert.deepEqual(distributePots(pot, h, ['b', 'a'])[0].winners, [
    { playerId: 'b', share: 51 }, { playerId: 'a', share: 50 },
  ]);
  // Same pot, other button position: a gets it.
  assert.deepEqual(distributePots(pot, h, ['a', 'b'])[0].winners, [
    { playerId: 'a', share: 51 }, { playerId: 'b', share: 50 },
  ]);
});

test('a split can happen in one pot while another pot has a single winner', () => {
  const board = '9d 9c 4h 4s 2c';
  // a and b both play two pair 9s and 4s with an ace kicker; c is covered by b only.
  const pots = buildPots([c('a', 100), c('b', 300), c('c', 300)]);
  const results = distributePots(pots, hands({
    a: `Ah 3d ${board}`,
    b: `As 3c ${board}`,
    c: `Kh Qh ${board}`,
  }), ['c', 'a', 'b']);

  assert.deepEqual(results[0], { amount: 300, winners: [{ playerId: 'a', share: 150 }, { playerId: 'b', share: 150 }] });
  assert.deepEqual(results[1], { amount: 400, winners: [{ playerId: 'b', share: 400 }] });
});

test('no chips are created or lost', () => {
  const contributions = [c('a', 37), c('b', 120, true), c('c', 250), c('d', 251), c('e', 999)];
  const pots = buildPots(contributions);
  const board = '2c 7d 9h Js Kd';
  const results = distributePots(pots, hands({
    a: `As Ah ${board}`, c: `Ks Kh ${board}`, d: `Qs Qh ${board}`, e: `3s 4h ${board}`,
  }), ['a', 'b', 'c', 'd', 'e']);
  assert.equal(totalOf(results), contributions.reduce((s, x) => s + x.amount, 0));
});

test('when everyone else folded the last player takes the pot without a hand', () => {
  const pots = buildPots([c('a', 30), c('b', 10, true), c('c', 20, true)]);
  const results = distributePots(pots, new Map(), ['a', 'b', 'c']);
  assert.deepEqual(results, [{ amount: 60, winners: [{ playerId: 'a', share: 60 }] }]);
});
