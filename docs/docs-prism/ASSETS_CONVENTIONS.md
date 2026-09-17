# ASSETS_CONVENTIONS.md

**Composant** : PRISM
**Statut** : [STABLE]
**Dernière mise à jour** : 17 septembre 2026
**Dépend de** : `SCENE_SPEC.md`
**Source Monographie** : §5.3.4, §5.5.2

---

## 1. Principe

Le prototype est **100 % procédural** : aucun asset externe. Les primitives sont générées au runtime et paramétrées par code (couleurs, tailles).

## 2. Correspondance élément ↔ mesh

| Élément | Mesh |
| :-- | :-- |
| Sol | PlaneMesh |
| Entité | CapsuleMesh |
| Ressource | SphereMesh |
| Obstacle | BoxMesh / CylinderMesh |

## 3. Code couleur des entités

| Condition | Couleur |
| :-- | :-- |
| Énergie basse (< 20) | Rouge |
| Faim élevée (> 80) | Orange |
| En bonne santé | Vert |
| Dans un groupe | Couleur du groupe (hash HSV) |
| Sélectionné | Modulation blanche |
| Non sélectionné | Modulation grise |

## 4. Code couleur des ressources

| Type | Couleur |
| :-- | :-- |
| Nourriture | Vert |
| Eau | Bleu |

Taille normalisée par `quantité / capacité`.

## 5. Couleur des groupes

```text
hue = groupId.GetHashCode() % 360
saturation = 0.8
value = 1.0
```

- Leaders : éclaircis (Lightened 0.3).
- Membres : couleur normale.

## 6. Couleur des pulsations de communication

| Type de pulsation | Couleur rendue |
| :-- | :-- |
| Warning | Rouge |
| Information | Blanc |
| Request | Jaune |
| Trading | Vert |
| Autre | Gris |

## 7. Convention de nommage des nœuds

- Préfixes typés : `Agent_<id>`, `Resource_<type>_<id>`, `Obstacle_<id>`, `Pulse_<messageId>`.
- Organisation par `PackedScene` réutilisable (capsule-entité, sphère-ressource, cube-obstacle).

---

## Points restés ouverts dans ce document
- Aucun. Les conventions proviennent de la Monographie §5.3.4 / §5.5.2.