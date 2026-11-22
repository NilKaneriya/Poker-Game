const test = require('node:test');
const assert = require('node:assert/strict');
const Table = require('../src/engine/Table');
const { BotPlayer, LEVELS, handStrength } = require('../src/bots/BotPlayer');
const { buildPlayerView } = require('../src/game/playerView');

test('hand strength: pocket aces beat 7-2 pre-flop, a flush beats a pair after', () => {
  assert.ok(handStrength(['Ah', 'Ad'], []) > handStrength(['7c', '2d'], []));
  assert.ok(handStrength(['Ah', '2h'], ['Kh', '9h', '4h']) > handStrength(['Ac', '2d'], ['As', '9h', '4c']));
});

test('every level only ever picks legal actions (200 bot-only hands)', () => {
  for (const level of LEVELS) {
    const table = new Table({ code: 'BOTS', name: 'Bots', maxSeats: 6, smallBlind: 10, bigBlind: 20, startingChips: 1000 });
    const bots = new Map();
    for (let i = 0; i < 4; i++) {
      const bot = new BotPlayer({ playerId: `b${i}`, name: `b${i}`, level });
      bots.set(bot.playerId, bot);
      table.addPlayer({ playerId: bot.playerId, name: bot.name, isBot: true });
    }

    for (let h = 0; h < 50 && table.canStartHand(); h++) {
      table.startHand();
      while (!table.hand.isComplete) {
        const id = table.hand.currentPlayerId;
        const view = buildPlayerView(table, id);
        const choice = bots.get(id).decide(view, view.hand.legal);
        const result = table.hand.applyAction(id, choice.action, choice.amount);
        assert.ok(result.ok, `${level} bot chose ${choice.action} ${choice.amount ?? ''}: ${result.error}`);
      }
      table.finishHand();
    }
    assert.equal(table.players.reduce((s, p) => s + p.chips, 0), 4000, `${level}: chips conserved`);
  }
});

test('a bot never sees other players\' hole cards', () => {
  const table = new Table({ code: 'PEEK', name: 'Peek', maxSeats: 6, smallBlind: 10, bigBlind: 20, startingChips: 1000 });
  table.addPlayer({ playerId: 'bot', name: 'bot', isBot: true });
  table.addPlayer({ playerId: 'human', name: 'human' });
  table.startHand();
  const view = JSON.stringify(buildPlayerView(table, 'bot'));
  for (const card of table.hand.getPlayer('human').holeCards) assert.ok(!view.includes(`"${card}"`));
});
