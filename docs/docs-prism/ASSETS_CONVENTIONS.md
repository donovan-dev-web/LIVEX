# ASSETS_CONVENTIONS — Présentation des éléments

**Composant** : PRISM
**Dernière mise à jour** : 27 septembre 2026
**Dépend de** : [`SCENE_SPEC.md`](SCENE_SPEC.md)

---

## 1. Périmètre

`PrismLdk` ne fournit ni bibliothèque de contenu ni représentation visuelle.
Les conventions ci-dessous sont des repères pour le projet Unreal PRISM, qui
choisit et maintient ses propres meshes, matériaux, animations et assets.

## 2. Correspondance visuelle suggérée

| Élément logique | Représentation possible |
| :-- | :-- |
| Monde / cellules | Sol ou grille lisible, cohérent avec `WorldDescription` |
| Agent | Forme ou modèle distinguable, repéré par ID SYNE |
| Ressource initiale | Marqueur typé, si le produit décide de les afficher |
| Obstacle | Forme adaptée à sa position et son rayon fournis |
| Événement transitoire | Effet d'interface ou visuel ponctuel |

Ces exemples n'imposent pas de primitives, d'échelle, de nommage Unreal ou
d'assets procéduraux.

## 3. Consistance visuelle

- Maintenir une distinction cohérente entre types de ressources, groupes,
  états et sélection.
- Préserver la lisibilité en cas de grand nombre d'entités ; PRISM
  choisit les techniques de niveau de détail, regroupement et culling.
- Ne pas présenter comme données simulées des effets purement décoratifs ou
  des valeurs non fournies par SYNE.
- Garder les IDs SYNE comme clés d'association des représentations réutilisées
  entre mises à jour.
