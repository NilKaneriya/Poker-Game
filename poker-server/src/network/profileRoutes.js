const express = require('express');
const { store } = require('../services/AccountStore');
const { profiles } = require('../services/ProfileService');
const { bearerToken } = require('./authRoutes');
const log = require('../utils/logger');

/**
 * Profile and settings of the logged-in player (Authorization: Bearer <token>).
 *
 *   GET  /profile/me         -> profile + settings
 *   PUT  /profile            { displayName?, avatarId?, bio? } -> profile
 *   PUT  /profile/settings   { musicVolume?, chatEnabled?, ... } -> settings
 *
 * @param {{ onProfileChanged?: (profile) => void }} hooks  e.g. update the table and chat names live
 */
function createProfileRoutes({ onProfileChanged = () => {} } = {}) {
  const router = express.Router();

  // Every route needs a valid login token.
  router.use(async (req, res, next) => {
    try {
      const user = await store.resolveToken(bearerToken(req));
      if (!user) return res.status(401).json({ error: 'Session expired, please log in' });
      req.playerId = user.id;
      next();
    } catch (err) {
      next(err);
    }
  });

  router.get('/me', async (req, res) => {
    const profile = await profiles.get(req.playerId);
    if (!profile) return res.status(404).json({ error: 'Account not found' });
    res.json(profile);
  });

  router.put('/', async (req, res) => {
    const result = await profiles.updateProfile(req.playerId, req.body || {});
    if (result.error) return res.status(400).json({ error: result.error });
    onProfileChanged(result.profile);
    res.json(result.profile);
  });

  router.put('/settings', async (req, res) => {
    const result = await profiles.updateSettings(req.playerId, req.body);
    if (result.error) return res.status(400).json({ error: result.error });
    res.json(result.settings);
  });

  // Storage errors: log them, answer with a short message (no internals to the client).
  router.use((err, req, res, next) => {
    log.error(`Profile request failed: ${err.message}`);
    res.status(500).json({ error: 'Could not save right now, please try again' });
  });

  return router;
}

module.exports = { createProfileRoutes };
