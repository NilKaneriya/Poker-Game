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
  constructor(port, env = {}, dataDir = null) {
    this.port = port;
    this.url = `http://127.0.0.1:${port}`;
    this.env = env;
    this.dataDir = dataDir || fs.mkdtempSync(path.join(os.tmpdir(), 'poker-eco-'));
    this.proc = null;
  }

  async start() {
    this.proc = spawn(process.execPath, [path.join(__dirname, '..', 'src', 'index.js')], {
      env: {
        ...process.env,
        PORT: String(this.port),
        NEXT_HAND_DELAY_MS: '100',
        RESULT_SHOW_MS: '200',
        BOT_THINK_MS: '300',
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

  /** Hard stop, like a crash (no clean shutdown). */
  async kill() {
    if (!this.proc || this.proc.exitCode !== null) return;
    const exited = new Promise((r) => this.proc.once('exit', r));
    this.proc.kill('SIGKILL');
    await exited;
  }

  async stop() {
    await this.kill();
    try {
      fs.rmSync(this.dataDir, { recursive: true, force: true, maxRetries: 5, retryDelay: 100 });
    } catch {
      // never fail a test run over temp cleanup
    }
  }
}

let counter = 0;

/** One account + one socket, remembering everything the server sends. */
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
    this.reset();
  }

  reset() {
    this.socket = null;
    this.wallet = null; //  last wallet balance
    this.table = null; //   last tableState
    this.session = null;
    this.errors = []; //    errorMessage texts
    this.rejected = []; //  actionRejected reasons
    this.shop = []; //      shopResult events
    this.kicked = false; // got signedInElsewhere
    this.log = []; //       every event name
    this.autoPlay = false;
    this.lastActed = '';
  }

  async connect() {
    this.reset();
    const socket = io(this.server.url, { auth: { token: this.token }, transports: ['websocket'], reconnection: false });
    this.socket = socket;
    socket.onAny((event, data) => {
      this.log.push(event);
      if (event === 'wallet') this.wallet = data.balance;
      if (event === 'tableState') {
        this.table = data;
        this.maybeAct();
      }
      if (event === 'session') this.session = data;
      if (event === 'errorMessage') this.errors.push(data.message);
      if (event === 'actionRejected') this.rejected.push(data.reason);
      if (event === 'shopResult') this.shop.push(data);
      if (event === 'signedInElsewhere') this.kicked = true;
    });
    await new Promise((resolve, reject) => {
      socket.once('connect_error', (err) => reject(new Error(`connect_error: ${err.message}`)));
      socket.once('session', resolve);
    });
    await until(() => this.wallet !== null, 3000, 'wallet event');
    return this;
  }

  emit(event, payload) {
    this.socket.emit(event, payload);
  }

  close() {
    this.socket?.close();
  }

  /** Ask the server again and wait for a fresh wallet value. */
  async refreshWallet() {
    this.wallet = null;
    this.emit('getWallet');
    return until(() => this.wallet !== null, 3000, 'wallet refresh').then(() => this.wallet);
  }

  async lastShop(count) {
    await until(() => this.shop.length >= count, 3000, `${count} shop results`);
    return this.shop[count - 1];
  }

  get mySeat() {
    return this.table?.you?.seat ?? -1;
  }

  get myChips() {
    return this.table?.seats?.[this.mySeat]?.chips;
  }

  async createTable(options = { tableName: 'T', mode: 'friends', maxSeats: 2 }) {
    this.emit('createTable', options);
    await until(() => this.table && this.table.you.seat >= 0, 3000, 'seat at new table');
    return this.table.code;
  }

  async join(code) {
    this.emit('joinTable', { code });
    await until(() => this.table?.code === code && this.table.you.seat >= 0, 3000, `seat at ${code}`);
  }

  /** Always shove: all-in if offered, otherwise call. Gets two players to a showdown fast. */
  maybeAct() {
    const legal = this.table?.hand?.legal;
    if (!this.autoPlay || !legal) return;
    const key = `${this.table.handNumber}:${this.table.hand.stage}:${this.table.hand.pot}:${legal.toCall}`;
    if (key === this.lastActed) return;
    this.lastActed = key;
    this.emit('playerAction', { action: legal.actions.includes('allIn') ? 'allIn' : 'call' });
  }
}

module.exports = { TestServer, Player, sleep, until };
