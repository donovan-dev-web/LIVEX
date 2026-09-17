# VISUALIZATION_SPEC.md

**Composant** : PRISM
**Statut** : [STABLE]
**Dernière mise à jour** : 17 septembre 2026
**Dépend de** : `RENDERING_SPEC.md`
**Source Monographie** : §5.7, §5.8, §5.9, §5.10

---

## 1. Visualisation des croyances

### 1.1 BeliefViewer

L'inspection d'une entité sélectionnée ouvre un panneau listant ses croyances, **triées par confiance décroissante**. Chaque croyance affiche :

- Le **fait** (subject, predicate, value).
- La **confiance** (avec icône : haute / moyenne / basse).
- La **source** (perception, mémoire, communication, inférence).
- L'**âge** (en ticks).

### 1.2 Heatmap de croyance (spatiale)

- Grille **50×50** (cellule = 10 unités) cartographiant les croyances, en projetant la confiance sur la position spatiale.
- Couleur : HSV teinte 120-0 (vert = confiance haute, rouge = confiance basse).

### 1.3 Bulle de croyance

- Au-dessus de chaque entité : affichage de la **croyance dominante** (confiance > 0.8), avec **disparition automatique après 2 secondes**.

## 2. Visualisation sociale

### 2.1 Graphe de relations

Relations affichées sous forme de **lignes** si confiance ≥ 0.3 :

| Confiance | Couleur de la ligne |
| :-- | :-- |
| > 0.7 | Vert |
| 0.4 – 0.7 | Orange |
| < 0.4 | Rouge (non affichée par défaut) |

- Épaisseur de ligne : `trust × 3`.
- Des **flèches** indiquent le sens de la relation.

### 2.2 Heatmap de confiance

- Heatmap **256×256** (un pixel par paire d'entités) visualisant la matrice de confiance globale.
- Couleur : HSV teinte `120 - trust × 120` (rouge = méfiance, vert = confiance).

### 2.3 Graphe social (interface ECHOS)

L'interface ECHOS complète PRISM avec un graphe **D3 force-directed** :

- **Force links** : distance 80.
- **ForceManyBody** : strength −300.
- **ForceCenter** : centrage.
- Couleur des arêtes par niveau de confiance.
- Nœuds : rayon 8 px, couleur par groupe.
- **Drag interactif**.

## 3. Visualisation des groupes

### 3.1 Couleur par groupe

Chaque groupe reçoit une couleur dérivée de son hash :

```text
hue = groupId.GetHashCode() % 360
saturation = 0.8
value = 1.0
```

- **Leaders éclaircis** (Lightened 0.3).
- **Membres** : couleur normale du groupe.

### 3.2 Panneau de groupe (GroupPanel)

- Nom du groupe.
- Nombre de membres.
- Couleur de fond issue de la couleur du groupe (**assombrie 50 %**).
- Rôles des membres (vue détaillée).

### 3.3 Vue groupes complémentaire (interface ECHOS)

`GroupList`, `GroupDetail`, `MembershipTree` (formation → dissolution, timeline).

## 4. Communication visuelle

Les **pulsations lumineuses** entre entités sont rendues comme des **éclairs transients** (durée de vie 0,5 s), colorés selon leur signification :

| Type de pulsation | Couleur rendue |
| :-- | :-- |
| Warning | Rouge |
| Information | Blanc |
| Request | Jaune |
| Trading | Vert |
| Autre | Gris |

Le rendu utilise une **file de pulsations avec minuterie delta-time**.

---

## Points restés ouverts dans ce document
- Aucun : les spécifications proviennent de la Monographie §5.7–5.10.