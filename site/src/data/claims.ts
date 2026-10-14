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
      'Au 12 octobre 2026, les suites locales totalisent 1 258 tests réussis et 16 ignorés, sans échec (SYNE 634, ECHOS 361, Launcher 217, syne-mock 46).',
    status: 'vérifiée',
    source: 'docs/governance/LIVEX-STATUS.md §1 et §5',
    date: '2026-10-12',
  },
  {
    id: 'coverage',
    statement:
      'La couverture de lignes mesurée le 12 octobre 2026 est de 90,55 % pour ECHOS et de 87,41 % pour les assemblages SYNE agrégés.',
    status: 'vérifiée',
    source: 'docs/governance/LIVEX-STATUS.md §1',
    date: '2026-10-12',
  },
  {
    id: 'ci',
    statement: 'La CI compte 13/13 jobs verts sur develop le 11 octobre 2026.',
    status: 'vérifiée',
    source: 'docs/governance/LIVEX-STATUS.md §1',
    date: '2026-10-11',
  },
  {
    id: 'release',
    statement:
      "Aucune release, publication ni tag n'existe à ce jour ; une version V0.1 est en préparation.",
    status: 'vérifiée',
    source: 'docs/governance/LIVEX-STATUS.md §1 ; VERSIONING.md §7',
    date: '2026-10-12',
  },
  {
    id: 'prism-scope',
    statement: "L'expérience de rendu PRISM est hors périmètre V0.1.",
    status: 'vérifiée',
    source: 'docs/governance/LIVEX-STATUS.md §1',
    date: '2026-10-12',
  },
];
