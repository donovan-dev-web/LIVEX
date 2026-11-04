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
    versionLabel: 'SYNE 0.17.0 · moteur .NET (dépôt)',
    status: {
      id: 'experimental',
      label: 'Expérimental',
      note: "Moteur fonctionnel dans le dépôt (0.17.0) ; aucune distribution publique à ce jour (octobre 2026).",
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
    versionLabel: 'ECHOS 0.1.0 · paquet Python (dépôt)',
    status: {
      id: 'experimental',
      label: 'Expérimental',
      note: "API headless fonctionnelle dans le dépôt (paquet 0.1.0) ; non distribuée publiquement.",
    },
    href: 'technique/echos/',
    contracts: ['Schéma de stockage SQLite v6', 'Rapport de calibration par run'],
  },
  {
    key: 'prism',
    name: 'PRISM',
    acronym: 'Persistent Reality & Interactive Simulation Module',
    role: 'Monde physique (cible V2)',
    summary:
      "Cible V2 : monde voxel 2.5D autoritaire, noyau .NET headless, actions matérielles et visualiseur Silk.NET facultatif.",
    version: '2.0.0',
    versionLabel: 'PRISM 2.0.0 · version cible, en préparation',
    status: {
      id: 'envisage',
      label: 'Cible V2',
      note: 'Cette description présente le périmètre prévu de PRISM V2 ; les validations et l’état de réalisation seront actualisés avant publication.',
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
    versionLabel: 'SYNE 0.17.0 · .NET engine (repository)',
    status: {
      id: 'experimental',
      label: 'Experimental',
      note: 'Engine working in the repository (0.17.0); no public distribution to date (October 2026).',
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
    versionLabel: 'ECHOS 0.1.0 · Python package (repository)',
    status: {
      id: 'experimental',
      label: 'Experimental',
      note: 'Headless API working in the repository (package 0.1.0); not publicly distributed.',
    },
    href: 'technique/echos/',
    contracts: ['SQLite storage schema v6', 'Per-run calibration report'],
  },
  {
    key: 'prism',
    name: 'PRISM',
    acronym: 'Persistent Reality & Interactive Simulation Module',
    role: 'Physical world (V2 target)',
    summary:
      'V2 target: authoritative 2.5D voxel world, headless .NET core, material actions and an optional Silk.NET viewer.',
    version: '2.0.0',
    versionLabel: 'PRISM 2.0.0 · target version, in preparation',
    status: {
      id: 'envisage',
      label: 'V2 target',
      note: 'This description presents the planned scope of PRISM V2; implementation status and validation will be updated before publication.',
    },
    href: 'technique/prism/',
    contracts: [],
  },
];
