const { randomInt } = require('crypto');
const Table = require('../engine/Table');
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

  create(name) {
    const code = this.generateCode();
    const table = new Table({ code, name: name || `Table ${code}`, ...config.table });
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
      if (table.findSeat(playerId) !== -1) return table;
    }
    return null;
  }

  list() {
    return [...this.tables.values()].map((t) => ({
      code: t.code,
      name: t.name,
      players: t.players.length,
      maxSeats: t.maxSeats,
      blinds: `${t.smallBlind}/${t.bigBlind}`,
      inHand: Boolean(t.hand),
    }));
  }
}

module.exports = new TableManager();
