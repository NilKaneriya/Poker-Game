// Smoke test against a running server:
// register/login, reject bad logins and token-less sockets, create + join a table
// by code, then reconnect with the same token and check the seat is kept.
// Usage: npm start (one terminal), then: npm run test:client
const { io } = require('socket.io-client');

const URL = process.env.SERVER_URL || 'http://localhost:3000';
const fail = (msg) => { console.error('FAIL:', msg); process.exit(1); };
const check = (cond, msg) => { if (!cond) fail(msg); };
setTimeout(() => fail('timed out'), 8000);

const once = (socket, event) => new Promise((resolve) => socket.once(event, resolve));

async function post(path, body, token) {
  const res = await fetch(URL + path, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', ...(token && { Authorization: `Bearer ${token}` }) },
    body: JSON.stringify(body || {}),
  });
  return { status: res.status, body: await res.json() };
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
