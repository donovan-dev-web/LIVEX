// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

/**
 * Finalise `dist/sitemap.xml` :
 *  - renomme le sitemap produit par @astrojs/sitemap (`sitemap-0.xml`) ;
 *  - ajoute un `<lastmod>` **réel** à chaque URL, pris sur la date du dernier
 *    commit du fichier source de la page (article Markdown ou page Astro).
 *
 * Un `lastmod` exact et stable est ce que Google utilise pour prioriser le
 * recrawl : sans lui, ou avec une date qui change à chaque build, il est ignoré.
 * Si l'historique Git n'est pas disponible (clone superficiel, archive), le
 * `lastmod` est omis plutôt que renseigné avec une date fausse — sauf pour les
 * articles, où la date de publication du frontmatter sert de repli.
 *
 * `fetch-depth: 0` est donc requis dans le workflow Pages (voir pages.yml).
 */

import { existsSync, readdirSync, readFileSync, writeFileSync, rmSync, copyFileSync } from 'node:fs';
import { execFileSync } from 'node:child_process';
import { join } from 'node:path';

const dist = join(process.cwd(), 'dist');
const src = join(dist, 'sitemap-0.xml');
const dst = join(dist, 'sitemap.xml');
const repoRoot = process.cwd();

if (!existsSync(src)) {
  console.error('fix-sitemap: sitemap-0.xml introuvable dans dist/');
  process.exit(1);
}

copyFileSync(src, dst);
console.log('fix-sitemap: sitemap-0.xml -> sitemap.xml');

/** Même base que `astro.config.mjs` : /LIVEX/ par défaut (GitHub Pages projet). */
const base = (
  process.env.BASE_PATH || new URL(process.env.SITE_URL || 'https://donovan-dev-web.github.io/LIVEX/').pathname
).replace(/\/?$/, '/');

let gitAvailable = true;

/** Date (YYYY-MM-DD) du dernier commit touchant `file`, ou null. */
function gitDate(file) {
  if (!gitAvailable) return null;
  try {
    const out = execFileSync('git', ['log', '-1', '--format=%cs', '--', file], {
      cwd: repoRoot,
      encoding: 'utf8',
      stdio: ['ignore', 'pipe', 'ignore'],
    }).trim();
    return /^\d{4}-\d{2}-\d{2}$/.test(out) ? out : null;
  } catch {
    gitAvailable = false;
    console.warn('fix-sitemap: historique Git indisponible, lastmod omis quand il n’y a pas de repli');
    return null;
  }
}

/** Date de publication du frontmatter (`date:`) d’un article, ou null. */
function frontmatterDate(file) {
  try {
    const head = readFileSync(file, 'utf8').slice(0, 2000);
    const m = head.match(/^date:\s*['"]?(\d{4}-\d{2}-\d{2})/m);
    return m ? m[1] : null;
  } catch {
    return null;
  }
}

/**
 * Date du dernier article publié : les pages qui listent les articles (accueil,
 * index du blog) changent à chaque publication, même quand leur propre fichier
 * n'est pas modifié.
 */
function latestArticleDate() {
  try {
    const dir = 'src/content/actualites';
    const dates = readdirSync(dir)
      .filter((f) => f.endsWith('.md') || f.endsWith('.mdx'))
      .map((f) => {
        const head = readFileSync(join(dir, f), 'utf8').slice(0, 2000);
        if (/^draft:\s*true/m.test(head)) return null;
        return head.match(/^date:\s*['"]?(\d{4}-\d{2}-\d{2})/m)?.[1] ?? null;
      })
      .filter(Boolean)
      .sort();
    return dates.at(-1) ?? null;
  } catch {
    return null;
  }
}
const newestArticle = latestArticleDate();

/** Pages dont le rendu dépend de la dernière publication. */
const articleIndexPages = new Set(['src/pages/index.astro', 'src/pages/actualites/index.astro']);

const bySrc = new Map();
/** Fichier source correspondant à une URL, ou null. */
function sourceFor(loc) {
  if (bySrc.has(loc)) return bySrc.get(loc);

  let found = null;
  try {
    const pathname = new URL(loc).pathname;
    if (pathname.startsWith(base)) {
      let rel = pathname.slice(base.length).replace(/\/$/, '');
      let dir = 'src/pages/';
      if (rel === 'en' || rel.startsWith('en/')) {
        rel = rel.slice(2).replace(/^\/?/, '');
        dir = 'src/pages/en/';
      }

      if (rel === '') {
        found = `${dir}index.astro`;
      } else if (rel === 'llms.txt') {
        found = 'public/llms.txt';
      } else {
        const candidates = [`${dir}${rel}.astro`, `${dir}${rel}/index.astro`];
        found = candidates.find((c) => existsSync(c)) ?? null;

        if (!found && rel.startsWith('actualites/')) {
          const slug = rel.slice('actualites/'.length);
          for (const ext of ['.md', '.mdx']) {
            if (existsSync(`src/content/actualites/${slug}${ext}`)) {
              found = `src/content/actualites/${slug}${ext}`;
              break;
            }
          }
        }
      }
    }
  } catch {
    found = null;
  }

  bySrc.set(loc, found);
  return found;
}

const xml = readFileSync(dst, 'utf8');
let withDate = 0;
let withoutDate = 0;

const out = xml.replace(/<url>[\s\S]*?<\/url>/g, (block) => {
  if (block.includes('<lastmod>')) return block;

  const loc = block.match(/<loc>([^<]+)<\/loc>/)?.[1];
  const file = loc ? sourceFor(decodeURI(loc)) : null;
  if (!file) {
    withoutDate += 1;
    return block;
  }

  const published = file.includes('src/content/actualites/') ? frontmatterDate(file) : null;
  const committed = gitDate(file);
  // Pour un article, la date de publication est un plancher honnête ; les pages
  // qui listent les articles suivent, elles, la dernière publication.
  const lastmod =
    [committed, published, articleIndexPages.has(file) ? newestArticle : null].filter(Boolean).sort().pop() ?? null;

  if (!lastmod) {
    withoutDate += 1;
    return block;
  }

  withDate += 1;
  return block.replace('</loc>', `</loc><lastmod>${lastmod}</lastmod>`);
});

writeFileSync(dst, out);
console.log(`fix-sitemap: lastmod ${withDate} URL(s), ${withoutDate} sans date`);

for (const f of ['sitemap-0.xml', 'sitemap-index.xml']) {
  const p = join(dist, f);
  if (existsSync(p)) {
    rmSync(p);
    console.log(`fix-sitemap: supprimé ${f}`);
  }
}
