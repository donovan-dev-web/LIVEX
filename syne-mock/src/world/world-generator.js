const { createInitialAgents } = require('./agent-factory');
const { OBSTACLE_SHAPES, isCellBlocked, createInitialObstacles } = require('./obstacle-service');
const { generateResourceLocations } = require('./resource-service');
const { TERRAIN_PROFILES, generateTerrainGrid } = require('./terrain-service');

class WorldGenerator {
  generate({ seed, ticksPerSecond, config }) {
    const { width, height, cellSize } = config.world;
    const cellCountX = Math.ceil(width / cellSize);
    const cellCountY = Math.ceil(height / cellSize);
    // Grille de reference partagee par le placement et par le deplacement : les
    // deux lisent `cells[]` via cell-grid, donc les memes cases.
    const grid = { width, height, cellSize, cellCountX, cellCountY, cells: null };

    this.validateConfig(config);

    // Terrain et obstacles sont construits d'abord pour que la marchabilité des cases tienne compte des deux.
    const obstacles = createInitialObstacles(config.world, seed, cellSize);
    const terrainGrid = generateTerrainGrid(seed, cellCountX, cellCountY, config.world.terrain);

    const cells = [];
    for (let y = 0; y < cellCountY; y++) {
      for (let x = 0; x < cellCountX; x++) {
        const obstacleIds = obstacles
          .filter(obstacle => isCellBlocked(x, y, cellSize, [obstacle]))
          .map(obstacle => obstacle.id)
          .sort();
        const terrainType = terrainGrid[y][x];
        const terrainProfile = TERRAIN_PROFILES[terrainType];
        cells.push({
          x, y, terrainType,
          walkable: obstacleIds.length === 0 && terrainProfile.walkable,
          height: 0,
          movementCost: obstacleIds.length > 0 ? 100 : terrainProfile.movementCost,
          obstacles: obstacleIds
        });
      }
    }
    grid.cells = cells;

    const resources = generateResourceLocations(seed, cellCountX, cellCountY, cells);
    const regions = this.generateRegions(cellCountX, cellCountY);
    const initialAgents = createInitialAgents(seed, config.agentCount, grid, config.agentSimulation);
    const world = {
      version: '1.0',
      width,
      height,
      cellSize,
      ticksPerSecond,
      cellCountX,
      cellCountY,
      agents: initialAgents.map(agent => ({
        id: agent.id,
        species: agent.species,
        position: { x: agent.x, y: agent.y }
      })),
      cells,
      obstacles: obstacles.map(obstacle => ({ ...obstacle })),
      resources,
      regions
    };

    return { world, initialAgents, obstacles };
  }

  generateRegions(cellCountX, cellCountY, regionSize = 10) {
    const regions = [];
    for (let y = 0; y < cellCountY; y += regionSize) {
      for (let x = 0; x < cellCountX; x += regionSize) {
        regions.push({
          id: `chunk-${x / regionSize}-${y / regionSize}`,
          x,
          y,
          width: Math.min(regionSize, cellCountX - x),
          height: Math.min(regionSize, cellCountY - y)
        });
      }
    }
    return regions;
  }

  validateConfig(config) {
    const { width, height, cellSize } = config.world;
    if (!Number.isFinite(width) || width <= 0 || !Number.isFinite(height) || height <= 0)
      throw new Error('world.width and world.height must be positive finite numbers');
    if (!Number.isFinite(cellSize) || cellSize <= 0)
      throw new Error('world.cellSize must be a positive finite number');
    if (!Number.isInteger(config.agentCount) || config.agentCount < 0)
      throw new Error('agents must be a non-negative integer');
    for (const kind of ['food', 'water', 'wood', 'mineral']) {
      const quantity = config.resources[kind];
      if (!Number.isFinite(quantity) || quantity < 0)
        throw new Error(`resources.${kind} must be a non-negative finite number`);
    }
    for (const [kind, rate] of Object.entries(config.resources.regeneration)) {
      if (!Number.isFinite(rate) || rate < 0)
        throw new Error(`resources.regeneration.${kind} must be a non-negative finite number`);
    }
    for (const [name, value] of Object.entries(config.agentSimulation.traits)) {
      if (!Number.isFinite(value) || value < 0 || value > 2)
        throw new Error(`agents.traits.${name} must be between 0 and 2`);
    }
    for (const [name, value] of Object.entries(config.agentSimulation.needs)) {
      if (!Number.isFinite(value) || value < 0)
        throw new Error(`agents.needs.${name} must be a non-negative finite number`);
      if (name.endsWith('TriggerThreshold') && value > 100)
        throw new Error(`agents.needs.${name} must be between 0 and 100`);
    }
    if (!Number.isFinite(config.agentSimulation.perceptionRadius) || config.agentSimulation.perceptionRadius < 0)
      throw new Error('agents.perceptionRadius must be a non-negative finite number');
    if (!Number.isInteger(config.agentSimulation.deliberationIntervalTicks) ||
        config.agentSimulation.deliberationIntervalTicks < 1)
      throw new Error('agents.deliberationIntervalTicks must be a positive integer');
    for (const name of [
      'moveEnergyCost', 'restEnergyGain', 'restFatigueRecovery',
      'eatEnergyCost', 'eatHungerRecovery', 'drinkEnergyCost',
      'drinkThirstRecovery', 'reserveConsumption'
    ]) {
      const value = config.agentSimulation[name];
      if (!Number.isFinite(value) || value < 0)
        throw new Error(`agents.${name} must be a non-negative finite number`);
    }

    const obstacleIds = new Set();
    for (const obstacle of config.world.obstacleLayout ?? []) {
      if (!obstacle.id || !Number.isFinite(Number(obstacle.x)) || !Number.isFinite(Number(obstacle.y)))
        throw new Error('world.obstacleLayout entries require an id and a finite position');
      if (obstacleIds.has(String(obstacle.id)))
        throw new Error(`world.obstacleLayout contains duplicate id ${obstacle.id}`);
      obstacleIds.add(String(obstacle.id));
      if (Number(obstacle.x) < 0 || Number(obstacle.x) > width ||
          Number(obstacle.y) < 0 || Number(obstacle.y) > height)
        throw new Error(`world.obstacleLayout obstacle ${obstacle.id} is outside the world`);

      if (obstacle.type === 'shape') {
        if (!OBSTACLE_SHAPES[obstacle.shape])
          throw new Error(`world.obstacleLayout obstacle ${obstacle.id} references unknown shape "${obstacle.shape}"`);
      } else if (!Number.isFinite(Number(obstacle.radius)) || Number(obstacle.radius) <= 0) {
        throw new Error(`world.obstacleLayout obstacle ${obstacle.id} requires a positive radius`);
      }
    }

    const procedural = config.world.proceduralObstacles;
    if (procedural?.enabled) {
      if (!Number.isInteger(procedural.count) || procedural.count < 0)
        throw new Error('world.proceduralObstacles.count must be a non-negative integer');
      if (procedural.shapes && procedural.shapes.some(shape => !OBSTACLE_SHAPES[shape]))
        throw new Error('world.proceduralObstacles.shapes contains an unknown shape name');
      if (procedural.minRadius != null && procedural.maxRadius != null &&
          Number(procedural.minRadius) > Number(procedural.maxRadius))
        throw new Error('world.proceduralObstacles.minRadius must not exceed maxRadius');
    }

    const terrain = config.world.terrain;
    if (terrain) {
      if (terrain.seaDepthRatio != null && (terrain.seaDepthRatio < 0 || terrain.seaDepthRatio >= 1))
        throw new Error('world.terrain.seaDepthRatio must be between 0 and 1');
      if (terrain.riverCount != null && (!Number.isInteger(terrain.riverCount) || terrain.riverCount < 0))
        throw new Error('world.terrain.riverCount must be a non-negative integer');
      if (terrain.riverWidth != null && (!Number.isInteger(terrain.riverWidth) || terrain.riverWidth < 1))
        throw new Error('world.terrain.riverWidth must be a positive integer');
    }
  }
}

module.exports = { WorldGenerator };
