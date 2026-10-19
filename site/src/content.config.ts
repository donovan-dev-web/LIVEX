// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

import { defineCollection } from 'astro:content';
import { z } from 'astro/zod';
import { glob } from 'astro/loaders';

/**
 * Actualités / journal de développement.
 * Les articles sont P1 ; le flux RSS et l'index (état vide) sont P0 (S18).
 */
const actualites = defineCollection({
  loader: glob({ pattern: '**/*.md', base: './src/content/actualites' }),
  schema: z.object({
    title: z.string(),
    description: z.string(),
    date: z.coerce.date(),
    draft: z.boolean().default(false),
    tags: z.array(z.string()).default([]),
  }),
});

export const collections = { actualites };
