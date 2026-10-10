# TICK_QUANTITIES — Inventaire des quantités par tick (classes A / B / C)

**Composant** : SYNE
**Statut** : [PROPOSITION — classement soumis à validation, spec « Profil gameplay PRISM » §10.1 (PR 0)]
**Dernière mise à jour** : 10 octobre 2026
**Dépend de** : `CONFIGURATION.md`, `DETERMINISM.md`, ADR-017
**Source** : spec « Profil gameplay PRISM — temps, échelle, vitesse » §3 (classement A/B/C)

---

## 1. Rappel des classes

`dt = simulatedSecondsPerTick / 60` (minutes simulées par tick ; 1,0 au défaut,
1/12 pour le profil `prism` — ADR-017).

| Classe | Règle | Exemple |
| :-- | :-- | :-- |
| **A** — processus continus du temps simulé | multipliés par `dt` (additifs) ; décroissances multiplicatives `facteur ^ dt` ; périodes `période_ticks = période_min / dt` | taux de besoins, régénérations, décroissances mémoire/croyances/confiance |
| **B** — cadences mécaniques de calcul | **inchangées, en ticks** | délibération, rotation de perception, budgets de communication, sauvegarde, A*, portées |
| **C** — événements / effets par action | **inchangés** | `energyRecovery` Eat/Drink, bonus/pénalités de confiance ponctuels |

Le **pas de déplacement n'est pas multiplié par `dt`** (ActionExecutor.cs:432-475 :
1 unité × trait `speed` par tick, vitesse apparente = `speed × k × ticksPerSecond`) —
c'est la vitesse visuelle qui fixe l'échelle spatiale (décision D3 de la spec).

Références : `syne/Simulation.Core/…`, lignes relevées sur la branche `develop`
(commit `0c99c24` + ADR-017).

## 2. Classe A — mise à l'échelle par `dt` (implémentée, ADR-017)

| Quantité | Clé de config | Application (fichier:ligne) | Traitement |
| :-- | :-- | :-- | :-- |
| Taux de faim | `agents.needs.hungerRate` | `Cognition/BodyNeeds.cs:99` (`Advance`, appelé `Cognition/CognitionPipeline.cs:226`) | × `dt` |
| Taux de soif | `agents.needs.thirstRate` | `Cognition/BodyNeeds.cs:100` | × `dt` |
| Taux de fatigue | `agents.needs.fatigueRate` | `Cognition/BodyNeeds.cs:101` | × `dt` |
| Dérive sécurité | `agents.needs.safetyDriftRate` | `Cognition/BodyNeeds.cs:102` | × `dt` |
| Dérive social | `agents.needs.socialDriftRate` | `Cognition/BodyNeeds.cs:103` | × `dt` |
| Dérive curiosité | `agents.needs.curiosityDriftRate` | `Cognition/BodyNeeds.cs:104` | × `dt` |
| Coût de déplacement | `agents.actions.moveEnergyCost` | défaut des actions `movement` — `Actions/ActionCatalog.cs:91` ; payé `Actions/ActionExecutor.cs:118-127` ; pèse aussi sur l'utilité `Cognition/UtilityEvaluator.cs:167-173` | × `dt` (décision D3 : **obligatoire** — sans cela, un cycle de besoin paie 12× plus d'énergie à R = 30) |
| Gain de repos (énergie) | `agents.actions.restEnergyGain` | `Actions/ActionCatalog.cs:94` | × `dt` |
| Récupération de repos (fatigue) | `agents.actions.restFatigueRecovery` | `Actions/ActionCatalog.cs:95` | × `dt` |
| Régénération des réserves | `resources.*.regenerationRate` | `World/ResourceStocks.cs:90-98` (`ApplyLifecycle`, appelé par `Simulation/SimulationLoop.cs`) | × `dt` |
| Période de dégradation | `resources.*.degradationTick` | `World/ResourceStocks.cs:99-104` (modulo de tick, perte = régénération cumulée de la période) | ` période / dt ` ticks |
| Décroissance mémoire (3 catégories) | `agents.memory.observationDecayRate` / `eventDecayRate` / `interactionDecayRate` | `Cognition/Memory.cs:91-95` (`DecayRateFor`), `Memory.cs:151` (`salience = exp(−decayRate × âge_ticks)`) | × `dt` (taux effectif par tick ; même durée d'oubli en temps simulé) |
| Expiration des croyances | `agents.beliefs.expiryTicks` | `Cognition/Belief.cs:147,152` (`tick + settings.ExpiryTicks`) | ` période / dt ` ticks |
| Décroissance des croyances | `agents.beliefs.timeDecayPerTick` | `Cognition/Belief.cs:187` (`× settings.TimeDecayPerTick`), appelé `Cognition/CognitionPipeline.cs:155` | `^ dt` (court-circuit `dt == 1` bit-exact) |
| Décroissance de confiance | `agents.trust.decayFactorPerTick` | `Cognition/Relationships.cs:113` (`× Settings.DecayFactorPerTick`), appelé `Cognition/CognitionPipeline.cs:156` | `^ dt` |
| Durée d'une saison | `world.seasons.seasonLengthTicks` | `Configuration/SimulationOptions.cs:590` (`SeasonSettings.At`) | ` période / dt ` ticks — 360 min → 4 320 ticks à 5 s/tick |

### Quantité A déclarée mais **morte** (à trancher)

| Quantité | Clé | Constat | Recommandation |
| :-- | :-- | :-- | :-- |
| Décroissance de confiance communication | `communication.trustDecay` (SimulationOptions.cs:434) | **Aucune lecture dans le moteur** — seule la déclaration et le doc existent (le pipeline utilise `agents.trust.decayFactorPerTick`). | Classer A si réactivée ; sinon retirer de la config (dette). Non implémentée (inerte). |

## 3. Classe B — cadences mécaniques (inchangées, en ticks)

| Quantité | Clé de config | Application |
| :-- | :-- | :-- |
| Intervalle de délibération | `agents.actions.deliberation.intervalTicks` | `Cognition/CognitionPipeline.cs:396` (`ShouldDeliberate`) |
| Rotation de perception | `agents.perception.rotationInterval` | `Perception/PerceptionSystem.cs:90,96` |
| Revue des groupes (+ TTL de l'objectif collectif) | `groups.reviewIntervalTicks` | `Social/GroupSystem.cs:102`, `Cognition/CognitionPipeline.cs:173` |
| Budgets de communication | `communication.maxSendsPerTick` / `maxReceivesPerTick` | `Communication/CommunicationSystem.cs` (files bornées par tick) |
| Sauvegarde automatique | `simulation.autoSaveEveryNTicks` | `Simulation/SimulationLoop.cs:65` (autsave handler) |
| Filet de saillance | `agents.salience.forcedReconsiderationTicks` | `Cognition/CognitionPipeline.cs` (délai de reconsidération forcée) |
| Plafond d'expansion A* / cache | `agents.pathfinding.maxExpansionCells` / `cacheCapacity` / `cellSize` | `Navigation/` (coût de calcul, pas de temps) |
| Portées spatiales | `agents.perception.radius`, `communication.transmissionRange`, `reproduction.mergeRange`, `agents.perception.spatialCellSize` | distances en unités monde, sans dimension temporelle |
| Pas de déplacement | trait `speed` (1 unité/tick) | `Actions/ActionExecutor.cs:432-475` — **décision assumée** (§1), voir D3 |

### Non classés par la spec (§11) — proposition du PR 0, **soumis à validation**

| Quantité | Clé / constante | Application | Proposition |
| :-- | :-- | :-- | :-- |
| Tentative de fusion | `reproduction.intervalTicks` (= 100) | `Population/BirthSystem.cs:82` | **B** : cadence de tentative (une tentative tous les N ticks), pas un processus continu. Si l'on veut une « rareté » en minutes simulées, classer A — à trancher. |
| Plafond de naissances | `reproduction.maxBirthsPerTick` | `Population/BirthSystem.cs` (garde-fou) | **B** : garde-fou mécanique par tick d'horloge. |
| Expiration des engagements | `agents.commitments.expiryTicks` (= 100) | `Cognition/CognitionPipeline.cs:450` | **A probable** (durée de validité temporelle, homologue de `beliefs.expiryTicks`) — **non implémentée** en attendant validation (engagements désactivés par défaut, trajectoire de référence inchangée). |
| Fenêtre d'objectif récent | constante `100` (`intention.Age < 100`) | `Cognition/CognitionPipeline.cs:537` (`ActiveGoals`) | **B codée en dur** — constante non configurée ; à extraire en config si elle doit devenir temporelle (décision ouverte). |
| Cooldown de demande d'aide | constante `20` (`% 20 != 0`) | `Cognition/CognitionPipeline.cs:408` (`EnqueueHelpRequest`) | **B codée en dur** — même réserve. |

## 4. Classe C — effets ponctuels par action (inchangés)

| Quantité | Clé de config | Application |
| :-- | :-- | :-- |
| Effets explicites du catalogue | `agents.actions.catalog.entries.*` : `energyCost` eat/drink 0,2, `hungerRecovery`/`thirstRecovery` 30, `energyRecovery` eat 2,0 / drink 1,0, `reserveConsumption` | `Actions/ActionCatalog.cs:91-97`, `Actions/ActionExecutor.cs` — un effet est consommé **par exécution**, quel que soit le pas temporel |
| Bonus/pénalités de confiance | `agents.trust.truthBonus` / `liePenalty` / `commitmentBonus` / `commitmentPenalty` | `Cognition/Relationships.cs` (`Reward`/`Penalize`) |
| Révision de croyance | `agents.beliefs.updateStrength` / `maxChangePerSnap` / `alignBonus` / `conflictPenalty` / `expiredCap` | `Cognition/Belief.cs` (`ApplyEvidence`, `PenalizeConflicting`) |
| Seuils d'interruption | `agents.actions.interruption.*` (niveaux 0-100) | `Cognition/InterruptionTrigger` |
| Bonus de groupe | `groups.sharedBeliefBonus` / `goalAlignmentBonus` / `consensusThreshold` / `trustThreshold` | `Social/GroupSystem.cs` |
| Communication par message | `communication.incomprehensionRate` / `hopConfidenceDecay` | `Communication/CommunicationSystem.cs` (par message/saut, pas par temps) |
| Mortalité | `agents.life.deathEnergyThreshold` (niveau) | `Population/DeathSystem.cs` |
| Héritage | `agents.inheritance.*` (dominance, mutation) | `Cognition/Inheritance.cs` (par naissance) |
| Seuils de déclenchement | `agents.needs.*TriggerThreshold` (niveaux 0-100) | `Cognition/BodyNeeds.cs` — des **niveaux**, pas des taux |

## 5. Application dans le code (ADR-017)

Les valeurs effectives par tick sont calculées **une fois au démarrage du run**,
de façon déterministe, par `Simulation.Core/Configuration/TemporalScale.cs` —
le pipeline cognitif construit ses options effectives (`CognitionPipeline` ctor,
`EffectiveOptions`), la boucle les siennes (ressources, saisons, `Clock`).
À `dt == 1` (profil par défaut et `reference`), chaque opération retourne
l'instance d'entrée : **aucune arithmétique, trajectoire bit-à-bit inchangée**
(pin contractuel ADR-016 ; vérifié par la suite dorée `DeterminismRegressionTests`,
`Ph10DeterminismBaselineTests` et les goldens de flux).

## 6. Points restés ouverts

1. Validation du classement B des non-classés du §3 (reproduction, engagements,
   constantes codées en dur) avant toute modification supplémentaire.
2. Sortie de `communication.trustDecay` (mort) ou réactivation documentée.
3. Densité sociale (spec §9.1) et terrain spatialisé : hors de cet inventaire.
