const TERRAIN_TYPES = ['plains', 'river', 'sea'];

// Profil de chaque type de terrain : coût de déplacement et franchissabilité.
// La mer bloque le déplacement (comme un obstacle) ; la rivière ralentit mais reste franchissable.
const TERRAIN_PROFILES = {
  plains: { movementCost: 1, walkable: true },
  river: { movementCost: 4, walkable: true },
  sea: { movementCost: Infinity, walkable: false }
};

function createSeededRandom(seed, salt) {
  let state = ((Number(seed) >>> 0) ^ (Math.floor(Number(seed) / 0x100000000) >>> 0) ^ salt) >>> 0;
  return () => {
    state = (Math.imul(state, 1664525) + 1013904223) >>> 0;
    return state / 0xffffffff;
  };
}

function distanceFromEdge(edge, x, y, cellCountX, cellCountY) {
  switch (edge) {
    case 'north': return y;
    case 'south': return cellCountY - 1 - y;
    case 'west': return x;
    case 'east': return cellCountX - 1 - x;
    default: return cellCountY - 1 - y;
  }
}

function carveRiver(grid, random, cellCountX, cellCountY, width) {
  const half = Math.floor(width / 2);
  // Point de départ aléatoire (mais reproductible) sur la moitié centrale de la carte.
  let x = Math.floor(cellCountX * (0.25 + random() * 0.5));

  for (let y = 0; y < cellCountY; y++) {
    for (let w = -half; w <= half; w++) {
      const cellX = x + w;
      if (cellX >= 0 && cellX < cellCountX && grid[y][cellX] !== 'sea') grid[y][cellX] = 'river';
    }
    // Marche aléatoire : la rivière serpente légèrement d'une ligne à l'autre.
    const step = random();
    if (step < 0.33) x -= 1;
    else if (step > 0.66) x += 1;
    x = Math.max(1, Math.min(cellCountX - 2, x));
  }
}

/**
 * Génère une grille de terrain [y][x] -> terrainType, de façon déterministe à partir du seed.
 * - Une bande de mer le long d'un bord de la carte (seaEdge / seaDepthRatio).
 * - Une ou plusieurs rivières serpentant du bord opposé jusqu'à la mer (ou jusqu'au bord opposé).
 * - Le reste de la carte est en plaine.
 */
function generateTerrainGrid(seed, cellCountX, cellCountY, terrainOptions = {}) {
  const {
    seaEdge = 'south',
    seaDepthRatio = 0.12,
    riverCount = 1,
    riverWidth = 1
  } = terrainOptions;

  const grid = Array.from({ length: cellCountY }, () => Array(cellCountX).fill('plains'));

  if (seaDepthRatio > 0) {
    const seaDepth = Math.max(1, Math.round(cellCountY * seaDepthRatio));
    for (let y = 0; y < cellCountY; y++) {
      for (let x = 0; x < cellCountX; x++) {
        if (distanceFromEdge(seaEdge, x, y, cellCountX, cellCountY) < seaDepth) grid[y][x] = 'sea';
      }
    }
  }

  const random = createSeededRandom(seed, 0x52697645); // salt 'RivE'
  for (let riverIndex = 0; riverIndex < riverCount; riverIndex++)
    carveRiver(grid, random, cellCountX, cellCountY, riverWidth);

  return grid;
}

module.exports = { TERRAIN_TYPES, TERRAIN_PROFILES, generateTerrainGrid };
