const RESOURCE_TYPES = ['food', 'water', 'wood', 'mineral'];
const LOCATIONS_PER_TYPE = 3;

function generateResourceLocations(seed, cellCountX, cellCountY, cells) {
  const cellCount = cellCountX * cellCountY;
  let state = ((Number(seed) >>> 0) ^ (Math.floor(Number(seed) / 0x100000000) >>> 0) ^ 0xa511e9b3) >>> 0;
  const nextCell = () => {
    state = (Math.imul(state, 1664525) + 1013904223) >>> 0;
    return state;
  };
  const available = cells.filter(cell => cell.walkable);
  const resources = [];

  for (const kind of RESOURCE_TYPES) {
    const used = new Set();
    const targetCount = Math.min(LOCATIONS_PER_TYPE, available.length);
    for (let index = 0; index < targetCount; index++) {
      let cellIndex = nextCell() % cellCount;
      let cell = cells[cellIndex];
      while (!cell.walkable || used.has(cellIndex)) {
        cellIndex = (cellIndex + 1) % cellCount;
        cell = cells[cellIndex];
      }
      used.add(cellIndex);
      resources.push({
        id: `resource-${kind}-${index + 1}`,
        kind,
        x: cell.x,
        y: cell.y,
        quantity: 100
      });
    }
  }
  return resources;
}

function createStocks(configuredStocks) {
  return Object.fromEntries(RESOURCE_TYPES.map(kind => [kind, Number(configuredStocks[kind] ?? 0)]));
}

function advanceStocks(stocks, regeneration) {
  for (const kind of RESOURCE_TYPES)
    stocks[kind] = Math.max(0, stocks[kind] + Number(regeneration[kind] ?? 0));
}

function consume(stocks, kind, quantity) {
  if (!(kind in stocks) || stocks[kind] < quantity) return false;
  stocks[kind] = Math.max(0, stocks[kind] - quantity);
  return true;
}

module.exports = {
  RESOURCE_TYPES,
  generateResourceLocations,
  createStocks,
  advanceStocks,
  consume
};
