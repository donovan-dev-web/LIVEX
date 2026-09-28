const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');
const { Simulation } = require('../src/server');
const { AgentDecisionService } = require('../src/simulation/agent-decision-service');
const { DEFAULTS } = require('../src/config');

function tempFile(name, content) {
  const file = path.join(fs.mkdtempSync(path.join(os.tmpdir(), 'syne-mock-')), name);
  if (content != null) fs.writeFileSync(file, content, 'utf8');
  return file;
}

test('a start refused for a missing replay file leaves no run active', () => {
  const simulation = new Simulation({ replay: { file: path.join(os.tmpdir(), 'absent-replay.jsonl') } }, () => {});

  assert.throws(() => simulation.start(42, 10));

  // Le monde a ete prepare par start() lui-meme, l'etat coherent est donc
  // 'ready'. Ce qui est exclu, c'est 'running' : un run declare actif sans timer
  // ne ticke jamais et bloque tout appel ulterieur a start().
  assert.equal(simulation.state, 'ready');
  assert.equal(simulation.timer, null);
  assert.equal(simulation.status().worldPrepared, true);
});

test('a start refused for a malformed replay line leaves no run active', () => {
  const file = tempFile('malformed.jsonl', '{"tick":1,"type":"snapshot"}\nceci n est pas du JSON\n');
  const simulation = new Simulation({ replay: { file } }, () => {});

  assert.throws(() => simulation.start(42, 10));

  assert.equal(simulation.state, 'ready');
  assert.equal(simulation.timer, null);
});

test('a refused start does not block a later start', () => {
  // Avant correction, l'echec laissait state='running' : l'appel suivant etait
  // rejete au motif « run is already active » et le run restait bloque.
  const simulation = new Simulation({ replay: { file: path.join(os.tmpdir(), 'absent-replay.jsonl') } }, () => {});

  assert.throws(() => simulation.start(42, 10), /introuvable/);

  // Le second appel atteint bien le chargement du replay, et n'est pas ecarte
  // par la garde d'etat actif : corriger le chemin suffit a demarrer.
  simulation.options.replay.file = tempFile('valid.jsonl', '{"tick":1,"type":"snapshot"}\n');
  const retried = simulation.start(42, 10);

  assert.equal(retried, true);
  assert.equal(simulation.state, 'running');
  assert.ok(simulation.timer, 'le timer doit etre demarre');
  simulation.stop();
});

test('a refused start reports the replay error, not an active-run rejection', () => {
  const simulation = new Simulation({ replay: { file: path.join(os.tmpdir(), 'absent-replay.jsonl') } }, () => {});

  assert.throws(() => simulation.start(42, 10), /introuvable/);
  assert.throws(() => simulation.start(42, 10), /introuvable/,
    'le second echec doit venir du replay, pas de la garde « run deja actif »');
});

test('the event buffer stays bounded and keeps the most recent events', () => {
  const cap = 50;
  const simulation = new Simulation({ agents: 5, maxTicks: 40, diagnostics: { maxEvents: cap } }, () => {});
  simulation.start(42, 40);
  for (let i = 0; i < 40; i++) simulation.step();
  simulation.stop();

  assert.ok(simulation.events.length > 0, 'des evenements ont bien ete produits');
  assert.ok(simulation.events.length <= cap, `le tampon depasse la borne : ${simulation.events.length} > ${cap}`);

  const ticks = simulation.events.map(event => event.tick);
  assert.deepEqual(ticks, [...ticks].sort((a, b) => a - b), 'les evenements restent ordonnes');
  assert.equal(ticks.at(-1), 40, 'le tick le plus recent est conserve');
  assert.ok(ticks[0] > 1, 'les premiers evenements ont bien ete liberes');
});

test('the event buffer is unbounded only if the bound is disabled', () => {
  const simulation = new Simulation({ agents: 3, maxTicks: 20, diagnostics: { maxEvents: 0 } }, () => {});
  simulation.start(42, 20);
  for (let i = 0; i < 20; i++) simulation.step();
  simulation.stop();
  assert.ok(simulation.events.length > 50, 'une borne desactivee laisse le tampon grossir');
});

test('goal age counts ticks since the intention last changed', () => {
  const decisions = new AgentDecisionService(
    DEFAULTS.agentSimulation,
    { food: 100, water: 100, wood: 100, mineral: 100 },
    { move: () => ({ moved: false, blocked: false }) }
  );
  const world = { width: 400, height: 300, cellSize: 10, cellCountX: 40, cellCountY: 30, cells: [], obstacles: [] };
  const agent = {
    id: 1, x: 50, y: 50, energy: 100, traits: { ...DEFAULTS.agentSimulation.traits },
    hunger: 0, thirst: 0, fatigue: 0, safety: 1, social: 0, curiosity: 0,
    currentIntention: 'Idle', currentAction: 'Idle', goals: [], intentionSince: 0, memoryCount: 0
  };

  agent.hunger = 100;
  for (let tick = 1; tick <= 3; tick++) {
    agent.hunger = 100;
    decisions.execute(agent, tick, world, []);
    assert.equal(agent.currentIntention, 'Eat');
    assert.equal(agent.goals.length, 1);
    assert.equal(agent.goals[0].age, tick - 1, `age vaut la duree du but, pas le tick (tick ${tick})`);
  }

  agent.hunger = 0;
  agent.fatigue = 100;
  decisions.execute(agent, 4, world, []);
  assert.equal(agent.currentIntention, 'Rest', 'le but change quand l intention change');
  assert.equal(agent.goals[0].age, 0, 'un but nouvellement pris a un age nul');

  agent.fatigue = 100;
  decisions.execute(agent, 5, world, []);
  assert.equal(agent.goals[0].age, 1, 'l age reprend a 1 au tick suivant');
});

test('a snapshot never publishes the internal intention marker', () => {
  const simulation = new Simulation({ agents: 3, maxTicks: 5 }, () => {});
  simulation.start(42, 5);
  for (let i = 0; i < 5; i++) simulation.step();
  simulation.stop();

  const agents = simulation.snapshot().agents;
  for (const published of agents) {
    assert.equal('intentionSince' in published, false, 'intentionSince ne doit pas etre publie');
    for (const goal of published.goals)
      assert.ok(goal.age <= simulation.tick, 'l age publie reste borne par le tick courant');
  }
});
