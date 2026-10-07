# LIVEX — état réel du projet

> Rapport d’inventaire et de maturité du dépôt. Il distingue le code présent,
> les validations exécutées et les intégrations effectivement disponibles.

| Référence | Valeur |
| :-- | :-- |
| Date de l’état observé | 2 octobre 2026 |
| Branche et commit | `develop` — `90f06463b02ce4630a4ef763c519867be1dff82f` |
| Fichiers suivis par Git | 618 |
| Résultat principal des suites locales | 1 086 réussis, 2 ignorés, 0 échec |
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
d’ECHOS (ingestion, analyse, API et interface), du projet Unreal PRISM et de
son plugin d’intégration PRISM-LDK, de `syne-mock` et du Launcher. Le Launcher
est une application de bureau Avalonia avec ses couches de domaine,
d’orchestration, d’infrastructure, de présentation et de gestion des paquets.

Au commit de référence, l’inventaire Git contient **618 fichiers**. La
classification de ce rapport en dénombre **226 fichiers source, 129 fichiers
de tests, 149 documents, 73 fichiers de configuration, 38 assets et 3 fichiers
autres**. Tokei détecte **61 953 lignes de code au sens de son analyseur** sur
les fichiers suivis ; ce chiffre inclut notamment les données JSON, les
fichiers de projets et les configurations et ne représente donc pas les seules
lignes de code applicatif.

Pour une mesure plus proche du code exécutable, le rapport isole **48 775 lignes
Tokei de code** dans les langages de programmation du dépôt, en excluant les
assets SVG et les exemples de code repérés dans la documentation. Ce total
comprend **30 644 lignes source et scripts** et **18 131 lignes de tests**.
Tokei exclut les lignes vides et les commentaires de ses comptes « code ».

Les suites effectivement exécutées localement totalisent **1 086 tests réussis
et 2 tests ignorés**, sans échec : SYNE 569, Launcher 102, ECHOS Python 309,
ECHOS UI 62 et `syne-mock` 44. Les deux tests ECHOS ignorés localement
correspondent aux intégrations avec SYNE et le mock ; des jobs CI dédiés les
ont exécutés séparément avec succès.

**Ces résultats ne signifient pas que toutes les capacités prévues de LIVEX
sont livrées.** En particulier, l’exécution de campagnes par SYNE réel,
l’analyse de campagne par les opérations attendues du Launcher, et l’expérience
de rendu PRISM restent incomplètes ou incompatibles avec les contrats actuels.

## 2. Périmètre et méthode

### Référence du comptage

- Le périmètre est le contenu **suivi par Git** au commit indiqué ci-dessus,
  obtenu depuis `git ls-files`. Les fichiers générés non suivis — dont le
  présent rapport lorsqu’il est produit — ne sont pas inclus dans les 618.
- Les répertoires de dépendances, environnements virtuels, caches, sorties de
  compilation et distributions ignorés par Git ne sont pas comptés.
- La ventilation « source / tests / documentation / configuration / assets /
  autres » est une classification pratique fondée sur les chemins et
  extensions. Elle décrit ce dépôt et non une taxonomie universelle.
- Les lignes de code par langage et les lignes source/tests sont analysées avec
  **Tokei 13.0.0** sur les fichiers suivis. Les nombres de lignes désignent les
  lignes classées comme code par Tokei, pas le nombre de lignes physiques.
- Les statistiques de code source/tests ci-dessous isolent les langages
  applicatifs et les scripts ; les fichiers de configuration et de données,
  les assets SVG et les exemples de code dans la documentation ne sont pas
  assimilés à du code applicatif.
- Les résultats de tests sont ceux des commandes/suites exécutées dans la
  validation de cette révision. Un résultat de test ne prouve ni l’exhaustivité
  fonctionnelle ni la compatibilité entre composants.

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

## 3. Structure et inventaire des fichiers

### Répartition des fichiers suivis

| Catégorie | Fichiers | Part |
| :-- | --: | --: |
| Code source | 226 | 36,6 % |
| Tests | 129 | 20,9 % |
| Documentation | 149 | 24,1 % |
| Configuration et manifests | 73 | 11,8 % |
| Assets | 38 | 6,1 % |
| Autres | 3 | 0,5 % |
| **Total** | **618** | **100 %** |

### Répartition par répertoire de premier niveau

| Répertoire | Fichiers suivis | Contenu principal |
| :-- | --: | :-- |
| `echos/` | 148 | Backend Python, tests, interface React/TypeScript et application Electron |
| `syne/` | 146 | Moteur .NET, outils console et tests |
| `docs/` | 117 | Documentation des composants et références techniques |
| `launcher/` | 107 | Application Avalonia, bibliothèques, outils et tests |
| `prism/` | 35 | Hôte Unreal technique et plugin PRISM-LDK |
| `syne-mock/` | 29 | Serveur Node.js, tests et documentation d’intégration |
| Racine du dépôt | 22 | Documentation et fichiers communs au projet |
| `.github/` | 8 | Workflows et modèles GitHub |
| `scripts/` | 4 | Scripts de développement et d’intégration |
| `configs/` | 2 | Configurations partagées |
| **Total** | **618** | |

### Volume de code source et de tests par composant

Les nombres de lignes de ce tableau sont des comptes Tokei. « Source » inclut
les scripts partagés ; « tests » désigne les fichiers des suites de tests. Le
nombre de fichiers de tests est fourni séparément pour rendre la portée de la
ventilation vérifiable.

| Composant / ensemble | Source et scripts (LOC) | Tests (LOC) | Fichiers de tests |
| :-- | --: | --: | --: |
| SYNE | 9 843 | 9 336 | 69 |
| ECHOS (backend et UI) | 8 917 | 5 353 | 41 |
| Launcher | 8 238 | 2 624 | 14 |
| `syne-mock` | 1 541 | 818 | 5 |
| PRISM / PRISM-LDK | 888 | 0 | 0 |
| Scripts partagés | 1 217 | 0 | 0 |
| **Total** | **30 644** | **18 131** | **129** |

Le « 0 » PRISM signifie qu’aucun fichier de test n’a été classé dans son
répertoire au moment de l’inventaire ; il ne signifie pas que le plugin ne
dispose d’aucune validation manuelle ou d’aucun test dans l’outillage Unreal.

## 4. Langages et lignes de code

Tokei compte ici les fichiers suivis qu’il reconnaît. Les statistiques
comprennent les sources, les tests, la documentation et les formats de
configuration ; elles ne sont pas toutes des lignes de code applicatif.

| Langage / format reconnu | Fichiers Tokei | Lignes « code » |
| :-- | --: | --: |
| C# | 206 | 29 098 |
| Python | 67 | 11 072 |
| JSON | 31 | 11 029 |
| JavaScript | 25 | 2 646 |
| TSX | 29 | 2 453 |
| TypeScript | 12 | 1 037 |
| AXAML | 2 | 989 |
| C Header | 5 | 439 |
| YAML | 4 | 424 |
| C++ | 3 | 403 |
| CSS | 1 | 399 |
| MSBuild | 20 | 311 |
| Shell | 4 | 227 |
| SVG | 17 | 204 |
| INI | 4 | 188 |
| Visual Studio Solution | 2 | 168 |
| HTML | 2 | 101 |
| RPM Specfile | 1 | 60 |
| Unreal Project | 1 | 31 |
| TOML | 1 | 25 |
| Unreal Plugin | 1 | 24 |
| Markdown | 145 | 0 |
| Plain Text | 3 | 0 |

Le total global affiché par Tokei est **61 953 lignes « code »** toutes
catégories confondues. Les lignes des formats de données et de configuration
(JSON, MSBuild, YAML, etc.) et les SVG ne doivent pas être additionnées aux
LOC applicatives. Pour les langages applicatifs et scripts uniquement, après
exclusion des SVG et des exemples de documentation, le total de référence est
**48 775 LOC** : **30 644 LOC source/scripts** et **18 131 LOC de tests**.

Les lignes reconnues individuellement par langage ne s’additionnent pas
exactement au total global de Tokei ; un écart de **625 lignes** reste dans
l’agrégat plutôt que d’être attribué à une ligne du tableau. Les comptes de
fichiers par langage sont ceux des rapports Tokei et peuvent refléter ses
attributions aux formats intégrés ; pour le nombre de fichiers versionnés, la
référence reste `git ls-files` (618).

## 5. Tests et validations

### Résultats observés

| Composant | Suite / type | Résultat local |
| :-- | :-- | :-- |
| SYNE | `Simulation.Core.Tests` | 508 réussis |
| SYNE | `Simulation.Console.Tests` | 61 réussis |
| ECHOS | Python : backend, analyse, ingestion et contrats | 309 réussis, 2 ignorés ; couverture mesurée : 93,04 % |
| ECHOS | Interface React/TypeScript | 62 réussis dans 14 fichiers de test |
| Launcher | Tests unitaires | 84 réussis |
| Launcher | Tests d’intégration | 7 réussis |
| Launcher | Tests end-to-end | 11 réussis |
| `syne-mock` | Suite Node.js | 44 réussis |
| PRISM / PRISM-LDK | Tests automatisés dans cette validation | Aucun lancé |
| **Total des suites locales** | **Exécutions comptabilisées** | **1 086 réussis, 2 ignorés, 0 échec** |

Les deux tests ECHOS ignorés en local sont les parcours d’intégration qui
demandent respectivement un SYNE Release et le serveur `syne-mock`. Les jobs
d’intégration dédiés de la CI ont exécuté ces deux parcours et les ont réussis.
Ces exécutions CI sont indiquées séparément et ne sont pas ajoutées au total
local ci-dessus.

Les checks de la PR Launcher #507 ciblant `develop` ont réussi, incluant les
tests des composants concernés et les builds Electron Linux et Windows. Cela
valide les checks de cette PR à la date de la fusion, pas chaque plateforme et
chaque scénario d’intégration en fonctionnement sur la machine locale.

### Interprétation

- Un test vert confirme le comportement couvert par ce test ; il ne valide pas
  l’ensemble du contrat entre composants.
- Les tests SYNE et `syne-mock` valident des produits distincts. Les résultats
  ne démontrent pas que leurs simulations sont scientifiquement équivalentes.
- Les tests du Launcher couvrent les règles du Launcher, l’infrastructure et
  les parcours simulés ; ils ne rendent pas opérationnelles les API absentes de
  SYNE ou d’ECHOS.
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
déclarées pour ECHOS et `syne-mock`. SYNE réel ne fournit pas encore le contrat
de manifeste, d’arguments, de readiness et d’arrêt attendu par le Launcher.
Les chemins d’analyse réels du Launcher ne correspondent pas aux opérations
actuelles de l’API ECHOS. Le Launcher ne doit donc pas être interprété comme
capable d’exécuter de bout en bout une campagne scientifique avec les
composants réels.

### SYNE — moteur réel

**Présent dans le dépôt.** Moteur .NET de simulation, outils CLI et serveur de
contrôle/observabilité, avec tests dédiés. Le README décrit notamment le PRNG,
le monde, les entités, les traits et la boucle de simulation.

**Limite d’intégration.** Le mode serveur actuel ne termine pas un run selon le
contrat batch du Launcher et ne prend pas en charge l’ensemble des arguments,
sondes et routes d’arrêt attendus. Il n’y a pas de manifeste Launcher SYNE
compatible. Lancer le service de contrôle n’équivaut donc pas à lancer une
campagne `.livexp` supervisée par le Launcher.

### ECHOS

**Présent dans le dépôt.** Backend Python pour ingestion, stockage et analyse,
API REST, interface web React/TypeScript et shell Electron. Les tests couvrent
les suites Python et UI. Des adaptateurs Linux permettent au Launcher de
démarrer, sonder et arrêter le service ECHOS, sous réserve d’une configuration
valide de sa base analytique.

**Limite d’intégration.** L’API REST existante permet notamment de consulter
des runs et des métriques, mais ne fournit pas les opérations
`/analysis/run`, `/analysis/experiment` et `/analysis/report` attendues par le
service d’analyse du Launcher. Une interface disponible ou un backend sain ne
prouve donc pas que le Launcher peut générer un rapport de campagne réelle.

### `syne-mock`

**Présent dans le dépôt.** Serveur Node.js déterministe destiné à émuler le
contrat de transport pour le développement et les intégrations clientes. Il
possède un manifeste Linux, un cycle de vie supervisable et 44 tests.

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
constituent pas à eux seuls l’expérience complète de rendu 3D prévue. Les
objectifs de rendu et d’interaction documentés sont une feuille de route, pas
des fonctions automatiquement livrées. Le profil PRISM reste verrouillé dans
le Launcher tant que ce composant n’est pas implémenté/intégré.

## 7. Limites et travaux encore nécessaires

Les principaux écarts visibles à ce commit sont :

1. **Contrat batch SYNE ↔ Launcher** : aligner arguments de run, démarrage,
   readiness, export des résultats, fin de run et arrêt propre ; fournir et
   tester un manifeste compatible.
2. **Analyse ECHOS pilotée par le Launcher** : définir et implémenter les
   opérations d’analyse d’expérience/campagne et de génération de rapport
   attendues, ou adapter explicitement les services du Launcher au contrat
   REST réellement offert par ECHOS.
3. **Campagnes de bout en bout** : vérifier l’exécution multi-run, l’agrégation
   et la traçabilité des résultats avec les composants réels, et non uniquement
   avec des stubs ou le mock.
4. **PRISM** : poursuivre le rendu interactif Unreal et les tests de validation
   de l’intégration sur un environnement disposant de l’outillage Unreal.
5. **Matrice de plateformes et de modes** : différencier les capacités
   réellement disponibles par OS et par mode ; les manifestes Launcher fournis
   par ECHOS et `syne-mock` sont Linux seulement, même si des builds Electron
   Linux/Windows existent en CI.
6. **Métriques de flux et intégration UI/headless** : certaines métriques de
   flux et variantes d’exécution de composants ne sont pas fournies par les
   composants actuels ; l’interface doit signaler leur indisponibilité plutôt
   que fabriquer des mesures.

### Conclusion

Le dépôt contient une base logicielle substantielle et testée pour le Launcher,
SYNE, ECHOS et `syne-mock`, ainsi qu’un plugin PRISM en développement. La
couverture de tests observée est solide sur les suites présentes, mais les
contrats manquants entre le Launcher, SYNE et les opérations d’analyse ECHOS
restent les facteurs déterminants pour déclarer l’orchestration scientifique
complète. Ce rapport décrit donc **l’état du code versionné et des validations
réalisées**, et non une certification que toutes les fonctionnalités V1
prévues sont opérationnelles.
