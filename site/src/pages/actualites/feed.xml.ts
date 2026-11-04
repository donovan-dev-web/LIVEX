// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

import rss from '@astrojs/rss';
import { getCollection } from 'astro:content';
import type { APIContext } from 'astro';
import { absoluteUrl } from '../../utils/url';

export const prerender = true;

export async function GET(context: APIContext) {
  const base = import.meta.env.BASE_URL;

  const entries = (await getCollection('actualites', ({ data }) => !data.draft)).sort(
    (a, b) => b.data.date.valueOf() - a.data.date.valueOf()
  );

  return rss({
    title: 'Journal LIVEX',
    description:
      'Jalons, décisions techniques et articles de fond du projet LIVEX — datés et sourcés.',
    // Jamais l'origine seule : GitHub Pages de type projet, l'URL de base inclut /LIVEX/.
    site: context.site ? new URL(base, context.site).href : absoluteUrl(),
    trailingSlash: true,
    items: entries.map((e) => ({
      title: e.data.title,
      description: e.data.description,
      pubDate: e.data.date,
      link: `${base}actualites/${e.id}/`,
    })),
    customData: '<language>fr</language>',
  });
}
