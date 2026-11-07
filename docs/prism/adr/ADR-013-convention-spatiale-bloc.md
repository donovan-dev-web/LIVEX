# ADR-013 : Convention spatiale unique — le bloc comme unité de monde

**Composant** : PRISM (et SYNE, par contrat partagé)
**Statut** : [Accepted]
**Dernière mise à jour** : 6 novembre 2026
**Dépend de** : ADR-004, ADR-006, ADR-011
**Remplace** : ADR-003 (échelle `k = 100 uu / unité`), le monde de 2 240 unités du profil SYNE `prism`

---

## Contexte

Deux échelles spatiales ont coexisté, héritées de la version Unreal Engine de
PRISM :

- **SYNE** décrivait son monde en « unités » (2 240 × 2 240 pour le profil
  `prism`, 70 × 70 cases de 32) calibrées sur le partitionnement de monde
  Unreal, avec un facteur `k = 100 uu par unité SYNE` (1 uu = 1 cm) ;
- **PRISM**, réécrit en noyau C# autonome, décrit un monde voxel de
  **blocs** (256 × 256 × 64, ADR-011) et publie les positions d'agents en
  **milli-blocs** (`int`, 1 000 par bloc).

Conséquences mesurées en P9 : la même grandeur était exprimée dans trois
systèmes (uu, unité SYNE, bloc PRISM). Le synchroniseur couplé lisait les
milli-blocs comme s'il s'agissait d'unités SYNE, si bien qu'une position de
`(128 500, 128 500)` milli-blocs était rognée à `(2 240, 2 240)` — le coin du
monde — par `Position.Clamp`. Aucune donnée ne reliait 2 240 unités à
256 colonnes : le facteur implicite (8,75) n'existait nulle part.

Un facteur d'échelle n'a plus d'objet : il servait à convertir des unités
Unreal (uu) vers une unité de jeu, or les deux composants sont désormais des
noyaux .NET qui échangent des données brutes.

## Décision

Le propriétaire du projet retient **une seule unité de monde : le bloc**.

- `1 bloc = 1 mètre = 1 unité SYNE`. `world.metersPerUnit` vaut donc `1.0` et
  n'est plus qu'une redondance documentaire.
- **PRISM est autoritaire sur la géométrie** : la grille du monde (largeur,
  hauteur, altitude maximale) vient de son contenu (`world.size`, ADR-006), et
  SYNE **adopte** ces dimensions. Le mode couplé les **lit** dans l'instantané
  de monde de PRISM (aucune constante de monde côté SYNE) — le profil `prism`
  qui les déclarait est retiré depuis le 6 novembre 2026, la géométrie venant
  de PRISM et non plus d'un fichier de configuration.
- **Aucune échelle de monde** : ni `k`, ni uu, ni facteur de conversion
  position SYNE ↔ case PRISM. Un déplacement SYNE de 1 unité par tick et un
  agent PRISM à `moveBudgetPerTick = 1 000` milli-blocs parcourent la même
  distance physique (1 bloc/tick) — c'est la vérification de cohérence de la
  convention.
- **Le milli-bloc reste un encodage interne**, pas une unité de monde : c'est
  la virgule fixe de PRISM pour sa précision infra-bloc (`1 000` milli-blocs =
  1 bloc, centre de case = `x * 1 000 + 500`). La seule arithmétique admise au
  bord du contrat est cette échelle fixe, isolée dans une constante nommée
  (`WorldSpace.MilliBlocksPerBlock`), divisée une fois à la lecture.
- **Encodage des messages** : ce que le contrat publie reste inchangé (ADR-008,
  ADR-012) — les commandes (`Incarner`, `Aller`, …) portent des coordonnées de
  **bloc** entières, les observations (`self.position`) des **milli-blocs**.
  Les deux représentations désignent la même unité de monde.
- **Les positions SYNE restent continues** en unités de bloc (double) : le
  centre d'une case PRISM (`128.5`) est représenté exactement, sans arrondi ni
  perte, et la cognition conserve des distances métriques réelles.

Les valeurs calibrées sur l'ancienne échelle (monde 2 240, rayon de perception
50, portée des gisements 200) sont ajustées au monde de 256 blocs **en
conservant les ratios du profil de référence** (monde 500) : rayon 25, portée
100, cellule spatiale 25. Le profil de référence SYNE (500 × 500) n'est pas
touché : sa trajectoire reste bit-à-bit identique.

## Conséquences

### Positives
- Une seule grandeur à lire, écrire, journaliser et comparer d'un composant à
  l'autre ; les journaux PRISM et SYNE se comparent directement.
- La classe de bug « coordonnée rognée au coin du monde » disparaît : une
  position hors bornes signale désormais une vraie erreur de contrat.
- Les distances physiques (perception, portée, pas de déplacement) redeviennent
  interprétables en mètres, donc calibrâbles.
- La géométrie du monde n'est plus dupliquée : changer la taille d'un pack
  PRISM suffit, SYNE suit.

### Négatives
- Rupture pour les clients de l'ancienne échelle : le monde passe de 2 240 à
  celle du pack PRISM (256 unités pour le pack minimal), la description du
  monde de 70 × 70 cases de 32 à 16 × 16 cases de 16. Les harnais qui figeaient
  2 240 sont mis à jour. Le profil `prism` qui portait ces valeurs est retiré :
  elles sont désormais lues dans l'instantané de PRISM.
- Les valeurs calibrées de SYNE appliquées à un monde de 256 blocs doivent être
  assumées comme des ratios (pas des mètres absolus) tant qu'elles n'ont pas été
  recalibrées sur cette taille : c'était le rôle des portées du profil `prism`,
  supprimé pour cette raison (un profil dédié ne justifiait plus sa
  maintenance).
- Une reprise (SYNE-225) reste refusée si la grille du checkpoint PRISM diffère
  de la grille annoncée : c'est voulu, un monde différent n'est pas le même
  monde.
