// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

import { copyFileSync, rmSync, existsSync } from 'node:fs';
import { join } from 'node:path';

const dist = join(process.cwd(), 'dist');
const src = join(dist, 'sitemap-0.xml');
const dst = join(dist, 'sitemap.xml');

if (!existsSync(src)) {
  console.error('fix-sitemap: sitemap-0.xml introuvable dans dist/');
  process.exit(1);
}

copyFileSync(src, dst);
console.log('fix-sitemap: sitemap-0.xml -> sitemap.xml');

for (const f of ['sitemap-0.xml', 'sitemap-index.xml']) {
  const p = join(dist, f);
  if (existsSync(p)) {
    rmSync(p);
    console.log(`fix-sitemap: supprimé ${f}`);
  }
}
