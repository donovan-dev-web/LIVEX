# SCENE_SPEC.md

**Composant** : PRISM
**Statut** : [STABLE]
**Dernière mise à jour** : 17 septembre 2026
**Dépend de** : `ARCHITECTURE.md`
**Source Monographie** : §5.3

---

## 1. Objectif

Spécifie la structure des scènes Godot et l'organisation des assets. Les primitives sont **100 % procédurales** (aucun asset externe).

## 2. Structure des scènes (V1)

```text
res://
  main.tscn                        (scène racine)
  scripts/
    SimClient.cs                   (client WebSocket)
    CameraController.cs            (contrôle caméra)
    Hud.cs                         (interface HUD + contrôles)
```

| Nœud | Rôle |
| :-- | :-- |
| `main.tscn` | Scène racine, assemble monde + caméra + HUD |
| `SimClient.cs` | Connexion WebSocket à SYNE, réception snapshot/event, reconnexion 1,5 s |
| `CameraController.cs` | Orbite, zoom, déplacement ZQSD/WASD, suivre entité |
| `Hud.cs` | Overlay du HUD (tick, score, contrôle start/pause/resume/reset) |

## 3. Structure enrichie (V2)

La scène V2 enrichit la V1 avec (Monographie §5.3.2) :
- visualisation des croyances (heatmaps, bulles) ;
- visualisation sociale (graphe de relations, heatmap de confiance) ;
- visualisation des groupes (couleurs, GroupPanel) ;
- communication visuelle (pulsations lumineuses) ;
- panneau d'inspection (BeliefViewer).

## 4. Le mapping 2D → 3D

| Simulation (2D) | Godot (3D) |
| :-- | :-- |
| x | x |
| y | z |
| — | y (hauteur) |

Le sol est un `PlaneMesh` (plan XZ = défaut en Godot 4.7, vérifié via AABB — aucune rotation nécessaire).

## 5. Les meshes de base

| Élément | Mesh |
| :-- | :-- |
| Sol | `PlaneMesh` |
| Entité | `CapsuleMesh` |
| Ressource | `SphereMesh` |
| Obstacle | `BoxMesh` / `CylinderMesh` |

## 6. Organisation des scènes

- Une scène par type d'objet réutilisable (capsule-entité, sphère-ressource, cube-obstacle) via `PackedScene`.
- Positionnement des instances par conversion (`x → x`, `y → z`), hauteur constante au-dessus du sol.

---

## Points restés ouverts dans ce document
- Le rendu du monde à grande échelle doit intégrer du culling au-delà de ~1000 entités (§5.14.6) — optimisations PRISM V2.
- Structure/scènes détaillées V2 à affiner avec les maquettes (références Monographie §5.3.2).