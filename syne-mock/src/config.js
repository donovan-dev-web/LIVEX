const DEFAULTS = {
  seed: 42,
  agents: 50,
  maxTicks: 400,
  ticksPerSecond: 10,
  world: {
    width: 500,
    height: 500,
    cellSize: 10,
    obstacles: false,
    obstacleLayout: [],
    proceduralObstacles: {
      enabled: false,
      count: 12,
      shapes: ['single', 'duo', 'line3', 'line3Vertical', 'cross', 'block2x2', 'lShape', 'tShape'],
      minRadius: 4,
      maxRadius: 12
    },
    terrain: {
      seaEdge: 'south',
      seaDepthRatio: 0.12,
      riverCount: 1,
      riverWidth: 1
    }
  },
  agentSimulation: {
    species: 'human',
    traits: {
      bravery: 1,
      curiosity: 1,
      sociability: 1,
      greed: 1,
      pessimism: 1,
      aggressiveness: 1,
      strength: 1,
      speed: 1
    },
    needs: {
      hungerRate: 0.5,
      thirstRate: 0.7,
      fatigueRate: 0.3,
      safetyDriftRate: 0.001,
      socialDriftRate: 0.001,
      curiosityDriftRate: 0.002,
      hungerTriggerThreshold: 50,
      thirstTriggerThreshold: 50,
      fatigueTriggerThreshold: 70
    },
    perceptionRadius: 50,
    deliberationIntervalTicks: 10,
    moveEnergyCost: 0.5,
    restEnergyGain: 0.5,
    restFatigueRecovery: 1,
    eatEnergyCost: 0.2,
    eatHungerRecovery: 30,
    drinkEnergyCost: 0.2,
    drinkThirstRecovery: 30,
    reserveConsumption: 1
  },
  resources: {
    food: 10000,
    water: 10000,
    wood: 50,
    mineral: 0,
    regeneration: { food: 5, water: 5, wood: 0.1, mineral: 0 }
  },
  communication: {
    enabled: true,
    transmissionRange: 55,
    maxSendsPerTick: 5,
    maxReceivesPerTick: 3,
    maxHops: 2
  },
  groups: { enabled: true, lodInterval: 10, formationRange: 25 },
  territories: {
    enabled: false,
    zones: [{ id: 'camp', x: 250, y: 250, radius: 40 }]
  },
  books: { enabled: false },
  replay: { file: null, loop: false },
  engineVersion: '0.11.0',
  contractVersion: '0.2.0'
};

function merge(base, overlay) {
  if (Array.isArray(overlay)) return overlay.map(value => structuredClone(value));
  if (!overlay || typeof overlay !== 'object') return overlay;

  const result = { ...base };
  for (const [key, value] of Object.entries(overlay)) {
    result[key] = value && typeof value === 'object' && !Array.isArray(value)
      ? merge(base?.[key] && typeof base[key] === 'object' ? base[key] : {}, value)
      : Array.isArray(value) ? value.map(item => structuredClone(item)) : value;
  }
  return result;
}

function resolveConfig(options = {}) {
  const config = merge(DEFAULTS, options);
  const configuredAgents = config.agents;
  config.agentCount = typeof configuredAgents === 'number'
    ? configuredAgents
    : configuredAgents?.initialCount ?? DEFAULTS.agents;

  if (configuredAgents && typeof configuredAgents === 'object') {
    config.agentSimulation = merge(config.agentSimulation, configuredAgents);
  }

  if (config.resources.regeneration == null)
    config.resources.regeneration = { ...DEFAULTS.resources.regeneration };

  return config;
}

module.exports = { DEFAULTS, merge, resolveConfig };
