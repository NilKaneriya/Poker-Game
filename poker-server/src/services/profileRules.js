/**
 * What a valid profile and valid settings look like. Pure functions, no storage, so
 * they are easy to test and the same rules apply to MongoDB and JSON storage.
 */
const AVATAR_COUNT = 12; // Unity: Constants.Avatars (colours + symbols)
const DISPLAY_NAME_RE = /^[A-Za-z0-9_ ]{3,16}$/;
const BIO_MAX = 80;

/**
 * Every setting a client may save, with its type, range and default.
 * Unknown keys are ignored; wrong types are refused.
 */
const SETTINGS_SCHEMA = Object.freeze({
  musicOn: { type: 'boolean', default: true },
  soundOn: { type: 'boolean', default: true },
  musicVolume: { type: 'number', min: 0, max: 1, default: 0.6 },
  soundVolume: { type: 'number', min: 0, max: 1, default: 0.8 },
  breakReminders: { type: 'boolean', default: true },
  chatEnabled: { type: 'boolean', default: true },
  showHandStrength: { type: 'boolean', default: true }, // "your hand" pill + chart mark
  fourColorDeck: { type: 'boolean', default: false }, //  diamonds blue, clubs green: easier to tell suits apart
});

const defaultSettings = () => Object.fromEntries(Object.entries(SETTINGS_SCHEMA).map(([k, s]) => [k, s.default]));

/** Same avatar for the same id every time, so new players don't all look alike. */
function defaultAvatarFor(id) {
  let h = 0;
  for (const ch of String(id)) h = (h * 31 + ch.charCodeAt(0)) >>> 0;
  return h % AVATAR_COUNT;
}

const tidy = (text) =>
  text
    .normalize('NFC')
    .replace(/[\p{Cc}\p{Cf}]/gu, ' ')
    .replace(/\s+/g, ' ')
    .trim();

/**
 * Check a profile edit. Only fields that are present are checked and returned.
 * @returns {{ changes: object } | { error: string }}
 */
function validateProfile({ displayName, avatarId, bio } = {}) {
  const changes = {};
  if (displayName !== undefined) {
    const name = typeof displayName === 'string' ? tidy(displayName) : '';
    if (!DISPLAY_NAME_RE.test(name)) return { error: 'Display name must be 3-16 letters, numbers, spaces or _' };
    changes.displayName = name;
  }
  if (avatarId !== undefined) {
    if (!Number.isInteger(avatarId) || avatarId < 0 || avatarId >= AVATAR_COUNT) return { error: 'Pick one of the avatars' };
    changes.avatarId = avatarId;
  }
  if (bio !== undefined) {
    if (typeof bio !== 'string') return { error: 'Bio must be text' };
    const clean = tidy(bio);
    if ([...clean].length > BIO_MAX) return { error: `Bio can be at most ${BIO_MAX} characters` };
    changes.bio = clean;
  }
  if (Object.keys(changes).length === 0) return { error: 'Nothing to change' };
  return { changes };
}

/**
 * Check a settings edit and merge it over the current settings.
 * @returns {{ settings: object } | { error: string }}
 */
function mergeSettings(current, incoming) {
  if (!incoming || typeof incoming !== 'object' || Array.isArray(incoming)) return { error: 'Settings must be an object' };
  const settings = { ...defaultSettings() };
  // Keep only known, well-typed stored values (older data may have junk).
  for (const [key, value] of Object.entries(current || {})) {
    if (SETTINGS_SCHEMA[key] && typeof value === SETTINGS_SCHEMA[key].type) settings[key] = value;
  }
  for (const [key, value] of Object.entries(incoming)) {
    const rule = SETTINGS_SCHEMA[key];
    if (!rule) continue; // e.g. a setting from a newer client
    if (typeof value !== rule.type || (rule.type === 'number' && !Number.isFinite(value))) {
      return { error: `Setting "${key}" must be a ${rule.type}` };
    }
    settings[key] = rule.type === 'number' ? Math.min(rule.max, Math.max(rule.min, value)) : value;
  }
  return { settings };
}

/** Stored settings with defaults filled in (and nothing unknown). */
const withDefaults = (stored) => mergeSettings(stored, {}).settings;

module.exports = {
  AVATAR_COUNT,
  BIO_MAX,
  SETTINGS_SCHEMA,
  defaultSettings,
  defaultAvatarFor,
  validateProfile,
  mergeSettings,
  withDefaults,
};
