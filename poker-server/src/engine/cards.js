const { randomInt } = require('crypto');

// Cards are 2-char strings: rank + suit, e.g. "Ah", "Td", "2c".
const RANKS = ['2', '3', '4', '5', '6', '7', '8', '9', 'T', 'J', 'Q', 'K', 'A'];
const SUITS = ['h', 'd', 'c', 's'];

/** Numeric rank: 2..14 (ace high). */
const rankValue = (card) => RANKS.indexOf(card[0]) + 2;
const suitOf = (card) => card[1];

function createDeck() {
  const deck = [];
  for (const suit of SUITS) {
    for (const rank of RANKS) deck.push(rank + suit);
  }
  return deck;
}

/**
 * Fisher-Yates shuffle. Uses crypto.randomInt by default so the order can't be
 * predicted from Math.random state. Pass a custom `randInt(max)` in tests.
 */
function shuffle(cards, randInt = randomInt) {
  const out = cards.slice();
  for (let i = out.length - 1; i > 0; i--) {
    const j = randInt(i + 1);
    [out[i], out[j]] = [out[j], out[i]];
  }
  return out;
}

function isValidCard(card) {
  return typeof card === 'string' && card.length === 2 && RANKS.includes(card[0]) && SUITS.includes(card[1]);
}

module.exports = { RANKS, SUITS, rankValue, suitOf, createDeck, shuffle, isValidCard };
