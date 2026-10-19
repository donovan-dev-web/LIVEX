// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

/**
 * Projet : LIVEX (SYNE-MOCK)
 * Auteur : CHARTRAIN Donovan
 * Rôle : Obstacles statiques du mock — surface exacte de WorldDescription.
 *
 * Le moteur n'a qu'une seule forme d'obstacle : le disque
 * (`StaticObstacleSettings { Id, X, Y, Radius }`, SYNE-071). Le mock ne connaît
 * donc que celle-là, et publie directement la géométrie du contrat
 * (`{ id, x, y, radius }`, comme `WorldObstacle`) sans représentation interne
 * supplémentaire : aucune forme, aucun `type`, aucun obstacle procédural ne
 * peut atteindre un consommateur (PRISM, ECHOS) que le moteur ne produirait pas.
 */

/** Projection d'un obstacle configuré sur la géométrie publiée par WorldDescription. */
function normalizeObstacle(rawObstacle) {
  return {
    id: String(rawObstacle.id),
    x: Number(rawObstacle.x),
    y: Number(rawObstacle.y),
    radius: Number(rawObstacle.radius),
  };
}

/**
 * Obstacles initiaux : le layout configuré, et rien d'autre.
 *
 * `world.obstacles` est le même drapeau que côté moteur : faux par défaut, et le
 * layout n'est posé que s'il est vrai. Aucun tirage de PRNG n'est consommé ici,
 * donc ajouter un layout ne décale pas la génération du reste du monde.
 */
function createInitialObstacles(worldOptions) {
  if (!worldOptions.obstacles) return [];
  return (worldOptions.obstacleLayout ?? []).map(normalizeObstacle);
}

/** Vrai si la position est couverte par un disque (test point contre cercle). */
function isPositionBlocked(position, obstacles) {
  return obstacles.some(
    (obstacle) => Math.hypot(position.x - obstacle.x, position.y - obstacle.y) <= obstacle.radius
  );
}

/**
 * Vrai si la case est couverte, avec la règle exacte de `WorldDescriptionBuilder` :
 * le centre de la case est à `radius + cellSize / 2` ou moins du centre du disque.
 * C'est cette règle qui décide de `cells[].walkable` et de `cells[].obstacles`,
 * pas un recalcul propre au mock.
 */
function isCellBlocked(cellX, cellY, cellSize, obstacles) {
  const center = { x: (cellX + 0.5) * cellSize, y: (cellY + 0.5) * cellSize };
  return obstacles.some(
    (obstacle) =>
      Math.hypot(center.x - obstacle.x, center.y - obstacle.y) <= obstacle.radius + cellSize / 2
  );
}

module.exports = { createInitialObstacles, isPositionBlocked, isCellBlocked };
