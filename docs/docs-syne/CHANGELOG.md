# CHANGELOG — SYNE

**Composant** : SYNE
**Statut** : [DRAFT]
**Dernière mise à jour** : 22 septembre 2026
**Dépend de** : `../../VERSIONING.md`

Format : [Keep a Changelog](https://keepachangelog.com/en/1.1.0/). Versionnement : SemVer (`syne-vX.Y.Z`).

## [Unreleased]

### Added
- **Jalon SYNE ph6 — Groupes & Naissance (SYNE-060 → SYNE-063, issues #29–#32, milestone ph6)** :
  - **`GROUPES` (élément de réseau social, SYNE-060/061)** : `GroupSystem` (propriété `Cognition.Groups`).
    Cohésion = min trust réciproque × affinité (1 + `sharedBeliefBonus` + `goalAlignmentBonus`),
    lien = confiance ≥ `trustThreshold` **et** affinité > 1 (décisions n°23/24) ; composantes
    union-find (racine = id min), révision LOD déterministe (`reviewIntervalTicks` = 10),
    cycle de vie par correspondance exacte des membres (turnover ⇒ dissolution + refonte),
    leader émergent = somme de confiance entrante max (tie-break id min), décisions collectives
    pondérées par la confiance au leader (quorum `consensusThreshold` = 0.5).
  - **`NAISSANCE` (fusion consentie, SYNE-062)** : `BirthSystem` — passe `reproduction.intervalTicks`
    = 100, consentement = min trust réciproque ≥ `consentTrustThreshold` 0.6 (décision n°17),
    première paire qualifiante en ordre d'id (mère = moindre), enfant au point médian clampé
    (id = max+1), `MindState.Born` → mémoire + buts + `Born` ; `NewbornMinds` fusionnées dans le
    pipeline **après** la révision des groupes (ordre causal).
  - **`HERITAGE` (mécanismes fins, SYNE-063)** : `FuseTraits(a, b, settings, seed)` — parent
    exprimant sous dominance ∈ [0, 1], mutation déterministe `SplitMix64(newTraitId, seed, tick)`
    (+ clamps [0, 2]) ; `InheritMemory(double? salienceThreshold = null)` → défaut
    `agents.inheritance.salienceThreshold` (0.01).
  - **`Observabilité (additif, contrat 0.5.0 → engineVersion 0.5.0)`** : événements
    `group_formed`/`group_dissolved`/`group_decision` (bilan de vie `lifetime`/`success`,
    turnover `membersOut`/`membersIn`) et `agent_spawned` ({childId, motherId, fatherId, species, x, y});
    snapshot `groups[]` (`WorldSnapshot.GroupSnapshot`, camelCase).
  - **`Calibration`** : `communication.transmissionRange` défaut 20 → **55** (bornes [1, 70]
    indépendantes de la perception) — le scénario défaut forme un tapis de confiance.
  - Tests : +32 (210 → **242**), dont `GroupSystemTests` (9), `BirthSystemTests` (7),
    `GroupBirthDeterminismTests` (3), mécanismes fins d'héritage (7), validations de config (3),
    observabilité (3). Document `SOCIAL_NETWORK.md` créé ; checksum doré re-épinglé **0x864e72f57e1fe0d0**.
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

### Added
- **Jalon SYNE ph4 — Actions (SYNE-040 → SYNE-043, issues #20/#21/#22/#23, milestone ph4, engineVersion 0.3.0)** :
  - **Sous-système d'actions déclaratif (SYNE-040)** : nouveau `Simulation.Core/Actions/` — `ActionCatalog` (définitions issues de `agents.actions.catalog`, ordre stable de l'enum, viabilité contre les réserves), `ActionExecutor` (**une action atomique par entité par tick**, itération par identifiant croissant, aucun tirage PRNG), `ActionResult`/`ActionOutcome` (Executed/Blocked + deltas d'effets). Les effets (coûts énergie, récupérations, consommation de réserve) proviennent du catalogue déclaratif (CONFIGURATION §6.2).
  - **Déplacement + obstacles (SYNE-041)** : cible pseudo-aléatoire déterministe par (id, tick, désir) via finaliseur SplitMix64 (reproductible, 0 PRNG), **pas borné par la vitesse**, clamp aux limites du monde, **jamais de pas dans un obstacle** (rejet → sur place) ; coût par défaut = `actions.moveEnergyCost` pour les actions de déplacement.
  - **Besoins déclenchés ≥ 50 + réserves (SYNE-042)** : seuils par défaut **50/50/70** (`needs.hungerTriggerThreshold`/`thirstTriggerThreshold`/`fatigueTriggerThreshold`, décision n°4) ; nouvelles actions terminales **Eat/Drink** (`DesireKind.Eat = 7`, `Drink = 8`, append) résolues depuis SeekFood/SeekWater quand la réserve est disponible ; **réserves globales** `ResourceStocks` (`Food` 100 / `Water` 1000 / `Wood` 50, régénération décision n°4), consommées par Eat/Drink et exposées `SimulationLoop.Resources` ; « instruire » reporté (jalon ph7, ROADMAP).
  - **Déclencheur d'interruption centralisé (SYNE-043)** : `InterruptionTrigger` — unique point « action en cours interrompue ? » (faim critique > 85 → Eat si réserve, sinon SeekFood ; énergie < 10 → Rest), évalué à tout tick hors délibération, utilité + marge `utilityExcessMargin`, aucune consommation PRNG.
  - **Observabilité (additif, contrat V0.1 compatible)** : événement **`action_completed`** (1/entité/tick — action, `outcome`, deltas, réserve consommée) ; snapshot ajoute **`resources`** peuplées (type/quantity, DATA_MODEL §8) ; `engineVersion` → **0.3.0**.
  - **Config** : `SimulationOptionsValidator` chemins corrigés (`agents.actions.deliberation.*`/`interruption.*`, défaut ph3) + validation `needs.*TriggerThreshold` et catalogue complet.
  - **Déterminisme** : checksum de trajectoire **recalculé** (0xe8d69e462fc22df7 → **0xdfbc9a6c4a1d8122**, DETERMINISM.md §6) ; contrat « 0 tirage PRNG » préservé.
  - Tests : +30 (166 → **197**, dont 3 `Simulation.Console.Tests`). Build Release 0 warning / 0 erreur.

### Added
- **Jalon SYNE ph5 — Communication (SYNE-050 → SYNE-054, issues #24/#25/#26/#27/#28, milestone ph5, engineVersion 0.4.0)** :
  - **Sous-système de communication (SYNE-050)** : nouveau `Simulation.Core/Communication/` — `Message` (enum `MessageType` des 7 types, id `ulong` **SplitMix64 déterministe** — 0 PRNG global, `Relayed()` applique `hops+1` et `confidence × hopConfidenceDecay`), `CommunicationState` (file sortante bornée, file entrante bornée, `SentThisTick` partagé envois+relais, ensemble `RelaySeen` anti-boucle), `CommunicationSystem.Step` (**une passe par tick** : *diffusion* par identifiant croissant puis *relais*, `PERFORMANCE.md` — `batchCommunication`). Pulsation « Information » émise par toute entité sociable (`sociability ≥ 0.5`) percevant une entité vivante (`perceived-{id}#{x},{y}`) — partage public.
  - **Publicité + interception (SYNE-051)** : toute entité dans la portée (`transmissionRange`, **20 u. héritée — décision n°7**) et en **ligne de vue** (`LineOfSight.IsClear`) reçoit, **quelle que soit la cible** (décision n°8) ; réception capée `maxReceivesPerTick` (une réception écartée ne coûte rien).
  - **Coûts hérités configurables (SYNE-052)** : décision n°9 — envoi **0.5 + p×0.1**, réception **0.2 + p×0.05** (`sendEnergyCost`/`sendEnergyPayloadFactor`/`receiveEnergyCost`/`receiveEnergyPayloadFactor`), appliqués via `BodyNeeds.ExertEnergy`.
  - **Protocole de confiance (SYNE-053)** : décision n°10 — relais **× 0.9/hop** (`hopConfidenceDecay`), borné `maxHops` (défaut 2, câble public), `SenderId` d'origine préservé ; confiance effective = confiance du message × confiance du récepteur envers l'émetteur (`Relationships.TrustWith`, inconnue 0.0) + marquage `Interact` ; incompréhension (5 %) par tirage déterministe `SplitMix64(receiverId, messageId)`.
  - **Observabilité (additif, contrat V0.1 compatible)** : événements **`message_sent`**/**`message_received`** (`EventSensor`, `ObservabilityContract` 0.4.0) diffusés par `ObservabilityTickEmitter` après les boucles entités ; `engineVersion` → **0.4.0**.
  - **Config** : clés `communication.*` (transmissionRange, relayEnabled, maxHops, coûts, facteurs, hopConfidenceDecay) + validation (`SimulationOptionsValidator`, CONFIGURATION §6.3).
  - **Déterminisme** : checksum de trajectoire **recalculé** (0xdfbc9a6c4a1d8122 → **0x6aa2b2d87b32a8a5**, DETERMINISM.md §6) ; contrat « 0 tirage PRNG » préservé (id + incompréhension via SplitMix64 stable).
  - Tests : +13 (197 → **210**, dont 3 `Simulation.Console.Tests` ; `CommunicationSystemTests` : portée/LOS, interception, coûts n°9, confiance récepteur, caps, relais × 0.9/hop + maxHops, déterminisme). Build Release 0 warning / 0 erreur.

### Added
- **Jalon SYNE ph3 — Décision + Utilité (SYNE-030 → SYNE-033, issues #16/#17/#18/#19, milestone ph3, engineVersion 0.2.0)** :
  - **Formule d'utilité complète (SYNE-030)** : `U = (benefit − cost − risk) × confidence × personalityModifier + urgency` ; **bonus d'alignement ×1.2** (`deliberation.alignBonus`) quand l'action rejoint l'objectif courant ; seuils critiques **configurables** faim > 85 / énergie < 10 (`interruption.criticalHunger`/`criticalEnergy`, COGNITIVE_ARCHITECTURE §6) ; **hystérésis anti-oscillation** `actionSwitchMargin` (défaut 0.05, `ApplyActionSwitchMargin`) — ne changer d'action que si elle surpasse l'action courante de la marge.
  - **Sélecteur d'action + fréquence de délibération (SYNE-031)** : sélection déterministe par utilité maximale ; **fréquence configurable** (`deliberation.intervalTicks`, défaut 10 — décision n°14, LOD « 1 tick tous les 10 ») avec **holdover** de l'intention entre deux délibérations, décalée par entité (lissage de charge) ; **trace `DecisionRecord`** complète (tick, entité, scores par action, délibéré/interrompu — COGNITIVE_ARCHITECTURE §7).
  - **Interruptions d'actions (SYNE-032)** : `TryInterrupt` — besoin critique dont l'utilité surpasse de > `utilityExcessMargin` (10) l'action en cours reprend la main, y compris entre deux délibérations (décision n°15) ; décision interrompue tracée.
  - **Conflits de priorités (SYNE-033)** : `PriorityConflictResolver` — candidats à moins de `conflictTieMargin` (0.5) du maximum → **résolution probabiliste `p = drive × confidence / Σ`**, tirage **SplitMix64 déterministe sans PRNG** (décision n°22, aucun arbitraire d'ancienneté ; à force nulle, ordre du catalogue stable).
  - **Observabilité (additif, contrat V0.1 compatible)** : `decision_made` ajoute `deliberated`/`interrupted` (bool) par tick ; snapshot ajoute `engineVersion` (déterminsime : `0.2.0`, épinglé).
  - **Déterminisme** : checksum de trajectoire **recalculé** (0xab56603aedd578af → 0xe8d69e462fc22df7, DETERMINISM.md §6) — délibération à fréquence + interruptions altèrent volontairement la trajectoire ; contrat « 0 tirage PRNG » préservé.
  - Tests : +20 (146 → **166**, dont 2 `Simulation.Console.Tests`). Build Release 0 warning / 0 erreur.

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
| 21 septembre 2026 | Jalon SYNE ph2 : Mémoire intergénérationnelle + Croyances + Confiance | SYNE-020 → SYNE-022 |
| 21 septembre 2026 | Jalon SYNE ph3 : Décision + Utilité (engineVersion 0.2.0) | SYNE-030 → SYNE-033 |
| 22 septembre 2026 | Jalon SYNE ph4 : Actions déclaratives + réserves (engineVersion 0.3.0) | SYNE-040 → SYNE-043 |