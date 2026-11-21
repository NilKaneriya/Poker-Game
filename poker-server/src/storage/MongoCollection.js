/**
 * A MongoDB collection behind the same small async API as JsonCollection.
 * Documents are stored with _id = id; _id is removed again when reading.
 */
const strip = (doc) => {
  if (!doc) return null;
  const { _id, ...rest } = doc;
  return rest;
};

class MongoCollection {
  /** @param {import('mongodb').Collection} collection */
  constructor(collection) {
    this.collection = collection;
  }

  async get(id) {
    return strip(await this.collection.findOne({ _id: id }));
  }

  async findOne(field, value) {
    return strip(await this.collection.findOne({ [field]: value }));
  }

  async set(id, doc) {
    await this.collection.replaceOne({ _id: id }, { ...doc, _id: id }, { upsert: true });
  }

  async update(id, fields) {
    return strip(await this.collection.findOneAndUpdate({ _id: id }, { $set: fields }, { returnDocument: 'after' }));
  }

  async delete(id) {
    await this.collection.deleteOne({ _id: id });
  }
}

module.exports = MongoCollection;
