# SCENE_SPEC — Monde présenté par PRISM

**Composant** : PRISM
**Dernière mise à jour** : 27 septembre 2026
**Dépend de** : [`ARCHITECTURE.md`](ARCHITECTURE.md), [`PRISM_UNREAL_IMPLEMENTATION.md`](PRISM_UNREAL_IMPLEMENTATION.md)

---

## 1. Périmètre

Ce document décrit les responsabilités de présentation du projet Unreal PRISM,
qui intègre le plugin PRISM-LDK (`PrismLdk`). Il ne spécifie pas une scène
livrée par le plugin : **PrismLdk expose les données Blueprint, mais ne génère ni tuiles,
acteurs, collisions, navigation, ni interface utilisateur**. Le projet PRISM
choisit la structure de ses niveaux et les objets qui représentent les données
SYNE.

## 2. Flux de construction du monde

À la réception de `OnWorldInitialized`, PRISM peut préparer sa
présentation avant de confirmer `Ready` à SYNE. Une organisation de haut
niveau peut comporter :

| Élément | Responsabilité de PRISM |
| :-- | :-- |
| Environnement | Représenter les dimensions et la topologie logique reçues |
| Obstacles | Représenter les obstacles initiaux et appliquer les mutations |
| Ressources | Présenter les marqueurs initiaux si le produit le souhaite |
| Agents | Créer/mettre à jour la présentation, indexée par ID SYNE |
| Interface | Exposer les contrôles, statuts et vues adaptés au produit |

Ces catégories sont des possibilités de conception, pas des objets générés ou
imposés par le plugin.

## 3. Repères et données

SYNE expose un monde logique 2D. Les coordonnées et conversions doivent être
interprétées selon le type des données :

- `Cells[].X/Y` et les positions des ressources initiales sont des indices de
  cellule.
- Les positions d'agents et d'obstacles sont des coordonnées continues SYNE.
- Les stocks `Resources[]` des snapshots sont des réserves globales par type,
  pas des quantités synchronisées pour des acteurs locaux.
- La projection 3D, les hauteurs, les collisions et la navigation appartiennent
  à la présentation Unreal ; elles ne deviennent pas pour autant l'état
  autoritaire de la simulation.

Le plugin et les contrats conservent les dimensions et `CellSize` fournis par
SYNE. Aucune échelle Unreal universelle n'est imposée par PRISM : le projet
final choisit sa convention de rendu et doit l'appliquer de façon cohérente.
Pour les interprétations, conversions et champs des structures exposées,
consulter [`PRISM_UNREAL_IMPLEMENTATION.md`](PRISM_UNREAL_IMPLEMENTATION.md).

## 4. État initial et mises à jour

1. Recevoir `OnWorldInitialized` et construire la représentation initiale.
2. N'appeler `Ready` qu'une fois le projet prêt à afficher le monde préparé.
3. Après `Start`, traiter les snapshots complets sur `OnSnapshot` comme état
   dynamique autoritaire.
4. Utiliser `OnWorldDelta` et `OnSyneEvent` pour les notifications et effets
   ponctuels ; éviter d'appliquer de nouveau les mutations déjà reflétées dans
   le snapshot.
5. Utiliser les identifiants SYNE pour faire correspondre les mises à jour aux
   acteurs visuels.

La liste exhaustive des messages et le cycle des commandes sont dans
[`TRANSPORT_API.md`](TRANSPORT_API.md).
