# PRISM — Perceptual Rendering & Interactive Simulation Module

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
godot --path echos-ui  # remplacé par le projet Godot PRISM (godot-renderer/)
# Le partenaire SYNE se lance en --headless
dotnet run --project simulation-core/Simulation.Console -- --headless
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