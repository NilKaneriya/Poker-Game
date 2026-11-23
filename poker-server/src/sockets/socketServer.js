const EVENTS = require('./events');
const { store, nameOf } = require('../services/AccountStore');
const { defaultAvatarFor } = require('../services/profileRules');
const tables = require('../services/TableManager');
const log = require('../utils/logger');
const { playerRoom, tableRoom, sendStats, sendWallet } = require('./broadcast');
const { runnerFor } = require('../game/TableRunner');
const { startGrace, cancelGrace } = require('../game/reconnectGrace');
const registerLobbyHandlers = require('./handlers/lobbyHandlers');
const registerGameHandlers = require('./handlers/gameHandlers');
const registerShopHandlers = require('./handlers/shopHandlers');
const wallet = require('../services/WalletService');

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

  io.on('connection', async (socket) => {
    const { playerId, name } = socket.data;

    // One live session per account: an older tab/socket is told and disconnected, so it
    // can't keep showing a seat it no longer controls. The seat itself stays (same player).
    const older = (await io.in(playerRoom(playerId)).fetchSockets()).filter((s) => s.id !== socket.id);
    for (const old of older) {
      old.emit(EVENTS.SIGNED_IN_ELSEWHERE, { message: 'You signed in from another tab or device.' });
      old.disconnect(true);
    }
    socket.join(playerRoom(playerId));
    log.info(`Connected ${name} (socket ${socket.id})`);

    // Reconnect: put the player back at their table, seat and chips as they were.
    cancelGrace(playerId);
    const table = tables.findByPlayer(playerId);
    // Chips handed to a table that no longer exists (server restart) go back to the wallet.
    try {
      await wallet.reconcile(playerId, Boolean(table));
    } catch (err) {
      log.error(`Wallet check failed for ${playerId}: ${err.message}`);
    }
    // tableCode tells the client whether its table still exists (it doesn't after a server restart).
    socket.emit(EVENTS.SESSION, { playerId, username: name, tableCode: table?.code ?? null });
    sendStats(io, playerId);
    sendWallet(io, playerId);
    if (table) {
      table.setConnected(playerId, true);
      socket.join(tableRoom(table.code));
      runnerFor(io, table).update();
      log.info(`${name} rejoined table ${table.code}`);
    }

    registerLobbyHandlers(io, socket);
    registerGameHandlers(io, socket);
    registerShopHandlers(io, socket);

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
