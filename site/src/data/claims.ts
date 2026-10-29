// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

/**
 * Registre des affirmations sensibles (référentiel éditorial §3.6).
 * Chaque affirmation publiée sur le site doit pouvoir citer sa source.
 * Statut : vérifiée | prudente | interdite.
 */

export type ClaimStatus = 'vérifiée' | 'prudente';

export interface Claim {
  id: string;
  /** Formulation autorisée sur le site. */
  statement: string;
  status: ClaimStatus;
  /** Source interne et date de vérification. */
  source: string;
  date: string;
}

export const CLAIMS: Claim[] = [
  {
    id: 'tests',
    statement:
      'Au 19 octobre 2026, les suites locales totalisent 1 372 cas : 1 356 réussis, 0 échec (SYNE 695, ECHOS 397, syne-mock 53, Launcher 227).',
    status: 'vérifiée',
    source: 'docs/governance/LIVEX-STATUS.md §1 et §5',
    date: '2026-10-19',
  },
  {
    id: 'coverage',
    statement:
      'La couverture partielle mesurée le 19 octobre 2026 est de 91,32 % pour ECHOS et de 81,20 % pour le ratio disponible.',
    status: 'vérifiée',
    source: 'docs/governance/LIVEX-STATUS.md §1',
    date: '2026-10-19',
  },
  {
    id: 'ci',
    statement: 'La CI compte 13/13 jobs verts sur develop le 19 octobre 2026.',
    status: 'vérifiée',
    source: 'docs/governance/LIVEX-STATUS.md §1',
    date: '2026-10-19',
  },
  {
    id: 'release',
    statement:
      "Aucune release, publication ni tag n'existe à ce jour ; le projet est passé en V0.2.",
    status: 'vérifiée',
    source: 'docs/governance/LIVEX-STATUS.md §1 ; VERSIONING.md §7',
    date: '2026-10-19',
  },
  {
    id: 'prism-scope',
    statement: "L'expérience de rendu PRISM est hors périmètre V0.2.",
    status: 'vérifiée',
    source: 'docs/governance/LIVEX-STATUS.md §1',
    date: '2026-10-19',
  },
  {
    id: 'prism-ldk-package',
    statement: "PRISM-LDK (LIVEX Development Kit) est le cœur de la représentation 3D : plugin Unreal qui traduit les données logiques de SYNE en représentation graphique 3D, réalisé et packagé en version 0.1.0. PRISM en est l'implémentation stylisée par défaut.",
    status: 'vérifiée',
    source: 'Commit #596 - feat(prism): LDK Validate V0.1.0 Plugin Package ; docs/prism/PRISM-LDK_Guide_Blueprint.md',
    date: '2026-10-29',
  },
];
