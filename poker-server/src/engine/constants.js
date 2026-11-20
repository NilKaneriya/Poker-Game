const STAGES = Object.freeze({
  WAITING: 'waiting',
  PREFLOP: 'preflop',
  FLOP: 'flop',
  TURN: 'turn',
  RIVER: 'river',
  SHOWDOWN: 'showdown',
});

const ACTIONS = Object.freeze({
  FOLD: 'fold',
  CHECK: 'check',
  CALL: 'call',
  RAISE: 'raise', // also used for the opening bet of a street
  ALL_IN: 'allIn',
});

// Higher value beats lower value.
const HAND_RANKS = Object.freeze({
  HIGH_CARD: 0,
  ONE_PAIR: 1,
  TWO_PAIR: 2,
  THREE_OF_A_KIND: 3,
  STRAIGHT: 4,
  FLUSH: 5,
  FULL_HOUSE: 6,
  FOUR_OF_A_KIND: 7,
  STRAIGHT_FLUSH: 8,
  ROYAL_FLUSH: 9,
});

const HAND_NAMES = Object.freeze([
  'High Card',
  'One Pair',
  'Two Pair',
  'Three of a Kind',
  'Straight',
  'Flush',
  'Full House',
  'Four of a Kind',
  'Straight Flush',
  'Royal Flush',
]);

module.exports = { STAGES, ACTIONS, HAND_RANKS, HAND_NAMES };
