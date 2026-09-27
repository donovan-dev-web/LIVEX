const { isPositionBlocked } = require('../world/obstacle-service');
const { round } = require('./deterministic-random');

function stepToward(from, target, speed) {
  const dx = target.x - from.x;
  const dy = target.y - from.y;
  const distance = Math.hypot(dx, dy);
  if (distance <= 1e-12 || speed <= 0) return { ...from };
  const step = Math.min(distance, speed);
  return { x: from.x + dx / distance * step, y: from.y + dy / distance * step };
}

class MovementService {
  constructor(destinationService) {
    this.destinationService = destinationService;
  }

  move(agent, tick, action, world, obstacles) {
    const destination = this.destinationService.choose(agent, tick, action, world);
    const speed = Math.max(0, Number(agent.traits.speed ?? 1));
    const next = stepToward(agent, destination, speed);
    const previous = { x: agent.x, y: agent.y };

    if (!isPositionBlocked(next, obstacles)) {
      agent.x = round(next.x);
      agent.y = round(next.y);
      return { moved: agent.x !== previous.x || agent.y !== previous.y, destination };
    }

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
      const candidate = { x: agent.x + direction.x * speed, y: agent.y + direction.y * speed };
      if (candidate.x < 0 || candidate.y < 0 ||
          candidate.x > world.width || candidate.y > world.height ||
          isPositionBlocked(candidate, obstacles)) continue;
      agent.x = round(candidate.x);
      agent.y = round(candidate.y);
      return { moved: true, destination };
    }

    return { moved: false, destination };
  }
}

module.exports = { MovementService, stepToward };
