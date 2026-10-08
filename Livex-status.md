# LIVEX — état réel du projet

> Rapport d’inventaire et de maturité du dépôt. Il distingue le code présent,
> les validations exécutées et les intégrations effectivement disponibles.

| Référence | Valeur |
| :-- | :-- |
| Date de l’état observé | 8 octobre 2026 |
| Branche et commit | `develop` — `68af96a5` (code ; le présent rapport et le lot documentaire qui l’accompagne n’ajoutent que de la documentation) |
| Fichiers suivis par Git | 632 |
| Résultat principal des suites locales | 1 205 réussis, 13 ignorés, 0 échec |
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
d’orchestration, d’infrastructure, de présentation et de gestion des paquets.
L’interface web d’ECHOS et son shell Electron ont été retirés (ADR-007,
05/10/2026) : la présentation est portée par les fenêtres natives du Launcher.

Au commit de référence, l’inventaire Git contient **632 fichiers** (621 fichiers
suivis avant le présent lot, auxquels s’ajoutent les 11 documents de
`docs/docs-installer/`). La classification de ce rapport en dénombre **216
fichiers source, 156 fichiers de tests, 173 documents, 46 fichiers de
configuration, 37 assets et 4 fichiers autres**. Tokei détecte **63 162 lignes
de code au sens de son analyseur** sur 602 fichiers reconnus ; ce chiffre
inclut notamment les données JSON, les fichiers de projets et les
configurations et ne représente donc pas les seules lignes de code applicatif.

Pour une mesure plus proche du code exécutable, le rapport isole **60 788
lignes Tokei de code** dans les langages de programmation et scripts du dépôt
(les assets SVG et les exemples de code repérés dans la documentation ne sont
pas assimilés à du code applicatif). Ce total comprend **37 978 lignes source
et scripts** et **22 810 lignes de tests**. Tokei exclut les lignes vides et
les commentaires de ses comptes « code ».

Les suites effectivement exécutées localement totalisent **1 205 tests
réussis et 13 tests ignorés**, sans échec : SYNE 592, Launcher 206, ECHOS
Python 361 et `syne-mock` 46. Les 13 tests ECHOS ignorés localement sont des
parcours d’intégration qui exigent un SYNE publié en Release (12, exécutés par
le job CI U8) ou le serveur `syne-mock` (1, job CI dédié). La couverture de
lignes mesurée le 08/10 est de **91,57 %** pour ECHOS et de **85,86 %** pour
les assemblages SYNE agrégés (Core 88,68 %, Console 73,49 %), au-delà du seuil
de 80 % requis.

**Ces résultats ne signifient pas que toutes les capacités prévues de LIVEX
sont livrées.** Le plan de sortie fait foi : `ROADMAP-V01.md`. Les campagnes
scientifiques (V2′ rejouée par le chemin `reset`, 6/6 conforme à ADR-016 ;
benchmarks V5 refaits sur la machine de référence) sont validées, mais il
reste à prouver le parcours J3 de bout en bout contre les composants réels, à
ajouter le support Windows (manifestes et CI), à exécuter la validation
transverse V3 (hors long-run) et à publier les artefacts multiplateformes.
L’expérience de rendu PRISM reste hors périmètre V0.1.

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
  `LICENSE` sont des **documents** ; images et assets Unreal forment les
  **assets** ; JSON, YAML, MSBuild, INI, TOML et fichiers cachés forment la
  **configuration** ; les extensions de langages applicatifs et les scripts
  forment la **source**. Elle décrit ce dépôt et non une taxonomie universelle.
- Les lignes de code par langage et les lignes source/tests sont analysés avec
  **Tokei 13.0.0** sur les fichiers suivis. Les nombres de lignes désignent les
  lignes classées comme code par Tokei, pas le nombre de lignes physiques.
- Les résultats de tests sont ceux des commandes/suites exécutées dans la
  validation du 08/10/2026 (voir §5). Un résultat de test ne prouve ni
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

Le présent rapport ne prétend pas donner la taille disque de l’installation
locale ; celle-ci varie avec les builds, les dépendances installées et les
caches.

### Évolution depuis le snapshot du 02/10/2026

L’état du 02/10 (618 fichiers, 1 086 tests) est obsolète sur plusieurs points.
Les écarts principaux avec le présent inventaire sont : le retrait de
l’interface ECHOS et de son shell Electron (ADR-007, 05/10 — 53 fichiers
retirés de `echos/`), l’ajout des documents d’installation (`docs/docs-installer/`),
les tests ajoutés par les lots V0.1 (correctif du chemin `reset`, contrôles de
cycle de vie, E2E Launcher) et la suppression des artefacts Docker (arbitrage
A1). Les comptes de ce rapport remplacent ceux du snapshot.

## 3. Structure et inventaire des fichiers

### Répartition des fichiers suivis

| Catégorie | Fichiers | Part |
| :-- | --: | --: |
| Code source | 216 | 34,2 % |
| Tests | 156 | 24,7 % |
| Documentation | 173 | 27,4 % |
| Configuration et manifests | 46 | 7,3 % |
| Assets | 37 | 5,9 % |
| Autres | 4 | 0,6 % |
| **Total** | **632** | **100 %** |

Les parts sont arrondies au dixième.

### Répartition par répertoire de premier niveau

| Répertoire | Fichiers suivis | Contenu principal |
| :-- | --: | :-- |
| `syne/` | 154 | Moteur .NET, outils console, serveur de contrôle et tests |
| `launcher/` | 145 | Application Avalonia, bibliothèques, outils et tests (unitaires, intégration, E2E) |
| `docs/` | 136 | Documentation des composants, installateur et références techniques |
| `echos/` | 95 | Backend Python headless (API, ingestion, analyse) et tests |
| `prism/` | 35 | Hôte Unreal technique et plugin PRISM-LDK |
| `syne-mock/` | 29 | Serveur Node.js, tests et documentation d’intégration |
| Racine du dépôt | 24 | Documentation et fichiers communs au projet |
| `.github/` | 7 | Modèles et workflows (`ci.yml`, `release.yml`) |
| `scripts/` | 5 | Scripts de développement et d’intégration |
| `configs/` | 2 | Configurations de simulation partagées |
| **Total** | **632** | |

### Volume de code source et de tests par composant

Les nombres de lignes de ce tableau sont des comptes Tokei. « Source » inclut
les scripts partagés ; « tests » désigne les fichiers des suites de tests. Le
nombre de fichiers de tests est fourni séparément pour rendre la portée de la
ventilation vérifiable.

| Composant / ensemble | Source et scripts (LOC) | Tests (LOC) | Fichiers de tests |
| :-- | --: | --: | --: |
| SYNE | 10 448 | 10 208 | 75 |
| Launcher | 14 934 | 5 393 | 30 |
| ECHOS (backend) | 8 448 | 6 364 | 46 |
| `syne-mock` | 1 560 | 845 | 5 |
| Scripts partagés | 1 611 | 0 | 0 |
| PRISM / PRISM-LDK | 888 | 0 | 0 |
| Exemples HTML de la documentation | 89 | 0 | 0 |
| **Total** | **37 978** | **22 810** | **156** |

Le « 0 » PRISM signifie qu’aucun fichier de test n’a été classé dans son
répertoire ; il ne signifie pas que le plugin ne dispose d’aucune validation
manuelle ou d’aucun test dans l’outillage Unreal.

## 4. Langages et lignes de code

Tokei compte ici les fichiers suivis qu’il reconnaît (602 sur 632 ; les assets
binaires, `LICENSE` et scripts sans extension ne le sont pas). Les statistiques
comprennent les sources, les tests, la documentation et les formats de
configuration ; elles ne sont pas toutes des lignes de code applicatif.

| Langage / format reconnu | Fichiers Tokei | Lignes « code » |
| :-- | --: | --: |
| C# | 241 | 39 043 |
| Python | 78 | 15 751 |
| JavaScript | 20 | 2 405 |
| AXAML | 4 | 1 822 |
| JSON | 28 | 1 212 |
| C Header | 5 | 439 |
| C++ | 3 | 403 |
| MSBuild | 20 | 313 |
| YAML | 2 | 274 |
| SVG | 17 | 204 |
| INI | 4 | 188 |
| Visual Studio Solution | 2 | 168 |
| Shell | 2 | 102 |
| HTML | 1 | 89 |
| Unreal Project | 1 | 31 |
| TOML | 1 | 25 |
| Unreal Plugin | 1 | 24 |
| Markdown | 169 | 0 |
| Plain Text | 3 | 0 |

Le total global affiché par Tokei est **63 162 lignes « code »** toutes
catégories confondues. Les lignes des formats de données et de configuration
(JSON, MSBuild, YAML, etc.) et les SVG ne doivent pas être additionnées aux
LOC applicatives. Pour les langages applicatifs et scripts uniquement, après
exclusion des assets et des exemples de documentation, le total de référence
est **60 788 LOC** : **37 978 LOC source/scripts** et **22 810 LOC de tests**.

Les lignes reconnues individuellement par langage ne s’additionnent pas
exactement au total global de Tokei ; un écart de **669 lignes** reste dans
l’agrégat plutôt que d’être attribué à une ligne du tableau. Les comptes de
fichiers par langage sont ceux des rapports Tokei et peuvent refléter ses
attributions aux formats intégrés ; pour le nombre de fichiers versionnés, la
référence reste `git ls-files` (632).

## 5. Tests et validations

### Résultats observés (exécutés le 08/10/2026)

| Composant | Suite / type | Résultat local |
| :-- | :-- | :-- |
| SYNE | `Simulation.Core.Tests` | 514 réussis |
| SYNE | `Simulation.Console.Tests` | 78 réussis |
| ECHOS | Python : backend, analyse, ingestion et contrats | 361 réussis, 13 ignorés ; couverture mesurée : 91,57 % |
| Launcher | Tests unitaires | 162 réussis |
| Launcher | Tests d’intégration | 15 réussis |
| Launcher | Tests end-to-end (contre un SYNE publié) | 29 réussis |
| `syne-mock` | Suite Node.js | 46 réussis |
| PRISM / PRISM-LDK | Tests automatisés dans cette validation | Aucun lancé |
| **Total des suites locales** | **Exécutions comptabilisées** | **1 205 réussis, 13 ignorés, 0 échec** |

Commandes : `dotnet test Syne.sln` (SYNE), `pytest echos/tests` (ECHOS),
`npm test` (`syne-mock`), `dotnet test` des trois projets Launcher avec
`TMPDIR` sur disque (le `/tmp` en tmpfs de 7,5 Gio déborde sur le test de
paquet > 2 Gio) et `LIVEX_SYNE_PUBLISHED_ROOT` pointant sur un SYNE publié
pour les E2E.

Les 13 tests ECHOS ignorés en local sont des parcours d’intégration : 12
exigent un build SYNE Release (job CI U8) et 1 exige le serveur `syne-mock`
(job CI dédié). Ces jobs CI sont conçus pour les exécuter séparément ; leurs
résultats ne sont pas ajoutés au total local ci-dessus.

### Couverture de lignes

- **ECHOS** : 91,57 % (`pytest echos/tests`, seuil requis 80 %).
- **SYNE** : 85,86 % agrégé — `Simulation.Core` 88,68 % (6 120/6 901),
  `Simulation.Console` 73,49 % (1 159/1 577) — mesuré le 08/10 avec
  `dotnet test --collect:"XPlat Code Coverage"` (coverlet). Filtre appliqué :
  `FullyQualifiedName!~ScaleTargetsTests&FullyQualifiedName!~LauncherBatchProcessTests`,
  ces deux classes étant incompatibles avec l’instrumentation (voir ci-dessous).

Le chiffre historique de 96,19 % (jalon SYNE-100, moteur 0.8.0) concernait un
périmètre et une base de code antérieurs ; il n’est pas comparable à la mesure
ci-dessus.

### Points de vigilance sur les mesures

- `ScaleTargetsTests.OneThousandEntities_StayAboveRegressionFloor` (plancher
  de débit à 1000 entités) a échoué une fois le 08/10 lorsque plusieurs suites
  tournaient simultanément, puis est repassé vert en reprise isolée. Le test
  mesure un débit : il est sensible à la charge de la machine et échoue
  systématiquement sous instrumentation coverlet. Les checksums de déterminisme
  sont inchangés ; il s’agit d’un test fragile sous charge, pas d’une
  régression du moteur.
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
  (campagne → interruption → reprise → analyse → paquet, J3) reste à établir
  hors de ces tests.
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
supervisé + batch `reference`, J2A accepté — `launcher/V1-CAPABILITY-MATRIX.md`).
Les chemins d’analyse du Launcher sont raccordés aux opérations headless de
l’API ECHOS (J2B accepté). L’installation ECHOS Linux est validée (étape 9 de
`ROADMAP-V01.md`, procédure en `docs/docs-launcher/INTEGRATION_CONTRACT.md`
§10.3). **Reste ouvert avant clôture : le parcours J3 de bout en bout contre
les composants réels** (étape 4) et les validations Windows (étape 5).

### SYNE — moteur réel

**Présent dans le dépôt.** Moteur .NET de simulation, outils CLI et serveur de
contrôle/observabilité, avec tests dédiés. Le README décrit notamment le PRNG,
le monde, les entités, les traits et la boucle de simulation.

**Contrat d’intégration.** Le serveur de contrôle applique le profil de
référence sur les chemins `prepare`, `start` **et** `reset` (correctif
`ef878f21` : `reset` sans config produit les mêmes options que `prepare`), le
manifeste `component.json` et le contrat batch (arguments, readiness, arrêt
authentifié) sont publiés et acceptés (J2A), et la campagne V2′ rejouée
**par le chemin `reset`** est conforme aux critères ADR-016 sur 6/6 couples
population/graine (`RAPPORT-ELEMENTS-OUVERTS.md` §5.1 bis). Lancer le service
de contrôle équivaut désormais au contrat attendu par le Launcher, sous
réserve de la preuve d’extrémité J3.

**Limite résiduelle.** La preuve reste à répéter dans le parcours complet du
Launcher (étape 4), et les benchmarks V5 de référence sont ceux de
`docs/docs-syne/PERFORMANCE.md` §9 (machine i7-8750H, 08/10/2026).

### ECHOS

**Présent dans le dépôt.** Backend Python pour ingestion, stockage et analyse,
API REST **headless** — l’interface web React/TypeScript et le shell Electron
ont été retirés (ADR-007, 05/10/2026) ; la présentation est portée par les
fenêtres natives du Launcher. Les tests couvrent les suites Python (361 tests
verts, couverture 91,57 %). Des adaptateurs Linux permettent au Launcher de
démarrer, sonder et arrêter le service ECHOS.

**Contrat d’intégration.** L’API REST expose les opérations headless attendues
par le service d’analyse du Launcher (`/analysis/run`, `/analysis/experiment`
et `/analysis/report`, J2B accepté). L’installation Linux est validée sur
machine propre (venv vierge, 212 Mo, imports et tests verts — étape 9).
**Reste ouvert : l’acceptation pilotée depuis l’interface du Launcher** dans
le parcours J3 (étape 4).

### `syne-mock`

**Présent dans le dépôt.** Serveur Node.js déterministe destiné à émuler le
contrat de transport pour le développement et les intégrations clientes. Il
possède un manifeste Linux, un cycle de vie supervisable et 46 tests.

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
`launcher/V1-CAPABILITY-MATRIX.md` §4-7).

## 7. Limites et travaux encore nécessaires

Le plan de sortie fait foi : **`ROADMAP-V01.md`**. Les étapes 1, 2, 3, 7, 9,
10 et 11 y sont cochées avec leurs preuves ; les écarts restants sont :

1. **Parcours J3 de bout en bout (étape 4)** : campagne Console réelle avec
   interruption → reprise sans rejouer un run réussi, archivage des artefacts
   d’analyse dans le paquet, contrôle des erreurs composant par composant, et
   E2E complet au-delà de `PublishedSyneCampaignEndToEndTests.cs` — en CI Linux
   puis CI Windows.
2. **Support Windows (étape 5)** : manifestes sans clé `executable.windows`,
   CI 100 % `ubuntu-latest` — rédiger les clés par OS et les jobs
   `windows-latest`, puis valider localement sous Windows `ManifestDetector`,
   `ProcessTreeKiller`, `ProcessRunExecutor` et `--check`.
3. **Validation transverse V3 (étape 6, hors long-run — arbitrage A2)** :
   cadence contrôlée, backpressure et lag mesurés (sinon affichage « non
   fourni »), reprise du worker ECHOS après mort violente du flux SYNE, parcours
   UI complets du Launcher.
4. **Publication multiplateforme (étape 8)** : artefacts `linux-x64` et
   `win-x64` produits en CI pour SYNE, ECHOS, `syne-mock` et le Launcher,
   manifestes alignés et tags composants selon `VERSIONING.md`.
5. **PRISM** : rendu interactif Unreal et validation d’intégration — hors
   périmètre V0.1, porte d’intégration séparée.
6. **Fiabilité des preuves** : un test de débit (§5) fragile sous charge
   machine, une couverture mesurée avec deux classes exclues, et 13 tests
   d’intégration qui ne dépendent que de la CI : ces réserves doivent rester
   explicites tant qu’elles ne sont pas levées par des jobs CI ou des
   correctifs dédiés.

### Conclusion

Le dépôt contient une base logicielle substantielle et testée pour le Launcher,
SYNE, ECHOS et `syne-mock` (1 205 tests verts, 0 échec, couvertures au-dessus
de 80 %), ainsi qu’un plugin PRISM hors périmètre de sortie. Les contrats
inter-composants (J2A, J2B), l’installation ECHOS Linux et les campagnes
scientifiques rejouées par `reset` sont désormais établis ; ce qui manque pour
déclarer **LIVEX V0.1** tient aux preuves d’extrémité (J3), au support
Windows, à la validation V3 et à la publication multiplateforme, toutes
inscrites dans `ROADMAP-V01.md`. Ce rapport décrit **l’état du code versionné
et des validations réalisées au 08/10/2026**, et non une certification que
toutes les fonctionnalités V1 prévues sont opérationnelles.
