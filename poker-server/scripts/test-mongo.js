// Runs the storage tests against a real MongoDB as well as the JSON backends.
// mongodb-memory-server starts a throwaway MongoDB (downloaded on the first run), so
// no database needs to be installed. Usage: npm run test:mongo
const { spawnSync } = require('child_process');
const path = require('path');

const result = spawnSync(process.execPath, ['--test', path.join(__dirname, '..', 'test', 'storage.test.js')], {
  env: { ...process.env, TEST_MONGO: '1' },
  stdio: 'inherit',
});
process.exit(result.status ?? 1);
