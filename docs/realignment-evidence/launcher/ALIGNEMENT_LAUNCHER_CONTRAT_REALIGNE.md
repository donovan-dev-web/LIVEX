# Alignement du Launcher sur le contrat réaligné (SYNE → ECHOS → Launcher)

*Preuves pour `ANALYSE_DIVERGENCES_FONDAMENTALES_ET_PLAN_REALIGNEMENT.md` §8 (ligne
Launcher) et pour la spec `docs/launcher/USER_INTERFACE.md` §9.2.*

## Objet

Le réalignement du moteur (localisation des gisements, croyances, fenêtre causale,
traces décisionnelles complètes, re-calibration) change **ce qu'ECHOS publie**.
Le Launcher ne calcule rien : tout écart entre ce qu'il consomme et ce qui est
publié se traduit soit par une **donnée manquante**, soit — pire — par une
**donnée fabriquée**. Cette campagne vérifie la consommation réelle de la fenêtre
d'analyse (graphique relationnel, rendu 2D du monde et déplacement des entités,
fiches, rapport) et corrige ce qui diverge, sans jamais déplacer une mesure dans
le Launcher (ADR-003).

## Méthode

1. **Confrontation de contrat**, endpoint par endpoint, entre ce que la fenêtre
   d'analyse consomme (`Launcher.Application/Ports.cs`,
   `Launcher.App/Composition/EchosTelemetryService.cs`) et ce que le moteur et
   ECHOS publient réellement (`ObservabilitySerializer`, `api/routes.py`,
   `storage/pipeline.py`).
2. **Rejeu réel** : campagne `EXP-A-RUN-0001` (20 ticks, profil de référence)
   lancée par le Launcher, ingérée par ECHOS, puis quatre captures d'écran
   *headless* de la fenêtre d'analyse (`launcher/tools/UiScreenshot`, aucun
   serveur web, aucun navigateur).
3. **Suites** : unitaires, intégration et bout-en-bout sur le **vrai** SYNE
   publié, en Release.

## Écarts constatés et corrections

| # | Écart | Symptôme observable | Correction |
| :-- | :-- | :-- | :-- |
| 1 | **Réserves du tick supposées localisées** — `EchosTickResource` portait `double X, double Y` non nullables, alors que le snapshot SYNE publie `ResourceSnapshot(Type, Quantity)` : **aucune position**. La désérialisation rendait `0` pour un champ absent. | Chaque réserve agrégée était **dessinée au coin (0,0) du monde** — un point inventé — et le cadre de la carte n'était pas étendu par elle. Exactement la faute que le plan interdit : une position publiée nulle part, présentée comme une position. | `EchosTickResource(Type, double? X, double? Y, Quantity, double? Capacity)` + `Located` (`X` et `Y` publiés). La réserve non localisée est **listée avec sa quantité** (panneau dédié) et **n'est pas projetée** ; `WorldViewControl` ne l'utilise ni pour dessiner, ni pour calculer l'étendue du cadre. |
| 2 | **Chaîne causale non consommée** — ECHOS publie `GET /api/runs/{id}/causal-chains/{agent}` (Action → Intention → Objectif → Besoin → Croyance → Mémoire → Perception, boucles, troncature) ; la fenêtre n'avait aucun écran pour la lire et n'appelait pas la route. La trace décisionnelle complète du réalignement restait donc invisible dans le Launcher. | Aucun moyen de répondre « pourquoi cette entité a-t-elle fait ceci, et à partir de quoi ? » depuis le Launcher, alors que la réponse est publiée. | Nouvel appel `ReadCausalChainAsync` (`tick` optionnel, `depth` borné 1–7 côté écran, plafond ECHOS 12 respecté) et sous-écran **Chaîne causale** : une carte par couche (accent de couleur par couche, numéro d'étape, tick), bandeau **« boucle signalée par ECHOS »** avec les ticks de récurrence publiés, mention de troncature, états vides explicites. Aucun nœud n'est complété, réordonné ni déduit. |
| 3 | **Communauté absente de la liste des entités** — le groupement est publié pour chaque entité (`agents[].group`), la liste n'affichait que l'identifiant et l'action. | Impossible de relier la liste des entités au graphique relationnel : un nœud du graphe ne se retrouvait pas dans la liste. | Liste des entités étiquetée « communauté N · action » dans les écrans **Entités** et **Chaîne causale** (une seule liste, deux écrans : la sélection reste partagée). Le mot est **communauté**, pas « groupe » : la valeur publiée est le libellé de communauté d'ECHOS (plus petit membre, stable), et non l'identité d'un groupe natif SYNE — les deux notions sont explicitement distinguées par ECHOS (`group_dynamics.py` : « ce ne sont pas des groupes déclarés par SYNE »). |
| 4 | **Étiquette de communauté écrasée** — la taille d'un nœud était posée à `radius + 4`, soit **sur le point du membre** situé sous le nœud. | Le libellé « 5 m. » se superposait à des points du graphe des communautés ; la valeur « m. » était en outre ambiguë (membres vs mètres). | Taille posée sous l'anneau des membres (`linkRadius + 10`) et libellé explicite « 5 membres » (singulier géré). |

## État de l'alignement après correction

| Écran | Source (ECHOS) | Alignement vérifié |
| :-- | :-- | :-- |
| Viabilité, Comportements, Statistiques, Comparer | `viability`, `metrics`, `experiments/summary`, `metrics/catalog` | inchangé — les valeurs publiées (dont `measured`, `sampledDetails.agentContextEvery` et les ticks manquants du réalignement) sont affichées telles quelles |
| **Monde observé** | `GET /api/world` (description `world_initialized` + `agents`/`resources` du tick) | **corrigé** : gisements localisés de la description (losanges verts), entités colorées par communauté, réserves du tick dans un panneau à droite — quantité et capacité publiées, provenance de position explicite (« position publiée » / « agrégée par type »), aucune réserve non localisée sur la carte, cadre non étendu par elles |
| Relations et groupes | `GET /api/trust-graph`, `GET /api/groups` | **corrigé au rendu** (étiquette de taille) ; la confiance et les communautés restent celles publiées, aucun seuil ni agrégat calculé localement |
| Entités | `beliefs`, `relationships`, `decisions` | **complété** : la communauté publiée apparaît dans la liste ; âge de l'observation (cadence de contexte) inchangé |
| **Chaîne causale** | `GET /api/runs/{id}/causal-chains/{agent}` | **nouveau** : profondeur demandée/servie, boucles, troncature, états vides |
| Rapports (écran principal) | rapport d'émergence produit par ECHOS dans le paquet | inchangé et vérifié : le Markdown du rapport est **rendu tel quel** (statut affiché : « rapport produit par ECHOS, affiché tel quel »), avec l'état explicite « non encore produit » quand il manque — la mise en page du rapport reste donc celle d'ECHOS, seul son rendu appartient au Launcher |
| Relecture | curseur de tick, direct/relire | inchangé ; la chaîne causale et le monde suivent le tick choisi |

### Écart trouvé puis corrigé : BL-09/B-10 défait à l'ingestion ECHOS

Point de contrôle : le plan interdit qu'une **absence de décision** se confonde
avec un **zéro observé** (§7 S-28 / D-03 / B-10, item BL-09 « Réalisé + validé » :
`DecisionRecord.Utility` nullable, `DecisionState`, `heldUtility`).

- **Source (SYNE)** : correct. `ExternalEvent.DecisionMade` publie
  `utility: null` + `decisionTick: null` pour une **absence**, `utility: null` +
  `heldUtility: <score>` + `decisionTick: <tick d'origine>` pour un **report**,
  et un nombre seulement pour une délibération `New`/`Interrupted`.
- **Ingestion (ECHOS)** : **défait**.
  `echos/instrumentation/decision_traces.py` fait
  `"utility": float(value.get("utility", 0.0) or 0.0)` — un `null` devient
  `0.0` — et **ne reprend ni `heldUtility` ni `decisionState`** ; la colonne est
  `utility REAL NOT NULL` (`storage/sqlite.py`).
  Vérification exécutée (script archivé) :

  ```
  ABSENCE -> {'chosen_action': 'Rest', 'utility': 0.0, 'deliberated': False}
  REPORT  -> {'chosen_action': 'Rest', 'utility': 0.0, 'deliberated': False}
  champs publiés conservés : [agent_id, beliefs_count, cause, chosen_action,
                              deliberated, goals_count, interrupted,
                              memory_count, needs, run_id, tick, utility]
  ```

  `evidence/verif-trace-decision-echos.txt` (script :
  `evidence/verif-trace-decision-echos.py`).
- **Présentation (Launcher)** : `EchosDecision.Utility` est un `double` non
  nullable et `ReadDouble` rend `0` pour tout champ absent ou `null` : la fiche
  d'entité affiche donc « 0 » comme une mesure.

Conséquence : sur le run de référence, où **89 % des ticks sont des reports**
(222 821 `holdover` pour 24 983 décisions `fresh`, phase 6), la fiche d'entité
affichait « 0 » là où le contrat dit « aucune décision ce tick, score reporté
0,71 ». C'est la confusion que BL-09 a corrigée à la source, réintroduite en
aval : l'écart était **côté ECHOS** (schéma + ingestion) et **côté Launcher**
(affichage d'un manquant comme un nombre).

**Correction (18/10) — les deux côtés :**

- **ECHOS, schéma SQLite v7** : `decision_traces.utility` devient **nullable** et
  trois colonnes sont ajoutées — `held_utility`, `decision_state`,
  `decision_tick`. SQLite ne sachant pas retirer un `NOT NULL`, la migration
  **reconstruit** la table (renommage, création au schéma courant, copie,
  suppression) : les lignes existantes sont conservées telles quelles, avec
  `decision_state` à `NULL` (« non renseigné ») — aucune valeur passée n'est
  réécrite ni reconstruite. La définition du schéma est extraite en une source
  unique (`_DECISION_TRACES_DDL`) partagée par la création et la migration.
- **ECHOS, ingestion** : `build_decision_trace` conserve l'absence (`utility`
  `None`) et publie `held_utility`, `decision_state` et `decision_tick` — plus de
  `float(... or 0.0)`. La couche « Action » de la chaîne causale expose les mêmes
  champs : `decisionState`, `utility` (nullable), `heldUtility`, `decisionTick`.
- **Launcher** : `EchosDecision.Utility` devient `double?` (lecture nullable, plus
  de `ReadDouble` par défaut à 0), la fiche d'entité affiche « **non mesurée** »
  en gris pour une absence ou un report, une colonne **ÉTAT** (délibérée /
  reportée / interrompue / absente, « état non renseigné » pour une base
  antérieure) et le score reporté sous son propre nom
  (« reportée · 0.41 (décision du tick 14) »). Un zéro **publié** reste un zéro
  mesuré.

Preuves de la correction :

| Preuve | Commande | Résultat |
| :-- | :-- | :-- |
| Suite ECHOS complète | `.venv/Scripts/python.exe -m pytest -q` (depuis `echos/`) | **376 tests verts, 0 échec, 14 sautés, exit 0**, couverture 91 % — `evidence/echos-pytest-v7-utility-nullable.txt` |
| Migration v7 sur une base v6 fabriquée | `evidence/verif-migration-v7-echos.py` | ligne historique conservée (`utility 0.0`, état `null`), absence → `utility null` + `none`, report → `held_utility 0.71` + `holdover` + `decision_tick 3`, délibération → `0.83` + `fresh` — `evidence/verif-migration-v7-echos.txt` (exit 0) |
| Tests ECHOS dédiés | `test_instrumentation.py`, `test_sqlite_store.py` | absence ≠ zéro, report sous `held_utility`, migration d'une base pré-v7 |
| Launcher | `dotnet test launcher/Launcher.Tests.Unit -c Release` | **172 tests verts, exit 0**, dont le test « utilité non publiée → non mesurée, jamais zéro » |
| Rendu final | `dotnet run --project launcher/tools/UiScreenshot … agents --analysis` | `evidence/agents.png` : colonnes TICK · ACTION · UTILITÉ · ÉTAT · CAUSE, « non mesurée », « absente », « reportée · 0.41 (décision du tick 14) » |

### Limite structurelle déclarée : les groupes natifs SYNE ont rejoint le Launcher

> **Statut (18/10) — écart fermé.** La limite décrite ci-dessous était réelle au
> moment de l'alignement ; elle a été comblée par la route ECHOS
> `GET /api/runs/{id}/native-groups` et le panneau **Groupes natifs SYNE** du
> sous-écran « Relations et groupes ». Le texte d'origine est conservé ci-dessous
> comme trace de l'état constaté.

Depuis l'arbitrage A0-3, SYNE publie de vrais groupes (`groupId`, `leaderId`,
`bornTick`, `cohesion`, `decision`, `consensus`, `maxBondDistance`,
`meanBondDistance`). **Aucun point d'API ECHOS ne les servait** :

- `/api/groups` et le champ `groups` de `/api/world` renvoient les **communautés
  inférées** d'ECHOS (propagation d'étiquettes sur le graphe de confiance,
  `label` = plus petit membre, `members`, `size`) — documenté comme tel des deux
  côtés ;
- la dynamique de groupe passe par les **métriques agrégées**
  (`GroupDynamicsMetrics` : `InferredCommunities`, `AverageCommunitySize`,
  `CommunityCoverage`, `AverageGroupLifetime`, `GroupFormationRate`,
  `GroupDissolutionRate`, `DissolvedGroupSuccessShare`,
  `MemberExitsPerDissolution`) que l'écran **Comportements** découvre
  génériquement, et par les **événements natifs** (`group_formed` /
  `group_dissolved`) ;
- ces événements sont affichés par type, tick, entité, action et cause : leur
  charge publiée (`cohesion`, `consensus`, `maxBondDistance`) n'est pas rendue.

Conséquence à connaître : « la cohésion du groupe 3 après A0-3 » est **publiée
par le moteur mais pas consultable dans le Launcher**, faute d'une route ECHOS —
ce n'est pas un défaut du Launcher, qui ne peut pas inventer un point d'accès.
Fermer l'écart demande soit une route ECHOS (état des groupes natifs), soit
l'exposition de la charge des événements de groupe.

**Ticket ouvert (18/10)** : *ECHOS : publier l'état des groupes natifs SYNE
(cohésion, consensus, chef, distances)* — id `fb5663d9-fc3f-4761-a707-c2fe07e1b1b2`,
relié à la tâche d'alignement du Launcher. Il porte la proposition de route, les
contraintes de contrat (aucune valeur recalculée, `null` pour un champ non
publié, ne jamais fusionner groupe natif et communauté inférée) et les critères
d'acceptation.

## Preuves

| Preuve | Commande | Résultat |
| :-- | :-- | :-- |
| Unitaires | `dotnet test launcher/Launcher.Tests.Unit -c Release` | **172 réussis, 0 échec**, dont 4 tests dédiés : réserves du tick sans position listées et jamais projetées à l'origine, chaîne causale servie (couches + boucle), chaîne absente affichée sans remplissage, utilité non publiée affichée « non mesurée » (jamais 0) — `evidence/launcher-tests-unit-release.txt` |
| Intégration | `dotnet test launcher/Launcher.Tests.Integration -c Release` | **20 réussis, 0 échec** — `evidence/launcher-tests-integration-release.txt` |
| Bout-en-bout (SYNE réel publié) | `LIVEX_SYNE_PUBLISHED_ROOT=D:/LIVEX/syne dotnet test launcher/Launcher.Tests.EndToEnd -c Release` | **33 réussis, 0 échec** — `evidence/launcher-tests-endtoend-real-syne.txt`. Repassé **après** la correction v7 (schéma ECHOS modifié + Launcher modifié) : le flux real SYNE → ECHOS → paquet est vérifié sur le code final |
| Captures headless | `dotnet run --project launcher/tools/UiScreenshot` | `evidence/monde.png`, `evidence/relations.png`, `evidence/causalite.png`, `evidence/agents.png` |
| Suite ECHOS (schéma v7) | `.venv/Scripts/python.exe -m pytest -q` depuis `echos/` | **376 verts, 0 échec, exit 0**, couverture 91 % — `evidence/echos-pytest-v7-utility-nullable.txt` |
| Parité de contrat SYNE réel ↔ syne-mock | sonde Node archivée `evidence/contrat-syne-vs-syne-mock.js` (à exécuter depuis `syne-mock/`, où `ws` est installé) | **clés de réserve identiques** (`quantity`, `type`, **aucune position**), versions identiques (`world` 1.1, `snapshot` 0.5.0, moteur 0.17.0) et mêmes clés d'entité des deux côtés — `evidence/contrat-syne-vs-syne-mock.json`. Aucun des deux moteurs ne publie de position de réserve au tick : le point au coin (0,0) était donc **entièrement fabriqué**, quel que soit le moteur |

Les captures montrent, sur la campagne réelle (`EXP-A-RUN-0001`, monde
250 × 250, tick 20, 6 entités) :

- **Monde observé** — la carte avec les gisements localisés (losanges verts) et
  les entités, l'état « 4 réserve(s) publiée(s) · 0 localisée(s) », et le panneau
  listant `food`, `water`, `wood`, `mineral` avec quantité et capacité, chacune
  marquée **« agrégée par type »** : **aucune n'est projetée sur la carte**, donc
  plus aucun point au coin (0,0) ;
- **Relations et groupes** — communautés dont la taille est posée sous l'anneau
  des membres (« N membres »), sans recouvrir un point ;
- **Chaîne causale** — 7 couches servies (Action → Perception) pour l'entité 1 au
  tick 19, avec le bandeau **« boucle signalée par ECHOS »** et la récurrence
  publiée (`Action — Eat`, ticks 11, 15, 19) ;
- **Entités** — la fiche de l'entité sélectionnée (croyances, confiance déclarée,
  dernières décisions) et liste étiquetée « communauté N · action », par exemple
  « 1 · communauté 3 · SeekFood ». Le tableau des décisions porte les colonnes
  TICK · ACTION · UTILITÉ · ÉTAT · CAUSE : `0.72` / « délibérée » pour une
  délibération, « non mesurée » / « reportée · 0.41 (décision du tick 14) » pour
  un report, « non mesurée » / « absente » pour une absence.

### Instabilité observée (déclarée, non reproduite)

Une exécution complète de la suite bout-en-bout, lancée **pendant** la
publication de SYNE et une autre suite, a rendu **32/33** (`J3RealComponents…`
en échec au deuxième run du banc, code de sortie 1 du moteur). Le banc isolé
puis la suite complète relancée au repos rendent **33/33**. Hypothèse retenue :
contention de machine (publication + deux suites concurrentes), pas une
régression du produit — aucune trace de code n'a été modifiée entre les deux.
À connaître pour l'interprétation des rapports : un run E2E lancé sous charge
peut rendre un échec que le rejeu au repos ne reproduit pas.

## Ce que la campagne n'a pas fait (et pourquoi)

- **Aucun calcul déplacé dans le Launcher** : pas de moyenne, pas d'écart, pas de
  seuil, pas de reconstruction de chaîne. Le Launcher présente, ECHOS calcule
  (ADR-003).
- **Aucun onglet supprimé** : les quatre écarts se corrigent par ajout ou par
  rendu ; retirer un écran aurait retiré une lecture publiée.
- **Portes inchangées** : le signal de saut dynamique trompeur de la suite
  bout-en-bout hors composants publiés (`PHASE5_LAUNCHER_ACCEPTANCE.md`) reste
  **non franchi** — hors périmètre de cette campagne.
