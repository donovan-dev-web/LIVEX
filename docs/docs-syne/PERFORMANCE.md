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

---## Points restés ouverts dans ce document
- Benchmarks V1 conservés comme référence historique [HÉRITÉ] — les chiffres V0.1 seront refaits après implémentation.
  - **V0.1 refait le 08/10/2026 (V5, §9.2)** : mesures honnêtes à charge complète sur la
    machine de référence — cibles V2 atteintes à 50 et 500, **écart ~1,7× à 1000**
    (≈ 6 t/s vs ≥ 10) documenté comme chantier d'optimisation (perception), hors
    blocage V0.1 (scénarios ≤ 100 agents).
- Machine de référence de la performance V0.1 à définir (processeur/coeurs utilisés) — impacte les budgets de tick.
  - **FIGÉE le 08/10/2026 (§9.1)** : i7-8750H (6c/12t), 14 Gio, Ubuntu x86-64, .NET 10.0.401 — arbitrage A3 de `ROADMAP-V01.md`.
- L'impact des optimisations (pooling, interning) sur le déterminisme reste à valider lors du codage.
  - **Validé au jalon ph9** : pooling (`ObjectPool`, buffer de tri de perception) et instrumentation
    (`TickBudgetCollector`) sont déterministes — checksum doré inchangé, tests d'égalité trajectoire.

---

## 9. Résultats V0.1 (jalon ph9 — SYNE-090 → SYNE-093)

### 9.1 Machine de référence

**Machine de référence V0.1 (figée le 08/10/2026, arbitrage A3 de `ROADMAP-V01.md`)** :
Intel Core **i7-8750H** (6 cœurs / 12 threads, 2,2–4,1 GHz), **14 Gio** de RAM,
Ubuntu x86-64 (noyau 7.0.0-34), SDK **.NET 10.0.401**, alimentation sur secteur.

Benchmark exécuté en **Release**, monde **500×500**, cellule spatiale **50**, config par défaut,
**300 ticks**, seeds {12345, 999, 7} — en une seule passe mono-thread (déterminisme).

Commande : `dotnet run -c Release --project syne/Simulation.Console -- --benchmark`
(ticks/populations ajustables via `--benchmark-ticks`, `--benchmark-populations`).

Protocole identique à la mesure ph9 ; **deux passes consécutives du 08/10/2026**
(reproductibilité : checksums FNV-1a identiques d'une passe à l'autre, débits à ±3 %).

### 9.2 Résultats mesurés (refaits le 08/10/2026 — V5)

| Population | t/s (2 passes, 3 seeds) | tick moyen | Cible V2 (décision n°30) | Verdict |
| :-- | :-- | :-- | :-- | :-- |
| 50 | **1354 – 2257** | 0,44 – 0,73 ms | ≥ 30 t/s | ✓ ~45× au-dessus |
| 500 | **42,8 – 53,9** | 18,5 – 23,3 ms | ≥ 20 t/s | ✓ ~2,1× au-dessus |
| 1000 | **5,9 – 6,5** | 153 – 169 ms | ≥ 10 t/s | **✗ écart : ~1,7× en deçà** |

La part de computation mesurée est de **92–99,8 %** selon les cellules — le seuil
**≥ 30 %** (décision n°30) est respecté partout. Sur les paliers ≥ 500, la phase
**perception** domine (0,03–0,12 ms par entité-tick, soit l'essentiel du tick
pondéré), suivie des **événements** (~15 ms/tick à N=1000) et de la
**communication** (~3 ms/tick) ; l'allocation/GC n'apparaît plus qu'en queue.

> **Écart à documenter (V5, ouvert).** Les chiffres ph9 publiés ici (`≥ 1187` à
> N=500, `≥ 505` à N=1000) ont été mesurés sur des runs **dont la population
> mourait en cours de route** par défaut de calibration : les ticks devenaient
> quasi nuls en fin de run. Depuis ADR-016 les populations survivent (0 mort),
> les mesures ci-dessus sont donc les **premières mesures honnêtes à charge
> complète**. À N=1000 le débit réel (≈ 6 t/s) reste **~1,7× sous la cible V2** :
> l'optimisation de la passe de perception (et le traitement par lots des
> événements) devient le chantier d'optimisation — suivi en ouverture de
> `RAPPORT-ELEMENTS-OUVERTS.md` §3.4/§4.2, hors blocage V0.1 (les scénarios
> V0.1 tournent à ≤ 100 agents, ~45× au-dessus de la cible).

> **Correction de la métrique (calibration B1, engineVersion 0.15.0, ADR-016).** Le calcul de
> `TickBudgetSnapshot.ComputationShare()` sommait `MeanMs(phase)`, c'est-à-dire du temps **par
> échantillon** — or les phases intra-entité (perception, mémoire, besoins, actions) sont
> échantillonnées à chaque entité × tick quand communication/événements le sont une fois par
> tick. La part affichée revenait donc à (communication + événements) / tick, sous-estimant les
> phases intra-entité d'un facteur ≈ population (les mesures ci-dessus, obtenues avec un défaut
> qui laissait mourir la majorité des entités en cours de run, en héritaient). La part est
> désormais le temps **par tick** de chaque phase ÷ temps de tick : mesures de l'ordre de 90–98 %
> sur les mêmes scénarios, le complément restant le travail hors scopes (tri de l'ordre causal,
> allocations, GC). Le seuil **≥ 30 %** de la décision n°30 reste inchangé et toujours tenu.

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