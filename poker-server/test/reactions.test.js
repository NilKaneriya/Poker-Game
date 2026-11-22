const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('fs');
const path = require('path');
const http = require('http');
const { Server } = require('socket.io');
const ioClient = require('socket.io-client');
const { attachReactions, REACTION_EVENTS, EMOTE_IDS, ReactionService } = require('../src/reactions');

// A tiny "platform" with two groups, so the module is tested without any poker code.
const GROUPS = { ann: 'G1', bob: 'G1', cat: 'G2' };
const groupOf = (id) => GROUPS[id] ?? null;
const isInGroup = (group, id) => GROUPS[id] === group;

function service(now = () => 1000) {
  return new ReactionService({ groupOf, isInGroup }, { rateMax: 3, rateWindowMs: 1000 }, now);
}

test('reaction: a known emote to everyone in my group', () => {
  const r = service().send({ id: 'ann', name: 'Ann' }, { emoteId: 'laugh' });
  assert.equal(r.group, 'G1');
  assert.deepEqual(r.reaction, { id: 1, emoteId: 'laugh', fromId: 'ann', fromName: 'Ann', targetId: null, time: 1000 });
});

test('reaction: aimed at another player in my group', () => {
  const r = service().send({ id: 'ann', name: 'Ann' }, { emoteId: 'fire', targetId: 'bob' });
  assert.equal(r.reaction.targetId, 'bob');
});

test('reaction: aiming at myself or an empty id means everyone', () => {
  const s = service();
  assert.equal(s.send({ id: 'ann', name: 'Ann' }, { emoteId: 'gg', targetId: 'ann' }).reaction.targetId, null);
  assert.equal(s.send({ id: 'ann', name: 'Ann' }, { emoteId: 'gg', targetId: '' }).reaction.targetId, null);
});

test('reaction: refused outside a group, or aimed at a player of another group', () => {
  const s = service();
  assert.ok(s.send({ id: 'zed', name: 'Zed' }, { emoteId: 'wow' }).error);
  assert.ok(s.send({ id: 'ann', name: 'Ann' }, { emoteId: 'wow', targetId: 'cat' }).error);
  assert.ok(s.send({ id: 'ann', name: 'Ann' }, { emoteId: 'wow', targetId: 42 }).error);
});

test('reaction: unknown emotes and junk payloads are ignored quietly', () => {
  const s = service();
  for (const payload of [undefined, null, {}, { emoteId: 'nope' }, { emoteId: 7 }, 'laugh']) {
    assert.deepEqual(s.send({ id: 'ann', name: 'Ann' }, payload), { ignored: true });
  }
});

test('reaction: rate limited per player', () => {
  let now = 0;
  const s = service(() => now);
  const send = (id) => s.send({ id, name: id }, { emoteId: 'heart' });
  assert.deepEqual([1, 2, 3, 4].map(() => Boolean(send('ann').reaction)), [true, true, true, false]);
  assert.ok(send('ann').error);
  assert.ok(send('bob').reaction); // other players are not affected
  now = 1001;
  assert.ok(send('ann').reaction);
});

test('emote list: unique ids, the 12 the clients draw', () => {
  assert.equal(EMOTE_IDS.length, 12);
  assert.equal(new Set(EMOTE_IDS).size, 12);
});

test('reactions over a real Socket.IO server: only my group sees it, errors only to me', async () => {
  const server = http.createServer();
  const io = new Server(server);
  io.use((socket, next) => {
    socket.data.playerId = socket.handshake.auth.id;
    socket.data.name = socket.handshake.auth.id.toUpperCase();
    socket.join(`group:${GROUPS[socket.data.playerId]}`);
    next();
  });
  attachReactions(io, { groupOf, isInGroup, roomOf: (g) => `group:${g}` });
  await new Promise((resolve) => server.listen(0, resolve));
  const url = `http://localhost:${server.address().port}`;

  const connect = (id) => new Promise((resolve) => {
    const s = ioClient(url, { auth: { id }, transports: ['websocket'] });
    s.got = [];
    s.on(REACTION_EVENTS.REACTION, (r) => s.got.push(r));
    s.on(REACTION_EVENTS.ERROR, (e) => s.got.push({ error: e.message }));
    s.on('connect', () => resolve(s));
  });
  const [ann, bob, cat] = await Promise.all(['ann', 'bob', 'cat'].map(connect));
  try {
    ann.emit(REACTION_EVENTS.SEND, { emoteId: 'cool', targetId: 'bob' });
    ann.emit(REACTION_EVENTS.SEND, { emoteId: 'cool', targetId: 'cat' });
    await new Promise((r) => setTimeout(r, 150));

    assert.deepEqual(bob.got.map((r) => [r.emoteId, r.fromName, r.targetId]), [['cool', 'ANN', 'bob']]);
    assert.equal(ann.got.length, 2);
    assert.ok(ann.got[1].error);
    assert.equal(cat.got.length, 0);
  } finally {
    [ann, bob, cat].forEach((s) => s.close());
    io.close();
    server.close();
  }
});

test('reactions module is separate: it imports nothing outside its own folder', () => {
  const dir = path.join(__dirname, '..', 'src', 'reactions');
  for (const file of fs.readdirSync(dir)) {
    const source = fs.readFileSync(path.join(dir, file), 'utf8');
    for (const [, target] of source.matchAll(/require\('([^']+)'\)/g)) {
      assert.ok(target.startsWith('./'), `${file} imports ${target}; the module must stand alone`);
    }
  }
});
