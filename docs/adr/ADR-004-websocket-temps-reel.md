# ADR-004 : WebSocket en temps réel

**Composant** : LIVEX (transverse — transport SYNE→ECHOS/PRISM)
**Statut** : [Accepted — décision de transport ; détails de l'implémentation mis à jour dans les contrats courants]
**Dernière mise à jour** : 27 septembre 2026
**Dépend de** : —
**Source Monographie** : Annexe F.5 (ADR-004), §5.4.1

---

## Contexte

Le moteur doit émettre des événements en temps réel vers des consommateurs externes (analyseur, renderer, interface ECHOS) sans polling HTTP coûteux.

## Décision

Utiliser un **WebSocket** sur le port **5180** avec :

- **Messages texte UTF-8 contenant du JSON** ;
- **Événements typés** : `tick_summary`, `agent_spawned`, `agent_died`, `decision_made` (nomenclature étendue en V0.1) ;
- Deux formats de messages : `snapshot` (WorldSnapshot) et `event` (ExternalEvent) ;
- Diffusion aux consommateurs connectés (multi-consommateur).

Les consommateurs actuels comprennent ECHOS (client Python) et PRISM
(plugin Unreal `PrismLdk`). Ce protocole reste indépendant du framework client.

## Conséquences

### Positives
- Le renderer peut consommer les snapshots et les transitions d'état.
- L'analyseur peut construire des métriques en temps réel.
- Pas de polling — diffusion événementielle.

### Négatives
- Le serveur diffuse vers les clients connectés ; chaque client doit gérer les reconnexions et son propre état de réception.

### Risques
- Perte de messages si le consommateur est en retard (pas de file d'attente illimitée).

## Alternatives considérées

- **Polling HTTP** : coûteux, rejeté (cela était l'objet même de la question).
- **TCP custom** : non interopérable avec les clients et outils cibles.

## Validation / rejet

- Les formats actuels `world_initialized`, `snapshot` et `event` sont détaillés dans `docs/docs-syne/API_CONTRACTS.md` ; cette ADR n'est pas la source de vérité des schémas.

---

## Mises à jour

| Date | Changement | Motif |
| :-- | :-- | :-- |
| 17 septembre 2026 | Création | — |