const test = require('node:test');
const assert = require('node:assert/strict');
const Table = require('../src/engine/Table');

function makeTable(names = ['ann', 'bob', 'cat']) {
  const table = new Table({ code: 'TEST1', name: 'Test', maxSeats: 6, smallBlind: 10, bigBlind: 20, startingChips: 1000 });
  for (const name of names) table.addPlayer({ playerId: name, name });
  return table;
}

/** Everyone folds to the big blind so the hand ends quickly. */
function foldAround(table) {
  while (!table.hand.isComplete) table.hand.applyAction(table.hand.currentPlayerId, 'fold');
  table.syncChips();
  return table.finishHand();
}

test('players fill free seats; a full table rejects more', () => {
  const table = makeTable(['a', 'b', 'c', 'd', 'e', 'f']);
  assert.equal(table.addPlayer({ playerId: 'g', name: 'g' }), -1);
  assert.equal(table.addPlayer({ playerId: 'a', name: 'a' }), 0); // already seated: same seat
});

test('needs two players with chips to start', () => {
  const table = makeTable(['solo']);
  assert.equal(table.canStartHand(), false);
  assert.throws(() => table.startHand());
});

test('dealer button moves one seat clockwise each hand', () => {
  const table = makeTable();
  const buttons = [];
  for (let i = 0; i < 4; i++) {
    table.startHand();
    buttons.push(table.buttonSeat);
    foldAround(table);
  }
  assert.deepEqual(buttons, [0, 1, 2, 0]);
  assert.equal(table.handNumber, 4);
});

test('button skips empty seats and players with no chips', () => {
  const table = makeTable(['a', 'b', 'c', 'd']);
  table.removePlayer('b');                 // seat 1 empty
  table.getSeat('c').chips = 0;            // seat 2 busted
  table.startHand();
  assert.equal(table.buttonSeat, 0);
  foldAround(table);
  table.startHand();
  assert.equal(table.buttonSeat, 3);       // skipped seats 1 and 2
  assert.equal(table.isDealtIn('c'), false); // busted player watches
});

test('seat chips follow the hand and settle when it ends', () => {
  const table = makeTable();
  table.startHand(); // button 0: ann; bob small blind, cat big blind
  assert.equal(table.getSeat('bob').chips, 990);
  assert.equal(table.getSeat('cat').chips, 980);
  const result = foldAround(table);
  assert.equal(result.winners[0].playerId, 'cat');
  assert.deepEqual(table.players.map((p) => p.chips), [1000, 990, 1010]);
  assert.equal(table.hand, null);
  assert.ok(table.lastHand.isComplete);
});

test('leaving mid-hand folds the player and keeps their bet in the pot', () => {
  const table = makeTable();
  table.startHand();
  table.removePlayer('bob');               // small blind leaves
  assert.equal(table.findSeat('bob'), -1);
  assert.equal(table.hand.getPlayer('bob').folded, true);
  assert.equal(table.hand.pot, 30);        // his 10 is still in the pot
});

test('a player joining mid-hand waits for the next hand', () => {
  const table = makeTable(['a', 'b']);
  table.startHand();
  table.addPlayer({ playerId: 'late', name: 'late' });
  assert.equal(table.isDealtIn('late'), false);
  foldAround(table);
  table.startHand();
  assert.equal(table.isDealtIn('late'), true);
});

test('rebuy only when out of chips and not in a running hand', () => {
  const table = makeTable(['a', 'b']);
  assert.equal(table.rebuy('a').ok, false);           // still has chips
  table.getSeat('a').chips = 0;
  assert.deepEqual(table.rebuy('a'), { ok: true });
  assert.equal(table.getSeat('a').chips, 1000);
  assert.equal(table.rebuy('nobody').ok, false);
});

test('public snapshot never contains hole cards', () => {
  const table = makeTable();
  table.startHand();
  const json = JSON.stringify(table.toPublic());
  for (const p of table.hand.players) {
    for (const card of p.holeCards) assert.ok(!json.includes(`"${card}"`));
  }
});
