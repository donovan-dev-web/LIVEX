// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

/**
 * Projet : LIVEX (SYNE-MOCK)
 * Auteur : CHARTRAIN Donovan
 * Rôle : Construction des snapshots du monde et des agents à chaque tick.
 */

const { round } = require('./deterministic-random');

class SnapshotBuilder {
  build({
    config,
    runId,
    seed,
    tick,
    simulatedTimeSeconds,
    simulatedTimeMinutes,
    agents,
    stocks,
    obstacles,
    worldChanges,
    actions,
    groups,
    books,
  }) {
    const seasonIndex = Math.floor(tick / 90) % 4;
    const seasons = ['spring', 'summer', 'autumn', 'winter'];
    const territories = config.territories.enabled
      ? config.territories.zones.map((zone) => {
        const members = agents
          .filter((agent) => Math.hypot(agent.x - zone.x, agent.y - zone.y) <= zone.radius)
          .map((agent) => agent.id);
        return { ...zone, memberCount: members.length, members };
      })
      : [];

    return {
      type: 'snapshot',
      version: config.contractVersion,
      engineVersion: config.engineVersion,
      runId,
      // Parité de contrat V0.2.1 : le seed voyage dans
      // le snapshot — ECHOS n'a plus à le dériver du runId.
      seed,
      tick,
      // Parité de contrat : minutes simulées (plancher
      // entier, stockage ECHOS INTEGER) + secondes simulées exactes (additif
      // contrat 0.4.0) — `tick × simulatedSecondsPerTick`.
      simulatedTimeMinutes: simulatedTimeMinutes ?? tick,
      simulatedTimeSeconds: simulatedTimeSeconds ?? tick * config.simulatedSecondsPerTick,
      aliveCount: agents.length,
      season: seasons[seasonIndex],
      seasonIndex,
      agents: agents.map((agent) => ({
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
        goals: agent.goals.map((goal) => ({ ...goal })),
        trust: [],
        memoryCount: agent.memoryCount,
      })),
      resources: Object.entries(stocks).map(([type, quantity]) => ({
        type,
        quantity: round(quantity),
      })),
      // Parité de contrat : les obstacles du snapshot
      // portent la géométrie du monde {id, x, y, radius}, sans les détails de
      // forme interne (SYNE sérialise ObstacleSnapshot(Id, X, Y, Radius)). Les
      // formes multi-cellules, sans rayon, gardent leur ancre avec un rayon nul.
      obstacles: obstacles.map(({ id, x, y, radius }) => ({ id, x, y, radius: radius ?? 0 })),
      territories,
      // Contrat 0.5.0 (additif) : `maxBondDistance`/`meanBondDistance` sont
      // publiées dans `groups[]`. Le mock n'a pas de modèle de liens : la
      // valeur est NON MESURÉE (`null`), jamais 0.0 — la même distinction que
      // SYNE entre absence de mesure et zéro observé.
      groups: groups.map((group) => ({
        ...group,
        maxBondDistance: null,
        meanBondDistance: null,
        members: [...group.members],
      })),
      books: books.map(({ cost, ...book }) => ({ ...book, readers: [...book.readers] })),
      worldChanges: worldChanges.map((change) => ({ ...change })),
      // Parité de forme `ActionSnapshot` (SYNE) : `actions[]` porte agentId,
      // action, outcome, cause et les deltas (+ reserve/reserveConsumed si
      // consommée). Les champs internes de la décision (`utility`,
      // `deliberated`) restent dans l'événement `decision_made`, pas ici.
      actions: actions.map((action) => ({
        agentId: String(action.agentId),
        action: action.action,
        outcome: action.outcome,
        cause: action.cause ?? null,
        energyDelta: action.energyDelta,
        hungerDelta: action.hungerDelta,
        thirstDelta: action.thirstDelta,
        fatigueDelta: action.fatigueDelta,
        ...(action.reserve
          ? { reserve: action.reserve, reserveConsumed: action.reserveConsumed }
          : {}),
      })),
    };
  }
}

module.exports = { SnapshotBuilder };
