# syne-mock

Mini-serveur Node.js déterministe pour intégrer Unreal au contrat SYNE. `npm install`
puis `npm start -- config_example.json`. Le WebSocket texte JSON camelCase est sur
`ws://127.0.0.1:5180/`; le contrôle HTTP est sur `http://127.0.0.1:5181`.

Le terminal affiche le suivi avec le préfixe `[SYNE-MOCK ...]` : démarrage des
ports, connexions/déconnexions WebSocket, requêtes/réponses HTTP et transitions
`PREPARE`, `READY`, `START`, `PAUSE`, `RESUME`, `STOP` et `RESET`.

## Intégration au Launcher LIVEX

`component.json` déclare le démarrage Linux direct par `src/cli.js`. L'adaptateur
accepte les arguments de service communs du Launcher et applique son
`--control-port` au serveur HTTP ; le WebSocket reste au port `dataPort` du
fichier JSON (5180 par défaut). La sonde `/api/control/status` ne démarre ni ne
modifie un run. `POST /control/shutdown` exige le jeton `LIVEX_SESSION_TOKEN`
fourni par le Launcher, puis ferme les serveurs HTTP/WebSocket. SIGINT et
SIGTERM utilisent la même fermeture propre. Les arguments inconnus sont
refusés ; le mock ne déclare pas les capacités de campagnes SYNE.

## Documentation d'intégration Unreal

La documentation destinée au futur plugin Unreal (C++ minimal, API native et
Blueprint) se trouve dans [`docs/`](docs/):

- [`UNREAL_PLUGIN.md`](docs/UNREAL_PLUGIN.md) : architecture, responsabilités
  C++, API Blueprint, cycle de vie, sécurité et exemples ;
- [`CONTRACT_REFERENCE.md`](docs/CONTRACT_REFERENCE.md) : contrats réellement
  émis par ce mock, mapping JSON vers `USTRUCT`/`UENUM`, ordre et erreurs ;
- [`IMPLEMENTATION_CHECKLIST.md`](docs/IMPLEMENTATION_CHECKLIST.md) : tests,
  compatibilité, versionnage et checklist de livraison.

Ces fichiers sont une façade d'intégration et ne constituent pas un nouveau
contrat : en cas de divergence, les services de `src/`, les tests de `test/`
et `docs/docs-syne/` restent la source de vérité.

Routes : `GET /api/control/status`, `GET /api/world`, `POST
/api/control/prepare|ready|start|pause|resume|stop|reset`.
Le cycle est `idle → worldPreparing → ready → running ⇋ paused → finished`.
`prepare` est explicite : appelez `ready` avec `{"worldVersion":"1.0"}` avant
`start`, sinon la réponse est `409 world_not_ready`. Un `start` direct conserve
l’auto-préparation implicite rétrocompatible. Le statut expose
`worldPrepared`, `worldVersion` et `worldReadyAcknowledged`.
Le corps de `start/reset` accepte `{"seed":42,"maxTicks":400}`. La génération par
défaut produit 50 agents et 400 ticks, sans hasard dépendant du temps. Chaque tick
émet un `snapshot`, `tick_summary`, `decision_made` et `action_completed` par agent.
Avant tout snapshot, le WebSocket émet `world_initialized` avec une
`WorldDescription` version `1.0`. Le monde est généré dans l'ordre
topologie → obstacles → ressources → agents. Les emplacements de ressources
initiaux sont des marqueurs de grille ; les stocks dynamiques dans les snapshots
restent globaux par type. Les obstacles initiaux restent identiques au monde
préparé jusqu'à une mutation explicite ; le mock n'ajoute plus d'obstacle
artificiel au premier tick.

Le code sépare transport HTTP/WebSocket (`src/server.js`), orchestration du run
(`src/simulation/simulation.js`), génération (`src/world/`), besoins/décisions,
destination et mouvement (`src/simulation/`) et assemblage du snapshot. La
génération et les mouvements sont déterministes à seed égale. Le mouvement
reproduit le choix de cible SYNE par agent/tick/action, sa vitesse et les
collisions d'obstacles, mais le détour local du mock ne remplace pas
l'implémentation A* de SYNE. La délibération et les systèmes sociaux restent
des approximations destinées à tester l'intégration Blueprint ; les trajectoires
ne sont pas garanties bit-à-bit identiques au moteur C#.

Tests ciblés : `npm test` (44 tests, sans dépendance à un
port fixe). La suite s'exécute aussi en intégration continue via le job `Tests
(SYNE-MOCK Node)` de `.github/workflows/ci.yml`, déclenché sur toute
modification de `syne-mock/`. Ports et paramètres sont configurables en JSON.
`replay.file` permet de rejouer un fichier JSONL (un message SYNE par ligne) ;
`replay.loop` le répète indéfiniment. Le replay est volontairement un transport
de messages et ne prétend pas restaurer l'état interne C#.

## Brancher ECHOS sur le mock

Le mock parle le même contrat que SYNE, ce qui permet de travailler sur ECHOS
sans faire tourner le moteur .NET. Deux variables d'environnement suffisent :

```bash
SYNE_OBSERVABILITY_URL=ws://127.0.0.1:5180/   # défaut : même valeur
SYNE_CONTROL_URL=http://127.0.0.1:5181        # défaut : même valeur
```

Les défauts d'ECHOS (`echos/echos/dev_ingest.py`, `echos/echos/api/routes.py`)
pointent déjà sur ces ports : sans rien définir, ECHOS se branche sur le mock
dès qu'il écoute. Le test
`echos/echos/tests/test_syne_mock_integration.py` vérifie le trajet complet
(contrôle HTTP → flux WebSocket → ingestion → stockage) sur le job
`Intégration mock → ECHOS` de `.github/workflows/ci.yml`.

Ce que le mock ne couvre pas, et qu'il ne faut pas attendre de lui :
`beliefs[]` et `trust[]` sont publiés vides, donc les moteurs cognitifs et sociaux
d'ECHOS tournent sur des données dégénérées ; `Flee` est inatteignable ; la
mémoire, les livres, la perception et la mort sont décoratifs. Voir
[`CONTRACT_REFERENCE.md`](docs/CONTRACT_REFERENCE.md).