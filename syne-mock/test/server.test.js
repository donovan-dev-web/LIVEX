const test = require('node:test');
const assert = require('node:assert/strict');
const http = require('node:http');
const WebSocket = require('ws');
const { createServer, Simulation } = require('../src/server');

function openServer(t, options) {
  const server = createServer(options);
  t.after(() => server.close());
  return server;
}

function closeClient(client) {
  if (client.readyState === WebSocket.CLOSED) return Promise.resolve();
  return new Promise(resolve => {
    client.once('close', resolve);
    client.close();
  });
}

test('generation is deterministic and has the documented population', () => {
  const a = new Simulation({ seed: 7, agents: 50 }); a.start(); a.step(); a.stop();
  const b = new Simulation({ seed: 7, agents: 50 }); b.start(); b.step(); b.stop();
  assert.deepEqual(a.snapshot(), b.snapshot()); assert.equal(a.agents.length, 50);
});

test('advanced systems are represented in snapshots and events', () => {
  const simulation = new Simulation({
    seed: 7,
    agents: 2,
    communication: { transmissionRange: 1000 },
    groups: { formationRange: 1000 },
    territories: { enabled: true },
    books: { enabled: true }
  });
  simulation.start();
  for (let i = 0; i < 100; i++) simulation.step();
  const types = simulation.events.map(event => event.type);
  assert.ok(types.includes('message_sent') || types.includes('message_received'));
  assert.ok(types.includes('group_decision'));
  assert.ok(types.includes('world.book_written'));
  assert.equal(simulation.snapshot().resources.length, 4);
  assert.equal(simulation.snapshot().territories.length, 1);
  simulation.stop();
});

test('HTTP control and websocket emit contract messages', async (t) => {
  const server = openServer(t, { agents: 2, ticksPerSecond: 100, maxTicks: 2 });
  await server.listen(0, 0);
  const controlPort = server.httpServer.address().port;
  const dataPort = server.dataServer.address().port;
  const messages = []; const client = new WebSocket(`ws://127.0.0.1:${dataPort}`);
  client.on('message', data => messages.push(JSON.parse(data)));
  await new Promise(resolve => client.once('open', resolve));
  await fetch(`http://127.0.0.1:${controlPort}/api/control/start`, { method: 'POST', body: JSON.stringify({ seed: 9, maxTicks: 1 }), headers: { 'content-type': 'application/json' } });
  await new Promise(resolve => setTimeout(resolve, 50));
  assert.ok(messages.some(x => x.type === 'snapshot' && x.tick === 1));
  const status = await (await fetch(`http://127.0.0.1:${controlPort}/api/control/status`)).json();
  assert.equal(status.tick, 1); await closeClient(client);
});

test('explicit prepare requires ready and exposes the world contract', async (t) => {
  const server = openServer(t, { agents: 0, ticksPerSecond: 100, maxTicks: 1 });
  await server.listen(0, 0);
  const port = server.httpServer.address().port;
  const post = (path, body = {}) => fetch(`http://127.0.0.1:${port}${path}`, {
    method: 'POST', body: JSON.stringify(body), headers: { 'content-type': 'application/json' }
  });
  assert.equal((await post('/api/control/prepare', { seed: 19, ticksPerSecond: 0 })).status, 400);
  assert.equal((await post('/api/control/prepare', { seed: 19, ticksPerSecond: '24' })).status, 400);
  const invalidWorld = await post('/api/control/prepare', {
    seed: 19, ticksPerSecond: 24, config: { world: { width: 0 } }
  });
  assert.equal(invalidWorld.status, 400);
  assert.equal(server.simulation.status().worldPrepared, false);
  const prepared = await post('/api/control/prepare', {
    seed: 19,
    ticksPerSecond: 24,
    config: {
      world: {
        obstacles: true,
        obstacleLayout: [{ id: 'configured-rock', x: 20, y: 20, radius: 3 }]
      }
    }
  });
  assert.equal(prepared.status, 200);
  const preparedBody = await prepared.json();
  assert.equal(preparedBody.ticksPerSecond, 24);
  assert.deepEqual(preparedBody.world.obstacles, [
    { id: 'configured-rock', type: 'circle', x: 20, y: 20, radius: 3 }
  ]);
  const world = await (await fetch(`http://127.0.0.1:${port}/api/world`)).json();
  assert.equal(world.version, '1.0');
  assert.equal(world.ticksPerSecond, 24);
  assert.equal(world.cells.length, world.cellCountX * world.cellCountY);
  assert.equal((await post('/api/control/start')).status, 409);
  assert.equal((await post('/api/control/ready', { worldVersion: '1.0' })).status, 200);
  const mismatchedStart = await post('/api/control/start', { seed: 20, maxTicks: 1 });
  assert.equal(mismatchedStart.status, 409);
  assert.equal((await mismatchedStart.json()).error, 'prepared_seed_mismatch');
  const started = await post('/api/control/start', { seed: 19, maxTicks: 1 });
  assert.equal(started.status, 200);
  const status = await (await fetch(`http://127.0.0.1:${port}/api/control/status`)).json();
  assert.equal(status.worldPrepared, true);
  assert.equal(status.worldReadyAcknowledged, true);
  assert.equal(status.ticksPerSecond, 24);
});

test('world_initialized precedes a global snapshot and configured obstacles remain authoritative', async (t) => {
  const server = openServer(t, {
    agents: 2,
    ticksPerSecond: 100,
    maxTicks: 1,
    world: {
      obstacles: true,
      obstacleLayout: [{ id: 'initial-rock', x: 250, y: 250, radius: 10 }]
    }
  });
  await server.listen(0, 0);
  const controlPort = server.httpServer.address().port, dataPort = server.dataServer.address().port;
  const messages = [], client = new WebSocket(`ws://127.0.0.1:${dataPort}`);
  client.on('message', data => messages.push(JSON.parse(data)));
  await new Promise(resolve => client.once('open', resolve));
  await fetch(`http://127.0.0.1:${controlPort}/api/control/start`, {
    method: 'POST', body: JSON.stringify({ seed: 4, maxTicks: 1 }),
    headers: { 'content-type': 'application/json' }
  });
  await new Promise(resolve => setTimeout(resolve, 50));
  const initialized = messages.findIndex(x => x.type === 'world_initialized');
  const snapshot = messages.findIndex(x => x.type === 'snapshot');
  assert.ok(initialized >= 0 && initialized < snapshot);
  const snapshots = messages.filter(x => x.type === 'snapshot');
  assert.equal(snapshots.length, 1);
  assert.equal(snapshots[0].agents.length, 2);
  assert.equal(snapshots[0].actions.length, 2);
  assert.ok(Array.isArray(snapshots[0].resources));
  assert.ok(Array.isArray(snapshots[0].obstacles));
  assert.deepEqual(snapshots[0].obstacles, [{ id: 'initial-rock', type: 'circle', x: 250, y: 250, radius: 10 }]);
  assert.deepEqual(snapshots[0].worldChanges, []);
  assert.equal(messages.some(x => x.type === 'world_delta'), false);
  await closeClient(client);
});

test('WorldDescription generation is deterministic by seed', () => {
  const a = new Simulation({ seed: 123 }); a.prepare(123);
  const b = new Simulation({ seed: 123 }); b.prepare(123);
  assert.deepEqual(a.worldDescription(), b.worldDescription());
  assert.equal(a.worldDescription().agents.length, 50);
  assert.equal(a.worldDescription().resources.length, 12);
  for (const kind of ['food', 'water', 'wood', 'mineral']) {
    const locations = a.worldDescription().resources.filter(resource => resource.kind === kind);
    assert.equal(locations.length, 3);
    assert.equal(new Set(locations.map(resource => `${resource.x},${resource.y}`)).size, 3);
  }
  assert.deepEqual(a.worldDescription().agents[0], {
    id: 1, species: 'human',
    position: { x: a.initialAgents[0].x, y: a.initialAgents[0].y }
  });
});

test('WorldDescription includes configured obstacle geometry and cell references', () => {
  const simulation = new Simulation({
    seed: 31,
    agents: 0,
    world: { obstacles: true, obstacleLayout: [{ id: 'initial-rock', x: 15, y: 15, radius: 2 }] }
  });
  simulation.prepare(31);
  const world = simulation.worldDescription();
  assert.deepEqual(world.obstacles, [{ id: 'initial-rock', type: 'circle', x: 15, y: 15, radius: 2 }]);
  assert.deepEqual(world.cells.find(cell => cell.x === 1 && cell.y === 1).obstacles, ['initial-rock']);
  assert.equal(world.cells.find(cell => cell.x === 1 && cell.y === 1).walkable, false);
});

test('world generation places resources and agents only on walkable cells', () => {
  const simulation = new Simulation({
    seed: 56,
    agents: 20,
    world: {
      obstacles: true,
      obstacleLayout: [{ id: 'blocked-center', x: 250, y: 250, radius: 30 }]
    }
  });
  simulation.prepare(56);
  const world = simulation.worldDescription();
  for (const resource of world.resources) {
    assert.equal(world.cells.find(cell => cell.x === resource.x && cell.y === resource.y).walkable, true);
  }
  for (const agent of world.agents) {
    const x = Math.floor(agent.position.x / world.cellSize);
    const y = Math.floor(agent.position.y / world.cellSize);
    assert.equal(world.cells.find(cell => cell.x === x && cell.y === y).walkable, true);
  }
});

test('agent decisions use SYNE need thresholds and configured movement speed', () => {
  const simulation = new Simulation({
    seed: 1,
    agents: 1,
    agentSimulation: {
      needs: { hungerRate: 50, thirstRate: 0, fatigueRate: 0, curiosityDriftRate: 0.3 },
      deliberationIntervalTicks: 1,
      traits: { speed: 2 }
    },
    resources: {
      food: 10, water: 10, wood: 0, mineral: 0,
      regeneration: { food: 0, water: 0, wood: 0, mineral: 0 }
    }
  });
  simulation.start(1, 3);
  const initial = { x: simulation.agents[0].x, y: simulation.agents[0].y };
  simulation.step();
  const agent = simulation.agents[0];
  assert.equal(agent.currentAction, 'Eat');
  assert.equal(agent.hunger, 20);
  assert.equal(simulation.resources.food, 9);
  simulation.step();
  assert.ok(Math.hypot(agent.x - initial.x, agent.y - initial.y) <= 2);
  simulation.stop();
});

test('prepared agent positions are reused by the first simulation snapshot', () => {
  const simulation = new Simulation({ seed: 23, agents: 3 });
  simulation.prepare(23);
  const initialAgents = simulation.worldDescription().agents;
  simulation.acknowledgeReady('1.0');
  simulation.start(23, 10);
  const snapshotAgents = simulation.snapshot().agents;
  assert.deepEqual(snapshotAgents.map(agent => ({
    id: Number(agent.id), species: agent.species, position: agent.position
  })), initialAgents);
  simulation.stop();
});

test('simulation finishes only when maxTicks is reached', async () => {
  const simulation = new Simulation({ seed: 23, agents: 0, ticksPerSecond: 100, maxTicks: 3 });
  simulation.start(23, 3);
  await new Promise((resolve, reject) => {
    const deadline = Date.now() + 1000;
    const poll = () => {
      if (simulation.state === 'finished') return resolve();
      if (Date.now() >= deadline) return reject(new Error('simulation did not finish at maxTicks'));
      setTimeout(poll, 5);
    };
    poll();
  });
  assert.equal(simulation.tick, 3);
  assert.equal(simulation.state, 'finished');
  simulation.stop();
});
