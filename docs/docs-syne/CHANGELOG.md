# CHANGELOG — SYNE

**Composant** : SYNE
**Statut** : [DRAFT]
**Dernière mise à jour** : 21 septembre 2026
**Dépend de** : `../../VERSIONING.md`

Format : [Keep a Changelog](https://keepachangelog.com/en/1.1.0/). Versionnement : SemVer (`syne-vX.Y.Z`).

## [Unreleased]

### Added
- Documentation technique V0.1 complète du composant (VISION, ARCHITECTURE, DATA_MODEL, SIMULATION_LOOP, COGNITIVE_ARCHITECTURE, SYSTEMS_SPEC, COMMUNICATION_PROTOCOL, PERSISTENCE, DETERMINISM, CONFIGURATION, API_CONTRACTS, PERFORMANCE, TESTING, ROADMAP).
- Formalisation des ADR-001, ADR-002, ADR-005 à ADR-011 (Annexe F de la Monographie).
- **Socle U0 (SYNE-1)** : solution `Syne.sln`, bibliothèque `Simulation.Core` (configuration Annexe H, loader JSON générique, validation, flags CLI), `Simulation.Console` (conf résolue + sonde PRNG), tests xUnit (28). PRNG déterministe **xoshiro256\*\*** + **splitmix64** (vecteurs épinglés), `global.json` SDK 10.0.400. ADR-012 (config JSON + CLI).
- **Noyau U0 (SYNE-2)** : boucle minimale (1 tick = 1 min simulée, `maxTicks` respecté, tête/queue affichées), monde continu 500×500 non-toroidal (positions clampées), **grille spatiale uniforme** (requêtes par rayon déterministes, cellule configurable), entités typées (identité séquentielle, espèce, position, traits **8 traits [0, 2]** hérités d'un **paramétrage** plages de traits), fabrique déterministe épinglée (référence indépendante), tests xUnit (62).
- **Jalon SYNE ph1 — BDI + Perception (SYNE-010 à SYNE-015, issues #7–#12)** :
  - **Perception (SYNE-011/012)** : rayon par défaut **50** (décision n°6), confiance `1 − (d/r)×0.3` clampée [0.7, 1.0], perception **étagée** (`id % 4`), **ligne de vue** obstacle cercle (ADR-013), obstacles observés (id FNV-1a), requête `QueryCircle` bornée (fenêtre 3×3) avec **micro-benchmark CI** (< 10 ms/requête).
  - **Mémoire (SYNE-013)** : salience exponentielle (decay 0.01/0.005/0.002), seuil d'oubli 0.01, capacité 1000 avec **éviction du moins saillant**, rappel ordonné (StoredAt puis Sequence).
  - **Croyances (SYNE-014)** : faits `(subject, predicate, value)`, révision (alignement +0.2 / moyenne / **conflit** −0.1 avec création), plafond par snap, expiration (plafond 0.4) et décroissance `timeDecayPerTick`.
  - **BDI + utilité (SYNE-010)** : pipeline 10/15 étapes branché dans la boucle (perception → mémoire → croyances → besoins → désirs → délibération → intention → action), utilité `U = (benefit − cost − risk) × confidence × personalityModifier + urgency`, mouvements déterministes sans consommation PRNG.
  - **Déterminisme (SYNE-015)** : `DeterminismRegressionTests` — hash FNV-1a **épinglé** de la trajectoire perception+décision, égalité bit-à-bit entre 2 runs identiques, divergence entre seeds.
  - Tests : +60 (62 → **122**). ADR-013 (ligne de vue en V1).

### Added
- **Observabilité (SYNE-080, issue #37, milestone ph8)** : émetteur WebSocket **BCL minimal**
  (HttpListener + `AcceptWebSocketAsync`, zéro dépendance) dans `Simulation.Console` activé par
  `--observe` (`--observe-port`, défaut 5180, bind `127.0.0.1`). Contrat API_CONTRACTS §2 :
  **1 snapshot/tick** (version, runId `run-<seed>`, tick, simulatedTimeMinutes, aliveCount,
  agents[{id, species, position{x,y}, energy, hunger, thirst, fatigue, currentAction}], resources[])
  + **1 `tick_summary`/tick** + **1 `decision_made`/entité/tick** ({intention, utility}) en **JSON
  camelCase déterministe**. Aucun tirage PRNG ajouté (déterminisme préservé). Diffusion à tous les
  consommateurs connectés. Tests : `ObservabilitySensorTests` (Core, format/épinglage camelCase) +
  **`Simulation.Console.Tests`** (tests de fil WebSocket réels, 2). Suite : **129 tests**.
- **Jalon SYNE ph2 — Mémoire intergénérationnelle + Croyances + Confiance (SYNE-020 → SYNE-022, issues #13/#14/#15, milestone ph2)** :
  - **Confiance inter-entités (SYNE-021)** : `Relationships` (Interact +bonus, ObserveDeception −sanction, Tick décroissance ×`TrustDecayFactorPerTick`, défaut **0.9** — COMMUNICATION_PROTOCOL §3), confiance initiale 0.5, bonus de vérité 0.05, sanction de mensonge 0.2 ; `MindState.Trust` intégré au pipeline (Tick décroissance à chaque step).
  - **Mémoire intergénérationnelle (SYNE-020)** : `Inheritance.FuseTraits` (moyenne), `InheritMemory` (union, seuil de salience, ré-horodatage `birthTick`), `InheritBeliefs` (union, confiance max sur fait identique, source « héritage », expiration restampée) ; naissance par fusion consentie `MindState.Born(options, parentA, parentB, birthTick)` (décision n°16, COGNITIVE_ARCHITECTURE §6.6).
  - **Éviction mémoire (SYNE-022)** : `Memory.AllEntries` ; stress test — capacité 1000 **jamais dépassée** (catégories mixtes, éviction du moins saillant).
  - **Observabilité étendue (additif, contrat V0.1 inchangé)** : `AgentSnapshot.From(entity, mind, currentTick)` émet `traits`, `beliefs` (±confiance), `goals` (kind/age), `trust` (peerId/level), `memoryCount` (camelCase) — consommé par les 7 moteurs ECHOS au jalon U2 ECHOS.
  - Tests : +17 (129 → **146**). Build Release 0 warning / 0 erreur.

### Changed
- ARCHITECTURE.md : §4 (couche applicative réelle, Dockerfile reporté) et §6 (PRNG défini) mis à jour.
- (SYNE-2) ARCHITECTURE.md : §4 précise la couche implémentée (mondes, entités, grille, boucle).
- (SYNE ph1) DATA_MODEL, COGNITIVE_ARCHITECTURE, SYSTEMS_SPEC : rayon défaut 50, ligne de vue V1 (ADR-013), obstacles cercle V0.1.
- (SYNE ph1) CONFIGURATION : clés perception/mémoire/croyances/actions/dérives de besoins alignées sur l'implémentation.
- (SYNE ph1) DETERMINISM : contrat « pipeline = 0 tirage PRNG », tests de régression.
- (SYNE ph1) PERFORMANCE : méthodologie micro-benchmark grille (SYNE-012).
- (SYNE ph1) TESTING : périmètre 122 tests, commandes par filtre.

### Deprecated
- (aucun)

## [0.0.0] — à venir

Version initiale (prototype V1/V2 de la Monographie référencé comme [HÉRITÉ]).

---

## Mises à jour

| Date | Changement | Motif |
| :-- | :-- | :-- |
| 17 septembre 2026 | Création | Documentation V0.1 |
| 21 septembre 2026 | Socle U0 : solution, config, PRNG, ADR-012 | SYNE-001 / SYNE-005 / SYNE-006 |
| 21 septembre 2026 | Noyau U0 : boucle, monde + grille, entités + traits | SYNE-002 / SYNE-003 / SYNE-004 |
| 21 septembre 2026 | Jalon SYNE ph1 : BDI + Perception | SYNE-010 → SYNE-015 |