# ADR — index du composant Launcher

**Composant** : LIVEX (Launcher)
**Statut** : index à jour
**Dernière mise à jour** : 30 septembre 2026

Ce dossier contient les ADR spécifiques au Launcher.

## ADR locaux

| ADR | Sujet | Statut |
| :-- | :-- | :-- |
| `ADR-001-stack-dotnet-avalonia.md` | Pile technique .NET et Avalonia du Launcher | [Proposed] |
| `ADR-002-modes-analyse-et-immersion-de-poids-egal.md` | Deux modes d'utilisation de poids égal, mode Contrôle comme socle | [Accepted] |
| `ADR-003-analyse-propriete-de-echos.md` | ECHOS est seul propriétaire de l'analyse scientifique | [Accepted] |
| `ADR-004-format-de-paquet-livexp.md` | Format unique `.livexp`, vivant puis scellé | [Accepted] |
| `ADR-005-execution-sequentielle-mono-espace.md` | Exécution séquentielle et mono-espace en V0.1 | [Accepted] |
| `ADR-006-prism-verrouille-en-attente.md` | PRISM conçu au niveau du modèle, accès verrouillé | [Accepted] |

## ADR transverses référencés

| ADR | Sujet | Lien |
| :-- | :-- | :-- |
| ADR-002 | Unreal Engine et plugin PRISM-LDK pour PRISM | [`../../docs-prism/adr/ADR-002-choix-unreal-prism-ldk.md`](../../docs-prism/adr/ADR-002-choix-unreal-prism-ldk.md) |
| ADR-003 | API HTTP REST légère (contrôle SYNE :5181) | [`../../adr/ADR-003-api-http-rest.md`](../../adr/ADR-003-api-http-rest.md) |
| ADR-004 | WebSocket temps réel (données SYNE :5180) | [`../../adr/ADR-004-websocket-temps-reel.md`](../../adr/ADR-004-websocket-temps-reel.md) |

Références transverses : [`../../../COMMUNICATION.md`](../../../COMMUNICATION.md) et
[`../../../VERSIONING.md`](../../../VERSIONING.md).

## Modèle

Le modèle d'ADR est celui de [`0000-template.md`](0000-template.md), identique au
modèle des autres composants. Les ADR sont **numérotés à partir de 1** ; le numéro
`0000` est réservé au gabarit et n'est jamais utilisé pour une décision.

---

## Mises à jour

| Date | Changement | Motif |
| :-- | :-- | :-- |
| 30 septembre 2026 | Création des ADR-001 à ADR-006 | Consigner les décisions structurantes du Launcher |
