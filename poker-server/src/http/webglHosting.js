const fs = require('fs');
const path = require('path');
const express = require('express');

/**
 * Serves a Unity WebGL build (index.html + Build/ + TemplateData/) from this server, so
 * the game and the server share one address: one deployment, no CORS or mixed-content
 * (https page -> http server) problems. Handles Unity's pre-compressed .br / .gz files.
 * @returns {boolean} true if a build was found and is being served
 */
function serveWebGL(app, dir) {
  if (!dir || !fs.existsSync(path.join(dir, 'index.html'))) return false;

  app.use(
    express.static(dir, {
      setHeaders(res, filePath) {
        if (filePath.endsWith('.br')) res.setHeader('Content-Encoding', 'br');
        else if (filePath.endsWith('.gz')) res.setHeader('Content-Encoding', 'gzip');

        const original = filePath.replace(/\.(br|gz)$/, '');
        if (original.endsWith('.wasm')) res.setHeader('Content-Type', 'application/wasm');
        else if (original.endsWith('.js')) res.setHeader('Content-Type', 'application/javascript');
        else if (original.endsWith('.data')) res.setHeader('Content-Type', 'application/octet-stream');
      },
    }),
  );
  return true;
}

module.exports = { serveWebGL };
