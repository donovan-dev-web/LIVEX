const { createDeterministicOffset } = require('./deterministic-random');

class DestinationService {
  constructor(settings) {
    this.settings = settings;
  }

  choose(agent, tick, action, world) {
    const offset = createDeterministicOffset(
      agent.id, tick, action, this.settings.perceptionRadius);
    return {
      x: Math.max(0, Math.min(world.width, agent.x + offset.x)),
      y: Math.max(0, Math.min(world.height, agent.y + offset.y))
    };
  }
}

module.exports = { DestinationService };
