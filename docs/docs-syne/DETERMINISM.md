# DETERMINISM.md

**Composant** : SYNE
**Statut** : [STABLE]
**Dernière mise à jour** : 22 septembre 2026
**Dépend de** : `PERSISTENCE.md`, `CONFIGURATION.md`
**Source Monographie** : §2.3.4, §3.6.2–3.6.4 (seed, PRNG, sérialisation), ADR-006, §7.5 (validation)

---

## 1. Définition

**Déterminisme bit-à-bit** : deux exécutions identiques (même seed, même config, même version moteur, même état initial) produisent **exactement** la même trajectoire — mêmes événements, mêmes décisions, mêmes états.

## 2. Les 4 piliers d'une exécution reproductible

| Pillier | Contenu | Source |
| :-- | :-- | :-- |
| **Seed** | Entier 64 bits initialisant le PRNG | §3.6.2 |
| **Configuration** | `config.json` : paramètres du monde | §3.6.2 |
| **Version moteur** | `engineVersion` — garantit des règles identiques | §3.6.2 |
| **État initial** | positions, ressources, entités | §3.6.2 |

## 3. Le PRNG : xoshiro256\*\*

- **Algorithme** : xoshiro256\*\* (`prng_engine = xoshiro256**`), rapide (l'un des plus rapides en 64 bits), qualité éprouvée (BigCrush de TestU01), état compact (4 × 64 bits = 256 bits).
- **Initialisation** : **splitmix64(seed)** — dérive une séquence reproductible à partir de la seed.
- **Règle stricte** : `System.Random` est **interdit** — pas stable entre versions .NET, état non directement sérialisable.
- **Reproductibilité bit-à-bit** garantie si seed identique.

(ADR-006, §3.6.3)

## 4. Sérialisation de l'état RNG

- L'état complet du PRNG (**4 × ulong**) est sérialisé dans `tick_states.rng_state`.
- Une simulation sauvegardée au tick 1000, rechargée puis poursuivie, produit exactement les mêmes événements qu'une exécution ininterrompue (§3.6.4).

## 5. Ordre causal strict

- Les sous-systèmes sont exécutés dans un **ordre causal strict** (boucle 15 étapes, `SIMULATION_LOOP.md`).
- Aucune exécution parallèle ne doit introduire de non-déterminisme : la parallélisation du prototype (`parallelPerception`, etc.) doit rester déterministe (agrégation d'ordre fixe).

## 6. Vérification (tests)

| Test | Objectif |
| :-- | :-- |
| `BitIdenticalPersistenceTest` | Reprise bit-à-bit après sauvegarde/charge |
| Checksum de run | Hash de trajectoire pour détecter toute divergence |
| Matrices seeds × configs (Annexe I.4) | Pour chaque seed ∈ [12345, 67890, 99999, 42, 999], exécuter 1000 ticks, valider checksum |
| `DeterminismRegressionTests` (SYNE-015) | Hash **épinglé** de la trajectoire perception+décision (seed 12345, 25 ent., 200 ticks) + égalité bit-à-bit entre deux runs identiques et différence entre seeds |
| Auto-égalité (SYNE-015) | Deux exécutions (même seed/config) → chaîne de perception identique |

**Contrat V0.1 (jalon SYNE ph1)** : le pipeline cognitif (perception → décision) ne consomme **aucun** tirage du PRNG — l'avance du générateur reste **1 tirage/tick** ; les cibles de déplacement dérivent d'un déterminisme propre (hash SplitMix64 stable, sans passerelle RNG).

**Jalon SYNE ph3 (engineVersion 0.2.0)** : la délibération à fréquence configurable (décision n°14), les interruptions par besoin critique (décision n°15), la sélection avec hystérésis et le tirage de conflit de priorités (décision n°22) restent **0 tirage PRNG** — le tirage probabiliste de `PriorityConflictResolver` dérive d'un hash SplitMix64 de (entityId, tick, kinds). Checksum de la trajectoire recalculé (0xab56603aedd578af → **0xe8d69e462fc22df7**).

**Jalon SYNE ph4 (engineVersion 0.3.0)** : le catalogue d'actions déclaratif, l'exécuteur atomique et le déclencheur d'interruption centralisé restent **0 tirage PRNG** — la cible de déplacement dérive du hash SplitMix64 de (id, tick, désir) (ActionExecutor.DeterministicOffset), l'itération reste par identifiant croissant, le pas est clampé au monde et **jamais posé dans un obstacle** (rejet → sur place, SYNE-041). Checksum de la trajectoire recalculé (0xe8d69e462fc22df7 → **0xdfbc9a6c4a1d8122**).

**Jalon SYNE ph5 (engineVersion 0.4.0)** : le sous-système de communication reste **0 tirage PRNG** — les identifiants de message dérivent du hash SplitMix64 de (émetteur, tick, séquence) et l'incompréhension (5 %) de `SplitMix64(receiverId, messageId)` (CommunicationSystem, COMMUNICATION_PROTOCOL.md §3/4) ; la passe par tick (diffusion par identifiant croissant puis relais, borné à `maxHops`) préserve l'ordre causal. Checksum de la trajectoire recalculé (0xdfbc9a6c4a1d8122 → **0x6aa2b2d87b32a8a5**).

**Jalon SYNE ph6 (engineVersion 0.5.0)** : le réseau social reste **0 tirage PRNG** — les liens de cohésion découlent de l'ordre trié des paires d'entités (id croissant), les composantes d'un union-find à racine minimale, le leader du max de confiance entrante (tie-break id min), la mutation d'héritage de `SplitMix64(nouvelTraitId, seed, tick)` (Inheritance, DATA_MODEL §6.6.3) ; la révision des groupes s'intercale déterministement **après** les décréments croyances/confiance et **avant** le merge des naissances ; `transmissionRange` recalibré 20 → **55** (au défaut ph5, le scénario de référence n'échangeait aucune pulsation → trajectoire « silencieuse » ; la calibration ph6 produit des événements de groupe/naissance dans le run de référence). Checksum de la trajectoire recalculé (0x6aa2b2d87b32a8a5 → **0x864e72f57e1fe0d0**).

## 7. Impacts & contractuels

- Toute modification qui altère la trajectoire à seed identique impose :
  - incrément `MINOR`/`MAJOR` (cf. `../../VERSIONING.md`) ;
  - mise à jour de `engineVersion` (0.5.0 au jalon SYNE ph6 ; émise dans chaque snapshot, `ObservabilityContract.EngineVersion`).
- Les benchmarks (Annexe I) vérifient le déterminisme via checksum.

---

## Points restés ouverts dans ce document
- Aucun — le déterminisme est un contrat ferme et vérifié. Les implémentations parallèles devront maintenir l'ordre causal (détail à valider au moment du code).