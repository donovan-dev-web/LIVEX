// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

import { readdirSync, statSync, existsSync } from 'node:fs';
import { join } from 'node:path';

const dist = 'dist';
const base = '/LIVEX/';

function walk(dir) {
  const out = [];
  for (const name of readdirSync(dir)) {
    const p = join(dir, name);
    if (statSync(p).isDirectory()) out.push(...walk(p));
    else if (name.endsWith('.html')) out.push(p);
  }
  return out;
}

const files = walk(dist);
const hrefRe = /href="([^"]+)"/g;
const missing = new Set();

for (const file of files) {
  const html = (await import('node:fs')).readFileSync(file, 'utf8');
  let m;
  while ((m = hrefRe.exec(html)) !== null) {
    const href = m[1];
    if (!href.startsWith(base) && !href.startsWith('/')) continue;
    if (href.startsWith('http') || href.startsWith('mailto:') || href.startsWith('#')) continue;
    const path = href.split('#')[0].split('?')[0];
    if (!path.startsWith(base)) continue;
    const rel = path.slice(base.length).replace(/^\/+/, '');
    const target = rel === '' ? join(dist, 'index.html') : join(dist, rel, rel.endsWith('.html') ? '' : 'index.html');
    // handle direct file links (sitemap.xml etc.)
    const direct = join(dist, rel);
    if (!existsSync(target) && !existsSync(direct)) {
      missing.add(path);
    }
  }
}

if (missing.size) {
  console.error('Liens internes manquants :');
  for (const m of [...missing].sort()) console.error(' - ' + m);
  process.exit(1);
} else {
  console.log('Tous les liens internes résolus (' + files.length + ' pages analysées).');
}