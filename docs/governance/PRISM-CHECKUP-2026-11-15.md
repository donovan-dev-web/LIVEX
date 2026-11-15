# PRISM — checkup complet au 15 novembre 2026

**Composant** : PRISM (avec SYNE, ECHOS et Launcher)
**Objet** : établir où en est réellement PRISM V2, ce qui reste pour atteindre l'objectif du projet, et quelles tâches du backlog sont justifiées.
**Méthode** : les documents ont été lus, puis **vérifiés dans le code et par exécution des suites**. Tous les chiffres de ce rapport viennent de l'arbre de travail au 15 novembre 2026 (branche `Feat-Refonte-PRISM-V2`, HEAD `34e6ba5`, 135 fichiers modifiés + 64 non suivis, +6 881/−740 lignes), pas des compteurs affichés dans la documentation.

> **Rapport historique.** Les constats et recommandations ci-dessous ont été
> appliqués dans la passe documentaire du 15 novembre 2026. Les identifiants
> `ECHOS-100…103` / `ECHOS-110` cités ici décrivent l'état **avant**
> renumérotation ; les tickets V2 sont désormais `ECHOS-120…123` et
> `ECHOS-130`. Voir [`LIVEX-CURRENT-STATUS.md`](LIVEX-CURRENT-STATUS.md) et
> [`IMPLEMENTATION-PLAN-2026-11-15.md`](IMPLEMENTATION-PLAN-2026-11-15.md) pour
> l'état courant.

---

## 0. Verdict en dix lignes

1. **Le socle PRISM est solide et vert** : 487/487 tests, chaînes jouées par des bots, déterminisme épinglé, hôte headless, flux de lecture `/data/read`, persistance, contenu et structures.
2. **La documentation est en retard sur le code** : trois totaux de tests différents (423, 444, 477) pour une réalité de **487**, et plusieurs portes annoncées ouvertes qui sont franchies.
3. **Le chemin critique n'est plus PRISM** : c'est **ECHOS** (ingestion de production + les quatre indicateurs) puis le **calibrage du comportement SYNE**.
4. **G1 n'est pas requis par le protocole scientifique** : V1–V7 sont définis à 100 agents, et le run de production R3 coûte **157,7 ms/tick**, pas les 431 ms du banc de bots B9.
5. **Le vrai risque est déjà visible dans R3** : **85 % des décisions sont `Socialize`**, et seules **2 chaînes sur 5** se terminent à 100 agents. Aujourd'hui, V3 (outils) et V4 (agriculture) échoueraient.
6. **Les ablations A1–A8 ne sont pas prêtes** : seules A2 et A8 se projettent directement sur un levier PRISM ; A3/A4/A5 sont des paramètres de contenu, A6/A7 des mécanismes SYNE, A1 n'est pas isolable. Il manque le schéma de profil d'ablation (ECHOS-110).
7. **Les identifiants ECHOS sont en collision** : `ECHOS-100…103` et `ECHOS-110` désignent dans les docs PRISM l'intégration V2, alors que le backlog ECHOS les utilise pour le packaging desktop (livré).
8. **G2 est couvert à 90 %** par la CI Windows/Linux et les tests de jouabilité ; il manque une empreinte de monde **v2** et un rapport daté.
9. **PRISM V2 ne peut pas être orchestré par le Launcher actuel** : le manifeste `type: world` sans `renderCadence` est refusé par `PrismRequirements`, qui attend `immersion`/`renderCadence`.
10. **Objectif réaliste** : 6 à 10 semaines de travail focalisé, dont l'essentiel hors PRISM, pour produire un rapport expérimental conforme au protocole A0-4 — positif ou négatif.

---

## 1. L'objectif réel du projet

L'objectif n'est pas « que PRISM fonctionne », ni « livrer un installateur ». La monographie le fixe au chapitre **2b.4** : produire un **rapport expérimental pérenne** qui applique une définition opératoire de l'émergence faible, avec cinq critères cumulatifs :

1. aucune règle ne nomme ni ne programme la structure observée ;
2. une chaîne `action → intention → objectif → besoin → croyance → mémoire → perception` est reconstruite sur un échantillon ;
3. elle se reproduit sur **K = 5 graines** à configuration identique ;
4. elle disparaît ou change de façon mesurée sous **ablation/contraste préenregistré** ;
5. elle est mesurée par une statistique **définie et seuillée avant le run**, avec une provenance distinguant **mesuré / absent / zéro observé**.

Le protocole exige en outre le **préenregistrement**, l'interdiction de déplacer les critères après coup, et accepte un **résultat négatif**. La V1 a déjà produit un tel rapport (`docs/governance/realignment/EXPERIMENTAL-VALIDATION-2026-10.md`, campagne du 17 octobre 2026 : quête spatiale confirmée 5/5, cycle social non confirmé). **La V2 doit refaire cet exercice dans un monde plus riche**, où la coopération, la spécialisation, les infrastructures et l'inégalité de stocks deviennent observables.

La chaîne de valeur est donc :

```
cognition SYNE  →  monde PRISM  →  mesure ECHOS  →  verdict préenregistré
```

Le reste (viewer, Launcher, installateur, G1) sert la **livraison du produit**, pas la **production du résultat scientifique**.

---

## 2. État vérifié au 15 novembre 2026

### 2.1 Suites exécutées

| Composant | Commande | Résultat | Détail |
| :-- | :-- | --: | :-- |
| PRISM | `dotnet test Prism.sln -c Release` | **487/487** | Architecture 8, Contracts 23, Viewer 62, Core 242, Content 51, Bots 23, Host 78 |
| SYNE | `dotnet test Syne.sln -c Release` | **813/813** | Core 600, Console 213 |
| ECHOS | `.venv\Scripts\python -m pytest` | **393 réussis, 14 ignorés** | couverture **91,24 %** |
| Launcher | `dotnet test Launcher.sln -c Release` | **229 réussis, 4 ignorés** | Unit 174, Integration 20, E2E 35 ; les 4 « échecs » du résumé sont des skips dynamiques faute de `LIVEX_SYNE_PUBLISHED_ROOT` en local, pas des régressions |

Aucune régression n'est donc à déplorer. Le problème n'est pas la santé du code, c'est **ce qui n'est pas encore branché**.

### 2.2 Portes

| Porte | État réel | Preuve / limite |
| :-- | :-- | :-- |
| G0 | Franchie | solution, CI, analyseur d'architecture |
| G1 | **Ouverte, non bloquante** | 431,5 ms/tick à 100 agents sur le banc de bots (budget 50 ms) ; 2,3–2,7 ticks/s contre 20. Le ROADMAP dit explicitement que G1 ne bloque pas l'implémentation fonctionnelle (§1 et ligne de suivi P1) |
| G2 | **Quasi franchie** | hashes dorés identiques Windows/Linux via la matrice CI ; contrôles de jouabilité et viewer v0 testés. Manquent : une empreinte du monde **v2** et un rapport daté |
| G3 | Franchie | contrat 1.0.0 gelé, matrice 23/23, reprise bit-à-bit |
| G4 | Franchie | R1 5 agents × 2 500 ticks, 5/5 chaînes, audit vert, empreinte `abab272bd13e6ba3` |
| G5 | Franchie le 14/11 | R3 100 × 5 000, empreinte `855fdc2e1387e8d3`, coût propre SYNE 30,5 ms/tick |
| G6 | **Ouverte** | ECHOS n'ingère pas le flux PRISM en production ; Launcher refuse le manifeste PRISM V2 ; installateur absent |
| G7 | **Non commencée** | aucune ablation A1–A8 exécutée, aucun rapport V2 |
| G8 | **Non commencée** | release, notices, archivage V1, installation sur machine propre |

### 2.3 Ce qui est réellement prouvé dans le code

- **Monde et contenu** : génération déterministe, `WorldGenVersion = 2`, côte corrigée (`ShoreBand = 0`) pour que l'herbe touche l'eau, preuve d'acceptation `ShorelineAgricultureAcceptanceTests` (3 graines).
- **Chaînes** : outils, agriculture, eau, cuisson, stockage joués par des `PlannerBot` via le contrat, sans plan scripté (G4).
- **Déterminisme** : empreintes à deux niveaux (trajectoire/octets), 9 empreintes de campagne re-épinglées après le changement de monde.
- **Performance** : cache de chemins partagé et invalidé par région (PRISM-104/109 livrées), perception incrémentale (PRISM-105), index spatial et charges utiles typées (PRISM-141 partielle), plafond de payload 8 Mio (PRISM-142).
- **Flux de lecture** : `/data/read` publie `world_snapshot`, `entity_snapshot`, `tick_result`, `checksum` et `metrics`. `AgentEntitySnapshot` contient **l'inventaire de chaque agent**, ce qui rend l'inégalité de stocks et la spécialisation calculables **sans changer le contrat 1.0.0**.
- **ECHOS** : validation et persistance atomique des enveloppes PRISM, route `GET /api/runs/{id}/prism-stream`.

### 2.4 Ce qui n'est pas branché (vérifié, pas supposé)

| Élément | Constat dans le code |
| :-- | :-- |
| Ingestion ECHOS en production | `ingest_prism_stream` n'a **aucun appelant hors tests** ; le run R3 lit la sortie console de SYNE, pas le flux PRISM |
| Indicateurs ECHOS V2 | aucun module de `echos/analysis` ne référence PRISM ni `contentHash` ; `emergence.py` travaille sur les métriques SYNE V1 |
| Comparaison de runs V2 | absente (pas de premier tick de divergence, pas de comparaison par `contentHash`) |
| Champs de distance PRISM-143 | présents mais **désactivés par défaut** (`PRISM_DISTANCE_FIELDS=1` requis) |
| Parallélisme par agent (PRISM-140) | aucun `Parallel.For` dans le tick de l'hôte ; seuls les benchmarks et `RandomTickProcessor` parallélisent |
| Viewer live (PRISM-115) | `ViewerReadStreamClient` existe, mais **aucun `--connect`** dans `Prism.Viewer/Program.cs` |
| Launcher PRISM V2 | `PrismRequirements` exige `type: immersion` (ou `contributesTo: immersion`) + capacités `snapshotStream` et `renderCadence` ; le manifeste PRISM déclare `type: world`, `["headless","snapshotStream"]` → PRISM est verrouillé |
| Parallélisme interplateforme | la CI exécute bien les tests PRISM sur `ubuntu-latest` **et** `windows-latest` avec les hashes épinglés : la comparaison G2 est structurellement faite |

---

## 3. Écarts documentation ↔ code

Chaque ligne est vérifiable dans le fichier cité. La gravité indique le risque pour un lecteur qui prend le document au mot.

| # | Document | Ce qui est écrit | Réalité vérifiée | Gravité |
| :-- | :-- | :-- | :-- | :-- |
| 1 | `docs/prism/README.md:15` | « PRISM 423/423, SYNE 768/768 », G1, G2, **G5**, G6 ouvertes | 487/487, 813/813, **G5 franchie** | Haute |
| 2 | `docs/prism/TESTING.md:4` | « PRISM 444/444 au 11 novembre » | 487/487 au 15 novembre | Haute |
| 3 | `docs/governance/LIVEX-CURRENT-STATUS.md:31` | « PRISM 477/477 (Core 232) » | 487 (Core 242, +10 tests de champ de distance) | Moyenne |
| 4 | `docs/governance/LIVEX-CURRENT-STATUS.md:8` | « état connu au 8 novembre », alors que l'en-tête dit 15 novembre | contradiction interne ; la ligne 24 répète une phrase orpheline | Moyenne |
| 5 | `docs/prism/PRISM_SYNTHESIS.md:4` | « G4 franchie, G1 en attente », 9 novembre | G5 franchie, travail du 15 novembre absent | Haute |
| 6 | `docs/prism/VISION.md:4` | « SPÉCIFICATION V2 — NON IMPLÉMENTÉ » | P0–P11 implémentés, 487 tests | Haute |
| 7 | `docs/prism/ROADMAP.md:4` | baseline B7 du 11 novembre : 4 533 ms à 1 000 agents, 1 744 ms à 100 | B9 du 14–15 novembre : 560 ms à 1 000, 431 ms à 100 (banc de bots) | Moyenne |
| 8 | `docs/prism/CHANGELOG.md` | aucune entrée pour `WorldGenVersion 2` / `ShoreBand 0` | changement non commité qui re-épingle 9 empreintes et rend l'agriculture côtière possible | Haute |
| 9 | `docs/prism/WORLD_GENERATION.md:67` | « toute modification incrémente `worldGenVersion` et **recale les empreintes de mondes dorés** » | `ReferenceWorldSeedsProduceGoldenHashes` reste en `WorldGenVersion: 1` ; aucune empreinte du monde de production n'est épinglée | Moyenne |
| 10 | `docs/prism/ISSUES.md` §4 et §ph10 | `ECHOS-100…103`, `ECHOS-110` = intégration PRISM V2 | dans `docs/echos/ISSUES.md`, `ECHOS-100…103` = shell Electron, UI, PyInstaller, packaging : **livrés**, et `ECHOS-110` **n'existe pas** | Critique |
| 11 | `docs/prism/ISSUES.md` (mesure du 15/11) | « variance 14,8 %, rejeté par le harnais = 10 % » | 14,78 % est l'**erreur-type de la perception**, pas la variance ; l'étendue maximale est 29,56 %. Le total (431,5 ms, SE 5,15 %) et le pathfinding (404,7 ms, SE 5,49 %) sont dans la tolérance | Faible |
| 12 | `README.md` badge | SYNE `0.17.0` | `syne/component.json` = **0.18.0** dans l'arbre de travail | Moyenne |
| 13 | `docs/prism/ROADMAP.md:27` | PRISM-037 : comparaison Windows/Linux « reportée » | la CI la fait sur deux OS avec hashes épinglés | Faible |
| 14 | `docs/prism/ROADMAP.md:29` | PRISM-053 attend « la mesure B1 » | B1 est déclaré supplanté par B9 ; l'attente n'a plus d'objet | Moyenne |
| 15 | `docs/governance/LIVEX-CURRENT-STATUS.md:32` | ECHOS 383 réussis, 91,32 %, 19 octobre | 393 réussis, 14 ignorés, 91,24 % | Faible |

**Règle à appliquer** : conformément à la consigne « une mise à jour de documentation porte sur toute la documentation », ces 15 points doivent être corrigés **dans la même passe**, avec le CHANGELOG comme point d'entrée.

---

## 4. Ce qui reste

### 4.1 Chemin critique scientifique (ordre obligatoire)

#### S1. Committer et remettre la documentation à la vérité — 1 à 2 jours

- Committer le lot courant (487/487 vert) avec un message `feat(prism): ...` décrivant la côte, `worldGenVersion 2`, les champs de distance et les bots.
- Corriger les 15 écarts du §3, y compris les identifiants ECHOS et l'ajout de `ECHOS-110` au backlog ECHOS.
- Ajouter une entrée CHANGELOG pour `WorldGenVersion 2` et `ShoreBand 0`.
- **Critère de sortie** : un seul total de tests par composant, cohérent dans README, TESTING, CURRENT-STATUS, ISSUES, CHANGELOG ; plus aucune porte annoncée ouverte à tort.

#### S2. Brancher l'ingestion PRISM dans ECHOS — 3 à 5 jours

- Un harnais de run qui lance SYNE + `prism-host`, s'abonne à `/data/read`, collecte les enveloppes et appelle `ingest_prism_stream` à la fin du run.
- Persistance atomique, `contentHash`, `seed`, `run_id`, provenance (mesuré/absent/zéro), reprise après incident.
- **Ne pas** passer par le Launcher : un script suffit pour la science, et cela évite de bloquer sur G6.
- **Critère de sortie** : un run réel de 1 000 ticks produit une ligne SQLite interrogeable par `/api/runs/{id}/prism-stream`, et un test d'intégration le rejoue.

#### S3. Implémenter les quatre indicateurs ECHOS-102 — 1 à 2 semaines

Les données existent déjà dans le flux :

| Indicateur | Source dans le flux | Remarque |
| :-- | :-- | :-- |
| Coopération | événements `item_transferred`, contributions de chantier, usage d'effets `use` | distinguer don, échange et contribution |
| Spécialisation | inventaires par agent (`entity_snapshot.agents[].inventory`) + actions réussies | entropie des catégories d'objets produits/portés |
| Infrastructures | `metrics.structures`, `ReservedStations`, `ActiveStructureEffects` | compter les structures achevées et leur usage |
| Inégalité de stocks | inventaires par agent | Gini ou coefficient de variation, avec zéro observé distinct de l'absence |

- **Aucun changement de contrat n'est nécessaire** : `AgentEntitySnapshot.Inventory` est déjà publié.
- **Critère de sortie** : les quatre indicateurs sont calculés sur un run réel, avec un test par indicateur et la provenance à trois états.

#### S4. Comparaison de runs — 2 à 3 jours

- Comparer deux runs par `contentHash`, `seed`, empreinte, et **premier tick de divergence**.
- **Critère de sortie** : deux runs identiques ont un premier tick de divergence nul ; deux runs dont un paramètre change divergent au tick attendu.

#### S5. Run de calibration à blanc — 1 jour

- **1 graine × 1 000 ticks × 100 agents**, avec le vrai couple SYNE/PRISM (pas des bots).
- Mesurer le coût réel du tick et produire les quatre indicateurs.
- **But** : trancher l'écart R3 (157,7 ms) vs B9 (431,5 ms). L'explication la plus probable est la charge réelle : R3 émet **2,2 commandes/tick** (11 227 commandes / 5 000 ticks), tandis que les bots de B9 pathfinding à chaque tick. Si c'est confirmé, G1 n'a aucun rapport avec le coût de l'expérience.
- **Critère de sortie** : un chiffre unique, reproductible, accepté comme base de dimensionnement des campagnes.

#### S6. Calibrer le comportement, pas seulement le contenu — 2 à 4 semaines

C'est le vrai chantier scientifique. R3 montre :

| Décision | Part |
| :-- | --: |
| `Socialize` | **426 554 (85,4 %)** |
| `Flee` | 35 384 (7,1 %) |
| `Explore` | 27 891 (5,6 %) |
| `Produce` | 9 501 (1,9 %) |
| `Idle` | 120 (0,02 %) |

Et **seules 2 chaînes sur 5** se terminent à 100 agents (`chest`, `wooden_bucket`, 25 au total). Les seuils V3 (≥ 80 % des agents fabriquent un outil de palier 1) et V4 (≥ 1 récolte par saison) ne peuvent pas être atteints dans cet état.

- Le calibrage doit porter sur **les deux côtés** : seuils/rendement du besoin social et récupération dans SYNE, durées et rendements dans le contenu PRISM.
- Itérer par ADR de calibrage, sur **3 graines**, en lisant V1–V7 à chaque passe.
- **Critère de sortie** : V1–V7 atteints sur 3 graines, ou un écart documenté et préenregistré avant l'expérience.

#### S7. Préenregistrer le protocole V2 et figer les empreintes — 2 à 3 jours

- Écrire le protocole **avant** les runs : phénomène, métriques, seuils, graines, population, horizon, bras, logique de verdict, provenance.
- Figer les empreintes dorées (PRISM-125), y compris **une empreinte du monde de production en v2**.
- **Critère de sortie** : document daté, seuils non modifiables après le premier run, empreintes épinglées.

#### S8. Exécuter les ablations A1–A8 — 1 à 2 semaines

**Point bloquant vérifié** : le mapping entre le protocole et les mécanismes n'est pas prêt.

| Ablation | Mécanisme à retirer | Où il vit aujourd'hui | État |
| :-- | :-- | :-- | :-- |
| A1 | structures partagées / coûts fixes | PRISM `production`/`storage` (partiel) | non isolable tel quel |
| A2 | maîtrise | PRISM levier `mastery` | prêt |
| A3 | péremption | contenu `container.decayPermillePerTick = 0` | possible, mais global |
| A4 | limite de transport | contenu `agent.inventorySlots` | possible, pas un levier |
| A5 | usure des outils | contenu `tools.*.durability` | possible, pas un levier |
| A6 | messages locaux | SYNE `CommunicationSettings` | possible, non orchestré |
| A7 | savoir riche vs simple | SYNE (profil) | possible, non orchestré |
| A8 | usure des structures | PRISM levier `wear` | prêt |

- **ECHOS-110 doit d'abord définir un schéma de profil d'ablation** couvrant PRISM (leviers + contenu) et SYNE (options), avec un fichier par bras et une empreinte de profil.
- Ensuite seulement : `référence + 8 ablations × 5 graines × 10 000 ticks`.
- **Dimensionnement réaliste** : à 157,7 ms/tick, un run de 10 000 ticks coûte ~26 min ; 45 runs ≈ 20 h en séquentiel, ~5 h sur 4 processus. C'est faisable sans G1.
- **Critère de sortie** : données archivées, profils reproductibles, statistiques calculées.

#### S9. Rapport expérimental pérenne (PRISM-124) — 1 semaine

- Appliquer les cinq critères A0-4, publier le verdict **même négatif**, avec les limites.
- **Critère de sortie** : rapport daté, reproductible, lié aux données et aux profils.

### 4.2 Livraison (nécessaire au produit, pas à l'expérience)

| Chantier | Statut | Recommandation |
| :-- | :-- | :-- |
| **G1** (PRISM-143, 140, 141, 122) | PRISM-143 socle livré mais **désactivé par défaut** ; 140 non commencé ; 141 partiel ; R4 non fiable | Garder **uniquement PRISM-143** : trancher l'ADR (mouvement par classe ou heuristique), mesurer, activer si le gain est prouvé. Repousser 140/141/R4 après le rapport scientifique |
| **G2** | Quasi couvert | Ajouter une empreinte du monde **v2** (3 graines) et un rapport `G2-REPORT.md` daté ; rien d'autre |
| **G6 — voie science** | S2/S3/S4 | C'est le chemin critique ci-dessus |
| **G6 — voie produit** | LAUNCHER-200/201/202, INSTALLER-200 | Corriger d'abord le manifeste PRISM (`type`, `contributesTo`, `renderCadence`) et `PrismRequirements` ; ensuite ports, `.livexp`, arrêt propre |
| **G8** | Non commencé | PRISM-130/131/132/133/134/135 : à faire **après** le rapport, sinon la release sera livrée sans résultat |

### 4.3 Tâches non justifiées (à supprimer, reporter ou recadrer)

| Tâche | Verdict | Justification vérifiée | Remplacement |
| :-- | :-- | :-- | :-- |
| PRISM-039 (B6 génération), PRISM-046 (B3 croissance), PRISM-057 (B1+B2) | **Supprimer** | B6 : la génération prend 2,0–2,4 s par monde, hors du tick et sans effet sur les campagnes. B3 : les mises à jour et la croissance pèsent 0,025 ms/tick dans B9. B1/B2 : `ISSUES.md` déclare B9 référence et B1/B2/B4 synthétiques non représentatifs | PRISM-106/B9 pour B1/B2 ; aucune mesure nécessaire pour B3/B6 |
| PRISM-053 (pathfinding hiérarchique câblé) | **Supprimer** | implémenté mais non câblé ; l'attente « mesure B1 » n'a plus d'objet ; le cache de chemins (PRISM-104/109) a livré le gain ; la hiérarchie peut allonger le chemin et casser les empreintes | rouvrir seulement sur un besoin mesuré |
| PRISM-103 (chaîne eau depuis un monde généré) | **Clore** | G4 a joué la chaîne eau 5/5 ; `ShoreBand = 0` met l'herbe au contact de l'eau ; `ShorelineAgricultureAcceptanceTests` le prouve sur 3 graines ; la prémisse « aucune eau potable atteignable » est fausse | — |
| PRISM-140 (parallélisme par agent) | **Reporter** | aucun parallélisme dans le tick ; le gain visé est un budget d'interactivité, pas le coût de l'expérience ; risque de déterminisme élevé | après G7 |
| PRISM-141 reste (pools) | **Reporter** | GC mesuré à 12,6 ms/tick (2,9 % du tick) à 100 agents ; cible 1 000 agents non pertinente pour V1–V7 | après G7 |
| PRISM-122 / R4 à 1 000 agents | **Retirer du chemin critique** | V1–V7 sont définis à 100 agents ; le scénario 1 000 agents de PRISM-143 est mort au tick 7 dans l'environnement de mesure | option de démonstration G1 |
| PRISM-116 (interventions journalisées) | **Reporter** | le protocole est automatisé (graines, profils) ; aucune intervention manuelle n'est nécessaire | après G7 |
| PRISM-110 à 115, 118 (viewer) | **Reporter** | présentation ; ne produit aucune mesure scientifique | G8 |
| PRISM-117 (indépendance du rendu) | **Garder en P2** | test peu coûteux, garde-fou structurel utile | — |
| MOCK-200 | **Supprimer** | `syne-mock` est un émulateur V1 ; PRISM utilise `Prism.Bots` et le SYNE réel | — |
| PRISM-120/121 (calibrage) | **Garder, recadrer** | le facteur dominant est le mix de décisions SYNE (85 % `Socialize`), pas seulement les paramètres de contenu PRISM | S6 |
| PRISM-123/124/125 | **Garder** | cœur du résultat : ablations, rapport, empreintes | S7–S9 |
| ECHOS-100…103 / 110 (tels que nommés dans PRISM) | **Renuméroter et déplacer** | collision d'identifiants avec le packaging ECHOS livré ; `ECHOS-110` absent du backlog ECHOS | nouvelle série `ECHOS-12x` |

**Bilan** : 4 tâches à supprimer (039/046/057 + MOCK-200), 1 à clore (103), 1 à supprimer (053), 1 à retirer du critique (122/R4), 4 à reporter (140, 141, 116, viewer), 2 à recadrer (120/121, 143), 1 série à renuméroter.

---

## 5. Prochaines étapes, dans l'ordre

| Ordre | Action | Durée | Critère de sortie |
| --: | :-- | --: | :-- |
| 1 | Committer le lot + corriger les 15 écarts documentaires + CHANGELOG `worldGenVersion 2` | 1–2 j | docs cohérentes, un seul total de tests |
| 2 | Renuméroter les tickets ECHOS V2 et créer `ECHOS-110` dans le backlog ECHOS | 0,5 j | plus de collision, dépendance de PRISM-123 existante |
| 3 | Harnais d'ingestion PRISM → ECHOS (S2) | 3–5 j | un run réel persisté et rejouable |
| 4 | Quatre indicateurs (S3) + comparaison de runs (S4) | 1,5–2 sem. | indicateurs calculés sur run réel, divergence au tick attendu |
| 5 | Run de calibration à blanc 1×1 000 (S5) | 1 j | coût réel unique, écart R3/B9 tranché |
| 6 | Calibrage SYNE/PRISM sur 3 graines (S6) | 2–4 sem. | V1–V7 atteints ou écart préenregistré |
| 7 | Préenregistrement + empreintes v2 (S7) | 2–3 j | protocole daté, seuils figés |
| 8 | Schéma de profil d'ablation (ECHOS-110) | 3–5 j | A1–A8 mappés, un fichier par bras |
| 9 | Campagne d'ablations (S8) | 1–2 sem. | 45 runs archivés, statistiques |
| 10 | Rapport (S9) | 1 sem. | verdict publié, positif ou négatif |

**Total** : 6 à 10 semaines, avec le calibrage comme principal aléa.

---

## 6. Risques et décisions à prendre

1. **Risque de calibration** (élevé) : si le mix de décisions ne bascule pas vers la production, V3/V4 échouent. Un résultat négatif reste publiable, mais il doit être **préenregistré** avant les runs, sinon il ne vaut rien.
2. **Risque de mesure** (moyen) : l'écart 157,7 ms vs 431,5 ms doit être tranché par le run S5 avant tout dimensionnement.
3. **Risque de provenance** (moyen) : brancher ECHOS après les campagnes rendrait les données inexploitables. L'ordre S2→S3→S4 avant S8 n'est pas négociable.
4. **Risque de déterminisme** (moyen) : `WorldGenVersion 2` a re-épinglé 9 empreintes ; il manque une empreinte de monde v2. À faire en S7 avant l'expérience.
5. **Risque de dispersion** (élevé) : G1, le viewer et le Launcher peuvent consommer tout le temps disponible sans produire un seul résultat scientifique. Le ROADMAP autorise explicitement à ne pas bloquer sur G1 ; il faut s'en servir.
6. **Décision à prendre** : confirmes-tu que l'objectif prioritaire est le **rapport expérimental V2** (et non la release 2.0.0) ? Si oui, G1/viewer/Launcher passent après G7 et le backlog est élagué comme au §4.3.

---

## 7. Annexe — preuves et fichiers

**Commandes exécutées**

```powershell
dotnet test D:\LIVEX\prism\Prism.sln -c Release
dotnet test D:\LIVEX\syne\Syne.sln -c Release
cd D:\LIVEX\echos; .\.venv\Scripts\python.exe -m pytest
dotnet test D:\LIVEX\launcher\Launcher.sln -c Release
```

**Fichiers de référence**

- `docs/prism/ISSUES.md` — backlog et états détaillés.
- `docs/prism/ROADMAP.md` — portes G0–G8, §1 (G1 non bloquante).
- `docs/prism/PERFORMANCE.md` — budgets, B9, profilage.
- `docs/prism/G5-R3-REPORT.md` — R3, mix de décisions, note de vérification §6.
- `docs/monographie/02b-question-decomposee.md` §2b.4 — définition opératoire de l'émergence.
- `docs/governance/realignment/EXPERIMENTAL-VALIDATION-2026-10.md` — précédent V1 du protocole.
- `prism/Prism.Host/Hosting/PrismHostRuntime.cs` — flux `metrics`, `entity_snapshot`, `AgentEntitySnapshot.Inventory`.
- `prism/Prism.Core/World/AgentLifecycleSystem.cs:10` — inventaire par agent.
- `prism/Prism.Core/World/AblationMechanisms.cs` — six leviers (shelter, water_access, storage, production, wear, mastery).
- `prism/Prism.Core/World/WorldZone.cs` — `ShoreBand = 0`.
- `prism/Prism.Content/WorldGenerationConfigCompiler.cs` — `WorldGenVersion: 2`.
- `prism/Prism.Core.Tests/DeterministicCoreTests.cs:520` — empreintes de monde encore en v1.
- `echos/echos/ingestion/prism_stream.py` — ingestion sans appelant de production.
- `echos/echos/analysis/*.py` — aucune référence PRISM.
- `launcher/Launcher.Domain/Model/PrismRequirements.cs` — exigences `immersion`/`renderCadence`.
- `.github/workflows/ci.yml` — matrice PRISM Ubuntu/Windows et tests Launcher avec composants réels.
- `docs/prism/benchmarks/B9-coupled-tick-2026-11-15-v1.json` — 431,5 ms, SE 14,78 % sur la perception, rejet global du harnais.

---

*Rapport produit le 15 novembre 2026 sur l'arbre de travail non commité. Les chiffres de tests sont ceux des runs du jour ; ils remplacent les compteurs 423/444/477 des documents existants.*
