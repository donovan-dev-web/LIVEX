# ADR — index de l'installateur

**Composant** : LIVEX (Installateur)
**Statut** : index à jour
**Dernière mise à jour** : 8 octobre 2026

Ce dossier contient les ADR spécifiques à l'installateur. Le modèle est celui de
[`../../docs-launcher/adr/0000-template.md`](../../docs-launcher/adr/0000-template.md),
identique aux autres composants. Les ADR sont numérotés à partir de 1.

## ADR locaux

| ADR | Sujet | Statut |
| :-- | :-- | :-- |
| `ADR-001-installateur-avalonia-coeur-sans-interface.md` | Installateur Avalonia séparé du Launcher, cœur sans interface, mode non interactif | [Proposed] |
| `ADR-002-installation-par-composant-sans-elevation.md` | Installation par composant, sans élévation, workspace séparé | [Proposed] |
| `ADR-003-dependances-embarquees.md` | Runtimes embarqués plutôt que dépendances système | [Proposed] |
| `ADR-004-catalogue-et-mise-a-jour-atomique.md` | Catalogue signé, mise à jour manuelle atomique par composant | [Proposed] |

## ADR référencés

| ADR | Sujet | Lien |
| :-- | :-- | :-- |
| ADR-001 (Launcher) | .NET et Avalonia | [`../../docs-launcher/adr/ADR-001-stack-dotnet-avalonia.md`](../../docs-launcher/adr/ADR-001-stack-dotnet-avalonia.md) |
| ADR-006 (Launcher) | PRISM verrouillé en attente | [`../../docs-launcher/adr/ADR-006-prism-verrouille-en-attente.md`](../../docs-launcher/adr/ADR-006-prism-verrouille-en-attente.md) |
| ADR-007 (Launcher) | Consoles natives, ECHOS sans interface | [`../../docs-launcher/adr/ADR-007-consoles-et-fenetre-analyse-natives.md`](../../docs-launcher/adr/ADR-007-consoles-et-fenetre-analyse-natives.md) |

---

## Mises à jour

| Date | Changement | Motif |
| :-- | :-- | :-- |
| 8 octobre 2026 | Création de l'index | Ouverture du dossier de l'installateur |
