# API_CONTRACTS.md

**Composant** : SYNE
**Statut** : [STABLE]
**Dernière mise à jour** : 21 septembre 2026
**Dépend de** : `../COMMUNICATION.md`, `DATA_MODEL.md`
**Source Monographie** : §2.4 (contrats de transport), §5.4 (PRISM), §3.24 (événements), ADR-003/ADR-004

---

## 1. Objectif

Définit les **contrats de données** exposés par SYNE — ils sont la langue commune entre SYNE, ECHOS et PRISM. Toute évolution est gérée par `../../VERSIONING.md`.

## 2. Contrat temps réel — WebSocket 5180

Transport : WebSocket local, **binaires JSON** (`camelCase`). Deux types de messages (Monographie §5.4.1) :

> **Implémentation V0.1 (SYNE-080, livré avec U1)** : émetteur BCL (HttpListener + `AcceptWebSocketAsync`,
> zéro dépendance) dans `Simulation.Console`, activé par `--observe` (port `--observe-port`, défaut 5180,
> bind `127.0.0.1`). Chaque tick émet **1 snapshot + 1 `tick_summary` + 1 `decision_made` par entité**,
> diffusion à **tous** les consommateurs connectés. L'émission n'ajoute aucun tirage PRNG (déterminisme
> inchangé, DETERMINISM.md §3).

### 2.1 `snapshot` — WorldSnapshot

| Champ | Type | Description |
| :-- | :-- | :-- |
| `version` | string | Version du contrat (SemVer) |
| `runId` | string | Identifiant du run |
| `tick` | uint | Numéro de tick courant |
| `simulatedTimeMinutes` | uint | Temps simulé (minutes) |
| `aliveCount` | uint | Entités vivantes |
| `agents[]` | array | État des entités (position, santé, énergie, faim, soif, action courante...) |
| `resources[]` | array | Ressources (type, position, quantity/capacity) |

Exemple (format condensé) :

```json
{ "type": "snapshot", "version": "0.1.0", "runId": "run-abc",
  "tick": 5010, "simulatedTimeMinutes": 5010, "aliveCount": 98,
  "agents": [ { "id": "a1", "position": {"x": 53.0, "y": 76.5}, "health": 80,
                "energy": 60, "hunger": 30, "thirst": 40, "currentAction": "MoveTo" } ],
  "resources": [ { "id": "f1", "type": "food", "position": {"x": 60, "y": 80},
                   "quantity": 90, "capacity": 100 } ] }
```

> En V2 : intégrer beliefs, goals, relations, groupes dans le snapshot (prototype V2 §04-ARCHITECTURE).
> V0.1 émet par entité : `id` (uint), `species`, `position{x,y}`, `energy`, `hunger`, `thirst`, `fatigue`,
> `currentAction` (intention `DesireKind`, ex. `Idle`, `SeekWater`) ; `runId` = `run-<seed>`.

### 2.2 `event` — ExternalEvent

| Champ | Type | Description |
| :-- | :-- | :-- |
| `type` | string | Type d'événement (`decision_made`, `agent_spawned`, `agent_died`, `message_sent`, `group_formed`, `conflict`...) |
| `tick` | uint | Tick |
| `agentId?` | string | Entité concernée |
| `targetId?` | string | Cible |
| `action?` | string | Action (le cas échéant) |
| `cause?` | string | Cause |
| `value?` | object | Valeur contextuelle (détails) |

Exemple :

```json
{ "type": "decision_made", "tick": 5010, "agentId": "a1",
  "action": "Eat", "cause": "hunger 75", "value": { "utility": 15.5 } }
```

> Événements typés du prototype : `tick_summary`, `agent_spawned`, `agent_died`, `decision_made` (ADR-004).
> **V0.1 émet** `tick_summary` (1/tick, `value.aliveCount`) et `decision_made` (1/entité/tick,
> `value = {intention, utility}`, `cause = "hunger=…,thirst=…,fatigue=…"`).
> `agent_spawned`/`agent_died` attendront la mortalité (ph4).

## 3. Contrat de contrôle — HTTP 5181

API REST locale de contrôle, relayée par PRISM (Monographie §5.4.2) :

| Méthode | Endpoint | Corps |
| :-- | :-- | :-- |
| POST | `/api/control/start` | `{ seed, config }` (optionnel) |
| POST | `/api/control/pause` | — |
| POST | `/api/control/resume` | — |
| POST | `/api/control/reset` | `{ seed, runId }` |

- Binding local : `127.0.0.1:5181`.
- État interrogé par polling (~2 s, [HÉRITÉ]).

## 4. Contrat de persistance

Le schéma SQLite (Annexe G) sert de **contrat de persistance** — voir `PERSISTENCE.md`. Les tables `events_log` et `decision_traces` alimentent l'analyse ECHOS (voir `LOGGING_INSTRUMENTATION.md` ECHOS).

## 5. Compatibilité ascendante

- Ajout de champs = compatible (`MINOR`).
- Suppression/renommage/redéfinition de sens = **breaking** (`MAJOR`).
- Le champ `version` du snapshot permet aux consommateurs de détecter les changements.

---

## Points restés ouverts dans ce document
- Extension exacte du `WorldSnapshot` V0.1 (beliefs/goals/relations/groups) à figer lors de l'implémentation.
- Nomenclature exhaustive des types d'`ExternalEvent` V0.1 (alignée sur les événements du système).
- Multi-consommateur : **tranché en V0.1** — diffusion à tous les clients connectés (pas de règle single-consumer).