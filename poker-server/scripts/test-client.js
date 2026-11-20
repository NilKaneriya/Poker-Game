// Smoke test against a running server:
// register/login, reject bad logins and token-less sockets, create + join a table
// by code, then reconnect with the same token and check the seat is kept.
// Usage: npm start (one terminal), then: npm run test:client
const { io } = require('socket.io-client');

const URL = process.env.SERVER_URL || 'http://localhost:3000';
const fail = (msg) => { console.error('FAIL:', msg); process.exit(1); };
const check = (cond, msg) => { if (!cond) fail(msg); };
setTimeout(() => fail('timed out'), 20000);

const once = (socket, event) => new Promise((resolve) => socket.once(event, resolve));

async function post(path, body, token) {
  const res = await fetch(URL + path, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', ...(token && { Authorization: `Bearer ${token}` }) },
    body: JSON.stringify(body || {}),
  });
  return { status: res.status, body: await res.json() };
}

/** Wait for the next tableState that passes `test` (states arrive often). */
const stateWhere = (socket, test) =>
  new Promise((resolve) => {
    const handler = (state) => {
      if (!test(state)) return;
      socket.off('tableState', handler);
      resolve(state);
    };
    socket.on('tableState', handler);
  });

/**
 * Play-with-friends and play-with-bots tables:
 * - a friends table waits until every seat is taken, then the game starts by itself
 * - only the host can start early, and only with 2+ players
 * - a bots table fills its empty seats with bots, starts at once and can't be joined
 */
async function waitingRoomChecks(alice, bob) {
  const errorFrom = (socket) => once(socket, 'errorMessage');

  // Friends table for 3: Alice hosts, Bob joins, the game must not start yet.
  alice.emit('createTable', { tableName: 'Friends', mode: 'friends', maxSeats: 3 });
  const room = await stateWhere(alice, (s) => s.name === 'Friends');
  check(room.mode === 'friends' && room.maxSeats === 3 && room.seats.length === 3, 'friends table has wrong mode/size');
  check(room.started === false && room.hostId === room.you.playerId, 'new friends table should wait, hosted by Alice');

  bob.emit('joinTable', { code: room.code });
  const both = await stateWhere(bob, (s) => s.code === room.code);
  check(both.seats.filter(Boolean).length === 2 && !both.started && !both.hand, 'game started before the table was full');
  console.log(`Friends table ${room.code}: 2/3 seated, still waiting`);

  // Non-host can't start it.
  bob.emit('startGame');
  const denied = await errorFrom(bob);
  check(/host/i.test(denied.message), 'non-host was allowed to start: ' + denied.message);

  // Host starts early with 2 of 3 seats: the first hand gets dealt to both.
  alice.emit('startGame');
  const dealt = await stateWhere(bob, (s) => s.code === room.code && s.started && s.hand);
  check(dealt.hand.yourCards.length === 2, 'Bob was not dealt in after the host started');
  console.log('Host started early; first hand dealt');

  // Full-table auto start: a 2-seat table starts as soon as the second player sits.
  alice.emit('createTable', { tableName: 'Heads up', mode: 'friends', maxSeats: 2 });
  const duel = await stateWhere(alice, (s) => s.name === 'Heads up');
  check(!duel.started, '2-seat table started with 1 player');
  bob.emit('joinTable', { code: duel.code });
  const full = await stateWhere(alice, (s) => s.code === duel.code && s.started);
  check(full.seats.every(Boolean), 'started without every seat filled');
  await stateWhere(alice, (s) => s.code === duel.code && s.hand);
  console.log(`Table ${duel.code} started by itself when the last seat was taken`);

  // Bots table for 4: Alice + 3 bots, starts right away, private.
  alice.emit('createTable', { tableName: 'Bots', mode: 'bots', maxSeats: 4 });
  const bots = await stateWhere(alice, (s) => s.name === 'Bots');
  const seated = bots.seats.filter(Boolean);
  check(seated.length === 4 && seated.filter((s) => s.isBot).length === 3, 'bots table not filled with 3 bots');
  check(bots.started, 'bots table should start at once');
  bob.emit('joinTable', { code: bots.code });
  const privateErr = await errorFrom(bob);
  check(/bots/i.test(privateErr.message), 'joined a private bots table');
  const list = await new Promise((resolve) => { bob.once('tableList', resolve); bob.emit('listTables'); });
  check(!list.some((t) => t.code === bots.code), 'bots table shows in the open list');
  console.log(`Bots table ${bots.code}: 1 human + 3 bots, private`);

  // Bad sizes fall back to the 6-seat default.
  bob.emit('createTable', { mode: 'friends', maxSeats: 9 });
  const fallback = await stateWhere(bob, (s) => s.code !== duel.code && s.hostId === s.you.playerId);
  check(fallback.maxSeats === 6, 'invalid seat count was not clamped');
  alice.emit('leaveTable');
  bob.emit('leaveTable');
}

(async () => {
  const suffix = Date.now().toString(36).slice(-5);
  const password = 'secret123';

  const reg = await post('/auth/register', { username: `alice_${suffix}`, password, remember: true });
  check(reg.status === 200 && reg.body.token, 'register failed: ' + JSON.stringify(reg.body));
  const dup = await post('/auth/register', { username: `ALICE_${suffix}`, password });
  check(dup.status === 400, 'duplicate username (case-insensitive) should be rejected');
  const bad = await post('/auth/login', { username: `alice_${suffix}`, password: 'wrong-pass' });
  check(bad.status === 401, 'wrong password should be rejected');
  await post('/auth/register', { username: `bob_${suffix}`, password });
  const bobLogin = await post('/auth/login', { username: `bob_${suffix}`, password });
  check(bobLogin.status === 200, 'login failed');
  console.log('Auth: register, duplicate, wrong password, login OK');

  // Auto-login check, as the Splash scene does with a remembered token.
  const me = await fetch(`${URL}/auth/me`, { headers: { Authorization: `Bearer ${reg.body.token}` } });
  check(me.status === 200, '/auth/me should accept a valid token');

  // A socket without a token must be refused.
  const anon = io(URL, { reconnection: false });
  const err = await once(anon, 'connect_error');
  check(err.message === 'unauthorized', 'token-less socket was not rejected');
  console.log('Socket without token rejected');

  const alice = io(URL, { auth: { token: reg.body.token } });
  await once(alice, 'session');
  alice.emit('createTable', { tableName: 'Smoke Test' });
  const created = await once(alice, 'tableState');
  console.log('Alice created table', created.code);

  const bob = io(URL, { auth: { token: bobLogin.body.token } });
  await once(bob, 'session');
  bob.emit('joinTable', { code: created.code.toLowerCase() });
  const joined = await once(bob, 'tableState');
  const names = joined.seats.filter(Boolean).map((s) => s.name);
  console.log('Bob joined; seated:', names.join(', '));
  check(names.length === 2, 'expected 2 players');

  bob.emit('playerAction', { action: 'raise', amount: 50 });
  const rejected = await once(bob, 'actionRejected');
  console.log('Action with no hand rejected:', rejected.reason);

  // Drop Bob and reconnect with the same token.
  bob.disconnect();
  const bob2 = io(URL, { auth: { token: bobLogin.body.token } });
  // Server sends session and tableState back to back, so listen for both up front.
  const [resumed, back] = await Promise.all([once(bob2, 'session'), once(bob2, 'tableState')]);
  check(resumed.playerId === bobLogin.body.playerId, 'reconnect gave a different player');
  check(back.you.seat !== -1, 'seat lost after reconnect');
  console.log(`Bob reconnected to seat ${back.you.seat}`);

  await waitingRoomChecks(alice, bob2);

  // Logout revokes the token.
  await post('/auth/logout', {}, bobLogin.body.token);
  const after = await fetch(`${URL}/auth/me`, { headers: { Authorization: `Bearer ${bobLogin.body.token}` } });
  check(after.status === 401, 'token still valid after logout');
  console.log('Logout revoked token');

  console.log('OK');
  alice.close();
  bob2.close();
  process.exit(0);
})().catch((e) => fail(e.message));
