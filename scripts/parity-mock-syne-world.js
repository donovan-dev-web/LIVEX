#!/usr/bin/env node
/**
 * Projet : LIVEX (PRISM/SYNE-MOCK)
 * Auteur : CHARTRAIN Donovan
 * Rôle : Comparer la WorldDescription du mock SYNE et celle du moteur SYNE reel,
 *        champ par champ, sur le profil `prism` tel qu'il est livre : c'est le
 *        monde que PRISM recoit, donc l'objet a verifier.
 *
 * Code de sortie 1 des qu'une divergence est trouvee.
 *
 * Motivation : les deux sources parlaient le meme contrat sur le papier, mais le
 * mock publiait des cases de terrain que le moteur ne sait pas produire
 * (`sea`/`river`, `movementCost: null` issu d'un `Infinity`) et des obstacles
 * proceduraux, tandis que le profil `prism` du moteur ne declarait aucun
 * obstacle : PRISM affichait un monde impossible d'un cote et vide de l'autre.
 * Ce script compare les deux sorties reelles au lieu de comparer des documents.
 *
 * Usage :
 *   node scripts/parity_mock_syne_world.js [--syne <chemin exe>] [--seed <entier>]
 *
 * Le moteur est lance en supervise avec `--simulation prism` (sans surcouche :
 * les valeurs viennent de `SimulationProfiles.Prism()`), le mock est instancie
 * avec `syne-mock/config_prism.json`. Les deux jeux d'obstacles doivent etre
 * identiques : c'est ce que ce script verifie champ par champ.
 */

const { spawn } = require('node:child_process');
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');

const root = path.join(__dirname, '..');
const WebSocket = require(path.join(root, 'syne-mock', 'node_modules', 'ws'));
const { Simulation } = require(path.join(root, 'syne-mock', 'src', 'server'));
const { readConfig } = require(path.join(root, 'syne-mock', 'src', 'cli'));

const PRISM_CONFIG = path.join(root, 'syne-mock', 'config_prism.json');

const CELL_FIELDS = ['x', 'y', 'terrainType', 'walkable', 'height', 'movementCost', 'obstacles'];
const RESOURCE_FIELDS = ['id', 'kind', 'x', 'y', 'quantity'];

function parseArgs(argv) {
  const options = { syneExe: null, seed: 42 };
  for (let i = 0; i < argv.length; i++) {
    if (argv[i] === '--syne') options.syneExe = argv[++i];
    // `--seed` permet de rejouer les graines ou les deux sources ont deja diverge :
    // le placement des ressources depend de la seed, comparer la seule graine par
    // defaut laisserait passer une regle de tirage fausse 43 % du temps.
    else if (argv[i] === '--seed') options.seed = Number(argv[++i]);
  }
  if (!Number.isSafeInteger(options.seed) || options.seed < 0)
    throw new Error('--seed attend un entier positif ou nul');
  return options;
}

/** Lance SYNE en supervise (profil prism) et rend la trame `world_initialized`. */
function captureEngineWorld(syneExe, seed) {
  const workDir = path.join(os.tmpdir(), 'livex-parity-world');
  fs.rmSync(workDir, { recursive: true, force: true });
  fs.mkdirSync(workDir, { recursive: true });

  const observePort = 5390;
  const correlationId = 'parity-mock-syne';
  const child = spawn(syneExe, [
    '--headless',
    '--instance-id', 'syne-parity',
    '--correlation-id', correlationId,
    '--control-port', '5391',
    '--observe-port', String(observePort),
    '--work-dir', workDir,
    '--log-dir', workDir,
    '--simulation', 'prism',
    '--seed', String(seed),
    '--ticks', '2',
    '--autostart'
  ], {
    cwd: root,
    env: { ...process.env, LIVEX_SESSION_TOKEN: 'parity-token', LIVEX_CORRELATION_ID: correlationId }
  });

  const engineLog = [];
  child.stdout.on('data', d => engineLog.push(d.toString()));
  child.stderr.on('data', d => engineLog.push(d.toString()));

  return new Promise((resolve, reject) => {
    let attempts = 0;
    const fail = error => {
      child.kill();
      reject(new Error(`${error}\n--- sortie SYNE ---\n${engineLog.join('')}`));
    };
    const connect = () => {
      attempts += 1;
      const socket = new WebSocket(`ws://127.0.0.1:${observePort}/`);
      socket.on('message', raw => {
        const message = JSON.parse(raw.toString());
        if (message.type !== 'world_initialized') return;
        socket.close();
        child.kill();
        resolve(message);
      });
      socket.on('error', error => {
        if (attempts < 40) setTimeout(connect, 500);
        else fail(`connexion au port d'observation impossible: ${error.message}`);
      });
    };
    setTimeout(connect, 500);
    setTimeout(() => fail("le moteur n'a pas annonce de monde en 60 s"), 60000);
  });
}

/** Le monde du mock, avec la configuration reellement livree pour PRISM. */
function mockWorld(seed) {
  const config = readConfig(PRISM_CONFIG);
  const simulation = new Simulation(config);
  simulation.prepare(seed, config.ticksPerSecond);
  return simulation.worldDescription();
}

function compare(engineWorld, mockWorld_) {
  const divergences = [];
  const note = message => divergences.push(message);

  // 1. Jeu de cles de la description.
  const engineKeys = Object.keys(engineWorld).sort();
  const mockKeys = Object.keys(mockWorld_).sort();
  for (const key of engineKeys) if (!mockKeys.includes(key)) note(`cle absente du mock : ${key}`);
  for (const key of mockKeys) if (!engineKeys.includes(key)) note(`cle en trop dans le mock : ${key}`);

  // 2. Entete.
  for (const field of ['width', 'height', 'cellSize', 'ticksPerSecond', 'simulatedSecondsPerTick', 'metersPerUnit', 'cellCountX', 'cellCountY', 'version']) {
    if (engineWorld[field] !== mockWorld_[field])
      note(`${field} : moteur=${JSON.stringify(engineWorld[field])} mock=${JSON.stringify(mockWorld_[field])}`);
  }

  // 3. Cellules, une par une (meme ordre attendu : y puis x).
  if (engineWorld.cells.length !== mockWorld_.cells.length) {
    note(`nombre de cellules : moteur=${engineWorld.cells.length} mock=${mockWorld_.cells.length}`);
  } else {
    const signatures = new Map();
    for (const cell of mockWorld_.cells) signatures.set(`${cell.x},${cell.y}`, cell);
    let mismatches = 0;
    for (const cell of engineWorld.cells) {
      const match = signatures.get(`${cell.x},${cell.y}`);
      if (!match) { note(`case (${cell.x},${cell.y}) absente du mock`); continue; }
      for (const field of CELL_FIELDS) {
        const a = JSON.stringify(cell[field]);
        const b = JSON.stringify(match[field]);
        if (a === b) continue;
        if (mismatches < 10) note(`case (${cell.x},${cell.y}).${field} : moteur=${a} mock=${b}`);
        mismatches += 1;
      }
    }
    if (mismatches > 10) note(`... et ${mismatches - 10} autre(s) difference(s) de cellule`);
  }

  // 4. Obstacles : geometrie publiee, dans le meme ordre.
  if (JSON.stringify(engineWorld.obstacles) !== JSON.stringify(mockWorld_.obstacles))
    note(`obstacles : moteur=${JSON.stringify(engineWorld.obstacles)} mock=${JSON.stringify(mockWorld_.obstacles)}`);

  // 4 bis. Marqueurs de ressources : ils dependent de la seed (LCG local), donc
  //        ils doivent etre compares comme le reste — c'est le seul champ de la
  //        description ou les deux sources piochaient avec des regles differentes
  //        (le moteur tirait sans regarder la marchabilite, le mock avancait
  //        jusqu'a une case libre), divergence invisible tant que le tirage
  //        tombait sur une case libre par chance.
  if (engineWorld.resources.length !== mockWorld_.resources.length) {
    note(`nombre de marqueurs de ressources : moteur=${engineWorld.resources.length} mock=${mockWorld_.resources.length}`);
  } else {
    let mismatches = 0;
    for (let i = 0; i < engineWorld.resources.length; i++) {
      const a = engineWorld.resources[i];
      const b = mockWorld_.resources[i];
      for (const field of RESOURCE_FIELDS) {
        if (JSON.stringify(a[field]) === JSON.stringify(b[field])) continue;
        if (mismatches < 10)
          note(`ressource ${a.id}.${field} : moteur=${JSON.stringify(a[field])} mock=${JSON.stringify(b[field])}`);
        mismatches += 1;
      }
    }
    if (mismatches > 10) note(`... et ${mismatches - 10} autre(s) difference(s) de marqueur`);
  }

  // 5. Aucun champ non serialisable dans le JSON du mock (l'ancien `Infinity`
  //    de la mer devenait `null` et passait inapercu).
  const serialized = JSON.stringify(mockWorld_);
  if (/null/.test(serialized) && !/null/.test(JSON.stringify(engineWorld)))
    note('le monde du mock contient une valeur null que le moteur ne produit pas');

  return divergences;
}

async function main() {
  const options = parseArgs(process.argv.slice(2));
  const syneExe = options.syneExe
    ?? path.join(root, 'syne', 'Simulation.Console', 'bin', 'Release', 'net10.0', 'Simulation.Console.exe');
  if (!fs.existsSync(syneExe))
    throw new Error(`binaire SYNE introuvable : ${syneExe} (dotnet build syne/Simulation.Console -c Release)`);

  const config = readConfig(PRISM_CONFIG);
  console.log(`Profil prism livre — moteur : SimulationProfiles.Prism() | mock : ${path.relative(root, PRISM_CONFIG)} | seed ${options.seed}`);
  console.log(`Monde ${config.world.width}x${config.world.height}, cellule ${config.world.cellSize}, ${config.world.obstacleLayout.length} obstacle(s) declares par le mock`);

  const engine = await captureEngineWorld(syneExe, options.seed);
  const mocks = mockWorld(options.seed);
  const divergences = compare(engine.world, mocks);

  const blocked = world => world.cells.filter(cell => !cell.walkable).length;
  console.log(`moteur : ${engine.world.cells.length} cases dont ${blocked(engine.world)} bloquee(s), ${engine.world.obstacles.length} obstacle(s), version ${engine.version}`);
  console.log(`mock   : ${mocks.cells.length} cases dont ${blocked(mocks)} bloquee(s), ${mocks.obstacles.length} obstacle(s), version ${mocks.version}`);

  // Sans case bloquee, comparer deux mondes vides ne prouverait rien : le profil
  // doit declarer des obstacles des deux cotes.
  if (blocked(engine.world) === 0 || engine.world.obstacles.length === 0) {
    console.error("ECHEC : le moteur n'a publie aucun obstacle — le profil prism ne les declare pas.");
    return 1;
  }
  console.log('Périmètre comparé : jeu de clés, en-tête, cells[] champ par champ, obstacles[], resources[] champ par champ.');
  console.log('Non comparé (approximation assumée du mock) : contenu de agents[] — le mock place ses agents avec son propre tirage, pas avec le Xoshiro du moteur.');

  if (divergences.length === 0) {
    console.log('OK : les deux descriptions de monde sont identiques champ par champ.');
    return 0;
  }
  console.log(`\n${divergences.length} divergence(s) :`);
  for (const divergence of divergences) console.log(`  - ${divergence}`);
  return 1;
}

main()
  .then(code => { process.exitCode = code; })
  .catch(error => { console.error(error.message); process.exitCode = 1; });
