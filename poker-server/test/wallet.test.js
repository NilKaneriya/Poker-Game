const test = require('node:test');
const assert = require('node:assert/strict');
const config = require('../src/config');
const { WalletService } = require('../src/services/WalletService');
const { ITEMS, findItem } = require('../src/services/shopCatalog');
const { storageBackends } = require('./storageBackends');

const { startingBalance, minBuyIn, maxBalance } = config.economy;
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

/** A wallet service on a fresh storage. cooldown 0 unless a test is about the cooldown. */
async function open(backend, { slow = false } = {}) {
  const storage = await backend.open();
  if (slow) {
    // Real waiting on every call, like a database round trip: exposes any race in the service.
    const real = storage.wallets;
    storage.wallets = {
      get: async (id) => (await sleep(1 + Math.random() * 4), real.get(id)),
      set: async (id, doc) => (await sleep(1 + Math.random() * 4), real.set(id, doc)),
    };
  }
  const service = new WalletService().attach(storage);
  return { service, storage };
}

const BAD_AMOUNTS = [
  0,
  -1,
  -1000,
  1.5,
  NaN,
  Infinity,
  -Infinity,
  '1000',
  '',
  null,
  undefined,
  {},
  [],
  [1000],
  true,
  2 ** 60,
  1e21,
];

for (const backend of storageBackends()) {
  const t = (name, fn) => test(`wallet: ${name} (${backend.name})`, () => fn(backend));

  t('a new account starts with the starting balance and nothing at a table', async (b) => {
    const { service } = await open(b);
    assert.deepEqual(await service.get('alice'), { balance: startingBalance, inPlay: 0 });
    assert.equal(startingBalance, 10000);
  });

  t('the wallet is saved: a second service on the same storage sees it', async (b) => {
    const { service, storage } = await open(b);
    await service.buyIn('alice', 1000);
    const other = new WalletService().attach(storage);
    assert.deepEqual(await other.get('alice'), { balance: startingBalance - 1000, inPlay: 1000 });
  });

  t('wallets are separate per account', async (b) => {
    const { service } = await open(b);
    await service.buyIn('alice', 1000);
    assert.equal((await service.get('bob')).balance, startingBalance);
  });

  t('buy-in moves chips from the wallet to the table', async (b) => {
    const { service } = await open(b);
    const r = await service.buyIn('alice', 1000);
    assert.deepEqual([r.ok, r.amount, r.balance], [true, 1000, startingBalance - 1000]);
    assert.equal((await service.get('alice')).inPlay, 1000);
  });

  t('buy-in rejects every invalid amount and changes nothing', async (b) => {
    const { service } = await open(b);
    for (const bad of BAD_AMOUNTS) {
      const r = await service.buyIn('alice', bad);
      assert.equal(r.ok, false, `buyIn(${String(bad)}) must be rejected`);
    }
    assert.deepEqual(await service.get('alice'), { balance: startingBalance, inPlay: 0 });
  });

  t('buy-in takes what is left when the wallet holds less than asked, but not below the minimum', async (b) => {
    const { service } = await open(b);
    await service.buyIn('alice', startingBalance - 300); // 300 left
    const small = await service.buyIn('alice', 1000);
    assert.deepEqual([small.ok, small.amount, small.balance], [true, 300, 0]);

    const poor = await service.buyIn('bob', startingBalance - (minBuyIn - 1)); // 199 left
    assert.equal(poor.ok, true);
    const refused = await service.buyIn('bob', 1000);
    assert.equal(refused.ok, false);
    assert.match(refused.error, /at least 200/);
    assert.equal((await service.get('bob')).balance, minBuyIn - 1); // untouched by the refusal
  });

  t('exactly the minimum buy-in is allowed', async (b) => {
    const { service } = await open(b);
    await service.buyIn('alice', startingBalance - minBuyIn);
    const r = await service.buyIn('alice', 1000);
    assert.equal(r.ok, true);
    assert.equal(r.amount, minBuyIn);
  });

  t('the balance can never go negative: 50 parallel buy-ins spend each chip at most once', async (b) => {
    const { service } = await open(b, { slow: true });
    const results = await Promise.all(Array.from({ length: 50 }, () => service.buyIn('alice', 1000)));
    const wins = results.filter((r) => r.ok);
    assert.equal(wins.length, startingBalance / 1000);
    const w = await service.get('alice');
    assert.deepEqual(w, { balance: 0, inPlay: startingBalance });
  });

  t('cash-out returns chips, closes the table money and is recorded', async (b) => {
    const { service } = await open(b);
    await service.buyIn('alice', 1000);
    const r = await service.cashOut('alice', 1750, 'table X');
    assert.equal(r.balance, startingBalance - 1000 + 1750);
    assert.deepEqual(await service.get('alice'), { balance: startingBalance + 750, inPlay: 0 });
  });

  t('cash-out of 0 (busted) is fine and gives nothing', async (b) => {
    const { service } = await open(b);
    await service.buyIn('alice', 1000);
    await service.cashOut('alice', 0);
    assert.deepEqual(await service.get('alice'), { balance: startingBalance - 1000, inPlay: 0 });
  });

  t('cash-out rejects invalid amounts and changes nothing', async (b) => {
    const { service } = await open(b);
    await service.buyIn('alice', 1000);
    for (const bad of [-1, -1000, 1.5, NaN, Infinity, '500', null, undefined, {}, 2 ** 60, 1e21]) {
      assert.equal((await service.cashOut('alice', bad)).ok, false, `cashOut(${String(bad)})`);
    }
    assert.deepEqual(await service.get('alice'), { balance: startingBalance - 1000, inPlay: 1000 });
  });

  t('cash-out can never push the wallet past the ceiling', async (b) => {
    const { service } = await open(b);
    await service.cashOut('alice', maxBalance * 2);
    assert.equal((await service.get('alice')).balance, maxBalance);
  });

  t('a partial release (refund of an unused buy-in) keeps the other table money tracked', async (b) => {
    const { service } = await open(b);
    await service.buyIn('alice', 1000); // at a table
    await service.buyIn('alice', 500); //  a second, pending buy-in
    await service.cashOut('alice', 500, 'refund', 500);
    assert.deepEqual(await service.get('alice'), { balance: startingBalance - 1000, inPlay: 1000 });
  });

  t('reconcile gives chips back when the table is gone (server restart), once', async (b) => {
    const { service } = await open(b);
    await service.buyIn('alice', 1000);
    await service.reconcile('alice', false);
    assert.deepEqual(await service.get('alice'), { balance: startingBalance, inPlay: 0 });
    await service.reconcile('alice', false); // second time: nothing more to give
    assert.deepEqual(await service.get('alice'), { balance: startingBalance, inPlay: 0 });
  });

  t('reconcile does nothing while the player is still seated', async (b) => {
    const { service } = await open(b);
    await service.buyIn('alice', 1000);
    await service.reconcile('alice', true);
    assert.deepEqual(await service.get('alice'), { balance: startingBalance - 1000, inPlay: 1000 });
  });

  t('shop: a purchase adds exactly the catalog amount', async (b) => {
    const { service } = await open(b);
    for (const item of ITEMS) {
      service.lastPurchaseAt.clear();
      const before = (await service.get('alice')).balance;
      const r = await service.purchase('alice', item.id);
      assert.equal(r.ok, true);
      assert.equal(r.balance, before + item.chips);
    }
  });

  t('shop: unknown, malformed and prototype-key ids are refused and change nothing', async (b) => {
    const { service } = await open(b);
    const bad = [
      '',
      'nope',
      'CHIPS_SMALL',
      ' chips_small',
      'chips_small ',
      'constructor',
      '__proto__',
      'toString',
      'hasOwnProperty',
      '0',
      0,
      1,
      -1,
      null,
      undefined,
      {},
      [],
      ['chips_small'],
      { id: 'chips_small' },
      true,
      'chips_small\u0000',
      'x'.repeat(10000),
    ];
    for (const id of bad) {
      service.lastPurchaseAt.clear();
      assert.equal((await service.purchase('alice', id)).ok, false, `purchase(${String(id).slice(0, 20)})`);
    }
    assert.deepEqual(await service.get('alice'), { balance: startingBalance, inPlay: 0 });
  });

  t('shop: purchases are limited to one per cooldown, even 30 at the same instant', async (b) => {
    const { service } = await open(b, { slow: true });
    const results = await Promise.all(Array.from({ length: 30 }, () => service.purchase('alice', 'chips_small')));
    assert.equal(results.filter((r) => r.ok).length, 1);
    assert.equal((await service.get('alice')).balance, startingBalance + 5000);
    await sleep(config.economy.purchaseCooldownMs + 50);
    assert.equal((await service.purchase('alice', 'chips_small')).ok, true); // allowed again later
  });

  t('shop: the cooldown is per account', async (b) => {
    const { service } = await open(b);
    assert.equal((await service.purchase('alice', 'chips_small')).ok, true);
    assert.equal((await service.purchase('bob', 'chips_small')).ok, true);
  });

  t('shop: a full wallet refuses the purchase and keeps its balance', async (b) => {
    const { service } = await open(b);
    await service.cashOut('alice', maxBalance); // now at the ceiling
    const r = await service.purchase('alice', 'chips_small');
    assert.equal(r.ok, false);
    assert.equal((await service.get('alice')).balance, maxBalance);
  });

  t('the ledger keeps the most recent entries and a bounded size', async (b) => {
    const { service, storage } = await open(b);
    for (let i = 0; i < 50; i++) {
      await service.buyIn('alice', 200);
      await service.cashOut('alice', 200);
    }
    const doc = await storage.wallets.get('alice');
    assert.equal(doc.recent.length, config.economy.recentLedger);
    assert.deepEqual(Object.keys(doc.recent.at(-1)).sort(), ['amount', 'at', 'balanceAfter', 'ref', 'type']);
    assert.equal(doc.recent.at(-1).balanceAfter, doc.balance);
  });

  t('fuzz: after 400 random operations the balance is a whole number >= 0 and money is conserved', async (b) => {
    const { service } = await open(b, { slow: true });
    const ids = ['a', 'b', 'c'];
    const model = new Map(ids.map((id) => [id, { atTable: 0 }]));
    let seed = 12345;
    const rnd = (n) => (seed = (seed * 1103515245 + 12345) & 0x7fffffff) % n;
    for (let i = 0; i < 400; i++) {
      const id = ids[rnd(3)];
      const m = model.get(id);
      if (rnd(2) === 0 && m.atTable === 0) {
        const r = await service.buyIn(id, [200, 500, 1000, 5000][rnd(4)]);
        if (r.ok) m.atTable = r.amount;
      } else if (m.atTable > 0) {
        const result = rnd(3) === 0 ? 0 : m.atTable; // busted, or break even
        await service.cashOut(id, result);
        m.atTable = 0;
      }
      const w = await service.get(id);
      assert.ok(Number.isSafeInteger(w.balance) && w.balance >= 0);
      assert.equal(w.inPlay, m.atTable);
    }
  });

  t('the catalog cannot be changed at run time', async () => {
    assert.throws(() => {
      'use strict';
      ITEMS[0].chips = 999999999;
    });
    assert.throws(() => ITEMS.push({ id: 'free', chips: 1 }));
    assert.equal(findItem('chips_small').chips, 5000);
  });
}
