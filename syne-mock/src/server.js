const http = require('node:http');
const { timingSafeEqual } = require('node:crypto');
const { WebSocketServer } = require('ws');
const { DEFAULTS, merge, resolveConfig } = require('./config');
const { Simulation, log } = require('./simulation/simulation');

/** Erreurs JSON du contrat SYNE : { ok, error, detail } (ControlServer.ErrorJson). */
function errorJson(error, detail) {
  return { ok: false, error, detail: detail ?? error };
}

/** Enveloppe les commandes de contrôle : réponse 200 au format SYNE, erreurs 409 codées. */
function controlOutcome(simulation, action) {
  const status = simulation.status();
  return {
    ok: true,
    action,
    runId: simulation.runId || null,
    state: status.state,
    tick: status.tick,
    aliveCount: status.aliveCount,
    seed: status.seed
  };
}

function createServer(options = {}) {
  const config = resolveConfig(options);
  const shutdownToken = options.sessionToken ?? process.env.LIVEX_SESSION_TOKEN;
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
    if (request.method === 'POST' && url.pathname === '/control/shutdown') {
      const authorization = request.headers.authorization ?? '';
      const expected = shutdownToken ? `Bearer ${shutdownToken}` : '';
      const authorized = expected.length > 0
        && Buffer.byteLength(authorization) === Buffer.byteLength(expected)
        && timingSafeEqual(Buffer.from(authorization), Buffer.from(expected));
      if (!authorized) {
        return send(401, errorJson('unauthorized', 'Jeton de session absent ou invalide.'));
      }
      send(202, { ok: true, action: 'shutdown' });
      setImmediate(() => void server.close());
      return;
    }
    if (request.method === 'GET' && url.pathname === '/api/world') {
      if (!simulation.worldDescription()) {
        log('HTTP response GET world -> 409 world_not_prepared');
        return send(409, errorJson('world_not_prepared', 'Préparez le monde avant de le consulter.'));
      }
      log('HTTP response GET world -> 200');
      return send(200, worldDescriptionGeometry(simulation.worldDescription()));
    }
    if (request.method !== 'POST' || !url.pathname.startsWith('/api/control/')) {
      log('HTTP response -> 404 not_found');
      return send(404, errorJson('not_found', 'Endpoint inconnu.'));
    }

    let body;
    try {
      body = await readJsonBody(request);
    } catch (error) {
      log('HTTP response -> 400 invalid_json (%s)', error.message);
      return send(400, errorJson('invalid_json', 'Le corps de la requête n\'est pas un JSON valide.'));
    }
    const action = url.pathname.slice('/api/control/'.length);
    log('CONTROL request action=%s body=%j', action, body);

    try {
      if (action === 'prepare') {
        const ticksPerSecond = body.ticksPerSecond ?? config.ticksPerSecond;
        if (!Number.isInteger(ticksPerSecond) || ticksPerSecond <= 0) {
          log('CONTROL response action=prepare -> 400 invalid_ticks_per_second');
          return send(400, errorJson('invalid_ticks_per_second', 'ticksPerSecond doit être un entier strictement positif.'));
        }
        if (!simulation.prepare(
          body.seed ?? config.seed,
          ticksPerSecond,
          body.config ?? {}))
          return send(409, errorJson('run_active',
            'Un run est déjà en cours — utilisez /stop ou /reset avant de redémarrer.'));
        return send(200, {
          ok: true,
          action: 'prepared',
          ticksPerSecond: simulation.options.ticksPerSecond,
          world: worldDescriptionGeometry(simulation.worldDescription()),
          ...simulation.status()
        });
      }
      if (action === 'ready') {
        if (!simulation.acknowledgeReady(body.worldVersion)) {
          log('CONTROL response action=ready -> 409 world_not_ready');
          return send(409, errorJson('world_not_ready',
            'Le monde n\'est pas préparé ou sa version est incorrecte.'));
        }
        log('CONTROL response action=ready -> 200');
        return send(200, controlOutcome(simulation, 'ready'));
      }
      if (action === 'start') {
        if (simulation.state === 'running' || simulation.state === 'paused') {
          log('CONTROL response action=start -> 409 run_active');
          return send(409, errorJson('run_active',
            'Un run est déjà en cours — utilisez /stop ou /reset avant de redémarrer.'));
        }
        if (simulation.state === 'finished') {
          // Parité SYNE (API_CONTRACTS.md §3) : un run « finished » a atteint
          // maxTicks — le remède est /reset, pas /prepare (code run_finished).
          log('CONTROL response action=start -> 409 run_finished');
          return send(409, errorJson('run_finished',
            'Run terminé — appelez /api/control/reset avant de redémarrer.'));
        }
        simulation.start(body.seed ?? config.seed, body.maxTicks ?? config.maxTicks, body.config ?? {});
      } else if (action === 'pause') simulation.pause();
      else if (action === 'resume') simulation.resume();
      else if (action === 'stop') simulation.stop();
      else if (action === 'reset') simulation.reset(body.seed ?? config.seed, body.maxTicks ?? config.maxTicks);
      else {
        log('CONTROL response action=%s -> 404 not_found', action);
        return send(404, errorJson('not_found', `Action de contrôle inconnue : ${action}.`));
      }
    } catch (error) {
      const conflictErrors = new Set([
        'world_not_ready', 'prepared_seed_mismatch', 'prepared_config_mismatch', 'run_finished'
      ]);
      if (conflictErrors.has(error.code)) {
        log('CONTROL response action=%s -> 409 %s', action, error.code);
        return send(409, errorJson(error.code, error.message));
      }
      if (/^(seed|ticksPerSecond|maxTicks|world\.|agents(?:[ .]|$)|resources\.|Unable to place agent)/.test(error.message)) {
        log('CONTROL response action=%s -> 400 invalid_configuration (%s)', action, error.message);
        return send(400, errorJson('invalid_configuration', error.message));
      }
      log('CONTROL failed action=%s: %s', action, error.stack || error.message);
      return send(500, errorJson('internal_error', 'Erreur interne du serveur de contrôle.'));
    }

    log('CONTROL response action=%s -> 200 (state=%s, tick=%s)',
      action, simulation.state, simulation.tick);
    return send(200, controlOutcome(simulation, action));
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
        // Géométrie du monde, comme WorldDescription (ObstacleSnapshot sans
        // type/shape/cells) : les consommateurs PRISM/ECHOS n'ont pas à connaître
        // la représentation interne des obstacles du mock.
        world: worldDescriptionGeometry(simulation.worldDescription())
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

  let closePromise;
  const server = {
    simulation,
    httpServer,
    dataServer,
    listen: async (controlPort = 5181, dataPort = 5180) => {
      await listenOnLoopback(httpServer, controlPort);
      try {
        await listenOnLoopback(dataServer, dataPort);
      } catch (error) {
        await new Promise(resolve => httpServer.close(resolve));
        throw error;
      }
      log('SERVER listening (http=127.0.0.1:%s, websocket=127.0.0.1:%s)',
        httpServer.address().port, dataServer.address().port);
      return {
        controlPort: httpServer.address().port,
        dataPort: dataServer.address().port
      };
    },
    close: () => closePromise ??= new Promise(resolve => {
      simulation.stop();
      for (const client of clients) client.terminate();
      clients.clear();
      websocketServer.close(() => {
        dataServer.closeAllConnections();
        httpServer.closeAllConnections();
        dataServer.close(() => httpServer.close(resolve));
      });
    })
  };

  return server;
}

function listenOnLoopback(server, port) {
  return new Promise((resolve, reject) => {
    const onError = error => {
      server.off('listening', onListening);
      reject(error);
    };
    const onListening = () => {
      server.off('error', onError);
      resolve();
    };
    server.once('error', onError);
    server.once('listening', onListening);
    server.listen(port, '127.0.0.1');
  });
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

/** Projette les obstacles sur leur géométrie {id, x, y, radius}, comme WorldDescription. */
function worldDescriptionGeometry(world) {
  return {
    ...world,
    obstacles: (world.obstacles ?? []).map(({ id, x, y, radius }) => ({ id, x, y, radius: radius ?? 0 }))
  };
}

module.exports = { createServer, Simulation, DEFAULTS, merge };
