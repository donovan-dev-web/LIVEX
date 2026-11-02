# WORLD_GENERATION — Génération du monde par seed

**Composant** : PRISM
**Statut** : [SPÉCIFICATION V2 — NON IMPLÉMENTÉ]
**Dernière mise à jour** : 1er novembre 2026
**Dépend de** : [`DATA_MODEL.md`](DATA_MODEL.md), [`DETERMINISM.md`](DETERMINISM.md), [`adr/ADR-007-determinisme-entier-flux-hasard.md`](adr/ADR-007-determinisme-entier-flux-hasard.md)
**Source Monographie** : —

---

## 1. Principes

- Le monde est entièrement déterminé par `(runSeed, worldGenVersion, paramètres de génération, pack de contenu)`. Deux hôtes sur deux plateformes produisent la **même empreinte**.
- Génération **par couches**, chaque couche utilisant un **flux nommé** (`terrain`, `ores`, `forest`, `spawn`) : activer ou modifier une couche ne décale pas les autres.
- **Arithmétique entière ou virgule fixe 16.16** exclusivement (ADR-007). Un bruit flottant peut différer entre Windows et Linux et casserait le déterminisme.
- La génération est **de la donnée + un algorithme versionné** : `worldGenVersion` entre dans l'empreinte de reproductibilité ; toute modification qui change un monde incrémente cette version.

## 2. Primitives

| Primitive | Définition |
| :-- | :-- |
| `Hash(ix, iy, iz, stream)` | Mélange entier 64 bits (`Mix64`) → valeur signée sur 16 bits |
| Bruit de valeurs | Interpolation trilinéaire/bilinéaire des hachages du treillis, courbe de lissage polynomiale en virgule fixe |
| Octaves | Somme de `octaves` bruits de fréquences doublées, amplitudes divisées par `persistence` (entier, en permille) |
| Déformation de domaine | Décalage entier des coordonnées par un second bruit avant l'échantillonnage |

## 3. Couches (dans l'ordre)

| # | Couche | Résultat | Paramètres principaux |
| :-- | :-- | :-- | :-- |
| 1 | **Relief** | Carte de hauteur par colonne `surfaceZ(x,y)` | `baseZ = 30`, amplitude ±12, 4 octaves, bordure |
| 2 | **Zones** | Une zone par colonne : *plaine*, *forêt*, *roche*, *bord d'eau* | Bruit basse fréquence + seuils ; la hauteur et la proximité d'eau influencent la zone |
| 3 | **Strates** | `bedrock` (z = 0), `stone` jusqu'à `surfaceZ − dirtDepth`, `dirt` (3–4 couches), couche de surface selon la zone (`grass`, `sand`, `stone` nu) | `dirtDepth`, `strataJitter` |
| 4 | **Filons** | `coal` et `iron_ore` en grappes par bruit 3D seuillé ; probabilité par plage de profondeur | `coalVeinThreshold`, `ironVeinThreshold`, plages de z |
| 5 | **Eau** | Nappes : bassins sous `seaLevelZ = 26` remplis de `water` (bloc source statique) ; **lacs** dans les cuvettes par seuil local | `seaLevelZ`, `lakeMinDepth` |
| 6 | **Forêts** | Arbres (gabarits de `templates/trees`) placés sur une grille à gigue (cellule 5 × 5) avec densité par zone | `forestDensity` par zone, `treeSpacing` |
| 7 | **Sites d'apparition** | Cases de départ choisies en plaine, à portée d'eau, de bois et de roche | `spawnMinSpacing`, `spawnCandidates` |

### 3.1 Bordure

Le monde est **borné** ; hors limites = solide. La bordure est une **falaise** : sur `borderWidth` (défaut 6) colonnes, la hauteur est portée à `maxZ − 8` et la surface remplacée par `stone`, de sorte qu'aucun agent ne tente de la franchir et que la limite soit lisible dans le visualiseur.

### 3.2 Zones et contenu garanti

| Zone | Rôle | Contenu typique |
| :-- | :-- | :-- |
| Plaine | Habitat et agriculture | `grass`, sol plat, un peu de bois |
| Forêt | Bois | Arbres denses, sol `grass`/`dirt` |
| Roche | Minéraux | `stone` affleurant, filons exposés par l'érosion (falaises) |
| Bord d'eau | Eau, sable | `sand`, lacs, rives marchables |

Chaque zone doit rester **atteignable à pied** depuis les sites d'apparition (voir §4).

## 4. Contrôles de jouabilité

Après génération, un contrôle déterministe vérifie :

1. Chaque site d'apparition atteint, par un chemin de longueur ≤ `maxResourceDistance` (hypothèse : 120 blocs), au moins un bloc de chaque catégorie *eau*, *bois*, *pierre*, *charbon*, *minerai de fer exposé*, *terre labourable*.
2. Aucune zone de départ n'est enclavée.
3. La quantité totale de chaque ressource de base dépasse un plancher paramétré.

En cas d'échec, la génération **réessaie avec une graine dérivée** (`attempt = 1…8`, `Mix64(seed, attempt)`), et l'index retenu est inscrit dans les métadonnées du monde (et donc dans l'empreinte). Au-delà de 8 échecs, le démarrage échoue avec une erreur structurée.

## 5. Sorties

- Monde (tableaux de chunks), `WorldMetadata{ worldGenVersion, attempt, zoneMap, spawnSites[], contentHash }`.
- **Empreinte du monde généré** (hachage ordonné des chunks) : référence de test (« mondes dorés », `TESTING.md` §3).

## 6. Performance attendue

Génération complète en moins de quelques secondes sur une machine de développement (4 M blocs) ; mesurée par le benchmark B6 (ISSUES `PRISM-0xx`). La génération est parallélisable **par chunk** pour les couches sans dépendance inter-chunks (3, 4, 5) ; le relief, les zones et les forêts sont calculés par colonne depuis des échantillons purs.

## Points restés ouverts dans ce document

Les valeurs de bruit (fréquences, seuils) se fixent par itération visuelle dans le visualiseur de debug (phase P3). Elles ne changent jamais l'API.

## Mises à jour

| Date | Changement | Motif |
| :-- | :-- | :-- |
| 1er novembre 2026 | Création | Refonte V2 |
