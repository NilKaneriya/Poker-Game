// Plays real hands against the bots through a running server and prints what happens.
// Usage: npm start (one terminal), then: npm run play:bots
// Tip: NEXT_HAND_DELAY_MS=300 RESULT_SHOW_MS=300 BOT_THINK_MS=50 npm start  for a fast run.
const { io } = require('socket.io-client');

const URL = process.env.SERVER_URL || 'http://localhost:3000';
const HANDS_TO_PLAY = Number(process.env.HANDS || 3);
const fail = (msg) => { console.error('FAIL:', msg); process.exit(1); };
setTimeout(() => fail('timed out'), 60000);

(async () => {
  const username = `tester_${Date.now().toString(36).slice(-5)}`;
  const res = await fetch(`${URL}/auth/register`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ username, password: 'secret123' }),
  });
  const { token, playerId } = await res.json();
  if (!token) fail('could not register');

  const socket = io(URL, { auth: { token } });
  socket.on('actionRejected', ({ reason }) => fail(`server rejected our move: ${reason}`));
  await new Promise((r) => socket.once('session', r));
  socket.emit('createTable', { tableName: 'Bot test' });

  let handsSeen = 0;
  let lastLine = '';
  let resultShownFor = -1;

  socket.on('tableState', (state) => {
    const names = state.seats.filter(Boolean).map((s) => `${s.name}(${s.chips})`).join(', ');
    const hand = state.hand;
    if (!hand) {
      const line = `-- waiting | ${names}`;
      if (line !== lastLine) console.log((lastLine = line));
      return;
    }

    const line = `#${state.handNumber} ${hand.stage.padEnd(8)} board [${hand.board.join(' ')}] pot ${hand.pot} | my cards [${hand.yourCards.join(' ')}]`;
    if (line !== lastLine) console.log((lastLine = line));

    // Security check: we must never see another player's cards before showdown.
    for (const s of hand.seats) {
      if (s.cards && !hand.result) fail(`saw seat ${s.seat}'s cards before showdown`);
    }

    if (hand.result && resultShownFor !== state.handNumber) {
      resultShownFor = state.handNumber;
      for (const w of hand.result.winners) console.log(`   => ${w.name} wins ${w.amount}${w.handName ? ` with ${w.handName}` : ''}`);
      if (++handsSeen >= HANDS_TO_PLAY) {
        console.log(`OK: played ${handsSeen} hands against bots`);
        process.exit(0);
      }
    }

    // Our turn: check if we can, otherwise call (or go all-in if that's the only way to call).
    if (hand.legal && hand.currentSeat === state.you.seat) {
      const a = hand.legal.actions;
      const action = a.includes('check') ? 'check' : a.includes('call') ? 'call' : 'allIn';
      console.log(`   me: ${action}`);
      socket.emit('playerAction', { action });
    }
  });
})().catch((e) => fail(e.message));
