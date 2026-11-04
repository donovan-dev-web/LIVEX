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
      'La release LIVEX 1.0.0 est publiée ; la cible globale 2.0.0 est en préparation.',
    status: 'prudente',
    source: 'Release V1.0.0 et cible V2 communiquées par le mainteneur',
    date: '2026-11-04',
  },
  {
    id: 'prism-scope',
    statement: 'La cible PRISM V2 est un monde physique autoritaire .NET ; son viewer Silk.NET est facultatif.',
    status: 'prudente',
    source: 'docs/prism/VISION.md et docs/prism/ARCHITECTURE.md',
    date: '2026-11-04',
  },
  {
    id: 'prism-ldk-package',
    statement: 'PRISM-LDK est le plugin Unreal de PRISM V1 ; cette architecture est historique et ne décrit pas la cible V2.',
    status: 'prudente',
    source: 'Commit #596 - feat(prism): LDK Validate V0.1.0 Plugin Package ; docs/prism/PRISM-LDK_Guide_Blueprint.md',
    date: '2026-10-29',
  },
];
