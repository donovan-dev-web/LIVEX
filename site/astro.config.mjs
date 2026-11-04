// @ts-check
// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

import { defineConfig } from 'astro/config';
import sitemap from '@astrojs/sitemap';
import { satteri } from '@astrojs/markdown-satteri';

/**
 * Site vitrine statique de LIVEX.
 * Publie sur GitHub Pages (branche `gh-pages`) via `.github/workflows/pages.yml`.
 *
 * GitHub Pages de type « projet » : l'URL de base du site est toujours
 * https://donovan-dev-web.github.io/LIVEX/ — jamais l'origine seule.
 * Astro n'accepte que l'origine dans `site` ; le préfixe de projet va dans `base`.
 * Les deux sont dérivés de cette URL unique pour qu'aucun chemin ne puisse être perdu.
 */
const publicUrl = new URL(process.env.SITE_URL || 'https://donovan-dev-web.github.io/LIVEX/');
const site = publicUrl.origin;
const base = process.env.BASE_PATH || publicUrl.pathname.replace(/\/?$/, '/');

/**
 * Les tableaux Markdown ne portent pas d'attributs : on ajoute `scope="col"`
 * aux en-têtes de colonne pour les lecteurs d'écran.
 */
function satteriTableScope() {
  return {
    name: 'livex-table-scope',
    element: {
      filter: ['th'],
      /** @param {any} node */
      visit(node) {
        return { ...node, properties: { ...node.properties, scope: 'col' } };
      },
    },
  };
}

export default defineConfig({
  site,
  base,
  trailingSlash: 'always',
  build: {
    format: 'directory',
  },
  markdown: {
    processor: satteri({ hastPlugins: [satteriTableScope] }),
  },
  i18n: {
    defaultLocale: 'fr',
    locales: ['fr', 'en'],
    routing: {
      prefixDefaultLocale: false,
    },
  },
  integrations: [
    sitemap({
      i18n: {
        defaultLocale: 'fr',
        locales: {
          fr: 'fr',
          en: 'en',
        },
      },
      customPages: [new URL('llms.txt', publicUrl).href],
    }),
  ],
  devToolbar: {
    enabled: false,
  },
});
