# ADR-011 : Géométrie 2.5D par colonnes et navigation de surface

**Composant** : PRISM
**Statut** : [Accepted]
**Dernière mise à jour** : 2 novembre 2026
**Dépend de** : ADR-004, ADR-006, ADR-007
**Source Monographie** : —

---

## Contexte

Le monde 3D voxel complet augmente le coût de la navigation, de la perception,
du stockage et du rendu. Le besoin scientifique porte sur les ressources,
la production, la logistique et les structures, pas sur les cavernes ou les
surplombs. Ces éléments peuvent être représentés sans simuler un volume 3D
dense.

## Décision

Le propriétaire du projet retient le modèle **2.5D dès le début de
l'implémentation**. L'espace navigable est une grille horizontale bornée de
colonnes ; chaque colonne possède une altitude de surface et un profil de
couches géologiques déterministe. Les altitudes restent des entiers en blocs.

- Les régions sont des tuiles horizontales de 16 × 16 colonnes ; leur
identifiant est `regionId = cx + regionCountX * cy`. Il n'existe pas de
partition verticale en chunks.
- Le terrain de base est représenté par des tableaux SoA de colonnes
(`surfaceZ`, matériau de surface, état, profil de strates). Les couches
souterraines sont résolues par le profil de contenu et la profondeur relative ;
les extractions et poses produisent des surcharges clairsemées triées par
coordonnée dans les instantanés et empreintes.
- Les arbres, objets, agents et structures utilisent des coordonnées
`(x, y, z)` entières. Les gabarits peuvent occuper plusieurs blocs en hauteur,
mais aucune colonne n'a de surplomb ni de cavité.
- La navigation s'effectue sur les colonnes accessibles ; les transitions
autorisent une montée d'un bloc et une descente de trois blocs. L'eau demeure
infranchissable. Le pathfinding est un A* horizontal à coûts entiers, pondéré
par les changements d'altitude.
- La génération emploie du bruit entier 2D pour l'altitude et les zones, puis
des profils de strates et des surcharges déterministes pour les filons.
- Le contrat conserve les coordonnées entières et les identifiants de régions,
mais ne promet aucune topologie 3D. Toute modification du format de monde
incrémente `worldGenVersion` avant le gel des mondes dorés.

Dimensions initiales : 256 × 256 colonnes, avec une altitude comprise dans
0…63. Les champs définis (`surfaceZ` : 1 octet, matériau : 2, état : 1,
profil de strates : 2) utilisent 6 octets par colonne, soit environ 384 Kio
pour une carte dense, hors surcharge et tables de contenu, au lieu de 12,6 Mo
de blocs 3D denses. Les régions encore uniformes peuvent rester compactes.

## Conséquences

### Positives
- Le coût de navigation, de perception de terrain et de génération dépend
  d'abord de la surface 2D.
- Les dimensions logiques du monde restent familières et le contenu demeure
  extensible par données.
- Les empreintes et sauvegardes portent des données de surface compactes et
  des modifications clairsemées.

### Négatives
- Les grottes, les surplombs, le minage en galeries et les tunnels ne sont pas
  représentables.
- L'accès aux minerais souterrains est limité aux couches exposées ou à la
  progression verticale de la surface ; les recettes et gabarits doivent rester
  compatibles avec cette topologie.
- Les outils de rendu, de génération et de navigation doivent traiter
  explicitement les transitions d'altitude.

### Risques
- Une représentation clairsemée mal canonisée rendrait les empreintes
  dépendantes de l'ordre d'insertion. Toute sérialisation et tout hachage trient
  les coordonnées en ordre `(z, y, x)`.

## Alternatives considérées

- **Volume dense 3D** : écarté pour réduire le coût structurel avant
  l'implémentation et parce que la topologie volumique n'est pas nécessaire aux
  questions V2.
- **Monde strictement 2D sans altitude** : écarté, car il ne permettrait pas
  les falaises, pentes, ponts et bâtiments de plusieurs blocs.

## Validation / rejet

Décision de géométrie acceptée le 2 novembre 2026. La porte G1 vérifie les
budgets de navigation, perception, génération et rendu sur ce modèle ; G2
valide la jouabilité et les empreintes. Réouverture uniquement si les
expériences V2 nécessitent une topologie de cavernes ou si les critères de
viabilité échouent à cause de cette limitation.

---

## Mises à jour

| Date | Changement | Motif |
| :-- | :-- | :-- |
| 2 novembre 2026 | Décision 2.5D acceptée | Optimiser la géométrie dès le départ |
