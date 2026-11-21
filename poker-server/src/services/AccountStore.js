const { randomBytes, randomUUID, scrypt, timingSafeEqual, createHash } = require('crypto');
const { promisify } = require('util');
const config = require('../config');
const { defaultAvatarFor } = require('./profileRules');

const scryptAsync = promisify(scrypt);
const KEY_LENGTH = 64;

const USERNAME_RE = /^[A-Za-z0-9_]{3,16}$/;
const PASSWORD_MIN = 6;
const PASSWORD_MAX = 64;
const DUPLICATE_KEY = 11000; // MongoDB error code (unique index on usernameLower)

const hashToken = (token) => createHash('sha256').update(token).digest('hex');

/**
 * Player accounts and login tokens, kept in storage (MongoDB or JSON files).
 * - Passwords are stored as scrypt hashes with a per-user salt.
 * - Login tokens are random 32-byte values; only their SHA-256 is stored, so a leaked
 *   database can't be used to log in.
 * The user document also holds the profile and settings (see ProfileService).
 */
class AccountStore {
  /** @param {{ users, tokens }} storage  collections from storage/createStorage */
  attach(storage) {
    this.users = storage.users;
    this.tokens = storage.tokens;
    return this;
  }

  getUser(id) {
    return this.users.get(id);
  }

  findByUsername(username) {
    return this.users.findOne('usernameLower', String(username).toLowerCase());
  }

  /** @returns {Promise<{ user } | { error }>} */
  async register(username, password) {
    if (typeof username !== 'string' || !USERNAME_RE.test(username)) {
      return { error: 'Username must be 3-16 letters, numbers or _' };
    }
    if (typeof password !== 'string' || password.length < PASSWORD_MIN || password.length > PASSWORD_MAX) {
      return { error: `Password must be ${PASSWORD_MIN}-${PASSWORD_MAX} characters` };
    }
    if (await this.findByUsername(username)) return { error: 'Username is taken' };

    const salt = randomBytes(16).toString('hex');
    const hash = (await scryptAsync(password, salt, KEY_LENGTH)).toString('hex');
    const id = randomUUID();
    const user = {
      id,
      username,
      usernameLower: username.toLowerCase(),
      salt,
      hash,
      createdAt: Date.now(),
      // Profile and settings (editable later through ProfileService)
      displayName: username,
      avatarId: defaultAvatarFor(id),
      bio: '',
      settings: {},
    };
    try {
      await this.users.set(id, user);
    } catch (err) {
      if (err.code === DUPLICATE_KEY) return { error: 'Username is taken' }; // two sign-ups at the same moment
      throw err;
    }
    return { user };
  }

  /** @returns {Promise<object|null>} the user, or null if the name/password pair is wrong */
  async verifyPassword(username, password) {
    const user = typeof username === 'string' ? await this.findByUsername(username) : null;
    if (!user || typeof password !== 'string') return null;
    const hash = await scryptAsync(password, user.salt, KEY_LENGTH);
    return timingSafeEqual(hash, Buffer.from(user.hash, 'hex')) ? user : null;
  }

  /** Create a login token. "Remember me" tokens live longer. */
  async issueToken(userId, remember) {
    const token = randomBytes(32).toString('hex');
    const ttl = remember ? config.auth.rememberTokenTtlMs : config.auth.sessionTokenTtlMs;
    const expiresAt = Date.now() + ttl;
    // expireAt (a Date) lets MongoDB delete the token by itself once it runs out.
    await this.tokens.set(hashToken(token), { userId, expiresAt, expireAt: new Date(expiresAt) });
    return token;
  }

  /** @returns {Promise<object|null>} the user that owns a valid, unexpired token */
  async resolveToken(token) {
    if (typeof token !== 'string' || token.length !== 64) return null;
    const entry = await this.tokens.get(hashToken(token));
    if (!entry) return null;
    if (entry.expiresAt < Date.now()) {
      await this.revokeToken(token);
      return null;
    }
    return this.users.get(entry.userId);
  }

  async revokeToken(token) {
    if (typeof token !== 'string') return;
    await this.tokens.delete(hashToken(token));
  }
}

/** What a player is called at tables and in chat. */
const nameOf = (user) => user.displayName || user.username;

/** Public fields only - never send salt/hash to a client. */
const toPublicUser = (user) => ({
  playerId: user.id,
  username: user.username,
  displayName: nameOf(user),
  avatarId: user.avatarId ?? defaultAvatarFor(user.id),
});

module.exports = { store: new AccountStore(), AccountStore, toPublicUser, nameOf };
