# TRANSPORT_API — PrismLdk ↔ SYNE

**Composant** : PRISM
**Statut** : documentation d'intégration
**Dernière mise à jour** : 27 septembre 2026
**Source de vérité des contrats** : [`../docs-syne/API_CONTRACTS.md`](../docs-syne/API_CONTRACTS.md)

---

## 1. Vue d'ensemble

`PrismLdk` utilise deux transports SYNE distincts :

| Sens | Transport | Adresse locale par défaut | Usage |
| :-- | :-- | :-- | :-- |
| SYNE → plugin | WebSocket | `ws://127.0.0.1:5180/` | Monde préparé, snapshots, deltas et événements |
| Plugin → SYNE | HTTP | `http://127.0.0.1:5181` | Commandes de contrôle et requête de statut |

Ce sont les valeurs par défaut configurées par le plugin ; les URLs sont
remplaçables via `FPrismSyneConnectionOptions`. Le serveur WebSocket SYNE est
activé avec `--observe` (port par défaut 5180) et le serveur HTTP de contrôle
avec `--serve` (port par défaut 5181). Les deux serveurs écoutent localement
par défaut. Le lancement de SYNE est distinct du lancement du projet Unreal.

## 2. WebSocket : données SYNE vers Blueprint

Les messages sont des trames texte UTF-8 contenant du JSON camelCase, pas des
trames binaires. Après `Connect`, le plugin expose les types de messages SYNE
en types Blueprint et déclenche notamment :

| Message SYNE | Événement Blueprint | Contenu / rôle |
| :-- | :-- | :-- |
| `world_initialized` | `OnWorldInitialized` | Description du monde préparé, émise avant les snapshots |
| `snapshot` | `OnSnapshot` | État dynamique complet du monde pour un tick |
| `world_delta` | `OnWorldDelta` | Deltas de monde, notamment mutations d'obstacles |
| autres événements typés | `OnSyneEvent` | Notifications de décision, action, communication, groupes et monde |

Un snapshot global est émis par tick. Il contient l'état des agents et des
systèmes actifs, les ressources globales, les obstacles, ainsi que
`worldChanges[]` et `actions[]` du tick. La topologie initiale arrive une fois
dans `world_initialized`. Les détails et les champs versionnés sont définis
dans [`../docs-syne/API_CONTRACTS.md`](../docs-syne/API_CONTRACTS.md).

**Échelle temporelle (ADR-017, contrat description 1.1 / observabilité 0.4.0)** :
`world_initialized.world` porte `simulatedSecondsPerTick` (secondes simulées par
tick — 60 au défaut, 5 pour le profil `prism`) et `metersPerUnit` (mètres par
unité SYNE, informatif — k = 100 uu/unité) ; chaque `snapshot` porte
`simulatedTimeSeconds` (`tick × simulatedSecondsPerTick`) à côté de
`simulatedTimeMinutes` (plancher entier, historique). **Aucune de ces valeurs
n'est codée en dur dans le plugin** : elles se lisent dans `world_initialized`
(§6 de la spec PRISM : `k = tuile_WP / cellSize`, horloge UI affichée via
`simulatedTimeSeconds` **et** le ratio « 1 s = R s simulées »,
`R = ticksPerSecond × simulatedSecondsPerTick`).

**Règle de consommation** : utiliser `OnSnapshot` comme source de vérité pour
réconcilier l'état courant présenté par le projet Unreal. Les événements et
deltas restent utiles pour le journal ou les effets ponctuels ; ils peuvent
décrire une modification également agrégée dans le snapshot. Ne pas appliquer
deux fois la même mutation.

La connexion initiale est déclenchée explicitement par `Connect`. La structure
des options prévoit la reconnexion automatique après une perte inattendue
(activée par défaut, délai par défaut de 2 secondes). `Disconnect` annule la
demande de connexion et sa reconnexion. `OnConnected`, `OnDisconnected` et
`OnError` exposent le cycle de vie au Blueprint.

## 3. HTTP : commandes Blueprint vers SYNE

Les commandes HTTP sont des POST JSON vers `/api/control/<action>`. Le plugin
fournit les fonctions Blueprint `Prepare`, `Ready`, `Start`, `Pause`, `Resume`,
`Stop` et `Reset`. `RequestStatus` effectue un GET sur
`/api/control/status`. Le résultat asynchrone d'une commande est transmis par
`OnControlResult` ; les erreurs réseau ou API sont communiquées par `OnError`.

| Méthode | Route | Fonction du plugin | Usage |
| :-- | :-- | :-- | :-- |
| POST | `/api/control/prepare` | `Prepare(seed, ticksPerSecond)` | Préparer un monde et sa cadence |
| POST | `/api/control/ready` | `Ready(worldVersion)` | Accuser réception après préparation côté projet |
| POST | `/api/control/start` | `Start(seed, maxTicks)` | Démarrer le monde préparé |
| POST | `/api/control/pause` | `Pause()` | Suspendre les ticks |
| POST | `/api/control/resume` | `Resume()` | Reprendre les ticks |
| POST | `/api/control/stop` | `Stop()` | Arrêter le run |
| POST | `/api/control/reset` | `Reset(seed, maxTicks)` | Réinitialiser selon le contrat SYNE |
| GET | `/api/control/status` | `RequestStatus()` | Lire l'état du run |

Le cycle de préparation recommandé est :

```text
Connect
 → Prepare(seed, ticksPerSecond)
 → OnWorldInitialized
 → le projet Unreal PRISM construit/prépare sa présentation
 → Ready(worldVersion)
 → attendre OnControlResult(ok)
 → Start(seed, maxTicks)
 → OnSnapshot
```

Après un `prepare` explicite, SYNE exige l'accusé `ready` avant `start` ;
ignorer l'ordre ou réutiliser une seed incompatible peut produire une erreur
HTTP (par exemple `409 world_not_ready`). La cadence `ticksPerSecond` est celle
du moteur de simulation, pas le framerate Unreal. `GET /api/control/status`
restitue `ticksPerSecond` et `simulatedSecondsPerTick` du monde préparé
(`null` tant qu'aucun monde n'est préparé). Les corps précis, états et
codes d'erreur suivent le contrat SYNE.

Le plugin ne fait pas de polling périodique automatique documenté de l'état :
Blueprint peut appeler `RequestStatus()` quand il a besoin de le relire.

## 4. SYNE et syne-mock

SYNE est le moteur décisionnel et l'autorité de production. `syne-mock` est un
simulateur de développement indépendant qui permet de tester le transport,
les contrats consommés par `PrismLdk` et les graphes Blueprint sans lancer le
moteur C#. Il parle les transports attendus et reproduit un sous-ensemble utile
à l'intégration, mais **n'est pas équivalent à SYNE**.

Ses limites documentées comprennent des approximations de délibération et de
systèmes sociaux, un détour local autour des obstacles qui ne reproduit pas
l'A* de SYNE, et l'absence de garantie de trajectoires identiques bit à bit.
Le mode replay rejoue des messages JSONL sans restaurer l'état interne de SYNE.
Consulter [`../../syne-mock/README.md`](../../syne-mock/README.md) pour les
différences à jour. Ne pas utiliser le mock pour valider l'équivalence des
décisions, trajectoires ou résultats métier.
