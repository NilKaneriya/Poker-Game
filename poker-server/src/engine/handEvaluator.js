const { HAND_RANKS, HAND_NAMES } = require('./constants');
const { rankValue, suitOf } = require('./cards');

/**
 * Hand evaluation for Texas Hold'em.
 *
 * evaluate(cards) takes 5 to 7 cards and returns the best 5-card hand:
 *   {
 *     rank:   HAND_RANKS value (0 = high card ... 9 = royal flush)
 *     name:   display name, e.g. "Full House"
 *     score:  numbers compared left to right: [rank, then tie-breakers high to low]
 *     cards:  the 5 cards that make the hand, most important first
 *   }
 *
 * compare(a, b) returns > 0 if a wins, < 0 if b wins, 0 for an exact tie.
 *
 * Approach: try every 5-card combination (21 for 7 cards) and keep the best.
 * Simple to verify and fast enough for a 6-player table.
 */

/** Evaluate exactly 5 cards. */
function evaluateFive(cards) {
  const values = cards.map(rankValue).sort((a, b) => b - a);
  const isFlush = cards.every((c) => suitOf(c) === suitOf(cards[0]));
  const straightHigh = findStraightHigh(values);

  // Group by value: biggest groups first, then higher values first.
  // e.g. K K K 7 7 -> [[13, 3], [7, 2]]
  const counts = new Map();
  for (const v of values) counts.set(v, (counts.get(v) || 0) + 1);
  const groups = [...counts.entries()].sort((a, b) => b[1] - a[1] || b[0] - a[0]);
  const groupValues = groups.map(([v]) => v);
  const shape = groups.map(([, n]) => n).join(''); // "41", "32", "311", "221", "2111", "11111"

  let rank;
  let tiebreak;
  if (isFlush && straightHigh) {
    rank = straightHigh === 14 ? HAND_RANKS.ROYAL_FLUSH : HAND_RANKS.STRAIGHT_FLUSH;
    tiebreak = [straightHigh];
  } else if (shape === '41') {
    rank = HAND_RANKS.FOUR_OF_A_KIND;
    tiebreak = groupValues;
  } else if (shape === '32') {
    rank = HAND_RANKS.FULL_HOUSE;
    tiebreak = groupValues;
  } else if (isFlush) {
    rank = HAND_RANKS.FLUSH;
    tiebreak = values;
  } else if (straightHigh) {
    rank = HAND_RANKS.STRAIGHT;
    tiebreak = [straightHigh];
  } else if (shape === '311') {
    rank = HAND_RANKS.THREE_OF_A_KIND;
    tiebreak = groupValues;
  } else if (shape === '221') {
    rank = HAND_RANKS.TWO_PAIR;
    tiebreak = groupValues;
  } else if (shape === '2111') {
    rank = HAND_RANKS.ONE_PAIR;
    tiebreak = groupValues;
  } else {
    rank = HAND_RANKS.HIGH_CARD;
    tiebreak = values;
  }

  return {
    rank,
    name: HAND_NAMES[rank],
    score: [rank, ...tiebreak],
    cards: orderForDisplay(cards, groupValues, straightHigh),
  };
}

/**
 * Highest card of a straight in these values (sorted high to low), or 0.
 * The wheel A-2-3-4-5 counts as a 5-high straight.
 */
function findStraightHigh(sortedValues) {
  const unique = [...new Set(sortedValues)];
  if (unique.length !== 5) return 0;
  if (unique[0] - unique[4] === 4) return unique[0];
  if (unique.join(',') === '14,5,4,3,2') return 5; // wheel
  return 0;
}

/** Cards in "reading" order: groups first (K K K 7 7), straights high to low (wheel ends with A). */
function orderForDisplay(cards, groupValues, straightHigh) {
  const order = (c) => {
    const v = rankValue(c);
    if (straightHigh === 5 && v === 14) return 99; // ace goes last in the wheel
    return groupValues.indexOf(v);
  };
  return cards.slice().sort((a, b) => order(a) - order(b));
}

/** All k-element combinations of an array. */
function combinations(items, k) {
  const result = [];
  const pick = (start, chosen) => {
    if (chosen.length === k) return result.push(chosen.slice());
    for (let i = start; i < items.length; i++) {
      chosen.push(items[i]);
      pick(i + 1, chosen);
      chosen.pop();
    }
  };
  pick(0, []);
  return result;
}

function evaluate(cards) {
  if (!Array.isArray(cards) || cards.length < 5 || cards.length > 7) {
    throw new Error(`evaluate needs 5-7 cards, got ${cards?.length}`);
  }
  let best = null;
  for (const five of combinations(cards, 5)) {
    const hand = evaluateFive(five);
    if (!best || compare(hand, best) > 0) best = hand;
  }
  return best;
}

function compare(a, b) {
  const len = Math.max(a.score.length, b.score.length);
  for (let i = 0; i < len; i++) {
    const diff = (a.score[i] ?? 0) - (b.score[i] ?? 0);
    if (diff !== 0) return diff;
  }
  return 0;
}

module.exports = { evaluate, compare };
