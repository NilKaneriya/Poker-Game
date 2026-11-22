// Load test: many players, each at their own bots table, all playing at once.
// Starts a throwaway server (own port and data folder) unless --url is given, plays for a
// while, then reports how fast the server answered and what /metrics saw.
//
// Run from qa/ (npm install once):
//   npm run load-test                                 50 tables of 6 = 50 humans + 250 bots, 60 s
//   npm run load-test -- --tables=200 --seconds=120
//   npm run load-test -- --report                     also save qa/reports/load-test/<date>-<tables>t.md
//   npm run load-test -- --url=http://localhost:3000  against a running server (needs AUTH_RATE_MAX raised)
const { spawn } = require('child_process');
const fs = require('fs');
const os = require('os');
const path = require('path');
const { io } = require('socket.io-client');

const args = Object.fromEntries(
  process.argv.slice(2).map((a) => {
    const [key, value] = a.replace(/^--/, '').split('=');
    return [key, value ?? 'true'];
  }),
);
const TABLES = Number(args.tables || 50);
const SECONDS = Number(args.seconds || 60);
const SEATS = Number(args.seats || 6);
const PORT = 3200;
const URL = args.url || `http://localhost:${PORT}`;
const RUN_ID = Date.now().toString(36).slice(-5);
const SERVER_ENTRY = path.join(__dirname, '..', '..', 'poker-server', 'src', 'index.js');
const REPORT_DIR = path.join(__dirname, '..', 'reports', 'load-test');

// Fast timings for the throwaway server: bots act ~10x faster than in real play.
const SERVER_TIMINGS = { BOT_THINK_MS: '150', NEXT_HAND_DELAY_MS: '300', RESULT_SHOW_MS: '300' };

// Everything printed is also kept for the report.
const output = [];
const say = (line = '') => {
  console.log(line);
  output.push(line);
};

const stats = { actions: 0, rejected: 0, errors: 0, rebuys: 0, latencies: [] };
let server = null;

// ---------- Server ----------

async function startServer() {
  const dataDir = fs.mkdtempSync(path.join(os.tmpdir(), 'poker-load-'));
  server = spawn(process.execPath, [SERVER_ENTRY], {
    env: {
      ...process.env,
      PORT: String(PORT),
      DATA_DIR: dataDir,
      MONGODB_URI: '', // JSON files in a temp folder: never touches real player data
      AUTH_RATE_MAX: '1000000',
      ...SERVER_TIMINGS,
    },
    stdio: ['ignore', 'ignore', 'inherit'],
  });
  for (let i = 0; i < 50; i++) {
    try {
      if ((await fetch(`${URL}/health`)).ok) return;
    } catch {
      // not up yet
    }
    await sleep(200);
  }
  throw new Error('server did not start');
}

// ---------- One player ----------

async function startPlayer(index) {
  const res = await fetch(`${URL}/auth/register`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ username: `lt${RUN_ID}${index}`, password: 'secret123' }),
  });
  const { token, error } = await res.json();
  if (!token) throw new Error(`register failed: ${error}`);

  const socket = io(URL, { auth: { token }, transports: ['websocket'], reconnection: false });
  let sentAt = 0; // when our last action left; 0 = not waiting for an answer
  let lastTurn = '';

  socket.on('connect_error', () => stats.errors++);
  socket.on('actionRejected', () => {
    stats.rejected++;
    sentAt = 0;
  });
  socket.on('tableState', (state) => {
    if (sentAt) {
      stats.latencies.push(performance.now() - sentAt); // action -> new state
      sentAt = 0;
    }
    const mySeat = state.seats?.[state.you?.seat];
    if (mySeat && mySeat.chips === 0 && (!state.hand || state.hand.result)) {
      stats.rebuys++;
      socket.emit('rebuy');
      return;
    }
    const hand = state.hand;
    if (!hand?.legal || hand.currentSeat !== state.you.seat) return;
    const turn = `${hand.stage}:${hand.pot}:${hand.board.length}`;
    if (turn === lastTurn) return; // already answered this turn
    lastTurn = turn;
    setTimeout(
      () => {
        sentAt = performance.now();
        stats.actions++;
        socket.emit('playerAction', pickAction(hand.legal));
      },
      50 + Math.random() * 150,
    ); // a (very fast) human
  });

  await new Promise((resolve, reject) => {
    socket.once('session', resolve);
    socket.once('connect_error', reject);
  });
  socket.emit('createTable', { tableName: `Load ${index}`, mode: 'bots', maxSeats: SEATS });
  return socket;
}

/** Mostly check/call, sometimes raise the minimum, sometimes fold. */
function pickAction({ actions, minRaiseTo }) {
  const roll = Math.random();
  if (roll < 0.1 && actions.includes('fold')) return { action: 'fold' };
  if (roll < 0.25 && actions.includes('raise')) return { action: 'raise', amount: minRaiseTo };
  if (actions.includes('check')) return { action: 'check' };
  if (actions.includes('call')) return { action: 'call' };
  return { action: actions.includes('allIn') ? 'allIn' : 'fold' };
}

// ---------- Report ----------

async function metrics() {
  return (await fetch(`${URL}/metrics`)).json();
}

function percentile(sorted, p) {
  if (sorted.length === 0) return 0;
  return sorted[Math.min(sorted.length - 1, Math.floor((p / 100) * sorted.length))];
}

const fmt = (ms) => `${ms.toFixed(1)} ms`;
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

async function main() {
  if (!args.url) await startServer();
  say(`Load test: ${TABLES} tables x ${SEATS} seats (${TABLES} humans, ${TABLES * (SEATS - 1)} bots) for ${SECONDS} s`);

  const startedAt = Date.now();
  const sockets = [];
  for (let i = 0; i < TABLES; i += 20) {
    // join in batches of 20 so start-up itself isn't the test
    const batch = Array.from({ length: Math.min(20, TABLES - i) }, (_, k) => startPlayer(i + k));
    sockets.push(...(await Promise.all(batch)));
  }
  say(`All ${sockets.length} players connected in ${((Date.now() - startedAt) / 1000).toFixed(1)} s\n`);

  const before = await metrics();
  let after = before;
  let worstLoopP99 = 0;
  let worstLoopMax = 0;
  for (let t = 10; t <= SECONDS; t += 10) {
    await sleep(10_000);
    const m = await metrics();
    after = m;
    worstLoopP99 = Math.max(worstLoopP99, m.eventLoopDelayMs.p99);
    worstLoopMax = Math.max(worstLoopMax, m.eventLoopDelayMs.max);
    say(
      `${String(t).padStart(4)} s  tables ${m.tables}  sockets ${m.sockets}  hands/min ${m.hands.lastMinute}  ` +
        `loop delay p99 ${m.eventLoopDelayMs.p99} ms  memory ${m.memoryMb.rss} MB`,
    );
  }
  sockets.forEach((s) => s.close());

  const sorted = stats.latencies.slice().sort((a, b) => a - b);
  const hands = after.hands.finished - before.hands.finished;
  say('\n----- Result -----');
  say(`Tables / players / bots : ${after.tables} / ${after.players.humans} / ${after.players.bots}`);
  say(`Hands finished          : ${hands} (${Math.round((hands / SECONDS) * 60)} per minute)`);
  say(`Your actions sent       : ${stats.actions} (rejected ${stats.rejected}, rebuys ${stats.rebuys})`);
  say(
    `Action -> new state     : p50 ${fmt(percentile(sorted, 50))}  p95 ${fmt(percentile(sorted, 95))}  ` +
      `p99 ${fmt(percentile(sorted, 99))}  max ${fmt(sorted[sorted.length - 1] || 0)}`,
  );
  say(`Server event-loop delay : worst p99 ${worstLoopP99} ms, worst max ${worstLoopMax} ms (per 10 s window)`);
  say(`Server memory           : ${after.memoryMb.rss} MB`);
  say(`Connection errors       : ${stats.errors}`);

  if (args.report) writeReport({ hands, sorted, after, worstLoopP99, worstLoopMax });
}

/** Saves the run as Markdown in qa/reports/load-test/. */
function writeReport({ hands, sorted, after, worstLoopP99, worstLoopMax }) {
  const date = new Date().toLocaleDateString('sv'); // local YYYY-MM-DD
  const file = path.join(REPORT_DIR, `${date}-${TABLES}t-${SECONDS}s.md`);
  const cpu = os.cpus()[0]?.model?.trim() || 'unknown CPU';
  const ms = (v) => `${v.toFixed(1)} ms`;
  const text = `# Load test: ${TABLES} tables, ${SECONDS} s (${date})

Generated by \`qa/load-test/load-test.js --report\`.

## Setup
| | |
|---|---|
| Server | ${args.url ? args.url : 'throwaway server started by the test (JSON storage in a temp folder)'} |
| Tables | ${TABLES} bots tables x ${SEATS} seats = ${TABLES} players + ${TABLES * (SEATS - 1)} bots |
| Duration | ${SECONDS} s of play after everyone joined |
| Timings | ${args.url ? 'the running server\'s own' : Object.entries(SERVER_TIMINGS).map(([k, v]) => `${k}=${v}`).join(', ')} |
| Machine | ${os.type()} ${os.release()}, ${cpu}, ${os.cpus().length} threads, ${Math.round(os.totalmem() / 1073741824)} GB RAM |
| Node | ${process.version} |

## Result
| Metric | Value |
|---|---|
| Hands finished | ${hands} (${Math.round((hands / SECONDS) * 60)} per minute) |
| Player actions sent | ${stats.actions} (rejected ${stats.rejected}, rebuys ${stats.rebuys}) |
| Action -> new state p50 | ${ms(percentile(sorted, 50))} |
| Action -> new state p95 | ${ms(percentile(sorted, 95))} |
| Action -> new state p99 | ${ms(percentile(sorted, 99))} |
| Action -> new state max | ${ms(sorted[sorted.length - 1] || 0)} |
| Event-loop delay, worst p99 / max (10 s windows) | ${worstLoopP99} / ${worstLoopMax} ms |
| Server memory (RSS) at the end | ${after.memoryMb.rss} MB |
| Connection errors | ${stats.errors} |

Note: on Windows the event-loop delay has a floor of about 15-25 ms even when idle (the OS
timer resolution), so only values well above that point to real blocking.

Memory grows during a run because the JSON backend keeps every player's last 30 hands of
history in memory; it stops growing once every player has 30.

## Console output
\`\`\`
${output.join('\n')}
\`\`\`
`;
  fs.mkdirSync(REPORT_DIR, { recursive: true });
  fs.writeFileSync(file, text);
  console.log(`\nReport saved: ${path.relative(path.join(__dirname, '..', '..'), file)}`);
}

main()
  .catch((err) => {
    console.error(`Load test failed: ${err.message}`);
    process.exitCode = 1;
  })
  .finally(() => {
    if (server) server.kill();
    setTimeout(() => process.exit(), 200);
  });
