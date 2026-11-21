// Socket.IO event names. Mirrored in Unity: Assets/Scripts/Network/PokerEvents.cs
module.exports = Object.freeze({
  // Client -> Server
  CREATE_TABLE: 'createTable', // { tableName, mode: 'bots' | 'friends', maxSeats: 2..6 }
  JOIN_TABLE: 'joinTable', //     { code }
  LEAVE_TABLE: 'leaveTable',
  START_GAME: 'startGame', //     host only: start a friends table before every seat is filled
  GET_STATS: 'getStats', //       your own statistics (answered with STATS)
  LIST_TABLES: 'listTables',
  PLAYER_ACTION: 'playerAction', // { action, amount? }  amount = total bet to raise to
  REBUY: 'rebuy', //               out of chips: restart with the starting stack

  // Server -> Client
  SESSION: 'session', //           { playerId, username, tableCode }  sent after the login token is accepted;
  //                               tableCode = the table you are seated at, null if none (e.g. after a server restart)
  STATS: 'stats', //               { handsPlayed, handsWon, biggestPot, bestHand, bestHandRank }
  TABLE_LIST: 'tableList',
  TABLE_STATE: 'tableState', //    public table snapshot (+ your own cards once hands run)
  ACTION_REJECTED: 'actionRejected', // { reason }
  ERROR: 'errorMessage', //        { message }
});
