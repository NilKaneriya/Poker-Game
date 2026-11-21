const test = require('node:test');
const assert = require('node:assert/strict');
const Table = require('../src/engine/Table');
const { TABLE_MODES } = require('../src/engine/constants');
const { runnerFor, removeRunner } = require('../src/network/TableRunner');

// The runner only needs io to broadcast; these tests call manageBots() directly.
const fakeIo = { to: () => ({ emit() {} }), emit() {} };

function botsTable(code, maxSeats = 4) {
  const table = new Table({
    code, name: 'Bots', mode: TABLE_MODES.BOTS, maxSeats, smallBlind: 10, bigBlind: 20, startingChips: 1000,
  });
  table.addPlayer({ playerId: 'host', name: 'Host' });
  const runner = runnerFor(fakeIo, table);
  runner.manageBots(); // fills the empty seats with bots
  return { table, runner };
}

test('a waiting player is a member of the table but has no seat', () => {
  const { table } = botsTable('WAIT1');
  table.addWaiting({ playerId: 'friend', name: 'Friend' });
  assert.equal(table.findSeat('friend'), -1);
  assert.ok(table.isWaiting('friend'));
  assert.ok(table.hasMember('friend'));
  assert.equal(table.humanCount, 2);
  table.addWaiting({ playerId: 'friend', name: 'Friend' }); // joining twice does not duplicate
  assert.equal(table.waiting.length, 1);
  removeRunner('WAIT1');
});

test('a waiting player is not dealt into the running hand', () => {
  const { table } = botsTable('WAIT2');
  table.startHand();
  table.addWaiting({ playerId: 'friend', name: 'Friend' });
  assert.equal(table.isDealtIn('friend'), false);
  removeRunner('WAIT2');
});

test('between hands a bot stands up and the waiting player takes its seat', () => {
  const { table, runner } = botsTable('WAIT3');
  assert.equal(table.players.filter((p) => p.isBot).length, 3);
  table.addWaiting({ playerId: 'friend', name: 'Friend' });

  runner.manageBots();

  assert.notEqual(table.findSeat('friend'), -1);
  assert.equal(table.isWaiting('friend'), false);
  assert.equal(table.players.length, 4);
  assert.equal(table.players.filter((p) => p.isBot).length, 2);
  assert.equal(runner.bots.size, 2); // the runner forgot the removed bot
  removeRunner('WAIT3');
});

test('the bot with the fewest chips is the one that stands up', () => {
  const { table, runner } = botsTable('WAIT4');
  const shortBot = table.players.find((p) => p.isBot);
  shortBot.chips = 150;
  table.addWaiting({ playerId: 'friend', name: 'Friend' });
  runner.manageBots();
  assert.equal(table.findSeat(shortBot.playerId), -1);
  removeRunner('WAIT4');
});

test('a waiting player who leaves is taken off the list', () => {
  const { table } = botsTable('WAIT5');
  table.addWaiting({ playerId: 'friend', name: 'Friend' });
  table.removePlayer('friend');
  assert.equal(table.hasMember('friend'), false);
  assert.equal(table.humanCount, 1);
  removeRunner('WAIT5');
});

test('connection state is tracked for waiting players too', () => {
  const { table, runner } = botsTable('WAIT6');
  table.addWaiting({ playerId: 'friend', name: 'Friend' });
  table.setConnected('friend', false);
  assert.equal(table.isConnected('friend'), false);
  runner.manageBots();
  assert.equal(table.getSeat('friend').connected, false); // carried over to the seat
  removeRunner('WAIT6');
});
