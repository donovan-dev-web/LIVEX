# ISSUES.md

**Composant** : SYNE
**Statut** : [STABLE]
**Dernière mise à jour** : 21 septembre 2026
**Dépend de** : `ISSUES.md` (racine, conventions), `KANBAN.md` (governance), `DECISIONS_V01.md`, `ROADMAP.md`
**Source Monographie** : Annexe J (feuille de route V2), §1.7.5 (déterminisme), §9.6.4 (issues ADR)

---

## 1. Objectif

Ce document est le **backlog complet** des issues du moteur SYNE, conçu pour être la **source unique de création des cartes du tableau Kanban** (`docs/governance/KANBAN.md`). Il couvre la réalisation **de A à Z** de SYNE, en liant chaque carte aux **décisions tranchées** (`DECISIONS_V01.md`), aux **ADR** (`adr/ADRD-*.md`) et aux **phases du ROADMAP** (`ROADMAP.md`).

Chaque issue est **prête à être copiée** dans un système d'issues (GitHub/GitLab) avec son **titre**, son **label**, son **milestone**, sa **priorité**, ses **dépendances** et son **critère d'acceptation** — conformément aux règles de `docs/governance/ISSUES.md` (types, templates, cycle de vie) et aux colonnes du Kanban.

## 2. Taxonomy des labels et milestones

Alignée sur la convention de labels racine (`docs/governance/ISSUES.md` §5) et le ROADMAP SYNE (phases 0 à 11).

### 2.1 Labels de type (préfixe `type/`)

| Label | Usage |
| :-- | :-- |
| `type/feature` | Nouvelle fonctionnalité (comportement du moteur) |
| `type/bug` | Correction d'anomalie constatée |
| `type/docs` | Documentation / ADR / décision |
| `type/perf` | Performance, budgets, benchmarks |
| `type/test` | Tests, couverture, fiabilisation |
| `type/calibration` | Calibration des valeurs (`DECISIONS_V01` n°4, 5, 6, 9, 18, 19, 20…) |
| `type/persistence` | Persistance, sauvegarde/chargement, reprise |
| `type/obs` | Observabilité / événements / traces |
| `type/governance` | Process, kanban, jalons |

### 2.2 Labels de composant

| Label | Usage |
| :-- | :-- |
| `component/syne` | De base — toute issue SYNE porte ce label |
| `component/echos` | Si l'issue touche l'interface ECHOS (métriques, événements) |
| `component/prism` | Si l'issue touche l'interface PRISM (rendu, grille) |

### 2.3 Labels de statut / priorité / milestone

- Statut (complété au fil de l'eau, selon `KANBAN.md`) : `status/blocked`, `status/needs-input`, `status/good-first-issue`.
- Priorité : `priority/P0`, `priority/P1`, `priority/P2`, `priority/P3` (mêmes conventions que la Monographie Annexe J / `ROADMAP.md`).
- Milestones = **phases du ROADMAP** : `milestone/ph0` … `milestone/ph11`, plus `milestone/v0.1`, `milestone/v1`.

### 2.4 Règle de traçabilité

Toute valeur **non chiffrée** reste un point **hors spec** : les issues de calibration (décisions n°4, 5, 6, 9, 18, 19, 20…) sont **volontairement** positionnées en fin de cycle (après les premiers runs valides), conformément à `DECISIONS_V01.md` (bilan : 30 tranchées, 0 ouverte ; les 7 anciennes `[OUVERTES]` devenues `[TRANCHÉES]` restent **configurables**, pas figées par accident).

---

## 3. Backlog — réalisation A → Z de SYNE

Chaque sous-section = un milestone. Colones : ID · Titre · Labels · Priorité · Dépend de (ADR / décision / doc) · Critère d'acceptation.

### Milestone ph0 — Socle & architecture

| ID | Titre | Labels | Prio | Dépend de | Critère d'acceptation |
| :-- | :-- | :-- | :-- | :-- | :-- |
| SYNE-001 | Découpage en projets .NET (`Simulation.Core`, `Simulation.Console`) | `type/docs`, `component/syne`, `type/governance` | P0 | ADR-001 | `Simulation.Core` et `Simulation.Console` séparés, builds séparés, doc `ARCHITECTURE.md` à jour |
| SYNE-002 | Boucle de simulation minimale (tick) | `type/feature`, `component/syne` | P0 | ADR-002, décision n°1, `SIMULATION_LOOP.md` | 1 tick = 1 minute simulée ; `max-ticks` respecté ; boucle tête/queue affichée |
| SYNE-003 | Structure du monde 500×500 + grille spatiale | `type/feature`, `component/syne` | P0 | décision n°2, `DATA_MODEL.md` | Monde continu 2D de 500×500, grille de perception configurable, déterminisme des positions |
| SYNE-004 | Contrats d'entité (identité, position, traits) | `type/feature`, `component/syne` | P0 | décision n°4, `DATA_MODEL.md`, `API_CONTRACTS.md` | Entité typée avec traits hérités, validation des invariants (V0.1) |
| SYNE-005 | Conventions ADR du composant (template ADRG-*) | `type/docs`, `component/syne`, `type/governance` | P1 | ADR-001, `docs/governance/ISSUES.md` | Template ADR prêt, 1 ADR pilote fusionné |
| SYNE-006 | Configuration `global.json` + paramètres moteur (tick, seed, monde) | `type/feature`, `component/syne` | P1 | ADR-001, `CONFIGURATION.md` | `config.json` chargé ; flags CLI (`--seed`, `--max-ticks`, `--world-size`) opérationnels |

### Milestone ph1 — BDI + Perception

| ID | Titre | Labels | Prio | Dépend de | Critère d'acceptation |
| :-- | :-- | :-- | :-- | :-- | :-- |
| SYNE-010 | Cœur BDI (croyance, désir, intention) V0.1 | `type/feature`, `component/syne` | P0 | décisions n°12, 13, 14 ; `COGNITIVE_ARCHITECTURE.md` | Boucle BDI exécutée à chaque tick ; désirs priorités par l'utilité |
| SYNE-011 | Perception partielle (rayon 50, ligne de vue) | `type/feature`, `component/syne` | P0 | décision n°6, `COGNITIVE_ARCHITECTURE.md` §3, `DATA_MODEL.md` §4, ADR-013 | Entité ne perçoit que dans son rayon ; obstacle masque la ligne de vue |
| SYNE-012 | Grille spatiale pour la perception | `type/perf`, `component/syne` | P1 | SYNE-011, `ARCHITECTURE.md` | Requête de voisinage < budget tick ; benchmark documenté |
| SYNE-013 | Mémoire à court/long terme (décroissance) | `type/feature`, `component/syne` | P0 | décision n°11, `COGNITIVE_ARCHITECTURE.md` §3.2.2 | Souvenirs avec source/type/contenu/confiance/date ; decay 0.01/0.005/0.002 |
| SYNE-014 | Révision des croyances (décision n°12) | `type/feature`, `component/syne` | P0 | SYNE-013, décision n°12 | `belief = belief + (signal − belief) × strength` ; plafond par snap |
| SYNE-015 | Déterminisme bit-à-bit de la perception | `type/test`, `component/syne` | P0 | SYNE-011, `DETERMINISM.md` | Même seed + config = même séquence de perception (test de régression) |

### Milestone ph2 — Mémoire + Croyances

| ID | Titre | Labels | Prio | Dépend de | Critère d'acceptation |
| :-- | :-- | :-- | :-- | :-- | :-- |
| SYNE-020 | Mémoire intergénérationnelle (héritage) | `type/feature`, `component/syne` | P0 | décision n°16, `COGNITIVE_ARCHITECTURE.md` §6.6 | Héritage par fusion consentie ; traits + savoir transmis (§6.6.3) |
| SYNE-021 | Perte de confiance / trustDecay | `type/feature`, `component/syne` | P1 | décision n°10, `COMMUNICATION_PROTOCOL.md` §3 | Confiance décroît sans interaction (0.9) et après mensonge |
| SYNE-022 | Éviction mémoire (capacité 1000) | `type/feature`, `component/syne` | P1 | SYNE-013, décision n°11 | Éviction par decay + seuil ; jamais de dépassement de capacité |

### Milestone ph3 — Décision + Utilité

| ID | Titre | Labels | Prio | Dépend de | Critère d'acceptation |
| :-- | :-- | :-- | :-- | :-- | :-- |
| SYNE-030 | Formule d'utilité (décision n°13) | `type/feature`, `component/syne` | P0 | décision n°13, `COGNITIVE_ARCHITECTURE.md` §3.4.10 | `U = (benefit − cost − risk) × confidence × personalityModifier + urgency` implémenté et testé |
| SYNE-031 | Sélecteur d'action (max utilité, délibération) | `type/feature`, `component/syne` | P0 | décision n°14, `SIMULATION_LOOP.md` §4.2.8 | Sélection déterministe ; fréquence de délibération configurable |
| SYNE-032 | Interruptions d'actions (besoins critiques) | `type/feature`, `component/syne` | P1 | décision n°15, `SYSTEMS_SPEC.md` §3.4.7 | Action interrompue par besoin urgent / pulsation, reprise cohérente |
| SYNE-033 | Gestion des conflits de priorités (V0.1) | `type/feature`, `component/syne` | P1 | décision n°22, `SYSTEMS_SPEC.md` §5 | Résolution probabiliste (confiance × force) ; aucun arbitraire d'ancienneté |

### Milestone ph4 — Actions

| ID | Titre | Labels | Prio | Dépend de | Critère d'acceptation |
| :-- | :-- | :-- | :-- | :-- | :-- |
| SYNE-040 | Catalogue d'actions (déclaratif) | `type/feature`, `component/syne` | P0 | `SYSTEMS_SPEC.md` §4, `CONFIGURATION.md` | Actions déclarées en config ; exécution atomique par tick |
| SYNE-041 | Action Déplacement | `type/feature`, `component/syne` | P0 | SYNE-002, SYNE-010 | Déplacement respecte les obstacles et le coût énergie |
| SYNE-042 | Action Besoin (manger/boire/instruire) | `type/feature`, `component/syne` | P0 | décisions n°4, 5 ; `SYSTEMS_SPEC.md` §3.9.1 | Besoins déclenchés ≥ 50 ; réserves mises à jour |
| SYNE-043 | Interruption d'action (déclenchement) | `type/feature`, `component/syne` | P1 | SYNE-032, décision n°15 | Déclencheur d'interruption centralisé |

### Milestone ph5 — Communication

| ID | Titre | Labels | Prio | Dépend de | Critère d'acceptation |
| :-- | :-- | :-- | :-- | :-- | :-- |
| SYNE-050 | Pulsations publiques (relais, portée 50) | `type/feature`, `component/syne` | P0 | décisions n°7, 8 ; `COMMUNICATION_PROTOCOL.md` | Signal émis par pulsation ; perçu par toute entité dans la portée (ligne de vue) |
| SYNE-051 | Interception des pulsations | `type/feature`, `component/syne` | P0 | décisions n°8, 9 | Interception possible et publique ; pas de canaux privés (décision n°8) |
| SYNE-052 | Coûts émission/réception (hérités) | `type/calibration`, `component/syne` | P1 | décision n°9, `SYSTEMS_SPEC.md` §4.8 | Coûts hérités (envoi 0.5+p×0.1, réception 0.2+p×0.05) configurables |
| SYNE-053 | Protocole de confiance (relais 10 %/hop) | `type/feature`, `component/syne` | P1 | décision n°10, `COMMUNICATION_PROTOCOL.md` §3 | Confiance du message dégradée de 10 % par relais |
| SYNE-054 | Communication inter-composants (ECHOS/PRISM) | `type/feature`, `component/syne`, `component/echos` | P0 | ADR-011, `ARCHITECTURE.md`, `COMMUNICATION.md` | Événements pourables vers ECHOS + snapshot vers PRISM via contrats transport |

### Milestone ph6 — Groupes

| ID | Titre | Labels | Prio | Dépend de | Critère d'acceptation |
| :-- | :-- | :-- | :-- | :-- | :-- |
| SYNE-060 | Cohésion de groupe (confiance × buts) | `type/feature`, `component/syne` | P1 | décisions n°23, 24 ; `SOCIAL_NETWORK.md` | Groupes formés par cohésion émergente (pas de script de coalition) |
| SYNE-061 | Leadership & décisions collectives | `type/feature`, `component/syne` | P1 | décision n°24 | Leader émergent ; décision de groupe via consensus/confiance |
| SYNE-062 | Naissance par fusion consentie (V0.2) | `type/feature`, `component/syne` | P1 | décisions n°17, 16 ; ADR-002 | Fusion volontaire mère-père → nouvelle entité héritant traits/mémoire |
| SYNE-063 | Héritage de traits (V0.2) | `type/feature`, `component/syne` | P1 | décision n°16 | Mécanismes fins (réadaptation/dominance/mutation) configurables §6.6.3 |

### Milestone ph7 — Ressources + Environnement

| ID | Titre | Labels | Prio | Dépend de | Critère d'acceptation |
| :-- | :-- | :-- | :-- | :-- | :-- |
| SYNE-070 | Cycle des ressources (nourriture, eau, bois, minéraux) | `type/feature`, `component/syne` | P0 | décision n°4, `DATA_MODEL.md`, `SYSTEMS_SPEC.md` §3.10 | Ressources V1 non régénératives ; V2 régénération/dégradation |
| SYNE-071 | Constructions (obstacles statiques) | `type/feature`, `component/syne` | P1 | décision n°20, `SYSTEMS_SPEC.md` §3.14, §6.4 | Construction = obstacle statique de la grille ; modification d'environnement tracée |
| SYNE-072 | Saisons & environnement dynamique | `type/feature`, `component/syne` | P2 | SYNE-070, `SYSTEMS_SPEC.md` §6. titled | Variations périodiques appliquées ; déterminisme conservé |
| SYNE-073 | Territoire (décision n°21) | `type/feature`, `component/syne` | P1 | décision n°21, `SYSTEMS_SPEC.md` §6.5 | Territoire = zone des ressources autour du point de survie ; perception V0.1 |

### Milestone ph8 — Observabilité

| ID | Titre | Labels | Prio | Dépend de | Critère d'acceptation |
| :-- | :-- | :-- | :-- | :-- | :-- |
| SYNE-080 | Événements typés (snapshot + event) — **LIVRÉ (issue #37, PR S2, U1)** | `type/obs`, `component/syne`, `component/echos` | P0 | décision n°26, `API_CONTRACTS.md`, `PERCEPTION_SPEC.md` | Événements émis à chaque tick + événements discrets ; schéma JSON typé — ✓ snapshot/tick + `tick_summary` + `decision_made`, JSON camelCase (emetteur BCL `--observe`) |
| SYNE-081 | Anti-triche / observabilité non intrusive | `type/obs`, `component/syne` | P1 | SYNE-080, `ROADMAP.md` §8 | Vérification que l'observation ne modifie pas l'état du monde |
| SYNE-082 | Métriques émises vers ECHOS | `type/feature`, `component/syne`, `component/echos` | P1 | SYNE-080, `METRICS_SPEC.md` | Flux ECHOS consommable pour l'analyse (déterministe) |

### Milestone ph9 — Performance & Scalabilité

| ID | Titre | Labels | Prio | Dépend de | Critère d'acceptation |
| :-- | :-- | :-- | :-- | :-- | :-- |
| SYNE-090 | Budgets par tick (§3.19.2) | `type/perf`, `component/syne` | P0 | `PERFORMANCE.md` §2 | Respect des budgets (≥ 30 % computation) ; benchmark annexe I |
| SYNE-091 | Cibles 50/500/1000 entités | `type/perf`, `component/syne` | P0 | décision n°29, `PERFORMANCE.md` | ≥ 30 t/s (50) ; ≥ 20 t/s (500) ; ≥ 10 t/s (1000) |
| SYNE-092 | Grille spatiale + pooling | `type/perf`, `component/syne` | P1 | SYNE-012, `PERFORMANCE.md` | Scalabilité ≥ 500 entités sans dégradation > budget |
| SYNE-093 | Déterminisme performance (bit-à-bit) | `type/test`, `component/syne` | P1 | `DETERMINISM.md` | Benchmark reproductible bit-à-bit à seed égale |

### Milestone ph10 — Tests & Couverture

| ID | Titre | Labels | Prio | Dépend de | Critère d'acceptation |
| :-- | :-- | :-- | :-- | :-- | :-- |
| SYNE-100 | Suite de tests unitaires (> 160 tests) | `type/test`, `component/syne` | P0 | `TESTING.md`, jalons T5 | 160+ tests ; couverture ≥ 80 % |
| SYNE-101 | Tests d'intégration (boucle complète) | `type/test`, `component/syne` | P1 | SYNE-100, `ROADMAP.md` §10 | Ticks chaînés ECHOS/PRISM sans perte |
| SYNE-102 | Tests de non-régression déterminisme | `type/test`, `component/syne` | P0 | SYNE-015, SYNE-093 | Baseline bit-à-bit : même seed = même trajectoire |

### Milestone ph11 — Persistance & Reprise

| ID | Titre | Labels | Prio | Dépend de | Critère d'acceptation |
| :-- | :-- | :-- | :-- | :-- | :-- |
| SYNE-110 | Modèle de persistance (SQLite, 11 tables) | `type/persistence`, `component/syne` | P0 | décision n°25, `PERSISTENCE.md`, `DATA_MODEL.md` | Schéma SQLite (11 tables) créé ; migration depuis V1 JSON |
| SYNE-111 | Sérialisation bit-à-bit + reprise | `type/persistence`, `component/syne` | P0 | SYNE-110, `DETERMINISM.md` | Reprise bit-à-bit : même seed+config = même suite |
| SYNE-112 | Reprise après crash (reprise du monde) | `type/persistence`, `component/syne` | P1 | SYNE-111 | Reprise sans perte de ticks ; test de récupération |

### Milestone v0.1 — Cross-cutting / validation

| ID | Titre | Labels | Prio | Dépend de | Critère d'acceptation |
| :-- | :-- | :-- | :-- | :-- | :-- |
| SYNE-120 | Calibration des valeurs (énergie, livres, constructions) | `type/calibration`, `component/syne` | P2 | décisions n°4, 5, 18, 19, 20, `DECISIONS_V01` | Valeurs fixées après premiers runs valides ; trace ADR |
| SYNE-121 | Livres : coût/bénéfice (V1, tranché) | `type/feature`, `component/syne` | P2 | décisions n°18, 19, `SYSTEMS_SPEC.md` §3.18 | Coût d'écriture configurable (auteur), bénéfice de lecture posé en principe |
| SYNE-122 | Jalon T0–T5 (validation du moteur) | `type/test`, `component/syne` | P0 | SYNE-100, `ROADMAP.md` §3 | T0..T5 validés (50 ent., 1000 ticks ; 50/2000 ; traits→décisions ; T5 160+) |

### Milestone v1 — Consolidation

| ID | Titre | Labels | Prio | Dépend de | Critère d'acceptation |
| :-- | :-- | :-- | :-- | :-- | :-- |
| SYNE-130 | Abandon de la morphologie (V1, ADR-010) | `type/docs`, `component/syne` | P1 | ADR-010 | Morphologie retirée ; doc de migration |
| SYNE-131 | Processus de calibration continue | `type/governance`, `component/syne` | P2 | SYNE-120, `KANBAN.md` | Pipeline de calibration relancé après chaque run |

---

## 4. Rules de suivi (Kanban)

- Toute carte `SYNE-*` doit avoir **exactement 1 label de milestone** (`milestone/ph*`, `milestone/v0.1`, `milestone/v1`) et **≥ 1 label de type** (`type/*`).
- Chaque carte porte la colonne Kanban correspondante à son état (Backlog → Todo → In Progress → In Review → Done, `KANBAN.md` §2).
- Une carte ne passe en **In Review** qu'avec une PR référençant l'issue (lien `Closes #SYNE-*`).
- Une carte **status/blocked** doit référencer le blocage (ADR, décision `[OUVERTE]`, donnée manquante).
- **Conformité** : toute carte doit tracer sa **décision** (`DECISIONS_V01.md` n°) quand elle existe — une valeur non tranchée reste calibrée plus tard, jamais figée par accident (Monographie §9.6.4).

## 5. Maintenance de ce document

- Document régénéré/actualisé lors de chaque **jalon** atteint (`ROADMAP.md` §4 fin de phase) et de chaque décision tranchée.
- Les IDs `SYNE-###` sont **stables** : aucune réindexation rétroactive ; un ID consommé est conservé (trace), jamais réutilisé.

---

## Points restés ouverts dans ce document

- Les **volumes** chiffrés (énergie, coûts livres, constructions) font l'objet des décisions de calibration reconnues **configurables** (`DECISIONS_V01` : 7 décisions proposées ouvertes devenues tranchées en principe, valeurs calibrées à l'implémentation). Les cartes concernées (SYNE-052, SYNE-120, SYNE-121) restent **créées** et **documentées** — rien n'est figé par accident.
