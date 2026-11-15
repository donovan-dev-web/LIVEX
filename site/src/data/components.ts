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
    version: '0.18.0',
    versionLabel: 'SYNE 0.18.0 · moteur .NET (dépôt)',
    status: {
      id: 'experimental',
      label: 'Expérimental',
      note: "Moteur fonctionnel dans le dépôt (0.18.0, relevé du 15 novembre 2026) : cognition couplée à PRISM V2, aucune distribution publique à ce jour.",
    },
    href: 'technique/syne/',
    contracts: ['Contrat d’observabilité 0.6.0', 'run_id « run-<seed>-<12hex> »', 'Snapshot schemaVersion 5'],
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
      note: "API headless fonctionnelle dans le dépôt (paquet 0.1.0, relevé du 15 novembre 2026) : ingestion du flux PRISM en production, non distribuée publiquement.",
    },
    href: 'technique/echos/',
    contracts: ['Schéma de stockage SQLite v9', 'POST /ingest/prism', 'Rapport de calibration par run'],
  },
  {
    key: 'prism',
    name: 'PRISM',
    acronym: 'Perceptual Rendering & Interactive Simulation Module',
    role: 'Monde physique (V2)',
    summary:
      "Monde voxel 2.5D autoritaire, hôte .NET headless, actions matérielles et viewer Silk.NET facultatif : le socle P0–P11 est livré, viewer compris.",
    version: '2.0.0-preview.1',
    versionLabel: 'PRISM 2.0.0-preview.1 · hôte .NET headless',
    status: {
      id: 'developpement',
      label: 'En développement',
      note: 'Socle P0–P11 livré au 15 novembre 2026 : hôte headless, contrat 1.0.0, viewer et plan de contrôle ; la performance de l’hôte (G1) reste ouverte.',
    },
    href: 'technique/prism/',
    contracts: ['Contrat SYNE↔PRISM 1.0.0', 'Flux de lecture /data/read', 'Contrôle Play/Pause/Stop/Quit'],
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
    version: '0.18.0',
    versionLabel: 'SYNE 0.18.0 · .NET engine (repository)',
    status: {
      id: 'experimental',
      label: 'Experimental',
      note: 'Engine working in the repository (0.18.0, read on 15 November 2026): cognition coupled to PRISM V2, no public distribution to date.',
    },
    href: 'technique/syne/',
    contracts: ['Observability contract 0.6.0', 'run_id “run-<seed>-<12hex>”', 'snapshot schemaVersion 5'],
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
      note: 'Headless API working in the repository (package 0.1.0, read on 15 November 2026): PRISM stream ingestion in production, not publicly distributed.',
    },
    href: 'technique/echos/',
    contracts: ['SQLite storage schema v9', 'POST /ingest/prism', 'Per-run calibration report'],
  },
  {
    key: 'prism',
    name: 'PRISM',
    acronym: 'Perceptual Rendering & Interactive Simulation Module',
    role: 'Physical world (V2)',
    summary:
      'Authoritative 2.5D voxel world, headless .NET host, material actions and an optional Silk.NET viewer: the P0–P11 foundation is delivered, viewer included.',
    version: '2.0.0-preview.1',
    versionLabel: 'PRISM 2.0.0-preview.1 · headless .NET host',
    status: {
      id: 'developpement',
      label: 'In development',
      note: 'P0–P11 foundation delivered on 15 November 2026: headless host, 1.0.0 contract, viewer and control plan; host performance (G1) remains open.',
    },
    href: 'technique/prism/',
    contracts: ['SYNE↔PRISM 1.0.0 contract', 'Read stream /data/read', 'Play/Pause/Stop/Quit control'],
  },
];
