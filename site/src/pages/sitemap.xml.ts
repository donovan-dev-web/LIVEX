// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

import type { APIContext } from 'astro';

export const prerender = true;

/**
 * Point d'entrée canonique `/sitemap.xml` : redirige vers le sitemap généré
 * par `@astrojs/sitemap` (`/sitemap-index.xml`), que certains outils et moteurs
 * attendent sous ce nom. La redirection est permanente.
 */
export function GET(context: APIContext) {
  const base = import.meta.env.BASE_URL;
  const target = new URL(`${base}sitemap-index.xml`, context.site ?? 'https://donovan-dev-web.github.io').href;
  return Response.redirect(target, 301);
}