# DETERMINISM — Déterminisme du monde PRISM

**Composant** : PRISM
**Statut** : [SPÉCIFICATION V2 — NON IMPLÉMENTÉ]
**Dernière mise à jour** : 1er novembre 2026
**Dépend de** : [`adr/ADR-007-determinisme-entier-flux-hasard.md`](adr/ADR-007-determinisme-entier-flux-hasard.md), `../syne/DETERMINISM.md`
**Source Monographie** : §2.3.4

---

## 1. Engagement

À **seed, configuration, version de PRISM, version de SYNE et `contentHash`** identiques, un run produit une trajectoire du monde identique : mêmes blocs, mêmes inventaires, mêmes événements, **mêmes empreintes**.

| Niveau | Garantie | Condition |
| :-- | :-- | :-- |
| **Exact, même plateforme** | Bit-à-bit | — |
| **Exact, entre plateformes** (cible V2) | Bit-à-bit pour l'état du monde PRISM | Arithmétique entière, aucune dépendance flottante dans `Prism.Core` ; **à prouver** par test Windows/Linux (G3) |
| **Ensemble SYNE + PRISM** | Exact sur une plateforme ; entre plateformes, le niveau de SYNE (statistique tant que ses flottants influencent la cognition) | Voir `../launcher/INTEGRATION_CONTRACT.md` §12 |

## 2. Les cinq piliers

1. **Seed unique** du run, dérivée en **graines de flux nommés** par `Mix64(runSeed, hash(nom))`.
2. **Arithmétique entière / virgule fixe** pour tout ce qui influence l'état.
3. **Hasard à compteur** (sans état) pour ce qui doit être indépendant de l'ordre ou parallèle ; `xoshiro256**` seulement pour les processus séquentiels.
4. **Ordre causal strict** : l'ordre fixe de `SIMULATION_LOOP.md` §2 ; identifiants croissants ; clés de priorité stateless.
5. **Aucune source d'aléa implicite** : pas d'horloge, pas d'itération sur un dictionnaire non trié, pas d'identités d'objets, pas de threads qui écrivent.

## 3. Flux de hasard

| Flux | Famille | Usage | Clé de compteur |
| :-- | :-- | :-- | :-- |
| `terrain` | séquentiel / treillis | Relief, zones | `(ix, iy, iz)` |
| `ores` | séquentiel / treillis | Filons | `(ix, iy, iz)` |
| `forest` | compteur | Placement des arbres | `(cellX, cellY)` |
| `spawn` | compteur | Sites d'apparition | `(candidateIndex)` |
| `randomTicks` | **compteur** | Mises à jour aléatoires | `(tick, chunkId, k)` |
| `conflicts` | **compteur** | Priorité d'arbitrage | `(tick, agentId)` |
| `growthJitter` | **compteur** | Gigue de croissance | `(blockIndex, plantTick)` |
| `drops` | **compteur** | Drops probabilistes | `(blockIndex, tick)` |

`Mix64` : fonction de mélange 64 bits à entiers (SplitMix64), implémentée à l'identique sur toutes les plateformes. Ajouter un flux ne décale jamais les autres ; ajouter un tirage dans un flux à compteur ne décale rien.

## 4. Empreintes

| Empreinte | Définition |
| :-- | :-- |
| Chunk | FNV-1a 64 de `ids ‖ states` |
| Monde | FNV-1a 64 de la suite ordonnée (`chunkId` croissant) des empreintes de chunks, plus des tables d'état (agents, conteneurs, chantiers, structures, calendrier, files programmées) en ordre canonique |
| Run | Suite des empreintes de monde aux ticks `k × checksumIntervalTicks` + empreinte finale |

Les empreintes sont publiées dans `checksum` (flux de lecture) et consignées dans les résultats de run. ECHOS peut comparer deux runs et situer **le premier tick de divergence**.

## 5. Mondes et runs dorés

- **Monde doré** : pour chaque `(seed, worldGenVersion, contentHash)` de référence, l'empreinte du monde généré est figée dans le dépôt.
- **Run doré** : un run de bots de 5 000 ticks (seeds `12345`, `424242`, `999`, les seeds déjà utilisées pour la calibration SYNE) ; empreintes aux ticks 500, 1000, … figées.
- Toute altération volontaire impose `MINOR` au minimum, la mise à jour de `worldGenVersion` ou `engineVersion`, et le **re-calage assumé** des empreintes dorées dans la même PR (même règle que SYNE).

## 6. Vérifications

| Test | Contenu |
| :-- | :-- |
| Rejeu | Deux exécutions du même run → empreintes identiques à chaque intervalle |
| Parallélisme | Même run avec 1, 2, 8 threads → empreintes identiques |
| Plateformes | Windows et Linux → mêmes empreintes (job CI dédié) |
| Reprise | Run interrompu au tick `t`, restauré, poursuivi → empreintes identiques à un run continu |
| Ordre | Permutation des commandes d'un tick (même ensemble) → même résultat (l'ordre vient des clés de priorité) |
| Analyseur | Refus de `float`/`double`, `DateTime.Now`, `Random`, `Dictionary` itéré sans tri dans `Prism.Core` |
| Indépendance du rendu | Run avec et sans visualiseur connecté → mêmes empreintes |

## 7. Déterminisme et interventions humaines

Une intervention (Q-05) est une **commande journalisée** (`interventions.log` : tick, commande) et rejouable. Un run avec interventions porte un drapeau `interventions: true` dans ses métadonnées et est exclu des comparaisons ECHOS par défaut.

## Points restés ouverts dans ce document

Preuve inter-plateformes à produire en phase P7.

## Mises à jour

| Date | Changement | Motif |
| :-- | :-- | :-- |
| 1er novembre 2026 | Création | Refonte V2 |
