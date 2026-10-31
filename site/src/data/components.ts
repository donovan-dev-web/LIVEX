// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

/**
 * Les trois composants d'architecture de LIVEX.
 * Acronymes issus de README.md / GLOSSARY.md (autorité).
 * Statuts : disponible · expérimental · en développement · envisagé · hors périmètre.
 */

export type StatusId = 'disponible' | 'experimental' | 'developpement' | 'envisage' | 'hors-perimetre';

export interface ComponentStatus {
  id: StatusId;
  label: string;
  /** Justification datée, reprise telle quelle dans le site. */
  note: string;
}

export interface Component {
  key: 'syne' | 'echos' | 'prism';
  name: string;
  acronym: string;
  role: string;
  /** Résumé affiché sur la page d'accueil. */
  summary: string;
  version: string;
  versionLabel: string;
  status: ComponentStatus;
  href: string;
  /** Contrats d'interface publics, quand ils existent. */
  contracts: string[];
}

export const COMPONENTS: Component[] = [
  {
    key: 'syne',
    name: 'SYNE',
    acronym: 'Systems & Emergent Network Engine',
    role: 'Moteur de simulation',
    summary:
      "Moteur déterministe de simulation multi-agents : règles, monde, ticks et rejouabilité bit-à-bit à partir d'une graine.",
    version: '0.17.0',
    versionLabel: 'SYNE 0.17.0 · moteur .NET',
    status: {
      id: 'experimental',
      label: 'Expérimental',
      note: 'Moteur inclus dans la distribution LIVEX 1.0.0 ; résultats détaillés dans les rapports datés.',
    },
    href: 'technique/syne/',
    contracts: ['Contrat de snapshot 0.5.0', 'run_id « run-<seed>-<12hex> »', 'Schéma de persistance v5'],
  },
  {
    key: 'echos',
    name: 'ECHOS',
    acronym: 'Emergent Cognition & Holistic Observation System',
    role: 'Observation et analyse',
    summary:
      "Ingestion, analyse et API headless : lecture des runs, métriques de viabilité, détection de boucles et de motifs émergents.",
    version: '0.1.0',
    versionLabel: 'ECHOS 0.1.0 · paquet Python',
    status: {
      id: 'experimental',
      label: 'Expérimental',
      note: "Paquet d'observation inclus dans la distribution LIVEX 1.0.0 ; API headless.",
    },
    href: 'technique/echos/',
    contracts: ['Schéma de stockage SQLite v6', 'Rapport de calibration par run'],
  },
  {
    key: 'prism',
    name: 'PRISM',
    acronym: 'Perceptual Rendering & Interactive Simulation Module',
    role: 'Rendu perceptif',
    summary:
      "Rendu perceptif et module de simulation interactive : projet Unreal et plugin d'intégration PRISM-LDK.",
    version: '1.0.0',
    versionLabel: 'PRISM 1.0.0 · Windows/Linux',
    status: {
      id: 'disponible',
      label: 'Disponible',
      note: 'Projet PRISM packagé pour Windows/Linux ; plugin PRISM-LDK PrismLdk 1.0.0.',
    },
    href: 'technique/prism/',
    contracts: [],
  },
];

export const STATUS_ORDER: StatusId[] = [
  'disponible',
  'experimental',
  'developpement',
  'envisage',
  'hors-perimetre',
];

/** Miroir anglais (mêmes identifiants, mêmes liens) — traduction relue. */
export const COMPONENTS_EN: Component[] = [
  {
    key: 'syne',
    name: 'SYNE',
    acronym: 'Systems & Emergent Network Engine',
    role: 'Simulation engine',
    summary:
      'Deterministic multi-agent simulation engine: rules, world, ticks and bit-for-bit replayability from a seed.',
    version: '0.17.0',
    versionLabel: 'SYNE 0.17.0 · .NET engine',
    status: {
      id: 'experimental',
      label: 'Experimental',
      note: 'Engine included in the LIVEX 1.0.0 distribution; results are detailed in dated reports.',
    },
    href: 'technique/syne/',
    contracts: ['Snapshot contract 0.5.0', 'run_id “run-<seed>-<12hex>”', 'Persistence schema v5'],
  },
  {
    key: 'echos',
    name: 'ECHOS',
    acronym: 'Emergent Cognition & Holistic Observation System',
    role: 'Observation and analysis',
    summary:
      'Ingestion, analysis and headless API: reading runs, viability metrics, detection of loops and emergent patterns.',
    version: '0.1.0',
    versionLabel: 'ECHOS 0.1.0 · Python package',
    status: {
      id: 'experimental',
      label: 'Experimental',
      note: 'Observation package included in the LIVEX 1.0.0 distribution; headless API.',
    },
    href: 'technique/echos/',
    contracts: ['SQLite storage schema v6', 'Per-run calibration report'],
  },
  {
    key: 'prism',
    name: 'PRISM',
    acronym: 'Perceptual Rendering & Interactive Simulation Module',
    role: 'Perceptual rendering',
    summary:
      'Perceptual rendering and interactive simulation module: Unreal project and PRISM-LDK integration plugin.',
    version: '1.0.0',
    versionLabel: 'PRISM 1.0.0 · Windows/Linux',
    status: {
      id: 'disponible',
      label: 'Available',
      note: 'PRISM project packaged for Windows/Linux; PRISM-LDK PrismLdk plugin 1.0.0.',
    },
    href: 'technique/prism/',
    contracts: [],
  },
];
