const http = require('http');
const express = require('express');
const cors = require('cors');
const { Server } = require('socket.io');

const config = require('./config');
const log = require('./utils/logger');
const tables = require('./services/TableManager');
const authRoutes = require('./network/authRoutes');
const registerSocketServer = require('./network/socketServer');
const { serveWebGL } = require('./network/webglHosting');
const { tableRoom } = require('./network/broadcast');
const gameEvents = require('./network/gameEvents');
const { attachChat } = require('./chat');

const app = express();
app.use(cors({ origin: config.corsOrigin }));
app.use(express.json({ limit: '10kb' }));

app.get('/health', (req, res) => res.json({ status: 'ok' }));
app.get('/tables', (req, res) => res.json(tables.list()));
app.use('/auth', authRoutes);
const servingGame = serveWebGL(app, config.webglDir);

const server = http.createServer(app);
const io = new Server(server, { cors: { origin: config.corsOrigin } });

registerSocketServer(io);

// Chat is a separate module: it only gets what it needs from the game, passed in here.
attachChat(io, {
  tableOf: (playerId) => tables.findByPlayer(playerId)?.code ?? null,
  roomOf: tableRoom,
  gameEvents,
});

server.listen(config.port, () => {
  log.info(`Poker server listening on http://localhost:${config.port}`);
  if (servingGame) log.info(`Serving the WebGL game from ${config.webglDir} at http://localhost:${config.port}/`);
});
