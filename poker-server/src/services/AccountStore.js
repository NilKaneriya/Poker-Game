const fs = require('fs');
const path = require('path');
const { randomBytes, randomUUID, scrypt, timingSafeEqual, createHash } = require('crypto');
const { promisify } = require('util');
const config = require('../config');

const scryptAsync = promisify(scrypt);
const KEY_LENGTH = 64;

const USERNAME_RE = /^[A-Za-z0-9_]{3,16}$/;
const PASSWORD_MIN = 6;
const PASSWORD_MAX = 64;

const hashToken = (token) => createHash('sha256').update(token).digest('hex');

/**
 * Player accounts and login tokens, saved to a JSON file so they survive restarts.
 * - Passwords are stored as scrypt hashes with a per-user salt.
 * - Login tokens are random 32-byte values; only their SHA-256 is stored, so a leaked
 *   accounts file can't be used to log in.
 * Fine for a prototype; a real deployment would use a database.
 */
class AccountStore {
  constructor(filePath) {
    this.filePath = filePath;
    this.data = { users: {}, tokens: {} };
    this.load();
  }

  load() {
    try {
      this.data = JSON.parse(fs.readFileSync(this.filePath, 'utf8'));
    } catch {
      // No file yet (first run) - start empty.
    }
  }

  save() {
    fs.mkdirSync(path.dirname(this.filePath), { recursive: true });
    fs.writeFileSync(this.filePath, JSON.stringify(this.data, null, 2));
  }

  findByUsername(username) {
    const key = String(username).toLowerCase();
    return Object.values(this.data.users).find((u) => u.usernameLower === key) || null;
  }

  /** @returns {Promise<{ user } | { error }>} */
  async register(username, password) {
    if (typeof username !== 'string' || !USERNAME_RE.test(username)) {
      return { error: 'Username must be 3-16 letters, numbers or _' };
    }
    if (typeof password !== 'string' || password.length < PASSWORD_MIN || password.length > PASSWORD_MAX) {
      return { error: `Password must be ${PASSWORD_MIN}-${PASSWORD_MAX} characters` };
    }
    if (this.findByUsername(username)) return { error: 'Username is taken' };

    const salt = randomBytes(16).toString('hex');
    const hash = (await scryptAsync(password, salt, KEY_LENGTH)).toString('hex');
    const user = { id: randomUUID(), username, usernameLower: username.toLowerCase(), salt, hash, createdAt: Date.now() };
    this.data.users[user.id] = user;
    this.save();
    return { user };
  }

  /** @returns {Promise<object|null>} the user, or null if the name/password pair is wrong */
  async verifyPassword(username, password) {
    const user = typeof username === 'string' ? this.findByUsername(username) : null;
    if (!user || typeof password !== 'string') return null;
    const hash = await scryptAsync(password, user.salt, KEY_LENGTH);
    return timingSafeEqual(hash, Buffer.from(user.hash, 'hex')) ? user : null;
  }

  /** Create a login token. "Remember me" tokens live longer. */
  issueToken(userId, remember) {
    const token = randomBytes(32).toString('hex');
    const ttl = remember ? config.auth.rememberTokenTtlMs : config.auth.sessionTokenTtlMs;
    this.data.tokens[hashToken(token)] = { userId, expiresAt: Date.now() + ttl };
    this.save();
    return token;
  }

  /** @returns {object|null} the user that owns a valid, unexpired token */
  resolveToken(token) {
    if (typeof token !== 'string' || token.length !== 64) return null;
    const entry = this.data.tokens[hashToken(token)];
    if (!entry) return null;
    if (entry.expiresAt < Date.now()) {
      this.revokeToken(token);
      return null;
    }
    return this.data.users[entry.userId] || null;
  }

  revokeToken(token) {
    if (typeof token !== 'string') return;
    delete this.data.tokens[hashToken(token)];
    this.save();
  }
}

/** Public fields only - never send salt/hash to a client. */
const toPublicUser = (user) => ({ playerId: user.id, username: user.username });

module.exports = { store: new AccountStore(config.auth.accountsFile), toPublicUser };
