// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

import type { Lang } from '../i18n/ui';

/**
 * Sommaire de la monographie, partagé par les portails FR et EN.
 * Les chapitres eux-mêmes restent rédigés en français dans le dépôt.
 */
export const MONOGRAPH_REPO = 'https://github.com/donovan-dev-web/LIVEX/blob/main/docs/monographie';
export const MONOGRAPH_TREE = 'https://github.com/donovan-dev-web/LIVEX/tree/main/docs/monographie';

const t = (fr: string, en: string): Record<Lang, string> => ({ fr, en });

export interface MonographItem {
  label: Record<Lang, string>;
  href: string;
}

export interface MonographGroup {
  title: Record<Lang, string>;
  items: MonographItem[];
}

export const MONOGRAPH_GROUPS: MonographGroup[] = [
  {
    title: t('Sommaire', 'Contents'),
    items: [
      { label: t('Sommaire général', 'General table of contents'), href: `${MONOGRAPH_REPO}/00-SOMMAIRE-GENERAL.md` },
      { label: t('Avant-propos', 'Foreword'), href: `${MONOGRAPH_REPO}/00-AVANT-PROPOS.md` },
      { label: t("Note de l’auteur", "Author's note"), href: `${MONOGRAPH_REPO}/00-NOTE-AUTEUR.md` },
      { label: t('Conventions', 'Conventions'), href: `${MONOGRAPH_REPO}/00-CONVENTIONS.md` },
    ],
  },
  {
    title: t('Introduction', 'Introduction'),
    items: [
      { label: t('01 — Introduction', '01 — Introduction'), href: `${MONOGRAPH_REPO}/01-introduction.md` },
      { label: t('02 — Contexte et philosophie', '02 — Context and philosophy'), href: `${MONOGRAPH_REPO}/02-contexte-philosophie.md` },
      { label: t('02b — Question décomposée', '02b — Decomposed question'), href: `${MONOGRAPH_REPO}/02b-question-decomposee.md` },
      { label: t('03 — Fondements', '03 — Foundations'), href: `${MONOGRAPH_REPO}/03-fondements.md` },
      { label: t('03b — Concepts et références', '03b — Concepts and references'), href: `${MONOGRAPH_REPO}/03b-concepts-references.md` },
      { label: t('04 — Approche méthodologique', '04 — Methodological approach'), href: `${MONOGRAPH_REPO}/04-approche-methodologique.md` },
      { label: t('04b — Vision et architecture', '04b — Vision and architecture'), href: `${MONOGRAPH_REPO}/04b-projet-vision-architecture.md` },
    ],
  },
  {
    title: t('SYNE', 'SYNE'),
    items: [
      { label: t('README SYNE', 'SYNE README'), href: `${MONOGRAPH_REPO}/05-syne/README.md` },
      { label: t('01 — Monde et moteur', '01 — World and engine'), href: `${MONOGRAPH_REPO}/05-syne/01-monde-et-moteur.md` },
      { label: t('02 — Agents et cognition', '02 — Agents and cognition'), href: `${MONOGRAPH_REPO}/05-syne/02-agents-cognition.md` },
      { label: t('03 — Boucle de simulation', '03 — Simulation loop'), href: `${MONOGRAPH_REPO}/05-syne/03-boucle-simulation.md` },
      { label: t('04 — Persistance', '04 — Persistence'), href: `${MONOGRAPH_REPO}/05-syne/04-persistance.md` },
      { label: t('05 — Contrats', '05 — Contracts'), href: `${MONOGRAPH_REPO}/05-syne/05-contrats.md` },
      { label: t('06 — Configuration', '06 — Configuration'), href: `${MONOGRAPH_REPO}/05-syne/06-configuration.md` },
      { label: t('07 — Systèmes collectifs', '07 — Collective systems'), href: `${MONOGRAPH_REPO}/05-syne/07-systemes-collectifs.md` },
      { label: t('08 — Communication', '08 — Communication'), href: `${MONOGRAPH_REPO}/05-syne/08-communication.md` },
      { label: t('09 — Traits et tempéraments', '09 — Traits and temperaments'), href: `${MONOGRAPH_REPO}/05-syne/09-traits-et-temperaments.md` },
      { label: t('10 — Besoins, seuils et dérive', '10 — Needs, thresholds and drift'), href: `${MONOGRAPH_REPO}/05-syne/10-besoins-seuils-et-derive.md` },
      { label: t('11 — Perception, mémoire, croyances', '11 — Perception, memory, beliefs'), href: `${MONOGRAPH_REPO}/05-syne/11-perception-memoire-et-croyances.md` },
      { label: t("12 — Formule d’utilité et risque", '12 — Utility formula and risk'), href: `${MONOGRAPH_REPO}/05-syne/12-la-formule-dutilite-et-le-risque.md` },
    ],
  },
  {
    title: t('ECHOS', 'ECHOS'),
    items: [
      { label: t('README ECHOS', 'ECHOS README'), href: `${MONOGRAPH_REPO}/06-echos/README.md` },
      { label: t('01 — Architecture', '01 — Architecture'), href: `${MONOGRAPH_REPO}/06-echos/01-architecture.md` },
      { label: t('02 — Métriques', '02 — Metrics'), href: `${MONOGRAPH_REPO}/06-echos/02-metriques.md` },
      { label: t('03 — Analyse causale', '03 — Causal analysis'), href: `${MONOGRAPH_REPO}/06-echos/03-analyse-causale.md` },
      { label: t('04 — API', '04 — API'), href: `${MONOGRAPH_REPO}/06-echos/04-api.md` },
      { label: t('05 — Outils mathématiques partagés', '05 — Shared mathematical tools'), href: `${MONOGRAPH_REPO}/06-echos/05-les-outils-mathematiques-partages.md` },
      { label: t('06 — Formules des métriques', '06 — Metric formulas'), href: `${MONOGRAPH_REPO}/06-echos/06-les-formules-des-metriques.md` },
      { label: t('07 — Indice de viabilité dynamique', '07 — Dynamic viability index'), href: `${MONOGRAPH_REPO}/06-echos/07-indice-de-viabilite-dynamique.md` },
      { label: t('08 — Composites, reproductibilité, causalité', '08 — Composites, reproducibility, causality'), href: `${MONOGRAPH_REPO}/06-echos/08-composites-reproductibilite-et-causalite.md` },
    ],
  },
  {
    title: t('PRISM', 'PRISM'),
    items: [
      { label: t('README PRISM', 'PRISM README'), href: `${MONOGRAPH_REPO}/07-prism/README.md` },
      { label: t('01 — Projet Unreal', '01 — Unreal project'), href: `${MONOGRAPH_REPO}/07-prism/01-projet-unreal.md` },
      { label: t('02 — Plugin LDK', '02 — LDK plugin'), href: `${MONOGRAPH_REPO}/07-prism/02-plugin-ldk.md` },
      { label: t('03 — Échelle, cadence, transport', '03 — Scale, cadence, transport'), href: `${MONOGRAPH_REPO}/07-prism/03-echelle-cadence-et-transport.md` },
      { label: t('04 — Flux de données et rendu', '04 — Data flow and rendering'), href: `${MONOGRAPH_REPO}/07-prism/04-flux-de-donnees-et-rendu.md` },
    ],
  },
  {
    title: t('Launcher', 'Launcher'),
    items: [
      { label: t('README Launcher', 'Launcher README'), href: `${MONOGRAPH_REPO}/08-launcher/README.md` },
      { label: t('01 — Orchestration', '01 — Orchestration'), href: `${MONOGRAPH_REPO}/08-launcher/01-orchestration.md` },
      { label: t('02 — Campagnes LIVEXP', '02 — LIVEXP campaigns'), href: `${MONOGRAPH_REPO}/08-launcher/02-campagnes-livexp.md` },
      { label: t('03 — Observabilité et supervision', '03 — Observability and supervision'), href: `${MONOGRAPH_REPO}/08-launcher/03-observabilite-et-supervision.md` },
      { label: t('04 — Paquet flux et réseau', '04 — Stream and network package'), href: `${MONOGRAPH_REPO}/08-launcher/04-paquet-flux-et-reseau.md` },
    ],
  },
  {
    title: t('Résultats et limites', 'Results and limits'),
    items: [
      { label: t('09 — Installer', '09 — Install'), href: `${MONOGRAPH_REPO}/09-installer.md` },
      { label: t('09b — Exigences et parcours', '09b — Requirements and paths'), href: `${MONOGRAPH_REPO}/09b-installer-exigences-et-parcours.md` },
      { label: t('10 — Intégration système', '10 — System integration'), href: `${MONOGRAPH_REPO}/10-integration-systeme.md` },
      { label: t('11 — Reproductibilité et déterminisme', '11 — Reproducibility and determinism'), href: `${MONOGRAPH_REPO}/11-reproductibilite-determinisme.md` },
      { label: t('12 — Expérimentation', '12 — Experimentation'), href: `${MONOGRAPH_REPO}/12-experimentation.md` },
      { label: t('13 — Résultats et observables', '13 — Results and observables'), href: `${MONOGRAPH_REPO}/13-resultats-et-observables.md` },
      { label: t('14 — Limites réelles', '14 — Real limits'), href: `${MONOGRAPH_REPO}/14-limites-reelles.md` },
      { label: t('15 — Conclusion', '15 — Conclusion'), href: `${MONOGRAPH_REPO}/15-conclusion.md` },
      { label: t('16 — Bibliographie', '16 — Bibliography'), href: `${MONOGRAPH_REPO}/16-bibliographie.md` },
    ],
  },
  {
    title: t('Annexes', 'Appendices'),
    items: [
      { label: t('Annexes (dossier)', 'Appendices (folder)'), href: MONOGRAPH_TREE + '/annexes' },
    ],
  },
];
