const EVENTS = require('./events');
const { store } = require('../services/AccountStore');
const tables = require('../services/TableManager');
const log = require('../utils/logger');
const { playerRoom, tableRoom, sendStats } = require('./broadcast');
const { runnerFor } = require('./TableRunner');
const { startGrace, cancelGrace } = require('./reconnectGrace');
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
    log.info(`Connected ${name} (socket ${socket.id})`);

    // Reconnect: put the player back at their table, seat and chips as they were.
    cancelGrace(playerId);
    const table = tables.findByPlayer(playerId);
    // tableCode tells the client whether its table still exists (it doesn't after a server restart).
    socket.emit(EVENTS.SESSION, { playerId, username: name, tableCode: table?.code ?? null });
    sendStats(io, playerId);
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
      // The same account may have another socket open (e.g. a second tab); only mark
      // the seat offline when none are left. The seat is kept for the grace period.
      const stillConnected = (await io.in(playerRoom(playerId)).fetchSockets()).length > 0;
      const current = tables.findByPlayer(playerId);
      if (current && !stillConnected) {
        current.getSeat(playerId).connected = false;
        runnerFor(io, current).update();
        startGrace(io, playerId, name);
      }
    });
  });
};
