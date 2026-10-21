// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

/**
 * Internationalisation.
 * Français = langue source (racine). Anglais = miroir sous `/en/`, même arborescence.
 * Aucune redirection automatique : le choix de langue est explicite (voir Header).
 */

export const languages = {
  fr: 'Français',
  en: 'English',
} as const;

export type Lang = keyof typeof languages;

export const defaultLang: Lang = 'fr';

/** Langues réellement publiées (sert à générer hreflang / le sélecteur). */
export const publishedLangs: Lang[] = ['fr', 'en'];

export const ui = {
  fr: {
    'site.tagline': 'living intelligent virtual ecosystem experience',
    'nav.skip': 'Aller au contenu',
    'nav.comprendre': 'Comprendre',
    'nav.architecture': 'Architecture',
    'nav.telecharger': 'Télécharger',
    'nav.journal': 'Journal',
    'nav.documentation': 'Documentation',
    'nav.apropos': 'À propos',
    'nav.theme': 'Thème sombre',
    'nav.lang': 'Langue',
    'nav.home': "LIVEX — accueil",
    'nav.main': 'Navigation principale',
    'nav.menu': 'Menu',
    'nav.follow': 'Suivre',
    'nav.breadcrumb': "Fil d'Ariane",
    'footer.license': 'Code sous licence GNU GPL v3.0 ou ultérieure',
    'footer.repo': 'Dépôt GitHub',
    'footer.nav': 'Navigation de pied de page',
    'footer.col.navigation': 'Navigation',
    'footer.col.resources': 'Ressources',
    'footer.col.project': 'Projet',
    'footer.col.legal': 'Légal',
    'footer.brand': 'Écosystème de simulation multi-agents : moteur, cognition émergente et rendu perceptif.',
    'footer.citation': 'Citer ce site (CITATION.cff)',
    'article.share': 'Partager cet article',
    'article.copy': 'Copier le lien',
    'article.copied': 'Lien copié',
    'translation.notice': 'Traduction automatique, relue — le français fait foi.',
    'translation.link': 'Lire la version française',
  },
  en: {
    'site.tagline': 'living intelligent virtual ecosystem experience',
    'nav.skip': 'Skip to content',
    'nav.comprendre': 'Understand',
    'nav.architecture': 'Architecture',
    'nav.telecharger': 'Download',
    'nav.journal': 'Journal',
    'nav.documentation': 'Documentation',
    'nav.apropos': 'About',
    'nav.theme': 'Dark theme',
    'nav.lang': 'Language',
    'nav.home': 'LIVEX — home',
    'nav.main': 'Main navigation',
    'nav.menu': 'Menu',
    'nav.follow': 'Follow',
    'nav.breadcrumb': 'Breadcrumb',
    'footer.license': 'Code under the GNU GPL v3.0 or later',
    'footer.repo': 'GitHub repository',
    'footer.nav': 'Footer navigation',
    'footer.col.navigation': 'Navigation',
    'footer.col.resources': 'Resources',
    'footer.col.project': 'Project',
    'footer.col.legal': 'Legal',
    'footer.brand': 'Multi-agent simulation ecosystem: engine, emergent cognition and perceptual renderer.',
    'footer.citation': 'Cite this site (CITATION.cff)',
    'article.share': 'Share this article',
    'article.copy': 'Copy link',
    'article.copied': 'Link copied',
    'translation.notice': 'Machine translation, reviewed — the French version is authoritative.',
    'translation.link': 'Read the French version',
  },
} as const;

export type UIKey = keyof (typeof ui)[typeof defaultLang];

export function useTranslations(lang: Lang = defaultLang) {
  return function t(key: UIKey): string {
    return ui[lang][key] ?? ui[defaultLang][key];
  };
}

/** Détermine la langue à partir de l'URL rendue (tient compte du `base`). */
export function getLangFromUrl(url: URL): Lang {
  const base = import.meta.env.BASE_URL;
  let path = url.pathname;
  if (path.startsWith(base)) path = path.slice(base.length);
  const first = path.split('/').filter(Boolean)[0];
  return first === 'en' ? 'en' : 'fr';
}

/** Chemin relatif sans le préfixe de langue ni le base (ex. « comprendre/limites/ »). */
export function stripLangFromPath(pathname: string): string {
  const base = import.meta.env.BASE_URL;
  let path = pathname;
  if (path.startsWith(base)) path = path.slice(base.length);
  path = path.replace(/^\/+/, '');
  if (path === 'en' || path.startsWith('en/')) {
    path = path.slice(2).replace(/^\/+/, '');
  }
  return path;
}
