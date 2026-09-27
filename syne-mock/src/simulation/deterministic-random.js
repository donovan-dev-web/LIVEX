const MASK_64 = (1n << 64n) - 1n;

function createSeededRandom(seed) {
  const numericSeed = Number(seed);
  let state = ((numericSeed >>> 0) ^ (Math.floor(numericSeed / 0x100000000) >>> 0)) || 1;
  return () => {
    state = (Math.imul(1664525, state) + 1013904223) >>> 0;
    return state / 0x100000000;
  };
}

function createDeterministicOffset(agentId, tick, action, radius) {
  const actionIndex = {
    Idle: 0, SeekFood: 1, SeekWater: 2, Rest: 3, Flee: 4,
    Socialize: 5, Explore: 6, Eat: 7, Drink: 8
  }[action] ?? 0;
  let hash = BigInt(agentId);
  hash ^= (BigInt(tick) * 0x9E3779B97F4A7C15n) & MASK_64;
  hash ^= (BigInt(actionIndex) * 0xBF58476D1CE4E5B9n) & MASK_64;
  hash ^= hash >> 30n;
  hash = (hash * 0xBF58476D1CE4E5B9n) & MASK_64;
  hash ^= hash >> 27n;
  hash = (hash * 0x94D049BB133111EBn) & MASK_64;
  hash ^= hash >> 31n;

  const angle = Number(hash % 10000n) / 10000 * Math.PI * 2;
  const distance = Number((hash >> 17n) % 100n) / 100 * radius * 0.5;
  return { x: Math.cos(angle) * distance, y: Math.sin(angle) * distance };
}

function round(value) {
  return Math.round(value * 10000) / 10000;
}

module.exports = { createSeededRandom, createDeterministicOffset, round };
