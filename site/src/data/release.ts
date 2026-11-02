// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

/**
 * État du projet et des versions — données factuelles et datées.
 * Source : docs/governance/LIVEX-STATUS.md, VERSIONING.md, CHANGELOG.md.
 *
 * Aucune release publique n'existe à ce jour : la page Télécharger doit
 * rester en état neutre (voir site/src/data/release.ts, RELEASE.published).
 */

export const RELEASE = {
  /** Une version publique est-elle téléchargeable ? (aucun tag posé à ce jour) */
  published: false,
  /** Version cible en préparation. */
  planned: 'V0.2',
  plannedLabel: 'V0.2 — en préparation',
  releasesUrl: 'https://github.com/donovan-dev-web/LIVEX/releases',
  changelogUrl: 'https://github.com/donovan-dev-web/LIVEX/blob/main/CHANGELOG.md',
  /** Dernière version publiée (null tant qu'aucune release n'est posée). */
  latest: null as string | null,
  latestDate: null as string | null,
} as const;

export const PROJECT_STATE = {
  observedDate: '19 octobre 2026',
  branch: 'feature/livex-realignement-divergences-v0.2',
  commit: 'a229c6a',
  trackedFiles: 640,
  loc: {
    total: 63231,
    source: 38904,
    tests: 24327,
  },
  tests: {
    passed: 1356,
    ignored: 16,
    failed: 0,
    byComponent: [
      { name: 'SYNE', value: 695 },
      { name: 'ECHOS (Python)', value: 397 },
      { name: 'Launcher', value: 227 },
      { name: 'syne-mock', value: 53 },
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
  { component: 'PRISM', version: '0.x', note: 'Projet Unreal de représentation stylisée par défaut, construit sur PRISM-LDK (hors périmètre V0.2)' },
  { component: 'PRISM-LDK', version: '0.1.0', note: 'Cœur de la représentation 3D : plugin Unreal (LIVEX Development Kit) traduisant les données SYNE' },
  { component: 'Launcher', version: '0.1.0', note: 'Application de bureau Avalonia' },
] as const;

/** Miroir anglais — chaînes localisées uniquement (les chiffres restent dans PROJECT_STATE). */
export const RELEASE_EN = {
  plannedLabel: 'V0.2 — in preparation',
} as const;

export const PROJECT_STATE_EN = {
  observedDate: '19 October 2026',
  coverage: {
    echos: '91.32%',
    syne: '81.20%',
    syneCore: '91.17%',
    syneConsole: '63.46%',
  },
  ci: '13/13 jobs green, 19 Oct 2026',
  windows: 'Windows support validated locally since 8 Oct 2026',
} as const;

export const VERSION_TABLE_EN = [
  { component: 'SYNE', version: '0.17.0', note: '.NET simulation engine' },
  { component: 'ECHOS', version: '0.1.0', note: 'Python package (observation, headless API)' },
  { component: 'PRISM', version: '0.x', note: 'Default stylised Unreal representation project, built on PRISM-LDK (out of scope for V0.2)' },
  { component: 'PRISM-LDK', version: '0.1.0', note: 'Core of 3D representation: Unreal plugin (LIVEX Development Kit) translating SYNE data' },
  { component: 'Launcher', version: '0.1.0', note: 'Avalonia desktop application' },
] as const;
