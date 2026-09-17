# DETERMINISM.md

**Composant** : SYNE
**Statut** : [STABLE]
**Dernière mise à jour** : 17 septembre 2026
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

## 7. Impacts & contractuels

- Toute modification qui altère la trajectoire à seed identique impose :
  - incrément `MINOR`/`MAJOR` (cf. `../../VERSIONING.md`) ;
  - mise à jour de `engineVersion`.
- Les benchmarks (Annexe I) vérifient le déterminisme via checksum.

---

## Points restés ouverts dans ce document
- Aucun — le déterminisme est un contrat ferme et vérifié. Les implémentations parallèles devront maintenir l'ordre causal (détail à valider au moment du code).