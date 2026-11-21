/**
 * Chat history per table, in memory (it goes when the table closes). Two kinds:
 *   player  - typed by a player at the table (already cleaned by chatText)
 *   system  - written by the server: "Bob joined the table", "Ava won 300 with Flush"
 *
 * Message: { id, kind, playerId, name, text, time }
 */
class ChatStore {
  constructor(historySize) {
    this.historySize = historySize;
    this.byTable = new Map(); // code -> messages, oldest first
    this.nextId = 1;
  }

  addPlayerMessage(code, { playerId, name }, text) {
    return this.add(code, { kind: 'player', playerId, name, text });
  }

  addSystemMessage(code, text) {
    return this.add(code, { kind: 'system', playerId: null, name: null, text });
  }

  add(code, fields) {
    const message = { id: this.nextId++, ...fields, time: Date.now() };
    const list = this.byTable.get(code) || [];
    list.push(message);
    if (list.length > this.historySize) list.splice(0, list.length - this.historySize);
    this.byTable.set(code, list);
    return message;
  }

  history(code) {
    return [...(this.byTable.get(code) || [])];
  }

  clear(code) {
    this.byTable.delete(code);
  }
}

module.exports = ChatStore;
