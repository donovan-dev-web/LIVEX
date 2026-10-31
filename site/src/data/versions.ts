// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

/**
 * Historique des versions publiées (fiches par version, S14).
 * Fiches des versions publiées — voir data/release.ts, RELEASE.published.
 * Ajouter une entrée génère automatiquement /versions/<slug>/ (FR) et /en/versions/<slug>/.
 */
import type { StatusId } from './components';

export interface VersionAsset {
  platform: string;
  url: string;
  size?: string;
  sha256?: string;
}

export interface VersionEntry {
  /** Identifiant d'URL, sans préfixe (ex. « v0-1 »). */
  slug: string;
  /** Libellé affiché (ex. « V0.1 »). */
  label: string;
  /** Date de publication, lisible (ex. « 12 octobre 2026 »). */
  date: string;
  dateEn: string;
  status: StatusId;
  summary: string;
  summaryEn: string;
  highlights: { component: string; items: string[]; itemsEn: string[] }[];
  changes: string[];
  changesEn: string[];
  fixes: string[];
  fixesEn: string[];
  known: string[];
  knownEn: string[];
  assets: VersionAsset[];
}

const releaseBase = 'https://github.com/donovan-dev-web/LIVEX/releases/download/livex-v1.0.0';

export const VERSIONS: VersionEntry[] = [
  {
    slug: 'livex-v1-0-0',
    label: 'LIVEX 1.0.0',
    date: '31 octobre 2026',
    dateEn: '31 October 2026',
    status: 'disponible',
    summary: 'Première release publique de LIVEX, distribuée avec un installateur Windows et Linux. PRISM et PRISM-LDK sont réalisés et packagés.',
    summaryEn: 'First public LIVEX release, distributed with Windows and Linux installers. PRISM and PRISM-LDK are implemented and packaged.',
    highlights: [
      {
        component: 'Distribution',
        items: ['Installateur Windows x64 et Linux x64, variantes core et full.'],
        itemsEn: ['Windows x64 and Linux x64 installers, core and full variants.'],
      },
      {
        component: 'PRISM',
        items: ['Application PRISM packagée pour Windows et Linux.', 'Plugin Unreal PRISM-LDK (`PrismLdk`) version 1.0.0.'],
        itemsEn: ['PRISM application packaged for Windows and Linux.', 'PRISM-LDK Unreal plugin (`PrismLdk`) version 1.0.0.'],
      },
    ],
    changes: ['Publication de la release publique taguée `livex-v1.0.0`.'],
    changesEn: ['Published the public release tagged `livex-v1.0.0`.'],
    fixes: [],
    fixesEn: [],
    known: ['Les résultats de tests sont datés par composant dans la documentation ; le packaging ne signifie pas que chaque capacité décrite est validée.'],
    knownEn: ['Test results are dated per component in the documentation; packaging does not mean every described capability has been validated.'],
    assets: [
      { platform: 'Windows x64 · core', url: `${releaseBase}/livex-1.0.0-win-x64-core.zip` },
      { platform: 'Windows x64 · full', url: `${releaseBase}/livex-1.0.0-win-x64-full.zip` },
      { platform: 'Linux x64 · core', url: `${releaseBase}/livex-1.0.0-linux-x64-core.tar.gz` },
      { platform: 'Linux x64 · full', url: `${releaseBase}/livex-1.0.0-linux-x64-full.tar.gz` },
    ],
  },
];
