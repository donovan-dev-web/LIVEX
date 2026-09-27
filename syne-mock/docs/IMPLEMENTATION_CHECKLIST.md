# Checklist plugin Unreal SYNE

## Compatibilité et tests

- [ ] Tester connexion/déconnexion et plusieurs clients WebSocket.
- [ ] Vérifier trames texte UTF-8 et rejet d'une trame JSON invalide.
- [ ] Vérifier `snapshot.tick` monotone et `runId` stable pendant un run.
- [ ] Vérifier le contenu du snapshot : 50 agents par défaut et quatre ressources.
- [ ] Vérifier l'ordre `decision_made` → `action_completed` → `snapshot` →
      `tick_summary` sur un tick.
- [ ] Vérifier `message_sent`/`message_received` quand la portée le permet.
- [ ] Vérifier `group_decision` selon `lodInterval` et `world.book_written` au
      tick 100 quand les options sont activées.
- [ ] Vérifier `Pause` ne fait pas avancer le tick, puis `Resume`.
- [ ] Vérifier `Stop`, `Reset`, fin `maxTicks` et état `finished`.
- [ ] Vérifier HTTP 400 (`invalid_json`), 404 (`not_found`) et 409
      (`run_active`).
- [ ] Rejouer un JSONL et vérifier que le plugin tolère des types arbitraires.
- [ ] Comparer deux runs de même seed sans exiger l'équivalence avec le moteur C#.
- [ ] Tester versions `0.1.x` inconnues, champs ajoutés et valeurs enum inconnues.
- [ ] Tester fermeture réseau, timeout, backoff et absence de double `Start`.

## Contrat de livraison

- [ ] URL, ports, TLS, délais et politique de reconnexion configurables.
- [ ] Aucun accès `UObject` depuis le thread WebSocket.
- [ ] Toutes les notifications Blueprint sur le game thread.
- [ ] File bornée ou stratégie de backpressure documentée.
- [ ] Logs sans secrets ni payloads sensibles en production.
- [ ] `version`, `engineVersion`, `runId` et seed visibles dans les diagnostics.
- [ ] Mapping JSON camelCase documenté pour chaque `USTRUCT`.
- [ ] `Unknown` présent dans les enums et événement générique conservé.
- [ ] Tests automatisés exécutables avec le mock (`npm test` côté mock).
- [ ] Tests Unreal Automation/Functional couvrant les scénarios ci-dessus.

## Limites à rappeler dans la documentation du plugin

- Le mock ne reproduit pas bit-à-bit le moteur C#.
- Les événements et champs futurs peuvent être absents ou additifs.
- Le replay transporte des messages et ne restaure pas l'état interne.
- La reconnexion ne récupère pas les trames manquées.
- Le loopback par défaut n'est pas une authentification.
