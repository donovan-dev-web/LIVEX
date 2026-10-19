#!/usr/bin/env node
// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

/**
 * Projet : LIVEX (SYNE-MOCK)
 * Auteur : CHARTRAIN Donovan
 * Rôle : Interface en ligne de commande du mock SYNE (arguments, lancement du serveur).
 */

const fs = require('node:fs');
const path = require('node:path');
const { createServer } = require('./server');

function parseArgs(args) {
  let configPath = null;
  let tickLog;
  const ports = {};
  const valueOptions = new Set([
    '--control-port',
    '--data-port',
    '--instance-id',
    '--work-dir',
    '--log-dir',
    '--correlation-id',
  ]);
  for (let i = 0; i < args.length; i++) {
    const argument = args[i];
    if (argument === '--headless') continue;
    if (argument === '--tick-log') {
      tickLog = true;
      continue;
    }
    if (argument === '--no-tick-log') {
      tickLog = false;
      continue;
    }
    if (argument === '--config' || valueOptions.has(argument)) {
      const value = args[++i];
      if (!value || value.startsWith('--')) throw new Error(`Missing value for ${argument}`);
      if (argument === '--config') {
        if (configPath) throw new Error('Only one config file may be provided');
        configPath = value;
      } else if (argument === '--control-port' || argument === '--data-port') {
        const port = Number(value);
        if (!Number.isInteger(port) || port < 1 || port > 65535)
          throw new Error(`${argument} must be an integer between 1 and 65535`);
        ports[argument === '--control-port' ? 'controlPort' : 'dataPort'] = port;
      }
      continue;
    }
    if (!argument.startsWith('--') && !configPath) {
      configPath = argument;
      continue;
    }
    throw new Error(`Unknown argument: ${argument}`);
  }
  return { configPath, ports, tickLog };
}

/**
 * Fichier voisin du chemin demandé, quand celui-ci n'existe pas : le nom réel
 * n'est comparé qu'aux noms du même dossier, séparateurs (`_`, `.`, `-`) et casse
 * ignorés. `config.prism.json` au lieu de `config_prism.json` est l'erreur
 * réellement commise par un opérateur, et proposer le nom attendu vaut mieux
 * qu'un « introuvable » que l'on croit avoir déjà corrigé.
 */
function nearMissConfigPath(resolved) {
  const directory = path.dirname(resolved);
  const key = (entry) =>
    path
      .basename(entry)
      .toLowerCase()
      .replace(/[^a-z0-9]/g, '');
  const wanted = key(resolved);
  let entries;
  try {
    entries = fs.readdirSync(directory);
  } catch {
    return null;
  }
  const matches = entries.filter((entry) => key(entry) === wanted);
  return matches.length === 1 ? path.join(directory, matches[0]) : null;
}

function readConfig(configPath) {
  if (!configPath) return {};
  const resolved = path.resolve(configPath);
  // Un fichier demandé et introuvable est une erreur, pas un retour aux défauts :
  // le repli silencieux lançait le mock sur 500x500/50 agents alors que
  // l'opérateur croyait charger le profil `prism`, et le monde publié ne
  // correspondait plus à rien (ni à sa configuration, ni au moteur).
  if (!fs.existsSync(resolved)) {
    const suggestion = nearMissConfigPath(resolved);
    throw new Error(
      `Fichier de configuration introuvable : ${configPath} (résolu : ${resolved}). ` +
        (suggestion ? `Vouliez-vous dire : ${suggestion} ?` : 'Corrigez le chemin ou retirez-le.') +
        ' Les défauts ne sont pas appliqués à la place.'
    );
  }
  try {
    return JSON.parse(fs.readFileSync(resolved, 'utf8'));
  } catch (error) {
    throw new Error(`Fichier de configuration illisible (${resolved}) : ${error.message}`);
  }
}

async function run(args = process.argv.slice(2)) {
  const { configPath, ports, tickLog } = parseArgs(args);
  const config = { ...readConfig(configPath), ...ports };
  if (tickLog !== undefined) config.diagnostics = { ...(config.diagnostics || {}), tickLog };
  const server = createServer(config);
  await server.listen(config.controlPort || 5181, config.dataPort || 5180);
  console.log(
    `SYNE mock HTTP :${config.controlPort || 5181}, WebSocket :${config.dataPort || 5180}`
  );
  config.controlPort && console.log(`Control API: http://localhost:${config.controlPort}/`);
  config.dataPort && console.log(`Data API: ws://localhost:${config.dataPort}/`);

  let shutdown;
  const close = () => (shutdown ??= server.close());
  process.once('SIGINT', close);
  process.once('SIGTERM', close);
  return server;
}

if (require.main === module) {
  run().catch((error) => {
    console.error(error.stack || error.message);
    process.exitCode = 1;
  });
}

module.exports = { parseArgs, readConfig, run };
