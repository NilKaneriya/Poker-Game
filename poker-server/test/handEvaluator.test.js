const test = require('node:test');
const assert = require('node:assert/strict');
const { evaluate, compare } = require('../src/engine/handEvaluator');
const { HAND_RANKS } = require('../src/engine/constants');

const hand = (str) => evaluate(str.split(' '));
const beats = (a, b) => compare(hand(a), hand(b)) > 0;
const ties = (a, b) => compare(hand(a), hand(b)) === 0;

test('recognises every hand rank', () => {
  const cases = [
    ['Ah Kh Qh Jh Th', HAND_RANKS.ROYAL_FLUSH, 'Royal Flush'],
    ['9s 8s 7s 6s 5s', HAND_RANKS.STRAIGHT_FLUSH, 'Straight Flush'],
    ['Qc Qd Qh Qs 2d', HAND_RANKS.FOUR_OF_A_KIND, 'Four of a Kind'],
    ['Kc Kd Kh 7s 7d', HAND_RANKS.FULL_HOUSE, 'Full House'],
    ['Ad 9d 7d 4d 2d', HAND_RANKS.FLUSH, 'Flush'],
    ['9c 8d 7h 6s 5c', HAND_RANKS.STRAIGHT, 'Straight'],
    ['7c 7d 7h Ks 2d', HAND_RANKS.THREE_OF_A_KIND, 'Three of a Kind'],
    ['Jc Jd 4h 4s Ad', HAND_RANKS.TWO_PAIR, 'Two Pair'],
    ['Tc Td 8h 4s 2d', HAND_RANKS.ONE_PAIR, 'One Pair'],
    ['Ac Jd 8h 4s 2d', HAND_RANKS.HIGH_CARD, 'High Card'],
  ];
  for (const [cards, rank, name] of cases) {
    const result = hand(cards);
    assert.equal(result.rank, rank, cards);
    assert.equal(result.name, name, cards);
  }
});

test('ranks are ordered from royal flush down to high card', () => {
  const ladder = [
    'Ah Kh Qh Jh Th', '9s 8s 7s 6s 5s', 'Qc Qd Qh Qs 2d', 'Kc Kd Kh 7s 7d', 'Ad 9d 7d 4d 2d',
    '9c 8d 7h 6s 5c', '7c 7d 7h Ks 2d', 'Jc Jd 4h 4s Ad', 'Tc Td 8h 4s 2d', 'Ac Jd 8h 4s 2d',
  ];
  for (let i = 0; i < ladder.length - 1; i++) assert.ok(beats(ladder[i], ladder[i + 1]), `${ladder[i]} > ${ladder[i + 1]}`);
});

test('wheel A-2-3-4-5 is the lowest straight', () => {
  const wheel = hand('Ah 2c 3d 4s 5h');
  assert.equal(wheel.rank, HAND_RANKS.STRAIGHT);
  assert.equal(wheel.score[1], 5);
  assert.deepEqual(wheel.cards, ['5h', '4s', '3d', '2c', 'Ah']); // ace shown last
  assert.ok(beats('6h 2c 3d 4s 5h', 'Ah 2c 3d 4s 5h'));
  assert.equal(hand('Ac 2c 3c 4c 5c').rank, HAND_RANKS.STRAIGHT_FLUSH); // steel wheel, not royal
});

test('K-A-2-3-4 is not a straight', () => {
  assert.equal(hand('Kh Ac 2d 3s 4h').rank, HAND_RANKS.HIGH_CARD);
});

test('kickers decide between equal ranks', () => {
  assert.ok(beats('Ac Ad Kh 7s 2d', 'As Ah Qh 7d 2c'));          // pair of aces, K kicker beats Q
  assert.ok(beats('Jc Jd 4h 4s Ad', 'Jh Js 4c 4d Kd'));          // two pair, A kicker
  assert.ok(beats('Kc Kd 5h 5s 2d', 'Qc Qd Jh Js Ad'));          // higher top pair wins
  assert.ok(beats('Ad 9d 7d 4d 3d', 'Ac 9c 7c 4c 2c'));          // flush decided by 5th card
  assert.ok(beats('8c 8d 8h 2s 2d', '7c 7d 7h As Ad'));          // full house: trips first
  assert.ok(beats('Qc Qd Qh Qs 3d', 'Qc Qd Qh Qs 2d'));          // quads kicker
});

test('identical strength ties regardless of suits', () => {
  assert.ok(ties('Ac Kd Qh Js 9d', 'Ad Kc Qs Jh 9c'));
  assert.ok(ties('9c 8d 7h 6s 5c', '9d 8c 7s 6h 5d'));
});

test('picks the best 5 of 7 cards', () => {
  // Board makes a flush; the pair in hand doesn't matter.
  const flush = evaluate(['2h', '2c', 'Ah', 'Th', '7h', '4h', 'Ks']);
  assert.equal(flush.rank, HAND_RANKS.FLUSH);
  assert.deepEqual([...flush.cards].sort(), ['2h', '4h', '7h', 'Ah', 'Th'].sort());

  // Two pair + pair on board -> best two pair with best kicker.
  const twoPair = evaluate(['Kc', 'Kd', '9h', '9s', '4c', '4d', 'Ah']);
  assert.equal(twoPair.rank, HAND_RANKS.TWO_PAIR);
  assert.deepEqual(twoPair.score, [HAND_RANKS.TWO_PAIR, 13, 9, 14]);

  // Straight hidden in 7 cards with a pair.
  assert.equal(evaluate(['5c', '6d', '7h', '8s', '9c', '9d', '2h']).rank, HAND_RANKS.STRAIGHT);
});

test('board plays: both players tie when the board is the best hand', () => {
  const board = ['Ah', 'Kh', 'Qh', 'Jh', 'Th'];
  const a = evaluate([...board, '2c', '3d']);
  const b = evaluate([...board, '9s', '8s']);
  assert.equal(compare(a, b), 0);
});

test('rejects wrong card counts', () => {
  assert.throws(() => evaluate(['Ah', 'Kh', 'Qh', 'Jh']));
  assert.throws(() => evaluate(['Ah', 'Kh', 'Qh', 'Jh', 'Th', '9h', '8h', '7h']));
});
