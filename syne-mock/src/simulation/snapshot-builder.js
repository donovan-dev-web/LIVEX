const { round } = require('./deterministic-random');

class SnapshotBuilder {
  build({ config, runId, seed, tick, simulatedTimeMinutes, agents, stocks, obstacles, worldChanges, actions, groups, books }) {
    const seasonIndex = Math.floor(tick / 90) % 4;
    const seasons = ['spring', 'summer', 'autumn', 'winter'];
    const territories = config.territories.enabled
      ? config.territories.zones.map(zone => {
        const members = agents.filter(agent =>
          Math.hypot(agent.x - zone.x, agent.y - zone.y) <= zone.radius)
          .map(agent => agent.id);
        return { ...zone, memberCount: members.length, members };
      })
      : [];

    return {
      type: 'snapshot',
      version: config.contractVersion,
      engineVersion: config.engineVersion,
      runId,
      // Parité de contrat V0.2.1 (API_CONTRACTS.md §2.1) : le seed voyage dans
      // le snapshot — ECHOS n'a plus à le dériver du runId.
      seed,
      tick,
      // Parité de contrat (API_CONTRACTS.md §2.1) : minutes simulées, comme
      // SimulationTime.ToSimulatedMinutes (1 minute = 1 tick à la cadence 1x).
      simulatedTimeMinutes: simulatedTimeMinutes ?? tick,
      aliveCount: agents.length,
      season: seasons[seasonIndex],
      seasonIndex,
      agents: agents.map(agent => ({
        id: String(agent.id),
        species: agent.species,
        position: { x: agent.x, y: agent.y },
        energy: round(agent.energy),
        hunger: round(agent.hunger),
        thirst: round(agent.thirst),
        fatigue: round(agent.fatigue),
        currentIntention: agent.currentIntention,
        currentAction: agent.currentAction,
        traits: { ...agent.traits },
        beliefs: [],
        goals: agent.goals.map(goal => ({ ...goal })),
        trust: [],
        memoryCount: agent.memoryCount
      })),
      resources: Object.entries(stocks).map(([type, quantity]) => ({
        type, quantity: round(quantity)
      })),
      // Parité de contrat (API_CONTRACTS.md §2.1) : les obstacles du snapshot
      // portent la géométrie du monde {id, x, y, radius}, sans les détails de
      // forme interne (SYNE sérialise ObstacleSnapshot(Id, X, Y, Radius)). Les
      // formes multi-cellules, sans rayon, gardent leur ancre avec un rayon nul.
      obstacles: obstacles.map(({ id, x, y, radius }) => ({ id, x, y, radius: radius ?? 0 })),
      territories,
      groups: groups.map(group => ({ ...group, members: [...group.members] })),
      books: books.map(({ cost, ...book }) => ({ ...book, readers: [...book.readers] })),
      worldChanges: worldChanges.map(change => ({ ...change })),
      actions: actions.map(action => ({ ...action }))
    };
  }
}

module.exports = { SnapshotBuilder };
