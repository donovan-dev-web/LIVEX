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
  license: 'MIT',
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
      { key: 'limites', href: 'comprendre/limites/', label: { fr: 'Limites et portée', en: 'Limits and scope' } },
      { key: 'emergence', href: 'comprendre/emergence/', label: { fr: "L'émergence expliquée", en: 'Emergence explained' } },
      { key: 'glossaire', href: 'comprendre/glossaire/', label: { fr: 'Glossaire', en: 'Glossary' } },
    ],
  },
  {
    key: 'architecture',
    href: 'technique/',
    label: { fr: 'Architecture', en: 'Architecture' },
    children: [
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
      { key: 'installation', href: 'telecharger/installation/', label: { fr: 'Installation et prérequis', en: 'Installation and requirements' } },
    ],
  },
  { key: 'journal', href: 'actualites/', label: { fr: 'Journal', en: 'Journal' } },
  { key: 'documentation', href: 'documentation/', label: { fr: 'Documentation', en: 'Documentation' } },
  {
    key: 'apropos',
    href: 'a-propos/',
    label: { fr: 'À propos', en: 'About' },
    children: [
      { key: 'transparence-ia', href: 'a-propos/transparence-ia/', label: { fr: "Transparence sur l'usage de l'IA", en: 'Transparency on the use of AI' } },
    ],
  },
];

export const FOOTER_NAV: NavItem[] = [
  { key: 'comprendre', href: 'comprendre/', label: { fr: 'Comprendre', en: 'Understand' } },
  { key: 'technique', href: 'technique/', label: { fr: 'Architecture', en: 'Architecture' } },
  { key: 'telecharger', href: 'telecharger/', label: { fr: 'Télécharger', en: 'Download' } },
  { key: 'versions', href: 'versions/', label: { fr: 'Versions', en: 'Versions' } },
  { key: 'feuille-de-route', href: 'feuille-de-route/', label: { fr: 'Feuille de route', en: 'Roadmap' } },
  { key: 'journal', href: 'actualites/', label: { fr: 'Journal', en: 'Journal' } },
  { key: 'suivre', href: 'suivre/', label: { fr: 'Suivre', en: 'Follow' } },
  { key: 'documentation', href: 'documentation/', label: { fr: 'Documentation', en: 'Documentation' } },
  { key: 'faq', href: 'faq/', label: { fr: 'FAQ', en: 'FAQ' } },
  { key: 'glossaire', href: 'comprendre/glossaire/', label: { fr: 'Glossaire', en: 'Glossary' } },
  { key: 'participer', href: 'participer/', label: { fr: 'Participer', en: 'Contribute' } },
  { key: 'apropos', href: 'a-propos/', label: { fr: 'À propos', en: 'About' } },
  { key: 'transparence-ia', href: 'a-propos/transparence-ia/', label: { fr: 'Transparence IA', en: 'AI transparency' } },
  { key: 'presse', href: 'presse/', label: { fr: 'Kit média', en: 'Press kit' } },
  { key: 'mentions', href: 'mentions-legales/', label: { fr: 'Mentions légales', en: 'Legal notice' } },
  { key: 'confidentialite', href: 'confidentialite/', label: { fr: 'Confidentialité', en: 'Privacy' } },
  { key: 'accessibilite', href: 'accessibilite/', label: { fr: 'Accessibilité', en: 'Accessibility' } },
  { key: 'licence', href: 'licence/', label: { fr: 'Licence', en: 'License' } },
  { key: 'plan-du-site', href: 'plan-du-site/', label: { fr: 'Plan du site', en: 'Sitemap' } },
];
