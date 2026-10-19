// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

const fs = require('fs');
const path = require('path');
const YAML = require('yaml');

const dir = path.join(__dirname, 'src', 'content', 'actualites');
const files = [
  'livex-naissance-d-une-question.md',
  'reproductibilite-et-determinisme-au-coeur-de-livex.md',
  'syne-moteur-decisionnel-deterministe.md',
  'echos-observation-analyse-et-calibration.md',
];

let ok = true;
for (const f of files) {
  const raw = fs.readFileSync(path.join(dir, f), 'utf8');
  const m = raw.match(/^---\r?\n([\s\S]*?)\r?\n---\r?\n?/);
  if (!m) { console.log(`FAIL ${f}: no frontmatter`); ok = false; continue; }
  let doc;
  try { doc = YAML.parse(m[1]); } catch (e) { console.log(`FAIL ${f}: YAML error ${e.message}`); ok = false; continue; }
  const desc = doc.description || '';
  const tags = doc.tags || [];
  const checks = [
    ['title', typeof doc.title === 'string' && doc.title.length > 0],
    ['description <160', typeof desc === 'string' && desc.length > 0 && desc.length < 160],
    ['date', (doc.date instanceof Date || typeof doc.date === 'string') && !isNaN(new Date(doc.date))],
    ['draft boolean', typeof doc.draft === 'boolean'],
    ['tags 3-5', Array.isArray(tags) && tags.length >= 3 && tags.length <= 5],
    ['tags lowercase', Array.isArray(tags) && tags.every((t) => t === t.toLowerCase())],
    ['body length', raw.length - m[0].length > 3000],
  ];
  const bad = checks.filter(([, v]) => !v);
  if (bad.length) { ok = false; console.log(`FAIL ${f}: ${bad.map((c) => c[0]).join(', ')}`); }
  else {
    console.log(`OK   ${f} | desc=${desc.length} chars | tags=${tags.join(',')} | date=${new Date(doc.date).toISOString().slice(0, 10)} | words=${(raw.slice(m[0].length).match(/\S+/g) || []).length}`);
  }
}
console.log(ok ? 'ALL VALID' : 'VALIDATION FAILED');
process.exit(ok ? 0 : 1);
