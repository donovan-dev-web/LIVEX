# Profilage du tick couplé P9 — où part le temps (10 novembre 2026)

**Question** : le tick couplé coûtait 2 à 6 s à 20–100 agents, alors que le budget de référence est de 50 ms à 100 agents et que la cible est le temps réel à 1 000 agents. Où part ce temps, et que peut-on corriger sans changer les trajectoires ?

**Réponse courte** : ni SYNE ni le transport n'étaient en cause. Le tick était dominé par deux calculs de l'hôte — la perception (1 089 cases relues par agent et par tick) et le déplacement (une recherche A\* complète par pas d'agent) — plus un artefact de mesure : les checkpoints automatiques gonflaient la moyenne des derniers ticks, ce qui faisait croire à une dégradation dans le temps. Après correction : **84 ms/tick à 100 agents immobiles** (contre 620) et **705 ms/tick à 100 agents en production** (contre 2 150), à comportement identique.

## 1. Méthode : trois mesures, pas une

Un chiffre global ne dit pas où creuser. Trois niveaux ont été instrumentés, tous opt-in et gratuits quand désactivés :

| Niveau | Outil | Ce qu'il sépare |
| :-- | :-- | :-- |
| SYNE seul, sans PRISM | `--benchmark` existant (`TickPhase`) | la cognition : 4,6 ms/tick à 100 agents, 40 ms à 1 000 |
| Boucle couplée côté SYNE | `SYNE_COUPLED_PROFILE=1` (nouveau) | décision, appel PRISM, application des observations, taille du flux reçu |
| Hôte PRISM | `PRISM_TICK_PROFILE=1` (étendu) | les 12 phases du monde, le hors-monde du runtime, le handler HTTP, chaque écriture du monde, GC et allocations |

Le troisième niveau a été complété pour mesurer ce que les phases ne voient pas : horodatages absolus (ms Unix) autour du handler `/data/tick`, durée et étiquette de chaque écriture différée (`agent 4 Move`), temps de pause GC et octets alloués par tick.

## 2. Ce que la mesure a éliminé

- **La cognition SYNE n'est pas le problème.** En local : 4,6 ms/tick à 100 agents, 40 ms à 1 000 (25 t/s), dont perception locale 0,8 / 10,3 ms. Dans la boucle couplée, la décision SYNE (délibération + write-back) coûte 2,6 ms/tick à 20 agents, et ne croît pas.
- **Le transport n'est pas le problème.** Le flux reçu décroît (817 Ko au tick 0, 5 Ko au tick 39) ; l'attente des en-têtes suit exactement la durée du handler côté hôte.
- **« Le coût croît avec la durée du run » était un artefact.** Le dernier tick d'un run coûtait 4,2 s : c'est le **checkpoint automatique d'arrêt** (`checkpointOnStop`, plus un checkpoint toutes les 500 ticks), sérialisé dans le même handler. Les ticks 20 à 38 étaient stables à ~110 ms. Toute moyenne incluant la fin du run surestimait donc le coût, et plus le run était court, plus l'erreur était grande — d'où l'impression de dégradation.
- **Le GC n'est pas le problème.** 14 à 29 ms de pause par tick, pour 220 Mo alloués : l'allocation massive était un symptôme, pas la cause.

## 3. Cause n°1 — la perception relisait tout, tout le temps

`PerceptionSystem.Observe` construisait, pour chaque agent et chaque tick, un `SortedDictionary` de ~1 089 cases (rayon 16), le comparait à un second arbre trié, puis reconstruisait un troisième arbre pour l'état suivant. Coût mesuré : **6,2 ms par agent et par tick, constant** (6,5 à 20 agents, 6,2 à 100, 5,8 à 200 — donc bien proportionnel aux cases, pas aux agents).

Or le monde ne change qu'en quelques cases par tick. Correctifs, à comportement identique (les changements émis sont triés par le même comparateur, donc octet pour octet les mêmes) :

1. **Fenêtre de blocs mémorisée par agent**, invalidée seulement par `WorldGrid.Revision` ou par le changement de case de l'agent.
2. **Dictionnaires au lieu d'arbres triés** pour la fenêtre, l'état connu et le travail du tick, tous réutilisés (aucune allocation au régime établi).
3. **Diff appliqué en place** : plus de reconstruction de l'état connu, plus de `HashSet` de clés envoyées, tri unique au lieu de trois `OrderBy` chaînés.

| Scénario (25 ticks, profil hôte) | Avant | Après | Gain |
| :-- | --: | --: | --: |
| 100 agents immobiles — perception | 618 ms | 82 ms | 7,5× |
| 200 agents immobiles — perception | 1 160 ms | 71 ms | 16× |
| 100 agents en production — perception | 191 ms | 154 ms | 1,2× (les agents bougent : cache invalidé) |

## 4. Cause n°2 — une recherche A\* par pas, et des recherches impossibles

`AgentLifecycleSystem.TryMoveOneStep` relançait une recherche A\* **complète à chaque tick et pour chaque agent qui marche**, avec un budget de nœuds égal au monde entier (65 536). Une cible inatteignable épuisait donc ce budget à chaque tick : mesuré **1 s par recherche** sur les écritures `agent 4 Move` et `agent 94 Move`, deux agents par tick, soit 2 s/tick à eux seuls.

Correctifs, tous neutres sur les trajectoires (422/422 tests PRISM verts, campagnes à empreinte comprises) :

1. **A\* en tableaux indexés par case** (`y × largeur + x`) avec marqueur de génération, au lieu de deux dictionnaires de nœuds : 15 µs → 2,7 µs par nœud.
2. **Voisins énumérés sans allocation** (`MovementGraph.Neighbors` : structure enumerator au lieu d'un itérateur `yield`).
3. **`CanStandAt` mémoïsé** par révision de grille (une case est interrogée jusqu'à huit fois par recherche) : le coût par nœud tombe encore, et 233 000 nœuds passent de 1 820 ms à 640 ms.
4. **Cache de chemin par agent** (suffixe validé, échec mémorisé) invalidé par la révision du monde.

| Scénario (25 ticks, profil hôte) | Avant | Après | Gain |
| :-- | --: | --: | --: |
| 100 agents en production — `ApplyWrites` | 1 900 ms | 514 ms | 3,7× |
| 100 agents en production — tick complet | 2 134 ms | 705 ms | 3,0× |
| Run couplé 100 agents × 25 ticks, bout en bout | ≈ 2 500 ms/tick | ≈ 1 300 ms/tick | ≈ 2× |

Une piste a été essayée puis **retirée** : borner le budget A\* par la distance (32 nœuds par case, plancher 2 048). Elle cassait deux empreintes de campagne épinglées (`random`, graines 1 et 3) : ce n'est donc pas une optimisation neutre, et la re-épingler aurait masqué un changement de comportement.

## 5. État après cette passe

| Scénario | Avant | Après | Cible |
| :-- | --: | --: | :-- |
| 100 agents immobiles | ~620 ms/tick | **84 ms/tick** | 50 ms |
| 200 agents immobiles | ~1 160 ms/tick | **73 ms/tick** | — |
| 100 agents en production | ~2 150 ms/tick | **705 ms/tick** | 50 ms |
| 1 000 agents (extrapolation immobile) | ~5–6 s/tick | **~0,8 s/tick** | ≥ 4 ticks/s |

Le budget de 50 ms à 100 agents n'est pas atteint en production, et le temps réel à 1 000 agents n'est pas acquis. Mais on est passé d'un coût **dominé par deux calculs quadratiques** à un coût **dominé par une seule recherche de chemin**, dont la cause est nommée.

### 5 bis. Mesures à l'échelle (production, chaînes complètes, 10 ticks)

| Agents | tick complet | dont `ApplyWrites` | dont perception | écritures/tick | alloué/tick | GC/tick | réponse |
| --: | --: | --: | --: | --: | --: | --: | --: |
| 100 | 705 ms | 514 ms | 154 ms | ~40 | 220 Mo | 15–29 ms | 5 Ko |
| 500 | 5 302 ms | 3 904 ms | 1 245 ms | 196 | 1 333 Mo | 109 ms | 3,8 Mo |
| 1 000 | 9 556 ms | 6 492 ms | 1 801 ms | 423 | 1 986 Mo | 217 ms | 8 Mo (40 Mo au tick 0) |

Deux constats que ces paliers rendent visibles :

1. **Le coût par agent augmente avec la population en production** (7 ms à 100, 10,6 ms à 500, 9,6 ms à 1 000 par agent-tick), parce que la proportion d'agents en déplacement augmente et que chaque pas coûte une recherche A\* (15 ms par écriture à 1 000 agents, 400 ms pour la plus lente).
2. **Le transport devient un mur avant le calcul.** À 1 000 agents, la réponse par tick passe de 40 Mo (tick 0) à 8 Mo, et le run **s'arrête au tick 9** sur un rejet `HTTP 400 invalid_request — « The JSON value of length 222 938 004 is too large and not supported »`. Aucun run de 2 500 ticks à 1 000 agents n'est donc possible aujourd'hui, quelle que soit la vitesse du tick.

**Relevé du 11 novembre 2026 (PRISM-142).** Le relevé de taille de réponse de l'hôte (`payloadChars` alors, `payloadBytes` depuis le 14 novembre 2026, où la mesure porte sur les octets réellement écrits), sur le même scénario (1 000 agents, graine 7, pack `minimal`, production 5 objectifs, 25 ticks), ne reproduit **plus** l'arrêt : 25/25 ticks, empreinte `05a214079ff47bab`, 25 000 observations. Courbe mesurée : 40,9 / 40,6 / 40,2 / 23,6 Mo, puis 6,0 à 9,3 Mo. Le mur des 222 Mo n'est donc pas reproductible dans cette configuration — le chiffre du profil venait d'un run antérieur, sans relevé de taille par tick. Le constat de fond reste : **quatre réponses sur vingt-cinq dépassent 8 Mo**, et le tick 0 en demande six fois plus ; c'est la borne, pas l'arrêt, qui justifie PRISM-142 (ADR-014).

### 5 ter. L'hypothèse des caches est vérifiable — et elle tient (PRISM-108)

Les quatre caches de cette passe (`AgentView`, `_paths`, `CanStandAt`, `SearchScratch`) supposent que **tout effet sur le monde s'applique par une écriture de grille**, donc que `WorldGrid.Revision` suffit à les invalider. Ce n'était qu'une hypothèse : un effet qui modifierait la praticabilité sans écrire la grille aurait fait mentir le mémo en silence — pas d'exception, pas de test rouge, juste une trajectoire fausse.

`PRISM_VERIFY_CACHES=1` recalcule sans mémo, à chaque hit, la fenêtre de blocs de perception, le masque de tags de région et le mémo `CanStandAt`, et échoue sur la première divergence en nommant le cache, le tick, l'agent et la case. Mesure sur le même scénario (Release, 100 agents gloutons, graine 1, 25 ticks) :

| Mode | 25 ticks | Par tick | Empreinte |
| :-- | --: | --: | :-- |
| Désactivé | 7 532 / 7 547 / 7 608 ms | ≈ 0,30 s | `a7e6160916f90c42` |
| Activé | 37 382 / 40 272 / 42 526 ms | ≈ 1,61 s | `a7e6160916f90c42` |

L'empreinte est identique : le mode ne change pas la sortie, il la vérifie. Et les suites passent avec le mode **actif** (Core 199, Content 51, Host 72, Bots 23, dont les neuf empreintes de campagne dorée et celle de R1) : aucun cache ne ment sur les chemins réels. Le coût, ≈ 5,3×, en fait un mode de diagnostic à activer quand on touche à un cache, pas un mode de production.

### 5 quater. La fenêtre glissée — avant/après à 1 000 agents (PRISM-105)

La fenêtre de blocs d'un agent était déjà mémorisée (PRISM-109, clé = révision des régions couvertes), mais elle était **reconstruite intégralement dès que l'agent bougeait d'une case** : 1 089 cases relues pour n'en découvrir 33 à 66. `PerceptionSystem` glisse maintenant la fenêtre quand les régions couvertes n'ont pas été écrites et que le pas vaut au plus une case sur chaque axe : les cases qui quittent le disque sont retirées, la bande entrante est relue. Les huit directions sont couvertes — un pas vertical ou diagonal ajoute la différence exacte des intervalles de chaque ligne, parce que la demi-largeur d'une ligne varie avec sa distance au centre ; tout saut ou changement de rayon retombe sur un rescan complet, compté par `BlockWindowMoveRebuilds`.

Mesure : harnais de PRISM-106, graine 7, contenu `793A8996…`, production `stone_pickaxe,chest,wooden_bucket,stone_hoe,furnace`, trois exécutions après chauffe. « Avant » = `B9-coupled-tick-2026-11-14-v1.json` (état après PRISM-104, 109 et 141) ; « après » = `B9-coupled-tick-2026-11-14-v3-after-105.json`.

| Scénario | Phase | Avant | Après | Gain |
| :-- | :-- | --: | --: | --: |
| 100 agents × 25 ticks | perception | 137,9 ms | **28,9 ms** | ×4,8 |
| 100 agents × 25 ticks | tick complet | 466,6 ms | **377,4 ms** | ×1,24 |
| 1 000 agents × 8 ticks | perception | 1 473,6 ms | **425,2 ms** | ×3,5 |
| 1 000 agents × 8 ticks | `ApplyWrites` (pathfinding) | 103,7 ms | 113,5 ms | — (inchangé) |
| 1 000 agents × 8 ticks | macros | 110,4 ms | 104,0 ms | — (inchangé) |
| 1 000 agents × 8 ticks | tick complet | 2 043,7 ms | **917,3 ms** | ×2,2 |

La cible de PRISM-105 était 400 ms de perception à 1 000 agents : **425 ms mesurés, soit 25 ms (6 %) au-dessus**, dans l'erreur-type du harnais (4,6 %). Le écart se lit dans le profil : une fois la fenêtre glissée, le coût restant par agent n'est plus le scan de blocs mais la relecture des objets au sol (`GroundItemStore.Snapshot` alloue un tableau par agent et par tick) et le parcours linéaire des agents voisins — deux postes de PRISM-141 et PRISM-119. Une mesure intermédiaire, glissade limitée aux pas horizontaux, donnait 715 ms : les diagonales, un pas sur deux, étaient la moitié du gain.

Le gain est **neutre sur la sortie** : `PerceptionSlidingWindowTests` compare, à chaque tick d'un parcours de 26 pas (lignes, colonnes, diagonales), les changements d'une fenêtre glissée à ceux d'une fenêtre reconstruite — identiques octet pour octet — et le mode `PRISM_VERIFY_CACHES=1` reste silencieux sur le chemin glissé (il recalculé la fenêtre sans mémo à chaque hit). Empreintes de campagne et de R1 inchangées : aucun re-épinglage.

### 5 quinquies. Invalidation et cache par colonne — avant/après à 1 000 agents (PRISM-119)

La mesure a d'abord contredit l'hypothèse du ticket. PRISM-105 laissait croire que le coût restant était la reconstruction de la fenêtre quand une écriture tombe dans une région couverte ; les compteurs disent l'inverse : à 1 000 agents sur huit ticks, **1 000 reconstructions (la première), 1 737 glissades, 0 repli sur saut, 0 patch**. Le coût constant par agent dominait donc déjà, et une première passe limitée à l'invalidation par colonne n'a rien donné de mesurable (412,9 ms contre 425,2, dans l'erreur-type de 3,3 %).

La sonde de temps, ajoutée à `PRISM_ALLOC_PROBE=1` (quatre seaux : fenêtre, extras, diff, queue), a donné la répartition : **fenêtre ≈ 200 ms, diff ≈ 100 ms, extras ≈ 50 ms, queue ≈ 10 ms** par tick à 1 000 agents. La fenêtre était la lecture de la bande entrante : 33 à 66 colonnes par agent, chacune relisant ses surcharges dans le `SortedDictionary` global de 593 000 entrées et testant l'exposition de chacune — jusqu'à six recherches d'arbre par bloc. À 1 000 agents, la même colonne était relue jusqu'à 1 000 fois par tick.

Deux changements :

1. `WorldGrid` tient un **compteur d'écriture par colonne**, bumpé pour la colonne écrite et ses quatre voisines cardinales — exactement l'ensemble dont dépend la contribution d'une colonne, puisque le test d'exposition interroge ces quatre voisins. La diagonale n'y est pas : elle ne décide jamais d'une visibilité, la bumper n'invaliderait que du travail.
2. `PerceptionSystem` met en cache la **contribution d'une colonne** (surface + surcharges exposées), indexée sur ce compteur et partagée par tous les agents du système : une colonne est lue une fois par écriture, pas une fois par agent. L'invalidation devient un patch plutôt qu'une reconstruction quand une écriture tombe dans une région couverte : la fenêtre compare ses compteurs de colonne (comparaison de ligne par `SequenceEqual` de `Span<int>`, vectorisée par la BCL, en entiers), relit les seules colonnes changées et ne retombe sur un rescan complet que sur un saut de plus d'une case ou un changement de rayon.

Mesure : même harnais, mêmes scénarios, trois exécutions après chauffe. « PRISM-105 » = `B9-coupled-tick-2026-11-14-v3-after-105.json` ; « PRISM-119 » = `B9-coupled-tick-2026-11-14-v5-after-119.json`.

| Scénario | Phase | PRISM-105 | PRISM-119 | Gain |
| :-- | :-- | --: | --: | --: |
| 100 agents × 25 ticks | perception | 28,9 ms | **18,9 ms** | −35 % |
| 100 agents × 25 ticks | tick complet | 377,4 ms | **355,0 ms** | −6 % |
| 1 000 agents × 8 ticks | perception | 425,2 ms | **317,4 ms** | **−25 %** |
| 1 000 agents × 8 ticks | `ApplyWrites` (pathfinding) | 113,5 ms | 101,3 ms | inchangé (erreur-type 0,4 % sur l'un, 3,5 % sur l'autre) |
| 1 000 agents × 8 ticks | macros | 104,0 ms | 100,6 ms | inchangé |
| 1 000 agents × 8 ticks | tick complet | 917,3 ms | **850,0 ms** | −7 % |

**La cible de 250 ms n'est pas atteinte : 317 ms mesurés, soit 67 ms (27 %) au-dessus.** Le profil dit où : la fenêtre coûte encore ≈ 120 ms (insertion de la bande dans le dictionnaire de chaque agent, plus une lecture de colonne par écriture), le **diff ≈ 100 ms** (870 candidats et 909 entrées connues comparés par agent et par tick, soit ~2 700 recherches de dictionnaire) et les **extras ≈ 50 ms** (parcours linéaire des 1 000 agents par agent, `GroundItemStore.Snapshot`). Les deux premiers sont le prochain levier — un diff incrémental limité à la bande entrante et aux extras — et non un réglage de la fenêtre ; le troisième est l'index spatial d'agents de PRISM-141.

Deux honnêtetés sur la mesure. Le **patch par colonne ne se déclenche pas** sur ce scénario de huit ticks (0 patch, 0 repli) : les agents glissent, et sa valeur n'apparaîtra qu'à 2 500 ticks quand minage et pose écrivent autour d'eux — elle est couverte par tests mais non mesurée. Et le cache de colonne s'appuie sur la règle même du test d'exposition (une colonne et ses quatre voisines) : `PRISM_VERIFY_CACHES=1` le recalcule désormais sans mémo et lève `perception.column` à la première divergence.

Le gain est **neutre sur la sortie** : `PerceptionColumnInvalidationTests` (7 tests) compare un système qui patche et met en cache à un système reconstruit à chaque tick (`slideWindow: false`), changements identiques **octet pour octet** sur un parcours avec écritures, et fige les replis, la dépendance de voisinage et le silence du mode de vérification. Empreintes de campagne inchangées (`fbd59443744b0dbd` à 100 agents, `27f05beab5435150` à 1 000) : aucun re-épinglage.

## 6. Chantiers suivants, dans l'ordre formalisé

Chaque chantier a une issue avec critère mesurable ; l'ordre complet et les dépendances sont dans [`ISSUES.md`](ISSUES.md) §Milestone ph9.

1. **[PRISM-106](ISSUES.md)** — harnais de benchmark reproductible et budgets mesurés. Sans lui, les chiffres ci-dessus ne sont pas rejouables en CI.
2. **[PRISM-107](ISSUES.md)** — empreintes à deux niveaux : une optimisation ne doit pas forcer un re-épinglage.
3. **[PRISM-108](ISSUES.md)** — vérification des caches : l'hypothèse « tout effet de structure écrit la grille » doit être éprouvée.
4. **[PRISM-109](ISSUES.md)** — invalidation par région : la révision globale périme le cache de tous les agents à chaque écriture (0,07 ms/agent à l'arrêt contre 1,8 ms en production).
5. **[PRISM-142](ISSUES.md)** — plafond de payload : le run s'arrête au tick 9 à 1 000 agents (222 Mo).
6. **[PRISM-105](ISSUES.md)** ✅ — perception incrémentale au déplacement : **1 474 → 425 ms** à 1 000 agents (§5 quater), 33 à 66 cases relues au lieu de 1 089 ; cible de 400 ms manquée de 25 ms, le reste étant les extras par agent (PRISM-141/119).
7. **[PRISM-104](ISSUES.md)** — recherche de chemin bornée et mutualisée (68 % du tick ; ~233 000 nœuds/tick presque tous perdus).
8. **[PRISM-119](ISSUES.md)** — invalidation et cache par colonne (SIMD sur la perception) : **425 → 317 ms** à 1 000 agents (§5 quinquies), cible de 250 ms manquée de 67 ms ; le diff par agent (~100 ms) est le prochain levier.
9. **[PRISM-140](ISSUES.md)** — parallélisme par agent à commit ordonné, avec empreintes identiques à 1/2/4/8 workers.
10. **[PRISM-141](ISSUES.md)** — budget mémoire : 2 Go/tick et 217 ms de GC à 1 000 agents coûtent plus que la perception entière.
11. **[PRISM-143](ISSUES.md)** — champs de distance BFS partagés, post-G5 : la seule voie de pathfinding portable sur GPU sans casser le déterminisme.

Hors de ces chantiers, deux points restent ouverts et ne sont pas des optimisations : l'index spatial des agents (`AddNearbyAgents` en O(N²), invisible à 200 agents) et les checkpoints (4,2 s d'arrêt toutes les 500 ticks), à sortir du chemin critique.

## 7. Reproduire

```bash
# 1. cognition seule
cd syne && dotnet run --project Simulation.Console -c Release -- --benchmark \
  --benchmark-populations 100,1000 --benchmark-ticks 40 --seed 7

# 2. hôte profilé (phases, écritures, handler, GC)
cd prism && PRISM_TICK_PROFILE=1 LIVEX_SESSION_TOKEN=<token> dotnet run --project Prism.Host -c Release -- \
  --control-port 8131 --work-dir <dir> --instance-id prof --log-dir <dir>/logs

# 3. boucle couplée avec production
cd syne && SYNE_COUPLED_PROFILE=1 LIVEX_SESSION_TOKEN=<token> dotnet run --project Simulation.Console -c Release -- \
  --prism-url http://127.0.0.1:8131 --prism-agents 100 --prism-ticks 25 --prism-seed 7 \
  --prism-content-hash 793A8996297CF2562ABB112F638995BAD8D40CD843CE8CA4608712257F04AA82 \
  --prism-content-dir <prism>/content/minimal \
  --production stone_pickaxe,chest,wooden_bucket,stone_hoe,furnace

# 4. vérification des caches (PRISM-108) : même scénario, caches recalculés sans mémo
cd prism && PRISM_VERIFY_CACHES=1 dotnet run --project Prism.Bots -c Release -- \
  --agents 100 --bots greedy --seeds 1 --ticks 25 --timings

# 5. les fautes injectées, sans passer par un run complet
cd prism && dotnet test Prism.Core.Tests --filter "FullyQualifiedName~CacheVerification"
```

Le profilage n'écrit rien dans l'état du monde : les empreintes couplées et les empreintes de campagne sont inchangées par son activation. Il en va de même du mode de vérification des caches : il relit le monde, ne l'écrit jamais, et laisse l'empreinte du run identique (mesure ci-dessus).
