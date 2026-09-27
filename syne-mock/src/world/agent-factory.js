const { createSeededRandom, round } = require('../simulation/deterministic-random');

function createInitialAgents(seed, count, world, agentSettings, obstacles, cells) {
  const random = createSeededRandom(seed);
  const agents = [];
  const spacing = Math.max(0.1, Number(agentSettings.spawnSpacing ?? 1));

  // Le placement s'appuie sur cells[].walkable, l'indicateur que le monde publie lui-même,
  // plutôt que sur un recalcul : obstacles et terrain (mer) sont ainsi traités par la même
  // règle que celle exposée dans WorldDescription, et aucune divergence n'est possible.
  const cellCountX = Math.ceil(world.width / world.cellSize);
  const blocked = new Set();
  for (const cell of cells || [])
    if (!cell.walkable) blocked.add(cell.x * cellCountX + cell.y);

  // La position arrondie est celle que le monde publie : c'est donc elle qu'il faut valider.
  const placeable = candidate => {
    const position = { x: round(candidate.x), y: round(candidate.y) };
    const cellX = Math.floor(position.x / world.cellSize);
    const cellY = Math.floor(position.y / world.cellSize);
    if (blocked.has(cellX * cellCountX + cellY)) return null;
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
      hunger: 0,
      thirst: 0,
      fatigue: 0,
      safety: 1,
      social: 0,
      curiosity: 0,
      traits: { ...agentSettings.traits },
      currentIntention: 'Idle',
      currentAction: 'Idle',
      goals: [],
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

module.exports = { createInitialAgents, cloneAgents };
