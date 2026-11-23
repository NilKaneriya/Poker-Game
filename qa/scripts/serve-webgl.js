// Serves a Unity WebGL build with the HTTP headers Unity needs for compressed builds.
// Plain file servers (Live Server, python -m http.server) fail with
// "Unable to parse Build/<name>.framework.js.br! ... Content-Encoding".
//
// Usage (from qa/):  node scripts/serve-webgl.js <build-folder> [port]
//   node scripts/serve-webgl.js ../builds/webgl 8080
// Then open http://localhost:8080  (localhost is a secure context, so Brotli works).
//
// Brotli (.br) only works on https:// or localhost. Gzip (.gz) works everywhere.
const fs = require('fs');
const http = require('http');
const path = require('path');

const root = path.resolve(process.argv[2] || '.');
const port = Number(process.argv[3] || 8080);

if (!fs.existsSync(path.join(root, 'index.html'))) {
  console.error(`No index.html in ${root}. Pass the WebGL build folder.`);
  process.exit(1);
}

const TYPES = {
  '.html': 'text/html; charset=utf-8',
  '.js': 'application/javascript',
  '.wasm': 'application/wasm',
  '.data': 'application/octet-stream',
  '.json': 'application/json',
  '.css': 'text/css',
  '.png': 'image/png',
  '.jpg': 'image/jpeg',
  '.ico': 'image/x-icon',
  '.svg': 'image/svg+xml',
};
const ENCODINGS = { '.br': 'br', '.gz': 'gzip' };

const server = http.createServer((req, res) => {
  const urlPath = decodeURIComponent(new URL(req.url, 'http://x').pathname);
  const file = path.join(root, urlPath === '/' ? 'index.html' : urlPath);

  // Stay inside the build folder.
  if (file !== root && !file.startsWith(root + path.sep)) {
    res.writeHead(403).end('Forbidden');
    return;
  }
  fs.stat(file, (err, stat) => {
    if (err || !stat.isFile()) {
      res.writeHead(404).end('Not found');
      return;
    }
    const outer = path.extname(file).toLowerCase();
    const encoding = ENCODINGS[outer];
    // "WebGl.wasm.br" is a wasm file that is brotli-compressed.
    const inner = encoding ? path.extname(file.slice(0, -outer.length)).toLowerCase() : outer;
    const headers = {
      'Content-Type': TYPES[inner] || 'application/octet-stream',
      'Content-Length': stat.size,
      'Cache-Control': 'no-store',
    };
    if (encoding) headers['Content-Encoding'] = encoding;
    res.writeHead(200, headers);
    fs.createReadStream(file).pipe(res);
  });
});

server.listen(port, () => {
  console.log(`Serving ${root}`);
  console.log(`Open http://localhost:${port}`);
});
