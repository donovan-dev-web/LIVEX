# PERFORMANCE — Budgets, cibles et benchmarks PRISM

**Composant** : PRISM
**Statut** : [SPÉCIFICATION V2 — 2.5D DÉCIDÉ]
**Dernière mise à jour** : 2 novembre 2026
**Dépend de** : [`SIMULATION_LOOP.md`](SIMULATION_LOOP.md), `../syne/PERFORMANCE.md`
**Source Monographie** : Annexe I.3

---

## 1. Cibles de départ

**Hypothèses**, à confirmer par les premiers benchmarks.

| Charge | Cible en mode headless |
| :-- | :-- |
| 100 agents | ≥ 20 ticks par seconde (budget ≈ 50 ms/tick pour SYNE + PRISM) |
| 1 000 agents | ≥ 4 ticks par seconde (budget ≈ 250 ms/tick ; baseline de développement révisée après B1–B4) |
| Rendu | Ne ralentit **jamais** la simulation |

Un run headless n'a pas à tourner en temps réel : plus il va vite, plus les expériences multi-runs sont courtes. **Déterminisme et vitesse headless comptent plus que les images par seconde.**

> **Changement de goulot.** SYNE seul atteint aujourd'hui ≥ 505 ticks/s à 1 000 entités (`../syne/PERFORMANCE.md` §9). Avec PRISM, le tick global est borné par le monde (pathfinding, perception) : les budgets SYNE doivent être **réexaminés** (le planificateur ajoute du coût côté cognition).

## 2. Budget par phase (baseline révisée, 1 000 agents, 250 ms)

La répartition initiale de 200 ms était une hypothèse avant mesure. Les mesures récentes sur l'Intel Core i7-8750H donnent, en médiane, 78 ms pour 1 000 recherches parallèles, 33 ms de calcul de perception et 31 ms d'encodage JSON ; le round-trip WebSocket p95 est autour de 3 ms avec une réponse minimale. Le coût B2 a varié de 32 à 36 ms en calcul sur trois exécutions, signe d'une machine de développement partagée. La cible globale est ajustée à 250 ms (+25 % par rapport à l'hypothèse initiale), avec une marge explicite de 10 ms ; les postes internes sont réalloués selon ces mesures. C'est un budget d'ingénierie indicatif, pas une preuve de viabilité du tick complet.

| Phase | Part | ≈ ms |
| :-- | :-- | :-- |
| SYNE (cognition, planification, mémoire spatiale) | 22 % | 55 |
| PRISM — pathfinding et macro-actions | 36 % | 90 |
| PRISM — calcul de perception | 14 % | 35 |
| PRISM — mises à jour du monde (programmées, aléatoires, péremption) | 8 % | 20 |
| Sérialisation, échange, flux de lecture | 16 % | 40 |
| Marge | 4 % | 10 |

Référence de passage G1 : les budgets de 50 ms à 100 agents restent inchangés ; à 1 000 agents, seuil minimal explicite de **3 ticks/s** (≤ 333 ms) plutôt que d'étendre mécaniquement chaque budget de phase. Le budget nominal de 250 ms vise 4 ticks/s. Aucun benchmark isolé ne suffit : le seuil s'applique à un tick complet incluant SYNE, PRISM et transport, avec mesures représentatives. La réallocation rapproche les postes mesurés sans faire passer G1 : le tick complet reste à instrumenter.

### Mesures de production du tick (4 novembre 2026)

Windows, Release, pack minimal 256×256 (593 245 surcharges, 256 régions), `prism-bots --timings` et profilage `PRISM_TICK_PROFILE=1` :

| Poste | Avant | Après |
| :-- | :-- | :-- |
| `Prepare` (génération comprise) | 21–27 s | 2,0–2,4 s |
| Tick sans agent | 53 ms | ~2,0 ms |
| Tick, 1 agent inactif | 1,26–1,49 s | ~13,6 ms |
| Tick, 3 agents | — | ~30,4 ms |
| Campagne 3 graines × 200 ticks × 3 bots | plusieurs heures (non exécutée) | ~24 s |
| Campagne 3 graines × 5 000 ticks × 3 bots | — | 24–71 s par run selon contention, ≈3 min au total |

Trois scans mondiaux par tick ont été supprimés : énumération de toutes les surcharges par agent (`AddNearbyBlocks`), recalcul des résumés de 49 régions par agent, et `ResumeGrowingCrops` rejoué à chaque tick alors que la plantation programme déjà l'événement. Les empreintes de run sont identiques avant/après (1 graine × 20 ticks × 3 bots, worktree `HEAD`).

Reste à faire pour R1 : ~9 ms/tick/agent (perception : ~800 colonnes + diff d'observation), soit ~9 s/tick à 1 000 agents, très au-dessus du seuil de 333 ms. Les paramètres `perception` du pack (`nearRadius`, `midRadiusChunks`, `maxObservedPerAgent`) sont désormais compilés par `PerceptionSettingsRuntimeCompiler` et appliqués au cycle de vie des agents par `PrismHostRuntime` ; le calibrage B2/R1 peut donc réduire le rayon et le plafond d'observation depuis `params.json` sans reconstruire le host.

## 3. Benchmarks à écrire en premier

Écrits **avant** l'optimisation de production (phase P1), sur un prototype jetable de colonnes 2.5D et des bots. Ils mesurent les budgets et identifient les optimisations nécessaires ; le choix de géométrie n'est plus un critère de G1 (ADR-011).

| # | Benchmark | Mesure |
| :-- | :-- | :-- |
| **B1** | Pathfinding 2.5D : 1 000 agents, cibles aléatoires, transitions d'altitude | Temps par tick, taux d'échec, mémoire du cache |
| **B2** | Perception : coût d'une observation à trois niveaux pour 1 000 agents | Temps par tick, octets par observation |
| **B3** | Mises à jour aléatoires et croissance : monde entièrement cultivé | Coût par tick, deux variantes (programmée / aléatoire) |
| **B4** | Commandes : débit WebSocket JSON sous charge avec 1 000 agents émettant des macro-actions, comparé à la liaison embarquée | Commandes/s, latence du pas de verrouillage, temps de sérialisation |
| **B5** | Diffusion de régions : taille et temps d'un envoi complet puis de deltas | Octets, ms ; JSON et compression BCL |
| **B6** | Génération 2.5D du monde | Secondes, parallélisme par région |
| **B7** | Planification côté SYNE (prototype GOAP/HTN) | ms par replan, cache hit |
| **B8** | Rendu : meshing et dessin d'un monde complet | ms par région, images/s (viseur, non bloquant) |

Chaque benchmark a un **budget**, un **seuil d'alerte** et un **rapport** versionné (`docs/prism/benchmarks/`), à la manière de `docs/echos/BENCHMARKS.md`.

Le rapport initial B1 est publié dans [`benchmarks/B1-pathfinding-25d.md`](benchmarks/B1-pathfinding-25d.md). Le prototype synthétique atteint zéro échec ; en parallèle (degré maximal 12), la médiane des trois dernières exécutions est de 11,61 ms pour 100 routes et 81,23 ms pour 1 000. Le second résultat reste sous l'allocation indicative de 90 ms, à confirmer sur le tick complet.

Le rapport initial B2 est publié dans [`benchmarks/B2-perception-25d.md`](benchmarks/B2-perception-25d.md). Pour 1 000 observations synthétiques, le calcul médian récent est de 32,70 ms et leur encodage JSON médian de 34,78 ms ; l'observation moyenne fait 3 235 octets, plus 31 050 octets pour publier toutes les positions. Ces coûts restent sous les postes indicatifs révisés de 35 ms pour le calcul de perception et 40 ms pour sérialisation/échange, avec une faible marge. Le prototype ne modélise pas les différences d'observation, le plafond ni les structures. G1 reste ouverte.

Le rapport initial B3 est publié dans [`benchmarks/B3-growth-25d.md`](benchmarks/B3-growth-25d.md). Sur 65 536 cultures synthétiques, les transitions programmées mesurent 0,002 ms/tick, contre 0,016 ms/tick pour trois tirages déterministes par région. Ces variantes ne produisent pas le même état final ; le résultat confirme le coût faible des callbacks programmés dans ce prototype, pas l'équivalence ni le coût de production.

Le rapport initial B4 est publié dans [`benchmarks/B4-transport-25d.md`](benchmarks/B4-transport-25d.md). Sur 100 ticks de 1 000 commandes, le lockstep WebSocket JSON loopback avec snapshots complets et 1 000 événements synthétiques/tick a une médiane de 6 970 ms, un p50 de 69,44 ms et un p95 de 93,67 ms par tick. Cette borne haute dépasse le poste transport de 40 ms ; le trafic contractuel est différentiel, reste à implémenter et mesurer, et G1 n'est pas franchie.

Le rapport initial B5 est publié dans [`benchmarks/B5-region-streaming-25d.md`](benchmarks/B5-region-streaming-25d.md). Le snapshot synthétique complet (256 régions) fait 4 794 556 octets JSON ; le delta d'un tick (4 colonnes/région) fait 111 364 octets. Deflate réduit respectivement à 307 312 et 11 528 octets ; Brotli à 158 123 et 7 697. Le jeu est très répétitif ; les résultats de compression sont indicatifs.

Le rapport initial B6 est publié dans [`benchmarks/B6-generation-25d.md`](benchmarks/B6-generation-25d.md). Sur 256 régions, génération séquentielle médiane 3,43 ms ; génération parallèle régionale médiane 0,94 ms, avec égalité exacte des tableaux. Résultat indicatif, hors génération complète du contenu et création des structures.

## 4. Techniques envisagées (par ordre d'essai)

1. Pathfinding hiérarchique par régions (graphe de portails), cache de chemins, A* borné en nœuds.
2. Perception **incrémentale** : seules les régions *dirty* ou les agents ayant bougé sont recalculés ; filtre d'intérêt ; plafond par agent.
3. Mises à jour **par région parallèles** (hasard à compteur).
4. Observations différentielles ; codec binaire derrière le même schéma si B4 l'exige.
5. Pool d'objets et tableaux contigus (pas d'allocation dans la boucle de tick).

## 5. Budget mémoire

| Élément | Estimation |
| :-- | :-- |
| Métadonnées de surface (65 536 colonnes × 6 octets) | ≈ 384 Kio, hors surcharges ; régions uniformes compactes |
| Mémoire spatiale SYNE (1 000 agents × 256 régions × 16 o) | ≈ 4 Mo |
| Résumés de régions | < 1 Mo |
| Cache de chemins, observations, files | à mesurer (B1, B2) |

## 6. Rendu

Hors chemin critique. Cible de départ du visualiseur : 60 images/s sur un GPU intégré récent avec le monde de 256 × 256 colonnes (meshing glouton des surfaces et façades par région, remaillage incrémental des régions *dirty*).

## 7. Plan de repli (porte G1)

La géométrie retenue est 2.5D dès l'origine (ADR-011). Si navigation ou perception dépasse encore son budget, les optimisations portent sur l'indexation des régions, la recherche de chemins hiérarchique, le cache et le calcul différentiel ; le modèle de contenu reste indépendant de ces optimisations.

## Mises à jour

| Date | Changement | Motif |
| :-- | :-- | :-- |
| 1er novembre 2026 | Création | Refonte V2 |
