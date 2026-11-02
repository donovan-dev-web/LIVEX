// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

/**
 * Métadonnées du site et navigation principale (source unique).
 */
import type { Lang } from '../i18n/ui';

export const SITE = {
  name: 'LIVEX',
  title: 'LIVEX — Living Intelligent Virtual Ecosystem eXperience',
  description:
    "LIVEX est un écosystème de simulation multi-agents : un moteur (SYNE), une cognition émergente (ECHOS) et un rendu perceptif (PRISM). Documentation et principes ouverts.",
  descriptionEn:
    'LIVEX is a multi-agent simulation ecosystem: an engine (SYNE), an emergent-cognition system (ECHOS) and a perceptual renderer (PRISM). Open documentation and principles.',
  shortDescription:
    "Écosystème de simulation multi-agents : moteur, cognition émergente et rendu perceptif.",
  repo: 'https://github.com/donovan-dev-web/LIVEX',
  license: 'GPL-3.0-or-later',
  author: 'Donovan',
  lang: 'fr',
  favicon: 'favicon.svg',
} as const;

export interface NavItem {
  key: string;
  /** Chemin relatif à la racine, sans préfixe de langue. */
  href: string;
  label: Record<Lang, string>;
  /** Sous-entrées éventuelles (menu déroulant). */
  children?: NavItem[];
}

export const NAV: NavItem[] = [
  {
    key: 'comprendre',
    href: 'comprendre/',
    label: { fr: 'Comprendre', en: 'Understand' },
    children: [
      { key: 'comprendre-overview', href: 'comprendre/', label: { fr: 'Vue d’ensemble', en: 'Overview' } },
      { key: 'limites', href: 'comprendre/limites/', label: { fr: 'Limites et portée', en: 'Limits and scope' } },
      { key: 'emergence', href: 'comprendre/emergence/', label: { fr: "L'émergence expliquée", en: 'Emergence explained' } },
      { key: 'glossaire', href: 'comprendre/glossaire/', label: { fr: 'Glossaire', en: 'Glossary' } },
      { key: 'faq', href: 'faq/', label: { fr: 'FAQ', en: 'FAQ' } },
    ],
  },
  {
    key: 'architecture',
    href: 'technique/',
    label: { fr: 'Architecture', en: 'Architecture' },
    children: [
      { key: 'architecture-overview', href: 'technique/', label: { fr: 'Vue d’ensemble', en: 'Overview' } },
      { key: 'syne', href: 'technique/syne/', label: { fr: 'SYNE', en: 'SYNE' } },
      { key: 'echos', href: 'technique/echos/', label: { fr: 'ECHOS', en: 'ECHOS' } },
      { key: 'prism', href: 'technique/prism/', label: { fr: 'PRISM', en: 'PRISM' } },
      { key: 'launcher', href: 'technique/launcher/', label: { fr: 'Launcher', en: 'Launcher' } },
      { key: 'resultats', href: 'technique/resultats/', label: { fr: 'Reproductibilité et mesures', en: 'Reproducibility and measures' } },
    ],
  },
  {
    key: 'telecharger',
    href: 'telecharger/',
    label: { fr: 'Télécharger', en: 'Download' },
    children: [
      { key: 'telecharger-overview', href: 'telecharger/', label: { fr: "Vue d'ensemble", en: 'Overview' } },
      { key: 'getting-started', href: 'getting-started/', label: { fr: 'Démarrer en 5 minutes', en: 'Get started in 5 minutes' } },
      { key: 'installation', href: 'telecharger/installation/', label: { fr: 'Installation et prérequis', en: 'Installation and requirements' } },
      { key: 'versions', href: 'versions/', label: { fr: 'Versions', en: 'Versions' } },
    ],
  },
  {
    key: 'ressources',
    href: 'documentation/',
    label: { fr: 'Ressources', en: 'Resources' },
    children: [
      { key: 'documentation', href: 'documentation/', label: { fr: 'Documentation', en: 'Documentation' } },
      { key: 'etat-du-projet', href: 'etat-du-projet/', label: { fr: 'État du projet', en: 'Project status' } },
      { key: 'monographie', href: 'monographie/', label: { fr: 'Monographie', en: 'Monograph' } },
      { key: 'journal', href: 'actualites/', label: { fr: 'Journal', en: 'Journal' } },
      { key: 'plan-du-site', href: 'plan-du-site/', label: { fr: 'Plan du site', en: 'Sitemap' } },
    ],
  },
  {
    key: 'apropos',
    href: 'a-propos/',
    label: { fr: 'À propos', en: 'About' },
    children: [
      { key: 'apropos-overview', href: 'a-propos/', label: { fr: 'Vue d’ensemble', en: 'Overview' } },
      { key: 'auteur', href: 'a-propos/auteur/', label: { fr: 'Auteur', en: 'Author' } },
      { key: 'transparence-ia', href: 'a-propos/transparence-ia/', label: { fr: "Transparence sur l'usage de l'IA", en: 'Transparency on the use of AI' } },
      { key: 'participer', href: 'participer/', label: { fr: 'Participer', en: 'Contribute' } },
      { key: 'presse', href: 'presse/', label: { fr: 'Kit média', en: 'Press kit' } },
    ],
  },
];

export const FOOTER_NAV: NavItem[] = [
  { key: 'comprendre', href: 'comprendre/', label: { fr: 'Comprendre', en: 'Understand' } },
  { key: 'technique', href: 'technique/', label: { fr: 'Architecture', en: 'Architecture' } },
  { key: 'telecharger', href: 'telecharger/', label: { fr: 'Télécharger', en: 'Download' } },
  { key: 'versions', href: 'versions/', label: { fr: 'Versions', en: 'Versions' } },
  { key: 'getting-started', href: 'getting-started/', label: { fr: 'Démarrer en 5 min', en: 'Get started in 5 min' } },
  { key: 'etat-du-projet', href: 'etat-du-projet/', label: { fr: 'État du projet', en: 'Project state' } },
  { key: 'monographie', href: 'monographie/', label: { fr: 'Monographie', en: 'Monograph' } },
  { key: 'feuille-de-route', href: 'feuille-de-route/', label: { fr: 'Feuille de route', en: 'Roadmap' } },
  { key: 'journal', href: 'actualites/', label: { fr: 'Journal', en: 'Journal' } },
  { key: 'suivre', href: 'suivre/', label: { fr: 'Suivre', en: 'Follow' } },
  { key: 'documentation', href: 'documentation/', label: { fr: 'Documentation', en: 'Documentation' } },
  { key: 'faq', href: 'faq/', label: { fr: 'FAQ', en: 'FAQ' } },
  { key: 'glossaire', href: 'comprendre/glossaire/', label: { fr: 'Glossaire', en: 'Glossary' } },
  { key: 'participer', href: 'participer/', label: { fr: 'Participer', en: 'Contribute' } },
  { key: 'apropos', href: 'a-propos/', label: { fr: 'À propos', en: 'About' } },
  { key: 'auteur', href: 'a-propos/auteur/', label: { fr: 'Auteur', en: 'Author' } },
  { key: 'transparence-ia', href: 'a-propos/transparence-ia/', label: { fr: 'Transparence IA', en: 'AI transparency' } },
  { key: 'presse', href: 'presse/', label: { fr: 'Kit média', en: 'Press kit' } },
  { key: 'mentions', href: 'mentions-legales/', label: { fr: 'Mentions légales', en: 'Legal notice' } },
  { key: 'confidentialite', href: 'confidentialite/', label: { fr: 'Confidentialité', en: 'Privacy' } },
  { key: 'accessibilite', href: 'accessibilite/', label: { fr: 'Accessibilité', en: 'Accessibility' } },
  { key: 'licence', href: 'licence/', label: { fr: 'Licence', en: 'License' } },
  { key: 'plan-du-site', href: 'plan-du-site/', label: { fr: 'Plan du site', en: 'Sitemap' } },
];
