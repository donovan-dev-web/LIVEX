// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

/**
 * Projet : LIVEX (SYNE-MOCK)
 * Auteur : CHARTRAIN Donovan
 * Rôle : Tests du déplacement déterministe et de l'indexation des cases.
 */

const test = require('node:test');
const assert = require('node:assert/strict');
const { Simulation } = require('../src/server');
const { MovementService, stepToward } = require('../src/simulation/movement-service');
const { cellAt, cellIndexAt, cellIndexOf } = require('../src/world/cell-grid');

// Grille synthetique : `walkable` dicte ce que le mock autorise, comme WorldDescription.
function grid(rows) {
  const cellSize = 10;
  const cellCountY = rows.length;
  const cellCountX = rows[0].length;
  const cells = [];
  for (let y = 0; y < cellCountY; y++)
    for (let x = 0; x < cellCountX; x++)
      cells.push({
        x,
        y,
        terrainType: 'plains',
        height: 0,
        obstacles: [],
        walkable: rows[y][x] !== '#',
        movementCost: rows[y][x] === '#' ? 100 : 1,
      });
  return {
    width: cellCountX * cellSize,
    height: cellCountY * cellSize,
    cellSize,
    cellCountX,
    cellCountY,
    cells,
  };
}

// Un agent complet : AgentDecisionService lit ces champs, et un champ manquant
// devient NaN, ce qui supprime silencieusement toute candidate a une action.
function agent(x, y, speed = 1, needs = {}) {
  return {
    id: 1,
    x,
    y,
    energy: 100,
    traits: { speed },
    hunger: 0,
    thirst: 0,
    fatigue: 0,
    safety: 1,
    social: 0,
    curiosity: 0,
    currentIntention: 'Explore',
    currentAction: 'Idle',
    goals: [],
    ...needs,
  };
}

function serviceAt(destination) {
  return new MovementService({ choose: () => destination });
}

test('cell indexing agrees between world positions and cell coordinates', () => {
  const world = grid(['...', '...', '...']);
  for (let y = 0; y < 3; y++)
    for (let x = 0; x < 3; x++) {
      assert.equal(
        cellIndexAt(world, x * 10 + 5, y * 10 + 5),
        cellIndexOf(world, { x, y }),
        `position monde (${x * 10 + 5},${y * 10 + 5}) et case (${x},${y}) doivent designer la meme cellule`
      );
    }
  assert.equal(cellAt(world, 25, 25).x, 2, 'une position au bord appartient a la derniere colonne');
  assert.equal(cellAt(world, 30, 25), null, 'une position hors carte ne renvoie aucune case');
  assert.equal(cellAt(world, 25, -1), null);
});

test('an obstacle blocks every cell it covers', () => {
  // Un obstacle est un disque qui traverse la carte : quelle que soit la
  // trajectoire, aucune case publiee comme bloquee ne doit etre traversee.
  for (const radius of [3, 8, 20]) {
    const simulation = new Simulation({
      seed: 5,
      agents: 1,
      world: {
        width: 200,
        height: 200,
        cellSize: 10,
        obstacles: true,
        obstacleLayout: [{ id: 'mur', x: 100, y: 100, radius }],
      },
    });
    simulation.prepare(5);
    const world = simulation.worldDescription();
    const occupied = world.cells.filter((cell) => cell.obstacles.includes('mur'));
    assert.ok(occupied.length > 0, `rayon ${radius}: aucune case ne porte l'obstacle`);

    // Depart hors de l'obstacle : un agent place dans une case bloquee aurait
    // deja viole la regle avant le premier pas.
    assert.ok(world.cells.find((cell) => cell.x === 2 && cell.y === 2).walkable);
    const service = serviceAt({ x: 190, y: 190 });
    const walker = agent(25, 25, 4);
    let inside = null;
    for (let i = 0; i < 400; i++) {
      service.move(walker, i, 'Explore', world, world.obstacles);
      const cell = cellAt(world, walker.x, walker.y);
      if (occupied.some((c) => c.x === cell.x && c.y === cell.y)) inside = walker;
    }
    assert.equal(
      inside,
      null,
      `rayon ${radius}: l'agent a fini sur une case de l'obstacle en (${walker.x},${walker.y})`
    );
  }
});

test('agents stay on cells the world publishes as walkable', () => {
  const simulation = new Simulation({
    seed: 7,
    agents: 20,
    world: {
      width: 200,
      height: 200,
      cellSize: 10,
      obstacles: true,
      obstacleLayout: [
        { id: 'bloc-central', x: 100, y: 100, radius: 45 },
        { id: 'bloc-bas', x: 60, y: 170, radius: 25 },
      ],
    },
  });
  simulation.start(7, 400);
  const world = simulation.world;
  assert.ok(
    world.cells.some((cell) => !cell.walkable),
    'les obstacles doivent bloquer des cases'
  );

  for (const initial of simulation.agents)
    assert.equal(
      cellAt(world, initial.x, initial.y).walkable,
      true,
      `agent ${initial.id} place en (${initial.x},${initial.y}) sur une case non franchissable`
    );

  for (let i = 0; i < 400; i++) {
    simulation.step();
    for (const current of simulation.agents) {
      const cell = cellAt(world, current.x, current.y);
      assert.ok(cell, `agent ${current.id} sorti de la carte en (${current.x},${current.y})`);
      assert.equal(
        cell.walkable,
        true,
        `agent ${current.id} dans une case walkable:false (${cell.obstacles.join(',')}) au tick ${i + 1}`
      );
    }
  }
});

test('the published cells carry the SYNE description rule', () => {
  // Regle de `WorldDescriptionBuilder.Build` : `terrainType` vaut plains partout
  // (le moteur n'a pas de biome), `walkable` est vrai si la case ne porte aucun
  // obstacle, `movementCost` vaut 1 ou 100, et un cout est toujours un nombre
  // fini — l'ancienne mer du mock publiait `Infinity`, donc `null` en JSON.
  const simulation = new Simulation({
    seed: 42,
    agents: 4,
    world: { obstacles: true, obstacleLayout: [{ id: 'rock', x: 100, y: 100, radius: 12 }] },
  });
  simulation.prepare(42);
  const world = simulation.worldDescription();

  assert.deepEqual([...new Set(world.cells.map((cell) => cell.terrainType))], ['plains']);
  assert.deepEqual(
    [...new Set(world.cells.map((cell) => cell.movementCost))].sort((a, b) => a - b),
    [1, 100]
  );
  assert.ok(
    world.cells.some((cell) => !cell.walkable),
    'l obstacle doit bloquer au moins une case'
  );
  for (const cell of world.cells) {
    assert.equal(
      cell.walkable,
      cell.obstacles.length === 0,
      `case (${cell.x},${cell.y}) : walkable doit suivre les obstacles portes`
    );
    assert.equal(cell.movementCost, cell.obstacles.length === 0 ? 1 : 100);
    assert.ok(Number.isFinite(cell.movementCost), 'un cout non fini casse le JSON');
  }
  // Le monde decrit est serialisable tel quel : aucun `null` introduit par un
  // `Infinity` ne doit atteindre PRISM ou ECHOS.
  assert.equal(/null/.test(JSON.stringify(world)), false, 'aucun champ ne doit valoir null');
});

test('a cell the world publishes as blocked stops the agent aiming into it', () => {
  const world = grid(['...', '...', '..#']);
  const walker = agent(5, 25, 4);
  const outcome = serviceAt({ x: 25, y: 25 }).move(walker, 1, 'Explore', world, []);

  assert.equal(walker.x, 5, 'aucun pas vers une case non franchissable');
  assert.equal(walker.y, 25);
  assert.equal(outcome.moved, false);
});

test('an agent pinned against the world border never leaves it', () => {
  const world = grid(['...', '...', '...']);
  const service = serviceAt({ x: 40, y: 5 });
  const walker = agent(25, 5, 4);

  for (let tick = 1; tick <= 20; tick++) {
    service.move(walker, tick, 'Explore', world, []);
    const cell = cellAt(world, walker.x, walker.y);
    assert.ok(cell, `agent sorti de la carte au tick ${tick} en (${walker.x},${walker.y})`);
  }
});

test('a refused move costs no energy and is reported as blocked', () => {
  const world = grid(['.....', '.###.', '.#.#.', '.###.', '.....']);
  const simulation = new Simulation({ seed: 1, agents: 0, agentSimulation: { moveEnergyCost: 5 } });
  simulation.start(1, 1);
  simulation.stop();

  const service = new MovementService({ choose: () => ({ x: 45, y: 45 }) });
  const wedged = agent(25, 25, 10, { curiosity: 1 });
  const outcome = service.move(wedged, 1, 'Explore', world, []);

  assert.equal(outcome.moved, false);
  assert.equal(outcome.blocked, true);
  assert.equal(wedged.x, 25);
  assert.equal(wedged.y, 25);

  const { AgentDecisionService } = require('../src/simulation/agent-decision-service');
  const decisions = new AgentDecisionService(
    simulation.options.agentSimulation,
    { food: 1, water: 1, wood: 1, mineral: 1 },
    service
  );
  const result = decisions.execute(wedged, 1, world, []);

  assert.equal(result.outcome, 'blocked');
  assert.equal(result.cause, 'déplacement bloqué');
  assert.equal(result.energyDelta, 0, 'un deplacement refuse ne doit rien facturer');
  assert.equal(wedged.energy, 100);
  assert.equal(wedged.currentAction, 'Idle', 'currentAction reflects what happened');
  assert.equal(wedged.currentIntention, 'Explore', 'currentIntention keeps the decision');
});

test('a move that happens costs exactly moveEnergyCost', () => {
  const simulation = new Simulation({ seed: 1, agents: 0, agentSimulation: { moveEnergyCost: 5 } });
  simulation.start(1, 1);
  simulation.stop();
  const world = grid(['...', '...', '...']);
  const service = serviceAt({ x: 29, y: 5 });
  const walker = agent(5, 5, 4, { curiosity: 1 });
  const { AgentDecisionService } = require('../src/simulation/agent-decision-service');
  const decisions = new AgentDecisionService(
    simulation.options.agentSimulation,
    { food: 1, water: 1, wood: 1, mineral: 1 },
    service
  );

  const result = decisions.execute(walker, 1, world, []);
  assert.equal(walker.currentIntention, 'Explore');
  assert.equal(result.outcome, 'executed');
  assert.equal(result.energyDelta, -5);
  assert.equal(walker.x, 9);
});

test('an already reached destination is not a blockage', () => {
  const world = grid(['...', '...', '...']);
  const walker = agent(15, 15);
  const outcome = serviceAt({ x: 15, y: 15 }).move(walker, 1, 'Explore', world, []);
  assert.equal(outcome.moved, false);
  assert.equal(outcome.blocked, false, 'etre arrive n est pas un blocage');
});

test('the published position is the one that was validated', () => {
  // L arrondi est applique avant le test, sinon un agent peut etre publie dans
  // une case que le test disait libre.
  const world = grid(['...', '...', '...']);
  const walker = agent(0, 15);
  const step = stepToward(walker, { x: 29.4, y: 15 }, 29.4);
  assert.deepEqual(step, { x: 29.4, y: 15 }, 'la position calculee est deja arrondie');
  const world2 = grid(['..#', '...', '...']);
  const blocked = serviceAt({ x: 29.4, y: 15 });
  const walker2 = agent(0, 15);
  const outcome = blocked.move(walker2, 1, 'Explore', world2, []);
  assert.equal(outcome.moved, true);
  assert.equal(
    cellAt(world2, walker2.x, walker2.y).walkable,
    true,
    'la position publiee appartient a une case franchissable'
  );
});
