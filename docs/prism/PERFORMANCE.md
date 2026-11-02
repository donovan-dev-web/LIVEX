# PERFORMANCE — Budgets, cibles et benchmarks PRISM

**Composant** : PRISM
**Statut** : [SPÉCIFICATION V2 — NON IMPLÉMENTÉ]
**Dernière mise à jour** : 1er novembre 2026
**Dépend de** : [`SIMULATION_LOOP.md`](SIMULATION_LOOP.md), `../syne/PERFORMANCE.md`
**Source Monographie** : Annexe I.3

---

## 1. Cibles de départ

**Hypothèses**, à confirmer par les premiers benchmarks.

| Charge | Cible en mode headless |
| :-- | :-- |
| 100 agents | ≥ 20 ticks par seconde (budget ≈ 50 ms/tick pour SYNE + PRISM) |
| 1 000 agents | ≥ 5 ticks par seconde (budget ≈ 200 ms/tick) |
| Rendu | Ne ralentit **jamais** la simulation |

Un run headless n'a pas à tourner en temps réel : plus il va vite, plus les expériences multi-runs sont courtes. **Déterminisme et vitesse headless comptent plus que les images par seconde.**

> **Changement de goulot.** SYNE seul atteint aujourd'hui ≥ 505 ticks/s à 1 000 entités (`../syne/PERFORMANCE.md` §9). Avec PRISM, le tick global est borné par le monde (pathfinding, perception) : les budgets SYNE doivent être **réexaminés** (le planificateur ajoute du coût côté cognition).

## 2. Budget par phase (répartition de départ, 1 000 agents, 200 ms)

| Phase | Part | ≈ ms |
| :-- | :-- | :-- |
| SYNE (cognition, planification, mémoire spatiale) | 30 % | 60 |
| PRISM — pathfinding et macro-actions | 25 % | 50 |
| PRISM — perception | 20 % | 40 |
| PRISM — mises à jour du monde (programmées, aléatoires, péremption) | 10 % | 20 |
| Sérialisation, échange, flux de lecture | 10 % | 20 |
| Marge | 5 % | 10 |

## 3. Benchmarks à écrire en premier

Écrits **avant** le code de production (phase P1), sur un monde généré et des bots. Ils décident de la porte G1 (3D ou repli 2.5D).

| # | Benchmark | Mesure |
| :-- | :-- | :-- |
| **B1** | Pathfinding : 1 000 agents, cibles aléatoires, monde généré | Temps par tick, taux d'échec, mémoire du cache |
| **B2** | Perception : coût d'une observation à trois niveaux pour 1 000 agents | Temps par tick, octets par observation |
| **B3** | Mises à jour aléatoires et croissance : monde entièrement cultivé | Coût par tick, deux variantes (programmée / aléatoire) |
| **B4** | Commandes : débit sous charge avec 1 000 agents émettant des macro-actions | Commandes/s, latence du pas de verrouillage, JSON vs codec binaire |
| **B5** | Diffusion de chunks : taille et temps d'un envoi complet puis de deltas | Octets, ms ; compression Deflate vs Brotli |
| **B6** | Génération du monde | Secondes, parallélisme par chunk |
| **B7** | Planification côté SYNE (prototype GOAP/HTN) | ms par replan, cache hit |
| **B8** | Rendu : meshing et dessin d'un monde complet | ms par chunk, images/s (viseur, non bloquant) |

Chaque benchmark a un **budget**, un **seuil d'alerte** et un **rapport** versionné (`docs/prism/benchmarks/`), à la manière de `docs/echos/BENCHMARKS.md`.

## 4. Techniques envisagées (par ordre d'essai)

1. Pathfinding hiérarchique par chunks (graphe de portails), cache de chemins, A* borné en nœuds.
2. Perception **incrémentale** : seuls les chunks *dirty* ou les agents ayant bougé sont recalculés ; filtre d'intérêt ; plafond par agent.
3. Mises à jour **par chunk parallèles** (hasard à compteur).
4. Observations différentielles ; codec binaire derrière le même schéma si B4 l'exige.
5. Pool d'objets et tableaux contigus (pas d'allocation dans la boucle de tick).

## 5. Budget mémoire

| Élément | Estimation |
| :-- | :-- |
| Monde dense | ≈ 12,6 Mo |
| Mémoire spatiale SYNE (1 000 agents × 1 024 chunks × 16 o) | ≈ 16 Mo |
| Résumés de chunks | < 1 Mo |
| Cache de chemins, observations, files | à mesurer (B1, B2) |

## 6. Rendu

Hors chemin critique. Cible de départ du visualiseur : 60 images/s sur un GPU intégré récent avec le monde de 256 × 256 × 64 (meshing glouton par chunk, remaillage incrémental des chunks *dirty*).

## 7. Plan de repli (porte G1)

Si le pathfinding 3D ou le rendu reste **hors budget après optimisation**, le modèle de données (matériaux, recettes, structures) est indépendant de la géométrie : un repli vers une **grille 2.5D** (hauteur plus quelques couches) reste possible **sans réécrire le contenu** (ROADMAP §8).

## Mises à jour

| Date | Changement | Motif |
| :-- | :-- | :-- |
| 1er novembre 2026 | Création | Refonte V2 |
