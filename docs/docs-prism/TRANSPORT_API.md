# TRANSPORT_API.md

**Composant** : PRISM
**Statut** : [STABLE]
**Dernière mise à jour** : 17 septembre 2026
**Dépend de** : `../COMMUNICATION.md`, `../docs-syne/API_CONTRACTS.md`, `../adr/ADR-003-api-http-rest.md`, `../adr/ADR-004-websocket-temps-reel.md`
**Source Monographie** : §5.4

---

## 1. WebSocket (données) — :5180

PRISM se connecte à SYNE via WebSocket (`ws://127.0.0.1:5180/`) avec **reconnexion automatique (1,5 s)**. Deux types de messages (contract partagé avec ECHOS, cf. `../docs-syne/API_CONTRACTS.md`) :

- **`snapshot`** : état complet du monde (`WorldSnapshot`).
- **`event`** : événements ponctuels (`ExternalEvent`).

## 2. HTTP (contrôle) — :5181

PRISM **relaie** les commandes de contrôle à l'API REST de SYNE (`http://127.0.0.1:5181/api/control/`) :

| Commande | Action |
| :-- | :-- |
| `start` | Démarrer la simulation |
| `pause` | Mettre en pause |
| `resume` | Reprendre |
| `reset` | Réinitialiser (avec seed et run id) |

L'état de SYNE est interrogé toutes les **2 secondes** (polling léger).

> ⚠ PRISM relaie, il ne décide pas : tout contrôle passe par l'API HTTP de SYNE (principe invariant, cf. `VISION.md`). Ces endpoints sont documentés par les ADR transverses (ADR-003, ADR-004).

## 3. L'interface TypeScript (intégrée à ECHOS)

Dans le prototype, l'interface ECHOS (application web React + TypeScript) consommait les métriques d'ECHOS **et** le flux WebSocket de SYNE, offrant un tableau de bord complémentaire à PRISM.

En **V0.1**, cette interface est **intégrée à ECHOS** (web local React/Vite servie par FastAPI — pas de shell Electron) et sert aussi de complément d'affichage pour PRISM (liste des interfaces TypeScript : ses composants — voir `UX_INTERACTION.md`).

## 4. Alignement des contrats

- Les schémas `WorldSnapshot`/`ExternalEvent` sont définis une fois dans `../docs-syne/API_CONTRACTS.md` (source de vérité transport).
- `TRANSPORT_API.md` (PRISM) et `API_REST.md` (ECHOS) déclinent chacun leur face : PRISM consomme données WS + envoie commandes HTTP ; ECHOS observe WS + interroge son API REST :5000.
- Versionnage compatible : voir `../VERSIONING.md` et `../COMMUNICATION.md`.

---

## Points restés ouverts dans ce document
- Aucun : les contrats sont partagés et référencés. Le polling 2 s de l'état SYNE (prototype) pourrait être remplacé par le `snapshot` temps réel en V0.1 — décision d'implémentation.