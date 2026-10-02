const test = require('node:test');
const assert = require('node:assert/strict');
const { spawn } = require('node:child_process');
const fs = require('node:fs');
const net = require('node:net');
const path = require('node:path');
const { parseArgs, readConfig } = require('../src/cli');

async function freePort() {
  const listener = net.createServer();
  await new Promise((resolve, reject) => {
    listener.once('error', reject);
    listener.listen(0, '127.0.0.1', resolve);
  });
  const { port } = listener.address();
  await new Promise((resolve, reject) => listener.close(error => error ? reject(error) : resolve()));
  return port;
}

function waitForExit(child) {
  return new Promise((resolve, reject) => {
    child.once('error', reject);
    child.once('exit', (code, signal) => resolve({ code, signal }));
  });
}

async function startMock(t) {
  const dataPort = await freePort();
  let controlPort = await freePort();
  while (controlPort === dataPort) controlPort = await freePort();
  const token = 'launcher-test-token';
  const child = spawn(process.execPath, [
    path.join(__dirname, '..', 'src', 'cli.js'),
    '--headless',
    '--instance-id', 'mock-test',
    '--control-port', String(controlPort),
    '--data-port', String(dataPort),
    '--work-dir', 'unused',
    '--log-dir', 'unused',
    '--correlation-id', 'test-correlation'
  ], {
    env: { ...process.env, LIVEX_SESSION_TOKEN: token },
    stdio: 'ignore'
  });
  const exit = waitForExit(child);
  t.after(async () => {
    if (child.exitCode === null && child.signalCode === null) {
      child.kill('SIGTERM');
      const closed = await Promise.race([
        exit.then(() => true),
        new Promise(resolve => setTimeout(() => resolve(false), 1000))
      ]);
      if (!closed) {
        child.kill('SIGKILL');
        await exit;
      }
    }
  });

  const endpoint = `http://127.0.0.1:${controlPort}`;
  const deadline = Date.now() + 5000;
  let ready = false;
  while (Date.now() < deadline && !ready) {
    try {
      ready = (await fetch(`${endpoint}/api/control/status`)).ok;
    } catch {
      await new Promise(resolve => setTimeout(resolve, 25));
    }
  }
  assert.equal(ready, true, 'status endpoint should become ready');
  return { child, endpoint, exit, token };
}

test('launcher arguments select the allocated control port without hiding unknown options', () => {
  assert.deepEqual(parseArgs([
    '--headless', '--instance-id', 'mock-test', '--control-port', '50123',
    '--work-dir', 'work', '--log-dir', 'logs', '--correlation-id', 'corr'
  ]), { configPath: null, ports: { controlPort: 50123 } });
  assert.throws(() => parseArgs(['--autostart']), /Unknown argument/);
  assert.throws(() => parseArgs(['--control-port', '0']), /between 1 and 65535/);
});

test('the existing positional JSON configuration remains supported', () => {
  const configPath = path.join(__dirname, '..', 'config_example.json');

  assert.equal(parseArgs([configPath]).configPath, configPath);
  assert.equal(readConfig(configPath).controlPort, 5181);
});

test('Launcher manifest points to the executable and truthful readiness endpoint', () => {
  const manifest = JSON.parse(fs.readFileSync(path.join(__dirname, '..', 'component.json'), 'utf8'));
  assert.equal(manifest.id, 'syne-mock');
  assert.equal(manifest.executable.linux, 'src/cli.js');
  assert.equal(fs.statSync(path.join(__dirname, '..', manifest.executable.linux)).mode & 0o111, 0o111);
  assert.equal(manifest.health.path, '/api/control/status');
  assert.equal(manifest.endpoints.control.port, 5181);
});

test('launcher shutdown is authenticated and exits cleanly', async t => {
  const { endpoint, exit, token } = await startMock(t);
  assert.equal((await fetch(`${endpoint}/control/shutdown`, { method: 'POST' })).status, 401);
  const response = await fetch(`${endpoint}/control/shutdown`, {
    method: 'POST',
    headers: { authorization: `Bearer ${token}` }
  });
  assert.equal(response.status, 202);
  assert.deepEqual(await response.json(), { ok: true, action: 'shutdown' });
  assert.deepEqual(await exit, { code: 0, signal: null });
});

test('SIGTERM closes both servers and exits cleanly', async t => {
  const { child, exit } = await startMock(t);

  child.kill('SIGTERM');

  assert.deepEqual(await exit, { code: 0, signal: null });
});
