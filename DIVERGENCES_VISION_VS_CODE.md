# Divergences entre la vision (SnapV0-1 + monographie) et le projet actuel

**Version 2 — document de référence exhaustif**
Date : 16 octobre 2026
Portée : SYNE, ECHOS, PRISM-LDK, Launcher, installateur, syne-mock, prototype SSE.
Méthode : chaque affirmation est **prouvée** (fichier:ligne, test, ou mesure exécutée) ou explicitement marquée **non vérifiée**.

> Ce document remplace la v1 (qui n'auditait que 3 divergences cœur) et **arbitre point par point** l'autre audit `ANALYSE_DIVERGENCES_FONDAMENTALES_ET_PLAN_REALIGNEMENT.md`, qui couvre beaucoup de catégories mais dont une majorité d'affirmations est **contredite par le code réel**.

---

## 0. Comment lire ce document

Chaque constat porte une **grade de preuve** :

| Grade | Signification |
| :-- | :-- |
| **A — Mesuré** | Observé en exécutant le moteur (commande fournie). |
| **B — Prouvé par le code** | Lecture de code avec `fichier:ligne` reproductible. |
| **C — Documentaire** | Issu du livre/Snap/prototype, non confronté au code. |
| **? — Non vérifié** | Hypothèse plausible, non prouvée ici. À traiter comme question ouverte. |

Une divergence n'est **retenue** que si elle est A ou B. Le reste est listé en « questions ouvertes » (§7).

---

## 1. L'objectif réel du projet (rappel synthetique, grade C)

**Question directrice** (`docs/monographie/01-introduction.md`) :

> Peut-on construire un environnement virtuel régi par des lois fondamentales, au sein duquel des entités autonomes peuvent produire des comportements et des structures qui n'ont pas été explicitement programmés ?

**Exigences dérivées** (`docs/monographie/02b-question-decomposee.md`) — chacune testable :

| Terme | Exigence opérationnelle |
| :-- | :-- |
| Environnement virtuel | Espace à conséquences : tout coûte, ressources limitées |
| Entités autonomes | Décision sans commande externe ; **besoin → objectif → délibération → action**, traçable |
| Lois fondamentales | Locales, identiques pour tous ; aucune règle ne lit `population`/`nb_groupes`/tendance globale |
| Non programmé | Émergence **faible**, traçable et rejouable : `action ← intention ← objectif ← besoin ← croyance ← mémoire ← perception` |

**Les 4 responsabilités et leurs interdits** (`docs/monographie/04b-projet-vision-architecture.md`) :

| Composant | Rôle | Interdit |
| :-- | :-- | :-- |
| SYNE | Décider — unique vérité du monde | Être mis en scène, influencé par rendu/analyse |
| ECHOS | Mesurer sans toucher | Modifier le monde, interpoler, inventer des données |
| PRISM | Montrer sans décider | Calculer des décisions, modifier l'état, extrapoler |
| Launcher | Archiver sans relayer | Relayer le flux WS, s'interposer entre SYNE et consommateurs |

**La chaîne causale cible**, formalisée côté prototype (`SSE-emergent-simulation/docs/V2/01-VISION.md` §4 et §6) :

```
Perceive → UpdateMemory → UpdateBeliefs → UpdateInternalState → UpdateNeeds
        → GenerateGoals → SelectAction → ExecuteAction → EmitEvents → CommunicationProcessing
```

Le prototype V2 pose que les croyances nourrissent les besoins/objectifs (§4 : `BELIEFS → INTERNAL STATE → NEEDS → GOALS`). **C'est précisément cette flèche qui manque dans le projet actuel** (§2, D1/D2).

---

## 2. Divergences fondamentales prouvées (le cœur)

### D1 — CRITIQUE : la chaîne objective n'a pas de cible ; le déplacement est un offset déterministe autour de soi

**Grade B.** Deux faits, deux fichiers.

1. **L'objectif ne porte aucune cible.** [`DesireFactory.cs`](syne/Simulation.Core/Cognition/DesireFactory.cs) définit :

```csharp
public readonly record struct Goal(DesireKind Kind, ulong BornTick)
{
    public ulong Age(ulong currentTick) => currentTick >= BornTick ? currentTick - BornTick : 0;
}
```

Un `Goal` = `(type de désir, tick de naissance)`. **Pas de position, pas de cible, pas de référence à une ressource, pas de référence à une croyance.** Confirmé côté PRISM : le DTO exposé au Blueprint est `FPrismSyneGoal { Kind, Age }` ([`PrismLdkTypes.h:111`](prism/LDK/Plugins/PrismLdk/Source/PrismLdk/Public/PrismLdkTypes.h#L111)) — la cible n'existe même pas dans le contrat de sortie.

2. **Le déplacement ignore toute ressource perçue ou mémorisée.** [`ActionExecutor.cs:422`](syne/Simulation.Core/Actions/ActionExecutor.cs#L422) :

```csharp
private void MoveTowardDeterministicTarget(Entity entity, DesireKind kind, ulong currentTick)
{
    (double dx, double dy) = DeterministicOffset(entity.Id.Value, currentTick, kind);
    double targetX = entity.Position.X + dx;
    double targetY = entity.Position.Y + dy;
    ...
}
```

`DeterministicOffset` ([ligne 493](syne/Simulation.Core/Actions/ActionExecutor.cs#L493)) est un **hachage de `(id, tick, kind)`** : l'agent se dirige vers un point pseudo-aléatoire **autour de sa position courante**. Le `kind` (SeekFood…) ne modifie que la *graine du hachage*, pas la destination réelle. Un agent « qui a faim » ne va pas vers la nourriture qu'il a perçue ou qu'il se rappelle : il va vers un point arbitraire, puis recommence.

3. **Corroboration mesurée (grade A).** Sur le moteur SYNE réel (`--export-stream`, profil de référence par défaut) : **déplacement net / chemin parcouru ≈ 3,1 %**. Un agent qui *recherche* une cible a un ratio élevé (le chemin va dans une direction) ; un ratio de quelques pourcents est la signature d'une **marche autour d'un point**. C'est cohérent avec le point 2 : le monde « bouge » sans que personne ne *cherche* rien.

**Conséquence pour la question directrice.** La chaîne exigée `action ← intention ← objectif ← besoin` s'arrête à « objectif = type ». Il n'y a **pas de `objectif(cible)`**, donc pas de comportement orienté, donc l'« émergence » observée est un **effet de surface** (les agents se déplacent, s'énervent, se regroupent) et non une poursuite d'états désirés. C'est **exactement** l'écart « théâtre vs microscope » — mais localisé et mesurable, pas philosophique.

**Ce qu'il faudrait** : `Goal` doit porter une **cible issue des croyances** (position crue d'une ressource), et `MoveToward` doit viser cette cible (chemin A\* déjà présent). Le prototype V2 le pose noir sur blanc (§4, §6).

---

### D2 — CRITIQUE : la communication ne met pas à jour les croyances (et la monographie affirme le contraire)

**Grade B.** [`CommunicationSystem.cs:259`](syne/Simulation.Core/Communication/CommunicationSystem.cs#L259) :

```csharp
private void Receive(MindState receiverMind, Message message, ulong receiverId)
{
    receiverMind.Needs.ExertEnergy(ReceiveCost(message));
    double effective = Math.Clamp(message.Confidence * receiverMind.Trust.TrustWith(message.SenderId), 0.0, 1.0);
    receiverMind.Trust.Interact(message.SenderId);
    bool understood = DeterministicDraw(receiverId, message.MessageId) >= _settings.IncomprehensionRate;
    receiverMind.Communication.RecordReceived(message, understood);
    _lastReceived.Add(new MessageReceived(..., effective, understood));
}
```

`effective` est **calculé puis utilisé uniquement pour le journal d'observabilité**. Le `Payload` du message **n'est jamais converti en croyance** : aucune écriture dans `Beliefs`. Preuve croisée : `Beliefs.ApplyEvidence` n'est appelé **que** par la perception — [`CognitionPipeline.cs:672`](syne/Simulation.Core/Cognition/CognitionPipeline.cs#L672) et [`:682`](syne/Simulation.Core/Cognition/CognitionPipeline.cs#L682) (via `StoreObservation`), jamais depuis la communication.

**Le livre dit pourtant l'inverse.** [`docs/monographie/05-syne/08-communication.md`](docs/monographie/05-syne/08-communication.md) §4 : « Le récepteur **combine les deux au moment de réviser ses croyances**. » Le code combine (`effective`) mais **ne révise pas**.

**Conséquence.** Toute la mécanique de rumeur / désinformation / propagation de croyances — cœur revendiqué de la « communication dégradable » — est **du transport sans effet cognitif**. On peut envoyer 10 000 messages : aucun agent ne saura rien de plus. Le morceau de §4 du chapitre VIII (dégradation, trust, incompréhension) est implémenté *sauf son point d'arrivée*, la révision.

---

### D3 — MAJEUR : les objectifs ne dérivent pas des croyances (ni la décision d'ailleurs)

**Grade B.** La signature est sans ambiguïté ([`DesireFactory.Generate`](syne/Simulation.Core/Cognition/DesireFactory.cs)) :

```csharp
public static IReadOnlyList<Goal> Generate(
    BodyNeeds needs, ulong tick, IEnumerable<DesireKind> activeKinds,
    ActionCatalog catalog, ResourceStocks stocks)
```

Entrées = **besoins physiologiques** + inventaire d'objectifs actifs + catalogue + réserves **globales** (`ResourceStocks` — la réserve mondiale, pas la connaissance locale). **Aucune entrée `Beliefs`.** Les objectifs naissent donc de l'état du corps, jamais de la représentation du monde de l'agent.

**Où les croyances sont réellement utilisées** (grade B, `grep` exhaustif hors tests) :
- `Beliefs.Tick` — décroissance temporelle ([`CognitionPipeline.cs:161`](syne/Simulation.Core/Cognition/CognitionPipeline.cs#L161)) ;
- `Beliefs.ApplyEvidence` — perception seule (`:672`, `:682`) ;
- `OrderedByFact()` — observabilité, persistance, et **affinité de groupe** ([`GroupSystem.cs:428`](syne/Simulation.Core/Social/GroupSystem.cs#L428) : deux agents se rapprochent s'ils partagent une croyance ≥ 0,5 de confiance).

Donc les croyances servent à : se regrouper, s'exporter, se sauvegarder. **Elles ne servent jamais à décider où aller ni quoi chercher.** C'est le trou D1↔D3 : la perception et la mémoire alimentent bien les croyances, mais la flèche `croyance → objectif` est absente du code.

---

### D4 — MAJEUR : deux surengagements documentaires (le livre promet plus que le code)

**Grade B/C.** Deux endroits précis où la monographie (document de référence de la vision) décrit un mécanisme que le code n'a pas :

| # | Ce que dit le document | Ce que fait le code | Réf. |
| :-- | :-- | :-- | :-- |
| D4a | Le récepteur révise ses croyances avec confiance×trust | calcule sans écrire de croyance | `08-communication.md` §4 vs `CommunicationSystem.cs:263` |
| D4b | `Goal` = état recherché, cible du déplacement | `Goal` = (Kind, BornTick), cible absente | `05-syne/02-agents-cognition.md` vs `DesireFactory.cs` |

Ces deux écarts sont **le même défaut** vu de deux côtés (le livre et le code). Ils doivent être traités comme **un seul chantier** : rétablir la flèche croyance → objectif → cible → mouvement, et faire que la communication alimente les croyances.

---

## 3. Arbitrage du document de l'autre agent (claim par claim)

Le document `ANALYSE_DIVERGENCES_FONDAMENTALES_ET_PLAN_REALIGNEMENT.md` couvre **les bonnes catégories** (philosophie, architecture, cognition, monde, reproductibilité, tests, plan) mais il est rédigé en **hypothèses non testées** (« risque que… », « doit être vérifié », « tendance à… »). Confronté au code, voici le verdict.

| Claim | Affirmation | Verdict | Preuve |
| :-- | :-- | :-- | :-- |
| D-P1/P2/P3/P4 | Dérive philosophique « simulateur » vs « dispositif » | **Confirmée (mais imprécise)** | Reformulée en D1/D2/D3 : la dérive est **une boucle cognitive absente**, pas un état d'esprit. Grade B. |
| D-A1 | « Fuite d'autorité SYNE » | **Non démontrée** | Aucun composant n'écrit dans SYNE. ECHOS est en lecture, PRISM en lecture. Grade ? |
| D-A2 | ECHOS interpole, remplit les séries | **RÉFUTÉE** | ECHOS ne comble **jamais** un trou : `measured`/provenance introduits (migration v5, [`sqlite.py:35`](echos/echos/storage/sqlite.py#L35)), `provenance()` retire les fenêtres absentes ([`analysis/__init__.py:59`](echos/echos/analysis/__init__.py#L59)). |
| D-A3 | PRISM lisse / prédit / extrapole | **RÉFUTÉE** | `grep -i "lerp\|interp\|extrapol\|predict\|smooth"` sur le plugin : **0 résultat**. Le plugin ne fait que traduire des DTO. |
| D-A5 | Launcher relaie le flux WS | **NON PROUVÉE** | Une seule occurrence de « WebSocket » dans le Launcher, dans un **commentaire** ([`OrchestrationFacade.cs:841`](launcher/Launcher.App/Composition/OrchestrationFacade.cs#L841)) expliquant que la diffusion est exposée pour le plugin. Pas de proxy trouvé. |
| D-C1 | Perception trop large / accès global | **RÉFUTÉE** | `PerceptionSystem` passe par `_world.Grid.QueryCircle(...)` + rayon + ligne de vue ([`Perception.cs:118,139`](syne/Simulation.Core/Perception/Perception.cs#L118)). |
| D-C2 | « Besoin → action direct » | **RÉFUTÉE dans la forme, confirmée dans l'esprit** | Il existe bien `DesireFactory(needs) → Goal → UtilityEvaluator → PriorityConflictResolver → intention`. Mais l'objectif est **dégénéré** (pas de cible, pas de croyance) → voir D1/D3. |
| D-C3/C4 | BDI mal séparé | **RÉFUTÉE** | `Beliefs`, `Desires`/`Goal`, `Intention` sont des objets distincts (`MindState`), testés (`BeliefTests`, `MindStateRestoreDecisionTests`, `UtilityEvaluatorTests`). |
| D-C6 | Mémoire sans décroissance | **RÉFUTÉE** | Décroissance exponentielle réelle : `salience = salience₀ × exp(−decayRate × age)`, taux par catégorie 0,01/0,005/0,002 ([`Memory.cs:63,155`](syne/Simulation.Core/Cognition/Memory.cs#L63)). SYNE-013. |
| D-C7 | Confiance statique | **RÉFUTÉE** | `Trust.TrustWith` + `Trust.Interact` par message, avec décroissance sans interaction ([`CommunicationSystem.cs:265`](syne/Simulation.Core/Communication/CommunicationSystem.cs#L265)). |
| D-R1 | Déterminisme non vérifié | **RÉFUTÉE** | Tests dédiés : `DeterminismRegressionTests.GoldenChecksum_IsPinned` ([`:111`](syne/Simulation.Core.Tests/Simulation/DeterminismRegressionTests.cs#L111)), `ScaleTargetsTests.ScaleChecksum_IsBitForBitReproducible`, `GroupBirthDeterminismTests`, `Ph10DeterminismBaselineTests`, `AdrDeterminismNeutralTests`, `ObservabilityNonIntrusionTests`. |
| D-R2 | État PRNG incomplet en sauvegarde | **RÉFUTÉE** | Le snapshot sérialise l'état **xoshiro256\*\* (4×ulong)** et la restauration n'ajoute ni ne retire de tirage ([`SimulationSnapshot.cs:36`](syne/Simulation.Core/Persistence/SimulationSnapshot.cs#L36), `SimulationSnapshotRestorer`). |
| D-R3 | Absence de DecisionTrace | **PARTIELLEMENT CONFIRMÉE** | Pas de classe `DecisionTrace`. L'observabilité publie décision/intention/action, mais la **décomposition d'utilité** en sortie n'est pas garantie. À confirmer sur le contrat d'observabilité. Grade ? |
| D-R4 | Provenance absente dans ECHOS | **RÉFUTÉE** | Provenance par tick persistée (`tick_metrics.measured`, API qui publie provenance avec les valeurs, `test_metrics_expose_provenance_alongside_values`). |
| D-R5 | Gap detection inexistant | **RÉFUTÉE** | `test_metrics_publish_gaps_and_per_tick_provenance` ([`test_api_routes.py:401`](echos/echos/tests/test_api_routes.py#L401)). Les trous sont publiés. |
| D-R6 | Archivage `.livexp` non scellé | **NON VÉRIFIÉ** | À trancher sur le contrat Launcher. Grade ? |
| D-R7 | Config non centralisée / pas de `config_hash` | **PARTIELLEMENT CONFIRMÉE** | Config **centralisée** (`SimulationOptions` + `ConfigLoader` + validateur). Mais **pas de `config_hash`** : seule `EngineVersion = "0.15.0"` est publiée ([`ObservabilityContract.cs:95`](syne/Simulation.Core/Observability/ObservabilityContract.cs#L95)). |
| D-M1/M2/M3/M4/M5/M6/M7 | Lois locales, ordre causal, coûts, comm. parfaite, ressources, timers, groupes | **MÉLANGÉ** | Groupes : bottom-up par affinité de croyances (`GroupSystem.cs:428`) → conforme. Coûts/ressources/ordre causal : **non mesurés ici**. Grade ? |

**Bilan de l'arbitrage.** Sur ~14 claims vérifiables, **~8 sont réfutés par le code**, 3 sont partiels, 3 restent non vérifiés. **Aucune** des divergences que j'ai prouvées (D1, D2, D3) n'est correctement identifiée par l'autre document : il décrit un projet hypothétique, pas celui-ci. Sa valeur réelle est **sa couverture** (PRISM, Launcher, tests, méthodologie, plan) — pas son diagnostic.

---

## 4. État réel par composant (couverture exhaustive)

### 4.1 SYNE — moteur décisionnel

| Sous-système | État réel | Conforme ? | À faire |
| :-- | :-- | :-- | :-- |
| Perception | Locale : grille + rayon + ligne de vue + falloff de confiance | **Oui** | — |
| Mémoire | Décroissance exponentielle par catégorie, seuil de saillance | **Oui** | — |
| Croyances | Store distinct, révision bayésienne (`ApplyEvidence`), décroissance, expiration | **Oui** | Le rendre **sert de source d'objectifs** (D3) |
| Besoins | `BodyNeeds`, seuils, `IsTriggered` | **Oui** | — |
| Objectifs | `Goal(Kind, BornTick)` — **sans cible** | **NON (D1)** | Ajouter une cible issue des croyances |
| Utilité | `UtilityEvaluator` multicritère | **Oui** | Vérifier la décomposabilité exposée |
| Conflit | `PriorityConflictResolver` déterministe | **Oui** | — |
| Intention | Engagement explicite | **Oui** | — |
| Exécution | Actions + A\* + `PathCache` présents, mais `SeekX` → offset déterministe | **NON (D1)** | Viser la cible de l'objectif via A\* |
| Communication | Transport complet (public, portée 55, LOS, incompréhension 5 %, relais 2 sauts, trust) | **Partiel (D2)** | Consommer le payload → réviser les croyances |
| Groupes | Bottom-up, affinité de croyances, dissolution | **Oui** | — |
| Persistance | Snapshot bit-à-bit, PRNG xoshiro256\*\* sérialisé, restore sans tirage | **Oui** | Ajouter `config_hash` (D-R7) |
| Déterminisme | Checksums dorés pinnés + tests d'échelle | **Oui** | Re-pinner après D1/D2/D3 |

### 4.2 ECHOS — observatoire

**Verdict : le composant le plus mûr du dépôt.** Lecture seule pour la mesure (l'ingestion consomme les flux, n'écrit rien dans SYNE), **provenance par tick**, **détection de trous**, 7 moteurs d'analyse livrés (`emergence`, `cognitive_diversity`, `social_complexity`, `group_dynamics`, `information_propagation`, `goal_convergence`, `resource_sustainability`) + `causal`, `reproducibility`, `calibration`, `feedback_loop_detector`. API + stockage SQLite/Parquet.

Seule réserve **documentaire** : l'**indice de viabilité dynamique** (DVI, 7 composantes, noyau géométrique) est **spécifié mais non livré** ([`06-echos/07-indice-de-viabilite-dynamique.md`](docs/monographie/06-echos/07-indice-de-viabilite-dynamique.md) §6 : « pas encore livré »). Ce n'est pas une divergence, c'est un jalon déclaré.

> Corollaire important : **les divergences D1/D2/D3 sont précisément ce qu'ECHOS devrait mesurer et ne mesure pas** (à cause de la structure de sortie). Une fois D1/D2/D3 corrigées, ECHOS pourra exposer la chaîne causale — c'est écrit dans le prototype V2 §8.2 (« Analyzer doit exposer la chaîne causale complète »).

### 4.3 PRISM-LDK — rendu

**Verdict : conforme.** Couche mince, deux transports (WS 5180 lecture / HTTP 5181 contrôle), événements Blueprint, aucun calcul de décision, **aucun `lerp/interp/extrapolate/predict`**. Il expose même des DTO de croyances (`FPrismSyneBelief`) et d'objectifs (`FPrismSyneGoal`) — donc il *attend* des décisions et des croyances, ce que le moteur fournit… sauf des objectifs **avec cible** (D1).

### 4.4 Launcher — orchestration/archivage

Présent (13 projets), supervise les processus, expose le flux pour le plugin ([`OrchestrationFacade.cs:841`](launcher/Launcher.App/Composition/OrchestrationFacade.cs#L841)). **Interdit « sans relayer » : non prouvé, non infirmé** (grade ?). Scellement d'archive et `config_hash` non confirmés — à trancher sur `docs/launcher/INTEGRATION_CONTRACT.md`.

### 4.5 Installateur / syne-mock / prototype

- **Installateur** : pas de code trouvé (spécification seule). Grade ?.
- **syne-mock** : réimplémentation JS du modèle, avec `createDeterministicOffset` — **il reproduit le défaut D1** (déplacement par offset, [`deterministic-random.js`](syne-mock/src/simulation/deterministic-random.js)). Utile comme bac à sable de mesure.
- **Prototype SSE** : V1 = Utility AI (monde entièrement observable), V2 = spec BDI complète avec croyances → objectifs. **Source de conception pour D1/D3** (`docs/V2/01-VISION.md` §4, §6), pas source de vérité.

---

## 5. Ce qu'il ne faut PAS refaire (pour éviter le « tout recommencer »)

Prouvé conforme aujourd'hui (grade A/B) :

- Perception locale stricte (grille + rayon + LOS).
- Mémoire à décroissance exponentielle, catégorisée.
- Croyances comme store séparé, révisables, persistées.
- Déterminisme bit-à-bit avec checksums dorés et tests d'échelle.
- Persistance complète (PRNG xoshiro256\*\* sérialisé, restore sans tirage).
- ECHOS : lecture seule, provenance, détection de trous, 11 moteurs d'analyse.
- PRISM-LDK : couche mince sans logique.
- Transport public/local/dégradable de la communication (le canal est correct — c'est l'**effet cognitif** qui manque).
- Groupes bottom-up par affinité de croyances.

**Le réalignement n'est donc pas une réécriture** : c'est **rétablir trois flèches manquantes** dans une architecture par ailleurs saine.

---

## 6. Plan de réalignement séquencé

### Étape 0 — Décision de contrat (bloquant)
Changer `Goal` et la révision par communication **change la trajectoire du monde** ⇒ `engineVersion` **MINOR** et **re-calcul des checksums dorés**. À arbitrer avant toute écriture.

### Étape 1 — `Goal` porte une cible issue des croyances (D1 + D3)
- Étendre `Goal` : `Kind`, `BornTick`, **`Target`** (position crue), `Source` (croyance id).
- `DesireFactory.Generate` prend `Beliefs` en entrée ; la cible vient de la croyance la plus saillante pertinente.
- `ActionExecutor.MoveToward*` vise `Goal.Target` via A\* (déjà là) au lieu de `DeterministicOffset`.
- Test : « besoin déclenché + croyance de nourriture à (x,y) ⇒ l'agent s'en approche » (échec aujourd'hui).

### Étape 2 — La communication alimente les croyances (D2)
- Dans `Receive`, convertir le `Payload` compris en `ApplyEvidence` pondéré par `effective`.
- Plafonner la révision par tick (éviter la contamination instantanée).
- Test : « A émet une observation, B à portée ⇒ la croyance de B est mise à jour avec la confiance attendue ».

### Étape 3 — Aligner la documentation (D4)
Corriger `05-syne/02-agents-cognition.md` et `08-communication.md` **après** le code, pour que le livre décrive le code réel et non l'inverse.

### Étape 4 — Reproductibilité & traçabilité (claims partiels)
- Ajouter `ConfigHash` (config normalisée triée) au snapshot et à l'observabilité (D-R7).
- Confirmer/exposer la décomposition d'utilité en sortie d'observabilité (`DecisionTrace`) (D-R3).
- Trancher « sans relayer » (Launcher) et scellement `.livexp` (D-A5, D-R6).

### Étape 5 — Preuve méthodique
- Re-pinner les goldens ; vérifier que le déplacement net/chemin passe de ~3 % à un régime orienté.
- Test de non-régression : échoue si `Goal` redevient sans cible ou si `MoveToward` redevient un offset.

---

## 7. Questions ouvertes (grade ? — à trancher, non prouvées ici)

1. Launcher relaie-t-il réellement le flux WS ? (lire `docs/launcher/INTEGRATION_CONTRACT.md` en détail)
2. L'archive `.livexp` est-elle scellée (hash + immuable) ?
3. La décomposition d'utilité est-elle exposée par l'observabilité (DecisionTrace) ?
4. L'installateur existe-t-il en code, ou seulement en spécification ?
5. Les coûts de déplacement/socialisation créent-ils une pression réelle (claims D-M3/M5) ?

---

## 8. Annexe — commandes de vérification

```bash
# D1 : l'objectif n'a pas de cible
grep -n "record struct Goal" syne/Simulation.Core/Cognition/DesireFactory.cs
grep -n "MoveTowardDeterministicTarget\|DeterministicOffset" syne/Simulation.Core/Actions/ActionExecutor.cs

# D2 : la communication ne révise pas les croyances
grep -rn "ApplyEvidence" syne/Simulation.Core --include=*.cs   # perception uniquement

# Confirme : perception locale, mémoire à décroissance, perception-only evidence
grep -n "QueryCircle\|LineOfSight" syne/Simulation.Core/Perception/Perception.cs
grep -n "exp(-\|DecayRate" syne/Simulation.Core/Cognition/Memory.cs

# Réfutations de l'autre agent
grep -rln -i "lerp\|interp\|extrapol\|predict\|smooth" prism/LDK/Plugins/PrismLdk/Source   # vide
grep -rn "measured" echos/echos/storage/sqlite.py | head                                   # provenance

# Déterminisme (déjà testé)
dotnet test syne/Simulation.Core.Tests --filter DeterminismRegressionTests
```

**Sources** : `LIVEX-Monographie_SnapV0-1.md`, `docs/monographie/**`, `SSE-emergent-simulation/docs/V2/01-VISION.md`, `ANALYSE_DIVERGENCES_FONDAMENTALES_ET_PLAN_REALIGNEMENT.md`.
