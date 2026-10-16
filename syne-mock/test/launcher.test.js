/**
 * Projet : LIVEX (SYNE-MOCK)
 * Auteur : CHARTRAIN Donovan
 * Rôle : Tests de l'intégration mock SYNE / Launcher (spawn, ports).
 */

const test = require('node:test');
const assert = require('node:assert/strict');
const { spawn } = require('node:child_process');
const fs = require('node:fs');
const net = require('node:net');
const path = require('node:path');
const WebSocket = require('ws');
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
  return { child, endpoint, dataPort, exit, token };
}

test('launcher arguments select the allocated control port without hiding unknown options', () => {
  assert.deepEqual(parseArgs([
    '--headless', '--instance-id', 'mock-test', '--control-port', '50123',
    '--work-dir', 'work', '--log-dir', 'logs', '--correlation-id', 'corr'
  ]), { configPath: null, ports: { controlPort: 50123 }, tickLog: undefined });
  assert.equal(parseArgs(['--tick-log']).tickLog, true);
  assert.equal(parseArgs(['--no-tick-log']).tickLog, false);
  assert.equal(parseArgs([]).tickLog, undefined);
  assert.throws(() => parseArgs(['--autostart']), /Unknown argument/);
  assert.throws(() => parseArgs(['--control-port', '0']), /between 1 and 65535/);
});

test('the existing positional JSON configuration remains supported', () => {
  const configPath = path.join(__dirname, '..', 'config_example.json');

  assert.equal(parseArgs([configPath]).configPath, configPath);
  assert.equal(readConfig(configPath).controlPort, 5181);
});

test('a requested configuration file that does not exist fails instead of falling back', () => {
  // Le repli silencieux sur les defauts lancait le mock sur 500x500/50 agents
  // alors que l'operateur croyait charger le profil `prism` : le monde publie
  // ne correspondait ni a sa configuration, ni au moteur, sans aucun message.
  // Le message nomme le fichier ressemblant du meme dossier (`config.prism.json`
  // vs `config_prism.json`) quand il est unique, et ne propose rien sinon.
  assert.throws(
    () => readConfig(path.join(__dirname, '..', 'config.prism.json')),
    /introuvable[\s\S]*config_prism\.json/);
  assert.throws(() => readConfig(path.join(__dirname, '..', 'absent.json')), /introuvable/);
  assert.throws(() => readConfig(path.join(__dirname, '..', 'absent.json')), error => !/Vouliez-vous dire/.test(error.message));
  // Un chemin absent reste equivalent aux defauts : seul un chemin demande est
  // une erreur.
  assert.deepEqual(readConfig(null), {});
  assert.equal(readConfig(path.join(__dirname, '..', 'config_prism.json')).world.width, 2240);
});

test('the prism configuration mirrors the engine profile the plugin will consume', () => {
  // `configs/simulation/prism.json` (moteur) : monde 2240/cellule 32, 6 TPS,
  // 5 s/tick. Le mock doit annoncer le meme monde, sinon PRISM calibre son
  // echelle sur une geometrie que SYNE ne produira jamais.
  const config = readConfig(path.join(__dirname, '..', 'config_prism.json'));
  assert.equal(config.ticksPerSecond, 6);
  assert.equal(config.simulatedSecondsPerTick, 5);
  assert.equal(config.world.width, 2240);
  assert.equal(config.world.height, 2240);
  assert.equal(config.world.cellSize, 32);
  assert.equal(config.world.metersPerUnit, 1);
  // Le profil prism du moteur declare les memes obstacles : sans layout d'aucun
  // cote, PRISM affichait un monde vide et son rendu d'obstacle paraissait casse.
  assert.equal(config.world.obstacles, true);
  assert.deepEqual(config.world.obstacleLayout, [
    { id: 'cote-sud-1', x: 400, y: 2180, radius: 15 },
    { id: 'cote-sud-2', x: 1120, y: 2180, radius: 5 },
    { id: 'cote-sud-3', x: 1840, y: 2180, radius: 120 },
    { id: 'crete-centre', x: 1120, y: 1120, radius: 90 },
    { id: 'amer-1', x: 300, y: 300, radius: 80 }
  ]);
});

test('Launcher manifest points to the executable and truthful readiness endpoint', () => {
  const manifest = JSON.parse(fs.readFileSync(path.join(__dirname, '..', 'component.json'), 'utf8'));
  assert.equal(manifest.id, 'syne-mock');
  assert.equal(manifest.executable.linux, 'src/cli.js');
  if (process.platform !== 'win32') {
    // Le bit d'exécution POSIX n'a aucun sens sur Windows : sans cette garde,
    // le contrôle y est toujours faux sans rien prouver sur l'adaptateur Linux.
    assert.equal(fs.statSync(path.join(__dirname, '..', manifest.executable.linux)).mode & 0o111, 0o111);
  }
  assert.equal(manifest.executable.windows, 'src/cli.js');
  assert.ok(fs.existsSync(path.join(__dirname, '..', manifest.executable.windows)));
  assert.equal(manifest.health.path, '/api/control/status');
  assert.equal(manifest.endpoints.control.port, 5181);
});

test('launcher shutdown is authenticated and exits cleanly', async t => {
  const { endpoint, dataPort, exit, token } = await startMock(t);
  const client = new WebSocket(`ws://127.0.0.1:${dataPort}`);
  await new Promise((resolve, reject) => {
    client.once('error', reject);
    client.once('open', resolve);
  });
  const clientClosed = new Promise(resolve => client.once('close', resolve));
  assert.equal((await fetch(`${endpoint}/control/shutdown`, { method: 'POST' })).status, 401);
  const response = await fetch(`${endpoint}/control/shutdown`, {
    method: 'POST',
    headers: { authorization: `Bearer ${token}` }
  });
  assert.equal(response.status, 202);
  assert.deepEqual(await response.json(), { ok: true, action: 'shutdown' });
  assert.deepEqual(await exit, { code: 0, signal: null });
  await clientClosed;
});

test('SIGTERM closes both servers and exits cleanly', {
  // Windows n'a pas de livraison de signal : `kill(SIGTERM)` y équivaut à un
  // TerminateProcess (code null, signal 'SIGTERM'), donc la fermeture
  // gracieuse n'est pas observable. Le chemin POSIX reste couvert par Linux.
  skip: process.platform === 'win32' && 'SIGTERM non livrable sous Windows',
}, async t => {
  const { child, exit } = await startMock(t);

  child.kill('SIGTERM');

  assert.deepEqual(await exit, { code: 0, signal: null });
});

test('SIGINT closes both servers and exits cleanly', {
  skip: process.platform === 'win32' && 'SIGINT non livrable sous Windows',
}, async t => {
  const { child, exit } = await startMock(t);

  child.kill('SIGINT');

  assert.deepEqual(await exit, { code: 0, signal: null });
});
