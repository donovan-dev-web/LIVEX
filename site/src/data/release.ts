// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

/**
 * État du projet et des versions — données factuelles et datées.
 * Source : docs/governance/LIVEX-STATUS.md, VERSIONING.md, CHANGELOG.md.
 */

export const RELEASE = {
  /** Une version publique est-elle téléchargeable ? */
  published: true,
  /** Version publique courante. */
  planned: '1.0.0',
  plannedLabel: 'LIVEX 1.0.0 — publiée',
  releasesUrl: 'https://github.com/donovan-dev-web/LIVEX/releases',
  releaseUrl: 'https://github.com/donovan-dev-web/LIVEX/releases/tag/livex-v1.0.0',
  changelogUrl: 'https://github.com/donovan-dev-web/LIVEX/blob/main/CHANGELOG.md',
  latest: '1.0.0' as string | null,
  latestDate: '2026-10-31' as string | null,
} as const;

export const PROJECT_STATE = {
  observedDate: '31 octobre 2026',
  branch: 'main · tag livex-v1.0.0',
  commit: '196ad077',
  filesScanned: 1152,
  loc: {
    total: 111608,
    source: 68678,
    tests: 31173,
  },
  tests: {
    passed: 1550,
    ignored: 16,
    failed: 0,
    documentedTotal: 1566,
    prismSpecs: 76,
    installerCases: 118,
    byComponent: [
      { name: 'SYNE', value: 695 },
      { name: 'ECHOS (Python)', value: 397 },
      { name: 'Launcher', value: 227 },
      { name: 'syne-mock', value: 53 },
      { name: 'PRISM / PrismLdk', value: 76 },
      { name: 'Installateur', value: 118 },
    ],
  },
  coverage: {
    echos: '91,32 %',
    syne: '81,20 %',
    syneCore: '91,17 %',
    syneConsole: '63,46 %',
  },
  ci: '13/13 jobs verts, 19/10/2026',
  windows: 'Support Windows validé localement depuis le 08/10/2026',
} as const;

/** Composants visibles sur la page Versions (alignés sur le dépôt). */
export const VERSION_TABLE = [
  { component: 'SYNE', version: '0.17.0', note: 'Moteur de simulation .NET' },
  { component: 'ECHOS', version: '0.1.0', note: 'Paquet Python (observation, API headless)' },
  { component: 'PRISM', version: '1.0.0', note: 'Projet Unreal packagé pour Windows et Linux' },
  { component: 'PRISM-LDK', version: '1.0.0', note: 'Plugin Unreal PrismLdk pour intégrer les contrats SYNE' },
  { component: 'Launcher', version: '0.1.0', note: 'Application de bureau Avalonia' },
  { component: 'Installateur LIVEX', version: '1.0.0', note: 'Assistant de distribution Windows et Linux' },
] as const;

/** Miroir anglais — chaînes localisées uniquement (les chiffres restent dans PROJECT_STATE). */
export const RELEASE_EN = {
  plannedLabel: 'LIVEX 1.0.0 — released',
  releaseLabel: 'Public release',
} as const;

export const PROJECT_STATE_EN = {
  observedDate: '31 October 2026',
  coverage: {
    echos: '91.32%',
    syne: '81.20%',
    syneCore: '91.17%',
    syneConsole: '63.46%',
  },
  ci: 'CI status is reported by GitHub Actions',
  windows: 'Windows and Linux packages are available in LIVEX 1.0.0',
} as const;

export const VERSION_TABLE_EN = [
  { component: 'SYNE', version: '0.17.0', note: '.NET simulation engine' },
  { component: 'ECHOS', version: '0.1.0', note: 'Python package (observation, headless API)' },
  { component: 'PRISM', version: '1.0.0', note: 'Unreal project packaged for Windows and Linux' },
  { component: 'PRISM-LDK', version: '1.0.0', note: 'PrismLdk Unreal plugin for SYNE contract integration' },
  { component: 'Launcher', version: '0.1.0', note: 'Avalonia desktop application' },
  { component: 'LIVEX Installer', version: '1.0.0', note: 'Windows and Linux distribution assistant' },
] as const;
