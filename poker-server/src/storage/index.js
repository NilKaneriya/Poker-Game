const fs = require('fs');
const path = require('path');
const JsonCollection = require('./JsonCollection');
const MongoCollection = require('./MongoCollection');

/**
 * Where player data lives: users (account + profile + settings), login tokens, stats and hand histories.
 *
 *   MONGODB_URI set  -> MongoDB (production; survives redeploys)
 *   not set          -> JSON files in DATA_DIR (runs anywhere with nothing to install)
 *
 * Both give the same collections with the same API, so the services are written once.
 *
 * @param {{ mongoUri?: string, mongoDb?: string, dataDir?: string|null }} options
 *        dataDir = null keeps the JSON backend in memory (tests)
 */
async function createStorage({ mongoUri, mongoDb = 'poker', dataDir = null }) {
  if (mongoUri) return createMongoStorage(mongoUri, mongoDb);
  return createJsonStorage(dataDir);
}

async function createMongoStorage(uri, dbName) {
  const { MongoClient } = require('mongodb'); // only loaded when MongoDB is used
  const client = new MongoClient(uri, { serverSelectionTimeoutMS: 10000 });
  await client.connect();
  const db = client.db(dbName);

  await Promise.all([
    db.collection('users').createIndex({ usernameLower: 1 }, { unique: true }),
    db.collection('tokens').createIndex({ expireAt: 1 }, { expireAfterSeconds: 0 }), // MongoDB deletes expired tokens
  ]);

  return {
    kind: 'mongodb',
    users: new MongoCollection(db.collection('users')),
    tokens: new MongoCollection(db.collection('tokens')),
    wallets: new MongoCollection(db.collection('wallets')),
    stats: new MongoCollection(db.collection('stats')),
    handHistory: new MongoCollection(db.collection('handHistory')),
    close: () => client.close(),
  };
}

function createJsonStorage(dataDir) {
  const file = (name) => (dataDir ? path.join(dataDir, `${name}.json`) : null);
  if (dataDir) migrateLegacyAccounts(dataDir);
  const collections = {
    users: new JsonCollection(file('users')),
    tokens: new JsonCollection(file('tokens')),
    wallets: new JsonCollection(file('wallets')),
    stats: new JsonCollection(file('stats')),
    handHistory: new JsonCollection(file('handHistory')),
  };
  return {
    kind: dataDir ? 'json' : 'memory',
    ...collections,
    // Writes are batched (see JsonCollection); closing writes whatever is still pending.
    close: () => Promise.all(Object.values(collections).map((c) => c.flush())),
  };
}

/** Earlier versions kept users and tokens together in accounts.json; split it once. */
function migrateLegacyAccounts(dataDir) {
  const legacy = path.join(dataDir, 'accounts.json');
  const usersFile = path.join(dataDir, 'users.json');
  if (!fs.existsSync(legacy) || fs.existsSync(usersFile)) return;
  try {
    const { users = {}, tokens = {} } = JSON.parse(fs.readFileSync(legacy, 'utf8'));
    fs.writeFileSync(usersFile, JSON.stringify(users, null, 2));
    fs.writeFileSync(path.join(dataDir, 'tokens.json'), JSON.stringify(tokens, null, 2));
  } catch {
    // Unreadable legacy file: start fresh rather than refuse to boot.
  }
}

module.exports = { createStorage };
