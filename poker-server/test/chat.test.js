const test = require('node:test');
const assert = require('node:assert/strict');
const { cleanChatText } = require('../src/chat/chatText');
const RateLimiter = require('../src/chat/RateLimiter');
const ChatStore = require('../src/chat/ChatStore');
const { EventEmitter } = require('events');
const { attachChat } = require('../src/chat');
const { GAME_EVENTS } = require('../src/game/gameEvents');

test('chat text: trimmed, spaces collapsed, line breaks and control characters removed', () => {
  assert.equal(cleanChatText('  nice \n\n hand\t\u0007 ', 140), 'nice hand');
  assert.equal(cleanChatText('a\u200Bb', 140), 'a b'); // invisible format characters can't hide words
});

test('chat text: anything that is not a string, or only spaces, gives nothing to send', () => {
  for (const value of [undefined, null, 42, {}, ['hi'], '   ', '\n\t']) assert.equal(cleanChatText(value, 140), '');
});

test('chat text: cut to the maximum length without splitting an emoji', () => {
  assert.equal(cleanChatText('x'.repeat(300), 140).length, 140);
  assert.equal(cleanChatText('ab🂡🂡', 3), 'ab🂡');
});

test('chat text: swear words are masked, normal words are kept', () => {
  assert.equal(cleanChatText('what the FUCKING river', 140), 'what the ******* river');
  assert.equal(cleanChatText('nice classic hand', 140), 'nice classic hand');
});

test('rate limiter: allows N per window, then refuses until the window moves on', () => {
  let now = 0;
  const limiter = new RateLimiter({ max: 3, windowMs: 1000, now: () => now });
  assert.deepEqual(
    [1, 2, 3, 4].map(() => limiter.tryHit('a')),
    [true, true, true, false],
  );
  assert.equal(limiter.tryHit('b'), true); // per player
  now = 1001;
  assert.equal(limiter.tryHit('a'), true);
});

test('chat history: per table, oldest first, capped, cleared with the table', () => {
  const chat = new ChatStore(3);
  for (let i = 1; i <= 5; i++) chat.addPlayerMessage('T1', { playerId: 'p', name: 'P' }, `m${i}`);
  chat.addSystemMessage('T2', 'Bob joined the table');
  assert.deepEqual(
    chat.history('T1').map((m) => m.text),
    ['m3', 'm4', 'm5'],
  );
  assert.equal(chat.history('T2')[0].kind, 'system');
  assert.equal(chat.history('T2')[0].playerId, null);
  chat.clear('T1');
  assert.deepEqual(chat.history('T1'), []);
});

test('chat messages get increasing ids', () => {
  const chat = new ChatStore(10);
  const a = chat.addSystemMessage('T', 'one');
  const b = chat.addSystemMessage('T', 'two');
  assert.ok(b.id > a.id);
});

test('chat module: turns game events into system messages for that table only', () => {
  const sent = [];
  const io = { on() {}, to: (room) => ({ emit: (event, msg) => sent.push({ room, event, msg }) }) };
  const gameEvents = Object.assign(new EventEmitter(), { GAME_EVENTS });
  const settings = { maxLength: 140, historySize: 50, rateMax: 5, rateWindowMs: 10000 };
  const { store } = attachChat(io, { tableOf: () => null, roomOf: (c) => `table:${c}`, gameEvents }, settings);

  gameEvents.emit(GAME_EVENTS.PLAYER_JOINED, { code: 'T1', playerId: 'b', name: 'Bob' });
  gameEvents.emit(GAME_EVENTS.HAND_FINISHED, {
    code: 'T1',
    handNumber: 1,
    winners: [
      { playerId: 'a', name: 'Ava', amount: 150, handName: 'Flush' },
      { playerId: 'x', name: null, amount: 150, handName: '' },
    ],
  });

  assert.deepEqual(
    sent.map((s) => s.msg.text),
    ['Bob joined the table', 'Ava won 150 with Flush', 'A player who left won 150'],
  );
  assert.ok(sent.every((s) => s.room === 'table:T1' && s.event === 'chatMessage' && s.msg.kind === 'system'));
  assert.equal(store.history('T1').length, 3);

  gameEvents.emit(GAME_EVENTS.TABLE_CLOSED, { code: 'T1' });
  assert.equal(store.history('T1').length, 0);
});

test('chat module is separate: it never imports game code', () => {
  const fs = require('fs');
  const path = require('path');
  const dir = path.join(__dirname, '..', 'src', 'chat');
  for (const file of fs.readdirSync(dir)) {
    const source = fs.readFileSync(path.join(dir, file), 'utf8');
    const imports = [...source.matchAll(/require\('([^']+)'\)/g)].map((m) => m[1]);
    for (const target of imports) {
      assert.ok(!/engine|game|sockets|http|services|bots/.test(target), `${file} imports game code: ${target}`);
    }
  }
});
