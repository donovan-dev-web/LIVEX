# PRISM — Perceptual Rendering & Interactive Simulation Module

[![Statut: STABLE](https://img.shields.io/badge/Statut-STABLE-00d4a0.svg)](README.md)
[![Moteur: Godot 4.7.2](https://img.shields.io/badge/Moteur-Godot%204.7.2-1f7f6f.svg)](ARCHITECTURE.md)
[![100% procédural](https://img.shields.io/badge/Assets-100%25%20proc%C3%A9dural-1f7f6f.svg)](ASSETS_CONVENTIONS.md)

**Composant** : PRISM
**Statut** : [STABLE]
**Dernière mise à jour** : 17 septembre 2026
**Dépend de** : la documentation transversale (../)
**Source Monographie** : Partie 5

---

## Rôle

Couche qui rend le monde **perceptible et interactif** : il représente graphiquement l'état fourni par SYNE et fournit navigation, caméra, inspection et interaction. **Reflet du monde simulé, jamais co-auteur.**

## Lancement seul

```console
# Projet Godot (édition .NET), matériel C#
godot --path godot-renderer  # dossier PRISM (monorepo : prism/)
# Le partenaire SYNE se lance en --headless
dotnet run --project simulation-core/Simulation.Console -- --headless  # (syne/)
```

## Dépendances et ports

- **Godot 4.7.2 édition .NET** (C#), [HÉRITÉ] — moteur définitif ouvert.
- Se connecte à **SYNE** : WebSocket :5180 (données), HTTP :5181 (contrôle relayé).
- Interface d'analyse **intégrée à ECHOS** :5000.

## Documentation du composant

| Document | Rôle |
| :-- | :-- |
| `VISION.md` | Rôle, frontières, principe invariant |
| `ARCHITECTURE.md` | Choix Godot, scènes, mapping, transport |
| `SCENE_SPEC.md` | Structure des scènes, meshes |
| `TRANSPORT_API.md` | WebSocket (données) + HTTP (contrôle) |
| `RENDERING_SPEC.md` | Rendu des entités et ressources, code couleur |
| `VISUALIZATION_SPEC.md` | Croyances, social, groupes, communication |
| `UX_INTERACTION.md` | Caméra, HUD, interface d'analyse |
| `ASSETS_CONVENTIONS.md` | Primitives procédurales, nommage |
| `TESTING.md` | Tests unitaires, intégration transport, visuels |
| `ROADMAP.md` | Roadmap PRISM, évolution future |
| `CHANGELOG.md` | Versions |
| `adr/` | ADR locaux + références transverses |