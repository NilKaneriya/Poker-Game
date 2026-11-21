const { EventEmitter } = require('events');

/**
 * Things that happen at tables, for any other module to react to (today: chat's
 * system messages). The game publishes; it never knows who listens, so features
 * like chat can be added or removed without touching game code.
 *
 * Every payload has the table `code`.
 */
const GAME_EVENTS = Object.freeze({
  PLAYER_JOINED: 'playerJoined', //   { code, playerId, name }  took a seat
  PLAYER_WAITING: 'playerWaiting', // { code, playerId, name }  joined a bots table mid-hand
  PLAYER_SEATED: 'playerSeated', //   { code, playerId, name }  waited, now has a seat
  PLAYER_LEFT: 'playerLeft', //       { code, playerId, name }
  BOT_REPLACED: 'botReplaced', //     { code, name }            a bot stood up for a waiting player
  HAND_FINISHED: 'handFinished', //   { code, handNumber, winners: [{ playerId, name, amount, handName }] }
  TABLE_CLOSED: 'tableClosed', //     { code }
});

const gameEvents = new EventEmitter();
gameEvents.GAME_EVENTS = GAME_EVENTS;

module.exports = gameEvents;
