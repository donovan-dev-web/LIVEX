# RENDERING_SPEC.md

**Composant** : PRISM
**Statut** : [STABLE]
**Dernière mise à jour** : 17 septembre 2026
**Dépend de** : `SCENE_SPEC.md`
**Source Monographie** : §5.5, §5.6

---

## 1. Rendu des entités

Chaque entité est représentée par une **capsule 3D** avec :

- **Couleur** : par santé (vert → rouge) ou par action (touche A pour basculer).
- **Interpolation** : la position est interpolée entre deux snapshots pour un mouvement fluide.
- **Animation de mort** : fondu progressif.
- **Indicateur de cap** : ligne depuis la position jusqu'à `position + heading × 15`.

## 2. Code couleur des entités

| Condition | Couleur |
| :-- | :-- |
| Énergie basse (< 20) | Rouge |
| Faim élevée (> 80) | Orange |
| En bonne santé | Vert |
| Dans un groupe | Couleur du groupe (hash HSV) |
| Sélectionné | Modulation blanche |
| Non sélectionné | Modulation grise |

## 3. La sélection

- La sélection se fait par **clic** (raycast physique).
- Un `CircleShape2D` de rayon **5** sert de zone de détection.
- L'entité sélectionnée ouvre le panneau `BeliefViewer` (cf. `VISUALIZATION_SPEC.md`).

## 4. Rendu des ressources

Les ressources sont représentées par des **sphères** :

| Type | Couleur |
| :-- | :-- |
| Nourriture | Vert |
| Eau | Bleu |

- **Taille normalisée** par la proportion `quantité / capacité`.
- Des **taches translucides** sur le sol indiquent les zones de ressource.

## 5. Limites connues du rendu (Monographie §5.14)

1. **V1** : obstacles et taille du monde non transmis par le contrat (sol fixe 500×500, obstacles ignorés) — corrigé en V2.
2. Pas de sons.
3. Pas d'animations squelettiques (capsules simples).
4. Pas de minimap (navigation à grande échelle par zoom).
5. Pas de rendu LOD des entités distantes (V1).
6. **Complexité** : 1000 entités = 1000 capsules à rendre (culling nécessaire au-delà).

---

## Points restés ouverts dans ce document
- V0.1 : périmètre de correction des limites (taille du monde transmise, minimap, LOD) à prioriser dans `ROADMAP.md` PRISM.