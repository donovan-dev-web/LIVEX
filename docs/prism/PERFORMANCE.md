# PERFORMANCE — Budgets, cibles et benchmarks PRISM

**Composant** : PRISM
**Statut** : [SPÉCIFICATION V2 — 2.5D DÉCIDÉ]
**Dernière mise à jour** : 14 novembre 2026
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

## 2. Budget par phase et baseline mesurée (1 000 agents, 250 ms)

Cette table n'est plus une hypothèse : depuis le 11 novembre 2026, chaque poste est confronté à une mesure reproductible du tick couplé SYNE ↔ PRISM. Commande unique :

```sh
python scripts/prism-coupled-benchmark.py
```

Elle construit les binaires Release, lance l'hôte PRISM et le client couplé sur 100×25, 500×10 et 1 000×8 (seed 7, contenu `793A8996…`, production `stone_pickaxe,chest,wooden_bucket,stone_hoe,furnace`), jette un run de chauffe puis le premier et le dernier tick du relevé, et écrit [`benchmarks/B9-coupled-tick-2026-11-11-v1.json`](benchmarks/B9-coupled-tick-2026-11-11-v1.json). Trois exécutions consécutives par scénario : l'erreur-type relative des médianes reste sous 10 % (7,1 % au pire) et les empreintes sont identiques d'un run à l'autre (`27f05beab5435150` à 1 000 agents). L'étendue max-min est publiée séparément : jusqu'à 24 % sur les petits postes, dominée par le run le plus lent d'une machine de développement partagée.

Mesure à 1 000 agents × 8 ticks (médiane des trois runs, 12 vCPU, Release) :

| Phase | Budget | Mesure | Écart |
| :-- | --: | --: | :-- |
| SYNE (cognition, planification, mémoire spatiale) | 55 ms | 147,6 ms (`decide` client) | **+92,6 ms (×2,7)** |
| PRISM — pathfinding et macro-actions | 90 ms | 654,2 ms (328,5 + 325,7) | **+564,2 ms (×7,3)** |
| PRISM — calcul de perception | 35 ms | 2 958,7 ms | **+2 923,7 ms (×85)** |
| PRISM — mises à jour du monde (programmées, aléatoires, péremption) | 20 ms | 0,04 ms | −20 ms : aucune culture ni événement dans ce scénario |
| Sérialisation, échange, flux de lecture | 40 ms | 1 375,8 ms (`transport`, hors hôte) | **+1 335,8 ms (×34)** |
| Marge | 10 ms | — | absorbée par les dépassements |
| **Total du tick** | **250 ms** | **4 533,2 ms** | **+4 283,2 ms (×18)** |

L'écart est structurel, pas un bruit de mesure. La perception coûte à elle seule près de douze fois le budget total : elle traite les observations de 1 000 agents sur un monde de 256×256, soit ≈3,0 ms par agent et par tick. Le pathfinding et les macro-actions pèsent 2,6 fois le budget total, et le transport 5,5 fois le budget total ; le découpage paginé de la réponse (ADR-014) y ajoute des allers-retours et la reconstruction côté client. Les allocations atteignent 632 Mo par tick et la pause GC 232 ms par tick en médiane : la pression mémoire n'est pas un détail, c'est une part du coût.

La médiane masque en outre une queue catastrophique : le p95 du tick atteint **14 337 ms** à 1 000 agents, dont 10 265 ms d'écritures différées (pathfinding), contre 8 601 ms à 500 agents. Une minorité de ticks paie la collecte d'un tas de plusieurs gigaoctets ; un budget fondé sur la médiane seule ne verrait pas ces à-coups.

Les autres scénarios confirment que le coût ne décroît pas avec le nombre d'agents : 100 agents × 25 ticks coûtent 1 743,7 ms par tick (budget 50 ms, ×35) et 500 agents × 10 ticks 2 684,6 ms, sans budget de référence. À 100 agents, le poste dominant est le pathfinding et les écritures (1 440,7 ms) ; à 500 et 1 000 agents, c'est la perception.

Référence de passage G1 : les budgets de 50 ms à 100 agents restent inchangés ; à 1 000 agents, seuil minimal explicite de **3 ticks/s** (≤ 333 ms) plutôt que d'étendre mécaniquement chaque budget de phase. Le budget nominal de 250 ms vise 4 ticks/s. Le tick complet est désormais instrumenté.

### 2 bis. État au 14 novembre 2026 — après PRISM-104, 105, 119, 141 et 142

La table ci-dessus est la **baseline du 11 novembre** ; elle n'est plus l'état courant. Quatre passes d'optimisation et le plafond de payload ont changé la répartition. Mesure courante ([`benchmarks/B9-coupled-tick-2026-11-14-v5-after-119.json`](benchmarks/B9-coupled-tick-2026-11-14-v5-after-119.json), trois runs, empreintes stables, erreur-type ≤ 3,5 % sur les postes publiés) :

| Poste | 100 agents × 25 ticks | 1 000 agents × 8 ticks | Budget | Écart à 1 000 |
| :-- | --: | --: | --: | :-- |
| Tick du monde (hôte) | **355 ms** | **850 ms** | 250 ms | ×3,4 |
| dont perception | 19 ms | **317 ms** | 35 ms | ×9,1 |
| dont pathfinding + écritures | 325 ms | 101 ms | 90 ms | ×1,1 |
| dont macro-actions | 15 ms | 101 ms | — | — |
| Allocations par tick | 119 Mo | 311 Mo | — | — |
| Pause GC par tick | 11 ms | 87 ms | 20 ms | ×4,4 |
| Transport (client, hors hôte) | 16 ms | 576 ms | 40 ms | ×14,4 |
| **Aller-retour complet** | **368 ms** | **2 073 ms** | 50 / 333 ms | **×7,4 / ×6,2** |
| p95 du tick | 450 ms | 5 389 ms | — | — |

Progression depuis la baseline : tick **4 533 → 850 ms** (×5,3), perception **2 959 → 317 ms** (×9,3), pathfinding+macros 654 → 202 ms, transport 1 376 → 576 ms, allocations 632 → 311 Mo, pause GC 232 → 87 ms. Le run ne s'arrête plus : la pagination de PRISM-142 (ADR-014) tient 8/8 ticks à 1 000 agents.

**G1 n'est pas franchie** : 0,48 aller-retour par seconde à 1 000 agents contre 3 exigés (×6,2), et 2,7 ticks/s à 100 agents contre 20 (×7,4). L'écart a changé de nature : il n'est plus dominé par un calcul quadratique mais par le **transport et la sérialisation** (576 ms à 1 000 agents, plus que le tick de l'hôte), par la **perception restante** (317 ms, dont ≈ 100 ms de diff par agent et ≈ 50 ms d'extras O(N²)) et par la **pression mémoire** (311 Mo/tick, 87 ms de GC). Le prochain levier de perception est un diff incrémental ; celui du transport est la taille des observations par tick (≈ 25 000), pas la vitesse de l'hôte.

**14 novembre 2026 — sérialisation unique de la réponse (mesuré).** Deux gaspillages ont été retirés du chemin de sortie : l'hôte sérialisait le résultat en chaîne pour la mesure puis la pagination le **re-sérialisait** en octets (deux passes complètes, plus une chaîne UTF-16 intermédiaire), et la validation du protocole matérialisait un DOM par observation (`Changes.ValueKind`) alors que le convertisseur de PRISM-141 écrit les payloads typés directement. Mesure après ([`benchmarks/B9-coupled-tick-2026-11-14-v6-single-payload.json`](benchmarks/B9-coupled-tick-2026-11-14-v6-single-payload.json), mêmes scénarios, trois runs, empreintes identiques `fbd59443744b0dbd` et `27f05beab5435150`, 25/25 et 8/8 ticks) :

| Poste | v5 (avant) | v6 (après) | Écart |
| :-- | --: | --: | :-- |
| Tick hôte, 100 agents | 355,0 ms | 354,8 ms | −0,1 % |
| Tick hôte, 1 000 agents | 850,0 ms | 831,7 ms | −2,2 % |
| Transport, 1 000 agents | 576,2 ms | 508,5 ms | −11,7 % |
| Aller-retour, 1 000 agents | 2 072,6 ms | 1 929,5 ms | −6,9 % |
| Allocations par tick, 1 000 agents | 310,9 Mo | 283,6 Mo | −8,8 % |
| Pause GC par tick, 1 000 agents | 86,9 ms | 96,3 ms | non concluant (bruit) |

Le gain est réel mais **petit**, et il ne change aucun verdict : à 100 agents il est nul (charge utile de quelques mégaoctets), et à 1 000 agents la variation du tick (−18 ms) reste dans l'étendue des runs v5 (840 à 935 ms). Ce qui est solide, c'est la suppression d'une passe complète de sérialisation et de 27 Mo d'allocation par tick — un coût qui, lui, ne dépend pas du bruit. Le budget de transport (40 ms) n'est pas entamé : le levier reste la **taille** des observations, pas leur encodage.

### 2 ter. État au 14 novembre 2026 — observations typées (v7)

Le même défaut restait à deux endroits du chemin d'observation : la phase de perception construisait un **document JSON par agent** (`JsonSerializer.SerializeToElement`) que le convertisseur de PRISM-141 se contentait de recopier, et la lecture des pages faisait `JsonDocument.ParseValue` puis `RootElement.Clone()`, soit **deux copies** de chaque charge utile. La sonde `PRISM_ALLOC_PROBE=1` chiffrait le premier : 164,3 Mo alloués au premier tick à 1 000 agents, 24,7 Mo en régime, pour des octets identiques sur le fil.

Mesure après ([`benchmarks/B9-coupled-tick-2026-11-14-v7-typed-observations.json`](benchmarks/B9-coupled-tick-2026-11-14-v7-typed-observations.json), mêmes scénarios, trois runs, empreintes identiques `fbd59443744b0dbd` et `27f05beab5435150`, 25/25 et 8/8 ticks, erreur-type ≤ 4,6 %) :

| Poste | 100 agents (v6 → v7) | 1 000 agents (v6 → v7) |
| :-- | --: | --: |
| Tick du monde | 354,8 → **353,7 ms** | 831,7 → **582,0 ms** (−30,0 %) |
| dont perception | 20,0 → 16,8 ms | 316,7 → **177,7 ms** (−43,9 %) |
| dont pathfinding + écritures | 319,4 → 330,6 ms | 104,2 → 94,9 ms |
| Transport (client) | 14,9 → 13,6 ms | 508,5 → 473,5 ms |
| **Aller-retour complet** | **365,6 → 362,7 ms** | **1 929,5 → 1 538,9 ms** (−20,2 %) |
| Allocations par tick | 118,4 → 117,5 Mo | 283,6 → **231,4 Mo** |
| Pause GC par tick | 10,0 → 11,5 ms | 96,3 → 83,2 ms |
| Allocations d'enveloppes (sonde) | — | 24,7 → **0,2 Mo** |

À 100 agents rien ne bouge (charge utile de quelques mégaoctets), ce qui est le contrôle attendu : le gain suit la taille des observations, pas le code. Les empreintes de monde sont inchangées, donc les décisions des bots aussi.

**14 novembre 2026 — diff de fenêtre propre (v8).** La perception recalculait le diff de blocs pour chaque agent à chaque tick, même quand sa fenêtre était réutilisée à l'identique (agent immobile, aucune région couverte écrite) : dans ce cas le diff est vide par construction, et le parcours coûtait ~870 entrées de fenêtre plus ~900 entrées connues par agent. Le saut n'est sûr que si le tick précédent n'a rien tronqué — une entrée tombée au-delà du plafond de 256 doit être réémise — d'où un drapeau `BlocksInSync`, faux dès qu'un changement n'est pas retenu. Mesure ([`benchmarks/B9-coupled-tick-2026-11-14-v8-clean-window-diff.json`](benchmarks/B9-coupled-tick-2026-11-14-v8-clean-window-diff.json), trois runs, empreintes `fbd59443744b0dbd` et `27f05beab5435150`, 25/25 et 8/8 ticks) :

| Poste | 100 agents (v7 → v8) | 1 000 agents (v7 → v8) |
| :-- | --: | --: |
| Tick du monde | 353,7 → **348,0 ms** | 582,0 → **560,2 ms** (−3,7 %) |
| dont perception | 16,8 → **12,8 ms** (−23,7 %) | 177,7 → **161,8 ms** (−8,9 %) |
| Pause GC par tick | 11,5 → 9,9 ms | 83,2 → 73,4 ms |
| Aller-retour complet | 362,7 → 357,4 ms | 1 538,9 → 1 531,2 ms |
| Allocations par tick | 117,5 Mo | 231,4 Mo (inchangées) |

Le gain est concentré sur les ticks où rien n'est tronqué (4 à 6) : aux ticks 1 et 2 le plafond de 256 changements par agent tronque, donc le diff doit être rejoué. Contrôle d'équivalence : empreinte du run manuel 7 ticks `aaef5fc2f335993b`, identique avant et après. **Cumul de la journée** : tick 850 → 560 ms (−34 %), perception 317 → 162 ms (−49 %), aller-retour 2 073 → 1 531 ms (−26 %).

**14 novembre 2026 — index spatial du voisinage (PRISM-141).** La perception parcourait **tous** les agents pour chaque agent (**1 000 000** de tests de rayon par tick à 1 000 agents) et copiait **tout** le magasin d'objets au sol par agent (`GroundItemStore.Snapshot`). `PerceptionSpatialIndex` répartit agents et objets dans des seaux de 16 × 16, reconstruits une fois par tick (listes remises dans un pool), et ne rend que des candidats au test de rayon exact : la sortie reste celle d'un balayage complet, ce que trois tests comparent agent par agent et tick par tick (`PerceptionSpatialIndexTests`, dont l'arrivée et le départ d'un agent et la frontière de seau). Mesure sur le **même binaire**, l'ancien chemin forcé par `PRISM_PERCEPTION_FULL_SCAN=1` :

| Poste (1 000 agents × 8 ticks) | Balayage complet | Index spatial |
| :-- | --: | --: |
| Tests de rayon exacts par tick | 1 000 000 | **33 334** (−96,7 %) |
| Perception (médiane) | 205,5 ms | 147,3 ms |
| Tick du monde (médiane) | 693,5 ms | 584,8 ms |
| Allocations par tick | 238,1 Mo | 230,9 Mo |

Les deux timings de cet A/B sont **rejetés par le harnais** (variance 14,4 % sur une machine chargée) : seul le compteur de tests est publié comme fait, les millisecondes ne le sont pas. La paire publiable est celle mesurée avant/après modification dans la même session, erreur-type 3,1 % puis 1,4 % : tick **631,96 → 596,48 ms** (−5,6 %), perception **169,48 → 157,03 ms** (−7,3 %), allocations **237,8 → 230,9 Mo/tick**, empreintes `27f05beab5435150` stables sur les deux runs de chaque côté. Les objets au sol sont rares dans ce scénario (0 candidat objet) : l'index d'objets est couvert par les tests, pas par un gain mesuré, et l'essentiel du gain vient du balayage des agents. Suites **Core 232/232**, **Host 78/78**.

**Piste écartée le 14 novembre 2026 — plafond de nœuds de recherche (PRISM-104).** La mesure a nommé le gaspillage : à 100 agents × 25 ticks, **474 recherches sur 558 échouent** et chacune développe tout le graphe (113 000 à 120 000 nœuds), soit 2,36 M de nœuds et 330 ms de `ApplyWrites` par tick. Le plafond de nœuds valait la carte entière, donc il n'en était pas un. Un plafond fixe de 20 000 nœuds a été posé et mesuré : à 100 agents, tick 348 → **150,8 ms** (−57 %) et `ApplyWrites` 330 → 122 ms, **empreinte inchangée** (`fbd59443744b0dbd`) ; à 1 000 agents en revanche, **649,8 ms** contre 560, avec **17,8 % d'erreur-type** (étendue 56 %) — le harnais a **rejeté** la mesure. Cause : un échec borné n'est pas une absence prouvée, donc il ne se mémorise pas comme elle et le client repaie la recherche à chaque redemande (1 940 recherches sur 8 ticks). Le plafond et la mémorisation des échecs bornés ont été **retirés** ; le relevé `B9-coupled-tick-2026-11-14-v9-path-budget.json` n'est pas retenu. La voie correcte est celle de PRISM-104/143 : lecture par région pour mutualiser un échec exact, et un client qui cesse de viser une cible inatteignable.

**Ce que le volume coûte encore, mesuré.** Un tick à 1 000 agents reste 41 Mo de JSON, servis en 5 réponses de ~8,3 Mo ; côté client `parse` coûte 110 à 160 ms **par réponse**, soit ≈ 600 ms par tick — plus que la perception de l'hôte. Ce coût suit le nombre de jetons : `JsonElement.ParseValue`, qui supprime la copie intermédiaire, n'a rien changé de mesurable (155/142/128/112 ms contre 161/141/138/110 avant). Compacter les noms de champs du diff (`change`/`entry`/`kind`/`materialId`… lus par SYNE) réduirait les octets d'environ 40 %, soit ≈ 250 ms de parse par tick, mais c'est une **rupture de protocole** : version de contrat, format de checkpoint et lecteurs SYNE à faire évoluer ensemble. Décision du 14 novembre : non, pas maintenant — le levier restant est le tick hôte (582 ms) et la perception (178 ms), pas l'encodage.

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
| **B9** | Tick couplé SYNE ↔ PRISM : harnais unique et reproductible | Médianes et p95 par phase à 100 / 500 / 1 000 agents, allocations, pause GC, transport ; 3 exécutions après chauffe, empreintes stables. Livré le 11 novembre 2026 sous [`benchmarks/B9-coupled-tick-2026-11-11-v1.json`](benchmarks/B9-coupled-tick-2026-11-11-v1.json) ; chiffres en §2 |

Chaque benchmark a un **budget**, un **seuil d'alerte** et un **rapport** versionné (`docs/prism/benchmarks/`), à la manière de `docs/echos/BENCHMARKS.md`. B9 est le seul mesuré sur le tick **complet** couplé : ses chiffres remplacent les postes synthétiques de B1 et B2 dans la comparaison au budget.

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
