// Chaque forme est une liste de décalages [dx, dy] (en cases) par rapport à la case d'ancrage.
const OBSTACLE_SHAPES = {
  single: [[0, 0]],
  duo: [[0, 0], [1, 0]],
  line3: [[0, 0], [1, 0], [2, 0]],
  line3Vertical: [[0, 0], [0, 1], [0, 2]],
  cross: [[0, 0], [1, 0], [-1, 0], [0, 1], [0, -1]],
  block2x2: [[0, 0], [1, 0], [0, 1], [1, 1]],
  lShape: [[0, 0], [0, 1], [0, 2], [1, 2]],
  tShape: [[0, 0], [1, 0], [2, 0], [1, 1]]
};

function createSeededRandom(seed, salt) {
  let state = ((Number(seed) >>> 0) ^ (Math.floor(Number(seed) / 0x100000000) >>> 0) ^ salt) >>> 0;
  return () => {
    state = (Math.imul(state, 1664525) + 1013904223) >>> 0;
    return state / 0xffffffff;
  };
}

function toCellShape(shape, anchorX, anchorY) {
  const template = OBSTACLE_SHAPES[shape] ?? OBSTACLE_SHAPES.single;
  return template.map(([dx, dy]) => ({ x: anchorX + dx, y: anchorY + dy }));
}

// Un obstacle "circle" garde l'ancien comportement (position + rayon, en unités monde).
// Un obstacle "shape" est ancré sur une case (dérivée de x/y) et couvre une ou plusieurs cases.
function normalizeObstacle(rawObstacle, cellSize) {
  const id = String(rawObstacle.id);

  if (rawObstacle.type === 'shape') {
    const anchorX = Math.floor(Number(rawObstacle.x) / cellSize);
    const anchorY = Math.floor(Number(rawObstacle.y) / cellSize);
    return {
      id,
      type: 'shape',
      shape: rawObstacle.shape ?? 'single',
      x: Number(rawObstacle.x),
      y: Number(rawObstacle.y),
      cells: toCellShape(rawObstacle.shape ?? 'single', anchorX, anchorY)
    };
  }

  return {
    id,
    type: 'circle',
    x: Number(rawObstacle.x),
    y: Number(rawObstacle.y),
    radius: Number(rawObstacle.radius)
  };
}

// Disperse des obstacles supplémentaires (cercles et formes) de façon déterministe,
// en complément (optionnel) des obstacles définis manuellement dans obstacleLayout.
function generateProceduralObstacles(seed, worldOptions, cellSize) {
  const settings = worldOptions.proceduralObstacles;
  if (!settings?.enabled) return [];

  const {
    count = 0,
    shapes = Object.keys(OBSTACLE_SHAPES),
    minRadius = 4,
    maxRadius = 12
  } = settings;

  const random = createSeededRandom(seed, 0x4f627374); // salt 'Obst'
  const cellCountX = Math.ceil(worldOptions.width / cellSize);
  const cellCountY = Math.ceil(worldOptions.height / cellSize);
  const generated = [];

  for (let index = 0; index < count; index++) {
    const x = random() * worldOptions.width;
    const y = random() * worldOptions.height;

    if (shapes.length > 0 && random() < 0.5) {
      const shape = shapes[Math.floor(random() * shapes.length) % shapes.length];
      const obstacle = normalizeObstacle({ id: `auto-shape-${index + 1}`, type: 'shape', shape, x, y }, cellSize);
      obstacle.cells = obstacle.cells.filter(cell =>
        cell.x >= 0 && cell.x < cellCountX && cell.y >= 0 && cell.y < cellCountY);
      generated.push(obstacle);
    } else {
      const radius = minRadius + random() * (maxRadius - minRadius);
      generated.push(normalizeObstacle({ id: `auto-circle-${index + 1}`, type: 'circle', x, y, radius }, cellSize));
    }
  }

  return generated;
}

function createInitialObstacles(worldOptions, seed, cellSize) {
  if (!worldOptions.obstacles) return [];

  const manual = (worldOptions.obstacleLayout ?? []).map(obstacle => normalizeObstacle(obstacle, cellSize));
  const procedural = generateProceduralObstacles(seed, worldOptions, cellSize);
  return [...manual, ...procedural];
}

function isPositionBlocked(position, obstacles, cellSize) {
  return obstacles.some(obstacle => {
    if (obstacle.type === 'shape') {
      const cellX = Math.floor(position.x / cellSize);
      const cellY = Math.floor(position.y / cellSize);
      return obstacle.cells.some(cell => cell.x === cellX && cell.y === cellY);
    }
    return Math.hypot(position.x - obstacle.x, position.y - obstacle.y) <= obstacle.radius;
  });
}

function isCellBlocked(cellX, cellY, cellSize, obstacles) {
  return obstacles.some(obstacle => {
    if (obstacle.type === 'shape')
      return obstacle.cells.some(cell => cell.x === cellX && cell.y === cellY);

    const center = { x: (cellX + 0.5) * cellSize, y: (cellY + 0.5) * cellSize };
    return Math.hypot(center.x - obstacle.x, center.y - obstacle.y) <= obstacle.radius + cellSize / 2;
  });
}

module.exports = { OBSTACLE_SHAPES, createInitialObstacles, isPositionBlocked, isCellBlocked };
