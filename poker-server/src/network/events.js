// Socket.IO event names. Mirrored in Unity: Assets/Scripts/Network/PokerEvents.cs
module.exports = Object.freeze({
  // Client -> Server
  CREATE_TABLE: 'createTable', // { tableName }
  JOIN_TABLE: 'joinTable', //     { code }
  LEAVE_TABLE: 'leaveTable',
  LIST_TABLES: 'listTables',
  PLAYER_ACTION: 'playerAction', // { action, amount? }  amount = total bet to raise to
  REBUY: 'rebuy', //               out of chips: restart with the starting stack

  // Server -> Client
  SESSION: 'session', //           { playerId, username }  sent after the login token is accepted
  TABLE_LIST: 'tableList',
  TABLE_STATE: 'tableState', //    public table snapshot (+ your own cards once hands run)
  ACTION_REJECTED: 'actionRejected', // { reason }
  ERROR: 'errorMessage', //        { message }
});
