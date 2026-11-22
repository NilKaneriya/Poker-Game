const test = require('node:test');
const assert = require('node:assert/strict');
const {
  AVATAR_COUNT,
  validateProfile,
  mergeSettings,
  withDefaults,
  defaultSettings,
  defaultAvatarFor,
} = require('../src/services/profileRules');

test('display name: tidied, 3-16 letters, numbers, spaces or _', () => {
  assert.deepEqual(validateProfile({ displayName: '  Big   Slick ' }), { changes: { displayName: 'Big Slick' } });
  for (const bad of ['ab', 'x'.repeat(17), 'no<b>tags', 'emoji 🂡', 42, '']) {
    assert.ok(validateProfile({ displayName: bad }).error, `accepted ${bad}`);
  }
});

test('avatar: an integer in range', () => {
  assert.deepEqual(validateProfile({ avatarId: AVATAR_COUNT - 1 }).changes, { avatarId: AVATAR_COUNT - 1 });
  for (const bad of [-1, AVATAR_COUNT, 1.5, '3', null]) assert.ok(validateProfile({ avatarId: bad }).error);
});

test('bio: tidied, at most 80 characters, may be empty', () => {
  assert.deepEqual(validateProfile({ bio: ' I\nlove\tpoker ' }).changes, { bio: 'I love poker' });
  assert.deepEqual(validateProfile({ bio: '' }).changes, { bio: '' });
  assert.ok(validateProfile({ bio: 'x'.repeat(81) }).error);
});

test('an empty edit is refused', () => {
  assert.ok(validateProfile({}).error);
  assert.ok(validateProfile({ hash: 'evil', salt: 'x' }).error); // unknown fields are never written
});

test('settings: merged over current, numbers clamped, unknown keys ignored, wrong types refused', () => {
  const { settings } = mergeSettings({ chatEnabled: false }, { musicVolume: 3, soundVolume: -1, isAdmin: true });
  assert.equal(settings.chatEnabled, false);
  assert.equal(settings.musicVolume, 1);
  assert.equal(settings.soundVolume, 0);
  assert.equal('isAdmin' in settings, false);

  assert.ok(mergeSettings({}, { chatEnabled: 'no' }).error);
  assert.ok(mergeSettings({}, { musicVolume: Number.NaN }).error);
  assert.ok(mergeSettings({}, null).error);
  assert.ok(mergeSettings({}, [1]).error);
});

test('settings: stored junk is dropped and defaults filled in', () => {
  assert.deepEqual(withDefaults(undefined), defaultSettings());
  assert.deepEqual(withDefaults({ chatEnabled: 'yes', junk: 1 }), defaultSettings());
});

test('default avatar is stable for the same id and in range', () => {
  assert.equal(defaultAvatarFor('abc'), defaultAvatarFor('abc'));
  for (const id of ['a', 'b', 'some-long-uuid']) {
    const a = defaultAvatarFor(id);
    assert.ok(a >= 0 && a < AVATAR_COUNT);
  }
});
