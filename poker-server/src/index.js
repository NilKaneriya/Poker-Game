const http = require('http');
const express = require('express');
const cors = require('cors');
const { Server } = require('socket.io');

const config = require('./config');
const log = require('./utils/logger');
const { createStorage } = require('./storage');
const { store: accounts } = require('./services/AccountStore');
const { profiles } = require('./services/ProfileService');
const stats = require('./services/StatsService');
const handHistory = require('./services/HandHistoryService');
const tables = require('./services/TableManager');
const authRoutes = require('./http/authRoutes');
const { createProfileRoutes } = require('./http/profileRoutes');
const { syncProfile } = require('./sockets/profileSync');
const registerSocketServer = require('./sockets/socketServer');
const { serveWebGL } = require('./http/webglHosting');
const { tableRoom } = require('./sockets/broadcast');
const gameEvents = require('./game/gameEvents');
const { attachChat } = require('./chat');
const { attachReactions } = require('./reactions');

async function main() {
  // 1. Storage first: nothing can log in or save stats without it.
  const storage = await createStorage(config.storage);
  accounts.attach(storage);
  profiles.attach(storage);
  stats.attach(storage);
  handHistory.attach(storage);
  log.info(
    `Player data: ${storage.kind === 'mongodb' ? `MongoDB (database "${config.storage.mongoDb}")` : `JSON files in ${config.storage.dataDir}`}`,
  );

  // 2. HTTP: health, tables, login, profile, and the WebGL game.
  const app = express();
  app.use(cors({ origin: config.corsOrigin }));
  app.use(express.json({ limit: '10kb' }));

  const server = http.createServer(app);
  const io = new Server(server, { cors: { origin: config.corsOrigin } });

  app.get('/health', (req, res) => res.json({ status: 'ok' }));
  app.get('/tables', (req, res) => res.json(tables.list()));
  app.use('/auth', authRoutes);
  app.use('/profile', createProfileRoutes({ onProfileChanged: (profile) => syncProfile(io, profile) }));
  const servingGame = serveWebGL(app, config.webglDir);

  // 3. Real-time game.
  registerSocketServer(io);

  // Chat is a separate module: it only gets what it needs from the game, passed in here.
  attachChat(io, {
    tableOf: (playerId) => tables.findByPlayer(playerId)?.code ?? null,
    roomOf: tableRoom,
    gameEvents,
  });

  // Reactions (emotes) are another stand-alone module: a "group" here is a table.
  attachReactions(io, {
    groupOf: (playerId) => tables.findByPlayer(playerId)?.code ?? null,
    roomOf: tableRoom,
    isInGroup: (code, playerId) => tables.get(code)?.hasMember(playerId) ?? false,
  });

  server.listen(config.port, () => {
    log.info(`Poker server listening on http://localhost:${config.port}`);
    if (servingGame) log.info(`Serving the WebGL game from ${config.webglDir} at http://localhost:${config.port}/`);
  });

  // Close the database cleanly on Ctrl+C / platform shutdown.
  const shutdown = async () => {
    server.close();
    await storage.close();
    process.exit(0);
  };
  process.on('SIGINT', shutdown);
  process.on('SIGTERM', shutdown);
}

main().catch((err) => {
  log.error(`Server failed to start: ${err.message}`);
  if (config.storage.mongoUri) log.error('Check MONGODB_URI (and that this machine is allowed to reach the database).');
  process.exit(1);
});
