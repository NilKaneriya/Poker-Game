const fs = require('fs');
const path = require('path');

const copy = (doc) => (doc ? JSON.parse(JSON.stringify(doc)) : null);

/**
 * A collection of documents saved as one JSON file ({ id: doc }). Same async API as
 * MongoCollection, so services don't care which one they get.
 * filePath = null keeps everything in memory (tests).
 *
 * Writes are batched: changes mark the collection dirty and the file is written at most
 * once per `saveDelayMs`, in the background (temp file + rename, so a crash mid-write
 * never leaves half a file). close() / flush() write any pending change at once.
 * Writing the whole file on every change blocked the server for hundreds of ms under
 * load (found by scripts/load-test.js). The trade-off: a hard crash can lose the last
 * second of changes, which is fine for the no-setup backend; production uses MongoDB.
 */
class JsonCollection {
  constructor(filePath, { saveDelayMs = 1000 } = {}) {
    this.filePath = filePath;
    this.saveDelayMs = saveDelayMs;
    this.docs = {};
    this.dirty = false;
    this.timer = null;
    this.writing = null; // the write in progress, if any
    this.writes = 0; //     files written (tests check that changes are batched)
    if (filePath) {
      try {
        this.docs = JSON.parse(fs.readFileSync(filePath, 'utf8'));
      } catch {
        // No file yet (first run) - start empty.
      }
    }
  }

  async get(id) {
    return copy(this.docs[id]);
  }

  async findOne(field, value) {
    return copy(Object.values(this.docs).find((d) => d[field] === value));
  }

  /** Insert or replace. */
  async set(id, doc) {
    this.docs[id] = copy(doc);
    this.save();
  }

  /** Change some top-level fields. Returns the updated doc, or null if there is none. */
  async update(id, fields) {
    if (!this.docs[id]) return null;
    Object.assign(this.docs[id], copy(fields));
    this.save();
    return copy(this.docs[id]);
  }

  async delete(id) {
    if (!(id in this.docs)) return;
    delete this.docs[id];
    this.save();
  }

  /** Mark changed; the file is written within saveDelayMs. */
  save() {
    if (!this.filePath) return;
    this.dirty = true;
    if (!this.timer) this.timer = setTimeout(() => this.flush(), this.saveDelayMs);
  }

  /** Write pending changes now (one write at a time). */
  async flush() {
    if (this.timer) {
      clearTimeout(this.timer);
      this.timer = null;
    }
    while (this.writing) await this.writing;
    if (!this.dirty || !this.filePath) return;
    this.dirty = false;
    this.writes++;
    this.writing = writeAtomic(this.filePath, JSON.stringify(this.docs)).finally(() => {
      this.writing = null;
    });
    await this.writing;
  }
}

async function writeAtomic(file, text) {
  await fs.promises.mkdir(path.dirname(file), { recursive: true });
  const temp = `${file}.tmp`;
  await fs.promises.writeFile(temp, text);
  await fs.promises.rename(temp, file);
}

module.exports = JsonCollection;
