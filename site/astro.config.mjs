// @ts-check
// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

import { defineConfig } from 'astro/config';
import sitemap from '@astrojs/sitemap';

/**
 * Site vitrine statique de LIVEX.
 * Publie sur GitHub Pages (branche `gh-pages`) via `.github/workflows/pages.yml`.
 * URL de projet : https://donovan-dev-web.github.io/LIVEX/
 */
export default defineConfig({
  site: 'https://donovan-dev-web.github.io',
  base: '/LIVEX/',
  trailingSlash: 'always',
  build: {
    format: 'directory',
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
    }),
  ],
  devToolbar: {
    enabled: false,
  },
});
