const fs = require('node:fs');
const { createServer } = require('./server');
const file = process.argv[2];
const config = file && fs.existsSync(file) ? JSON.parse(fs.readFileSync(file, 'utf8')) : {};
const server = createServer(config);
server.listen(config.controlPort || 5181, config.dataPort || 5180).then(() => {
  console.log(`SYNE mock HTTP :${config.controlPort || 5181}, WebSocket :${config.dataPort || 5180}`);
  config.controlPort && console.log(`Control API: http://localhost:${config.controlPort}/`);
  config.dataPort && console.log(`Data API: ws://localhost:${config.dataPort}/`);
});
