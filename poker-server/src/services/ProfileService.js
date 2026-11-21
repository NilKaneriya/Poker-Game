const { validateProfile, mergeSettings, withDefaults, defaultAvatarFor } = require('./profileRules');

/**
 * A player's profile (display name, avatar, bio) and settings, stored on their user
 * document. The rules for valid values are in profileRules.js.
 */
class ProfileService {
  /** @param {{ users }} storage */
  attach(storage) {
    this.users = storage.users;
    return this;
  }

  /** @returns {Promise<object|null>} the profile as sent to its owner */
  async get(playerId) {
    const user = await this.users.get(playerId);
    return user ? toProfile(user) : null;
  }

  /** @returns {Promise<{ profile } | { error }>} */
  async updateProfile(playerId, edit) {
    const checked = validateProfile(edit);
    if (checked.error) return checked;
    const user = await this.users.update(playerId, checked.changes);
    return user ? { profile: toProfile(user) } : { error: 'Account not found' };
  }

  /** @returns {Promise<{ settings } | { error }>} */
  async updateSettings(playerId, incoming) {
    const user = await this.users.get(playerId);
    if (!user) return { error: 'Account not found' };
    const merged = mergeSettings(user.settings, incoming);
    if (merged.error) return merged;
    await this.users.update(playerId, { settings: merged.settings });
    return { settings: merged.settings };
  }
}

/** The owner's view: profile + settings, never the password hash or salt. */
function toProfile(user) {
  return {
    playerId: user.id,
    username: user.username,
    displayName: user.displayName || user.username,
    avatarId: user.avatarId ?? defaultAvatarFor(user.id),
    bio: user.bio || '',
    createdAt: user.createdAt,
    // false until the player saved settings once: the client then uploads its local ones
    // instead of replacing them with defaults.
    settingsSaved: Object.keys(user.settings || {}).length > 0,
    settings: withDefaults(user.settings),
  };
}

module.exports = { profiles: new ProfileService(), ProfileService, toProfile };
