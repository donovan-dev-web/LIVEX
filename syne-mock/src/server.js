const http = require('node:http');
const { WebSocketServer } = require('ws');
const { DEFAULTS, merge, resolveConfig } = require('./config');
const { Simulation, log } = require('./simulation/simulation');

function createServer(options = {}) {
  const config = resolveConfig(options);
  const clients = new Set();
  const simulation = new Simulation(config, message => {
    const text = JSON.stringify(message);
    for (const client of clients)
      if (client.readyState === 1) client.send(text);
  });

  const httpServer = http.createServer(async (request, response) => {
    const send = (statusCode, body) => {
      const text = JSON.stringify(body);
      response.writeHead(statusCode, {
        'content-type': 'application/json',
        'content-length': Buffer.byteLength(text)
      });
      response.end(text);
    };
    const url = new URL(request.url, 'http://127.0.0.1');
    log('HTTP request %s %s', request.method, url.pathname);

    if (request.method === 'GET' && url.pathname === '/api/control/status') {
      log('HTTP response GET status -> 200 (state=%s)', simulation.state);
      return send(200, simulation.status());
    }
    if (request.method === 'GET' && url.pathname === '/api/world') {
      if (!simulation.worldDescription()) {
        log('HTTP response GET world -> 409 world_not_prepared');
        return send(409, { ok: false, error: 'world_not_prepared' });
      }
      log('HTTP response GET world -> 200');
      return send(200, simulation.worldDescription());
    }
    if (request.method !== 'POST' || !url.pathname.startsWith('/api/control/')) {
      log('HTTP response -> 404 not_found');
      return send(404, { ok: false, error: 'not_found' });
    }

    let body;
    try {
      body = await readJsonBody(request);
    } catch (error) {
      log('HTTP response -> 400 invalid_json (%s)', error.message);
      return send(400, { ok: false, error: 'invalid_json' });
    }
    const action = url.pathname.slice('/api/control/'.length);
    log('CONTROL request action=%s body=%j', action, body);

    try {
      if (action === 'prepare') {
        const ticksPerSecond = body.ticksPerSecond ?? config.ticksPerSecond;
        if (!Number.isInteger(ticksPerSecond) || ticksPerSecond <= 0) {
          log('CONTROL response action=prepare -> 400 invalid_ticks_per_second');
          return send(400, { ok: false, error: 'invalid_ticks_per_second' });
        }
        if (!simulation.prepare(
          body.seed ?? config.seed,
          ticksPerSecond,
          body.config ?? {}))
          return send(409, { ok: false, error: 'run_active' });
        return send(200, {
          ok: true,
          action: 'prepared',
          ticksPerSecond: simulation.options.ticksPerSecond,
          world: simulation.worldDescription(),
          ...simulation.status()
        });
      }
      if (action === 'ready') {
        if (!simulation.acknowledgeReady(body.worldVersion)) {
          log('CONTROL response action=ready -> 409 world_not_ready');
          return send(409, { ok: false, error: 'world_not_ready' });
        }
        log('CONTROL response action=ready -> 200');
        return send(200, { ok: true, action: 'ready', ...simulation.status() });
      }
      if (action === 'start') {
        if (simulation.state === 'running' || simulation.state === 'paused') {
          log('CONTROL response action=start -> 409 run_active');
          return send(409, { ok: false, error: 'run_active' });
        }
        simulation.start(body.seed ?? config.seed, body.maxTicks ?? config.maxTicks, body.config ?? {});
      } else if (action === 'pause') simulation.pause();
      else if (action === 'resume') simulation.resume();
      else if (action === 'stop') simulation.stop();
      else if (action === 'reset') simulation.reset(body.seed ?? config.seed, body.maxTicks ?? config.maxTicks);
      else {
        log('CONTROL response action=%s -> 404 not_found', action);
        return send(404, { ok: false, error: 'not_found' });
      }
    } catch (error) {
      const conflictErrors = new Set([
        'world_not_ready', 'prepared_seed_mismatch', 'prepared_config_mismatch'
      ]);
      if (conflictErrors.has(error.code)) {
        log('CONTROL response action=%s -> 409 %s', action, error.code);
        return send(409, { ok: false, error: error.code });
      }
      if (/^(seed|ticksPerSecond|maxTicks|world\.|agents(?:[ .]|$)|resources\.|Unable to place agent)/.test(error.message)) {
        log('CONTROL response action=%s -> 400 invalid_configuration (%s)', action, error.message);
        return send(400, { ok: false, error: 'invalid_configuration', message: error.message });
      }
      log('CONTROL failed action=%s: %s', action, error.stack || error.message);
      return send(500, { ok: false, error: 'internal_error' });
    }

    log('CONTROL response action=%s -> 200 (state=%s, tick=%s)',
      action, simulation.state, simulation.tick);
    return send(200, { ok: true, action, runId: simulation.runId, ...simulation.status() });
  });

  const websocketServer = new WebSocketServer({ noServer: true });
  let nextClientId = 1;
  websocketServer.on('connection', client => {
    clients.add(client);
    client.syneMockClientId = nextClientId++;
    log('WEBSOCKET connected (client=%s, remote=%s, clients=%s)',
      client.syneMockClientId, client._socket.remoteAddress, clients.size);
    if (simulation.worldDescription()) {
      client.send(JSON.stringify({
        type: 'world_initialized',
        version: simulation.worldVersion,
        seed: simulation.options.seed,
        world: simulation.worldDescription()
      }));
    }
    client.on('error', error =>
      log('WEBSOCKET error (client=%s): %s', client.syneMockClientId, error.message));
    client.on('close', (code, reason) => {
      clients.delete(client);
      log('WEBSOCKET disconnected (client=%s, code=%s, reason=%s, clients=%s)',
        client.syneMockClientId, code, reason.toString() || '<none>', clients.size);
    });
  });

  const dataServer = http.createServer();
  dataServer.on('upgrade', (request, socket, head) =>
    websocketServer.handleUpgrade(request, socket, head,
      client => websocketServer.emit('connection', client, request)));

  return {
    simulation,
    httpServer,
    dataServer,
    listen: (controlPort = 5181, dataPort = 5180) => new Promise(resolve => {
      httpServer.listen(controlPort, '127.0.0.1', () =>
        dataServer.listen(dataPort, '127.0.0.1', () => {
          log('SERVER listening (http=127.0.0.1:%s, websocket=127.0.0.1:%s)',
            httpServer.address().port, dataServer.address().port);
          resolve({
            controlPort: httpServer.address().port,
            dataPort: dataServer.address().port
          });
        }));
    }),
    close: () => new Promise(resolve => {
      simulation.stop();
      websocketServer.close();
      dataServer.close(() => httpServer.close(resolve));
    })
  };
}

async function readJsonBody(request) {
  let raw = '';
  for await (const chunk of request) raw += chunk;
  if (!raw) return {};
  const body = JSON.parse(raw);
  if (!body || typeof body !== 'object' || Array.isArray(body))
    throw new TypeError('request body must be a JSON object');
  return body;
}

module.exports = { createServer, Simulation, DEFAULTS, merge };
