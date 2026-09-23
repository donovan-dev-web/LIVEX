# PERFORMANCE.md

**Composant** : SYNE
**Statut** : [STABLE]
**Dernière mise à jour** : 23 septembre 2026
**Dépend de** : `ARCHITECTURE.md`, `DETERMINISM.md`
**Source Monographie** : §7.4 (scalabilité), §7.5 (benchmarks V1), Annexe I (benchmarks détaillés)

---

## 1. Le problème

La perception naïve est **O(n²)** : à 1000 entités, 1 000 000 de comparaisons/tick. La communication naive double les coûts. LIVEX doit passer à une approche **linéaire/quasi-linéaire**.

| Échelle | Perceptions (naïf) | Messages communication |
| :-- | :-- | :-- |
| 50 entités | 2 500 | 250 |
| 500 entités | 250 000 | 2 500 |
| 1000 entités | 1 000 000 | 10 000 |

(Monographie §7.4.1)

## 2. Les quatre stratégies

1. **Grille spatiale** — perception O(1) moyen (9 cellules voisines).
2. **Cache de décisions** — pas de recalcul si objectifs inchangés.
3. **Traitement par lots** — messages en une passe (batchCommunication).
4. **LOD décisionnel** — les entités distantes décident moins souvent (1/2^LOD).
5. **Perception étagée** — rotation en 4 groupes (percevoir tous les 4 ticks).

(Monographie §7.4.2, §3.9.6–3.9.7)

## 3. Budget de tick (1000 entités, 100 ms)

| Sous-système | Budget |
| :-- | :-- |
| Perception | 20 ms |
| Mémoire / Croyances | 15 ms |
| Besoins / Objectifs | 10 ms |
| Utilité (décision) | 20 ms |
| Communication | 15 ms |
| Actions / Mouvement | 15 ms |
| Événements | 5 ms |
| **Total** | **100 ms** |

(Monographie §7.4.3)

## 4. Résultats attendus (objectifs V2)

| Échelle | Ticks/s | Perception | Décision | Communication | Total |
| :-- | :-- | :-- | :-- | :-- | :-- |
| 50 entités | ≥ 30 | ~2 ms | ~3 ms | ~1 ms | ~6 ms |
| 500 entités | ≥ 20 | ~12 ms | ~15 ms | ~8 ms | ~35 ms |
| 1000 entités | ≥ 10 | ~25 ms | ~30 ms | ~18 ms | ~73 ms |

(Monographie §7.4.4)

## 5. Optimisations mémoire

| Technique | Effet |
| :-- | :-- |
| Object pooling (Rent/Return) | −30-40% de charge GC |
| Pre-allocation (`List<Belief>(200)`) | Pas d'allocation dynamique |
| String interning | ~26 allocations économisées/entité/tick |
| Listes poolées (`ThreadLocal`) | Pas d'allocation par décision |
| Ring buffer d'événements | 500 000 événements bornés |

(Monographie §7.4.5)

## 6. Benchmarks V1 (référence historique)

Config : monde 500×500, config défaut, 3 seeds (12345, 999, 7).

- 20 entités / 2000 ticks : ~1 900–3 200 tps, 30–127 MB, survie 100 %.
- 100 entités / 1000 ticks : ~315–400 tps, 125–325 MB, survie 100 %.
- 1000 entités / 150 ticks : ~4.5–5.4 tps, 354–1241 MB, survie 100 % .

**Observations clés** (Monographie §7.5.2) :
- Déterminisme vérifié (état RNG identique entre runs).
- Aucun comportement scripté : moyenne de 4,7 actions distinctes par tick.
- **Émergence de l'attaque** à 100 entités (l'agressivité devient pertinente).
- Goulot : perception O(n²) — débit effondré à ~5 tps à 1000 entités (corrigé par la grille spatiale).

### Annexe I : optimisations spatiales (Phase 7)

| Échelle | Avant | Après | Gain | Mémoire après |
| :-- | :-- | :-- | :-- | :-- |
| 1000 entités | ~4.6 /s | ~6.3 /s | +37 % | 39 MB |
| 2000 entités | ~0.6 /s | ~0.9 /s | +50 % | 42 MB |

### Objectifs V2 (Annexe I.3)

| Population | tps ≥ | Mémoire ≤ | Couverture ≥ |
| :-- | :-- | :-- | :-- |
| 50 | 30 | 15 MB | 80 % |
| 500 | 20 | 40 MB | 80 % |
| 1000 | 10 | 80 MB | 80 % |

## 7. Plan d'exécution des benchmarks V2

Pour chaque population ∈ [50, 500, 1000], pour chaque seed ∈ [12345, 67890, 99999, 42, 999] : exécuter 1000 ticks ; mesurer débit/mémoire/CPU ; valider déterminisme (checksum) ; moyenner.

(Monographie Annexe I.4)

## 8. Micro-benchmark de voisinage (SYNE-012, jalon SYNE ph1)

Méthodologie CI : `PerceptionBenchmarkTests.QueryCircle_AverageStayUnderBudget_AtOneThousandEntities`
mesure le temps moyen de `Grid.QueryCircle` (rayon 50) sur 50 requêtes dans un monde
1000×1000 (cellule 50, 1000 entités réparties uniformément) et asserte **< 10 ms/requête** —
marge très large (≈ 500× le budget perception 20 ms) pour rester **déterministe en CI**
(sans flakiness machine) tout en bloquant toute régression O(n).

Le balayage est borné : une requête de rayon R ne visite que la **fenêtre de 3×3 cellules**
qui l'intersecte (jamais la population entière) — cf. `QueryCircle_ScanWindowIsCellBounded`.

---

## Points restés ouverts dans ce document
- Benchmarks V1 conservés comme référence historique [HÉRITÉ] — les chiffres V0.1 seront refaits après implémentation.
  - **V0.1 partiel (jalon ph9, §9)** : les débits mesurés au jalon ph9 (définitifs à l'échelle ≥ 500 sur la
    machine de référence) dépassent largement les cibles V2 ; un recalibrage complet est prévu au jalon
    ph10 (T4) et avant la validation v0.1.
- Machine de référence de la performance V0.1 à définir (processeur/coeurs utilisés) — impacte les budgets de tick.
  - **Définie au jalon ph9 (§9)** pour le benchmark `--benchmark` (Linux, x86-64).
- L'impact des optimisations (pooling, interning) sur le déterminisme reste à valider lors du codage.
  - **Validé au jalon ph9** : pooling (`ObjectPool`, buffer de tri de perception) et instrumentation
    (`TickBudgetCollector`) sont déterministes — checksum doré inchangé, tests d'égalité trajectoire.

---

## 9. Résultats V0.1 (jalon ph9 — SYNE-090 → SYNE-093)

### 9.1 Machine de référence

Benchmark exécuté en **Release**, monde **500×500**, cellule spatiale **50**, config par défaut,
**300 ticks**, seeds {12345, 999, 7} — en une seule passe mono-thread (déterminisme). Machine :
Linux x86-64 (poste de dev), .NET 10.0.400.

Commande : `dotnet run -c Release --project syne/Simulation.Console -- --benchmark`
(ticks/populations ajustables via `--benchmark-ticks`, `--benchmark-populations`).

### 9.2 Résultats mesurés

| Population | t/s min (3 seeds) | tick moyen min | part computation | Cible V2 (Decision n°30) |
| :-- | :-- | :-- | :-- | :-- |
| 50 | ≥ 2720 | 0.13 ms | 39–44 % | ≥ 30 t/s |
| 500 | ≥ 1187 | 0.56 ms | 39–68 % | ≥ 20 t/s |
| 1000 | ≥ 505 | 0.98 ms | 35–53 % | ≥ 10 t/s |

Les **cibles sont dépassées de ~30× (50), ~60× (500) et ~50× (1000)** avec une marge de
sécurité très large. La part de computation (Σ des sept sous-systèmes / temps de tick) reste
≥ 35 % tout au long — l'objectif **≥ 30 %** (décision n°30) est respecté ; le reste du tick est
de l'allocation/GC/overhead (à optimiser au ph10). Le goulot actuel est la passe de
**communication** (`batchCommunication`), suivie des événements/groupe/population.

### 9.3 Déterminisme performance (SYNE-093)

`--benchmark` affiche un **checksum FNV-1a canonique** de l'état (préfixe `population=N;ticks=T`,
puis une ligne `id;x;y;énergie` par entité, triée par id). Le checksum est **reproductible
bit-à-bit à seed égale** — vérifié par les tests `ScaleChecksum_IsBitForBitReproducible`,
`BudgetCollection_DoesNotAlterTrajectory` et la valeur épinglée `0x27fad50065d8c4a4` (inchangée
au ph9 : l'instrumentation et le pooling n'altèrent pas la trajectoire).
Note historique : aux échelles ≥ 500 sur des runs longs, la population s'éteint par épuisement
des réserves par défaut (problème de calibration, suivi jalon SYNE-120) ; le checksum reflète
alors fidèlement l'état (sans entité vivante).

### 9.4 Convention de test CI (anti-régression)

Les tests `ScaleTargetsTests` n'assertent **pas** les objectifs finaux (mesurés à la machine de
référence en 9.2) mais des **planchers anti-régression** environ 7× en-deçà des mesures réelles :
**50 → ≥ 120 t/s, 500 → ≥ 30 t/s, 1000 → ≥ 20 t/s** (meilleur de 3 essais). Une régression
d'ordre de grandeur (retour à la perception naïve O(n²), boucle cassée, etc.) les casse ; la
convention de marge est la même que pour le micro-benchmark `PerceptionBenchmarkTests` (§8).