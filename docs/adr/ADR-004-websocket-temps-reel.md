# ADR-004 : WebSocket en temps réel

**Composant** : LIVEX (transverse — transport SYNE→ECHOS/PRISM)
**Statut** : [Accepted]
**Dernière mise à jour** : 17 septembre 2026
**Dépend de** : —
**Source Monographie** : Annexe F.5 (ADR-004), §5.4.1

---

## Contexte

Le moteur doit émettre des événements en temps réel vers des consommateurs externes (analyseur, renderer, interface ECHOS) sans polling HTTP coûteux.

## Décision

Utiliser un **WebSocket** sur le port **5180** avec :

- **Messages binaires JSON** notifiés par le moteur ;
- **Événements typés** : `tick_summary`, `agent_spawned`, `agent_died`, `decision_made` (nomenclature étendue en V0.1) ;
- Deux formats de messages : `snapshot` (WorldSnapshot) et `event` (ExternalEvent) ;
- **Un seul consommateur à la fois par défaut** (single-consumer).

Naturellement supporté par les clients cibles (Godot, Python, JS — côté ECHOS : brut WebSocket).

## Conséquences

### Positives
- Le renderer peut consommer les snapshots et les transitions d'état.
- L'analyseur peut construire des métriques en temps réel.
- Pas de polling — diffusion événementielle.

### Négatives
- Single-consumer par défaut : une seule interface (ECHOS ou PRISM) se connecte ; multi-consommation à trancher si besoin.

### Risques
- Perte de messages si le consommateur est en retard (pas de file d'attente illimitée).

## Alternatives considérées

- **Polling HTTP** : coûteux, rejeté (cela était l'objet même de la question).
- **TCP custom** : non interopérable avec les clients cibles.

## Validation / rejet

- Réouverture si le besoin de multi-consommateurs simultanés émerge (décision V0.1 : router les flux vers une seule interface à la fois).

---

## Mises à jour

| Date | Changement | Motif |
| :-- | :-- | :-- |
| 17 septembre 2026 | Création | — |