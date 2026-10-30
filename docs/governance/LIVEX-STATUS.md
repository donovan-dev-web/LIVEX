# LIVEX — état du projet

**Composant** : LIVEX (général) · **Statut** : [EN VALIDATION] · **Dernière mise à jour** : 19 octobre 2026
**Dépend de** : [`ROADMAP.md`](ROADMAP.md), [`VERSIONING.md`](../../VERSIONING.md), manifestes, contrats et tests des composants

> État de référence documentaire, non release. Les mesures locales du 19/10 sont distinguées des résultats historiques et des rapports de campagne antérieurs; un résultat d’un composant ne certifie pas toute la pile.

| Référence | Valeur |
| :-- | :-- |
| Date des vérifications locales | 19 octobre 2026 |
| Branche et HEAD | `feature/livex-realignement-divergences-v0.2` — `a229c6a` (checkout local avec modifications en cours) |
| Versions majeures déclarées | Global LIVEX `V0.2`; SYNE `0.17.0`; ECHOS `0.1.0`; Launcher `0.1.0`; `syne-mock` `0.1.0`; PRISM / PRISM-LDK en version de travail |
| Contrats / schémas | Observabilité `0.5.0`; `world_initialized` `1.1`; snapshot SYNE `schemaVersion 5`; ECHOS `SCHEMA_VERSION = 6` |
| Suites locales pertinentes | SYNE 695/695; ECHOS 383 réussis, 14 ignorés; `syne-mock` 51 réussis, 2 ignorés; Launcher Unit 174/174, Integration 20/20, E2E 33/33 avec SYNE publié |
| Release | V1 en préparation (installateur, PRISM, composants) |

## Sommaire

1. [Synthèse](#1-synthèse)
2. [Périmètre et méthode](#2-périmètre-et-méthode)
3. [Versions et contrats](#3-versions-et-contrats)
4. [Inventaire des tests](#4-inventaire-des-tests)
5. [Inventaire du code, des fichiers et des langages](#5-inventaire-du-code-des-fichiers-et-des-langages)
6. [Vérifications exécutées le 19 octobre](#6-vérifications-exécutées-le-19-octobre)
7. [État fonctionnel par composant](#7-état-fonctionnel-par-composant)
8. [Limites et portes non franchies](#8-limites-et-portes-non-franchies)
9. [Sources de référence](#9-sources-de-référence)

## 1. Synthèse

LIVEX réunit le moteur déterministe SYNE, l’observatoire headless ECHOS, le projet Unreal PRISM et le plugin PRISM-LDK, l’application native d’orchestration Launcher et l’émulateur de contrats `syne-mock`. SYNE reste l’autorité de l’état simulé; ECHOS observe et analyse; PRISM consomme les contrats pour le rendu; le Launcher orchestre et archive. Le mock aide au développement, mais ne constitue pas un moteur scientifique équivalent.

Les identifiants vérifiés dans le code et les manifestes sont SYNE `0.17.0`, contrat d’observabilité `0.5.0`, description `world_initialized` `1.1`, snapshot `schemaVersion 5`, ECHOS `0.1.0`, Launcher `0.1.0` et `syne-mock` `0.1.0`. Le schéma ECHOS demande une réserve : `SCHEMA_VERSION` vaut encore `"6"`, alors que la migration BL-09 et ses tests nomment v7 la reconstruction de `decision_traces`. Ce rapport ne confond pas cette migration avec le compteur de schéma effectivement écrit dans `_meta`.

Le 19/10, SYNE a passé 604 tests Core et 91 Console. ECHOS a passé 383 tests, avec 14 ignorés (couverture mesurée 91,32 %); `syne-mock` a passé 51 tests, avec 2 ignorés sous Windows. Launcher Unit et Integration sont verts (174/174 et 20/20). Avec un SYNE publié réellement installé selon la disposition attendue, Launcher EndToEnd donne **33 réussis et 0 échec** : les deux parcours J3 réels (SYNE publié + ECHOS réel) passent désormais, la régression d’ingestion ECHOS qui bloquait leur assertion de persistance ayant été corrigée (voir § 8). Dans un run séparé sans le prérequis SYNE, les 4 tests gatés sont comptés comme échecs au lieu d’être marqués ignorés par le runner; ce run n’est pas inclus dans les 33 cas exécutés avec SYNE publié. Le parcours J3 complet est donc **accepté par la vérification locale du 19/10**.

Les tests et campagnes ne prouvent pas l’émergence générale. Les résultats expérimentaux pérennes rapportent un contraste préenregistré de quête spatiale confirmé, un cycle homéostatique social non confirmé et un verdict général « émergence non établie ». La viabilité locale est établie seulement pour les graines, configurations et horizons explicitement rapportés. PRISM, l’installateur et les livraisons restent soumis à leurs propres portes; aucune release publique n’est déclarée.

## 2. Périmètre et méthode

- Ce relevé porte sur le checkout local de la branche `feature/livex-realignement-divergences-v0.2`; le nom de branche et le HEAD sont ceux observés au début de cette révision. Le checkout est modifié; il ne décrit ni un arbre propre ni un état distant `develop`.
- Les versions sont relevées dans le code et les manifestes. Les résultats de test sont ceux de commandes locales; les rapports de campagne et d’acceptation d’autres dates sont cités comme tels, jamais présentés comme des exécutions du 19/10.
- Les sections 4 et 5 ci-dessous donnent un inventaire recalculé sur le checkout présent : inventaire de code du 18/10/2026 (§5, non relancé le 19/10) et résultats de test du 19/10/2026 (§4). Tokei analyse les fichiers visibles et non ignorés après exclusion explicite des dépendances, builds et caches indiqués; le checkout est fortement modifié et contient des suppressions, ajouts non suivis et fichiers locaux. Les métriques ne sont donc pas un instantané de `HEAD` seul ni un décompte des seuls fichiers suivis par Git.
- Les chiffres de tests exécutés sont les résultats locaux consignés pour le 19/10; les catégories unit/integration/E2E suivent le type des suites/projets, et un total logique sans double compte est fourni seulement pour les suites dont le nombre et le résultat sont connus. Les 69 specs PRISM documentées ne sont pas ajoutées au total exécuté, car elles n’ont pas été relancées dans cette vérification.
- Un test vert valide le comportement couvert; il ne certifie ni toute la compatibilité inter-composants ni une conclusion scientifique générale.

## 3. Versions et contrats

| Composant | Valeurs déclarées vérifiées | État résumé |
| :-- | :-- | :-- |
| SYNE | Moteur `0.17.0`; observabilité `0.5.0`; description de monde `1.1`; snapshot `schemaVersion 5` | Moteur .NET autoritaire, contrôle, flux et persistance. |
| ECHOS | Paquet `0.1.0`; métriques `2.0.0`; SQLite `SCHEMA_VERSION = "6"` | Service headless d’ingestion et d’analyse. Migration nullable BL-09 est nommée v7 dans les commentaires/tests, sans incrément du compteur global. |
| Launcher | `0.1.0`; paquet `.livexp` versionné séparément | Application Avalonia d’orchestration, présentation et archivage. |
| `syne-mock` | `0.1.0`; contrats SYNE `0.5.0` / monde `1.1` | Émulateur de contrats pour tests clients, pas une simulation équivalente. |
| PRISM / PRISM-LDK | Version de travail; consommateur observabilité `0.5.0` / monde `1.1` | Plugin Unreal en intégration; ne signifie pas que l’expérience interactive complète est acceptée. |
| Installateur | 1.0.0 (V1) | Spécification et code livrés ; release en préparation |

Le code déclare `SCHEMA_VERSION = "6"` dans [`sqlite.py`](../../echos/echos/storage/sqlite.py); une migration y reconstruit `decision_traces` pour rendre `utility` nullable et ajoute `held_utility`, `decision_state` et `decision_tick`. Les tests appellent cela « v7 », mais la valeur écrite dans `_meta.schema_version` est toujours celle de la constante `6`. Les mentions « schéma v7 » doivent donc être qualifiées de révision de migration BL-09 jusqu’à harmonisation du versionnage.

## 4. Inventaire des tests

### Nombre de tests et état observé

Le relevé local du 19/10/2026 compte **1 372 cas de test distincts** dans les suites exécutées : **1 356 réussis, 16 ignorés et 0 échec**. Le total additionne SYNE (695), ECHOS (383 réussis + 14 ignorés = 397), `syne-mock` (51 + 2 ignorés = 53) et Launcher avec SYNE publié (174 + 20 + 33 = 227). Un run Launcher distinct sans SYNE publié compte 4 erreurs de prérequis; il est hors de cette série E2E de 33 tests et donc hors total. Les 69 cas PRISM rapportés dans sa documentation antérieure ne sont pas inclus, car ils n’ont pas été exécutés pendant cette vérification.

| Composant | Type de suite | Cas détectés / résultat consigné le 19/10 | Interprétation |
| :-- | :-- | :-- | :-- |
| SYNE | Unitaire (Core) | 604 réussis, 0 échec | Suite `Simulation.Core.Tests`. |
| SYNE | Intégration (Console) | 91 réussis, 0 échec | Suite `Simulation.Console.Tests`; total SYNE 695/695. |
| ECHOS | Suite pytest mixte (API, stockage, analyse, ingestion) | 383 réussis, 14 ignorés, 0 échec | Les types unitaires et d’intégration partagent le même runner; pas de ventilation fiable par cas. +1 test de régression d’ingestion depuis le 18/10. |
| ECHOS | Intégration SYNE réel | 13 tests ignorés localement | Requiert le moteur publié; inclus dans les 14 ignorés pytest. |
| ECHOS | Intégration `syne-mock` | 1 test ignoré localement | Requiert le serveur mock; inclus dans les 14 ignorés pytest. |
| `syne-mock` | Unitaire / serveur / contrat | 51 réussis, 2 ignorés, 0 échec | Tests Node; deux tests de signaux d’arrêt ignorés sous Windows. |
| Launcher | Unitaire | 174 réussis, 0 échec | `Launcher.Tests.Unit`. |
| Launcher | Intégration | 20 réussis, 0 échec | `Launcher.Tests.Integration`. |
| Launcher | End-to-end réel | 33 cas : 33 réussis, 0 échec | SYNE publié; les deux parcours J3 réels passent. |
| PRISM / PRISM-LDK | Automation Specs Unreal | 69 annoncées dans `docs/prism/TESTING.md` : 65 réussies, 4 avec avertissements | Rapport documentaire UE 5.8, non relancé le 19/10; exclu du total exécuté le 19/10. |

**Bilan local du 19/10 : 1 372 cas de test = 1 356 réussis + 16 ignorés + 0 échec.** Il additionne SYNE, ECHOS, `syne-mock` et Launcher; les résultats PRISM antérieurs (69 specs documentées) sont exclus de ce total.

> Les chiffres suivent les compteurs des runners, et non les fichiers de test. Les tests d’intégration ECHOS sont inclus dans la suite pytest et ne sont pas additionnés une seconde fois. Le résultat Launcher E2E de référence est celui obtenu avec SYNE publié (33/33, exit 0); le run distinct sans prérequis, où quatre cas deviennent des erreurs, n’est pas dans ce décompte.

### Ventilation par niveau

| Niveau | Suites incluses | Cas documentés | Résultat / réserve |
| :-- | :-- | --: | :-- |
| Unitaire (suite dédiée) | SYNE Core + Launcher Unit | **778** (604 + 174) | Catégorie fiable à partir des projets de tests dédiés. |
| Intégration (suite dédiée) | SYNE Console + Launcher Integration | **111** (91 + 20) | Catégorie fiable à partir des projets de tests dédiés. |
| End-to-end | Launcher E2E réel | **33** (33 réussis, 0 échoué) | J3 sur SYNE publié + ECHOS réel : vert. |
| Types mêlés / non ventilés dans leur runner | ECHOS + `syne-mock` | **450** (397 + 53) | pytest et Node.js réunissent tests unitaires, API, stockage et intégration; le statut disponible ne permet pas d’attribuer honnêtement chaque cas à un seul niveau. Leurs 16 skips sont déjà compris dans 450. |
| Unreal Automation | PRISM / PRISM-LDK | 69 documentés, non exécutés le 19/10 : 65 réussis, 4 `SuccessWithWarnings` | Résultat antérieur, exclu du total exécuté le 19/10. |

La ventilation des suites totalise **1 372 cas comptabilisés** : 778 tests dans les projets unitaires dédiés + 111 dans les projets d’intégration + 33 E2E + 450 cas ECHOS/`syne-mock`, dont le type interne n’est pas séparé. Elle inclut les 16 skips ECHOS/mock et aucun échec. Les 69 tests Unreal sont antérieurs et rapportés séparément.

## 5. Inventaire du code, des fichiers et des langages

### Périmètre et règles de comptage

Inventaire exécuté sur le checkout local visible le 18/10/2026 avec **Tokei 13.0.0**. Commande reproductible depuis la racine :

```bash
tokei . --exclude .git --exclude node_modules --exclude bin --exclude obj \\
  --exclude .venv --exclude .pytest_cache --exclude __pycache__ \\
  --exclude dist --exclude .astro --exclude coverage -C
```

La commande compte les fichiers visibles/non ignorés par Tokei, pas tous les fichiers physiquement présents. Elle reconnaît **905 fichiers répartis dans 22 langages/formats** (dont **11 langages de programmation, d’interface ou de script**, hors HTML documentaire; le rapport global inclut aussi des blocs de code embarqués dans Markdown/HTML, détaillés séparément ci-dessous). L’index Git contient 888 chemins; à cette date, 885 des chemins indexés existent encore, 3 sont supprimés, et 91 fichiers non suivis/non ignorés sont présents. Il ne faut donc pas assimiler « 905 fichiers analysés », « chemins suivis Git » et « fichiers physiques ».

Le tableau sépare les langages de programmation et formats de code, les fichiers de configuration/données, la documentation et les manifestes. Les LOC Tokei sont des lignes classées comme code par le tokenizer, pas des lignes physiques; elles peuvent compter des fichiers générés locaux encore visibles et des exemples source embarqués dans la documentation. Le rapport JSON détaillé de Tokei fournit des comptes par fichier/langage (88 859 LOC primaires) et un total global (91 268 LOC), dont l’écart de 2 409 LOC correspond aux blocs embarqués dans les documents. Le tableau indique la somme par format, puis cet écart et le total global; les blocs embarqués ne sont pas ventilés par langage.

### Langages/formats reconnus par Tokei

| Langage / format | Fichiers | Lignes code Tokei |
| :-- | --: | --: |
| C# | 343 | 45 466 |
| Python | 81 | 17 619 |
| JavaScript | 23 | 3 308 |
| C++ | 11 | 2 649 |
| AXAML | 4 | 2 120 |
| Astro | 66 | 5 714 |
| CSS | 3 | 960 |
| C Header | 8 | 681 |
| TypeScript | 9 | 480 |
| Shell | 2 | 102 |
| Batch | 1 | 9 |
| **Sous-total langages/scripts (HTML exclu)** | **551** | **79 108** |
| HTML documentaire (exemples) | 3 | 839 |
| **Langages/scripts + HTML documentaire** | **554** | **79 947** |
| JSON | 33 | 7 257 |
| MSBuild | 20 | 313 |
| INI | 4 | 244 |
| Visual Studio Solution | 2 | 168 |
| TOML | 1 | 25 |
| Unreal Project | 1 | 31 |
| Unreal Plugin | 1 | 24 |
| SVG | 30 | 850 |
| Markdown | 254 | 0 |
| Plain Text | 5 | 0 |
| **Somme des lignes par format (blocs embarqués exclus)** | **905 fichiers distincts** | **88 859** |
| LOC dans les blocs embarqués Markdown/HTML | Fichiers déjà comptés ci-dessus | 2 409 |
| **Total Tokei global** | **905 fichiers distincts** | **91 268** |

Tokei distingue également **163 532 lignes physiques reconnues** au total, dont 91 268 code au total global (88 859 LOC des rapports primaires + 2 409 LOC dans les blocs embarqués), 47 602 commentaires et 24 662 lignes blanches. Les langages de programmation, d’interface et scripts comptent **79 108 LOC dans 551 fichiers**; les trois exemples HTML documentaires ajoutent **839 LOC dans 3 fichiers**. Ces métriques sont un snapshot du checkout modifié, pas une mesure de la branche distante.

### Fichiers de code source et de tests par composant

Pour cette ventilation, « source » désigne les fichiers Tokei des langages de programmation/UI/scripts (C#, Python, JavaScript/TypeScript, C/C++, Astro, AXAML, CSS, Shell, Batch), hors les 3 exemples HTML documentaires. Un fichier est classé test selon son chemin (`tests/`) ou son nom (`test_`, `*_test.py`, `*.test.*`, `*Tests.cs`, `*Spec.cpp`). Cette heuristique identifie **163 fichiers de test** (27 275 LOC) dans 551 fichiers code/scripts; les 388 autres fichiers totalisent 51 833 LOC. Ces comptes de fichiers ne sont pas le nombre de cas de test exécutés.

| Répertoire / composant | Fichiers code (non-tests) | LOC code (non-tests) | Fichiers de tests | LOC tests | Fichiers code total | LOC total |
| :-- | --: | --: | --: | --: | --: | --: |
| SYNE (`syne/`) | 144 | 12 940 | 89 | 12 336 | 233 | 25 276 |
| Launcher (`launcher/`) | 80 | 16 108 | 31 | 6 224 | 111 | 22 332 |
| ECHOS (`echos/`) | 43 | 8 656 | 33 | 6 473 | 76 | 15 129 |
| `syne-mock/` | 15 | 1 778 | 5 | 1 335 | 20 | 3 113 |
| PRISM (`prism/`) | 18 | 2 470 | 5 | 907 | 23 | 3 377 |
| Scripts partagés | 7 | 2 059 | 0 | 0 | 7 | 2 059 |
| Site (`site/`) | 79 | 7 185 | 0 | 0 | 79 | 7 185 |
| Documentation outillée (`docs/`) | 2 | 637 | 0 | 0 | 2 | 637 |
| **Total code/scripts** | **388** | **51 833** | **163** | **27 275** | **551** | **79 108** |

PRISM comprend cinq fichiers dont le chemin relève de la convention de test (spécifications/tests Unreal), mais cela ne représente pas cinq tests exécutés ici. L’inventaire de code ne contient pas les exemples HTML (839 LOC), les formats de configuration/données (dont 7 257 LOC JSON), les assets SVG, ni les exemples de code embarqués dans Markdown; ces catégories figurent dans la table Tokei générale. Les sources de vérité des résultats exécutés restent les tableaux de la section 4.

## 6. Vérifications exécutées le 19 octobre

| Suite | Résultat local | Notes |
| :-- | :-- | :-- |
| SYNE Core | 604 réussis, 0 échec | Release; inclus dans `Syne.sln`. |
| SYNE Console | 91 réussis, 0 échec | Release; 695/695 au total SYNE. |
| ECHOS | 383 réussis, 14 ignorés, 0 échec; 91,32 % | pytest-cov, Python 3.12.10; avertissement Starlette/httpx. |
| `syne-mock` | 51 réussis, 2 ignorés, 0 échec | Ignorés pour SIGTERM/SIGINT sous Windows. |
| Launcher Unit | 174 réussis, 0 échec | Release. |
| Launcher Integration | 20 réussis, 0 échec | Release. |
| Launcher EndToEnd avec SYNE publié | **33 réussis, 0 échec** | Les deux parcours J3 réels (SYNE publié + ECHOS réel) passent; exit 0, durée 4 m 23 s. Vérification effectuée avec la disposition de publication attendue par le banc. |
| Launcher EndToEnd sans prérequis publié | **4 échecs de prérequis dans un run distinct** | Les tests demandent un skip via `SkipException.ForSkip`, mais ce runner les comptabilise comme échecs. Pas une preuve d’échec du produit; ce run sans prérequis n’est pas inclus dans les 33 cas exécutés avec SYNE publié. |
| PRISM / PRISM-LDK | Non exécuté pendant cette vérification | [`docs/prism/TESTING.md`](../prism/TESTING.md) rapporte 69 Automation Specs UE 5.8 (65 réussies, 4 `SuccessWithWarnings`, 0 échec); rapport documentaire distinct. |

Le total local comptabilisé par les runners (1 372) comprend 1 356 réussites, 16 skips (ECHOS et `syne-mock`) et 0 échec. Les 69 Automation Specs Unreal (65 succès et 4 avertissements) sont un résultat documentaire antérieur et ne sont pas ajoutées à ce relevé du 19/10. Le run Launcher E2E de référence est celui avec SYNE publié.

### Couverture

- **ECHOS : 91,32 %** (6 625/7 255 statements); seuil configuré : 80 %.
- **SYNE :** les rapports Cobertura locaux donnent Core 91,17 % (6 034/6 618 lignes) et Console 63,46 % (4 988/7 859). L’ancien agrégat SYNE de 87,41 % reste daté du 12/10 et n’est pas actualisé ici.
- **Couverture partielle disponible : 81,20 %** — ratio pondéré descriptif des rapports présents pour ECHOS (6 625/7 255 statements), SYNE Core (6 034/6 618 lignes) et SYNE Console (4 988/7 859 lignes), soit 17 647/21 732 éléments rapportés. Ce n’est **pas la couverture globale du projet** : aucun rapport courant n’est disponible ici pour Launcher, `syne-mock` ou PRISM, et les compteurs Python (statements) et .NET (lignes) ne forment pas une union dédupliquée.
- La couverture n’est pas une mesure de conformité fonctionnelle, d’interopérabilité complète ou de validité scientifique.

## 7. État fonctionnel par composant

### SYNE

SYNE `0.17.0` est le moteur autoritaire. Sa version d’observabilité est `0.5.0`, la description `world_initialized` est `1.1`, et le codec déclare `SchemaVersion = 5`. Les suites Core et Console passent, respectivement 604/604 et 91/91. Le rapport expérimental pérenne situe la viabilité localisée au profil, aux graines et à l’horizon qu’il détaille; il ne conclut pas à une viabilité universelle ou à long terme.

### ECHOS

ECHOS `0.1.0` ingère les flux et fichiers batch, stocke les analyses et expose des API headless. Les tests locaux passent 383/383, avec 14 ignorés et 91,32 % de couverture. L’endpoint `/api/runs/{id}/viability` expose des séries et diagnostics observés, y compris le rapport de calibration; il **ne met pas en œuvre** l’indice DVI à sept composantes décrit comme projet dans la monographie. Le score composite d’émergence reste exploratoire. À clarifier séparément : révision migration BL-09 dite v7 versus constante globale `SCHEMA_VERSION = "6"`.

### Launcher

Application Avalonia `0.1.0`. Unit et Integration passent 174/174 et 20/20. EndToEnd passe **33/33 avec SYNE publié** (durée 4 m 23 s, exit 0) : les deux parcours J3 réels passent après la correction de la régression d’ingestion ECHOS. Le rapport d’acceptation archivé documentait déjà un run à 33/33 le 17 octobre; le relevé local du 19/10 le reproduit. Sans `LIVEX_SYNE_PUBLISHED_ROOT`, quatre tests conditionnels sont comptés comme échecs par le runner. L’acceptation complète J3 est donc **verte** dans le présent relevé.

### `syne-mock`

Serveur Node.js `0.1.0`, outil d’intégration qui émule des contrats. La suite locale obtient 51 réussis et 2 ignorés sous Windows pour les signaux d’arrêt. Son succès ne démontre ni une équivalence algorithmique ni des résultats scientifiques comparables à SYNE.

### PRISM / PRISM-LDK et installateur

PRISM/PRISM-LDK sont en version de travail; l’hôte `LDK.uproject` est un hôte technique, et non l’expérience produit complète. Les 69 Automation Specs UE 5.8 figurent dans le relevé de `docs/prism/TESTING.md`; elles n’ont pas été réexécutées pendant ce statut. Le rendu interactif complet et son acceptation restent des portes distinctes. L’installateur demeure une spécification sans artefact livré.

## 8. Limites et portes non franchies

1. **J3 sur composants réels : résolu le 19/10.** Les deux assertions qui échouaient dans le relevé du 18/10 provenaient d’une régression d’ingestion ECHOS : `append_tick_bundle` refusait de persister un `tick_bundle` dont l’utilité est nulle et les colonnes `held_utility`/`decision_state`/`decision_tick` absentes. Correction + test de régression; ECHOS 383/383, Launcher EndToEnd 33/33. La latence E2E reste sensible à la contention machine (un run chargé a déjà produit 32/33).
2. **Skip conditionnel E2E :** les tests déclarent un skip, mais xUnit/runner compte quatre cas comme échecs en absence de `LIVEX_SYNE_PUBLISHED_ROOT`.
3. **ECHOS DVI :** le profil de viabilité observable et l’endpoint existants ne doivent pas être confondus avec l’indice dynamique spécifié dans ECHOS VII; l’implémentation/calibration DVI n’est pas présente dans le code consulté.
4. **Numéro de schéma ECHOS :** code et tests/commentaires divergent sur v6/v7; ne pas publier « v7 » comme valeur courante de `SCHEMA_VERSION` sans harmoniser migrations et code.
5. **Portée scientifique :** les campagnes sont bornées à leurs graines, configurations, critères et horizons. Verdict global conservé : « émergence non établie ».
6. **PRISM, CI et release :** aucun test Unreal ni accès CI distant n’a été effectué et aucune release n’a été créée lors de cette mise à jour. L’inventaire de fichiers/LOC est présenté en §5, mais porte sur le checkout modifié et ses fichiers visibles, pas sur la branche distante propre.
7. **Mesures de performance bornées :** le relevé [`LIVEX-PERFORMANCE-2026-10.md`](LIVEX-PERFORMANCE-2026-10.md) porte sur une machine de bureau unique (i7-8750H, 15,8 Go). Le débit SYNE dépend fortement de l’horizon (accumulation mémoire/croyances) — un débit à horizon court ne vaut pas pour un run long — et la pile SYNE+ECHOS est bornée par l’ingestion ECHOS mono-thread (~13 t/s à 100 agents, 6 t/s visés à la cadence PRISM). PRISM n’a pas été mesuré (éditeur Unreal absent).

Les roadmaps transverse et composants font foi pour les travaux à venir. Les rapports versionnés de réalignement et d’acceptation sont indexés dans [`realignment/README.md`](realignment/README.md).

## 9. Sources de référence

- [`VERSIONING.md`](../../VERSIONING.md), [`syne/component.json`](../../syne/component.json), [`ObservabilityContract.cs`](../../syne/Simulation.Core/Observability/ObservabilityContract.cs), [`SimulationSnapshotCodec.cs`](../../syne/Simulation.Core/Persistence/SimulationSnapshotCodec.cs).
- [`echos/pyproject.toml`](../../echos/pyproject.toml), [`sqlite.py`](../../echos/echos/storage/sqlite.py), tests de migration `echos/echos/tests/test_sqlite_store.py`.
- [`Directory.Build.props`](../../launcher/Directory.Build.props), suites `launcher/Launcher.Tests.Unit`, `Launcher.Tests.Integration` et `Launcher.Tests.EndToEnd`; inventaire du code recalculé avec Tokei 13.0.0 (commande §5).
- [Tests PRISM](../prism/TESTING.md), [roadmap transverse](ROADMAP.md), [roadmap ECHOS](../echos/ROADMAP.md), [roadmap Launcher](../launcher/ROADMAP.md).
- [Rapport de performance et de tests](LIVEX-PERFORMANCE-2026-10.md), harnais [`benchmark-windows.py`](../../scripts/benchmark-windows.py), référence moteur [`docs/syne/PERFORMANCE.md`](../syne/PERFORMANCE.md).
- [Réalignement SYNE 0.17.0](realignment/SYNE-REALIGNMENT-0.17.0.md), [validation expérimentale](realignment/EXPERIMENTAL-VALIDATION-2026-10.md), [acceptation ECHOS](realignment/ECHOS-ACCEPTANCE-2026-10.md), [acceptation PRISM](realignment/PRISM-ACCEPTANCE-2026-10.md), [acceptation Launcher](realignment/LAUNCHER-ACCEPTANCE-2026-10.md).
