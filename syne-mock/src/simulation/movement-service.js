const { isPositionBlocked } = require('../world/obstacle-service');
const { cellAt } = require('../world/cell-grid');
const { round } = require('./deterministic-random');

/**
 * Position atteignable en un pas, deja arrondie.
 *
 * L'arrondi est applique ici, avant tout test, pour que la position validee
 * soit exactement celle que le snapshot publie ensuite : tester une position
 * non arrondie et publier sa version arrondie autorise un agent a se
 * retrouver dans une case que le test disait libre.
 *
 * Retourne null quand il n'y a rien a tenter (destination deja atteinte, ou
 * vitesse nulle) : ce n'est pas un blocage, seulement une absence de mouvement.
 */
function stepToward(from, target, maxStep) {
  const dx = target.x - from.x;
  const dy = target.y - from.y;
  const distance = Math.hypot(dx, dy);
  if (distance <= 1e-12 || maxStep <= 0) return null;
  const step = Math.min(distance, maxStep);
  return {
    x: round(from.x + dx / distance * step),
    y: round(from.y + dy / distance * step)
  };
}

class MovementService {
  constructor(destinationService) {
    this.destinationService = destinationService;
  }

  /**
   * Une position n'est atteignable que si la carte la publie comme franchissable
   * et si aucun obstacle ne la couvre.
   *
   * `cellAt` renvoie null hors carte, ce qui borne aussi le deplacement aux
   * bords du monde. La grille est celle de WorldDescription : obstacles et
   * terrain (mer) suivent donc exactement la regle publiee, au lieu d'un
   * recalcul qui pourrait diverger.
   */
  canEnter(world, position, obstacles) {
    const cell = cellAt(world, position.x, position.y);
    if (!cell || !cell.walkable) return false;
    // cellSize est indispensable ici : sans lui, la branche `shape` compare
    // des coordonnees NaN et laisse traverser toutes les formes d'obstacle.
    return !isPositionBlocked(position, obstacles, world.cellSize);
  }

  /**
   * Distance maximale par tick, divisee par le cout de terrain.
   *
   * Le cout retenu est le plus eleve entre la case de depart et la case visee :
   * entrer dans une riviere ralentit, et en sortir aussi. Une riviere
   * (movementCost 4) se traverse donc quatre fois moins vite qu'une plaine.
   *
   * Une case non franchissable donne un budget nul. C'est le cas de la mer, que
   * WorldGenerator publie avec walkable: false et movementCost: Infinity : il
   * vaut mieux un agent immobile qu'un agent qui traverse la mer.
   */
  stepBudget(world, from, destination, speed) {
    const departure = cellAt(world, from.x, from.y);
    const arrival = cellAt(world, destination.x, destination.y);
    if (arrival && arrival.walkable === false) return 0;

    const cost = Math.max(
      Number.isFinite(Number(departure?.movementCost)) ? Number(departure.movementCost) : 0,
      Number.isFinite(Number(arrival?.movementCost)) ? Number(arrival.movementCost) : 0
    );
    return cost > 0 ? speed / cost : 0;
  }

  move(agent, tick, action, world, obstacles) {
    const destination = this.destinationService.choose(agent, tick, action, world);
    const speed = Math.max(0, Number(agent.traits.speed ?? 1));
    const previous = { x: agent.x, y: agent.y };
    const budget = this.stepBudget(world, agent, destination, speed);

    const next = budget > 0 ? stepToward(agent, destination, budget) : null;
    if (!next) return { moved: false, blocked: false, destination };

    if (this.canEnter(world, next, obstacles))
      return this.commit(agent, next, destination, previous);

    // SYNE uses A* around blocked raster cells. The mock uses a deterministic
    // local detour so obstacle collisions stay visible without duplicating the
    // engine's full pathfinder.
    const dx = next.x - agent.x;
    const dy = next.y - agent.y;
    const distance = Math.hypot(dx, dy) || 1;
    const detours = [
      { x: -dy / distance, y: dx / distance },
      { x: dy / distance, y: -dx / distance }
    ];
    for (const direction of detours) {
      const candidate = {
        x: round(agent.x + direction.x * budget),
        y: round(agent.y + direction.y * budget)
      };
      if (this.canEnter(world, candidate, obstacles))
        return this.commit(agent, candidate, destination, previous);
    }

    return { moved: false, blocked: true, destination };
  }

  commit(agent, next, destination, previous) {
    agent.x = next.x;
    agent.y = next.y;
    return { moved: agent.x !== previous.x || agent.y !== previous.y, blocked: false, destination };
  }
}

module.exports = { MovementService, stepToward };
