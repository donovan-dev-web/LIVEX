const { createSeededRandom, round } = require('../simulation/deterministic-random');
const { cellIndexAt, cellIndexOf } = require('./cell-grid');

// Domaines des besoins : les trois premiers sont des échelles 0-100, les trois
// autres des fractions 0-1 utilisées comme seuils et comme multiplicateurs dans
// `utility()`. `fallback` est l'état d'un agent qui n'a rien fait encore, donc
// la valeur d'origine du mock : c'est ce repli qui rend l'absence de
// configuration équivalente au comportement historique. Les valeurs hors
// domaine sont rejetées en amont par `WorldGenerator.validateConfig`, comme
// toute autre configuration ; ici on ne fait que completer les cles absentes.
const NEEDS = {
  hunger: { range: [0, 100], fallback: 0 },
  thirst: { range: [0, 100], fallback: 0 },
  fatigue: { range: [0, 100], fallback: 0 },
  safety: { range: [0, 1], fallback: 1 },
  social: { range: [0, 1], fallback: 0 },
  curiosity: { range: [0, 1], fallback: 0 }
};

function initialNeeds(agentSettings) {
  const configured = agentSettings.needs?.initial ?? {};
  const resolved = {};
  for (const [need, { fallback }] of Object.entries(NEEDS)) {
    const value = Number(configured[need]);
    resolved[need] = Number.isFinite(value) ? value : fallback;
  }
  return resolved;
}

function createInitialAgents(seed, count, world, agentSettings) {
  const random = createSeededRandom(seed);
  const agents = [];
  const spacing = Math.max(0.1, Number(agentSettings.spawnSpacing ?? 1));
  const needs = initialNeeds(agentSettings);

  // Le placement s'appuie sur cells[].walkable, l'indicateur que le monde publie lui-même,
  // plutôt que sur un recalcul : obstacles et terrain (mer) sont ainsi traités par la même
  // règle que celle exposée dans WorldDescription, et aucune divergence n'est possible.
  // Le déplacement lit la même grille via cell-grid, doncagent et monde ne peuvent pas
  // diverger sur ce qui est franchissable.
  const blocked = new Set();
  for (const cell of world.cells || [])
    if (!cell.walkable) blocked.add(cellIndexOf(world, cell));

  // La position arrondie est celle que le monde publie : c'est donc elle qu'il faut valider.
  const placeable = candidate => {
    const position = { x: round(candidate.x), y: round(candidate.y) };
    if (blocked.has(cellIndexAt(world, position.x, position.y))) return null;
    const spaced = agents.every(agent => Math.hypot(agent.x - position.x, agent.y - position.y) >= spacing);
    return spaced ? position : null;
  };

  for (let index = 0; index < count; index++) {
    let position = null;
    for (let attempt = 0; attempt < 1000 && !position; attempt++)
      position = placeable({ x: random() * world.width, y: random() * world.height });

    // Repli : balayage déterministe de la carte, centre de case par centre de case.
    for (let y = 0; y < world.height && !position; y += world.cellSize)
      for (let x = 0; x < world.width && !position; x += world.cellSize)
        position = placeable({ x: x + world.cellSize / 2, y: y + world.cellSize / 2 });

    if (!position) throw new Error(`Unable to place agent ${index + 1}: no unoccupied walkable position remains`);
    agents.push({
      id: index + 1,
      species: agentSettings.species ?? 'human',
      x: position.x,
      y: position.y,
      energy: 100,
      hunger: needs.hunger,
      thirst: needs.thirst,
      fatigue: needs.fatigue,
      safety: needs.safety,
      social: needs.social,
      curiosity: needs.curiosity,
      traits: { ...agentSettings.traits },
      currentIntention: 'Idle',
      currentAction: 'Idle',
      goals: [],
      intentionSince: 0,
      memoryCount: 0
    });
  }
  return agents;
}

function cloneAgents(agents) {
  return agents.map(agent => ({
    ...agent,
    traits: { ...agent.traits },
    goals: agent.goals.map(goal => ({ ...goal }))
  }));
}

module.exports = { createInitialAgents, cloneAgents, initialNeeds, NEEDS };
