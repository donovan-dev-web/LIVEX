# syne-mock

Mini-serveur Node.js déterministe pour intégrer Unreal au contrat SYNE. `npm install`
puis `npm start -- config.example.json`. Le WebSocket texte JSON camelCase est sur
`ws://127.0.0.1:5180/`; le contrôle HTTP est sur `http://127.0.0.1:5181`.

Le terminal affiche le suivi avec le préfixe `[SYNE-MOCK ...]` : démarrage des
ports, connexions/déconnexions WebSocket, requêtes/réponses HTTP et transitions
`PREPARE`, `READY`, `START`, `PAUSE`, `RESUME`, `STOP` et `RESET`.

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

Tests ciblés : `npm test` (12 tests, moins d'une seconde, sans réseau ni port fixe).
La suite s'exécute aussi en intégration continue via le job `Tests (SYNE-MOCK Node)`
de `.github/workflows/ci.yml`, déclenché sur toute modification de `syne-mock/`.
Ports et paramètres sont configurables dans JSON.
`replay.file` permet de rejouer un fichier JSONL (un message SYNE par ligne);
`replay.loop` le répète indéfiniment. Le replay est volontairement un transport
de messages et ne prétend pas restaurer l'état interne C#.