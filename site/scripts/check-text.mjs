// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

/**
 * Audit de texte du site — s'exécute sur le build (`dist/`).
 *
 * Vérifie quatre familles de défauts qui se sont déjà/glissent :
 *  1. entités HTML non décodées (`&amp;eacute;`) : Astro ne décode pas les
 *     entités dans les attributs de composants, le visiteur lit la chaîne
 *     brute (« D&eacute;terminisme ») ;
 *  2. mojibake (double encodage UTF-8) ;
 *  3. chaînes françaises résiduelles dans les pages anglaises, et phrases
 *     anglaises dans les pages françaises ;
 *  4. composants acceptant `lang` mais sans `lang="en"` dans les pages EN.
 *
 * Usage : `npm run build && npm run check:text`
 */

import { readdirSync, readFileSync, statSync } from 'node:fs';
import { join, relative, sep } from 'node:path';

const DIST = 'dist';
const SRC = 'src/pages/en';
const errors = [];

function walk(dir, out = []) {
  let entries;
  try {
    entries = readdirSync(dir);
  } catch {
    return out;
  }
  for (const entry of entries) {
    const full = join(dir, entry);
    if (statSync(full).isDirectory()) walk(full, out);
    else out.push(full);
  }
  return out;
}

/** Texte visible d'une page (scripts et styles retirés). */
function visibleText(html) {
  return html
    .replace(/<script[\s\S]*?<\/script>/gi, ' ')
    .replace(/<style[\s\S]*?<\/style>/gi, ' ')
    .replace(/<[^>]+>/g, ' ')
    .replace(/&nbsp;/g, ' ')
    .replace(/&amp;/g, '&')
    .replace(/&#39;/g, "'")
    .replace(/&lt;/g, '<')
    .replace(/&gt;/g, '>')
    .replace(/\s+/g, ' ');
}

const ENGLISH = /\b(the|and|with|that|this|from|into|which|have|has|was|were|will|can|not|its|are|for|but|all|any|each|more|than|then|when|where|what|your|you|our|their|they|them|been|being|does)\b/gi;
const FRENCH = /\b(le|la|les|de|des|du|un|une|et|est|sont|dans|pour|avec|sur|aux|ce|cette|qui|que|nous|vous|leur|leurs|ne|pas|plus|son|ses|par|au|ou|si|comme|tout|tous)\b/gi;

/** Marques françaises attendues uniquement côté français. */
const FR_ONLY = [
  '>Accueil<',
  'aria-label="Fil d\'Ariane"',
  '>Détails sur ',
  'jobs verts',
  'validé localement',
  '19 octobre',
  '91,32 %',
  '81,20 %',
];

/** Composants dont la prop `lang` conditionne le texte rendu. */
const LANG_COMPONENTS = ['PageHeader', 'CardLink', 'CtaBand', 'StatusBadge', 'HeroFigure', 'TableOfContents'];

const pages = walk(DIST).filter((f) => f.endsWith('.html'));
if (pages.length === 0) {
  console.error('Aucun HTML trouvé dans dist/ — lancer `npm run build` avant cet audit.');
  process.exit(1);
}

for (const file of pages) {
  const rel = relative(DIST, file).split(sep).join('/');
  const html = readFileSync(file, 'utf8');
  const isEnglish = rel.startsWith('en/');

  for (const match of html.matchAll(/&amp;(#\d+|#x[0-9a-fA-F]+|[a-zA-Z][a-zA-Z0-9]{1,9});/g)) {
    errors.push(`${rel} : entité non décodée « ${match[0]} » — écrire le caractère en UTF-8 direct.`);
  }

  for (const match of visibleText(html).matchAll(/Ã[\u0080-\u00BF]|Â[\u0080-\u00BF]|�/g)) {
    errors.push(`${rel} : séquence mojibake « ${match[0]} » — fichier mal encodé ?`);
  }

  if (isEnglish) {
    for (const marker of FR_ONLY) {
      if (html.includes(marker)) {
        errors.push(`${rel} : chaîne française résiduelle « ${marker} » — utiliser le miroir *_EN / lang="en".`);
      }
    }
  } else if (rel !== '404.html') {
    for (const sentence of visibleText(html).split(/(?<=[.!?])\s+/)) {
      if (sentence.length < 40) continue;
      const en = (sentence.match(ENGLISH) || []).length;
      const fr = (sentence.match(FRENCH) || []).length;
      if (en >= 3 && fr < 2) {
        errors.push(`${rel} : phrase en anglais dans une page française — « ${sentence.slice(0, 90)} »`);
      }
    }
  }
}

for (const file of walk(SRC).filter((f) => f.endsWith('.astro'))) {
  const rel = relative(SRC, file).split(sep).join('/');
  const source = readFileSync(file, 'utf8');
  for (const component of LANG_COMPONENTS) {
    let index = -1;
    while ((index = source.indexOf(`<${component}`, index + 1)) !== -1) {
      const end = source.indexOf('/>', index);
      if (end === -1) continue;
      const tag = source.slice(index, end);
      if (!/lang="en"/.test(tag)) {
        errors.push(`en/${rel} : <${component}> sans lang="en" — fil d'Ariane et libellés d'accessibilité en français.`);
      }
    }
  }
}

if (errors.length > 0) {
  console.error(`Audit de texte : ${errors.length} problème(s) :`);
  for (const error of errors) console.error(`  - ${error}`);
  process.exit(1);
}

console.log(`Audit de texte : ${pages.length} pages vérifiées, aucun problème.`);
