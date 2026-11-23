// The chip economy end to end: a real server, real Socket.IO clients, hostile inputs.
// Chips are checked in two places: the wallet (what the player owns) and the table (what is at stake).
const test = require('node:test');
const assert = require('node:assert/strict');
const { io } = require('socket.io-client');
const { TestServer, Player, sleep, until } = require('./economyHarness');

const START = 10000;
const BUY_IN = 1000;

let server; //  normal economy (10,000 start)
let poor; //    accounts start with only 150 chips
let restartable; // for the crash / restart test

test.before(async () => {
  server = await new TestServer(3211).start();
  poor = await new TestServer(3212, { STARTING_BALANCE: '150' }).start();
  restartable = await new TestServer(3213).start();
});

test.after(async () => {
  await Promise.all([server.stop(), poor.stop(), restartable.stop()]);
});

/** Two humans at a 2-seat friends table; the game starts as soon as both sit. */
async function headsUp() {
  const a = await (await Player.register(server, 'a')).connect();
  const b = await (await Player.register(server, 'b')).connect();
  const code = await a.createTable();
  await b.join(code);
  return { a, b, code };
}

const closeAll = (...players) => players.forEach((p) => p.close());

// ---------------------------------------------------------------------------
// Starting chips and the wallet <-> table transfer
// ---------------------------------------------------------------------------

test('a new account gets 10,000 chips, shown as soon as it connects', async () => {
  const p = await (await Player.register(server)).connect();
  assert.equal(p.wallet, START);
  assert.equal(p.session.tableCode, null);
  p.close();
});

test('the wallet survives reconnecting: same account, same balance', async () => {
  const p = await (await Player.register(server)).connect();
  await p.createTable();
  p.close();
  await sleep(100);
  const again = new Player(server, p.token, p.playerId, p.username);
  await again.connect();
  assert.equal(again.wallet, START - BUY_IN); // still seated (grace period), chips still at the table
  again.close();
});

test('sitting down charges the buy-in once and the seat holds exactly that', async () => {
  const p = await (await Player.register(server)).connect();
  await p.createTable();
  await until(() => p.wallet === START - BUY_IN, 3000, 'wallet after buy-in');
  assert.equal(p.myChips, BUY_IN);
  p.close();
});

test('leaving returns the stack to the wallet', async () => {
  const p = await (await Player.register(server)).connect();
  await p.createTable();
  p.emit('leaveTable');
  await until(() => p.wallet === START, 3000, 'wallet after leaving');
  p.close();
});

test('the client cannot choose its own buy-in or chips when sitting down', async () => {
  const a = await (await Player.register(server, 'h')).connect();
  const code = await a.createTable();
  const b = await (await Player.register(server, 'g')).connect();
  b.emit('joinTable', { code, chips: 999999, buyIn: 999999, startingChips: 999999, amount: 999999, balance: 999999 });
  await until(() => b.table?.you.seat >= 0, 3000, 'seat');
  assert.equal(b.myChips, BUY_IN);
  await until(() => b.wallet === START - BUY_IN, 3000, 'wallet');
  closeAll(a, b);
});

test('creating a table with forged options cannot change the stack either', async () => {
  const p = await (await Player.register(server)).connect();
  p.emit('createTable', { tableName: 'x', mode: 'friends', maxSeats: 2, startingChips: 5e9, chips: 5e9, buyIn: 5e9 });
  await until(() => p.table?.you.seat >= 0, 3000, 'seat');
  assert.equal(p.myChips, BUY_IN);
  p.close();
});

test('unknown events cannot touch the wallet (wallet, cashOut, setChips, grant, admin...)', async () => {
  const p = await (await Player.register(server)).connect();
  for (const ev of [
    'wallet',
    'setWallet',
    'cashOut',
    'buyIn',
    'setChips',
    'addChips',
    'grant',
    'admin',
    'credit',
    'refund',
    'deposit',
  ]) {
    p.emit(ev, { balance: 1e9, chips: 1e9, amount: 1e9, playerId: p.playerId });
  }
  await sleep(300);
  assert.equal(await p.refreshWallet(), START);
  p.close();
});

// ---------------------------------------------------------------------------
// Chips are conserved while playing
// ---------------------------------------------------------------------------

test('two players go all-in: after both leave, wallets add up to exactly 20,000', async () => {
  const { a, b } = await headsUp();
  a.autoPlay = b.autoPlay = true;
  await until(() => a.table?.hand?.result, 8000, 'a finished hand');
  a.autoPlay = b.autoPlay = false;
  a.emit('leaveTable');
  b.emit('leaveTable');
  await sleep(600);
  const total = (await a.refreshWallet()) + (await b.refreshWallet());
  assert.equal(total, 2 * START);
  closeAll(a, b);
});

test('leaving in the middle of a hand: the blind stays in the pot, nothing is created or lost', async () => {
  const { a, b } = await headsUp();
  await until(() => a.table?.hand, 3000, 'hand started');
  a.emit('leaveTable');
  await until(() => b.table?.hand?.result, 4000, 'b wins');
  b.emit('leaveTable');
  await sleep(600);
  const wa = await a.refreshWallet();
  const wb = await b.refreshWallet();
  assert.equal(wa + wb, 2 * START);
  assert.ok(wa < START && wa >= START - 20, `leaver lost only the blind she had posted (wallet ${wa})`);
  closeAll(a, b);
});

test('busted: rebuy is paid from the wallet, never free, and cannot be done twice', async () => {
  const { a, b } = await headsUp();
  a.autoPlay = b.autoPlay = true;
  // keep shoving until one player has no chips left (a tie just plays another hand)
  const loser = await until(() => [a, b].find((p) => p.table && !p.table.hand && p.myChips === 0), 30000, 'one player busted');
  a.autoPlay = b.autoPlay = false;
  const winner = loser === a ? b : a;
  assert.equal(loser.wallet, START - BUY_IN);

  loser.emit('rebuy');
  loser.emit('rebuy'); // double click
  loser.emit('rebuy');
  await until(() => loser.myChips > 0, 3000, 'rebuy applied');
  await sleep(300);
  assert.equal(await loser.refreshWallet(), START - 2 * BUY_IN); // charged exactly once
  // a new hand starts at once, so a blind may already be in the pot: the stack is the buy-in minus at most that
  assert.ok(loser.myChips > BUY_IN - 25 && loser.myChips <= BUY_IN, `stack ${loser.myChips}`);

  // now they have chips again: a further rebuy is refused and costs nothing
  loser.rejected.length = 0;
  loser.emit('rebuy');
  await until(() => loser.rejected.length > 0, 3000, 'rejection');
  assert.match(loser.rejected[0], /still have chips/);
  assert.equal(await loser.refreshWallet(), START - 2 * BUY_IN);

  a.emit('leaveTable');
  b.emit('leaveTable');
  await sleep(600);
  assert.equal((await a.refreshWallet()) + (await b.refreshWallet()), 2 * START);
  assert.ok(winner);
  closeAll(a, b);
});

test('rebuy with chips on the table, or away from a table, is refused and free', async () => {
  const p = await (await Player.register(server)).connect();
  p.emit('rebuy');
  await until(() => p.rejected.length === 1, 3000, 'not at a table');
  assert.match(p.rejected[0], /not at a table/);
  await p.createTable();
  p.emit('rebuy');
  await until(() => p.rejected.length === 2, 3000, 'still have chips');
  assert.match(p.rejected[1], /still have chips/);
  assert.equal(await p.refreshWallet(), START - BUY_IN);
  p.close();
});

// ---------------------------------------------------------------------------
// Races: double clicks, two tabs, two players for one seat
// ---------------------------------------------------------------------------

test('double-click on "create table": charged once, one seat', async () => {
  const p = await (await Player.register(server)).connect();
  for (let i = 0; i < 5; i++) p.emit('createTable', { tableName: 'dbl', mode: 'friends', maxSeats: 2 });
  await sleep(700);
  assert.equal(await p.refreshWallet(), START - BUY_IN);
  assert.equal(p.myChips, BUY_IN);
  p.close();
});

test('five quick "join" clicks: charged once', async () => {
  const host = await (await Player.register(server)).connect();
  const code = await host.createTable({ tableName: 'j', mode: 'friends', maxSeats: 3 });
  const p = await (await Player.register(server)).connect();
  for (let i = 0; i < 5; i++) p.emit('joinTable', { code });
  await sleep(700);
  assert.equal(await p.refreshWallet(), START - BUY_IN);
  assert.equal(p.myChips, BUY_IN);
  closeAll(host, p);
});

test('two players grab the last seat: one sits, the other is refunded in full', async () => {
  const host = await (await Player.register(server)).connect();
  const code = await host.createTable({ tableName: 'last', mode: 'friends', maxSeats: 2 });
  const x = await (await Player.register(server)).connect();
  const y = await (await Player.register(server)).connect();
  x.emit('joinTable', { code });
  y.emit('joinTable', { code });
  await sleep(900);
  const seated = [x, y].filter((p) => p.table?.you?.seat >= 0);
  const refused = [x, y].filter((p) => !(p.table?.you?.seat >= 0));
  assert.equal(seated.length, 1);
  assert.equal(refused.length, 1);
  assert.equal(await seated[0].refreshWallet(), START - BUY_IN);
  assert.equal(await refused[0].refreshWallet(), START); // nothing kept
  assert.ok(refused[0].errors.some((m) => /full/i.test(m)));
  closeAll(host, x, y);
});

test('the same account in a second tab signs the first out, and the chips are not duplicated', async () => {
  const first = await (await Player.register(server)).connect();
  await first.createTable();
  const second = new Player(server, first.token, first.playerId, first.username);
  await second.connect();
  await until(() => first.kicked, 3000, 'first tab told');
  assert.equal(second.wallet, START - BUY_IN);
  second.emit('createTable', { tableName: 'again', mode: 'friends', maxSeats: 2 }); // leaves the old table, sits at a new one
  await until(() => second.table?.you?.seat >= 0, 3000, 'new seat');
  await sleep(500);
  assert.equal(await second.refreshWallet(), START - BUY_IN); // back + out again: net one buy-in
  second.close();
});

test('moving between tables moves the stack with the player', async () => {
  const h1 = await (await Player.register(server)).connect();
  const h2 = await (await Player.register(server)).connect();
  const c1 = await h1.createTable({ tableName: 'one', mode: 'friends', maxSeats: 3 });
  const c2 = await h2.createTable({ tableName: 'two', mode: 'friends', maxSeats: 3 });
  const p = await (await Player.register(server)).connect();
  await p.join(c1);
  await p.join(c2);
  await p.join(c1);
  await sleep(700);
  assert.equal(await p.refreshWallet(), START - BUY_IN); // one stack at one table
  closeAll(h1, h2, p);
});

// ---------------------------------------------------------------------------
// Disconnects and crashes never lose or duplicate chips
// ---------------------------------------------------------------------------

test('a dropped connection keeps the seat during the grace period (no refund yet)', async () => {
  const p = await (await Player.register(server)).connect();
  await p.createTable();
  p.close();
  await sleep(300);
  const back = new Player(server, p.token, p.playerId, p.username);
  await back.connect();
  assert.equal(back.session.tableCode !== null, true);
  assert.equal(back.wallet, START - BUY_IN);
  back.close();
});

test('not coming back: after the grace period the seat is freed and the chips return', async () => {
  const p = await (await Player.register(server)).connect();
  await p.createTable();
  p.close();
  await sleep(2200); // server grace is 1.2 s in tests
  const back = new Player(server, p.token, p.playerId, p.username);
  await back.connect();
  assert.equal(back.session.tableCode, null);
  assert.equal(back.wallet, START);
  back.close();
});

test('waiting for a seat on a bots table: the buy-in is held, and returned if you leave', async () => {
  const host = await (await Player.register(server)).connect();
  const code = await host.createTable({ tableName: 'bots', mode: 'bots', maxSeats: 3 });
  await until(() => host.table?.hand, 4000, 'hand running');
  const friend = await (await Player.register(server)).connect();
  friend.emit('joinTable', { code });
  await until(() => friend.table?.you?.waiting, 3000, 'waiting');
  assert.equal(await friend.refreshWallet(), START - BUY_IN);
  friend.emit('leaveTable');
  await until(() => friend.wallet === START || false, 100).catch(() => {});
  await sleep(500);
  assert.equal(await friend.refreshWallet(), START);
  closeAll(host, friend);
});

test('server crash while seated: the chips come back on the next login, exactly once', async () => {
  const p = await (await Player.register(restartable)).connect();
  await p.createTable();
  assert.equal(p.wallet !== null, true);
  await until(() => p.wallet === START - BUY_IN, 3000, 'buy-in charged');
  await sleep(1500); // JSON storage saves in batches every second
  p.close();
  await restartable.kill(); // hard crash: tables in memory are gone
  await restartable.start();

  const back = new Player(restartable, p.token, p.playerId, p.username);
  await back.connect();
  assert.equal(back.session.tableCode, null);
  assert.equal(back.wallet, START); // refunded
  back.close();
  await sleep(100);
  const again = new Player(restartable, p.token, p.playerId, p.username);
  await again.connect();
  assert.equal(again.wallet, START); // and not refunded a second time
  again.close();
});

// ---------------------------------------------------------------------------
// Not enough chips
// ---------------------------------------------------------------------------

test('below the minimum buy-in: cannot sit, no table is left behind, nothing is charged', async () => {
  const p = await (await Player.register(poor)).connect();
  assert.equal(p.wallet, 150);
  p.emit('createTable', { tableName: 'x', mode: 'friends', maxSeats: 2 });
  await until(() => p.errors.length > 0, 3000, 'error');
  assert.match(p.errors[0], /at least 200/);
  assert.equal(await p.refreshWallet(), 150);
  const tables = await (await fetch(`${poor.url}/tables`)).json();
  assert.equal(tables.length, 0);
  p.close();
});

test('after buying chips in the shop the same player can sit, with the part of the wallet they have', async () => {
  const p = await (await Player.register(poor)).connect();
  p.emit('buyItem', { itemId: 'chips_small' });
  await until(() => p.wallet === 5150, 3000, 'wallet 5150');
  await p.createTable();
  await until(() => p.wallet === 4150, 3000, 'wallet 4150');
  assert.equal(p.myChips, BUY_IN);
  p.close();
});

// ---------------------------------------------------------------------------
// The shop
// ---------------------------------------------------------------------------

test('shop: every item adds exactly its chips, and says it is a demo', async () => {
  const p = await (await Player.register(server)).connect();
  p.emit('buyItem', { itemId: 'chips_small' });
  const r = await p.lastShop(1);
  assert.equal(r.ok, true);
  assert.match(r.message, /demo/);
  await until(() => p.wallet === START + 5000, 3000, 'wallet');
  await sleep(1100);
  p.emit('buyItem', { itemId: 'chips_medium' });
  await until(() => p.wallet === START + 5000 + 25000, 3000, 'wallet');
  await sleep(1100);
  p.emit('buyItem', { itemId: 'chips_large' });
  await until(() => p.wallet === START + 5000 + 25000 + 100000, 3000, 'wallet');
  p.close();
});

test('shop: the catalog is sent to the client, with prices as plain labels', async () => {
  const p = await (await Player.register(server)).connect();
  const catalog = await new Promise((resolve) => {
    p.socket.once('shopCatalog', resolve);
    p.emit('getWallet');
  });
  assert.deepEqual(
    catalog.items.map((i) => i.id),
    ['chips_small', 'chips_medium', 'chips_large'],
  );
  assert.ok(catalog.items.every((i) => /demo/.test(i.price)));
  p.close();
});

test('shop: forged purchases (own amount, price, quantity) only ever give the catalog amount', async () => {
  const p = await (await Player.register(server)).connect();
  p.emit('buyItem', {
    itemId: 'chips_small',
    chips: 999999999,
    amount: 999999999,
    price: 0,
    quantity: 1000,
    qty: 1000,
    free: true,
    balance: 1e9,
  });
  await until(() => p.wallet === START + 5000, 3000, 'wallet');
  await sleep(400);
  assert.equal(await p.refreshWallet(), START + 5000);
  p.close();
});

test('shop: made-up, malformed and prototype ids are refused with no change', async () => {
  const p = await (await Player.register(server)).connect();
  const bad = [
    undefined,
    null,
    0,
    -1,
    1,
    1e308,
    true,
    '',
    'nope',
    'constructor',
    '__proto__',
    'toString',
    'chips_huge',
    'CHIPS_SMALL',
    ' chips_small',
    'chips_small\n',
    ['chips_small'],
    { id: 'chips_small' },
    { $ne: '' },
    'x'.repeat(100000),
  ];
  let sent = 0;
  for (const itemId of bad) {
    sent++;
    p.emit('buyItem', { itemId });
    await sleep(30);
  }
  // payloads that are not objects at all
  for (const payload of [undefined, null, 'chips_small', 5, [], true]) {
    sent++;
    p.emit('buyItem', payload);
    await sleep(30);
  }
  await until(() => p.shop.length === sent, 5000, 'all answers');
  assert.ok(p.shop.every((r) => r.ok === false));
  assert.ok(p.shop.every((r) => String(r.itemId).length <= 40)); // long input is not echoed back
  assert.equal(await p.refreshWallet(), START);
  p.close();
});

test('shop: 50 requests in a burst buy once (cooldown), not 50 times', async () => {
  const p = await (await Player.register(server)).connect();
  for (let i = 0; i < 50; i++) p.emit('buyItem', { itemId: 'chips_large' });
  await until(() => p.shop.length === 50, 5000, '50 answers');
  assert.equal(p.shop.filter((r) => r.ok).length, 1);
  assert.equal(await p.refreshWallet(), START + 100000);
  p.close();
});

test('shop: the same burst from two tabs of one account still buys once', async () => {
  const p = await (await Player.register(server)).connect();
  const q = new Player(server, p.token, p.playerId, p.username);
  await q.connect();
  for (let i = 0; i < 20; i++) {
    p.emit('buyItem', { itemId: 'chips_small' });
    q.emit('buyItem', { itemId: 'chips_small' });
  }
  await sleep(800);
  assert.equal(await q.refreshWallet(), START + 5000);
  closeAll(p, q);
});

test('shop: buying while seated changes the wallet but not the stack on the table', async () => {
  const p = await (await Player.register(server)).connect();
  await p.createTable();
  p.emit('buyItem', { itemId: 'chips_small' });
  await until(() => p.wallet === START - BUY_IN + 5000, 3000, 'wallet');
  assert.equal(p.myChips, BUY_IN);
  p.close();
});

test('shop: accounts are independent (one player cannot spend or spoil another wallet)', async () => {
  const a = await (await Player.register(server)).connect();
  const b = await (await Player.register(server)).connect();
  a.emit('buyItem', { itemId: 'chips_small', playerId: b.playerId, userId: b.playerId, target: b.playerId });
  await until(() => a.wallet === START + 5000, 3000, 'a wallet');
  assert.equal(await b.refreshWallet(), START);
  closeAll(a, b);
});

// ---------------------------------------------------------------------------
// Access
// ---------------------------------------------------------------------------

test('no token, a made-up token, or a logged-out token cannot connect', async () => {
  const attempt = (auth) =>
    new Promise((resolve) => {
      const s = io(server.url, { auth, transports: ['websocket'], reconnection: false });
      s.once('connect', () => (s.close(), resolve('connected')));
      s.once('connect_error', (e) => (s.close(), resolve(e.message)));
    });
  assert.equal(await attempt({}), 'unauthorized');
  assert.equal(await attempt({ token: 'a'.repeat(64) }), 'unauthorized');
  assert.equal(await attempt({ token: { $ne: '' } }), 'unauthorized');

  const p = await Player.register(server);
  await fetch(`${server.url}/auth/logout`, { method: 'POST', headers: { Authorization: `Bearer ${p.token}` } });
  assert.equal(await attempt({ token: p.token }), 'unauthorized');
});

test('there is no HTTP route that grants or changes chips', async () => {
  const p = await Player.register(server);
  const headers = { Authorization: `Bearer ${p.token}`, 'Content-Type': 'application/json' };
  for (const [method, route] of [
    ['POST', '/wallet'],
    ['PUT', '/wallet'],
    ['POST', '/shop/buy'],
    ['POST', '/profile/chips'],
    ['PUT', '/profile'],
    ['POST', '/auth/grant'],
    ['POST', '/admin/grant'],
    ['POST', '/chips'],
  ]) {
    const res = await fetch(server.url + route, {
      method,
      headers,
      body: JSON.stringify({ balance: 1e9, chips: 1e9, wallet: 1e9 }),
    });
    assert.ok([400, 401, 404, 405].includes(res.status), `${method} ${route} -> ${res.status}`);
  }
  const me = await (await fetch(`${server.url}/auth/me`, { headers })).json();
  assert.equal(JSON.stringify(me).includes('balance'), false); // the profile does not carry chips
  const c = await p.connect();
  assert.equal(c.wallet, START);
  c.close();
});
