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
  planned: 'V0.1',
  plannedLabel: 'V0.1 — en préparation',
  releasesUrl: 'https://github.com/donovan-dev-web/LIVEX/releases',
  changelogUrl: 'https://github.com/donovan-dev-web/LIVEX/blob/main/CHANGELOG.md',
  /** Dernière version publiée (null tant qu'aucune release n'est posée). */
  latest: null as string | null,
  latestDate: null as string | null,
} as const;

export const PROJECT_STATE = {
  observedDate: '12 octobre 2026',
  branch: 'develop',
  commit: '6c12421',
  trackedFiles: 640,
  loc: {
    total: 63231,
    source: 38904,
    tests: 24327,
  },
  tests: {
    passed: 1258,
    ignored: 16,
    failed: 0,
    byComponent: [
      { name: 'SYNE', value: 634 },
      { name: 'ECHOS (Python)', value: 361 },
      { name: 'Launcher', value: 217 },
      { name: 'syne-mock', value: 46 },
    ],
  },
  coverage: {
    echos: '90,55 %',
    syne: '87,41 %',
    syneCore: '90,43 %',
    syneConsole: '74,03 %',
  },
  ci: '13/13 jobs verts sur develop, le 11/10/2026',
  windows: 'Support Windows validé localement depuis le 08/10/2026',
} as const;

/** Composants visibles sur la page Versions (alignés sur le dépôt). */
export const VERSION_TABLE = [
  { component: 'SYNE', version: '0.15.0', note: 'Moteur de simulation .NET' },
  { component: 'ECHOS', version: '0.1.0', note: 'Paquet Python (observation, API headless)' },
  { component: 'PRISM', version: '0.x', note: 'Projet Unreal et plugin PRISM-LDK' },
  { component: 'Launcher', version: '0.x', note: 'Application de bureau Avalonia' },
] as const;

/** Miroir anglais — chaînes localisées uniquement (les chiffres restent dans PROJECT_STATE). */
export const RELEASE_EN = {
  plannedLabel: 'V0.1 — in preparation',
} as const;

export const PROJECT_STATE_EN = {
  observedDate: '12 October 2026',
  coverage: {
    echos: '90.55%',
    syne: '87.41%',
    syneCore: '90.43%',
    syneConsole: '74.03%',
  },
  ci: '13/13 jobs green on develop, 11 Oct 2026',
  windows: 'Windows support validated locally since 8 Oct 2026',
} as const;

export const VERSION_TABLE_EN = [
  { component: 'SYNE', version: '0.15.0', note: '.NET simulation engine' },
  { component: 'ECHOS', version: '0.1.0', note: 'Python package (observation, headless API)' },
  { component: 'PRISM', version: '0.x', note: 'Unreal project and PRISM-LDK plugin' },
  { component: 'Launcher', version: '0.x', note: 'Avalonia desktop application' },
] as const;
