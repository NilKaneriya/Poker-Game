const fs = require('fs');
const path = require('path');

const copy = (doc) => (doc ? JSON.parse(JSON.stringify(doc)) : null);

/**
 * A collection of documents saved as one JSON file ({ id: doc }). Same async API as
 * MongoCollection, so services don't care which one they get.
 * filePath = null keeps everything in memory (tests).
 */
class JsonCollection {
  constructor(filePath) {
    this.filePath = filePath;
    this.docs = {};
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

  save() {
    if (!this.filePath) return;
    fs.mkdirSync(path.dirname(this.filePath), { recursive: true });
    fs.writeFileSync(this.filePath, JSON.stringify(this.docs, null, 2));
  }
}

module.exports = JsonCollection;
