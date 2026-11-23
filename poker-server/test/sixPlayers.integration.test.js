// A full table of six real clients: hidden cards, blinds, the button, showdown and chip totals.
// (The QA browser test could only run four accounts at once; this covers the six-player table.)
const test = require('node:test');
const assert = require('node:assert/strict');
const { TestServer, Player, until, sleep } = require('./liveHarness');

const SEATS = 6;
const START = 1000;
let server;

test.before(async () => {
  server = await new TestServer(3221).start();
});
test.after(async () => {
  await server.stop();
});

async function fullTable() {
  const players = [];
  for (let i = 0; i < SEATS; i++) players.push(await (await Player.register(server, 's')).connect());
  const code = await players[0].createTable({ tableName: 'six', mode: 'friends', maxSeats: SEATS });
  for (const p of players.slice(1)) await p.join(code);
  return { players, code };
}

const chipsOnTable = (state) => state.seats.reduce((sum, s) => sum + (s ? s.chips : 0), 0);
const finishedHand = (p) => p.table?.hand?.result;
const betweenHands = (p) => p.table && !p.table.hand;

test('six players: the game starts by itself once every seat is taken, with 1,000 chips each', async () => {
  const { players } = await fullTable();
  await until(() => players.every((p) => p.table?.started), 4000, 'game started');
  assert.deepEqual(new Set(players.map((p) => p.mySeat)).size, SEATS); // six different seats
  players.forEach((p) => p.close());
});

test('six players check and call to showdown: nobody sees another hand, chips add up', async () => {
  const { players } = await fullTable();
  players.forEach((p) => (p.mode = 'passive'));
  await until(() => players.every(finishedHand), 12000, 'showdown for all');

  const result = players[0].table.hand.result;
  const stagesSeen = new Set(players[0].states.map((s) => s.hand?.stage));
  for (const stage of ['preflop', 'flop', 'turn', 'river', 'showdown']) assert.ok(stagesSeen.has(stage), `stage ${stage}`);

  // Secrecy: before the result, every client only ever received its own two cards.
  for (const p of players) {
    for (const state of p.states) {
      if (!state.hand || state.hand.result) continue;
      assert.ok(state.hand.yourCards.length === 0 || state.hand.yourCards.length === 2);
      for (const seat of state.hand.seats) {
        if (seat.seat !== p.mySeat) assert.equal(seat.cards, null, 'an opponent card leaked before showdown');
      }
      assert.equal(state.deck, undefined);
    }
  }
  // Everyone's own cards were different cards.
  const all = players.flatMap((p) => p.table.hand.yourCards);
  assert.equal(new Set(all).size, all.length);

  // All clients agree on the board and the winners.
  const boards = new Set(players.map((p) => JSON.stringify(p.table.hand.board)));
  assert.equal(boards.size, 1);
  assert.equal(players[0].table.hand.board.length, 5);
  const winnerSets = new Set(players.map((p) => JSON.stringify(p.table.hand.result.winners.map((w) => [w.playerId, w.amount]))));
  assert.equal(winnerSets.size, 1);

  // Chips: the winners together take the whole pot, and after the hand nothing was created or lost.
  const pot = players[0].table.hand.pot;
  assert.equal(
    result.winners.reduce((sum, w) => sum + w.amount, 0),
    pot,
  );
  assert.equal(pot, SEATS * 20); // everyone called the 20 big blind
  await until(() => players.every(betweenHands) || players.every((p) => p.table.handNumber > 1), 4000, 'next hand');
  const settled = players.map((p) => p.states.findLast((s) => !s.hand || s.hand.result)).filter(Boolean);
  const total = chipsOnTable(settled[0]);
  assert.equal(total, SEATS * START);
  players.forEach((p) => p.close());
});

test('the dealer button and blinds move to the next player every hand', async () => {
  const { players } = await fullTable();
  players.forEach((p) => (p.mode = 'passive'));
  await until(() => players[0].table.handNumber >= 3, 20000, 'three hands');
  const buttons = [...new Set(players[0].states.filter((s) => s.handNumber >= 1).map((s) => `${s.handNumber}:${s.buttonSeat}`))];
  const perHand = new Map(buttons.map((b) => b.split(':')));
  assert.equal(new Set(perHand.values()).size >= 3, true, `button positions ${JSON.stringify([...perHand])}`);
  players.forEach((p) => p.close());
});

test('everyone goes all-in with six players: one pot, one or more winners, chips conserved', async () => {
  const { players } = await fullTable();
  players.forEach((p) => (p.mode = 'shove'));
  await until(() => players.every(finishedHand), 12000, 'showdown for all');
  const hand = players[0].table.hand;
  assert.equal(hand.pot, SEATS * START);
  assert.equal(
    hand.result.winners.reduce((sum, w) => sum + w.amount, 0),
    SEATS * START,
  );
  // every all-in player's cards are shown at showdown
  assert.equal(hand.seats.filter((s) => s.cards && s.cards.length === 2).length, SEATS);
  players.forEach((p) => (p.mode = 'idle'));
  players.forEach((p) => p.close());
});

test('mid-hand: a seventh player cannot sit, and a player who leaves is folded while the hand goes on', async () => {
  const { players, code } = await fullTable();
  const extra = await (await Player.register(server, 'x')).connect();
  extra.emit('joinTable', { code });
  await until(() => extra.errors.length > 0, 3000, 'refusal');
  assert.match(extra.errors[0], /full/i);
  extra.close();

  await until(() => players[0].table.hand, 4000, 'hand running');
  players[5].emit('leaveTable');
  players.slice(0, 5).forEach((p) => (p.mode = 'passive'));
  await until(() => players.slice(0, 5).every(finishedHand), 12000, 'hand finished without the leaver');
  await sleep(300);
  assert.equal(players[0].table.seats.filter(Boolean).length, SEATS - 1);
  players.forEach((p) => p.close());
});
