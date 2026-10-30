// @ts-check
// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

import { defineConfig } from 'astro/config';
import sitemap from '@astrojs/sitemap';
import { satteri } from '@astrojs/markdown-satteri';

/**
 * Site vitrine statique de LIVEX.
 * Publie sur GitHub Pages (branche `gh-pages`) via `.github/workflows/pages.yml`.
 * URL de projet : https://donovan-dev-web.github.io/LIVEX/
 */
const site = process.env.SITE_URL || 'https://donovan-dev-web.github.io';
const base = process.env.BASE_PATH || '/LIVEX/';

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
      customPages: [
        'https://donovan-dev-web.github.io/LIVEX/llms.txt',
      ],
    }),
  ],
  devToolbar: {
    enabled: false,
  },
});
