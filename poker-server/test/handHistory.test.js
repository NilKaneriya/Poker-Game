const test = require('node:test');
const assert = require('node:assert/strict');
const { makeHand, play } = require('./helpers');
const { buildHandRecord } = require('../src/services/handRecord');
const { HandHistoryService, KEEP } = require('../src/services/HandHistoryService');
const { createStorage } = require('../src/storage');
const Table = require('../src/engine/Table');

const TABLE = { code: 'ABCDE', name: 'Test table' };
const INFO = {
  handNumber: 7,
  startedAt: 1700000000000,
  buttonSeat: 0,
  seats: [
    { seat: 0, playerId: 'p0', name: 'Ann', isBot: false },
    { seat: 1, playerId: 'p1', name: 'Bob', isBot: false },
    { seat: 2, playerId: 'p2', name: 'Cat', isBot: true },
  ],
};

/** Button p0, p1 small blind, p2 big blind. Ann flops top set and wins at showdown. */
function showdownHand() {
  const hand = makeHand({
    stacks: [1000, 1000, 1000],
    hole: [
      ['Ah', 'Ad'],
      ['Ks', 'Kc'],
      ['2c', '7d'],
    ],
    board: ['Ac', 'Kd', '5h', '9s', '3c'],
  });
  hand.info = INFO;
  play(hand, [
    ['p0', 'raise', 60],
    ['p1', 'call'],
    ['p2', 'fold'], //   pre-flop
    ['p1', 'check'],
    ['p0', 'raise', 100],
    ['p1', 'call'], // flop
    ['p1', 'check'],
    ['p0', 'check'], //                       turn
    ['p1', 'check'],
    ['p0', 'check'], //                       river
  ]);
  return hand;
}

const summary = (street) => street.actions.map((a) => `${a.name} ${a.kind} ${a.amount}`);

test('hand log: blinds, every action and the bet/raise difference, street by street', () => {
  const record = buildHandRecord(TABLE, showdownHand(), 'p0');
  assert.deepEqual(
    record.streets.map((s) => s.stage),
    ['preflop', 'flop', 'turn', 'river'],
  );
  assert.deepEqual(summary(record.streets[0]), ['Bob sb 10', 'Cat bb 20', 'Ann raise 60', 'Bob call 50', 'Cat fold 0']);
  assert.deepEqual(summary(record.streets[1]), ['Bob check 0', 'Ann bet 100', 'Bob call 100']);
  assert.deepEqual(record.streets[1].cards, ['Ac', 'Kd', '5h']);
  assert.deepEqual(record.streets[2].cards, ['9s']);
  assert.deepEqual(record.streets[3].cards, ['3c']);
  assert.deepEqual(record.board, ['Ac', 'Kd', '5h', '9s', '3c']);
});

test('hand record: chips won and lost per player, winners with their hand', () => {
  const record = buildHandRecord(TABLE, showdownHand(), 'p0');
  assert.equal(record.pot, 340);
  assert.deepEqual(
    record.players.map((p) => [p.name, p.net]),
    [
      ['Ann', 180],
      ['Bob', -160],
      ['Cat', -20],
    ],
  );
  assert.deepEqual(record.winners, [{ playerId: 'p0', name: 'Ann', amount: 340, handName: 'Three of a Kind' }]);
  assert.deepEqual(record.you, { cards: ['Ah', 'Ad'], net: 180, folded: false, handName: 'Three of a Kind' });
  assert.equal(record.players[0].isButton, true);
  assert.equal(record.players[2].isBot, true);
  assert.equal(record.id, 'ABCDE-7-1700000000000');
});

test('hand record: you see your own cards and cards shown at showdown, never folded cards', () => {
  const record = buildHandRecord(TABLE, showdownHand(), 'p1');
  const cards = Object.fromEntries(record.players.map((p) => [p.name, p.cards]));
  assert.deepEqual(cards, { Ann: ['Ah', 'Ad'], Bob: ['Ks', 'Kc'], Cat: [] });

  const catView = buildHandRecord(TABLE, showdownHand(), 'p2');
  assert.deepEqual(catView.you.cards, ['2c', '7d']);
  assert.equal(catView.you.folded, true);
});

test("hand record: won without showdown: nobody else's cards, the uncalled bet comes back", () => {
  const hand = makeHand({
    stacks: [1000, 1000, 1000],
    hole: [
      ['Ah', 'Ad'],
      ['Ks', 'Kc'],
      ['2c', '7d'],
    ],
  });
  hand.info = INFO;
  play(hand, [
    ['p0', 'raise', 60],
    ['p1', 'fold'],
    ['p2', 'fold'],
  ]);

  const record = buildHandRecord(TABLE, hand, 'p1');
  assert.deepEqual(
    record.streets.map((s) => s.stage),
    ['preflop'],
  );
  assert.deepEqual(summary(record.streets[0]).slice(-1), ['Ann returned 40']);
  assert.deepEqual(record.players.find((p) => p.name === 'Ann').cards, []);
  assert.deepEqual(
    record.winners.map((w) => [w.name, w.amount, w.handName]),
    [['Ann', 50, '']],
  );
  assert.deepEqual(
    record.players.map((p) => p.net),
    [30, -10, -20],
  );
});

test('hand log: timeouts and players who left are marked', () => {
  const hand = makeHand({ stacks: [1000, 1000, 1000] });
  hand.info = INFO;
  hand.actOnTimeout('p0'); // has to call 20: folds
  hand.foldOutOfTurn('p2');
  const actions = buildHandRecord(TABLE, hand, 'p1').streets[0].actions;
  assert.deepEqual(
    actions.find((a) => a.name === 'Ann'),
    { playerId: 'p0', name: 'Ann', kind: 'fold', amount: 0, total: 0, timeout: true },
  );
  assert.equal(actions.find((a) => a.name === 'Cat' && a.kind === 'fold').left, true);
});

test('all-in pre-flop: the rest of the board is dealt with no actions on later streets', () => {
  const hand = makeHand({
    stacks: [500, 500],
    button: 0,
    hole: [
      ['Ah', 'Ad'],
      ['Ks', 'Kc'],
    ],
  });
  hand.info = { ...INFO, seats: INFO.seats.slice(0, 2) };
  play(hand, [
    ['p0', 'allIn'],
    ['p1', 'call'],
  ]);
  const record = buildHandRecord(TABLE, hand, 'p0');
  assert.deepEqual(
    record.streets.map((s) => [s.stage, s.cards.length, s.actions.length]),
    [
      ['preflop', 0, 4],
      ['flop', 3, 0],
      ['turn', 1, 0],
      ['river', 1, 0],
    ],
  );
  assert.equal(record.streets[0].actions[2].kind, 'allIn');
});

test('table: each hand knows its number, button and who was dealt in', () => {
  const table = new Table({ code: 'T', name: 'T', maxSeats: 3, smallBlind: 10, bigBlind: 20, startingChips: 1000 });
  table.addPlayer({ playerId: 'a', name: 'Ann' });
  table.addPlayer({ playerId: 'b', name: 'Bob', isBot: true });
  const hand = table.startHand();
  assert.equal(hand.info.handNumber, 1);
  assert.equal(hand.info.buttonSeat, table.buttonSeat);
  assert.deepEqual(
    hand.info.seats.map((s) => [s.name, s.isBot]),
    [
      ['Ann', false],
      ['Bob', true],
    ],
  );
});

test('history service: per player, newest first, only the last hands are kept', async () => {
  const storage = await createStorage({});
  const history = new HandHistoryService().attach(storage);
  for (let n = 1; n <= KEEP + 5; n++) {
    const hand = showdownHand();
    hand.info = { ...INFO, handNumber: n };
    await history.recordHand(TABLE, hand, ['p0', 'p1']);
  }
  const ann = await history.list('p0');
  assert.equal(ann.length, KEEP);
  assert.equal(ann[0].handNumber, KEEP + 5);
  assert.equal(ann[0].you.net, 180);
  assert.equal((await history.list('p1'))[0].you.net, -160);
  assert.deepEqual(await history.list('nobody'), []);
});
