# TESTING — Plan de test et de calibrage PRISM

**Composant** : PRISM
**Statut** : [SUITES EN PLACE — PRISM 487/487 ET SYNE 813/813 AU 15 NOVEMBRE 2026 ; TOTAL PROJET 2 119 RÉUSSIS (PRISM + SYNE + ECHOS + LAUNCHER + INSTALLATEUR + `syne-mock`) ; G4 ET G5 FRANCHIES ; G1 OUVERTE NON BLOQUANTE ; EMPREINTES À DEUX NIVEAUX (PRISM-107) ; VÉRIFICATION DES CACHES (PRISM-108) ; PLAFOND DE RÉPONSE PAR TICK ET PAGINATION (PRISM-142) ; BENCHMARKS ET VALIDATION WINDOWS/LINUX DIFFÉRÉS]
**Dernière mise à jour** : 15 novembre 2026
**Dépend de** : [`PERFORMANCE.md`](PERFORMANCE.md), [`DETERMINISM.md`](DETERMINISM.md), [`SYSTEMS_SPEC.md`](SYSTEMS_SPEC.md), `../syne/TESTING.md`
**Source Monographie** : Annexe J.2

---

## 1. Objectifs

1. Prouver que le monde est **correct** (règles, conservation).
2. Prouver qu'il est **reproductible** (déterminisme, reprise).
3. Prouver qu'il est **viable** (agents peuvent y vivre) puis **utile** (la coopération y est mesurable).
4. Éviter la confusion entre « la simulation tourne » et « le monde sert l'hypothèse ».

## 2. Pyramide

| Niveau | Contenu | Outils |
| :-- | :-- | :-- |
| Unitaire | Indices, chunks, hachage, bruit, recettes, durabilité, péremption | xUnit |
| Par propriétés | Conservation, absence de duplication, idempotence, ordre | FsCheck (ou équivalent) |
| Contenu | Validation de pack : schéma, références, graphe de recettes, amorçage à mains nues | Tests du validateur |
| Génération | Mondes dorés, contrôles de jouabilité | Empreintes figées |
| Contrat | Mêmes cas sur liaison réseau et embarquée ; versions inconnues ; échecs motivés | `Prism.Contracts.Tests` |
| Intégration | Host + bots, runs courts | `Prism.Host.Tests` |
| Bots | Runner limité au contrat, empreintes de run, bout-en-bout sur le pack minimal | `Prism.Bots.Tests` |
| Rendu | Faces visibles par région, déterminisme du maillage, overlays de hauteur | `Prism.Viewer.Tests` |
| Bout en bout | SYNE + PRISM + ECHOS via Launcher | `Launcher.Tests.EndToEnd` |
| Performance | B1…B8 | `Prism.Benchmarks` |
| Rendu | Meshing, captures | CI optionnelle |

## 3. Tests de non-régression clés

| Test | Critère |
| :-- | :-- |
| **Monde doré** | Empreinte du monde généré inchangée pour `(seed, worldGenVersion, contentHash)` de référence |
| **Run doré** | Empreintes de run (bots, 5 000 ticks, 3 seeds) inchangées |
| **Conservation** | `MatterLedger` équilibré à chaque intervalle pour tous les runs de test |
| **Reprise** | Run restauré = run continu (empreintes) |
| **Parallélisme** | 1, 2, 8 threads → même résultat |
| **Plateformes** | Windows = Linux |
| **Ablation sans effet de bord** | Un mécanisme désactivé ne modifie pas ce qui n'en dépend pas |
| **Contenu minimal** | Les 5 chaînes sont jouables par un bot scripté de bout en bout |
| **Couverture** | ≥ 80 % sur `Prism.Core` et `Prism.Content` (même seuil que le projet) |

## 3.1 Empreintes à deux niveaux — trajectoire et octets (PRISM-107)

Une seule empreinte confondait deux questions : « la simulation se comporte-t-elle pareil ? » et « le flux d'octets est-il le même ? ». Le 10 novembre 2026, une borne A\* sans changement de sémantique du chemin a cassé deux empreintes de campagne (graines 1 et 3, bot `random`) et il fallait soit re-épingler à l'aveugle, soit renoncer à l'optimisation. Les deux niveaux sont désormais séparés, nommés et comparés séparément.

| Niveau | Fonction | Ce qui est haché | Attrape | Ignore |
| :-- | :-- | :-- | :-- | :-- |
| **Trajectoire** | `RunTrajectory` / `TrajectoryAccumulator` (`prism/Prism.Bots/RunTrajectory.cs`) | Toutes les 100 ticks : par agent son identifiant, sa case (`cellX`, `cellY`, `feetZ`), son état `sheltered` et ses **totaux d'inventaire par type d'objet** ; plus le nombre d'événements publiés par type depuis la frame précédente. Ordre canonique : agents par identifiant, objets par `ItemTypeId`, compteurs par type d'événement | Un agent qui finit sur une autre case, porte autre chose ou disparaît ; un monde qui publie un autre nombre d'événements | L'ordre d'émission des observations et des événements, le découpage des piles, la durabilité et la fraîcheur, la forme sérialisée |
| **Octets** | `RunFingerprint.AppendSerializedTick` (`prism/Prism.Bots/RunFingerprint.cs`) | Le flux complet de chaque tick : le tick, puis chaque événement (type, agent, payload JSON brut) et chaque observation (agent, `changes` et `self` JSON bruts), **dans l'ordre publié** | Toute différence de flux, y compris une simple permutation d'ordre d'itération interne | Rien : c'est le contrat, la reprise et le rejeu bit-à-bit |

**Règles d'usage.**

1. Un travail de performance (PRISM-104, 105, 109, 119, 140, 141) est jugé sur l'empreinte de **trajectoire** : inchangée = accepté, sans re-épinglage.
2. Une empreinte d'**octets** qui bouge alors que la trajectoire ne bouge pas est une différence d'ordre d'émission : elle se constate et se justifie par ce fait, elle n'est pas un changement de comportement.
3. Une **trajectoire** qui bouge est un changement de comportement : il se justifie (rapport, `ISSUES.md` §Mises à jour) et re-épingle les deux niveaux ensemble.
4. Le niveau octets reste le critère du contrat, de la reprise et du rejeu ; il n'est jamais le critère d'une optimisation.
5. Échantillonnage : fenêtres entières de 100 ticks (`RunTrajectory.DefaultIntervalTicks`) ; un run plus court qu'une fenêtre n'a pas de trajectoire (`TrajectoryFrames = 0`) et sa valeur n'est pas interprétable. Les campagnes réduites durent 200 ticks, soit deux frames.
6. Où la lire : `BotRunResult.TrajectoryFingerprintHex` / `BotRunResult.TrajectoryFrames`, et le champ `trajectoryFingerprint` de la sortie JSON de `prism-bots` (toutes modes, R1 compris).

**Preuve.** `TwoLevelFingerprintTests.AReorderedInternalEmissionLeavesTheTrajectoryFingerprintUnchanged` rejoue un run réel (six agents, 120 ticks, graine 1, pack minimal : un planificateur sur la chaîne outils, qui tient plusieurs types d'objets, et cinq gloutons), puis inverse **dans le test** l'ordre des événements, l'ordre des observations et l'ordre des piles d'un inventaire : l'empreinte de trajectoire reste **identique** et l'empreinte d'octets **change** — exactement le cas que PRISM-107 doit rendre lisible. `TheTrajectoryFingerprintMovesWhenAnAgentDoesNotEndWhereItDid` vérifie l'autre moitié du garde-fou : un agent décalé d'une case à la dernière frame casse la trajectoire, donc l'empreinte n'est pas vide de sens. Le test commence par exiger que chaque permutation **morde réellement** — un tick publiant plusieurs événements, un tick publiant plusieurs observations, un inventaire tenant plusieurs piles — sinon la preuve serait vide sans que rien ne le signale (une première version, deux gloutons sur 120 ticks, n'avait ni tick multi-événement ni inventaire multi-piles : les inversions étaient des no-op).

**Classement des empreintes épinglées** (aucune valeur modifiée par PRISM-107) :

| Test | Empreintes | Niveau |
| :-- | :-- | :-- |
| `DeterministicCampaignTests` | neuf cas (3 graines × 3 bots) : `1d57d8c21416379a`, `2290cf914cbad44a`, `435842c3fc778d52` (random) ; `733e128aebc76c3d`, `6896049703c9b10a`, `0181f2dc0e0c3dc5` (greedy) ; `ffde28674bbc6174`, `5444a165b8b0992a`, `fab40e8d19c72371` (planner) | octets |
| `R1CampaignTests` | `70455f3b3b4068e6` (5 agents, 200 ticks, graine 1) | octets |

Toutes datent d'avant la séparation et ont été produites par le flux sérialisé : elles sont donc au niveau octets, y compris les six re-épinglées le 9 novembre 2026 (`733e128aebc76c3d`, `6896049703c9b10a`, `5444a165b8b0992a`, `0181f2dc0e0c3dc5`, `fab40e8d19c72371`, `70455f3b3b4068e6`). Aucune empreinte de **trajectoire** n'est épinglée à ce jour : elle se compare entre deux exécutions (même seed, même contenu), ce que fait `TwoLevelFingerprintTests` et ce que feront les tickets de performance.

## 3.2 Vérification des caches — `PRISM_VERIFY_CACHES=1` (PRISM-108)

Les optimisations du 10 novembre 2026 reposent toutes sur la même hypothèse : **tout effet sur le monde s'applique par une écriture de grille, donc toute mutation incrémente `WorldGrid.Revision`**. Depuis PRISM-109, les caches de perception ne comparent plus la révision globale mais **la révision des régions qu'ils lisent** (`WorldGrid.GetRegionRevision`) : l'hypothèse devient « toute mutation incrémente la révision de la région écrite », plus fine et vérifiée par les mêmes recalculs. Si une mutation passe à côté du compteur, les caches répondent depuis l'ancien état : pas d'exception, pas de test rouge, une trajectoire fausse et une empreinte différente bien plus tard. Le mode rend cette hypothèse **vérifiable**.

| Cache vérifié | Quand | Ce qui est comparé |
| :-- | :-- | :-- |
| Fenêtre de blocs de perception (`PerceptionSystem.AgentView.Blocks`) | à chaque **réutilisation** du cache (révisions des régions couvertes, case et rayon inchangés) | fenêtre recalculée sans mémo par `AddNearbyBlocks`, clé par clé en ordre canonique |
| Masque de tags de région (`PerceptionSystem._regionMasks`) | à chaque **hit** du cache de région | `ComputeExposedRegionTagMask` recalculé sans cache |
| Mémo `CanStandAt` (`MovementGraph`) | à chaque **hit** du mémo, pendant les recherches de chemin et entre deux appels hors recherche | `CanStandAtCore` recalculé sans mémo (verdict et hauteur des pieds) |

**Comportement en cas de divergence.** La première divergence lève un `CacheVerificationException` : elle nomme le cache (`perception.window`, `perception.regionMask`, `movement.standMemo`), le tick, l'agent et la case (ou la région), puis le run échoue — pas de log silencieux. La case signalée est la première en ordre canonique (genre, puis `Z`, `Y`, `X`), donc deux exécutions signalent la même.

**Activation.** `PRISM_VERIFY_CACHES=1` dans l'environnement : l'hôte (`PrismHostRuntime`) lit la variable une fois au démarrage et la passe à `AgentLifecycleSystem`, qui la propage à `PerceptionSystem` et `MovementGraph` — y compris au graphe recréé par une restauration de checkpoint. En test, les constructeurs prennent le drapeau : `new PerceptionSystem(verifyCaches: true)`, `new MovementGraph(world, rules, provider, verifyCaches: true)`, `new AgentLifecycleSystem(world, rules, perceptionRules, settings, verifyCaches: true)`.

**Ce que le mode ne fait pas.** Il n'écrit jamais dans le monde ; désactivé, il ne recalcule rien et ne change ni une trame ni une empreinte. Activé, il ne change **pas** la sortie non plus : le run de référence 100 agents × 25 ticks rend la **même empreinte** `a7e6160916f90c42` avec et sans le mode.

**Coût mesuré** (11 novembre 2026, Release, 100 agents gloutons, graine 1, pack minimal, hôte local — `prism-bots` sans `--r1`, trois exécutions) :

| Mode | 25 ticks | Par tick | Empreinte |
| :-- | --: | --: | :-- |
| Désactivé | 7 532 / 7 547 / 7 608 ms | ≈ 0,30 s | `a7e6160916f90c42` |
| Activé | 37 382 / 40 272 / 42 526 ms | ≈ 1,61 s | `a7e6160916f90c42` |

Soit ≈ 5,3× le coût du tick : c'est un mode de diagnostic, pas un mode de production. Aucun budget n'est imposé ; le chiffre existe pour qu'on sache ce qu'on paie.

**Preuve que l'hypothèse tient — et que le mode détecte quand elle ne tient pas.** `CacheVerificationTests` :

| Test | Ce qu'il montre |
| :-- | :-- |
| `PerceptionWindowLiesSilentlyWhenTheWorldMovesWithoutARevisionBump` | faute injectée (bloc écrit puis compteur de révision rembobiné) : **sans** le mode, aucun changement n'est signalé et l'agent ne voit pas le bloc — le mensonge silencieux du ticket |
| `VerificationNamesTheCacheTheTickTheAgentAndTheDivergentCell` | **avec** le mode, la même faute lève `CacheVerificationException` avec `perception.window`, tick 1, agent 1 et la case |
| `StandMemoLiesSilentlyWhenTheWorldMovesWithoutARevisionBump` | même faute côté mémo `CanStandAt` : la réponse périmée est rendue sans bruit |
| `VerificationCatchesTheStandMemoAtTheDivergentCell` | avec le mode : `movement.standMemo`, la case et le tick |
| `VerificationStaysQuietWhenEveryWriteBumpsTheRevision` | une écriture légitime (pose, retrait, invalidation) ne déclenche rien : le mode ne crie pas au loup |
| `TheModeIsOffUnlessTheEnvironmentAsksForIt` | la variable n'active le mode que sur la valeur `1` |

Depuis PRISM-109, la **clé** elle-même est testée, indépendamment du mode : `PerceptionRegionInvalidationTests` et `StandMemoIsBoundToTheRegionRevisionOfItsCell` vérifient qu'une écriture dans une région que le cache ne lit pas ne le reconstruit pas, qu'une écriture dans la région voisine **cardinale** reconstruit bien le masque de tags (l'exposition lit les quatre voisins) et qu'une écriture dans la région **diagonale** ne le reconstruit pas. Les compteurs `PerceptionSystem.BlockWindowBuilds`, `PerceptionSystem.RegionMaskRebuilds` et `MovementGraph.StandMemoHits` existent pour rendre cette propriété observable ; ils ne sont jamais lus par la simulation. Depuis PRISM-105, deux compteurs de plus rendent la fenêtre glissée mesurable sans changer une seule sortie : `PerceptionSystem.BlockWindowSlides` (fenêtres glissées d'un pas) et `PerceptionSystem.BlockWindowMoveRebuilds` (pas qui ont dû reconstruire : écriture dans une région couverte, saut de plus d'une case, changement de rayon). `PerceptionSlidingWindowTests` est le garde-fou de l'optimisation : il refait un parcours de 26 pas (lignes, colonnes, diagonales) avec deux perceptions, l'une qui glisse et l'autre reconstruite à chaque tick (`new PerceptionSystem(slideWindow: false)`), et exige des changements **identiques octet pour octet** à chaque tick ; il fige aussi les trois replis et le silence de `PRISM_VERIFY_CACHES=1` sur le chemin glissé. Le constructeur porte le drapeau plutôt que l'environnement : une optimisation ne doit pas dépendre d'une variable pour être comparée à sa référence. Depuis PRISM-119, `PerceptionSystem.BlockWindowPatches` compte les fenêtres relues colonne par colonne après une écriture dans une région couverte, et le cache de contribution de colonne est vérifié par le mode sous le nom `perception.column` : `PerceptionColumnInvalidationTests` (7 tests) compare un système qui patche et met en cache à un système reconstruit à chaque tick, changements identiques octet pour octet sur un parcours avec écritures, et fige la dépendance aux quatre voisines cardinales, le repli sur un saut, la réutilisation d'une fenêtre quand l'écriture reste hors du disque et le silence du mode de vérification. La sonde `PRISM_ALLOC_PROBE=1` imprime en plus quatre durées (fenêtre, extras, diff, queue) : c'est elle qui a montré que le coût restant était le diff et non la fenêtre.

**Suites avec le mode actif** (11 novembre 2026) : Core 199/199, Content 51/51, Host 72/72, Bots 23/23 — **les neuf empreintes de campagne dorée et celle de R1 restent identiques**, donc aucun cache ne ment sur les chemins réels. Bots passe de 2 min 05 à 4 min 14 : le surcoût est celui du recalcul, pas d'un changement de comportement.

**Limites.** Le mode ne couvre que la fenêtre de bloc, le masque de région et le mémo `CanStandAt` : le cache de chemins (`AgentLifecycleSystem._paths`) et le `SearchScratch` de `Pathfinding` n'ont pas de recalcul équivalent. PRISM-109 a gardé `_paths` sur la révision **globale** et l'a écrit dans le code : le résultat d'un A\* dépend de chaque case qu'il a évaluée, pas seulement du chemin retenu, donc une invalidation par région exigrait que la recherche enregistre les régions qu'elle a lues — c'est le travail de PRISM-104, qui reprend la recherche. Le tick affiché pour `movement.standMemo` est celui de la dernière phase de perception (les déplacements d'un tick s'exécutent après elle) ; il vaut « unknown » si l'appelant n'a fourni aucun tick.

## 4. Runs de référence

| Run | Agents | Ticks | Rôle |
| :-- | :-- | :-- | :-- |
| R0 | bots (marche aléatoire) | 1 000 | Fumée |
| R1 | bots planificateurs, 50 | 5 000 | Validation des chaînes (G4) — **franchie le 9 novembre 2026** sur la forme 5 agents × 2 500 ticks : 5/5 chaînes, audit vert, `abab272bd13e6ba3`. La forme 50 agents × 5 000 ticks reste le gate de montée en charge (manuelle) |
| R2 | agents SYNE (adaptateur minimal), 50 | 5 000 | Validation du contrat avec la cognition actuelle (G3) — **concluante le 6 novembre 2026** (deuxième passe, après incarnation et ADR-013) : `minds = 50/50`, 250 000 observations positionnées, empreinte `0bf833f4bcfc1e5e` reproduite sur **trois hôtes distincts** ; reprise couplée (checkpoint `resA-299` → restauration → 300 ticks) = `44c2ea085d112e05`, identique au run continu de 600 ticks. Critère de succès : `minds > 0` en fin de run. La première passe (`079b736c91aae957` × 3, `minds = 0`) mesurait un état SYNE local faute de commande `Incarner` ; elle reste consignée. **Write-back branché (troisième passe, 6 novembre 2026)** : 10 agents × 1 500 ticks, seed 7, pack minimal — 388 commandes émises, 391 `action_completed`, 22 `path_not_found`, **3 832 déplacements observés**, 559 gisements exposés (bois), empreinte `b544dadd2d55f676` reproduite 3/3 sur le même hôte. Critère de succès : `minds > 0` **et** déplacements > 0. Limites : les désirs sous le profil `prism` restent majoritairement `Idle` (57 %) ; la mer du pack minimal est enterrée (niveau 26 sous un plateau à ~56), donc aucun gisement buvable n'existe et `Consommer` n'est couvert que par les tests de contrat ; coût ≈ 290 ms/tick à 50 agents, dont ≈ 60 ms fixes côté hôte PRISM. |
| R3 | agents SYNE refondus, 100 | 5 000 | Run de référence V2 (G5) — **produite et reproductible depuis le 14 novembre 2026** : 100 agents × 5 000 ticks, deux passes, empreinte `855fdc2e1387e8d3` identique, 25 chaînes terminées par passe, 0 abandon, coût propre à SYNE 30,5 ms/tick sous le budget de 50 ms. Pour mémoire, au 10 novembre la passe n'était pas produite : le tick couplé coûtait 2 s à 100 agents sans commande et ≈ 5 s avec production, un coût croissant avec la durée du run contre 50 ms de budget, et une passe aurait demandé ≈ 7 h. La forme réduite **20 agents × 120 ticks, deux passes, empreinte `233d76e194c897cf` identique** avait servi de preuve de reproductibilité et de capacité (5 chaînes). Voir [`G5-R3-REPORT.md`](G5-R3-REPORT.md). La procédure et le script sont dans [`G5-R3-PROCEDURE.md`](G5-R3-PROCEDURE.md) (`python scripts/prism-g5-r3.py`) |
| R4 | agents SYNE refondus, 1 000 | 2 000 | Charge (G5/P12) |
| R5 | ablations | 10 000 | Effet des mécanismes (G7) |

> **Durée.** Les runs de référence actuels du dépôt sont de **2 500 ticks** (ADR-016 de SYNE), insuffisants pour un cycle agricole complet (plusieurs centaines de ticks par culture, ≈ 1 920 ticks par saison). Les runs de validation V2 durent **au moins 5 000 ticks**. (Le brouillon évoquait 400 ticks : valeur antérieure aux campagnes d'octobre.)

## 5. Critères de viabilité (hypothèses à confirmer)

Évalués sur 3 graines (`12345`, `424242`, `999`), profil `reference`, 100 agents, 5 000 ticks.

| # | Critère | Seuil de départ |
| :-- | :-- | :-- |
| V1 | Pas d'extinction | 0 extinction sur 3 graines |
| V2 | Survie | ≥ 70 % des agents vivants à 5 000 ticks |
| V3 | Les chaînes sont jouées | ≥ 80 % des agents ont fabriqué un outil de palier 1 ; ≥ 1 outil de palier 2 par run |
| V4 | Agriculture effective | ≥ 1 récolte complète par saison ; ≥ 50 % de la nourriture consommée issue de production |
| V5 | Pas de duplication | Audit de conservation vert |
| V6 | Pas de blocage | < 5 % des agents sans action utile pendant 200 ticks consécutifs |
| V7 | Exploration | Couverture de ≥ 60 % des chunks par la mémoire spatiale collective |

## 6. Expériences d'ablation (test de l'hypothèse)

Chaque expérience compare le profil `reference` à un profil où **un mécanisme** est désactivé (`CONFIGURATION.md` §3), sur ≥ 5 graines, 10 000 ticks.

| Ablation | Mécanisme retiré | Observable attendu s'il compte |
| :-- | :-- | :-- |
| A1 | Structures partagées / coûts fixes | Moins de structures collectives, plus d'autarcie |
| A2 | Maîtrise (division du travail) | Moins de spécialisation et d'échanges |
| A3 | Péremption | Moins de recours aux greniers, moins de cuisson |
| A4 | Limite de transport | Moins de logistique, moins de dépôts partagés |
| A5 | Usure des outils | Baisse de la demande et des échanges d'outils |
| A6 | Partage d'information (messages locaux) | Découverte plus lente des ressources |
| A7 | Savoir riche (compare à simple) | Plus de formation de groupes pour la transmission |
| A8 | Usure des structures | Pas de dilemme d'entretien collectif |

**Lecture.** ECHOS compare les distributions (taille des groupes, indice de coopération, inégalité des stocks, spécialisation, usage des infrastructures) entre profils. Un effet doit dépasser la variabilité inter-graines (critère statistique défini dans `ECHOS/EXPERIMENT_COMPARISON.md`). **Un résultat nul ou négatif est un résultat** : il est publié avec ses limites.

## 7. Calibrage

Procédure itérative, comme les ADR-014/015/016 de SYNE :

1. Fixer un paramètre de `CONTENT_SPEC.md` §7 ; 2. lancer R1 sur 3 graines ; 3. lire V1…V7 ; 4. consigner l'itération dans un **ADR de calibrage** (`ADR-0xx-calibration-…`) ; 5. figer les empreintes dorées si un paramètre change. Ordre conseillé : durée des extractions → capacité de transport → croissance et eau → péremption → coûts de structures → maîtrise → seuils d'usure.

## 8. Mock et bots

Le mock Node.js de SYNE ne sert plus. `Prism.Bots` fournit quatre bots : *marche aléatoire* (fumée), *glouton* (bouclage simple besoin → source), *planificateur* (chaînage arrière sur recettes, sert aussi de **référence** pour le futur planificateur de SYNE) et *inactif* (`idle`, mesure du coût du tick sans commande). Les bots ne remplacent pas la validation contre le SYNE réel.

## Mises à jour

| Date | Changement | Motif |
| :-- | :-- | :-- |
| 15 novembre 2026 | **Passe de cohérence documentaire.** R3 est décrite comme **produite et reproductible** (`855fdc2e1387e8d3`, 100 agents × 5 000 ticks), l'ancienne phrase « non produite au 10 novembre » est conservée comme contexte historique daté ; la mesure B9 du 15 novembre est relue en **erreur-type** (total 5,15 %, ApplyWrites 5,49 %, perception 14,78 % non concluante) ; les tickets ECHOS V2 sont cités sous `ECHOS-120…123` / `ECHOS-130` ; le plan d'implémentation est [`../governance/IMPLEMENTATION-PLAN-2026-11-15.md`](../governance/IMPLEMENTATION-PLAN-2026-11-15.md) | Les portes, les compteurs et les identifiants doivent se lire d'un seul tenant ; une phrase historique non datée se confond avec un état courant |
| 15 novembre 2026 | **Relevé complet des suites et total projet.** PRISM **487/487** (Architecture 8, Contracts 23, Viewer 62, Core 242, Content 51, Bots 23, Host 78) ; SYNE **813/813** (Core 600, Console 213) ; ECHOS **419 réussis, 14 ignorés**, couverture 91,14 % (lot P10 inclus) ; Launcher **229 réussis, 4 ignorés** (Unit 174, Integration 20, E2E 35) ; Installateur **120 réussis** (Unit 73, App 34, Integration 7, E2E 6) ; `syne-mock` **51 réussis, 2 ignorés**. **Total projet : 2 119 réussis, 20 ignorés, 2 139 collectés.** Les 4 ignorés du Launcher sont dynamiques (`LIVEX_SYNE_PUBLISHED_ROOT` absent), pas des régressions | Les documents portaient trois totaux PRISM différents (423, 444, 477) pour une réalité de 487, et aucun total projet n'existait. Un compteur unique, daté et par composant remplace les chiffres dispersés |
| 11 novembre 2026 | **PRISM-142 : plafond de 8 Mio par réponse de tick et pagination à curseur** — trois familles de tests : contrat (`TickPagingContractTests`, 8) : position/curseur d'une page, dernière page complète, refus d'une page qui se prétend complète trop tôt, refus d'une page incomplète sans curseur, refus d'observations d'un autre tick, **refus de désérialiser une page comme un `TickResult`**, plafond à 8 Mio ; hôte (`TickPayloadCeilingTests`, 6) : pagination et recollement à l'octet près au-dessus du plafond, `413` sans corps partiel pour un élément insécable, curseur d'un tick précédent refusé, curseur illisible refusé, pagination transparente sur le monde réel (même flux, même empreinte de monde, plafond de test à 96 Kio pour forcer plusieurs pages), et **acceptation 1 000 agents × 25 ticks** (aucune réponse au-dessus de 8 Mio, huit premiers ticks identiques au run de référence de 8 ticks ; ≈ 47 s) ; client (`PrismClientTests`, 2) : drainage complet puis flux reconstruit dans l'ordre annoncé, et refus d'une séquence trouée (`PrismTickPageSequenceException`) au lieu d'un tick partiel. Suites **PRISM 444/444**, **SYNE `Simulation.Console.Tests` 213/213** | Un corps de réponse non borné ne peut pas être couvert par un test de bout en bout à 1 000 agents : le plafond et le refus doivent être prouvés à l'unité, et la non-régression du contenu par un run réel (empreinte inchangée) |
| 11 novembre 2026 | **PRISM-108 : mode `PRISM_VERIFY_CACHES=1`** — sur chaque hit, la fenêtre de blocs de perception, le masque de tags de région et le mémo `CanStandAt` sont recalculés sans mémo et comparés ; la première divergence lève `CacheVerificationException` (cache, tick, agent, case) et le run échoue. Coût mesuré 100 agents × 25 ticks : 0,30 s/tick sans le mode, 1,61 s/tick avec (≈ 5,3×), à empreinte identique (`a7e6160916f90c42`). Suites avec le mode actif : Core 199, Content 51, Host 72, Bots 23 — empreintes dorées inchangées ; sans le mode : **PRISM 430/430**, **SYNE 811/811**. Six tests dans `CacheVerificationTests`, dont la faute injectée (écriture sans bump de révision) | Un cache qui ment ne casse rien visible : il produit une trajectoire fausse et une empreinte différente des heures plus tard. L'hypothèse « tout effet de structure écrit la grille » devait être éprouvée avant d'ajouter d'autres caches (PRISM-105, 109) |
| 11 novembre 2026 | **PRISM-107 : empreintes à deux niveaux** — `RunTrajectory` (trajectoire : cases, inventaires agrégés par type, `sheltered`, compteurs d'événements par type toutes les 100 ticks) séparée de `RunFingerprint.AppendSerializedTick` (octets : flux complet dans l'ordre publié), les deux exposées par `BotRunResult` et la CLI `prism-bots`. Les neuf empreintes de `DeterministicCampaignTests` et celle de `R1CampaignTests` sont classées au niveau **octets** et restent **inchangées** ; PRISM-104/105/109/119/140/141 sont désormais jugées sur la trajectoire. `TwoLevelFingerprintTests` prouve qu'un parcours inversé des événements, des observations et des piles d'inventaire laisse la trajectoire identique et déplace les octets. Suites **PRISM 424/424** (Bots 23, deux tests ajoutés), **SYNE 811/811** | Une permutation d'ordre interne obligeait à re-épingler six empreintes, ce qui vidait le garde-fou de son sens et avait bloqué PRISM-104 le 10 novembre 2026 |
| 9 novembre 2026 | **G4 franchie et suites recalées** : R1 complet (5 × 2 500, graine 1) **5/5 chaînes**, audit vert, `cropRipeEvents` 4, empreinte `abab272bd13e6ba3` reproduite deux fois. Empreintes re-épinglées avec justification : R1 réduite `70455f3b3b4068e6` et cinq empreintes dorées de `DeterministicCampaignTests` (g1 greedy `733e128aebc76c3d`, g2 greedy `6896049703c9b10a`, g2 planner `5444a165b8b0992a`, g3 greedy `0181f2dc0e0c3dc5`, g3 planner `fab40e8d19c72371`). `MovementGraphTests` couvre le trou miné praticable ; trois tests de minage/transfert/conteneur visaient le bloc **sous les pieds** de l'agent (`support_required`) et visent désormais un bloc voisin ; `PrismHostRuntimeTests` attend `macro_step_completed` par `AdvanceUntil` au lieu du tick de départ. Suites **423/423** : Core 193, Content 51, Host 72, Bots 22, Viewer 62, Contracts 15, Architecture 8 | Le graphe de déplacement, la pose de station et l'atteignabilité des sources étaient les trois causes racines du dernier écart de la porte ; les tests minutés sous les pieds devenaient faux avec la règle `support_required` du 8 novembre |
| 3 novembre 2026 | Matrice de tests complétée (`Prism.Bots.Tests`) et quatre bots documentés | PRISM-077 |
| 1er novembre 2026 | Création | Refonte V2 |
