// Smoke test against a running server:
// register/login, reject bad logins and token-less sockets, create + join a table
// by code, then reconnect with the same token and check the seat is kept.
// Usage: npm start (one terminal), then: npm run test:client
const { io } = require('socket.io-client');

const URL = process.env.SERVER_URL || 'http://localhost:3000';
const fail = (msg) => { console.error('FAIL:', msg); process.exit(1); };
const check = (cond, msg) => { if (!cond) fail(msg); };
const GRACE_MS = Number(process.env.TEST_GRACE_MS || 0); // set by test:e2e (matches the server's RECONNECT_GRACE_MS)
setTimeout(() => fail('timed out'), 45000);

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

  // Bots table for 4: Alice + 3 bots, starts right away, not listed in the lobby.
  alice.emit('createTable', { tableName: 'Bots', mode: 'bots', maxSeats: 4 });
  const bots = await stateWhere(alice, (s) => s.name === 'Bots');
  const seated = bots.seats.filter(Boolean);
  check(seated.length === 4 && seated.filter((s) => s.isBot).length === 3, 'bots table not filled with 3 bots');
  check(bots.started, 'bots table should start at once');
  const list = await new Promise((resolve) => { bob.once('tableList', resolve); bob.emit('listTables'); });
  check(!list.some((t) => t.code === bots.code), 'bots table shows in the open list');

  // Bob joins it by code during a hand: he watches until the hand ends, then a bot
  // stands up and Bob takes its seat.
  await stateWhere(alice, (s) => s.code === bots.code && s.hand && !s.hand.result);
  bob.emit('joinTable', { code: bots.code });
  const watching = await stateWhere(bob, (s) => s.code === bots.code);
  if (watching.you.seat === -1) {
    check(watching.you.waiting, 'mid-hand joiner not marked as waiting');
    check(watching.hand && watching.hand.yourCards.length === 0, 'waiting player was dealt cards');
    console.log('Bob joined mid-hand and is watching from the waiting list');
  }
  const swapped = await stateWhere(bob, (s) => s.code === bots.code && s.you.seat >= 0);
  const after = swapped.seats.filter(Boolean);
  check(!swapped.you.waiting, 'seated player still marked as waiting');
  check(after.length === 4 && after.filter((s) => s.isBot).length === 2, 'a bot did not give up its seat');
  check(!swapped.hand || swapped.hand.stage === 'preflop', 'joiner was seated in the middle of a hand');
  console.log(`Bots table ${bots.code}: Bob took a bot's seat after the hand (2 humans + 2 bots)`);

  // Bad sizes fall back to the 6-seat default.
  bob.emit('createTable', { mode: 'friends', maxSeats: 9 });
  const fallback = await stateWhere(bob, (s) => s.code !== duel.code && s.hostId === s.you.playerId);
  check(fallback.maxSeats === 6, 'invalid seat count was not clamped');
  alice.emit('leaveTable');
  bob.emit('leaveTable');
}

/**
 * Statistics (the retention feature): sent on connect, and updated after every hand the
 * player was dealt into. Plays one quick hand against a bot, checking or calling.
 */
async function statsChecks(alice) {
  const autoPlay = (state) => {
    const hand = state.hand;
    if (hand?.legal && hand.currentSeat === state.you.seat) {
      const a = hand.legal.actions;
      alice.emit('playerAction', { action: a.includes('check') ? 'check' : a.includes('call') ? 'call' : 'allIn' });
    }
  };
  alice.on('tableState', autoPlay);
  const before = await new Promise((resolve) => { alice.once('stats', resolve); alice.emit('getStats'); });
  alice.emit('createTable', { tableName: 'Stats', mode: 'bots', maxSeats: 2 });
  const after = await new Promise((resolve) => {
    const handler = (s) => { if (s.handsPlayed > before.handsPlayed) { alice.off('stats', handler); resolve(s); } };
    alice.on('stats', handler);
  });
  alice.off('tableState', autoPlay);
  check(after.handsPlayed === before.handsPlayed + 1, 'hands played did not go up by one');
  check(after.handsWon >= before.handsWon, 'hands won went down');
  console.log(`Stats after a hand: played ${after.handsPlayed}, won ${after.handsWon}, biggest pot ${after.biggestPot}, best hand "${after.bestHand}"`);
  alice.emit('leaveTable');
}

/**
 * A player who disconnects keeps their seat for the grace period, then the seat is freed.
 * Needs a short RECONNECT_GRACE_MS (test:e2e starts the server with one).
 */
async function graceChecks(alice, bob, bobToken) {
  alice.emit('createTable', { tableName: 'Grace', mode: 'friends', maxSeats: 6 });
  const room = await stateWhere(alice, (s) => s.name === 'Grace');
  bob.emit('joinTable', { code: room.code });
  await stateWhere(alice, (s) => s.code === room.code && s.seats.filter(Boolean).length === 2);

  bob.disconnect();
  const offline = await stateWhere(alice, (s) => s.code === room.code && s.seats.some((x) => x && !x.connected));
  check(offline.seats.filter(Boolean).length === 2, 'seat should be kept while the player is away');
  console.log('Bob disconnected: seat kept and marked offline');

  const freed = await stateWhere(alice, (s) => s.code === room.code && s.seats.filter(Boolean).length === 1);
  check(freed.seats.filter(Boolean)[0].name === offline.seats.find((x) => x && x.connected).name, 'wrong seat freed');
  console.log(`Bob did not come back within ${GRACE_MS} ms: seat freed`);

  // Coming back later: no table any more, and the session says so.
  const bob3 = io(URL, { auth: { token: bobToken } });
  const session = await once(bob3, 'session');
  check(session.tableCode === null, 'session should report no table after the seat was freed');
  console.log('Bob reconnected later: session reports no table');
  bob3.close();
  alice.emit('leaveTable');
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
  check(resumed.tableCode === created.code, 'session should name the table the player is seated at');
  check(back.you.seat !== -1, 'seat lost after reconnect');
  console.log(`Bob reconnected to seat ${back.you.seat}`);

  await waitingRoomChecks(alice, bob2);
  await statsChecks(alice);
  if (GRACE_MS) await graceChecks(alice, bob2, bobLogin.body.token);
  else console.log('(skipping reconnect-grace check: run `npm run test:e2e` for the full suite)');

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
