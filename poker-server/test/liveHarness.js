// Helpers for tests that run a real server process and real Socket.IO clients.
const { spawn } = require('child_process');
const fs = require('fs');
const os = require('os');
const path = require('path');
const { io } = require('socket.io-client');

const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

/** Wait until fn() is truthy (returns its value) or fail after `ms`. */
async function until(fn, ms = 4000, what = 'condition') {
  const end = Date.now() + ms;
  for (;;) {
    const value = fn();
    if (value) return value;
    if (Date.now() > end) throw new Error(`Timed out waiting for ${what}`);
    await sleep(15);
  }
}

class TestServer {
  constructor(port, env = {}) {
    this.port = port;
    this.url = `http://127.0.0.1:${port}`;
    this.env = env;
    this.dataDir = fs.mkdtempSync(path.join(os.tmpdir(), 'poker-live-'));
    this.proc = null;
  }

  async start() {
    this.proc = spawn(process.execPath, [path.join(__dirname, '..', 'src', 'index.js')], {
      env: {
        ...process.env,
        PORT: String(this.port),
        NEXT_HAND_DELAY_MS: '100',
        RESULT_SHOW_MS: '200',
        BOT_THINK_MS: '100',
        RECONNECT_GRACE_MS: '1200',
        AUTH_RATE_MAX: '100000',
        DATA_DIR: this.dataDir,
        MONGODB_URI: '',
        WEBGL_DIR: '',
        ...this.env,
      },
      stdio: ['ignore', 'ignore', 'inherit'],
    });
    for (let i = 0; i < 200; i++) {
      try {
        if ((await fetch(`${this.url}/health`)).ok) return this;
      } catch {
        // not up yet
      }
      await sleep(50);
    }
    throw new Error('test server did not start');
  }

  async stop() {
    if (this.proc && this.proc.exitCode === null) {
      const exited = new Promise((r) => this.proc.once('exit', r));
      this.proc.kill('SIGKILL');
      await exited;
    }
    try {
      fs.rmSync(this.dataDir, { recursive: true, force: true, maxRetries: 5, retryDelay: 100 });
    } catch {
      // never fail a test run over temp cleanup
    }
  }
}

let counter = 0;

/** One account + one socket that records every table state it receives. */
class Player {
  static async register(server, label = 'p') {
    const username = `${label}${(Date.now() % 1e6).toString(36)}${counter++}`.slice(0, 16);
    const res = await fetch(`${server.url}/auth/register`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ username, password: 'secret123' }),
    });
    const body = await res.json();
    if (!body.token) throw new Error(`register failed: ${JSON.stringify(body)}`);
    return new Player(server, body.token, body.playerId, username);
  }

  constructor(server, token, playerId, username) {
    this.server = server;
    this.token = token;
    this.playerId = playerId;
    this.username = username;
    this.table = null;
    this.states = []; //   every tableState received
    this.errors = [];
    this.rejected = [];
    this.mode = 'idle'; //  'idle' | 'passive' (check or call) | 'shove' (all-in or call)
    this.lastActed = '';
  }

  async connect() {
    const socket = io(this.server.url, { auth: { token: this.token }, transports: ['websocket'], reconnection: false });
    this.socket = socket;
    socket.on('tableState', (state) => {
      this.table = state;
      this.states.push(state);
      this.maybeAct();
    });
    socket.on('errorMessage', (e) => this.errors.push(e.message));
    socket.on('actionRejected', (e) => this.rejected.push(e.reason));
    await new Promise((resolve, reject) => {
      socket.once('connect_error', (err) => reject(new Error(`connect_error: ${err.message}`)));
      socket.once('session', resolve);
    });
    return this;
  }

  emit(event, payload) {
    this.socket.emit(event, payload);
  }

  close() {
    this.socket?.close();
  }

  get mySeat() {
    return this.table?.you?.seat ?? -1;
  }

  async createTable(options) {
    this.emit('createTable', options);
    await until(() => this.table && this.table.you.seat >= 0, 3000, 'seat at new table');
    return this.table.code;
  }

  async join(code) {
    this.emit('joinTable', { code });
    await until(() => this.table?.code === code && this.table.you.seat >= 0, 3000, `seat at ${code}`);
  }

  maybeAct() {
    const legal = this.table?.hand?.legal;
    if (this.mode === 'idle' || !legal) return;
    const key = `${this.table.handNumber}:${this.table.hand.stage}:${this.table.hand.pot}:${legal.toCall}`;
    if (key === this.lastActed) return;
    this.lastActed = key;
    const { actions } = legal;
    let action;
    if (this.mode === 'shove') action = actions.includes('allIn') ? 'allIn' : 'call';
    else action = actions.includes('check') ? 'check' : actions.includes('call') ? 'call' : 'allIn';
    this.emit('playerAction', { action });
  }
}

module.exports = { TestServer, Player, sleep, until };
