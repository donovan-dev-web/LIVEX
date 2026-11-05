// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

/**
 * État du projet et des versions — données factuelles et datées.
 * Source : docs/governance/LIVEX-STATUS.md, VERSIONING.md, CHANGELOG.md.
 *
 * V1.0.0 est la release publique ; V2.0.0 est la cible en préparation.
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
  latestDate: null as string | null,
} as const;

export const PROJECT_STATE = {
  observedDate: '4 novembre 2026',
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
    total: 1566,
    passed: 1550,
    ignored: 16,
    failed: 0,
    byComponent: [
      { name: 'SYNE', value: 695 },
      { name: 'ECHOS (Python)', value: 397 },
      { name: 'Launcher', value: 227 },
      { name: 'syne-mock', value: 53 },
      { name: 'PRISM / PRISM-LDK', value: 76 },
      { name: 'Installateur', value: 118 },
    ],
  },
  coverage: {
    echos: '91,32 %',
    syne: '81,20 %',
    syneCore: '91,17 %',
    syneConsole: '63,46 %',
  },
  ci: '13 jobs verts sur 15 (2 ignorés), 04/11/2026',
  windows: 'Support Windows validé localement depuis le 08/10/2026',
} as const;

/** Composants visibles sur la page Versions (alignés sur le dépôt). */
export const VERSION_TABLE = [
  { component: 'SYNE', version: '0.17.0', note: 'Moteur de simulation .NET' },
  { component: 'ECHOS', version: '0.1.0', note: 'Paquet Python (observation, API headless)' },
  { component: 'PRISM', version: '2.0.0', note: 'Cible V2 en préparation : monde physique autoritaire .NET, viewer facultatif' },
  { component: 'Launcher', version: '0.1.0', note: 'Application de bureau Avalonia' },
] as const;

/** Miroir anglais — chaînes localisées uniquement (les chiffres restent dans PROJECT_STATE). */
export const RELEASE_EN = {
  plannedLabel: 'V2.0.0 — in preparation',
} as const;

export const PROJECT_STATE_EN = {
  observedDate: '4 November 2026',
  coverage: {
    echos: '91.32%',
    syne: '81.20%',
    syneCore: '91.17%',
    syneConsole: '63.46%',
  },
  ci: '13 of 15 jobs green (2 skipped), 4 Nov 2026',
  windows: 'Windows support validated locally since 8 Oct 2026',
} as const;

export const VERSION_TABLE_EN = [
  { component: 'SYNE', version: '0.17.0', note: '.NET simulation engine' },
  { component: 'ECHOS', version: '0.1.0', note: 'Python package (observation, headless API)' },
  { component: 'PRISM', version: '2.0.0', note: 'V2 target in preparation: authoritative .NET physical world, optional viewer' },
  { component: 'Launcher', version: '0.1.0', note: 'Avalonia desktop application' },
] as const;
