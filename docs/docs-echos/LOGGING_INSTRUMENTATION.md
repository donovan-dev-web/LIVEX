# LOGGING_INSTRUMENTATION.md

**Composant** : ECHOS
**Statut** : [STABLE]
**Dernière mise à jour** : 22 septembre 2026
**Dépend de** : `ARCHITECTURE.md`, `../docs-syne/API_CONTRACTS.md`, `CAUSAL_ANALYSIS.md`
**Source Monographie** : §4.8

---

Ce document aligne la spécification §4.8 sur le jalon ph5 réalisé en ECHOS
(Python) : les trois niveaux sont implémentés par le package
`echos/echos/instrumentation/` (ECHOS-050/051/052, issues #389/#390/#391) et
consommés par le pipeline `storage/pipeline.py`.

## 1. Les 3 niveaux de journalisation

| Niveau | Format | Support | Usage |
| :-- | :-- | :-- | :-- |
| **Événements structurés** | JSON Lines `logs/structured-<run>.jsonl` + schéma SQLite (`tick_metrics`, `events_log`, `tick_contexts`) | Interrogation ELT | Analyse scientifique |
| **Traces de décision** | JSON Lines `logs/decision-traces-<run>.jsonl` + schéma SQLite (`decision_traces`) | BDI complet | Reconstruction causale |
| **Logs texte** | `logging` standard (fichier quotidien) | Débogage | Développement |

Le répertoire de logs est configurable par `ECHOS_LOG_DIR` (défaut `logs/`).
Sérialisation **déterministe** : `json.dumps(..., sort_keys=True,
separators=(",", ":"))` — aucun horodatage d'émission dans les lignes
structurées (export reproductible, précepte ECHOS-027).

## 2. Les événements structurés

Écrits au fil de l'ingestion (WebSocket :5180) dans `events_log`
(colonnes `tick, type, agent_id, target_id, action, cause, value`) :

| Événement | Contenu |
| :-- | :-- |
| `PerceptionEvent` | `ObservedEntityIds`, `Count`, `AverageConfidence` |
| `DecisionEvent` | `ConsideredGoals`, `ActionScores`, `ChosenAction`, `ChosenUtility` |
| `ActionEvent` | `ActionType`, `ActionStatus` (Started/Updated/Completed/Failed), `Outcome` |
| `CommunicationEvent` | `SenderId`, `ReceiverIds`, `MessageType`, `MessageConfidence` (`message_sent`/`message_received` SYNE) |
| `BeliefEvent` | `Fact`, `OldConfidence`, `NewConfidence`, `Source` |
| `GroupEvent` | `GroupId`, `GroupAction` (Formed/Joined/Left/Dissolved) |

La nomenclature effective reçoit les types `ExternalEvent` émis par SYNE
(`decision_made`, `agent_spawned`, `agent_died`, `message_sent`,
`message_received`, `group_formed`, ...) — ingérés génériquement sans
allowlist stricte (ADR-004).

## 3. Les traces de décision

Chaque événement `decision_made` est tracé dans la table `decision_traces`
(schéma v3) avec le contexte BDI complet — brique de l'**analyse causale**
(cf. `CAUSAL_ANALYSIS.md`) et de la chaîne
`Action ← Intention ← Objectif ← Besoin ← Croyance ← Mémoire ← Perception`.

Schéma v3 (colonnes) :

| Colonne | Description |
| :-- | :-- |
| `run_id, tick, agent_id` | Clé `(run_id, tick, agent_id)` |
| `chosen_action` | Action choisie (ou `intention` du `value`) |
| `utility` | Utilité (score de décision) |
| `deliberated` / `interrupted` | Drapeaux de délibération / interruption |
| `cause` | Cause (ex. `hunger=100,thirst=100,fatigue=61.5`) |
| `beliefs_count, goals_count, memory_count` | Taille du contexte BDI observé au snapshot |
| `needs` | Besoins (`hunger, thirst, fatigue, energy`) stockés en JSON compact |

Construction : `instrumentation.decision_traces.build_decision_trace(run_id,
tick, event, engine_snapshot)` — fusion déterministe, aucun écrit dans le
monde observé. Ingestion : `storage.pipeline.consume` (compteur
`ConsumeResult.decision_traces_written`). Lecture : endpoint
`GET /api/runs/{run_id}/decisions` (tri `(tick, agent_id)`).

## 4. Les logs texte (logging standard — tag `SSE-V2`)

- Fichier quotidien : `logs/echos-{YYYY-MM-DD}.log`.
- Format : `"%(asctime)s [%(levelname)s] %(message)s"` (asctime local).
- Tag : chaque message est préfixé `[SSE-V2]` ; logger nommé `SSE-V2.*`.

| Niveau | Usage |
| :-- | :-- |
| ERROR | Échec d'action, état incohérent |
| WARNING | Chemin bloqué, ressources insuffisantes |
| INFO | Résumé de tick, formation de groupe |
| DEBUG | Décisions d'entités, mises à jour de croyances |

(Le niveau *Verbose* de la monographie n'est pas mappé : limitation à
`logging.DEBUG` par défaut, réglable par l'utilisateur du logger.)

## 5. Le profilage

ECHOS utilise des **ProfileMarkers** (`instrumentation.profiling`, basés sur
`time.perf_counter()`) pour mesurer le temps d'exécution de **chacun des
8 moteurs** d'analyse (`compute_all` avec marqueur optionnel — type inference
par duck-typing, aucun couplage de module). Le profilage n'altère jamais le
résultat : `compute_all_profiled(snapshot)` retourne des métriques
**bit-à-bit identiques** à `compute_all(snapshot)` (déterminisme ECHOS-027).

Le profil est écrit par tick dans `tick_contexts` (`context_type='profiling'`)
et en JSON Lines `logs/profilage-<run>.jsonl`.

Format de sortie (équivalent §5 de la monographie, aligné `name:<15`) :

```text
Perception    : 240.51 ms total,   0.48 ms avg
Decisions     :  12.34 ms total,   2.47 ms avg
```

Budgets V0.1 (calibration — cf. ISSUES.md §points restés ouverts) :

| Moteur | Budget CI (moyenne / tick) |
| :-- | :-- |
| Perception | < 8,0 ms |
| InformationPropagation | < 8,0 ms |
| SocialComplexity | < 8,0 ms |
| CognitiveDiversity | < 8,0 ms |
| GoalConvergence | < 8,0 ms |
| FeedbackLoopDetector | < 8,0 ms |
| ResourceSustainability | < 8,0 ms |
| EmergenceIndicators | < 10,0 ms |

Ces valeurs sont des cibles de **garde-fou CI** (1 moteur ≈ 1 appel par tick
sur fixture), pas une garantie de performance des plateformes réelles.

## 6. La console de débogage

Spécification monographie §4.8 (console interactive SYNE — hors périmètre
d'implémentation ECHOS). Les équivalents d'observation sont servis par l'API
REST (`/api/beliefs/{agent_id}`, `/api/runs/{run_id}/decisions`, ...).

## 7. L'export

- **JSON** des traces de décision : `GET /api/runs/{run_id}/decisions`
  (tri déterministe `(tick, agent_id)`, aucune dépendance temporelle).
- **JSON/CSV** des métriques : `GET /api/runs/{run_id}/export` (format RFC 4180).
- **CSV** des traces de décision (`AgentId, Tick, BeliefCount, GoalCount,
  ChosenAction, Utility`) : format documentaire — à exposer par l'API (point
  resté ouvert, §9).

---

## 8. Contrats vérifiés (CI)

- `test_instrumentation.py` : JSONL déterministe (clés triées), tag `SSE-V2`,
  fusion BDI (besoins/croyances/objectifs/mémoire), rejet des événements
  non-`decision_made`, profilage = `compute_all` bit-à-bit + couverture des
  8 moteurs + format §5.
- `test_pipeline.py` : `contexts_written == 12` (3 ticks × 4 contextes dont
  `profiling`), `decision_traces_written == 3`.
- `test_api_routes.py` : `/api/runs/{run_id}/decisions` reproductible + 404.

## 9. Points restés ouverts dans ce document

- Export **CSV** des traces de décision par l'API (format §7 documentaire).
- Calibration des budgets de profilage sur des sims réelles (SYNE — valeurs
  V0.1 posées en garde-fou CI, ECHOS-052).
- Niveau de log `Verbose` (monographie) — hors périmètre ECHOS, console SYNE.