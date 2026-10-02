#!/usr/bin/env node
const fs = require('node:fs');
const { createServer } = require('./server');

function parseArgs(args) {
  let configPath = null;
  const ports = {};
  const valueOptions = new Set([
    '--control-port', '--data-port', '--instance-id', '--work-dir', '--log-dir', '--correlation-id'
  ]);
  for (let i = 0; i < args.length; i++) {
    const argument = args[i];
    if (argument === '--headless') continue;
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
  return { configPath, ports };
}

function readConfig(configPath) {
  return configPath && fs.existsSync(configPath)
    ? JSON.parse(fs.readFileSync(configPath, 'utf8'))
    : {};
}

async function run(args = process.argv.slice(2)) {
  const { configPath, ports } = parseArgs(args);
  const config = { ...readConfig(configPath), ...ports };
  const server = createServer(config);
  await server.listen(config.controlPort || 5181, config.dataPort || 5180);
  console.log(`SYNE mock HTTP :${config.controlPort || 5181}, WebSocket :${config.dataPort || 5180}`);
  config.controlPort && console.log(`Control API: http://localhost:${config.controlPort}/`);
  config.dataPort && console.log(`Data API: ws://localhost:${config.dataPort}/`);

  let shutdown;
  const close = () => shutdown ??= server.close();
  process.once('SIGINT', close);
  process.once('SIGTERM', close);
  return server;
}

if (require.main === module) {
  run().catch(error => {
    console.error(error.stack || error.message);
    process.exitCode = 1;
  });
}

module.exports = { parseArgs, readConfig, run };
