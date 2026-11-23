const test = require('node:test');
const assert = require('node:assert/strict');
const wallet = require('../src/services/ChipWallet');
const Table = require('../src/engine/Table');

const newTable = () => new Table({ code: 'W1', name: 'w', maxSeats: 3, smallBlind: 10, bigBlind: 20, startingChips: 1000 });

test('a player who leaves with 710 chips sits down again with 710', () => {
  wallet.clear();
  const table = newTable();
  table.addPlayer({ playerId: 'a', name: 'A', chips: wallet.stackFor('a', 1000) });
  table.getSeat('a').chips = 710;
  wallet.save('a', table.removePlayer('a'));
  assert.equal(wallet.stackFor('a', 1000), 710);
});

test('a new or busted player gets the starting stack', () => {
  wallet.clear();
  assert.equal(wallet.stackFor('new', 1000), 1000);
  wallet.save('broke', 0);
  assert.equal(wallet.stackFor('broke', 1000), 1000);
});
