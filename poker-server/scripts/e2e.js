// Full end-to-end run with no setup: starts a throwaway server with fast timings and its
// own data, runs scripts/test-client.js against it, then shuts the server down.
// Usage: npm run test:e2e          (JSON files)
//        npm run test:e2e:mongo    (a throwaway MongoDB from mongodb-memory-server)
const { spawn } = require('child_process');
const fs = require('fs');
const os = require('os');
const path = require('path');

const PORT = 3100;
const GRACE_MS = 1500;
const USE_MONGO = process.argv.includes('--mongo');
const dataDir = fs.mkdtempSync(path.join(os.tmpdir(), 'poker-e2e-'));
let server = null;
let mongo = null;

async function startServer() {
  let mongoUri = '';
  if (USE_MONGO) {
    const { MongoMemoryServer } = require('mongodb-memory-server');
    mongo = await MongoMemoryServer.create();
    mongoUri = mongo.getUri();
    console.log('Using a throwaway MongoDB');
  }
  server = spawn(process.execPath, [path.join(__dirname, '..', 'src', 'index.js')], {
    env: {
      ...process.env,
      PORT: String(PORT),
      NEXT_HAND_DELAY_MS: '200',
      RESULT_SHOW_MS: '300',
      BOT_THINK_MS: '30',
      RECONNECT_GRACE_MS: String(GRACE_MS),
      DATA_DIR: dataDir,
      MONGODB_URI: mongoUri,
      MONGODB_DB: 'poker_e2e',
      WEBGL_DIR: '',
    },
    stdio: ['ignore', 'ignore', 'inherit'],
  });
}

async function finish(code) {
  server?.kill();
  if (mongo) await mongo.stop();
  fs.rmSync(dataDir, { recursive: true, force: true });
  process.exit(code);
}

async function waitForServer() {
  for (let i = 0; i < 50; i++) {
    try {
      if ((await fetch(`http://localhost:${PORT}/health`)).ok) return;
    } catch {
      // not up yet
    }
    await new Promise((r) => setTimeout(r, 100));
  }
  throw new Error('test server did not start');
}

startServer()
  .then(waitForServer)
  .then(() => {
    const client = spawn(process.execPath, [path.join(__dirname, 'test-client.js')], {
      env: { ...process.env, SERVER_URL: `http://localhost:${PORT}`, TEST_GRACE_MS: String(GRACE_MS) },
      stdio: 'inherit',
    });
    client.on('exit', (code) => finish(code ?? 1));
  })
  .catch((e) => {
    console.error('FAIL:', e.message);
    finish(1);
  });
