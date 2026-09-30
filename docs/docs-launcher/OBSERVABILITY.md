# OBSERVABILITY.md

**Composant** : LIVEX (Launcher)
**Statut** : [DRAFT]
**Dernière mise à jour** : 30 septembre 2026
**Dépend de** : `COMPONENTS.md`, `NETWORK.md`, `INTEGRATION_CONTRACT.md`, `../../COMMUNICATION.md`
**Source Monographie** : —

---

## 1. Objet et portée

Ce document spécifie la supervision de LIVEX à quatre emboîtements :

- **globale** : LIVEX dans son ensemble ;
- **par composant** : SYNE, ECHOS, PRISM, Launcher, Gateway ;
- **par simulation** : un run en cours ;
- **par campagne** : une expérience multi-run.

Trois exigences structurent le reste :

1. **Le monitoring ne modifie jamais le résultat d'une simulation.** Aucune métrique
   ne consomme le générateur aléatoire de SYNE ni ne conditionne la logique de
   simulation. Le surcoût de collecte est mesuré, pas supposé.
2. **Léger par défaut, extensible en option.** Le Launcher est une application de
   bureau : aucune dépendance obligatoire à Prometheus, Grafana ou Docker. Ces
   outils restent des **exports optionnels**.
3. **Un contrat identique pour tous les composants** — endpoints et nommage des
   métriques définis par `INTEGRATION_CONTRACT.md`, sans exception.

> Les seuils chiffrés de ce document sont des **valeurs de départ à calibrer** sur
> des runs réels. Ils ne sont pas des exigences.

## 2. Les six niveaux d'observation

| Niveau | Objet | Exemples |
| :-- | :-- | :-- |
| **L0 — Hôte** | La machine | CPU, RAM, disque libre, réseau |
| **L1 — Processus** | Un exécutable | PID, uptime, CPU/mémoire, redémarrages |
| **L2 — Service** | L'API d'un composant | Disponibilité, latence, erreurs, connexions |
| **L3 — Simulation** | Un run en cours | Tick, cadence de tick, agents, événements/s |
| **L4 — Campagne** | Une expérience multi-run | Progression, échecs, durée médiane, ETA |
| **L5 — Global** | LIVEX entier | Santé agrégée, alertes actives |

Un niveau supérieur se compose des niveaux inférieurs : aucun ne duplique une
mesure. Le niveau L3 se nourrit du flux WebSocket existant, sans second canal.

## 3. Modèle d'état

### 3.1 États d'un composant

```text
Non installé · Arrêté · Démarrage · Actif · Dégradé · Injoignable · Arrêt en cours · Échec · Inconnu
```

Deux clarifications souvent confondues :

- **Dégradé** : le processus et l'API répondent, mais un critère de santé est hors
  seuil (cadence de tick très basse, client WebSocket attendu absent).
- **Injoignable** : le processus est vivant **et** l'un de ces deux cas survient —
  la sonde `ready` échoue au-delà du délai, ou le tick est figé alors que la
  simulation est en cours. **Un tick figé en pause n'est pas un blocage.**
- **Connecté** n'est **pas** un état de composant. C'est une *relation*
  (« ECHOS connecté à SYNE ») à afficher comme attribut de lien.

### 3.2 Règle d'agrégation globale

Chaque composant porte un poids selon le profil actif : `requis` ou `optionnel`.

| Situation | État global |
| :-- | :-- |
| Tous les composants `requis` en **Actif**, aucun optionnel en Échec | **Sain** |
| Un `requis` en **Dégradé**, ou un optionnel en Échec/Injoignable | **Dégradé** |
| Un `requis` en Échec/Injoignable/Arrêté alors que le profil l'exige | **Hors service** |
| Aucun composant démarré | **Inactif** |

La règle est *le pire état parmi les composants requis, adouci pour les
optionnels*. L'état global affiche **toujours** sa cause principale, par exemple
`Dégradé — SYNE : cadence de tick basse`.

### 3.3 Détection de blocage

Un run est déclaré **bloqué** si le tick n'avance pas pendant `stallTimeout` — une
valeur de départ à calibrer en fonction de la durée de tick attendue — alors que
`simulationState == running`. Le Launcher lit l'indicateur `livex_syne_tick` et
l'événement WebSocket correspondant.

## 4. Contrat de santé

Chaque composant expose sur son port de contrôle :

| Endpoint | Sémantique | Usage |
| :-- | :-- | :-- |
| `GET /health/live` | Le processus est vivant | Niveau L1 |
| `GET /health/ready` | Prêt à travailler | Détermine l'état **Actif** fonctionnel |
| `GET /health/details` | Détail par sous-vérification, avec durée | Vue Composants, diagnostic |
| `GET /info` | id, version, `protocolVersion`, build, plateforme, seed courante, simulation chargée | Compatibilité, reproductibilité |
| `GET /metrics` | Métriques du §5, texte Prometheus et/ou JSON | Collecte |

Exemple de réponse `/health/details` :

```json
{
  "status": "Degraded",
  "checks": [
    { "name": "process",        "status": "Healthy",  "durationMs": 0 },
    { "name": "simulation_api", "status": "Healthy",  "durationMs": 3 },
    { "name": "tick_progress",  "status": "Degraded", "message": "cadence 2.1/s < seuil" }
  ]
}
```

**Définition de « prêt » par composant :**

| Composant | `ready` signifie |
| :-- | :-- |
| SYNE | API de contrôle en écoute **et** simulation chargée ou chargeable |
| ECHOS | API d'analyse en écoute ; la connexion à SYNE est un état de lien, pas une condition de `ready` |
| PRISM | Processus vivant et, si son moteur l'expose, scène chargée |
| Gateway | Routes chargées et registre accessible |

> Si le moteur de PRISM ne permet pas d'exposer HTTP simplement, l'alternative est
> un fichier de statut plus un battement de cœur sur le WebSocket. **[À CONFIRMER
> selon PRISM]**

## 5. Métriques par composant

Convention de nommage : `livex_<composant>_<nom>_<unité>`. Étiquettes autorisées :
`instance`, `experiment_id`, `run_id`, `component`.

**Interdit** : une étiquette par agent. Elle produirait une cardinalité incontrôlée
et ferait tomber la collecte.

### 5.1 SYNE

| Métrique | Type | Utilité |
| :-- | :-- | :-- |
| `livex_syne_tick` | jauge | Tick courant |
| `livex_syne_tick_rate_hz` | jauge | Cadence de tick effective |
| `livex_syne_tick_duration_seconds` | histogramme p50/p95/p99 | Charge de simulation |
| `livex_syne_agents` | jauge | Nombre d'agents |
| `livex_syne_events_total` | compteur | Événements émis |
| `livex_syne_snapshot_bytes` | histogramme | Taille des instantanés |
| `livex_syne_snapshot_serialize_seconds` | histogramme | Coût de sérialisation |
| `livex_syne_ws_clients` | jauge | Clients WebSocket connectés |
| `livex_syne_control_commands_total{cmd,result}` | compteur | Commandes de contrôle reçues |
| `livex_syne_process_memory_bytes` | jauge | Santé du runtime |
| `livex_syne_gc_pause_seconds` | histogramme | Santé du runtime |
| `livex_syne_simulation_state` | jauge énumérée | running / paused / stopped |

### 5.2 ECHOS

| Métrique | Utilité |
| :-- | :-- |
| `livex_echos_ws_state` | Connexion à SYNE |
| `livex_echos_snapshot_lag_ticks` | Retard de l'analyse en temps réel |
| `livex_echos_analysis_jobs{state}` | Travaux en file, en cours, échoués |
| `livex_echos_analysis_duration_seconds` | Durée par type d'analyse |
| `livex_echos_api_request_duration_seconds` | Latence API et interface |
| `livex_echos_process_memory_bytes` | Mémoire |

### 5.3 PRISM

| Métrique | Utilité |
| :-- | :-- |
| `livex_prism_fps` | Fluidité de rendu |
| `livex_prism_frame_time_seconds` | Temps par image |
| `livex_prism_snapshot_lag_ticks` | Retard par rapport à SYNE |
| `livex_prism_ws_state` | Connexion |
| `livex_prism_process_memory_bytes` | Mémoire |

### 5.4 Launcher

| Métrique | Utilité |
| :-- | :-- |
| `livex_launcher_component_state{component}` | État courant |
| `livex_launcher_component_restarts_total` | Instabilité |
| `livex_launcher_healthcheck_duration_seconds` | Santé du monitoring lui-même |
| `livex_launcher_experiment_runs{state}` | Progression de campagne |
| `livex_launcher_run_duration_seconds` | Base des alertes de durée et de l'ETA |
| `livex_launcher_disk_free_bytes{path}` | Espace disponible pour les runs |

### 5.5 Gateway

Requêtes par route, connexions WebSocket ouvertes, latence de relais, refus
d'authentification ou d'origine, erreurs amont. Voir `NETWORK.md`.

## 6. Collecte et stockage

```text
Composants ── /metrics (pull, cadence configurable) ──► Launcher
                                                         ├─ tampon mémoire borné ──► tableau de bord
                                                         ├─ Experiments/<id>/runs/<run>/metrics.jsonl
                                                         └─ [optionnel] OTLP / Prometheus ──► outils externes
```

- **Pull** pour la santé et les métriques : le Launcher fixe la cadence et n'est
  jamais sollicité par les composants. L'état fin de simulation utilise le flux
  WebSocket **existant** — pas de second canal.
- **`metrics.jsonl` par run** : une ligne par échantillon (`t_wall_utc`, `tick`,
  `name`, `value`, `labels`). Sert au diagnostic post-mortem et à l'historique.
  Le Launcher **transporte** ces données ; les **métriques scientifiques** restent
  définies par ECHOS.
- **Rétention** : tampon mémoire borné ; fichiers de run soumis aux règles de
  nettoyage et d'archivage de `DATA_FLOW.md`.
- **Docker / Prometheus / Grafana** : un `docker-compose` d'observabilité peut être
  fourni **pour le développement uniquement**.

## 7. Journaux et corrélation

### 7.1 Schéma de journal

```json
{
  "ts": "2026-09-30T10:12:03.482Z",
  "level": "Error",
  "component": "syne",
  "instanceId": "syne-0001",
  "node": "local",
  "operation": "Run",
  "experimentId": "EXP-2026-001",
  "runId": "RUN-0042",
  "correlationId": "c1f0…",
  "tick": 18452,
  "message": "…",
  "exception": null
}
```

- Format **JSON Lines**, un fichier par composant sous `Logs/<composant>/`,
  **rotation** par taille et par date.
- Les composants écrivent le même schéma. À défaut, le Launcher enveloppe leur
  `stdout`/`stderr` et y ajoute les champs manquants.
- Copie ou index des lignes d'un run vers `Experiments/<id>/runs/<run>/logs/`, pour
  rattacher un journal à un run.

### 7.2 Corrélation

- `correlationId` est créé par le Launcher pour chaque opération — démarrage, run,
  analyse — puis propagé en en-tête `X-Livex-Correlation-Id` **et** en argument ou
  variable d'environnement.
- Le traçage distribué complet (spans OpenTelemetry) est **optionnel, V1.x**. Pour
  V0.1, la corrélation par identifiant suffit.

## 8. Alertes

| Alerte | Condition de départ | Sévérité | Action par défaut |
| :-- | :-- | :-- | :-- |
| Battement de cœur perdu | `health/live` en échec sur N échecs consécutifs | Critique | Marquer **Injoignable**, appliquer `OnRunFailure` |
| Tick bloqué | Tick figé au-delà de `stallTimeout` en état running | Critique | Notification, capture de journaux, politique de run |
| Cadence de tick dégradée | Cadence < X % de la médiane des runs précédents | Avertissement | **Dégradé** + notification |
| Durée de run anormale | Supérieure à k × la durée médiane | Avertissement | Notification |
| Mémoire en croissance continue | Pente positive soutenue | Avertissement | Notification, fuite suspectée |
| Disque bas | Espace libre sous le seuil, ou sous l'estimation du reste de campagne | Critique | Empêcher le run suivant, notifier |
| WebSocket déconnecté | Client attendu absent au-delà du délai | Avertissement | **Dégradé** |
| Redémarrages répétés | Plus de M en T minutes | Critique | Arrêter la boucle de reprise |
| Refus d'authentification répétés | Au-delà du seuil | Avertissement | Journal d'audit, notification |

Tous les seuils vivent dans `Config/monitoring.json`, surchargeables par profil et
par expérience. Chaque alerte porte un identifiant, une sévérité, un composant, une
date et un état — active, acquittée ou résolue — et est journalisée.

## 9. Coût et innocuité

- **Budget de surcoût** à mesurer par spike : écart de durée de tick avec et sans
  instrumentation. Objectif : négligeable devant la durée d'un tick. **[À valider]**
- **Déterminisme** : test dédié — même seed, monitoring activé et désactivé, donne
  une **empreinte de résultat identique**.
- **Échantillonnage adaptatif** si nécessaire, en réduisant la cadence quand la
  charge est haute.
- Le Launcher ne charge jamais les données brutes pour afficher une progression : il
  lit des métriques.

## 10. Vues d'interface

| Vue | Contenu |
| :-- | :-- |
| **Tableau de bord global (L5)** | État agrégé et cause principale, alertes actives, une ligne par composant, simulation active, campagne en cours |
| **Vue Composant (L1–L2)** | Santé détaillée, courbes des métriques du §5, derniers journaux filtrés, redémarrages, version et protocole |
| **Vue Simulation (L3)** | Tick, cadence, p95 de durée de tick, agents, événements/s, clients WebSocket |
| **Vue Campagne (L4)** | Grille des runs par état, durées, échecs, espace disque estimé |
| **Vue Diagnostic** | Registre de services, derniers contrôles de santé, commandes de contrôle reçues, corrélation d'une opération de bout en bout |

Règle d'accessibilité : chaque état est rendu par une **icône et un texte**, jamais
par une couleur seule.

## 11. Plan de mise en œuvre

| Étape | Contenu | Jalon |
| :-- | :-- | :-- |
| **M0** | Contrat `/health`, `/info`, `/metrics` figé ; états étendus et règle d'agrégation | Avant tout composant réel |
| **M1** | Contrôles de santé à deux niveaux + tableau de bord global et par composant | Launcher V0.1 |
| **M2** | Métriques L1 et L2, tampon mémoire borné | V0.1 |
| **M3** | Journaux JSON Lines, rotation, corrélation par run | V0.1 |
| **M4** | `metrics.jsonl` par run, détection de blocage, alertes locales | V0.2 |
| **M5** | Vues Simulation et Campagne, ETA | V0.2 |
| **M6** | Export OTLP et Prometheus optionnel, traces | V1.x |
| **M7** | Monitoring multi-nœuds, avec Node Agent et Gateway | V1.x |

## 12. Tests

- Composant tué → état **Échec** détecté.
- Composant gelé, tick figé → état **Injoignable**.
- Pause volontaire → **aucune** alerte de blocage.
- Disque plein simulé → alerte et blocage du run suivant.
- Seuils modifiés à chaud → pris en compte sans redémarrage.
- Déterminisme : monitoring activé et désactivé, résultats identiques.
- Campagne longue → aucune fuite mémoire du tampon de métriques.
- Dégradé puis retour au vert → alerte de rétablissement journalisée.
## Points restés ouverts dans ce document

- Les seuils chiffrés sont des valeurs de départ : ils doivent être calibrés sur des
  runs réels avant de devenir des exigences.
- La capacité de PRISM à exposer des endpoints HTTP n'est pas confirmée. À défaut,
  un fichier de statut et un battement de cœur sur le WebSocket.
- Le surcoût de l'instrumentation sur la boucle de tick reste à mesurer.
- Le nombre de battements de cœur manqués avant de déclarer un nœud injoignable
  n'est pas calibré.
