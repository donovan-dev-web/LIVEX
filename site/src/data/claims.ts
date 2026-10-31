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
      'Les tests exécutés pour LIVEX 1.0.0 totalisent 1 566 cas : 1 550 réussis (dont cinq Unreal Automation avec avertissements), 16 ignorés et zéro échec. Les suites couvrent SYNE, ECHOS, syne-mock, Launcher, PRISM/PrismLdk et l’installateur.',
    status: 'vérifiée',
    source: 'docs/governance/LIVEX-STATUS.md §4',
    date: '2026-10-31',
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
      'LIVEX 1.0.0 est publié sous le tag livex-v1.0.0 avec des paquets Windows et Linux.',
    status: 'vérifiée',
    source: 'docs/governance/LIVEX-STATUS.md §1 ; VERSIONING.md §7',
    date: '2026-10-31',
  },
  {
    id: 'prism-scope',
    statement: 'PRISM 1.0.0 est packagé pour Windows et Linux ; le packaging ne certifie pas toutes les fonctions de rendu visées.',
    status: 'vérifiée',
    source: 'docs/governance/LIVEX-STATUS.md §1',
    date: '2026-10-31',
  },
  {
    id: 'prism-ldk-package',
    statement: 'PRISM-LDK (LIVEX Development Kit), module Unreal PrismLdk, est réalisé et packagé en version 1.0.0.',
    status: 'vérifiée',
    source: 'docs/prism/README.md ; docs/prism/TESTING.md ; docs/governance/LIVEX-STATUS.md §1',
    date: '2026-10-31',
  },
  {
    id: 'code-inventory',
    statement: 'Tokei 13.0.0 a relevé 1 152 fichiers et 111 608 lignes de code dans le checkout au 31 octobre 2026, selon les exclusions du statut du projet.',
    status: 'vérifiée',
    source: 'docs/governance/LIVEX-STATUS.md §5',
    date: '2026-10-31',
  },
];
