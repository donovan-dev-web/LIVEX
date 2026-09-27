const { createSeededRandom, round } = require('../simulation/deterministic-random');
const { isPositionBlocked } = require('./obstacle-service');

function createInitialAgents(seed, count, world, agentSettings, obstacles) {
  const random = createSeededRandom(seed);
  const agents = [];
  const spacing = Math.max(0.1, Number(agentSettings.spawnSpacing ?? 1));

  for (let index = 0; index < count; index++) {
    let position = null;
    for (let attempt = 0; attempt < 1000; attempt++) {
      const candidate = {
        x: random() * world.width,
        y: random() * world.height
      };
      const clear = !isPositionBlocked(candidate, obstacles, world.cellSize) &&
        agents.every(agent => Math.hypot(agent.x - candidate.x, agent.y - candidate.y) >= spacing);
      if (clear) {
        position = candidate;
        break;
      }
    }

    if (!position) {
      for (let y = 0; y < world.height && !position; y += world.cellSize) {
        for (let x = 0; x < world.width && !position; x += world.cellSize) {
          const candidate = { x: x + world.cellSize / 2, y: y + world.cellSize / 2 };
          if (!isPositionBlocked(candidate, obstacles, world.cellSize) &&
              agents.every(agent => Math.hypot(agent.x - candidate.x, agent.y - candidate.y) >= spacing))
            position = candidate;
        }
      }
    }

    if (!position) throw new Error(`Unable to place agent ${index + 1}: no unoccupied walkable position remains`);
    agents.push({
      id: index + 1,
      species: agentSettings.species ?? 'human',
      x: round(position.x),
      y: round(position.y),
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
