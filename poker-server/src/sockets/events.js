// Socket.IO event names. Mirrored in Unity: Assets/Scripts/Network/PokerEvents.cs
module.exports = Object.freeze({
  // Client -> Server
  CREATE_TABLE: 'createTable', // { tableName, mode: 'bots' | 'friends', maxSeats: 2..6 }
  JOIN_TABLE: 'joinTable', //     { code }
  LEAVE_TABLE: 'leaveTable',
  START_GAME: 'startGame', //     host only: start a friends table before every seat is filled
  GET_STATS: 'getStats', //       your own statistics (answered with STATS)
  GET_HAND_HISTORY: 'getHandHistory', // your last hands (answered with HAND_HISTORY)
  LIST_TABLES: 'listTables',
  PLAYER_ACTION: 'playerAction', // { action, amount? }  amount = total bet to raise to
  BUY_ITEM: 'buyItem', //          shop: { itemId }  (dummy purchase, no real money)
  GET_WALLET: 'getWallet', //      answered with WALLET and SHOP_CATALOG
  REBUY: 'rebuy', //               out of chips: restart with the starting stack

  // Server -> Client
  SESSION: 'session', //           { playerId, username, tableCode }  sent after the login token is accepted;
  //                               tableCode = the table you are seated at, null if none (e.g. after a server restart)
  WALLET: 'wallet', //             { balance }  your chips outside any table; sent after every change
  SHOP_CATALOG: 'shopCatalog', //  { items: [{ id, name, chips, price }] }
  SHOP_RESULT: 'shopResult', //    { ok, itemId, message }
  STATS: 'stats', //               { handsPlayed, handsWon, biggestPot, bestHand, bestHandRank }
  HAND_HISTORY: 'handHistory', //  { hands: [...] }  newest first, as you saw them (services/handRecord.js)
  TABLE_LIST: 'tableList',
  TABLE_STATE: 'tableState', //    public table snapshot (+ your own cards once hands run)
  ACTION_REJECTED: 'actionRejected', // { reason }
  SIGNED_IN_ELSEWHERE: 'signedInElsewhere', // sent to an older tab/socket when the same account connects again; it is then disconnected
  ERROR: 'errorMessage', //        { message }
});
