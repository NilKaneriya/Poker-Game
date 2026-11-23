const test = require('node:test');
const assert = require('node:assert');

const CONFIG = require.resolve('../src/config');

// Loads a fresh copy of config.js with CORS_ORIGIN set to the given value (or unset).
function corsOriginFor(value) {
  const before = process.env.CORS_ORIGIN;
  if (value === undefined) delete process.env.CORS_ORIGIN;
  else process.env.CORS_ORIGIN = value;
  delete require.cache[CONFIG];
  try {
    return require(CONFIG).corsOrigin;
  } finally {
    if (before === undefined) delete process.env.CORS_ORIGIN;
    else process.env.CORS_ORIGIN = before;
    delete require.cache[CONFIG];
  }
}

test('CORS_ORIGIN=* stays the plain string "*" (an array ["*"] would block every browser)', () => {
  assert.strictEqual(corsOriginFor('*'), '*');
  assert.strictEqual(corsOriginFor(' * '), '*');
});

test('CORS_ORIGIN unset or empty allows any origin', () => {
  assert.strictEqual(corsOriginFor(undefined), '*');
  assert.strictEqual(corsOriginFor(''), '*');
});

test('CORS_ORIGIN lists are split and trimmed', () => {
  assert.deepStrictEqual(corsOriginFor('https://a.com, https://b.com'), ['https://a.com', 'https://b.com']);
});
