const fs = require('node:fs');
const path = require('node:path');
const { resolveConfig, merge } = require('../config');
const { WorldGenerator } = require('../world/world-generator');
const { createStocks, advanceStocks } = require('../world/resource-service');
const { cloneAgents } = require('../world/agent-factory');
const { DestinationService } = require('./destination-service');
const { MovementService } = require('./movement-service');
const { AgentDecisionService } = require('./agent-decision-service');
const { SnapshotBuilder } = require('./snapshot-builder');
const { round } = require('./deterministic-random');

function log(message, ...args) {
  console.log(`[SYNE-MOCK ${new Date().toISOString()}] ${message}`, ...args);
}

class Simulation {
  constructor(options = {}, broadcast = () => {}) {
    this.options = resolveConfig(options);
    this.broadcast = broadcast;
    this.worldGenerator = new WorldGenerator();
    this.snapshotBuilder = new SnapshotBuilder();
    this.destinationService = new DestinationService(this.options.agentSimulation);
    this.movementService = new MovementService(this.destinationService);
    this.state = 'idle';
    this.timer = null;
    this.tick = 0;
    this.runId = '';
    this.agents = [];
    this.initialAgents = [];
    this.obstacles = [];
    this.resources = {};
    this.groups = [];
    this.books = [];
    this.events = [];
    this.tickWorldChanges = [];
    this.tickActions = [];
    this.world = null;
    this.worldVersion = null;
    this.worldReadyAcknowledged = false;
    this.explicitPreparation = false;
    this.replay = null;
    this.replayIndex = 0;
  }

  prepare(seed = this.options.seed, ticksPerSecond = this.options.ticksPerSecond, overlay = {}) {
    if (this.state === 'running' || this.state === 'paused') {
      log('PREPARE rejected: run is active (state=%s)', this.state);
      return false;
    }
    if (!Number.isSafeInteger(Number(seed)) || Number(seed) < 0)
      throw new Error('seed must be a non-negative safe integer');
    if (!Number.isInteger(ticksPerSecond) || ticksPerSecond <= 0)
      throw new Error('ticksPerSecond must be a positive integer');

    log('PREPARE start (seed=%s, ticksPerSecond=%s)', seed, ticksPerSecond);
    const previousState = this.state;
    this.state = 'worldPreparing';
    const nextOptions = resolveConfig(merge(this.options, overlay));
    nextOptions.seed = Number(seed);
    nextOptions.ticksPerSecond = ticksPerSecond;
    let generated;
    try {
      generated = this.worldGenerator.generate({
        seed: nextOptions.seed,
        ticksPerSecond,
        config: nextOptions
      });
    } catch (error) {
      this.state = previousState;
      log('PREPARE failed; keeping previous world (error=%s)', error.message);
      throw error;
    }

    this.options = nextOptions;
    this.destinationService = new DestinationService(this.options.agentSimulation);
    this.movementService = new MovementService(this.destinationService);
    this.world = generated.world;
    this.initialAgents = generated.initialAgents;
    this.obstacles = generated.obstacles;
    this.worldVersion = this.world.version;
    this.worldReadyAcknowledged = false;
    this.explicitPreparation = true;
    this.state = 'ready';

    this.broadcast({
      type: 'world_initialized',
      version: this.worldVersion,
      seed: this.options.seed,
      world: this.world
    });
    log('PREPARE complete (world=%sx%s, ticksPerSecond=%s, cells=%s, resources=%s, obstacles=%s, agents=%s)',
      this.world.width, this.world.height, ticksPerSecond, this.world.cells.length,
      this.world.resources.length, this.world.obstacles.length, this.world.agents.length);
    return true;
  }

  acknowledgeReady(version) {
    if (!this.world || (version && version !== this.worldVersion)) {
      log('READY rejected (requestedVersion=%s, worldVersion=%s)',
        version || '<none>', this.worldVersion || '<none>');
      return false;
    }
    this.worldReadyAcknowledged = true;
    log('READY accepted (worldVersion=%s)', this.worldVersion);
    return true;
  }

  start(seed = this.options.seed, maxTicks = this.options.maxTicks, overlay = {}) {
    if (this.state === 'running' || this.state === 'paused') {
      log('START rejected: run is already active (state=%s)', this.state);
      return false;
    }
    if (!Number.isSafeInteger(Number(seed)) || Number(seed) < 0)
      throw new Error('seed must be a non-negative safe integer');
    if (!Number.isSafeInteger(Number(maxTicks)) || Number(maxTicks) <= 0)
      throw new Error('maxTicks must be a positive safe integer');
    if (this.world && this.explicitPreparation && !this.worldReadyAcknowledged)
      throw Object.assign(new Error('world_not_ready'), { code: 'world_not_ready' });
    if (this.world && this.explicitPreparation && Number(seed) !== this.options.seed)
      throw Object.assign(new Error('prepared_seed_mismatch'), { code: 'prepared_seed_mismatch' });
    if (this.world && this.explicitPreparation && Object.keys(overlay).length)
      throw Object.assign(new Error('prepared_config_mismatch'), { code: 'prepared_config_mismatch' });

    if (!this.world || Number(seed) !== this.options.seed || Object.keys(overlay).length)
      this.prepare(seed, this.options.ticksPerSecond, overlay);

    // Tout ce qui précède `loadReplay` est réécritable sans conséquence : un
    // échec du chargement laisse donc le monde prêt, acquitté, et surtout pas
    // dans un état actif — un nouvel appel à `start()` se comporte alors comme
    // le premier.
    //
    // `loadReplay` lève sur un fichier absent ou une ligne JSON invalide. Levée
    // après `state = 'running'` et avant `startTimer()`, elle laissait un run
    // déclaré actif mais sans timer (state=running, timer=null) : rien ne
    // tickait, et `start()` rejettait ensuite tout nouvel appel au motif que le
    // run était déjà actif.
    this.worldReadyAcknowledged = true;
    this.explicitPreparation = false;
    this.options.seed = Number(seed);
    this.options.maxTicks = Number(maxTicks);
    const replay = this.options.replay.file ? loadReplay(this.options.replay.file) : null;

    this.runId = `run-${this.options.seed}`;
    this.tick = 0;
    this.state = 'running';
    this.agents = cloneAgents(this.initialAgents);
    this.resources = createStocks(this.options.resources);
    this.obstacles = this.world.obstacles.map(obstacle => ({ ...obstacle }));
    this.groups = this.createInitialGroups();
    this.books = [];
    this.events = [];
    this.tickWorldChanges = [];
    this.tickActions = [];
    this.replayIndex = 0;
    this.replay = replay;
    this.agentDecisionService = new AgentDecisionService(
      this.options.agentSimulation, this.resources, this.movementService);
    this.startTimer();
    log('START complete (runId=%s, agents=%s, ticksPerSecond=%s, maxTicks=%s)',
      this.runId, this.agents.length, this.options.ticksPerSecond, this.options.maxTicks);
    return true;
  }

  createInitialGroups() {
    if (!this.options.groups.enabled || this.agents.length < 2) return [];

    const range = Math.max(0, Number(this.options.groups.formationRange));
    const unassigned = new Set(this.agents.map(agent => agent.id));
    const groups = [];
    for (const first of this.agents) {
      if (!unassigned.has(first.id)) continue;
      const members = [];
      const frontier = [first];
      unassigned.delete(first.id);
      while (frontier.length) {
        const agent = frontier.shift();
        members.push(agent.id);
        for (const candidate of this.agents) {
          if (!unassigned.has(candidate.id) ||
              Math.hypot(agent.x - candidate.x, agent.y - candidate.y) > range) continue;
          unassigned.delete(candidate.id);
          frontier.push(candidate);
        }
      }
      if (members.length < 2) continue;
      groups.push({
        groupId: groups.length + 1,
        members,
        size: members.length,
        leaderId: members[0],
        bornTick: 0,
        cohesion: 1,
        decision: 'Idle',
        consensus: 1
      });
    }
    return groups;
  }

  startTimer() {
    if (this.timer) clearInterval(this.timer);
    this.timer = setInterval(() => this.step(), 1000 / this.options.ticksPerSecond);
  }

  pause() {
    if (this.state !== 'running') return;
    this.state = 'paused';
    if (this.timer) clearInterval(this.timer);
    this.timer = null;
    log('PAUSE complete (runId=%s, tick=%s)', this.runId, this.tick);
  }

  resume() {
    if (this.state !== 'paused') return;
    this.state = 'running';
    this.startTimer();
    log('RESUME complete (runId=%s, tick=%s)', this.runId, this.tick);
  }

  stop() {
    if (this.timer) clearInterval(this.timer);
    this.timer = null;
    const previousState = this.state;
    if (previousState !== 'idle') this.state = 'idle';
    log('STOP complete (runId=%s, tick=%s, previousState=%s)',
      this.runId || '<none>', this.tick, previousState);
  }

  reset(seed = this.options.seed, maxTicks = this.options.maxTicks) {
    log('RESET start (seed=%s, maxTicks=%s)', seed, maxTicks);
    this.stop();
    this.world = null;
    this.worldVersion = null;
    this.worldReadyAcknowledged = false;
    this.explicitPreparation = false;
    return this.start(seed, maxTicks);
  }

  step() {
    if (this.state !== 'running') return;

    this.tick++;
    this.tickWorldChanges = [];
    this.tickActions = [];

    if (this.replay) {
      this.stepReplay();
      return;
    }

    advanceStocks(this.resources, this.options.resources.regeneration);

    // Les événements du tick sont d'abord réunis dans `events` : le snapshot doit
    // partir avant eux. SYNE diffuse « le snapshot puis les événements du tick
    // courant » (ObservabilityTickEmitter, qui cite API_CONTRACTS.md §2), et tout
    // consommateur qui aligne le flux sur les ticks refuse un événement reçu
    // avant le premier snapshot ou portant un autre tick — c'est le cas d'ECHOS
    // (`ingestion.stream.aligned_ticks`), qui rejetait le mock à la première
    // trame avec « decision_made reçu avant le premier snapshot ».
    const events = [];

    for (const agent of this.agents) {
      const result = this.agentDecisionService.execute(agent, this.tick, this.world, this.obstacles);
      this.tickActions.push(result);
      events.push({
        type: 'decision_made',
        tick: this.tick,
        agentId: String(agent.id),
        action: result.action,
        cause: `hunger=${round(agent.hunger)},thirst=${round(agent.thirst)},fatigue=${round(agent.fatigue)}`,
        value: {
          intention: agent.currentIntention,
          utility: result.utility,
          deliberated: result.deliberated,
          interrupted: false
        }
      });
      events.push({
        type: 'action_completed',
        tick: this.tick,
        agentId: String(agent.id),
        action: result.action,
        value: {
          outcome: result.outcome,
          cause: result.cause,
          energyDelta: result.energyDelta,
          hungerDelta: result.hungerDelta,
          thirstDelta: result.thirstDelta,
          fatigueDelta: result.fatigueDelta,
          ...(result.reserve ? { reserve: result.reserve, reserveConsumed: result.reserveConsumed } : {})
        }
      });
    }

    this.emitSocialEvents(events);
    this.emitGroupEvents(events);
    this.emitBookEvents(events);

    this.broadcast(this.snapshot());

    // `tick_summary` suit immédiatement le snapshot, comme SYNE, puis viennent
    // les événements du tick dans leur ordre d'émission.
    for (const event of [
      { type: 'tick_summary', tick: this.tick, value: { aliveCount: this.agents.length } },
      ...events
    ]) {
      this.emit(event);
    }

    if (this.tick >= this.options.maxTicks) this.finish();
  }

  stepReplay() {
    if (this.replayIndex >= this.replay.length) {
      if (this.options.replay.loop) this.replayIndex = 0;
      else return this.finish();
    }
    this.broadcast(this.replay[this.replayIndex++]);
    if (this.replayIndex >= this.replay.length && !this.options.replay.loop) this.finish();
    else if (this.tick >= this.options.maxTicks) this.finish();
  }

  /**
   * Événements de communication du tick, versés dans `events` : l'ordre de
   * diffusion est fixé par `step()`, pas ici.
   */
  emitSocialEvents(events) {
    if (!this.options.communication.enabled || this.agents.length < 2 || this.tick % 5 !== 0) return;
    const sender = this.agents[0];
    const target = this.agents[1];
    if (Math.hypot(sender.x - target.x, sender.y - target.y) >
        this.options.communication.transmissionRange) return;

    const messageId = `${this.options.seed}-${this.tick}-1`;
    events.push({
      type: 'message_sent', tick: this.tick, agentId: String(sender.id),
      targetId: String(target.id), action: 'Information',
      value: { messageId, hops: 0, confidence: 1, payloadLength: 0 }
    });
    events.push({
      type: 'message_received', tick: this.tick, agentId: String(target.id),
      targetId: String(sender.id), action: 'Information',
      value: { messageId, hops: 0, confidence: 1, understood: true }
    });
  }

  emitGroupEvents(events) {
    const interval = Math.max(1, this.options.groups.lodInterval);
    if (!this.options.groups.enabled || !this.groups.length || this.tick % interval !== 0) return;
    const group = this.groups[0];
    group.decision = this.agents[0]?.currentIntention ?? 'Idle';
    events.push({
      type: 'group_decision', tick: this.tick, agentId: String(group.leaderId),
      action: group.decision,
      value: { groupId: group.groupId, decision: group.decision, consensus: group.consensus }
    });
  }

  emitBookEvents(events) {
    if (!this.options.books.enabled || this.tick !== 100 || !this.agents[0]) return;
    const book = {
      id: `book-${this.options.seed}`,
      authorId: this.agents[0].id,
      title: 'Observations',
      content: 'A deterministic observation.',
      writtenTick: this.tick,
      readers: [],
      readCount: 0
    };
    this.books.push(book);
    events.push({
      type: 'world.book_written',
      tick: this.tick,
      agentId: String(this.agents[0].id),
      value: { ...book }
    });
  }

  finish() {
    if (this.timer) clearInterval(this.timer);
    this.timer = null;
    this.state = 'finished';
    log('FINISH complete (runId=%s, tick=%s, maxTicks=%s)',
      this.runId, this.tick, this.options.maxTicks);
  }

  emit(event) {
    this.events.push(event);
    this.broadcast(event);
    this.trimEvents();
  }

  /**
   * Borne `events`, qui garderait deux evenements par agent et par tick sans
   * limite pendant toute la duree du run. Le flux diffuse reste la source faisant
   * autorite : ce tampon ne garde que les derniers evenements, pour inspection.
   *
   * La troncature vide jusqu'a mi-capacite plutot que d'un seul evenement : un
   * `splice(0, 1)` par emission deplacerait tout le tampon a chaque evenement.
   */
  trimEvents() {
    const cap = Number(this.options.diagnostics?.maxEvents) || 0;
    if (cap < 1 || this.events.length <= cap) return;
    this.events.splice(0, this.events.length - Math.floor(cap / 2));
  }

  snapshot() {
    return this.snapshotBuilder.build({
      config: this.options,
      runId: this.runId,
      tick: this.tick,
      agents: this.agents,
      stocks: this.resources,
      obstacles: this.obstacles,
      worldChanges: this.tickWorldChanges,
      actions: this.tickActions,
      groups: this.groups,
      books: this.books
    });
  }

  worldDescription() {
    return this.world;
  }

  status() {
    return {
      state: this.state,
      runId: this.runId || null,
      tick: this.tick,
      aliveCount: this.agents.length,
      seed: this.options.seed,
      maxTicks: this.options.maxTicks,
      ticksPerSecond: this.world?.ticksPerSecond ?? null,
      worldPrepared: !!this.world,
      worldVersion: this.worldVersion,
      worldReadyAcknowledged: this.worldReadyAcknowledged
    };
  }
}

function loadReplay(file) {
  const resolved = path.resolve(file);
  if (!fs.existsSync(resolved)) throw new Error(`Replay JSONL introuvable: ${resolved}`);
  return fs.readFileSync(resolved, 'utf8')
    .split(/\r?\n/)
    .filter(Boolean)
    .map(line => JSON.parse(line));
}

module.exports = { Simulation, log, loadReplay };
