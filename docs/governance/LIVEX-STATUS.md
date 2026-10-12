# LIVEX-STATUS.md — état réel du projet

**Composant** : LIVEX (général)
**Statut** : [STABLE]
**Dernière mise à jour** : 12 octobre 2026
**Dépend de** : `ROADMAP.md`, `../../CHANGELOG.md`, `docs/*/ROADMAP.md`

> Rapport d’inventaire et de maturité du dépôt. Il distingue le code présent,
> les validations exécutées et les intégrations effectivement disponibles.

| Référence | Valeur |
| :-- | :-- |
| Date de l’état observé | 12 octobre 2026 |
| Branche et commit | `develop` — `6c12421` (état de base ; le présent lot y ajoute un correctif d’IHM du Launcher et la mise à jour documentaire) |
| Fichiers suivis par Git | 640 |
| Résultat principal des suites locales | 1 258 réussis, 16 ignorés, 0 échec |
| Répertoire analysé | Racine du dépôt LIVEX |

## Sommaire

1. [Synthèse](#1-synthèse)
2. [Périmètre et méthode](#2-périmètre-et-méthode)
3. [Structure et inventaire des fichiers](#3-structure-et-inventaire-des-fichiers)
4. [Langages et lignes de code](#4-langages-et-lignes-de-code)
5. [Tests et validations](#5-tests-et-validations)
6. [État fonctionnel par composant](#6-état-fonctionnel-par-composant)
7. [Limites et travaux encore nécessaires](#7-limites-et-travaux-encore-nécessaires)

## 1. Synthèse

LIVEX est un dépôt multi-composants composé du moteur de simulation SYNE,
d’ECHOS (ingestion, analyse et API headless), du projet Unreal PRISM et de
son plugin d’intégration PRISM-LDK, de `syne-mock` et du Launcher. Le Launcher
est une application de bureau Avalonia avec ses couches de domaine,
orchestration, d’infrastructure, de présentation et de gestion des paquets.
L’interface web d’ECHOS et son shell Electron ont été retirés (ADR-007,
05/10/2026) : la présentation est portée par les fenêtres natives du Launcher.

Au commit de référence, l’inventaire Git contient **640 fichiers**. La
classification de ce rapport en dénombre **220 fichiers source, 164 fichiers
de tests, 167 documents, 46 fichiers de configuration, 38 assets et 5
fichiers autres**. Tokei 13.0.0 reconnaît **610 des 640 fichiers** et y
dénombre **65 142 lignes de code au sens de son analyseur** ; ce chiffre
inclut notamment les données JSON, les fichiers de projets, les configurations
et les SVG et ne représente donc pas les seules lignes de code applicatif.

Pour une mesure plus proche du code exécutable, le rapport isole **63 231
lignes Tokei de code** dans les fichiers classés source et tests du dépôt (les
assets et les exemples de code repérés dans la documentation ne sont pas
assimilés à du code applicatif). Ce total comprend **38 904 lignes source et
scripts** et **24 327 lignes de tests**. Tokei exclut les lignes vides et les
commentaires de ses comptes « code ».

Les suites effectivement exécutées localement le 12/10/2026 totalisent **1 258
tests réussis et 16 tests ignorés**, sans échec : SYNE 634, Launcher 217,
ECHOS Python 361 et `syne-mock` 46. Les 14 tests ECHOS ignorés localement sont
des parcours d’intégration qui exigent un SYNE publié en Release (13, exécutés
par le job CI U8) ou le serveur `syne-mock` (1, job CI dédié « Intégration
mock → ECHOS ») ; les 2 tests `syne-mock` ignorés sont les arrêts par
SIGTERM/SIGINT, non livrables sous Windows (le flux complet reste couvert par
la suite). La couverture de lignes mesurée le 12/10 est de **90,55 %** pour
ECHOS et de **87,41 %** pour les assemblages SYNE agrégés (Core 90,43 %,
Console 74,03 %), au-delà du seuil de 80 % requis.

**Ces résultats ne signifient pas que toutes les capacités prévues de LIVEX
sont livrées.** La feuille de route fait foi : `ROADMAP.md` (phases 0–5,
jalons T0–T6, backlog U0–U8) ; le plan de sortie détaillé `ROADMAP-V01.md` a
été retiré du dépôt par la PR #529 (cf. §7). Les campagnes
scientifiques (V2′ rejouée par le chemin `reset`, 6/6 conforme à ADR-016 ;
benchmarks V5 refaits sur la machine de référence), le parcours J3 de bout en
bout contre les composants réels (33/33 le 12/10) et la validation transverse
V3 (hors long-run) sont validés ; le support Windows est validé localement
depuis le 08/10/2026 (Lot W : manifestes, bancs portables, E2E, `--check`,
exécutable natif) et constaté en CI : **13/13 jobs verts sur `develop` le
11/10/2026** (PR #527 avait ouvert la voie le 08/10 avec 13/13 jobs Linux +
`windows-latest`). Aucune release, publication ni tag avant que tout ne soit
validé. L’expérience de rendu PRISM reste hors périmètre V0.1.

## 2. Périmètre et méthode

### Référence du comptage

- Le périmètre est le contenu **suivi par Git** à la date indiquée ci-dessus,
  obtenu depuis `git ls-files`. Les fichiers générés non suivis — dont les
  sorties de compilation et les caches — ne sont pas inclus.
- Les répertoires de dépendances, environnements virtuels, caches, sorties de
  compilation et distributions ignorés par Git ne sont pas comptés.
- La ventilation « source / tests / documentation / configuration / assets /
  autres » est une classification pratique fondée sur les chemins et
  extensions : un fichier est un **test** si un segment de son chemin contient
  `test`/`tests` ou si son nom suit les conventions (`test_*.py`, `*_test.*`,
  `*.test.*`, `*Tests.cs`) ; les extensions `.md`/`.txt` et le fichier
  `LICENSE` sont des **documents** ; images, PDF et assets Unreal forment les
  **assets** ; JSON, YAML, MSBuild, INI, TOML, fichiers AxAML et fichiers
  cachés forment la **configuration** ; les extensions de langages applicatifs
  et les scripts forment la **source** ; le reste (scripts d’émulation sans
  extension reconnue, manifeste Windows, fichiers Unreal `.uproject`/`.uplugin`)
  compte parmi les **autres**. Elle décrit ce dépôt et non une taxonomie
  universelle.
- Les lignes de code par langage et les lignes source/tests sont analysés avec
  **Tokei 13.0.0** sur les fichiers suivis. Les nombres de lignes désignent les
  lignes classées comme code par Tokei, pas le nombre de lignes physiques.
- Les résultats de tests sont ceux des commandes/suites exécutées dans la
  validation du 12/10/2026 (voir §5). Un résultat de test ne prouve ni
  l’exhaustivité fonctionnelle ni la compatibilité entre composants.

La collecte de l’inventaire suivie par Git est reproductible avec
`git ls-files`. La ventilation linguistique peut être recalculée avec
`git ls-files -z | xargs -0 tokei --output json --`.

### Pourquoi l’ancien rapport donnait d’autres chiffres

L’ancien `project-report . --count-all` comptait **34 415 fichiers physiques**
et environ **2,72 Go** sur cette machine. Il incluait notamment dépendances,
sorties de build, caches et autres artefacts locaux : il mesurait un état
disque ponctuel, pas la taille du projet versionné. Ses **508 782 lignes**
étaient donc dominées par des répertoires générés et des dépendances, et ne
constituaient pas un décompte fiable du code LIVEX.

Le présent rapport ne prétend pas donner la taille disque de
l’installation locale ; celle-ci varie avec les builds, les dépendances
installées et les caches.

### Évolution depuis le snapshot du 08/10/2026

L’état du 08/10 (632 fichiers, 1 205 tests) est obsolète sur plusieurs points.
Les écarts principaux avec le présent inventaire sont :

- **PR #529 (11/10/2026)** — conformité `NAMING_CONVENTIONS` : en-têtes de
  fichier normalisés sur 353 fichiers de code, renvois à documents retirés
  neutralisés, et **11 documents obsolètes supprimés** dont le plan de sortie
  `ROADMAP-V01.md` et `RAPPORT-ELEMENTS-OUVERTS.md` (cf. §7).
- Fichiers suivis **632 → 640** : retraits documentaires compensés par de
  nouveaux tests, scripts et configurations (le détail par répertoire figure
  en §3).
- Tests **1 205 → 1 258** : SYNE 592 → 634, Launcher 206 → 217 (E2E 29 → 33),
  `syne-mock` 46 → 48 tests (46 exécutés, 2 ignorés sous Windows), ECHOS
  inchangé à 361.
- Couverture : ECHOS 91,57 % → **90,55 %** ; SYNE 85,86 % → **87,41 %** (la
  base de code a grandi, la méthode est inchangée).
- CI : PR #527 (08/10, 13/13 jobs) → dernière exécution sur `develop`
  **13/13 jobs verts** le 11/10/2026.

## 3. Structure et inventaire des fichiers

### Répartition des fichiers suivis

| Catégorie | Fichiers | Part |
| :-- | --: | --: |
| Code source | 220 | 34,4 % |
| Tests | 164 | 25,6 % |
| Documentation | 167 | 26,1 % |
| Configuration et manifests | 46 | 7,2 % |
| Assets | 38 | 5,9 % |
| Autres | 5 | 0,8 % |
| **Total** | **640** | **100 %** |

Les parts sont arrondies au dixième. Les 5 fichiers « autres » sont les deux
scripts d’émulation ECHOS (`../../echos/echos-launcher`, `../../echos/echos-launcher.cmd`),
`../../launcher/Launcher.App/app.manifest`, `../../prism/LDK/LDK.uproject` et
`../../prism/LDK/Plugins/PrismLdk/PrismLdk.uplugin`.

### Répartition par répertoire de premier niveau

| Répertoire | Fichiers suivis | Contenu principal |
| :-- | --: | :-- |
| `../../syne/` | 160 | Moteur .NET, outils console, serveur de contrôle et tests |
| `../../launcher/` | 150 | Application Avalonia, bibliothèques, outils et tests (unitaires, intégration, E2E) |
| `../` | 130 | Documentation des composants, installateur et références techniques |
| `../../echos/` | 96 | Backend Python headless (API, ingestion, analyse) et tests |
| `../../prism/` | 35 | Hôte Unreal technique et plugin PRISM-LDK |
| `../../syne-mock/` | 29 | Serveur Node.js, tests et documentation d’intégration |
| Racine du dépôt | 24 | Documentation et fichiers communs au projet |
| `../../.github/` | 7 | Modèles et workflows (`ci.yml`, `release.yml`) |
| `../../scripts/` | 6 | Scripts de développement et d’intégration |
| `../../configs/` | 3 | Configurations de simulation partagées |
| **Total** | **640** | |

### Volume de code source et de tests par composant

Les nombres de lignes de ce tableau sont des comptes Tokei. « Source » inclut
les scripts partagés ; « tests » désigne les fichiers des suites de tests. Le
nombre de fichiers de tests est fourni séparément pour rendre la portée de la
ventilation vérifiable.

| Composant / ensemble | Source et scripts (LOC) | Tests (LOC) | Fichiers de tests |
| :-- | --: | --: | --: |
| SYNE | 10 787 | 10 882 | 79 |
| Launcher | 15 219 | 5 990 | 34 |
| ECHOS (backend) | 8 450 | 6 551 | 46 |
| `syne-mock` | 1 575 | 904 | 5 |
| Scripts partagés | 1 896 | 0 | 0 |
| PRISM / PRISM-LDK | 888 | 0 | 0 |
| Exemples HTML de la documentation | 89 | 0 | 0 |
| **Total** | **38 904** | **24 327** | **164** |

Le « 0 » PRISM signifie qu’aucun fichier de test n’a été classé dans son
répertoire ; il ne signifie pas que le plugin ne dispose d’aucune validation
manuelle ou d’aucun test dans l’outillage Unreal.

## 4. Langages et lignes de code

Tokei compte ici les fichiers suivis qu’il reconnaît (610 sur 640 ; les assets
binaires, `LICENSE` et fichiers sans extension prise en charge ne le sont pas).
Les statistiques comprennent les sources, les tests, la documentation et les
formats de configuration ; elles ne sont pas toutes des lignes de code
applicatif.

| Langage / format reconnu | Fichiers Tokei | Lignes « code » |
| :-- | --: | --: |
| C# | 252 | 40 837 |
| Python | 79 | 16 237 |
| JavaScript | 20 | 2 479 |
| AXAML | 4 | 1 911 |
| JSON | 29 | 1 262 |
| C Header | 5 | 439 |
| YAML | 2 | 421 |
| C++ | 3 | 403 |
| MSBuild | 20 | 313 |
| SVG | 17 | 204 |
| INI | 4 | 188 |
| Visual Studio Solution | 2 | 168 |
| Shell | 2 | 102 |
| HTML | 1 | 89 |
| Unreal Project | 1 | 31 |
| TOML | 1 | 25 |
| Unreal Plugin | 1 | 24 |
| Batch | 1 | 9 |
| Markdown | 163 | 0 |
| Plain Text | 3 | 0 |
| **Total** | **610** | **65 142** |

Les lignes des formats de données et de configuration (JSON, MSBuild, YAML,
SVG, etc.) ne doivent pas être additionnées aux LOC applicatives. Pour les
langages applicatifs et scripts, après exclusion des assets et des exemples de
documentation, le total de référence est **63 231 LOC** : **38 904 LOC
source/scripts** et **24 327 LOC de tests**. La somme des langages du tableau
ci-dessus est exactement le total Tokei de 65 142 lignes ; contrairement au
rapport du 08/10, aucun écart résiduel n’est à répartir. Pour le nombre de
fichiers versionnés, la référence reste `git ls-files` (640).

## 5. Tests et validations

### Résultats observés (exécutés le 12/10/2026)

| Composant | Suite / type | Résultat local |
| :-- | :-- | :-- |
| SYNE | `Simulation.Core.Tests` | 548 réussis |
| SYNE | `Simulation.Console.Tests` | 86 réussis |
| ECHOS | Python : backend, analyse, ingestion et contrats | 361 réussis, 14 ignorés ; couverture mesurée : 90,55 % |
| Launcher | Tests unitaires | 164 réussis |
| Launcher | Tests d’intégration | 20 réussis |
| Launcher | Tests end-to-end (SYNE publié win-x64 + ECHOS réel) | 33 réussis |
| `syne-mock` | Suite Node.js | 46 réussis, 2 ignorés (48 tests) |
| PRISM / PRISM-LDK | Tests automatisés dans cette validation | Aucun lancé |
| **Total des suites locales** | **Exécutions comptabilisées** | **1 258 réussis, 16 ignorés, 0 échec** |

Commandes : `dotnet test syne/Syne.sln` (SYNE), `python -m pytest` depuis
`../../echos/` (environnement `.venv`, options pytest dans `pyproject.toml` dont
`pytest-cov`), `npm test` (`syne-mock`), `dotnet test launcher/Launcher.sln`
(unitaires + intégration) et `dotnet test launcher/Launcher.Tests.EndToEnd`
avec `LIVEX_SYNE_PUBLISHED_ROOT` pointant sur un SYNE publié (33/33 en
9 min 14 s le 12/10). La mesure de couverture SYNE utilise
`dotnet test --collect:"XPlat Code Coverage"` avec le filtre
`FullyQualifiedName!~ScaleTargetsTests&FullyQualifiedName!~LauncherBatchProcessTests`.

Les 14 tests ECHOS ignorés en local sont des parcours d’intégration : 13
exigent un build SYNE Release (job CI U8) et 1 exige le serveur `syne-mock`
(job CI « Intégration mock → ECHOS »). Les 2 tests `syne-mock` ignorés sont
les arrêts par signal (`SIGTERM`/`SIGINT`), non livrables sous Windows. Ces
jobs CI sont conçus pour exécuter ces parcours séparément ; leurs résultats ne
sont pas ajoutés au total local ci-dessus.

### Couverture de lignes

- **ECHOS** : 90,55 % (`pytest` + pytest-cov, seuil requis 80 %).
- **SYNE** : 87,41 % agrégé — `Simulation.Core` 90,43 % (6 467/7 151),
  `Simulation.Console` 74,03 % (1 197/1 617) — mesuré le 12/10 avec
  `dotnet test --collect:"XPlat Code Coverage"` (coverlet). Méthode : union
  des deux exécutions instrumentées par ligne (maximum des coups de feu),
  après dédoublonnage — les rapports Cobertura listent chaque classe deux fois
  — et filtre hors `ScaleTargetsTests` et `LauncherBatchProcessTests`, ces deux
  classes étant incompatibles avec l’instrumentation (voir ci-dessous).

Le chiffre historique de 96,19 % (jalon SYNE-100, moteur 0.8.0) concernait un
périmètre et une base de code antérieurs ; il n’est pas comparable à la mesure
ci-dessus.

### Points de vigilance sur les mesures

- `ScaleTargetsTests.OneThousandEntities_StayAboveRegressionFloor` (plancher
  de débit à 1000 entités) mesure un débit : il est sensible à la charge de la
  machine (échec observé le 08/10 en exécution parallèle, vert en reprise
  isolée ; vert le 12/10 où les suites ont été exécutées séquentiellement) et
  échoue systématiquement sous instrumentation coverlet. Les checksums de
  déterminisme sont inchangés ; il s’agit d’un test fragile sous charge, pas
  d’une régression du moteur. Sa classe est exclue de la mesure de couverture.
- `LauncherBatchProcessTests` échoue sous le collecteur de couverture
  (le processus SYNE enfant avorte avec `HttpListener` déjà disposé) ; il passe
  en exécution standard. D’où l’exclusion de ces deux classes de la mesure de
  couverture.

### Interprétation

- Un test vert confirme le comportement couvert par ce test ; il ne valide pas
  l’ensemble du contrat entre composants.
- Les tests SYNE et `syne-mock` valident des produits distincts. Les résultats
  ne démontrent pas que leurs simulations sont scientifiquement équivalentes.
- Les tests du Launcher couvrent les règles du Launcher, l’infrastructure et
  les parcours E2E contre un SYNE publié ; la preuve de bout en bout complète
  (campagne → interruption → reprise → analyse → paquet, J3) est automatisée
  par `J3RealComponentsEndToEndTests` contre SYNE publié et ECHOS réel —
  33/33 le 12/10 sur la machine Windows, banc également déclaré sur
  `windows-latest` et vert en CI (PR #527, 08/10).
- Aucun résultat automatisé PRISM ne doit être déduit de la réussite des tests
  des autres composants.

## 6. État fonctionnel par composant

### Launcher

**Présent dans le dépôt.** Application de bureau Avalonia structurée en
couches, orchestration de services déclarés par manifestes, profils/modes,
allocation de ports, supervision et santé, journaux, gestion de paquets
`.livexp`, configuration d’installations, interface de monitoring et tests
unitaires, d’intégration et end-to-end. SYNE réel et son mock sont représentés
comme deux choix exclusifs du même rôle SYNE, et non comme deux composants
démarrables simultanément.

**Limite opérationnelle.** Le Launcher peut superviser les adaptations Linux
déclarées pour ECHOS et `syne-mock`. SYNE réel publie son manifeste
`component.json` et son contrat d’arguments, de readiness et d’arrêt (service
supervisé + batch `reference`, J2A accepté — `../../launcher/V1-CAPABILITY-MATRIX.md`).
Les chemins d’analyse du Launcher sont raccordés aux opérations headless de
l’API ECHOS (J2B accepté). L’installation ECHOS Linux est validée (procédure
en `../launcher/INTEGRATION_CONTRACT.md` §10.3). Le parcours J3 de bout
en bout contre les composants réels est acquis (`J3RealComponentsEndToEndTests`,
33/33 le 12/10). **Constat CI `windows-latest` : fait le 08/10/2026** (PR #527,
13/13 jobs verts) et dernière exécution `develop` vert **13/13 jobs le
11/10/2026** — les validations locales Windows (Lot W) sont faites et cochées
au 08/10/2026 (208 tests verts, `--check`, exécutable natif). Le suivi détaillé
du V1 vit dans `../../launcher/ROADMAP-V1.md` (étape V1, items cochés) et
`../launcher/ROADMAP.md`.

### SYNE — moteur réel

**Présent dans le dépôt.** Moteur .NET de simulation, outils CLI et serveur de
contrôle/observabilité, avec tests dédiés (634 tests). Le README décrit
notamment le PRNG, le monde, les entités, les traits et la boucle de
simulation.

**Contrat d’intégration.** Le serveur de contrôle applique le profil de
référence sur les chemins `prepare`, `start` **et** `reset` (correctif
`ef878f21` : `reset` sans config produit les mêmes options que `prepare`), le
manifeste `component.json` et le contrat batch (arguments, readiness, arrêt
authentifié) sont publiés et acceptés (J2A), et la campagne V2′ rejouée
**par le chemin `reset`** est conforme aux critères ADR-016 sur 6/6 couples
population/graine. Lancer le service de contrôle équivaut désormais au contrat
attendu par le Launcher ; la preuve d’extrémité J3 est fournie par le parcours
automatisé (33/33 le 12/10).

**Limite résiduelle.** Les benchmarks V5 de référence sont ceux de
`../syne/PERFORMANCE.md` §9 (machine i7-8750H, 08/10/2026).

### ECHOS

**Présent dans le dépôt.** Backend Python pour ingestion, stockage et analyse,
API REST **headless** — l’interface web React/TypeScript et le shell Electron
ont été retirés (ADR-007, 05/10/2026) ; la présentation est portée par les
fenêtres natives du Launcher. Les tests couvrent les suites Python (361 tests
verts, 14 ignorés en local, couverture 90,55 %). Des adaptateurs Linux
permettent au Launcher de démarrer, sonder et arrêter le service ECHOS.

**Contrat d’intégration.** L’API REST expose les opérations headless attendues
par le service d’analyse du Launcher (`/analysis/run`, `/analysis/experiment`
et `/analysis/report`, J2B accepté). L’installation Linux est validée sur
machine propre (venv vierge, 212 Mo, imports et tests verts). L’acceptation
pilotée depuis l’interface du Launcher est couverte par le parcours J3
(analyses individuelles et rapport archivé contre ECHOS réel).

### `syne-mock`

**Présent dans le dépôt.** Serveur Node.js déterministe destiné à émuler le
contrat de transport pour le développement et les intégrations clientes. Il
possède un manifeste Linux, un cycle de vie supervisable et 48 tests (46
exécutés localement, 2 ignorés sous Windows faute de livraison des signaux
d’arrêt).

**Limite fonctionnelle.** C’est une émulation de protocole, pas un moteur
scientifique équivalent à SYNE. La génération, les décisions et le mouvement
restent des approximations ; certaines données cognitives/sociales ne sont pas
émises comme par le moteur réel. Le mock ne déclare pas la capacité de campagne
batch SYNE.

### PRISM et PRISM-LDK

**Présent dans le dépôt.** Projet/hôte Unreal technique et plugin C++ exposant
les contrats SYNE et des éléments d’intégration Blueprint. La documentation
décrit l’architecture et les flux HTTP/WebSocket.

**Limite fonctionnelle.** L’hôte technique et le plugin d’intégration ne
constituent pas à eux seuls l’expérience complète de rendu 3D prévue. Le
profil PRISM reste verrouillé dans le Launcher. **PRISM est explicitement hors
périmètre V0.1** (portes d’intégration séparées,
`../../launcher/V1-CAPABILITY-MATRIX.md` §4-7).

## 7. Limites et travaux encore nécessaires

**Le plan de sortie V0.1 détaillé (`ROADMAP-V01.md`, 11 étapes) a été retiré
du dépôt par la PR #529** (11/10/2026, conformité `NAMING_CONVENTIONS`) ; ses
renvois ont été neutralisés dans les changelogs, ADR et ISSUES. La feuille de
route fait foi : **`ROADMAP.md`** (phases 0–5, jalons T0–T6, backlog unifié
U0–U8) ; le suivi détaillé du Launcher vit dans **`../../launcher/ROADMAP-V1.md`**
(items cochés) ; les preuves des lots passés restent tracées par les CHANGELOG,
la CI et les suites de tests citées en §5. Les travaux encore ouverts sont :

1. **PRISM** : rendu interactif Unreal et validation d’intégration contre SYNE
   réel — hors périmètre V0.1, porte d’intégration séparée
   (`../../launcher/V1-CAPABILITY-MATRIX.md` §4-7).
2. **Portée des preuves automatisées** : une couverture mesurée avec deux
   classes exclues (§5) et 16 tests qui ne s’exécutent qu’en CI (14 ECHOS —
   dont 13 exigent un SYNE publié Release, job U8, et 1 le mock, job dédié —
   plus les 2 arrêts par signal de `syne-mock`) restent hors des exécutions
   locales. Tant qu’aucun job ne les lève, la couverture locale reste partielle
   par construction.
3. **Backlog U8** (`ROADMAP.md` §6) : stabilité long-run, reprise worker et
   parcours UI complet ne sont pas encore des preuves de release.
4. **Aucune release** : pas de tag, ni publication, avant validation complète.

### Conclusion

Le dépôt contient une base logicielle substantielle et testée pour le Launcher,
SYNE, ECHOS et `syne-mock` (1 258 tests verts et 0 échec le 12/10/2026,
couvertures ECHOS 90,55 % et SYNE 87,41 % au-dessus de 80 %), ainsi qu’un
plugin PRISM hors périmètre de sortie. Les contrats inter-composants (J2A,
J2B), l’installation ECHOS Linux, les campagnes scientifiques rejouées par
`reset`, le parcours J3 de bout en bout (33/33) et la validation V3 sont
établis, et la CI `develop` est verte (13/13 jobs, 11/10/2026). Aucune release,
tag ni publication n’est programmé avant validation complète. Ce rapport décrit
**l’état du code versionné et des validations réalisées au 12/10/2026**, et non
une certification que toutes les fonctionnalités V1 prévues sont
opérationnelles.
