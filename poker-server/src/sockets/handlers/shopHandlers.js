const EVENTS = require('../events');
const wallet = require('../../services/WalletService');
const { ITEMS } = require('../../services/shopCatalog');
const { sendWallet } = require('../broadcast');
const log = require('../../utils/logger');

/** The dummy shop: the client may only ask to buy a catalog item by id; the server decides everything else. */
module.exports = (io, socket) => {
  const { playerId } = socket.data;
  const catalog = () => socket.emit(EVENTS.SHOP_CATALOG, { items: ITEMS });

  catalog();
  socket.on(EVENTS.GET_WALLET, () => {
    catalog();
    sendWallet(io, playerId);
  });

  socket.on(EVENTS.BUY_ITEM, async (payload) => {
    const itemId = payload && typeof payload === 'object' ? payload.itemId : undefined;
    const shown = typeof itemId === 'string' ? itemId.slice(0, 40) : '';
    try {
      const result = await wallet.purchase(playerId, itemId);
      if (!result.ok) return socket.emit(EVENTS.SHOP_RESULT, { ok: false, itemId: shown, message: result.error });
      log.info(`${socket.data.name} bought ${result.item.id} (demo): +${result.item.chips} chips`);
      socket.emit(EVENTS.SHOP_RESULT, {
        ok: true,
        itemId: shown,
        message: `+${result.item.chips} chips added (demo purchase, nothing was charged)`,
      });
      sendWallet(io, playerId);
    } catch (err) {
      log.error(`Shop purchase failed for ${playerId}: ${err.message}`);
      socket.emit(EVENTS.SHOP_RESULT, { ok: false, itemId: shown, message: 'The shop is unavailable right now' });
    }
  });
};
