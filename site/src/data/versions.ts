/**
 * Historique des versions publiées (fiches par version, S14).
 * Vide tant qu'aucune release n'est posée — voir data/release.ts, RELEASE.published.
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

export const VERSIONS: VersionEntry[] = [];
