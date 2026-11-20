const test = require('node:test');
const assert = require('node:assert/strict');
const HoldemHand = require('../src/engine/HoldemHand');
const { STAGES } = require('../src/engine/constants');
const { makeHand, play, totalChips } = require('./helpers');

const stacksOf = (hand) => hand.players.map((p) => p.stack);

// ---------------------------------------------------------------------------
// Blinds, dealing, turn order
// ---------------------------------------------------------------------------

test('3 players: blinds left of the button, first to act after the big blind', () => {
  const hand = makeHand({ stacks: [1000, 1000, 1000], button: 0 });
  assert.equal(hand.stage, STAGES.PREFLOP);
  assert.deepEqual(hand.players.map((p) => p.bet), [0, 10, 20]); // p1 small blind, p2 big blind
  assert.equal(hand.pot, 30);
  assert.equal(hand.currentPlayerId, 'p0');
  assert.ok(hand.players.every((p) => p.holeCards.length === 2));
  const dealt = [...hand.players.flatMap((p) => p.holeCards)];
  assert.equal(new Set(dealt).size, 6);
});

test('heads-up: the button posts the small blind and acts first pre-flop, last after', () => {
  const hand = makeHand({ stacks: [1000, 1000], button: 0 });
  assert.deepEqual(hand.players.map((p) => p.bet), [10, 20]);
  assert.equal(hand.currentPlayerId, 'p0');
  play(hand, [['p0', 'call'], ['p1', 'check']]);
  assert.equal(hand.stage, STAGES.FLOP);
  assert.equal(hand.currentPlayerId, 'p1'); // big blind first after the flop
});

test('big blind gets an option to raise when everyone just calls', () => {
  const hand = makeHand({ stacks: [1000, 1000, 1000] });
  play(hand, [['p0', 'call'], ['p1', 'call']]);
  assert.equal(hand.currentPlayerId, 'p2');
  assert.deepEqual(hand.getLegalActions('p2').actions, ['fold', 'check', 'raise', 'allIn']);
  play(hand, [['p2', 'check']]);
  assert.equal(hand.stage, STAGES.FLOP);
  assert.equal(hand.board.length, 3);
  assert.equal(hand.currentPlayerId, 'p1'); // first left of the button
});

test('streets go flop, turn, river, showdown with 3, 4, 5 board cards', () => {
  const hand = makeHand({ stacks: [1000, 1000, 1000] });
  play(hand, [['p0', 'call'], ['p1', 'call'], ['p2', 'check']]);
  const seen = [];
  while (!hand.isComplete) {
    seen.push([hand.stage, hand.board.length]);
    play(hand, [['p1', 'check'], ['p2', 'check'], ['p0', 'check']]);
  }
  assert.deepEqual(seen, [[STAGES.FLOP, 3], [STAGES.TURN, 4], [STAGES.RIVER, 5]]);
  assert.equal(hand.stage, STAGES.SHOWDOWN);
});

// ---------------------------------------------------------------------------
// Rejecting illegal actions (anti-cheat lives here)
// ---------------------------------------------------------------------------

test('actions out of turn or against the rules are rejected', () => {
  const hand = makeHand({ stacks: [1000, 1000, 1000] });
  assert.equal(hand.applyAction('p1', 'call').ok, false);            // not p1's turn
  assert.equal(hand.applyAction('p0', 'check').ok, false);           // facing the big blind
  assert.equal(hand.applyAction('p0', 'raise').ok, false);           // no amount
  assert.equal(hand.applyAction('p0', 'raise', 25.5).ok, false);     // not a whole number
  assert.equal(hand.applyAction('p0', 'raise', 5000).ok, false);     // more than the stack
  assert.equal(hand.applyAction('p0', 'dance').ok, false);           // unknown action
  assert.equal(hand.applyAction('ghost', 'fold').ok, false);         // not at the table
  assert.equal(hand.currentPlayerId, 'p0');                          // nothing changed
  assert.equal(hand.pot, 30);
});

test('minimum bet is the big blind; minimum raise is the last raise size', () => {
  const hand = makeHand({ stacks: [1000, 1000, 1000] });
  assert.equal(hand.getLegalActions('p0').minRaiseTo, 40);           // 20 + 20
  assert.equal(hand.applyAction('p0', 'raise', 30).ok, false);       // raise of 10 < 20
  play(hand, [['p0', 'raise', 60]]);                                 // raise of 40
  assert.equal(hand.getLegalActions('p1').minRaiseTo, 100);          // 60 + 40
  assert.equal(hand.applyAction('p1', 'raise', 90).ok, false);
  play(hand, [['p1', 'call'], ['p2', 'call']]);

  // New street: opening bet must be at least the big blind.
  assert.equal(hand.getLegalActions('p1').minRaiseTo, 20);
  assert.equal(hand.applyAction('p1', 'raise', 10).ok, false);
  play(hand, [['p1', 'raise', 20]]);
});

test('not enough chips to call: a call becomes an all-in automatically', () => {
  const hand = makeHand({ stacks: [1000, 100, 1000] });
  play(hand, [['p0', 'raise', 300]]);
  assert.deepEqual(hand.getLegalActions('p1').actions, ['fold', 'allIn']);
  play(hand, [['p1', 'call']]);                                      // client says call
  assert.equal(hand.getPlayer('p1').allIn, true);
  assert.equal(hand.getPlayer('p1').bet, 100);
  assert.equal(hand.getPlayer('p1').lastAction, 'allIn');
});

test('an all-in smaller than a full raise does not reopen betting', () => {
  // p2 is the big blind with only 70 chips.
  const hand = makeHand({ stacks: [1000, 1000, 70] });
  play(hand, [['p0', 'raise', 60], ['p1', 'call'], ['p2', 'allIn']]); // 70: a raise of only 10
  // p0 and p1 must match the extra 10 but may not re-raise.
  assert.equal(hand.currentPlayerId, 'p0');
  assert.deepEqual(hand.getLegalActions('p0').actions, ['fold', 'call']);
  play(hand, [['p0', 'call']]);
  assert.deepEqual(hand.getLegalActions('p1').actions, ['fold', 'call']);
  play(hand, [['p1', 'call']]);
  assert.equal(hand.stage, STAGES.FLOP);
});

test('a full raise reopens betting for players who already acted', () => {
  const hand = makeHand({ stacks: [1000, 1000, 1000] });
  play(hand, [['p0', 'raise', 60], ['p1', 'call'], ['p2', 'raise', 200]]);
  assert.ok(hand.getLegalActions('p0').actions.includes('raise'));
});

test('no raising when everyone else is already all-in', () => {
  const hand = makeHand({ stacks: [1000, 50] });
  play(hand, [['p0', 'call'], ['p1', 'allIn']]);
  assert.deepEqual(hand.getLegalActions('p0').actions, ['fold', 'call']);
});

// ---------------------------------------------------------------------------
// Ending the hand
// ---------------------------------------------------------------------------

test('everyone folds to the big blind: they win and the uncalled part comes back', () => {
  const hand = makeHand({ stacks: [1000, 1000, 1000] });
  play(hand, [['p0', 'fold'], ['p1', 'fold']]);
  assert.ok(hand.isComplete);
  assert.deepEqual(stacksOf(hand), [1000, 990, 1010]);
  assert.deepEqual(hand.result.winners, [{ playerId: 'p2', amount: 20, handName: '', bestCards: [] }]);
  assert.deepEqual(hand.result.shownHands, {}); // nobody has to show
});

test('showdown: best hand wins the pot and hands are revealed', () => {
  const hand = makeHand({
    stacks: [1000, 1000, 1000],
    hole: [['Ah', 'Ad'], ['Kh', 'Kd'], ['7c', '2d']],
    board: ['As', '9c', '5h', '3s', 'Jd'],
  });
  play(hand, [['p0', 'call'], ['p1', 'call'], ['p2', 'check']]);
  for (let street = 0; street < 3; street++) play(hand, [['p1', 'check'], ['p2', 'check'], ['p0', 'check']]);

  assert.equal(hand.result.winners[0].playerId, 'p0');
  assert.equal(hand.result.winners[0].handName, 'Three of a Kind');
  assert.equal(hand.result.winners[0].amount, 60);
  assert.deepEqual(stacksOf(hand), [1040, 980, 980]);
  assert.deepEqual(Object.keys(hand.result.shownHands).sort(), ['p0', 'p1', 'p2']);
});

test('all-in pre-flop deals the rest of the board automatically', () => {
  const hand = makeHand({
    stacks: [500, 500],
    hole: [['Ah', 'Kh'], ['Qc', 'Qd']],
    board: ['2h', '7h', '9h', '3c', '4d'], // ace-high flush for p0
  });
  play(hand, [['p0', 'allIn'], ['p1', 'call']]);
  assert.ok(hand.isComplete);
  assert.equal(hand.board.length, 5);
  assert.equal(hand.stage, STAGES.SHOWDOWN);
  assert.deepEqual(stacksOf(hand), [1000, 0]);
  assert.equal(hand.result.winners[0].handName, 'Flush');
});

test('side pots end-to-end: short stack wins the main pot, side pot to the next best', () => {
  const hand = makeHand({
    stacks: [100, 300, 300],
    hole: [['Ah', 'Ad'], ['Kh', 'Kd'], ['Qh', 'Qd']],
    board: ['2c', '7s', '9d', 'Jc', '3h'],
  });
  play(hand, [['p0', 'allIn'], ['p1', 'allIn'], ['p2', 'call']]);
  assert.ok(hand.isComplete);
  assert.deepEqual(hand.result.pots.map((p) => p.amount), [300, 400]);
  assert.deepEqual(stacksOf(hand), [300, 400, 0]);
});

test('uncalled part of an all-in is returned to the bigger stack', () => {
  const hand = makeHand({ stacks: [1000, 200], hole: [['2c', '3d'], ['Ah', 'Ad']], board: ['Ks', '9h', '7d', '5c', 'Jh'] });
  play(hand, [['p0', 'allIn'], ['p1', 'call']]); // p1 can only cover 200
  assert.deepEqual(hand.result.pots.map((p) => p.amount), [400]);
  assert.deepEqual(stacksOf(hand), [800, 400]);
});

test('split pot: odd chip goes to the first winner left of the button', () => {
  // Blinds 5/10, p1 (small blind) folds after posting: pot is 25 = odd.
  const hand = makeHand({
    stacks: [1000, 1000, 1000],
    sb: 5,
    bb: 10,
    hole: [['2c', '3d'], ['9s', '9h'], ['2d', '3c']],
    board: ['Ah', 'Kh', 'Qh', 'Jh', 'Th'], // royal flush on the board: p0 and p2 tie
  });
  play(hand, [['p0', 'call'], ['p1', 'fold'], ['p2', 'check']]);
  for (let street = 0; street < 3; street++) play(hand, [['p2', 'check'], ['p0', 'check']]);

  const shares = Object.fromEntries(hand.result.winners.map((w) => [w.playerId, w.amount]));
  assert.deepEqual(shares, { p2: 13, p0: 12 }); // p2 sits closer to the left of the button (p0)
});

// ---------------------------------------------------------------------------
// Timeouts and players leaving
// ---------------------------------------------------------------------------

test('timeout folds when facing a bet and checks when checking is free', () => {
  const hand = makeHand({ stacks: [1000, 1000, 1000] });
  hand.actOnTimeout('p0');
  assert.equal(hand.getPlayer('p0').folded, true);

  play(hand, [['p1', 'call']]);
  hand.actOnTimeout('p2'); // big blind can check
  assert.equal(hand.getPlayer('p2').folded, false);
  assert.equal(hand.getPlayer('p2').lastAction, 'check');
  assert.equal(hand.stage, STAGES.FLOP);
});

test('a player leaving mid-hand is folded; the turn moves on if it was theirs', () => {
  const hand = makeHand({ stacks: [1000, 1000, 1000] });
  hand.foldOutOfTurn('p2');                 // not their turn
  assert.equal(hand.currentPlayerId, 'p0');
  hand.foldOutOfTurn('p0');                 // their turn
  assert.ok(hand.isComplete);               // only p1 left
  assert.equal(hand.result.winners[0].playerId, 'p1');
});

// ---------------------------------------------------------------------------
// Invariants over many random hands
// ---------------------------------------------------------------------------

test('300 random hands: chips are conserved, stacks never go negative, hands always finish', () => {
  let seed = 12345;
  const random = () => (seed = (seed * 1103515245 + 12345) % 2147483648) / 2147483648;
  const randInt = (n) => Math.floor(random() * n);

  for (let h = 0; h < 300; h++) {
    const n = 2 + randInt(5);
    const stacks = Array.from({ length: n }, () => 1 + randInt(1500));
    const hand = new HoldemHand({
      players: stacks.map((stack, i) => ({ playerId: `p${i}`, stack })),
      buttonIndex: randInt(n),
      smallBlind: 10,
      bigBlind: 20,
    });
    const before = stacks.reduce((a, b) => a + b, 0);
    hand.start();

    let guard = 0;
    while (!hand.isComplete) {
      assert.ok(++guard < 200, 'hand did not finish');
      const id = hand.currentPlayerId;
      const legal = hand.getLegalActions(id);
      const action = legal.actions[randInt(legal.actions.length)];
      const amount = action === 'raise' ? legal.minRaiseTo + randInt(legal.maxRaiseTo - legal.minRaiseTo + 1) : undefined;
      assert.ok(hand.applyAction(id, action, amount).ok, `${action} should be legal`);
      assert.equal(totalChips(hand), before);
    }

    const after = hand.players.reduce((sum, p) => sum + p.stack, 0);
    assert.equal(after, before, `hand ${h}: chips changed`);
    assert.ok(hand.players.every((p) => p.stack >= 0));
    if (hand.stage === STAGES.SHOWDOWN) assert.equal(hand.board.length, 5);
  }
});
