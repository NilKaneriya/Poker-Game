const express = require('express');
const { store, toPublicUser } = require('../services/AccountStore');

const router = express.Router();

// Very small brute-force guard: max attempts per IP per minute on login/register.
const WINDOW_MS = 60_000;
const MAX_ATTEMPTS = 10;
const attempts = new Map(); // ip -> { count, resetAt }

function rateLimit(req, res, next) {
  const now = Date.now();
  const entry = attempts.get(req.ip);
  if (!entry || entry.resetAt < now) {
    attempts.set(req.ip, { count: 1, resetAt: now + WINDOW_MS });
    return next();
  }
  if (++entry.count > MAX_ATTEMPTS) {
    return res.status(429).json({ error: 'Too many attempts, try again in a minute' });
  }
  next();
}

function bearerToken(req) {
  const header = req.get('authorization') || '';
  return header.startsWith('Bearer ') ? header.slice(7) : null;
}

// POST /auth/register { username, password, remember }
router.post('/register', rateLimit, async (req, res) => {
  const { username, password, remember } = req.body || {};
  const result = await store.register(username, password);
  if (result.error) return res.status(400).json({ error: result.error });
  const token = store.issueToken(result.user.id, Boolean(remember));
  res.json({ token, ...toPublicUser(result.user) });
});

// POST /auth/login { username, password, remember }
router.post('/login', rateLimit, async (req, res) => {
  const { username, password, remember } = req.body || {};
  const user = await store.verifyPassword(username, password);
  if (!user) return res.status(401).json({ error: 'Wrong username or password' });
  const token = store.issueToken(user.id, Boolean(remember));
  res.json({ token, ...toPublicUser(user) });
});

// GET /auth/me  (Authorization: Bearer <token>) - used for auto-login
router.get('/me', (req, res) => {
  const user = store.resolveToken(bearerToken(req));
  if (!user) return res.status(401).json({ error: 'Session expired, please log in' });
  res.json(toPublicUser(user));
});

// POST /auth/logout  (Authorization: Bearer <token>)
router.post('/logout', (req, res) => {
  store.revokeToken(bearerToken(req));
  res.json({ ok: true });
});

module.exports = router;
