const HoldemHand = require('../src/engine/HoldemHand');
const { createDeck } = require('../src/engine/cards');

/**
 * Build a hand with known cards.
 * @param {object} o
 * @param {number[]} o.stacks         chips per player; ids are p0, p1, ...
 * @param {number} [o.button=0]
 * @param {string[][]} [o.hole]       hole cards per player index, e.g. [['Ah','Kd'], ...]
 * @param {string[]} [o.board]        5 board cards
 */
function makeHand({ stacks, button = 0, sb = 10, bb = 20, hole = [], board = [] }) {
  const n = stacks.length;
  const used = new Set([...hole.flat(), ...board]);
  const spare = createDeck().filter((c) => !used.has(c));
  const next = (card) => card || spare.shift();

  // Same order the engine deals in: two rounds starting left of the button,
  // then burn + flop, burn + turn, burn + river.
  const deck = [];
  for (let round = 0; round < 2; round++) {
    for (let k = 1; k <= n; k++) deck.push(next(hole[(button + k) % n]?.[round]));
  }
  deck.push(spare.shift(), next(board[0]), next(board[1]), next(board[2]));
  deck.push(spare.shift(), next(board[3]));
  deck.push(spare.shift(), next(board[4]));
  deck.push(...spare);

  const hand = new HoldemHand({
    players: stacks.map((stack, i) => ({ playerId: `p${i}`, stack })),
    buttonIndex: button,
    smallBlind: sb,
    bigBlind: bb,
    deck,
  });
  hand.start();
  return hand;
}

/** Apply a list of [playerId, action, amount?] and fail loudly if one is rejected. */
function play(hand, steps) {
  for (const [playerId, action, amount] of steps) {
    const result = hand.applyAction(playerId, action, amount);
    if (!result.ok) throw new Error(`${playerId} ${action} ${amount ?? ''} rejected: ${result.error}`);
  }
}

const totalChips = (hand) => hand.players.reduce((sum, p) => sum + p.stack + p.totalBet * (hand.isComplete ? 0 : 1), 0);

module.exports = { makeHand, play, totalChips };
