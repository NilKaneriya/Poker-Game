const EVENTS = require('./events');
const { store } = require('../services/AccountStore');
const tables = require('../services/TableManager');
const log = require('../utils/logger');
const { playerRoom, tableRoom } = require('./broadcast');
const { runnerFor } = require('./TableRunner');
const registerLobbyHandlers = require('./handlers/lobbyHandlers');
const registerGameHandlers = require('./handlers/gameHandlers');

module.exports = (io) => {
  // Every socket must present a valid login token (from /auth/login) in the handshake.
  io.use((socket, next) => {
    const user = store.resolveToken(socket.handshake.auth?.token);
    if (!user) return next(new Error('unauthorized'));
    socket.data.playerId = user.id;
    socket.data.name = user.username;
    next();
  });

  io.on('connection', (socket) => {
    const { playerId, name } = socket.data;
    socket.join(playerRoom(playerId));
    socket.emit(EVENTS.SESSION, { playerId, username: name });
    log.info(`Connected ${name} (socket ${socket.id})`);

    // Reconnect: put the player back at their table.
    const table = tables.findByPlayer(playerId);
    if (table) {
      table.getSeat(playerId).connected = true;
      socket.join(tableRoom(table.code));
      runnerFor(io, table).update();
      log.info(`${name} rejoined table ${table.code}`);
    }

    registerLobbyHandlers(io, socket);
    registerGameHandlers(io, socket);

    socket.on('disconnect', async (reason) => {
      log.info(`Disconnected ${name} (${reason})`);
      // Same account may have another socket open (e.g. second tab); only mark
      // the seat offline when none are left. The seat is kept so the player can return.
      // TODO: remove the seat after config.reconnectGraceMs.
      const stillConnected = (await io.in(playerRoom(playerId)).fetchSockets()).length > 0;
      const current = tables.findByPlayer(playerId);
      if (current && !stillConnected) {
        current.getSeat(playerId).connected = false;
        runnerFor(io, current).update();
      }
    });
  });
};
