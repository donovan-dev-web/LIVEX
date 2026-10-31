# LIVEX — état du projet

**Composant** : LIVEX (général) · **Statut** : [RELEASE 1.0.0] · **Dernière mise à jour** : 31 octobre 2026
**Dépend de** : [`ROADMAP.md`](ROADMAP.md), [`VERSIONING.md`](../../VERSIONING.md), manifestes, contrats et tests des composants

> État documentaire du checkout au 31/10/2026. La release publique `livex-v1.0.0` est publiée et taguée. Les suites des composants ont été exécutées et validées pour la release ; ce relevé en consolide les compteurs documentés. Un test vert valide le comportement couvert, pas toute la pile ni une conclusion scientifique générale.

| Référence | Valeur |
| :-- | :-- |
| État de la release | LIVEX `1.0.0`, tag public `livex-v1.0.0`; paquets installateur Windows et Linux |
| PRISM | PRISM packagé Windows/Linux; PRISM-LDK / `PrismLdk` compilé et packagé |
| Validation de tests pour la release | 1 566 cas comptabilisés sur l'ensemble des composants; 1 550 réussis (dont 5 `SuccessWithWarnings` Unreal), 16 ignorés, 0 échec |
| Tokei | 13.0.0; 1 152 fichiers, 111 608 lignes de code, 58 105 commentaires, 31 086 lignes blanches (200 799 lignes classées; checkout visible, exclusions décrites au §5) |
| Branche et HEAD | `main` / tag `livex-v1.0.0` — `196ad077a7a91722943eb0c62010068160fd3129` |
| Versions majeures déclarées | Global LIVEX `1.0.0`; SYNE `0.17.0`; ECHOS `0.1.0`; Launcher `0.1.0`; `syne-mock` `0.1.0`; PRISM et PRISM-LDK `1.0.0` |
| Contrats / schémas | Observabilité `0.5.0`; `world_initialized` `1.1`; snapshot SYNE `schemaVersion 5`; ECHOS `SCHEMA_VERSION = 6` |
| Tests de la release | 1 566 cas exécutés : 1 550 réussis (dont 5 `SuccessWithWarnings` Unreal), 16 ignorés, 0 échec |
| Release | Publique, taguée : `livex-v1.0.0` |

## Sommaire

1. [Synthèse](#1-synthèse)
2. [Périmètre et méthode](#2-périmètre-et-méthode)
3. [Versions et contrats](#3-versions-et-contrats)
4. [Inventaire des tests](#4-inventaire-des-tests)
5. [Inventaire du code, des fichiers et des langages](#5-inventaire-du-code-des-fichiers-et-des-langages)
6. [Détail de validation des composants](#6-détail-de-validation-des-composants)
7. [État fonctionnel par composant](#7-état-fonctionnel-par-composant)
8. [Limites et portes non franchies](#8-limites-et-portes-non-franchies)
9. [Sources de référence](#9-sources-de-référence)

## 1. Synthèse

LIVEX réunit le moteur déterministe SYNE, l’observatoire headless ECHOS, le projet Unreal PRISM et le plugin PRISM-LDK, le Launcher d’orchestration, l’installateur de distribution et l’émulateur de contrats `syne-mock`. SYNE reste l’autorité de l’état simulé; ECHOS observe et analyse; PRISM consomme les contrats pour le rendu; le Launcher orchestre et archive; l’installateur déploie et maintient les composants. Le mock aide au développement, mais ne constitue pas un moteur scientifique équivalent.

Les versions principales sont LIVEX `1.0.0` (release publique `livex-v1.0.0`), SYNE `0.17.0`, ECHOS `0.1.0`, Launcher `0.1.0`, `syne-mock` `0.1.0`, PRISM `1.0.0` et PRISM-LDK `1.0.0`. Le contrat d’observabilité est `0.5.0`, la description `world_initialized` `1.1` et le snapshot `schemaVersion 5`. Le schéma ECHOS demande une réserve : `SCHEMA_VERSION` vaut encore `"6"`, alors que la migration BL-09 et ses tests nomment v7 la reconstruction de `decision_traces`.

Les tests de SYNE, ECHOS, `syne-mock`, Launcher, PRISM/PRISM-LDK et de l'installateur ont tous été exécutés et validés pour la release publique. Le décompte consolidé est **1 566 cas : 1 550 réussis, 16 ignorés et 0 échec**. Les 5 `SuccessWithWarnings` Unreal sont inclus dans les réussites. La ventilation conserve les compteurs propres à chaque runner, car xUnit, pytest, Node et Unreal Automation n'emploient pas tous la même terminologie.

Les tests et campagnes ne prouvent pas l’émergence générale. Les résultats expérimentaux pérennes rapportent un contraste préenregistré de quête spatiale confirmé, un cycle homéostatique social non confirmé et un verdict général « émergence non établie ». La release publique existe ; les limites d'acceptation des composants restent celles précisées ci-dessous.

## 2. Périmètre et méthode

- Ce relevé décrit le checkout au tag public `livex-v1.0.0` (`196ad077a7a91722943eb0c62010068160fd3129`), observé le 31/10/2026.
- Les versions sont relevées dans le code et les manifestes. Les résultats des suites exécutées pour la release sont consolidés par composant ci-dessous.
- Le relevé Tokei (§5) a été recalculé le 31/10/2026 sur les fichiers visibles après les exclusions listées. Les résultats de tests consignés sont ceux des suites exécutées et validées pour produire la release.
- Un test vert valide le comportement couvert; il ne certifie ni toute la compatibilité inter-composants ni une conclusion scientifique générale.

## 3. Versions et contrats

| Composant | Valeurs déclarées vérifiées | État résumé |
| :-- | :-- | :-- |
| SYNE | Moteur `0.17.0`; observabilité `0.5.0`; description de monde `1.1`; snapshot `schemaVersion 5` | Moteur .NET autoritaire, contrôle, flux et persistance. |
| ECHOS | Paquet `0.1.0`; métriques `2.0.0`; SQLite `SCHEMA_VERSION = "6"` | Service headless d’ingestion et d’analyse. Migration nullable BL-09 est nommée v7 dans les commentaires/tests, sans incrément du compteur global. |
| Launcher | `0.1.0`; paquet `.livexp` versionné séparément | Application Avalonia d’orchestration, présentation et archivage. |
| `syne-mock` | `0.1.0`; contrats SYNE `0.5.0` / monde `1.1` | Émulateur de contrats pour tests clients, pas une simulation équivalente. |
| PRISM / PRISM-LDK | `1.0.0`; consommateur observabilité `0.5.0` / monde `1.1` | PRISM packagé Windows/Linux; plugin Unreal `PrismLdk` réalisé et packagé. |
| Installateur | `1.0.0` (V1) | Implémenté et inclus dans les distributions publiques Windows/Linux. |

Le code déclare `SCHEMA_VERSION = "6"` dans [`sqlite.py`](../../echos/echos/storage/sqlite.py); une migration y reconstruit `decision_traces` pour rendre `utility` nullable et ajoute `held_utility`, `decision_state` et `decision_tick`. Les tests appellent cela « v7 », mais la valeur écrite dans `_meta.schema_version` est toujours celle de la constante `6`. Les mentions « schéma v7 » doivent donc être qualifiées de révision de migration BL-09 jusqu’à harmonisation du versionnage.

## 4. Inventaire des tests

### Résultats de validation de la release

Le bilan consolidé des suites exécutées pour LIVEX 1.0.0 compte **1 566 cas : 1 550 réussis, 16 ignorés et 0 échec**. Il comprend les suites de tous les composants, y compris PRISM/PRISM-LDK et l'installateur. Les avertissements Unreal `SuccessWithWarnings` restent des réussites avec avertissement.

| Composant | Type de suite | Résultat validé pour la release | Interprétation |
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
| PRISM / PRISM-LDK | Automation Specs Unreal | 76 specs : 71 `succeeded`, 5 `SuccessWithWarnings`, 0 `failed` | Validation UE 5.8 consignée dans [`docs/prism/TESTING.md`](../prism/TESTING.md). |
| Installateur | Tests xUnit `Setup.Tests.*` | 118 cas : Unit 72, App 34, Integration 6, EndToEnd 6; tous réussis | Suites Unit, App, Integration et EndToEnd exécutées pour la release. |

**Bilan de validation de la release : 1 566 cas = 1 550 réussis + 16 ignorés + 0 échec.** Ce total comprend SYNE (695), ECHOS (397), `syne-mock` (53), Launcher (227), PRISM/PRISM-LDK (76) et installateur (118). Les totaux de sous-suites suivent les sorties des runners respectifs.

> Les chiffres suivent les compteurs des runners, et non les fichiers de test. Les tests d’intégration ECHOS sont inclus dans la suite pytest et ne sont pas additionnés une seconde fois. Le résultat Launcher E2E de référence est celui obtenu avec SYNE publié (33/33, exit 0).

### Ventilation par niveau

| Niveau | Suites incluses | Cas documentés | Résultat / réserve |
| :-- | :-- | --: | :-- |
| Unitaire (suite dédiée) | SYNE Core + Launcher Unit | **778** (604 + 174) | Catégorie fiable à partir des projets de tests dédiés. |
| Intégration (suite dédiée) | SYNE Console + Launcher Integration | **111** (91 + 20) | Catégorie fiable à partir des projets de tests dédiés. |
| End-to-end | Launcher E2E réel | **33** (33 réussis, 0 échoué) | J3 sur SYNE publié + ECHOS réel : vert. |
| Types mêlés / non ventilés dans leur runner | ECHOS + `syne-mock` | **450** (397 + 53) | pytest et Node.js réunissent tests unitaires, API, stockage et intégration; le statut disponible ne permet pas d’attribuer honnêtement chaque cas à un seul niveau. Leurs 16 skips sont déjà compris dans 450. |
| Unreal Automation | PRISM / PRISM-LDK | 76 (71 réussis, 5 `SuccessWithWarnings`) | Inclus dans le bilan de release. |
| Installateur | Unit + App + Integration + EndToEnd | **118** | Quatre projets xUnit, exécutés et validés pour la release. |

La ventilation complète totalise **1 566 cas exécutés** : 1 372 pour SYNE, ECHOS, `syne-mock` et Launcher; 76 pour Unreal Automation; 118 pour l'installateur. Elle comprend 16 ignorés et aucun échec.

## 5. Inventaire du code, des fichiers et des langages

### Périmètre et règles de comptage

Relevé du checkout au **31/10/2026**, avec **Tokei 13.0.0**, depuis la racine :

```powershell
tokei . --exclude .git --exclude node_modules --exclude bin --exclude obj `
  --exclude .venv --exclude .pytest_cache --exclude __pycache__ `
  --exclude dist --exclude .astro --exclude coverage
```

Tokei a reconnu **1 152 fichiers** : **111 608 lignes de code**, **58 105 commentaires** et **31 086 lignes blanches**, soit 200 799 lignes classées. Les LOC sont les lignes classées comme code par Tokei, pas un nombre de lignes physiques de source. L'inventaire comprend les formats de documentation, configuration, assets, exemples intégrés et fichiers générés encore visibles hors des répertoires exclus. Il compte aussi les deux copies du plugin PrismLdk (`prism/LDK/` et `prism/LDK_Package/`). Ce total n'est donc ni le nombre de fichiers suivis par Git ni une mesure de sources produit uniquement.

### Fichiers de code et tests par composant

La ventilation suivante agrège les langages de programmation, d'interface et de script reconnus par Tokei. Les fichiers de test sont identifiés par chemin ou nom (`tests/`, `test_`, `*Tests.cs`, `*Spec.cpp`, etc.) ; cette classification ne dénombre pas les cas exécutés.

| Composant | Fichiers source | LOC source | Fichiers de test | LOC tests |
| :-- | --: | --: | --: | --: |
| SYNE | 143 | 12 937 | 90 | 12 343 |
| ECHOS | 43 | 8 660 | 33 | 6 514 |
| Launcher | 79 | 16 041 | 32 | 6 291 |
| Installateur | 96 | 6 219 | 12 | 1 518 |
| PRISM / PrismLdk | 39 | 9 617 | 16 | 3 156 |
| `syne-mock` | 15 | 1 778 | 5 | 1 335 |
| Site | 100 | 10 346 | 0 | 0 |
| Scripts partagés | 13 | 3 080 | 1 | 16 |

Les nombres PRISM comptent les deux arborescences présentes localement, y compris le doublon du plugin empaqueté. Les catégories et périmètres sont ceux du checkout décrit ci-dessus ; les résultats de test exécutés restent ceux de la section 4.

## 6. Détail de validation des composants

| Composant / suite | Résultat validé pour la release | Notes |
| :-- | :-- | :-- |
| SYNE Core | 604 réussis, 0 échec | Release; inclus dans `Syne.sln`. |
| SYNE Console | 91 réussis, 0 échec | Release; 695/695 au total SYNE. |
| ECHOS | 383 réussis, 14 ignorés, 0 échec; 91,32 % | pytest-cov, Python 3.12.10; avertissement Starlette/httpx. |
| `syne-mock` | 51 réussis, 2 ignorés, 0 échec | Ignorés pour SIGTERM/SIGINT sous Windows. |
| Launcher Unit | 174 réussis, 0 échec | Release. |
| Launcher Integration | 20 réussis, 0 échec | Release. |
| Launcher EndToEnd avec SYNE publié | **33 réussis, 0 échec** | Les deux parcours J3 réels (SYNE publié + ECHOS réel) passent; exit 0, durée 4 m 23 s. Vérification effectuée avec la disposition de publication attendue par le banc. |
| PRISM / PRISM-LDK | 76 specs; 71 réussies, 5 `SuccessWithWarnings`, 0 échec | Unreal Engine 5.8; inclus au bilan de la release. |
| Installateur | 118 réussis, 0 échec | Unit 72, App 34, Integration 6, EndToEnd 6; suites exécutées pour la release. |

Le total consolidé comprend 1 550 réussites, dont 5 réussites avec avertissement Unreal, 16 skips ECHOS/`syne-mock` et 0 échec.

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

Application Avalonia `0.1.0`. Unit et Integration passent 174/174 et 20/20. EndToEnd passe **33/33 avec SYNE publié** (durée 4 m 23 s, exit 0) : les deux parcours J3 réels passent après la correction de la régression d’ingestion ECHOS. L’acceptation complète J3 est verte.

### `syne-mock`

Serveur Node.js `0.1.0`, outil d’intégration qui émule des contrats. La suite locale obtient 51 réussis et 2 ignorés sous Windows pour les signaux d’arrêt. Son succès ne démontre ni une équivalence algorithmique ni des résultats scientifiques comparables à SYNE.

### PRISM / PRISM-LDK et installateur

PRISM et PRISM-LDK `1.0.0` sont réalisés et packagés pour la release ; PRISM est disponible en paquets Windows/Linux. `LDK.uproject` est l'hôte Unreal technique du plugin, et non l'expérience PRISM finale. Les 76 Automation Specs UE 5.8 ont été exécutées et validées : 71 réussies, 5 réussies avec avertissements, 0 échec. L'installateur est livré dans la release ; ses 118 tests xUnit (Unit 72, App 34, Integration 6, EndToEnd 6) ont été exécutés et réussis.

## 8. Limites et portes non franchies

1. **J3 sur composants réels : résolu le 19/10.** Les deux assertions qui échouaient dans le relevé du 18/10 provenaient d’une régression d’ingestion ECHOS : `append_tick_bundle` refusait de persister un `tick_bundle` dont l’utilité est nulle et les colonnes `held_utility`/`decision_state`/`decision_tick` absentes. Correction + test de régression; ECHOS 383/383, Launcher EndToEnd 33/33. La latence E2E reste sensible à la contention machine (un run chargé a déjà produit 32/33).
2. **Skip conditionnel E2E :** les tests déclarent un skip, mais xUnit/runner compte quatre cas comme échecs en absence de `LIVEX_SYNE_PUBLISHED_ROOT`.
3. **ECHOS DVI :** le profil de viabilité observable et l’endpoint existants ne doivent pas être confondus avec l’indice dynamique spécifié dans ECHOS VII; l’implémentation/calibration DVI n’est pas présente dans le code consulté.
4. **Numéro de schéma ECHOS :** code et tests/commentaires divergent sur v6/v7; ne pas publier « v7 » comme valeur courante de `SCHEMA_VERSION` sans harmoniser migrations et code.
5. **Portée scientifique :** les campagnes sont bornées à leurs graines, configurations, critères et horizons. Verdict global conservé : « émergence non établie ».
6. **PRISM, CI et release :** PRISM/PrismLdk et l'installateur sont livrés dans la release publique `livex-v1.0.0`. Les résultats de validation de toutes les suites exécutées pour la release sont consolidés aux §4 et §6. Tokei décrit les fichiers visibles dans le checkout du tag, avec les limites de périmètre du §5.
7. **Mesures de performance bornées :** le relevé [`LIVEX-PERFORMANCE-2026-10.md`](LIVEX-PERFORMANCE-2026-10.md) porte sur une machine de bureau unique (i7-8750H, 15,8 Go). Le débit SYNE dépend fortement de l’horizon (accumulation mémoire/croyances) — un débit à horizon court ne vaut pas pour un run long — et la pile SYNE+ECHOS est bornée par l’ingestion ECHOS mono-thread (~13 t/s à 100 agents, 6 t/s visés à la cadence PRISM). PRISM n’a pas été mesuré (éditeur Unreal absent).

Les roadmaps transverse et composants font foi pour les travaux à venir. Les rapports versionnés de réalignement et d’acceptation sont indexés dans [`realignment/README.md`](realignment/README.md).

## 9. Sources de référence

- [`VERSIONING.md`](../../VERSIONING.md), [`syne/component.json`](../../syne/component.json), [`ObservabilityContract.cs`](../../syne/Simulation.Core/Observability/ObservabilityContract.cs), [`SimulationSnapshotCodec.cs`](../../syne/Simulation.Core/Persistence/SimulationSnapshotCodec.cs).
- [`echos/pyproject.toml`](../../echos/pyproject.toml), [`sqlite.py`](../../echos/echos/storage/sqlite.py), tests de migration `echos/echos/tests/test_sqlite_store.py`.
- [`Directory.Build.props`](../../launcher/Directory.Build.props), suites `launcher/Launcher.Tests.Unit`, `Launcher.Tests.Integration` et `Launcher.Tests.EndToEnd`; inventaire du code recalculé avec Tokei 13.0.0 (commande §5).
- [Tests PRISM](../prism/TESTING.md), [roadmap transverse](ROADMAP.md), [roadmap ECHOS](../echos/ROADMAP.md), [roadmap Launcher](../launcher/ROADMAP.md).
- [Rapport de performance et de tests](LIVEX-PERFORMANCE-2026-10.md), harnais [`benchmark-windows.py`](../../scripts/benchmark-windows.py), référence moteur [`docs/syne/PERFORMANCE.md`](../syne/PERFORMANCE.md).
- [Réalignement SYNE 0.17.0](realignment/SYNE-REALIGNMENT-0.17.0.md), [validation expérimentale](realignment/EXPERIMENTAL-VALIDATION-2026-10.md), [acceptation ECHOS](realignment/ECHOS-ACCEPTANCE-2026-10.md), [acceptation PRISM](realignment/PRISM-ACCEPTANCE-2026-10.md), [acceptation Launcher](realignment/LAUNCHER-ACCEPTANCE-2026-10.md).
