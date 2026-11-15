// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

/**
 * État du projet et des versions — données factuelles et datées.
 * Sources : docs/governance/LIVEX-CURRENT-STATUS.md (relevé du 15 novembre 2026),
 * docs/governance/LIVEX-STATUS.md (release V1), VERSIONING.md, CHANGELOG.md.
 *
 * V1.0.0 est la release publique ; V2.0.0 est la cible en préparation.
 * Les mesures de dépôt (fichiers, lignes) gardent leur propre date : elles ne sont
 * pas rejouées à chaque relevé de tests.
 */

export const RELEASE = {
  /** Une version publique est-elle téléchargeable ? */
  published: true,
  /** Version cible en préparation. */
  planned: 'V2.0.0',
  plannedLabel: 'V2.0.0 — en préparation',
  releasesUrl: 'https://github.com/donovan-dev-web/LIVEX/releases',
  changelogUrl: 'https://github.com/donovan-dev-web/LIVEX/blob/main/CHANGELOG.md',
  /** Dernière version publiée. */
  latest: '1.0.0' as string | null,
  latestDate: '30 octobre 2026' as string | null,
} as const;

export const PROJECT_STATE = {
  /** Date du relevé des suites de tests et des portes. */
  observedDate: '15 novembre 2026',
  /** Date du relevé Tokei (fichiers et lignes) : il n'est pas rejoué à chaque passe. */
  repoObservedDate: '4 novembre 2026',
  branch: 'main',
  commit: 'cd5b9d2',
  trackedFiles: 1421,
  loc: {
    files: 1335,
    total: 221717,
    code: 133820,
    comments: 54456,
    blank: 33441,
  },
  tests: {
    total: 2184,
    passed: 2164,
    ignored: 16,
    failed: 0,
    /** Cas Launcher conditionnels à LIVEX_SYNE_PUBLISHED_ROOT, rapportés en échec par VSTest. */
    dynamic: 4,
    byComponent: [
      { name: 'PRISM V2', value: 525 },
      { name: 'SYNE', value: 813 },
      { name: 'ECHOS (Python)', value: 419 },
      { name: 'Launcher', value: 236 },
      { name: 'Installateur', value: 120 },
      { name: 'syne-mock', value: 51 },
    ],
  },
  coverage: {
    echos: '91,14 %',
    echosDate: '15 novembre 2026',
    syne: '81,20 %',
    syneDate: '19 octobre 2026',
    syneCore: '91,17 %',
    syneConsole: '63,46 %',
  },
  ci: '13/15 jobs verts au dernier relevé (04/11/2026, 2 ignorés)',
  windows: 'Support Windows validé localement depuis le 08/10/2026',
} as const;

/** Composants visibles sur la page Versions (alignés sur le dépôt). */
export const VERSION_TABLE = [
  { component: 'SYNE', version: '0.18.0', note: 'Cognition multi-agents .NET ; plus de monde dans Simulation.Core' },
  { component: 'ECHOS', version: '0.1.0', note: 'Paquet Python : ingestion du flux PRISM, analyse, API headless' },
  { component: 'PRISM', version: '2.0.0-preview.1', note: 'Hôte .NET headless, contrat 1.0.0 et viewer facultatif livrés (P0–P11)' },
  { component: 'Launcher', version: '0.1.0', note: 'Application de bureau Avalonia ; modes Hôte et Viewer' },
] as const;

/** Miroir anglais — chaînes localisées uniquement (les chiffres restent dans PROJECT_STATE). */
export const RELEASE_EN = {
  plannedLabel: 'V2.0.0 — in preparation',
} as const;

export const PROJECT_STATE_EN = {
  observedDate: '15 November 2026',
  repoObservedDate: '4 November 2026',
  coverage: {
    echos: '91.14%',
    echosDate: '15 November 2026',
    syne: '81.20%',
    syneDate: '19 October 2026',
    syneCore: '91.17%',
    syneConsole: '63.46%',
  },
  ci: '13/15 jobs green at the last reading (4 Nov 2026, 2 skipped)',
  windows: 'Windows support validated locally since 8 Oct 2026',
} as const;

export const VERSION_TABLE_EN = [
  { component: 'SYNE', version: '0.18.0', note: '.NET multi-agent cognition; no world left in Simulation.Core' },
  { component: 'ECHOS', version: '0.1.0', note: 'Python package: PRISM stream ingestion, analysis, headless API' },
  { component: 'PRISM', version: '2.0.0-preview.1', note: 'Headless .NET host, 1.0.0 contract and optional viewer delivered (P0–P11)' },
  { component: 'Launcher', version: '0.1.0', note: 'Avalonia desktop application; Host and Viewer modes' },
] as const;
