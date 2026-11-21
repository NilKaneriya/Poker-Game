// Storage backends to run the same tests against.
//   always:            in-memory JSON, JSON files in a temp folder
//   TEST_MONGO=1:      also a real MongoDB (mongodb-memory-server downloads one the first time)
// Usage in a test file:  for (const backend of storageBackends()) test(`... (${backend.name})`, ...)
const fs = require('fs');
const os = require('os');
const path = require('path');
const { createStorage } = require('../src/storage');

let mongoServer = null; // one MongoDB for the whole test file
let dbCounter = 0;

async function startMongo() {
  if (!mongoServer) {
    const { MongoMemoryServer } = require('mongodb-memory-server');
    mongoServer = await MongoMemoryServer.create();
  }
  return mongoServer.getUri();
}

function storageBackends() {
  const backends = [
    { name: 'memory', open: () => createStorage({ dataDir: null }) },
    {
      name: 'json files',
      open: () => createStorage({ dataDir: fs.mkdtempSync(path.join(os.tmpdir(), 'poker-store-')) }),
    },
  ];
  if (process.env.TEST_MONGO === '1') {
    backends.push({
      name: 'mongodb',
      // A fresh database per test, so tests never see each other's data.
      open: async () => createStorage({ mongoUri: await startMongo(), mongoDb: `test${++dbCounter}` }),
    });
  }
  return backends;
}

async function stopMongo() {
  if (mongoServer) await mongoServer.stop();
  mongoServer = null;
}

module.exports = { storageBackends, stopMongo };
