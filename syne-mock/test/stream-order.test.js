const test = require('node:test');
const assert = require('node:assert/strict');
const { Simulation } = require('../src/server');

/**
 * Enregistre l'ordre exact des trames que le `broadcast` produit, comme le
 * ferait un client WebSocket.
 */
function record(options = {}, ticks = 4) {
  const frames = [];
  const simulation = new Simulation(options, frame => frames.push(frame));
  simulation.start(42, ticks);
  for (let i = 0; i < ticks; i += 1) simulation.step();
  simulation.stop();
  return { frames, simulation };
}

test('a tick diffuses the snapshot before its own events', () => {
  const { frames } = record({ agents: 3, maxTicks: 4 });

  const firstSnapshot = frames.findIndex(f => f.type === 'snapshot');
  assert.ok(firstSnapshot >= 0, 'aucun snapshot diffuse');

  // Tout ce qui precede le premier snapshot : ni evenement, ni resume de run.
  const before = frames.slice(0, firstSnapshot).filter(f => f.type !== 'world_initialized');
  assert.deepEqual(before.map(f => f.type), [],
    'aucun evenement ne doit preceder le premier snapshot');

  // Ordre interne d'un tick : snapshot, puis tick_summary, puis les evenements.
  const tick1 = frames.slice(firstSnapshot);
  const secondSnapshot = tick1.findIndex((f, i) => i > 0 && f.type === 'snapshot');
  const tick = secondSnapshot === -1 ? tick1 : tick1.slice(0, secondSnapshot);

  assert.equal(tick[0].type, 'snapshot', 'le snapshot ouvre le tick');
  assert.equal(tick[1].type, 'tick_summary', 'tick_summary suit immediatement le snapshot');
  for (const frame of tick.slice(2)) {
    assert.notEqual(frame.type, 'snapshot', 'un seul snapshot par tick');
    assert.equal(frame.tick, tick[0].tick, `evenement ${frame.type} desaligne sur le tick du snapshot`);
  }
  assert.ok(tick.length > 2, 'le tick doit porter des evenements');
});

test('the stream stays alignable tick by tick for a consumer', () => {
  // Reproduit l'invariant qu'ECHOS verifie dans `ingestion.stream.aligned_ticks` :
  // un snapshot, puis uniquement des evenements du meme tick, et aucun evenement
  // avant le premier snapshot. C'est cet invariant que le mock violait.
  const { frames } = record({ agents: 3, maxTicks: 5 }, 5);
  const segments = [];
  let current = null;

  for (const frame of frames) {
    if (frame.type === 'world_initialized') continue;
    if (frame.type === 'snapshot') {
      current = { tick: frame.tick, events: [] };
      segments.push(current);
      continue;
    }
    assert.ok(current, `evenement ${frame.type} recu avant le premier snapshot`);
    assert.equal(frame.tick, current.tick,
      `evenement ${frame.type} desaligne (tick ${frame.tick} != snapshot ${current.tick})`);
    current.events.push(frame);
  }

  assert.equal(segments.length, 5, 'un segment par tick');
  assert.deepEqual(segments.map(s => s.tick), [1, 2, 3, 4, 5]);
  for (const segment of segments) {
    assert.ok(segment.events.length > 0, `tick ${segment.tick} sans evenement`);
  }
});

test('the diagnostic buffer keeps the events of every tick', () => {
  // Reordonner la diffusion ne doit pas perdre d'evenement : `events` reste un
  // tampon de diagnostic borne, rempli dans le meme ordre que le flux.
  const { simulation } = record({ agents: 3, maxTicks: 4, diagnostics: { maxEvents: 10000 } });
  const ticks = new Set(simulation.events.map(e => e.tick));
  assert.deepEqual([...ticks].sort((a, b) => a - b), [1, 2, 3, 4]);

  for (const tick of ticks) {
    const ofTick = simulation.events.filter(e => e.tick === tick);
    assert.ok(ofTick.some(e => e.type === 'tick_summary'), `tick ${tick} sans tick_summary`);
    assert.ok(ofTick.some(e => e.type === 'decision_made'), `tick ${tick} sans decision_made`);
  }
});

test('an event type gated on a tick is still emitted after the snapshot', () => {
  // `world.book_written` ne part qu'au tick 100 : le test garantit que le
  // reordonnancement n'a pas dechappe aux emetteurs conditionnels.
  const { frames } = record({ agents: 2, maxTicks: 100, books: { enabled: true } }, 100);
  const book = frames.find(f => f.type === 'world.book_written');
  assert.ok(book, 'world.book_written attendu au tick 100');
  assert.equal(book.tick, 100);

  // `world.book_written` part apres les evenements par agent du tick, donc
  // l'invariant a verifier n'est pas « le snapshot juste avant » mais « aucun
  // evenement du tick 100 avant le snapshot du tick 100 ».
  const tickSnapshot = frames.filter(f => f.type === 'snapshot' && f.tick === 100);
  assert.equal(tickSnapshot.length, 1, 'un seul snapshot pour le tick 100');
  assert.ok(frames.indexOf(tickSnapshot[0]) < frames.indexOf(book),
    'le snapshot du tick 100 doit preceder world.book_written');
});
