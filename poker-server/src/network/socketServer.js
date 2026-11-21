const EVENTS = require('./events');
const { store, nameOf } = require('../services/AccountStore');
const { defaultAvatarFor } = require('../services/profileRules');
const tables = require('../services/TableManager');
const log = require('../utils/logger');
const { playerRoom, tableRoom, sendStats } = require('./broadcast');
const { runnerFor } = require('./TableRunner');
const { startGrace, cancelGrace } = require('./reconnectGrace');
const registerLobbyHandlers = require('./handlers/lobbyHandlers');
const registerGameHandlers = require('./handlers/gameHandlers');

module.exports = (io) => {
  // Every socket must present a valid login token (from /auth/login) in the handshake.
  io.use(async (socket, next) => {
    try {
      const user = await store.resolveToken(socket.handshake.auth?.token);
      if (!user) return next(new Error('unauthorized'));
      socket.data.playerId = user.id;
      socket.data.name = nameOf(user); // display name; kept up to date by profileSync
      socket.data.avatarId = user.avatarId ?? defaultAvatarFor(user.id);
      next();
    } catch (err) {
      log.error(`Login check failed: ${err.message}`);
      next(new Error('server error'));
    }
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
      table.setConnected(playerId, true);
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
        current.setConnected(playerId, false);
        runnerFor(io, current).update();
        startGrace(io, playerId, name);
      }
    });
  });
};
