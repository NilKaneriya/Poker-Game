/**
 * The dummy shop: free demo chips only. There are no prices and no payment of any kind.
 * The server owns this list; clients send an item id and nothing else, so a client can
 * never choose an amount or a price.
 */
const ITEMS = Object.freeze([
  Object.freeze({ id: 'chips_small', name: 'Handful of chips', chips: 5000, price: 'Free (demo)' }),
  Object.freeze({ id: 'chips_medium', name: 'Stack of chips', chips: 25000, price: 'Free (demo)' }),
  Object.freeze({ id: 'chips_large', name: 'Chip tower', chips: 100000, price: 'Free (demo)' }),
]);

const byId = new Map(ITEMS.map((item) => [item.id, item]));

/** The item for an id, or null. Only ids in the catalog match (no prototype keys like "constructor"). */
function findItem(id) {
  return typeof id === 'string' ? byId.get(id) || null : null;
}

module.exports = { ITEMS, findItem };
