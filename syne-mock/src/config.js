const DEFAULTS = {
  seed: 42,
  agents: 50,
  maxTicks: 400,
  ticksPerSecond: 10,
  // Parité de contrat SYNE (ADR-017) : secondes simulées par tick — 60 au
  // défaut (1 tick = 1 minute simulée), 5 pour le profil `prism`. Le temps
  // émis est `tick × simulatedSecondsPerTick`, jamais dérivé de la cadence.
  simulatedSecondsPerTick: 60,
  world: {
    width: 500,
    height: 500,
    cellSize: 10,
    // Parité de contrat SYNE (ADR-017) : mètres par unité — informatif,
    // transmis aux clients (PRISM k = 100 uu / unité).
    metersPerUnit: 1,
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
      // Besoins de départ, configurables via `agentSimulation.needs.initial`.
      // Ils décrivent ce qu'un agent hérite du monde à la génération, pas ce que
      // SYNE considère comme un agent neuf. Seul `curiosity` diffère d'un agent
      // « à zéro » : à 0.3, exactement le seuil d'exploration, les agents partent
      // explorer au tick 1 au lieu d'attendre les 150 ticks que met
      // `curiosityDriftRate` àfranchir. Les autres besoins restent à 0 pour que
      // Eat/Drink/Rest surviennent au même moment qu'une population qui n'a rien
      // fait encore.
      initial: { hunger: 0, thirst: 0, fatigue: 0, safety: 1, social: 0, curiosity: 0.3 },
      hungerRate: 0.5,
      thirstRate: 0.7,
      fatigueRate: 0.3,
      safetyDriftRate: 0.001,
      socialDriftRate: 0.001,
      // 0.001 et non 0.002 : explorer dès le tick 1 sature `curiosity` bien plus
      // tôt, et un besoin saturé garde une utilité supérieure à celle de `Rest`.
      // Mesuré sur la graine 42 avec 50 agents, 0.002 fait disparaître `Rest`
      // (jamais produit) alors que 0.001 conserve le répertoire complet :
      // Explore:1, Drink:72, Eat:100, Rest:234, Socialize:943.
      curiosityDriftRate: 0.001,
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
  // `simulation.events` est un tampon de diagnostic, pas l'historique complet :
  // le flux faisant autorite reste la diffusion (WebSocket). Sans borne, un run
  // long accumulait deux evenements par agent et par tick indefiniment.
  diagnostics: { maxEvents: 2000 },
  replay: { file: null, loop: false },
  // Parité de contrat avec SYNE (ObservabilityContract.cs) : la version du
  // contrat d'observabilité (0.3.0 : champs additifs inventory/commitments) et
  // la version moteur (0.14.0 : ADR cognitifs D7/D8/D5/D3/D2) annoncées sont
  // celles du moteur réel que le mock émule.
  engineVersion: '0.14.0',
  // 0.4.0 (ADR-017) : champs additifs simulatedTimeSeconds / simulatedSecondsPerTick /
  // metersPerUnit — parité du contrat d'observabilité SYNE.
  contractVersion: '0.4.0'
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
