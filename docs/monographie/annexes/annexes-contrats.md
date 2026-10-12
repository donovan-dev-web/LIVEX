# Annexe A. Contrats de transport

Cette annexe rassemble les contrats échangés entre composants, dans leur forme courante (contrat d'observabilité 0.4.0, description de monde 1.1, version moteur 0.15.0). Elle ne remplace pas le contrat détaillé du composant : en cas d'écart, le code et le contrat du composant font foi.

## A.1 Ports et transports

| Flux | Transport | Port par défaut | Bind |
| :-- | :-- | :-- | :-- |
| Observabilité du monde | WebSocket | 5180 | local |
| Contrôle du moteur | HTTP REST | 5181 | local |
| Lecture d'analyse | HTTP REST | 5000 | local |
| Plage interne de l'orchestrateur | HTTP | 5200 à 5399 | local |

Les ports sont configurables. Un consommateur ne doit jamais supposer une valeur en dur.

## A.2 Initialisation du monde

Le message `world_initialized` précède tout instantané. Il porte la version, la graine, et une description du monde versionnée (version 1.1) comprenant : largeur, hauteur, taille de cellule, cadence, durée simulée par tick, correspondance d'unités, nombre de cellules, agents initiaux, cellules, obstacles, emplacements de ressources et régions.

Chaque agent initial porte un identifiant, une espèce et une position. Chaque cellule porte sa position, un type de terrain, sa praticabilité, une hauteur, un coût de déplacement et les obstacles qui la recouvrent. La description 1.1 ajoute deux champs additifs : la durée simulée par tick et la correspondance d'unités.

## A.3 Instantané par tick

Le moteur émet exactement un instantané global par tick. Champs principaux :

| Champ | Rôle |
| :-- | :-- |
| `version`, `engineVersion` | versions du contrat et du moteur |
| `runId`, `seed` | identité du run, graine effective |
| `tick` | numéro de tick |
| `simulatedTimeMinutes`, `simulatedTimeSeconds` | temps simulé (minutes et secondes) |
| `aliveCount` | entités vivantes |
| `agents[]` | état complet exposé de chaque entité |
| `resources[]` | stocks globaux des quatre réserves |
| `obstacles[]` | obstacles et constructions |
| `groups[]`, `territories[]`, `books[]` | systèmes collectifs activés |
| `worldChanges[]` | mutations du monde de ce tick |
| `actions[]` | résultats d'action du tick |

Le format du `runId` pour un run piloté est `run-<seed>-<12hex>`. L'instantané est la source de vérité de l'état dynamique ; les événements servent au journal et aux effets ponctuels.

## A.4 Événements typés

| Événement | Déclencheur |
| :-- | :-- |
| `tick_summary` | une fois par tick |
| `decision_made` | une fois par entité et par tick |
| `action_completed` | une fois par entité et par tick |
| `message_sent`, `message_received` | à chaque pulsation |
| `group_formed`, `group_dissolved`, `group_decision` | à chaque révision de groupe |
| `agent_spawned` | à une naissance |
| `agent_died` | à une mort |
| `world.construction_placed`, `world.construction_removed` | à une mutation d'obstacle |
| `world.season_changed` | au basculement de saison |
| `world.territory_membership_changed` | au franchissement de zone |
| `world.book_written`, `world.book_read` | écriture ou lecture d'un livre |

Chaque événement porte un type, un tick et, selon le cas, un identifiant d'entité, une cible, une action, une cause et une valeur contextuelle.

## A.5 Contrôle

| Méthode | Route | Corps / rôle |
| :-- | :-- | :-- |
| POST | `/api/control/prepare` | `{ seed?, ticksPerSecond?, config? }` |
| GET | `/api/world` | restitution du monde préparé |
| POST | `/api/control/ready` | `{ worldVersion? }` |
| POST | `/api/control/start` | `{ seed?, config? }` |
| POST | `/api/control/pause` | aucun corps |
| POST | `/api/control/resume` | aucun corps |
| POST | `/api/control/reset` | `{ seed?, config?, maxTicks? }` |
| GET | `/api/control/status` | état courant |
| POST | `/api/control/stop` | arrêt propre |

États : inactif, monde en préparation, prêt, en cours, en pause, terminé. Un démarrage sur un run terminé renvoie une erreur dédiée orientant vers une réinitialisation. Le contrôle est non intrusif : un run piloté égale un run ininterrompu.

## A.6 Compatibilité

| Nature du changement | Effet |
| :-- | :-- |
| Ajout d'un champ | compatible (mineur) |
| Suppression, renommage, changement de sens | incompatible (majeur) |

Le champ de version du message permet à un consommateur de détecter les évolutions. De nombreuses fonctionnalités récentes sont émises sous drapeaux, avec une sortie identique lorsqu'elles sont désactivées.

## A.7 API de lecture (ECHOS)

| Méthode | Route | Rôle |
| :-- | :-- | :-- |
| GET | `/health` | santé du service |
| GET | `/api/control/status` | état du moteur relayé |
| POST | `/api/control/{action}` | relais vers le moteur |
| GET | `/api/runs` | liste des runs |
| GET | `/api/runs/{id}` | métriques complètes du run |
| GET | `/api/runs/{id}/metrics` | séries de métriques |
| GET | `/api/runs/{id}/export` | export JSON ou CSV |
| GET | `/api/runs/{id}/decisions` | traces de décision |
| GET | `/api/runs/{id}/calibration` | rapport post-run |
| GET | `/api/runs/{id}/viability` | profil de viabilité |
| GET | `/api/metrics/catalog` | catalogue des métriques |
| GET | `/api/experiments/summary` | synthèse multi-runs |
| GET | `/api/runs/{id}/events` | journal d'événements |
| GET | `/api/runs/{id}/causal-chains/{agentId}` | chaîne causale |
| GET | `/api/beliefs/{agentId}` | croyances d'une entité |
| GET | `/api/relationships/{agentId}` | réseau de confiance |
| GET | `/api/groups` | groupes actifs |
| GET | `/api/world` | description du monde |
| GET | `/api/trust-graph` | graphe de confiance |
| GET | `/api/emergent-phenomena` | phénomènes et avertissement |
| GET | `/api/compare` | comparaison de deux runs |

L'API est en lecture seule. Sans base configurée, les routes de données répondent une indisponibilité (503) ; le contrat reste publié dans la spécification d'API.
