const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('fs');
const os = require('os');
const path = require('path');
const { storageBackends, stopMongo } = require('./storageBackends');
const { createStorage } = require('../src/storage');
const { AccountStore } = require('../src/services/AccountStore');
const { ProfileService } = require('../src/services/ProfileService');
const { StatsService } = require('../src/services/StatsService');

test.after(stopMongo);

// The same tests run on every backend (memory, JSON files, and MongoDB with TEST_MONGO=1).
for (const backend of storageBackends()) {
  const withStorage = (fn) => async () => {
    const storage = await backend.open();
    try {
      await fn(storage);
    } finally {
      await storage.close();
    }
  };

  test(`collection: set, get, findOne, update, delete (${backend.name})`, withStorage(async ({ users }) => {
    await users.set('u1', { id: 'u1', usernameLower: 'ann', settings: { a: 1 } });
    assert.deepEqual(await users.get('u1'), { id: 'u1', usernameLower: 'ann', settings: { a: 1 } });
    assert.equal((await users.findOne('usernameLower', 'ann')).id, 'u1');
    assert.equal(await users.findOne('usernameLower', 'nobody'), null);

    const updated = await users.update('u1', { displayName: 'Ann B' });
    assert.equal(updated.displayName, 'Ann B');
    assert.equal(updated.usernameLower, 'ann'); // other fields kept
    assert.equal(await users.update('missing', { x: 1 }), null);

    await users.delete('u1');
    assert.equal(await users.get('u1'), null);
  }));

  test(`collection: returned documents are copies (${backend.name})`, withStorage(async ({ users }) => {
    await users.set('u1', { id: 'u1', settings: { chatEnabled: true } });
    const doc = await users.get('u1');
    doc.settings.chatEnabled = false;
    assert.equal((await users.get('u1')).settings.chatEnabled, true);
  }));

  test(`accounts: register, login, token, logout (${backend.name})`, withStorage(async (storage) => {
    const accounts = new AccountStore().attach(storage);
    const { user } = await accounts.register('Alice_1', 'secret1');
    assert.equal(user.displayName, 'Alice_1');
    assert.ok(Number.isInteger(user.avatarId));

    assert.deepEqual(await accounts.register('alice_1', 'other12'), { error: 'Username is taken' }); // case-insensitive
    assert.equal(await accounts.verifyPassword('alice_1', 'wrong!!'), null);
    assert.equal((await accounts.verifyPassword('ALICE_1', 'secret1')).id, user.id);

    const token = await accounts.issueToken(user.id, false);
    assert.equal((await accounts.resolveToken(token)).id, user.id);
    await accounts.revokeToken(token);
    assert.equal(await accounts.resolveToken(token), null);
  }));

  test(`accounts: the stored token is a hash, never the token itself (${backend.name})`, withStorage(async (storage) => {
    const accounts = new AccountStore().attach(storage);
    const { user } = await accounts.register('bob_22', 'secret1');
    const token = await accounts.issueToken(user.id, true);
    assert.equal(await storage.tokens.get(token), null);
    const stored = await storage.users.get(user.id);
    assert.ok(!JSON.stringify(stored).includes('secret1'));
  }));

  test(`profile: edit name, avatar and bio; settings merged and checked (${backend.name})`, withStorage(async (storage) => {
    const accounts = new AccountStore().attach(storage);
    const profiles = new ProfileService().attach(storage);
    const { user } = await accounts.register('cat_33', 'secret1');

    const { profile } = await profiles.updateProfile(user.id, { displayName: '  Cat   the Great ', avatarId: 3, bio: 'Loves flushes' });
    assert.equal(profile.displayName, 'Cat the Great');
    assert.equal(profile.avatarId, 3);
    assert.equal(profile.bio, 'Loves flushes');
    assert.equal(profile.hash, undefined);
    assert.equal(profile.salt, undefined);

    assert.ok((await profiles.updateProfile(user.id, { avatarId: 99 })).error);
    assert.equal((await profiles.get(user.id)).settingsSaved, false); // client will upload its local settings

    const { settings } = await profiles.updateSettings(user.id, { musicVolume: 0.25, chatEnabled: false, unknown: 1 });
    assert.equal(settings.musicVolume, 0.25);
    assert.equal(settings.chatEnabled, false);
    assert.equal(settings.soundOn, true); // default kept
    assert.equal('unknown' in settings, false);

    const reloaded = await profiles.get(user.id);
    assert.equal(reloaded.settings.chatEnabled, false);
    assert.equal(reloaded.settingsSaved, true);
    assert.equal(reloaded.displayName, 'Cat the Great');
  }));

  test(`stats: saved and loaded (${backend.name})`, withStorage(async (storage) => {
    const stats = new StatsService().attach(storage);
    await stats.recordHand(['a'], { winners: [{ playerId: 'a', amount: 200 }], handNames: { a: 'Straight' } });
    assert.deepEqual(await stats.get('a'), { handsPlayed: 1, handsWon: 1, biggestPot: 200, bestHand: 'Straight', bestHandRank: 4 });
  }));
}

test('json files: data survives a restart', async () => {
  const dataDir = fs.mkdtempSync(path.join(os.tmpdir(), 'poker-restart-'));
  const first = await createStorage({ dataDir });
  await new AccountStore().attach(first).register('dave_44', 'secret1');
  const second = await createStorage({ dataDir }); // like a server restart
  assert.ok(await new AccountStore().attach(second).findByUsername('dave_44'));
});

test('json files: old accounts.json is split into users and tokens once', async () => {
  const dataDir = fs.mkdtempSync(path.join(os.tmpdir(), 'poker-legacy-'));
  fs.writeFileSync(path.join(dataDir, 'accounts.json'), JSON.stringify({
    users: { u1: { id: 'u1', username: 'old_user', usernameLower: 'old_user' } },
    tokens: {},
  }));
  const storage = await createStorage({ dataDir });
  assert.equal((await storage.users.findOne('usernameLower', 'old_user')).id, 'u1');
});
