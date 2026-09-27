# ADR — index du composant PRISM

**Composant** : PRISM
**Statut** : index à jour
**Dernière mise à jour** : 27 septembre 2026

Ce dossier contient les ADR spécifiques à PRISM.

## ADR locaux

| ADR | Sujet | Statut |
| :-- | :-- | :-- |
| `ADR-002-choix-unreal-prism-ldk.md` | Unreal Engine 5.8 et plugin PRISM-LDK pour PRISM | [Accepted] |
| `ADR-001-choix-godot.md` | Choix historique du prototype Godot (édition .NET) | [Superseded] par l'ADR-002 |

## ADR transverses référencés (docs/adr/)

| ADR | Sujet | Lien |
| :-- | :-- | :-- |
| ADR-003 | API HTTP REST légère (contrôle SYNE :5181) | [`../../adr/ADR-003-api-http-rest.md`](../../adr/ADR-003-api-http-rest.md) |
| ADR-004 | WebSocket temps réel (données SYNE :5180) | [`../../adr/ADR-004-websocket-temps-reel.md`](../../adr/ADR-004-websocket-temps-reel.md) |

Références transverses : `TRANSPORT_API.md` et `../../COMMUNICATION.md`.

---

## Mises à jour

| Date | Changement | Motif |
| :-- | :-- | :-- |
| 27 septembre 2026 | ADR-002 (Unreal + plugin PRISM-LDK) ; ADR-001 marquée supersédée | Trancher le choix du moteur définitif |
| 17 septembre 2026 | Création | — |
