// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

/**
 * Construit une URL tenant compte du `base` Astro (/LIVEX/ sur GitHub Pages)
 * et de la langue (préfixe `en/` pour la version anglaise).
 * Gère les ancres et les URL absolues externes sans les modifier.
 */
import type { Lang } from '../i18n/ui';

export function url(path = '', lang: Lang = 'fr'): string {
  if (/^(?:[a-z]+:)?\/\//i.test(path) || path.startsWith('#')) {
    return path;
  }
  const base = import.meta.env.BASE_URL; // déjà terminé par « / »
  const clean = path.replace(/^\/+/, '');
  const prefix = lang === 'en' ? 'en/' : '';
  return `${base}${prefix}${clean}`;
}

/** URL absolue canonique, utile pour og:url, JSON-LD, flux RSS. */
export function absoluteUrl(path = '', lang: Lang = 'fr'): string {
  const site = import.meta.env.SITE;
  return new URL(url(path, lang), site).href;
}
