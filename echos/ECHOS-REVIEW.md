# ECHOS — revue et refactor

Périmètre : `echos/` uniquement. Aucun changement dans SYNE, syne-mock ni
Prism. Les docs existantes n'ont pas été prises comme référence normative :
lorsqu'elles contredisaient le code, le code a été corrigé et la suite de
tests a été réalignée explicitement.

État final : **283 tests passés, 2 skips, flake8 propre, couverture 93 %**
(contre 233 passés avant cette revue). Les 2 skips sont les tests d'intégration
exigeant un binaire SYNE Release ou le serveur syne-mock : **les deux ont été
exécutés et passent** (§10).

Côté interface : **61 tests passés sur 14 fichiers, ESLint propre, `tsc --noEmit`
propre, build propre** (contre 18 tests / 6 fichiers avant). Détail en §9.

---

## 1. Bugs critiques

### 1.1 `AnalyticsStore.runs()` plantait sur un run sans tick

`int(row[3])` sur un `last_tick` nul levait
`TypeError: int() argument must be a string... not 'NoneType'`. Un run
enregistré puis lu avant toute ingestion — cas normal d'une base Fraîche ou
d'un run en cours d'acquisition — rendait l'API entière inopérante (`/api/runs`
et toutes les routes qui résolvent un run par défaut).

Correction : lecture directe, `None` conservé et propagé.

### 1.2 `/api/compare` renvoyait 500 sur tout run contenant un événement

`_canonical_content` décompressait les événements avec
`dict(zip(columns, row))` alors que la requête `SELECT` en liste six colonnes
et non sept :

```
ValueError: not enough values to unpack (expected 7, got 6)
```

C'est-à-dire que la comparaison de reproductibilité — fonction centrale du
module — ne fonctionnait sur aucun run réel, puisque tout run ingéré contient
des événements. Le calcul de l'empreinte SHA-256 et donc `bit_identical` /
`is_reproducible` étaient inaccessibles.

Correction : accès par nom de colonne via deux nouvelles méthodes
`AnalyticsStore.event_records()` et `AnalyticsStore.events_by_type(...)`.
`compare()` valide aussi ses arguments hors HTTP (`ReproducibilityError`) au
lieu de lever `KeyError` sur un run absent.

---

## 2. Moteurs de métriques

Sept moteurs refondus autour d'un module d'outils partagés
`analysis/_common.py`, qui centralise la lecture tolérante des variantes de
contrat (`safe_ratio`, `agents_of`, `belief_facts`, `trust_edges`,
`community_sizes`, `events_of`, `event_values`…).

| Moteur | Bug corrigé |
|---|---|
| `CognitiveDiversityMetrics` | `DecisionDiversity` était calculée sur les **objectifs**, rendant la métrique redondante avec `GoalDiversity` et insensible aux décisions. Elle lit désormais les événements `decision_made` (`event.action`, repli `value.intention`), avec repli sur les objectifs quand le tick ne porte aucune décision. Le majoritaire de `BeliefDisagreement` était tranché par la valeur **la plus grande** ; il l'est par la plus petite. |
| `InformationPropagationMetrics` | `InformationDiffusionSpeed` renvoyait le **numéro de tick absolu**. Elle vaut désormais l'amplitude entre le premier message et le tick où 80 % de la population a émis. `MessageVolume` ne comptait que le tick courant (correct) mais `NetworkCentrality` calculait un ratio de cardinalités incohérent. |
| `SocialComplexityMetrics` | Le voisinage était construit deux fois, avec deux copies divergentes (clustering vs centralités). Une relation déclarée d'un seul côté ne comptait jamais dans le clustering : le graphe est maintenant symmetrisé avant de fermer les triangles. |
| `GoalConvergenceMetrics` | `GoalTypeCounts` dependait d'un `keyed()` local dupliqué. |
| `ResourceSustainabilityMetrics` | `_availability(100, None, 0)` renvoyait `100000000000.0` (`max(capacity, 1e-9)` comme dénominateur), contaminant toute la moyenne du moteur. Trois régimes définis désormais, bornés. `RecoveryTime` est réellement calculé à partir de l'historique quand il existe. |
| `GroupDynamicsMetrics` | Un unique événement valait `1000.0` par 1000 ticks : la fenêtre de dénominateur était calculée sur les seuls événements de groupe, donc 1 tick pour un événement isolé. Le dénominateur porte désormais sur la fenêtre d'observation réelle. `GroupObjectiveSuccessRate` ignorait `success: false` (les booléens étaient filtrés comme non-numériques). |
| `EmergenceIndicators` | `SystemComplexity` mélangeait un tick non borné et n'était pas borné du tout : il croissait linéairement avec la durée du run, rendant `SystemComplexity_Norm = 1 - x/100` négatif. La liste des moteurs entrants était hard-codée dans `compute`, ce qui la faisait diverger du registre dès qu'un moteur était ajouté. |

### 2.1 Dead metrics : 7 métriques à 0 sur tout run réel

Le snapshot ne portait que le **tick courant** et ses événements. Or :

- `FeedbackLoopDetector` (5 métriques) lit `history`
- `RecoveryTime` lit `history`
- `CommunityStability` lit `communityHistory`

Aucune de ces clés n'était jamais produite par le pipeline. Ces sept métriques
retombaient donc systématiquement sur leur repli neutre, en production comme
en test. La fixture de test les fournissait artificiellement, ce qui masquait
totalement le problème.

**Correction** : `_RollingContext` dans `storage/pipeline.py` construit et
borne trois fenêtres glissantes, transmises à chaque tick :

- `events` — fenêtre glissante, bornée en ticks puis en nombre d'événements
  (§8.2)
- `history` — 100 derniers points `{tick, actions, resources, communities}`
- `communityHistory` — tailles de communautés par tick

### 2.2 Communautés : les singletons n'étaient pas des communautés

Trois entités isolées, sans la moindre relation de confiance, devenaient
trois « communautés » et déclenchaient faussement `CommunityFormation` puis
`CollectiveCoordination`. Une confiance à soi-même suffisait également.
`communities()` et `community_sizes()` excluent désormais les singletons, et
`_groups_of` (pipeline) s'aligne sur la même définition.

Changement de comportement visible : le `label` d'un groupe est maintenant le
plus petit identifiant du groupe, et non l'indice de propagation d'étiquettes.
Ce dernier se décale dès qu'un nœud change de communauté, ce qui faisait
bouger les identifiants affichés par l'UI sans raison observable.

### 2.3 `emergence` : cycle d'import

La constante `COMPOSITE_ENGINE_NAME` est déclarée dans `_common.py` et le
registre chargé paresseusement via `load_engines()`. `analysis/__init__.py`
n'en est plus que la façade. `EmergenceIndicators` a besoin du registre pour
`compute`, et le registre a besoin de savoir quel moteur ne pas exécuter
avant le composite : les deux côtés partagent désormais une source unique.

### 2.4 Provenance : distinguer une mesure de son repli

C'est la conséquence structurelle de §2.1. Un moteur qui n'a pas sa fenêtre
renvoie `0.0` pour `RecoveryTime`, et l'interface affichait `0` sans que rien ne
distingue « mesuré à 0 » de « absent du tick » : un artefact d'acquisition
ressemblait à un résultat. Le retour neutre est **la bonne valeur** pour un
moteur sans donnée — mais il ne doit pas être présenté comme une observation.

`analysis.provenance(snapshot)` retourne, pour chaque moteur, la liste des
(métrique, disponible) calculée **depuis les mêmes entrées** que le calcul lui-même
: il n'existe pas de liste de métriques déclarée à côté du code, donc pas de
possibilité de divergence entre « ce que le moteur calcule » et « ce que l'UI
sait afficher ».

La provenance est calculée à la cadence d'analyse, persistée par tick
(`tick_metrics.measured`, schéma v5) et exposée par l'API. Le `measured=false`
signale à l'interface de distinguer une valeur de son repli (§9.6). Sur un run
réel de 3 ticks, l'E2E vérifie que les fenêtres du pipeline rendent toutes les
métriques mesurées : un `false` signalerait une fenêtre manquante, pas un
phénomène.

---

## 3. Analyse causale

- **`max_depth` n'était jamais appliqué.** Le paramètre était seulement
  validé (`> 12` rejeté) puis ignoré : `depth=12, max_depth=2` servait la
  chaîne complète de 7 couches. `available` est maintenant
  `min(cutoff, max_depth)`. `max_depth ≤ 0` et les booléens sont rejetés au
  lieu de produire une chaîne vide silencieuse.
- **`truncated` était faux.** La référence était `available`, qui inclut le
  plafond `max_depth` — donc `len(chain) < available` était faux dès que le
  plafond mordait. La bonne référence est le nombre de nœuds atteignables
  avant l'arrêt sur boucle, `min(len(nodes), cutoff)`.
- **Récurrence non scopée à l'entité.** Les 16 dernières traces du **run**
  étaient retenues *puis* filtrées sur l'entité. Sur un run où d'autres
  entités décident en dernier, la fenêtre ne contenait aucune trace de
  l'entité et la récurrence était toujours vide — donc la détection de boucle
  de rétroaction (ECHOS-062) ne fonctionnait pas en multi-entité. Le filtre
  précède désormais la troncature.
- **Lectures SQLite** : `_intention_of` et `_perceptions` rechargeaient
  l'intégralité du journal pour retrouver une ligne, avec un `import json`
  local dans une boucle. Ils utilisent `events_by_type(..., max_tick=...)`.

---

## 4. API REST

- **Découverte des séries limitée au dernier tick.** Les couples
  (moteur, métrique) étaient lus dans `latest_metrics`. Une métrique présente
  à un ancien tick et disparue depuis (aucun événement à ce tick) n'était
  plus servie, alors que sa série existait en base. Nouveau
  `AnalyticsStore.metric_names()` — `SELECT DISTINCT` sur `tick_metrics`.
- **Résolution du run par défaut.** Le tri portait sur `(last_tick, run_id)`
  via `max`, qui retient le **plus grand** identifiant à tick égal, à
  l'inverse du contrat documenté et du test. Tri par identifiant croissant
  puis `max` : `max` conserve le premier élément, donc le plus petit.
- **CORS et UI statique : non ajoutés.** `echos-ui/dist` n'est pas servi par
  FastAPI, et le projet n'a aucune politique CORS. Ajouter CORS à l'aveugle
  ouvrirait l'API d'analyse à tout site. Le proxy Vite
  (`echos-ui/vite.config.ts`) couvre déjà le développement, et c'est un
  choix de déploiement à trancher avec l'équipe, pas un correctif de bug. Voir
  §8.5.
- **Provenance dans le contrat de réponse.** `/metrics` publie désormais
  `measured` à côté de `values` et `/runs/{id}` expose le même objet, sans quoi
  l'interface ne peut pas distinguer une mesure de son repli (§2.4, §9.6).

---

## 5. Robustesse

- **Cadences nulles ou négatives.** `index % 0` levait `ZeroDivisionError` au
  premier tick, et une valeur négative ne divisait jamais : la cadence
  `sample_every` était silencieusement ignorée au lieu d'être signalée.
  `analysis_every`, `sample_every` et `parquet_flush_every` sont désormais
  validés `>= 1`, tout comme les fenêtres de `_RollingContext`.
- **Fuite de descripteurs.** `EchosLogger` n'avait aucun `close()` : chaque
  instance conservait son `FileHandler` ouvert jusqu'au ramasse-miettes. Une
  boucle de runs épuisait les descripteurs du processus. Ajout de `close()`,
  d'un context manager et d'un `__del__` prudent.
- **Reconnexion en boucle.** `dev_ingest` reconnectait toutes les 100 ms
  quand SYNE est arrêté, saturant le serveur de tentatives et la console.
  Backoff exponentiel plafonné à 30 s, remis à zéro sur connexion réussie.
- **Fin de flux par préfixe de chaîne.** `__next__` détectait la fermeture via
  `exc.detail.startswith("connexion fermée")` : toute reformulation du libellé
  d'erreur faisait disparaître la fin d'itération. Nouveau type
  `StreamClosed`, distinct d'`InvalidMessageError` (qui reste une erreur et
  remonte). Exporté depuis `echos.ingestion`.
- **Empaquetage.** `pyarrow` est importé par `storage/parquet.py` et
  `reporting.py` mais n'était déclaré que dans `requirements.txt` : un
  `pip install echos` produisait un paquet qui ne démarrait pas.

---

## 6. Parquet : réécriture O(n²)

`_flush_agent_series` relisait et réécrivait le fichier entier à chaque flush.
La série est désormais regroupée en mémoire sur `parquet_flush_every` ticks
puis écrite en une passe. Ordre toujours stable.

*Note : ce correctif était déjà présent dans le code au démarrage de cette
session ; il est mentionné pour la traçabilité du constat.*

---

## 7. Tests

### 7.1 Un angle mort majeur

`test_compare.py::_populate` construisait les traces de décision et les
métriques, mais **n'enregistrait aucun événement**. Or c'est précisément la
présence d'un événement qui faisait planter `_canonical_content`. Tous les
tests de comparaison passaient donc en vérifiant le chemin qui n'échouait
jamais. Le helper enregistre désormais les événements (`events` et
`groupEvents`).

### 7.2 Round-trip transport qui ne comparait pas la même chose

`test_transport_to_model_snake_case_is_stable` calculait `reflected` sur
`world_snapshot_u2.json` et `from_transport` sur `snapshot_analysis.json` —
deux fixtures différentes. L'égalité ne tenait que parce que les deux
retombaient sur les mêmes replis neutres. Les clés de contexte sont
maintenant réinjectées après le round-trip, ce qui rend la comparaison
significative pour les moteurs fenêtrés.

### 7.3 Régressions ajoutées par la première passe (28 tests)

Chaque bug corrigé ci-dessus a un test qui échouerait sans la correction :

| Test | Couvre |
|---|---|
| `test_runs_handles_a_run_without_tick` | §1.1 |
| `test_fingerprint_covers_events_and_decisions` | §1.2 |
| `test_compare_raises_on_unknown_run_outside_http` | §1.2 |
| `test_content_fingerprint_ignores_the_run_id` | §1.2 |
| `test_decision_diversity_counts_decisions_not_goals` | §2 |
| `test_resource_availability_is_bounded_without_capacity` | §2 |
| `test_resource_engine_stays_bounded_on_incomplete_snapshot` | §2 |
| `test_recovery_time_uses_history_when_present` | §2.1 |
| `test_single_group_event_does_not_saturate_the_rate` | §2 |
| `test_group_success_rate_reads_boolean_flags` | §2 |
| `test_no_trust_graph_isolates_every_agent` | §2.2 |
| `test_self_trust_relation_does_not_create_a_community` | §2.2 |
| `test_system_complexity_is_bounded_by_one` | §2 |
| `test_build_chain_max_depth_caps_the_served_chain` | §3 |
| `test_build_chain_full_depth_is_not_truncated` | §3 |
| `test_recurrence_is_scoped_to_the_entity` | §3 |
| `test_default_run_tie_breaks_on_smallest_id` | §4 |
| `test_default_run_prefers_the_furthest_tick` | §4 |
| `test_metrics_series_exposes_metrics_absent_from_the_last_tick` | §4 |
| `test_consume_rejects_non_positive_cadences` | §5 |
| `test_engine_context_carries_rolling_windows` | §2.1 |
| `test_rolling_context_exposes_the_three_engine_windows` | §2.1, §5 |
| `test_close_releases_the_file_handler` | §5 |
| `test_context_manager_closes_the_handler` | §5 |
| `test_stream_closed_is_its_own_exception_type` | §5 |
| `test_invalid_message_is_not_swallowed_as_end_of_stream` | §5 |
| `test_metric_names_spans_the_whole_run` | §4 |
| `test_event_records_and_events_by_type_are_deterministic` | §1.2 |

### 7.5 Régressions ajoutées par la seconde passe (25 tests)

| Test | Couvre |
|---|---|
| `test_group_members_join_agent_ids_despite_the_numeric_transport` | §8.1 |
| `test_group_members_are_accepted_in_string_form_too` | §8.1 |
| `test_u8_snapshot_preserves_seasons_territories_books_and_engine_version` | §8.1 |
| `test_u8_book_events_keep_typed_reader_and_write_payloads` | §8.1 |
| `test_event_window_is_bounded_by_ticks_then_by_events` | §8.2 |
| `test_group_rate_denominator_uses_the_observed_window` | §8.2 |
| `test_single_group_event_does_not_saturate_the_rate` | §8.2 |
| `test_provenance_marks_windowed_metrics_as_unmeasured_without_windows` | §2.4 |
| `test_provenance_is_complete_with_the_pipeline_windows` | §2.4 |
| `test_provenance_ignores_a_zero_valued_window` | §2.4 |
| `test_measured_flags_covers_every_declared_metric` | §2.4 |
| `test_consume_persists_metric_provenance` | §2.4 |
| `test_metrics_expose_provenance_alongside_values` | §2.4, §4 |
| `test_metrics_report_a_neutral_fallback_as_unmeasured` | §2.4 |
| `test_a_database_written_before_v5_is_migrated_without_losing_rows` | §2.4 |
| `test_migrated_column_rejects_a_non_boolean_measured_value` | §2.4 |
| `test_non_numeric_cadence_names_the_offending_variable` | §8.4 |
| `test_non_positive_cadence_is_rejected_before_connecting` | §8.4 |
| `test_read_config_restores_the_process_environment` | §8.4 |
| `test_empty_value_falls_back_to_the_default` | §8.4 |
| `test_environment_overrides_are_read` | §8.4 |
| `test_defaults_are_used_when_the_environment_is_empty` | §8.4 |
| `test_reconnect_delay_grows_exponentially_and_is_capped` | §5 |
| `test_real_syne_stream_is_ingested_and_served_by_echos` (provenance) | §2.4, §8.1 |
| `test_mock_stream_is_ingested_and_served_by_echos` (provenance) | §2.4, §8.1 |

La migration v5 mérite un mot : elle a d'abord été vérifiée à la main, puis
couverte par un test qui construit une **vraie base pré-v5** (DDL d'origine,
`CREATE TABLE` sans `measured`, lignes déjà écrites). Sans `_migrate_measured_column`,
ce test échoue sur `table tick_metrics has 5 columns but 6 values were
supplied` — c'est-à-dire que la base de production serait devenue inutilisable
au déploiement. Le test vérifie aussi qu'une valeur `0.0` pré-existante est relue
comme mesurée : la migration ne réécrit pas l'interprétation de l'historique.

### 7.4 Goldens : quatre valeurs changées, listées explicitement

```
InformationDiffusionSpeed : 10.0  -> 5.0
CommunityStability         :  0.0  -> 1.0
EmergenceScore             :  0.7585335893412777 -> 0.7635335893412777
SystemComplexity           :  4.5022968652028394 -> 1.0
```

Les trois autres tests qui encodaient l'ancien comportement ont été réécrits
pour affirmer la sémantique correcte, avec le motif en commentaire :
`test_information_propagation_hand_computed_values`,
`test_no_trust_graph_isolates_every_agent`,
`test_social_complexity_hand_computed_values`.

`test_j2_determinism` et `test_j3_determinism` rejouent désormais
`communityHistory` comme le pipeline, et la fixture de référence expose cette
clé pour que les deux chemins (moteur isolé et rejeu) concordent.

---

## 8. Points ouverts, arbitrés

Les sept points listés à l'issue de la première passe ont été arbitrés. Quatre
ont reçu une correction, deux restent ouverts par décision, un est traité en
§9.

### 8.1 `Group.members: list[int]` — tranché : le type était faux

SYNE sérialise `agents[].id` en chaîne mais `groups[].members` et `leaderId`
en entiers (`ulong`). Typés en `int`, ces champs ne pouvaient jamais être joints
à `Agent.id` (`"3" != 3`) : toute consommation de `groups` aurait produit des
identifiants d'une autre nature que ceux de `trust`, `beliefs` ou
`events_log.agent_id`. Le modèle est conservé, les identifiants sont normalisés
en `str` par des validateurs (`_agent_ids`, `_agent_id`), et `group_id` reste un
entier puisque c'est l'identité du groupe, jamais celle d'un agent.

La même normalisation est appliquée à `Territory.members`, `Book.author_id` et
`Book.readers`, pour ne pas laisser trois modèles voisins diverger.

Vérifié contre le vrai transport SYNE dans l'E2E (§10) : tous les membres de
groupe sont des chaînes et appartiennent à l'ensemble des ids d'agents du tick.

### 8.2 Fenêtre d'événements de 1000 — tranché : c'était le mauvais criteria

Borne en **nombre d'événements** seulement, la durée couverte dépendait de
l'activité : sur un tick chargé elle valait ~20 ticks, sur un run calme 1000
ticks. Deux runs contenant le même nombre d'événements donnaient donc des taux
de formation de groupe non comparables : à 2000 événements, le dénominateur
passait de 1000 ticks (run calme) à 20 (run chargé), soit un facteur 50 sur la
même métrique et le même contenu.

La fenêtre est désormais bornée en **ticks** (`_EVENT_WINDOW_TICKS`, aligné sur
la longueur de l'historique, qui a déjà une source contractuelle), avec un
plafond mémoire en nombre d'événements (`_EVENT_WINDOW_MAX_EVENTS = 2000`). Le
pipeline publie `eventWindow: {ticks, from, to}` — la durée **réellement
observée**, bornée par la longueur du run, indépendante de l'activité. Un run
de 3 ticks annonce 3, pas 100.

`GroupDynamics` consomme ce dénominateur en trois niveaux : `eventWindow.ticks`
si le pipeline l'a publié, sinon l'étendue des ticks présents dans `events`
(rejeu, tests), sinon `1`. Le repli intermédiaire est celui qui rendait le taux
dépendant de la charge ; il reste donc uniquement pour les contextes construits
hors pipeline, où aucune fenêtre observée n'est disponible.

### 8.3 Provenance des métriques — tranché : `measured` persisté et publié

Voir §2.4. Colonne `measured` en schéma v5 (migration additive, couverte par un
test sur une vraie base pré-v5 — §7.5), exposée par `/api/runs/{id}/metrics` et
`/api/runs/{id}`, consommée par l'interface (§9.6).

### 8.4 `process.env` de `dev_ingest` — tranché : validé avant connexion

`ECHOS_ANALYSIS_EVERY=abc` échouait sur un `int()` qui ne nommait pas la
variable, et `ECHOS_ANALYSIS_EVERY=0` était accepté au démarrage puis rejeté
par `consume` — après l'ouverture de la base et la connexion au flux. La
configuration est désormais lue et validée **avant** toute I/O
(`read_config`, `ConfigurationError`), avec sortie 2 distincte de la sortie 1
d'échec d'ingestion, et message nommant la variable fautive.

### 8.5 Politique CORS — laissé tel quel, décision assumée

Aucun middleware CORS n'est installé, et c'est le bon choix ici : en
développement le proxy Vite relaie `/api` vers `:5000` (même origine pour le
navigateur), et aucun déploiement de l'interface n'est prévu. La question n'est
donc pas « quelle politique CORS » mais « quel déploiement ». Point laissé
ouvert.

### 8.6 `StarletteDeprecationWarning` — laissé tel quel

`httpx` avec `starlette.testclient` est déprécié au profit de `httpx2`. Sans
impact fonctionnel aujourd'hui ; à traiter lors de la prochaine montée de
FastAPI, pas dans le cadre de cette revue.

### 8.7 Normalisation `/ 100` de `InformationDiffusionSpeed` — laissé ouvert

Le seuil de 100 ticks comme horizon est un choix de normalisation, pas une
constante issue d'un contrat. Le comportement est documenté dans le moteur et
couvert par un test à valeurs calculées à la main ; reste à faire figurer dans
`METRICS_SPEC.md` si l'interface l'affiche de façon engageante. Ce n'est pas un
bug : le rapport le mentionne pour arbitrage ultérieur, pas comme un défaut.

---

## 9. Interface `echos-ui`

L'interface présentait des défauts du même ordre que le backend : des données
affichées avec une confiance qu'elles n'avaient pas.

### 9.1 L'export « CSV » téléchargeait du JSON

`client.exportRun` annonçait `Accept: text/csv` et typait sa réponse
`ExportJsonResponse | Blob`. La route renvoie **toujours** du JSON, le CSV étant
transporté dans une enveloppe `{content_type, body}` : la branche `Blob` était
du code mort, et le fichier nommé `run-1.csv` contenait
`{"run_id": …, "content_type": "text/csv", "body": "…"}` — illisible par
n'importe quel tableur. Le client normalise désormais l'enveloppe en
`{filename, mime, content}` et un test vérifie le **contenu** réellement remis au
téléchargement, pas l'appel de téléchargement. Même correction pour
`/api/compare?format=csv`, jusqu'alors non exposé.

### 9.2 Deux runs confondus, sans signal visible

`beliefs`, `relationships` et `phenomena` appelaient l'API **sans `run_id`**.
La route résout alors « le run le plus récent » — pas le run sélectionné dans
l'interface. L'inspecteur d'entité et le panneau de phénomènes affichaient donc
les données d'un run sous le nom d'un autre, sans le moindre indice. Ces trois
appels transmettent désormais le run affiché.

Corollaire : le sélecteur de run n'existait nulle part. Il est dans la barre
supérieure, ce qui rend la sélection explicite au lieu d'être implicite par
défaut côté serveur.

### 9.3 Le cache de métriques mélangeait deux cadences

La clé de cache ignorait `every`. Le tableau de bord (`every=1`) et l'écran
Analyse (`every=2, 5, 10…`) écrivaient dans la même entrée : la fusion
intercalait des ticks absents de la réponse et produisait des `NaN`, rendus par
ECharts comme des zéros — un effondrement numérique là où il n'y avait qu'un
trou. La clé inclut la cadence, la fusion refuse de mélanger deux
échantillonnages, et `TimelineChart` convertit les valeurs non finies en `null`
avec un décompte explicite des ticks sans valeur observée.

### 9.4 Le graphe social : N+1 relancé à chaque tick

`SocialGraph` refaisait `/api/groups` puis **une requête `/api/relationships`
par membre, en série**, à chaque changement de tick. Sur 40 entités à 1 tick/2 s,
c'était 40 requêtes séquentielles par tick. Les requêtes sont désormais menées
par lots (6 en vol), une seule fois par tick réel — le tick est mémorisé, pas
recalculé à chaque rendu — et un test vérifie à la fois la borne de concurrence
et l'absence de requête supplémentaire sur re-rendu.

### 9.5 Les entités isolées n'étaient pas inspectables

`_groups_of` exclut volontairement les singletons : une entité isolée n'est pas
une communauté. L'onglet « Entités » ne lisait que les membres de groupe, donc
la liste pouvait afficher « 0 entité » pendant que le tableau de bord en
annonçait 40, et une entité seule ne pouvait pas être inspectable. La liste est
désormais l'union des membres de groupe et des agents vus dans le flux, avec
leur nombre d'isolées.

Ce n'est pas une contradiction avec §2.2 : l'analyse refuse de compter un
singleton comme communauté (une métrique de communauté n'a pas de sens avec un
membre), l'interface refuse de ne pas le montrer (un agent observé existe). Les
deux règles portent sur des objets différents.

### 9.6 Une jauge alertait sur le bon comportement

« Vitesse de diffusion » portait `warnAbove: 50` : un monde qui propageait
rapidement l'information s'affichait en rouge. Une vitesse de diffusion n'est pas
un défaut ; son anomalie est l'immobilisation. `Gauge` distingue désormais
`warnBelow` (trop peu) et `warnAbove` (trop beaucoup), refuse d'alerter sur une
valeur non finie (`NaN` affiché `—`, aiguille à zéro) et **n'alerte jamais sur
un repli neutre** : c'est la valeur du moteur faute de données, pas une
observation sous le seuil.

### 9.7 Cycle de vie et erreurs

- `disconnect()` fermait la socket, dont l'événement `close` replanifiait une
  connexion 2 s plus tard : une déconnexion volontaire se reconnectait. Un
  drapeau `shouldReconnect` est armé par `connect()`, désarmé par `disconnect()`,
  et `AppShell` nettoie au démontage — la socket et son minuteur ne survivent
  plus à l'écran. Les messages d'une socket détruite sont également ignorés.
- `useLoadRuns()` était appelé par **chaque** écran : une requête `/api/runs`
  par écran monté. Chargé une fois dans `AppShell`, avec requête partagée.
- `usePhenomena` avalait ses erreurs (`.catch(() => undefined)`) : un échec
  réseau s'affichait comme « Aucun phénomène détecté », soit un constat faux
  présenté comme une observation. Les erreurs de phénomènes et de groupes sont
  maintenant affichées.
- `useGroups` ne se rafraîchissait pas au tick (compteur figé) et conservait les
  groupes du run précédent au changement de run.
- `ControlScreen` déclarait `seed` après la closure qui l'utilise, et importait
  `client` en doublon ; l'ordre des états est corrigé.
- Les lignes d'entité sont maintenant des `role="option"` avec `aria-selected` et
  activables à la barre d'espace, et non des `div role="button"` qui
  n'activaient qu'à `Entrée`.

### 9.8 Tests

14 fichiers, 61 tests (contre 6 fichiers / 18 tests). Chaque correction a un
test qui échouerait sans elle : contenu réel du fichier CSV téléchargé,
transmission du `run_id`, isolation des cadences de cache, non-reconnexion après
`disconnect()`, nettoyage au démontage, requête unique pour la liste des runs,
borne de concurrence du graphe social, entités isolées, `NaN` en trou de série,
et les trois seuils de jauge.

`npm test` lance désormais `vitest run` : le script par défaut était `vitest`,
qui en mode `vitest` interactif ne termine jamais et accroche une CI. Le
`test:watch` reprend le mode interactif.

---

## 10. Validation

| Commande | Résultat |
|---|---|
| `pytest` (echos) | 283 passés, 2 skips |
| `pytest` + couverture | 93 % (seuil 80 %) |
| `flake8 echos --max-line-length=100` | propre |
| `npm run lint` (echos-ui) | propre |
| `npm test` (echos-ui) | 61 passés, 14 fichiers |
| `npx tsc --noEmit` (echos-ui) | propre |
| `npm run build` (echos-ui) | propre |
| E2E SYNE réel (`LIVEX_SYNE_E2E=1`) | 1 passé |
| E2E syne-mock (`LIVEX_MOCK_E2E=1`) | 1 passé |

Les deux scénarios d'intégration ont été exécutés, pas seulement collectionnés.
Chacun ingère 3 ticks d'un vrai flux et contrôle ensuite le contrat REST. Les
assertions de provenance (`measured`, §2.4) et de normalisation des groupes
(§8.1) y sont vérifiées, donc les deux corrections sont validées contre les
émetteurs réels et pas seulement contre des fixtures.

Le build émet toujours l'avertissement de chunk > 500 kB : c'est le chunk
`echarts` (1,14 Mo / 385 ko gzip), déjà isolé par `manualChunks` dans
`vite.config.ts` pour le cache long et le bundle initial léger. L'avertissement
porte donc sur la taille de la bibliothèque, pas sur un découpage absent.
Préexistant, sans effet sur le chargement initial.

---

## 11. Fichiers touchés

Source (20) : `analysis/` (`__init__`, `_common`, `causal`,
`cognitive_diversity`, `emergence`, `feedback_loop_detector`,
`goal_convergence`, `group_dynamics`, `information_propagation`,
`reproducibility`, `resource_sustainability`, `social_complexity`),
`api/routes.py`, `dev_ingest.py`, `ingestion/{__init__,models,ws_client}.py`,
`instrumentation/logging.py`, `storage/{pipeline,sqlite}.py`,
`pyproject.toml`.

Tests (15) : `test_analysis`, `test_api_routes`, `test_causal_analysis`,
`test_compare`, `test_dev_ingest`, `test_emergence`, `test_ingestion_models_v01`,
`test_instrumentation`, `test_j2_determinism`, `test_j3_determinism`,
`test_pipeline`, `test_sqlite_store`, `test_syne_echos_integration`,
`test_syne_mock_integration`, `test_ws_client`, plus
`fixtures/snapshot_analysis.json` et `golden/analysis_golden.json`.

Interface `echos-ui` — source (18) : `api/{client,types,download}.ts`,
`hooks/useData.ts`, `ws/realtime.ts`,
`components/agents/AgentInspector.tsx`, `components/layout/AppShell.tsx`,
`components/viz/{Gauge,SocialGraph,TimelineChart}.tsx`,
`screens/{Analysis,Control,Dashboard,Explore,Log,SocialGraph}Screen.tsx`,
`index.css`, `package.json`. `store/index.ts` n'a pas été modifié.

Interface `echos-ui` — tests (14, dont 8 nouveaux ou étendus) :
`api/client.test.ts`, `ws/realtime.test.ts`, `hooks/useData.test.tsx`,
`components/layout/AppShell.test.tsx`,
`components/viz/{SocialGraph,Gauge,TimelineChart}.test.tsx`,
`screens/{Control,Dashboard,Explore}Screen.test.tsx` (nouveaux ou étendus) ;
`App.test.tsx`, `components/agents/badges.test.tsx`,
`components/kpi/KPICard.test.tsx`, `components/viz/WorldGrid.test.tsx`
(préexistants, non modifiés).

Modifications hors ECHOS dans le dépôt (`docs/pages-dist/docs/styles/*.js`) :
**préexistantes, non touchées**. `syne-mock/node_modules` a été installé pour
exécuter l'E2E (répertoire ignoré par git).
