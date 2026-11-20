const test = require('node:test');
const assert = require('node:assert/strict');
const { createDeck, shuffle, rankValue, isValidCard } = require('../src/engine/cards');

test('deck has 52 unique valid cards', () => {
  const deck = createDeck();
  assert.equal(deck.length, 52);
  assert.equal(new Set(deck).size, 52);
  assert.ok(deck.every(isValidCard));
});

test('shuffle keeps the same cards and does not mutate input', () => {
  const deck = createDeck();
  const copy = deck.slice();
  const shuffled = shuffle(deck);
  assert.deepEqual(deck, copy);
  assert.deepEqual([...shuffled].sort(), [...deck].sort());
});

test('shuffle is deterministic with an injected random source', () => {
  const zero = () => 0;
  assert.deepEqual(shuffle(['a', 'b', 'c'], zero), shuffle(['a', 'b', 'c'], zero));
});

test('rank values run 2..14 with ace high', () => {
  assert.equal(rankValue('2h'), 2);
  assert.equal(rankValue('Ts'), 10);
  assert.equal(rankValue('Ad'), 14);
});
