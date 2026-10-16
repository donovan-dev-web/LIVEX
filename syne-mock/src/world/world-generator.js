/**
 * Projet : LIVEX (SYNE-MOCK)
 * Auteur : CHARTRAIN Donovan
 * Rôle : Génération déterministe du monde (cellules, obstacles, ressources, agents).
 *
 * Parité de description (`WorldDescriptionBuilder.Build`, SYNE) : le mock publie
 * exactement ce que le moteur publie, et rien de plus. En particulier les
 * cellules suivent la même règle — `terrainType` toujours `plains` (le moteur
 * n'a pas de biome), `walkable` vrai si la case ne porte aucun obstacle,
 * `movementCost` 1 sur une case libre et 100 sur une case bloquée. Un
 * consommateur (PRISM) ne doit pas avoir à traiter un cas que le moteur ne
 * produit jamais : ni mer, ni rivière, ni coût non entier.
 */

const { createInitialAgents } = require('./agent-factory');
const { isCellBlocked, createInitialObstacles } = require('./obstacle-service');
const { generateResourceLocations } = require('./resource-service');

class WorldGenerator {
  generate({ seed, ticksPerSecond, config }) {
    const { width, height, cellSize } = config.world;
    const cellCountX = Math.ceil(width / cellSize);
    const cellCountY = Math.ceil(height / cellSize);
    // Grille de reference partagee par le placement et par le deplacement : les
    // deux lisent `cells[]` via cell-grid, donc les memes cases.
    const grid = { width, height, cellSize, cellCountX, cellCountY, cells: null };

    this.validateConfig(config);

    const obstacles = createInitialObstacles(config.world);

    const cells = [];
    for (let y = 0; y < cellCountY; y++) {
      for (let x = 0; x < cellCountX; x++) {
        const obstacleIds = obstacles
          .filter(obstacle => isCellBlocked(x, y, cellSize, [obstacle]))
          .map(obstacle => obstacle.id)
          .sort();
        cells.push({
          x, y,
          terrainType: 'plains',
          walkable: obstacleIds.length === 0,
          height: 0,
          movementCost: obstacleIds.length > 0 ? 100 : 1,
          obstacles: obstacleIds
        });
      }
    }
    grid.cells = cells;

    const resources = generateResourceLocations(seed, cellCountX, cellCountY, cells);
    const regions = this.generateRegions(cellCountX, cellCountY);
    const initialAgents = createInitialAgents(seed, config.agentCount, grid, config.agentSimulation);
    const world = {
      // 1.1 : champs additifs simulatedSecondsPerTick / metersPerUnit.
      version: '1.1',
      width,
      height,
      cellSize,
      ticksPerSecond,
      simulatedSecondsPerTick: config.simulatedSecondsPerTick,
      metersPerUnit: config.world.metersPerUnit,
      cellCountX,
      cellCountY,
      agents: initialAgents.map(agent => ({
        id: agent.id,
        species: agent.species,
        position: { x: agent.x, y: agent.y }
      })),
      cells,
      // Ordre stable, comme le moteur qui trie les obstacles par identifiant
      // (StringComparer.Ordinal) : deux sources ne doivent pas différer par
      // l'ordre du tableau.
      obstacles: obstacles
        .map(obstacle => ({ ...obstacle }))
        .sort((a, b) => (a.id < b.id ? -1 : a.id > b.id ? 1 : 0)),
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
    const { width, height, cellSize, metersPerUnit } = config.world;
    if (!Number.isFinite(width) || width <= 0 || !Number.isFinite(height) || height <= 0)
      throw new Error('world.width and world.height must be positive finite numbers');
    if (!Number.isFinite(cellSize) || cellSize <= 0)
      throw new Error('world.cellSize must be a positive finite number');
    // Échelle temporelle (de la spec PRISM) : entier dans [1, 3600],
    // comme le validateur SYNE. Le mock ne reproduit pas la classe A — il doit
    // seulement émettre un temps cohérent (spec).
    if (!Number.isInteger(config.simulatedSecondsPerTick) ||
        config.simulatedSecondsPerTick < 1 || config.simulatedSecondsPerTick > 3600)
      throw new Error('simulatedSecondsPerTick must be an integer in [1, 3600]');
    if (!Number.isFinite(metersPerUnit) || metersPerUnit <= 0)
      throw new Error('world.metersPerUnit must be a positive finite number');
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
      // `initial` est un objet de besoins de départ, pas un besoin : il a son
      // propre domaine, validé juste après.
      if (name === 'initial') continue;
      if (!Number.isFinite(value) || value < 0)
        throw new Error(`agents.needs.${name} must be a non-negative finite number`);
      if (name.endsWith('TriggerThreshold') && value > 100)
        throw new Error(`agents.needs.${name} must be between 0 and 100`);
    }
    // Domaines des besoins de départ, alignés sur ceux qu'ils bornent :
    // hunger/thirst/fatigue sont des échelles 0-100, safety/social/curiosity des
    // fractions 0-1, parce que `curiosity >= 0.3` est le trigger d'exploration.
    for (const [name, max] of Object.entries({ hunger: 100, thirst: 100, fatigue: 100, safety: 1, social: 1, curiosity: 1 })) {
      const value = config.agentSimulation.needs.initial?.[name];
      if (!Number.isFinite(value) || value < 0 || value > max)
        throw new Error(`agents.needs.initial.${name} must be a finite number between 0 and ${max}`);
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

    // Clés de fonctionnalités qui n'existent pas dans le moteur. Elles sont
    // refusées au lieu d'être ignorées : une clé silencieusement inconnue est
    // exactement ce qui a laissé la mer, les rivières et les obstacles
    // procéduraux atteindre PRISM alors que SYNE ne sait pas les produire.
    for (const removed of ['terrain', 'proceduralObstacles']) {
      if (config.world[removed] !== undefined)
        throw new Error(
          `world.${removed} does not exist in SYNE: remove the key (the engine only places disc obstacles from world.obstacleLayout)`);
    }

    // Le layout n'accepte que la forme de `StaticObstacleSettings` : identifiant,
    // position, rayon strictement positif. Aucune forme composée, aucun `type` :
    // le moteur ne sait pas les décrire, donc le mock ne doit pas les accepter.
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
      if (obstacle.type !== undefined || obstacle.shape !== undefined)
        throw new Error(
          `world.obstacleLayout obstacle ${obstacle.id} is not a disc: SYNE only places {id, x, y, radius}`);
      if (!Number.isFinite(Number(obstacle.radius)) || Number(obstacle.radius) <= 0)
        throw new Error(`world.obstacleLayout obstacle ${obstacle.id} requires a positive radius`);
    }
  }
}

module.exports = { WorldGenerator };
