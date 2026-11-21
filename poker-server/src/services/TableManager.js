const { randomInt } = require('crypto');
const Table = require('../engine/Table');
const { TABLE_MODES } = require('../engine/constants');
const config = require('../config');

// No 0/O or 1/I so codes are easy to read out loud.
const CODE_CHARS = 'ABCDEFGHJKLMNPQRSTUVWXYZ23456789';
const CODE_LENGTH = 5;

class TableManager {
  constructor() {
    this.tables = new Map(); // code -> Table
  }

  generateCode() {
    let code;
    do {
      code = Array.from({ length: CODE_LENGTH }, () => CODE_CHARS[randomInt(CODE_CHARS.length)]).join('');
    } while (this.tables.has(code));
    return code;
  }

  /** @param {{ name?: string, mode: string, maxSeats: number, hostId: string }} options */
  create({ name, mode, maxSeats, hostId }) {
    const code = this.generateCode();
    const table = new Table({ ...config.table, code, name: name || `Table ${code}`, mode, maxSeats, hostId });
    this.tables.set(code, table);
    return table;
  }

  get(code) {
    return this.tables.get(String(code || '').trim().toUpperCase());
  }

  remove(code) {
    this.tables.delete(code);
  }

  findByPlayer(playerId) {
    for (const table of this.tables.values()) {
      if (table.hasMember(playerId)) return table; // seated, or waiting for a seat
    }
    return null;
  }

  /**
   * Tables shown in the lobby list. Games against bots are not listed, but a friend
   * with the code can still join one (they take a bot's seat after the current hand).
   */
  list() {
    return [...this.tables.values()]
      .filter((t) => t.mode === TABLE_MODES.FRIENDS)
      .map((t) => ({
        code: t.code,
        name: t.name,
        mode: t.mode,
        players: t.players.length,
        maxSeats: t.maxSeats,
        blinds: `${t.smallBlind}/${t.bigBlind}`,
        started: t.started,
        inHand: Boolean(t.hand),
      }));
  }
}

module.exports = new TableManager();
