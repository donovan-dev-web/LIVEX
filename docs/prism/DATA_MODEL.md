# DATA_MODEL — Modèle de données du monde PRISM (V2)

**Composant** : PRISM
**Statut** : [SPÉCIFICATION V2 — NON IMPLÉMENTÉ]
**Dernière mise à jour** : 1er novembre 2026
**Dépend de** : [`adr/ADR-006-modele-voxel-borne-chunks-tags.md`](adr/ADR-006-modele-voxel-borne-chunks-tags.md), [`CONTENT_SPEC.md`](CONTENT_SPEC.md)
**Source Monographie** : —

---

## 1. Paramètres de départ

Valeurs **hypothétiques**, à calibrer (inventaire complet : `CONTENT_SPEC.md` §7).

| Paramètre | Valeur | Conséquence |
| :-- | :-- | :-- |
| Dimensions | 256 × 256 × 64 blocs (X, Y horizontaux, Z vertical) | 4 194 304 blocs, 65 536 colonnes |
| Chunk | 16 × 16 × 16 blocs | 16 × 16 × 4 = 1 024 chunks de 4 096 blocs |
| Bloc | 1 m de côté | Un agent occupe 1 bloc au sol et 2 en hauteur |
| Octets par bloc | 3 (identifiant 16 bits + état 8 bits) | ≈ 12,6 Mo pour le monde dense |
| Types de blocs | jusqu'à 65 536 ; une quinzaine au départ | Le contenu s'ajoute par données |
| Densité d'agents | 100 agents : 1 pour 655 colonnes ; 1 000 : 1 pour 65 | Monde vaste face à la population |
| Rayon de communication | 50 blocs (disque de 100 de diamètre, ≈ 39 % de la largeur) | Calculé côté SYNE sur les positions |

## 2. Repères et indices

- Coordonnée de bloc `(x, y, z)`, `0 ≤ x,y < 256`, `0 ≤ z < 64`. Hors limites = **solide infranchissable** (bordure).
- Coordonnée de chunk `(cx, cy, cz) = (x>>4, y>>4, z>>4)` ; `chunkId = cx + 16·(cy + 16·cz)`.
- Index local `i = lx + 16·(ly + 16·lz)` (0 à 4 095).
- Position d'un agent en **milli-blocs** (`int`, 1 000 par bloc) ; case de référence = `floor(milli / 1000)`.

## 3. Le bloc

Un bloc = **type** (`ushort`, 0 = air) + **état** (`byte`).

| Champ | Rôle | Exemples d'état |
| :-- | :-- | :-- |
| Type | Renvoie à la fiche de matériau | `stone`, `wheat_crop` |
| État | Ce qui varie d'un bloc à l'autre | stade de croissance 0–3, orientation, niveau de remplissage |

La **fiche de matériau** (données, [`CONTENT_SPEC.md`](CONTENT_SPEC.md) §2) porte :

| Propriété | Rôle |
| :-- | :-- |
| `solid` | Franchissable ou non |
| `hardness`, `minToolTier` | Durée de minage et palier d'outil minimal requis |
| `tags` | Catégories pour agents et recettes (`bois`, `pierre`, `minerai`, `combustible`, `comestible`) |
| `drops` | Objets obtenus quand le bloc est miné ou récolté |
| `requires` | Conditions d'évolution (sol labouré, eau proche) |
| `evolves` | Règle de transformation dans le temps (stades, durées) |
| `station` | Nom de station de craft si le bloc en est une (`etabli`, `four`) |
| `container` | Capacité d'inventaire si le bloc est un conteneur (`coffre`) |
| `perceivable` | Le bloc est-il remonté dans les observations (filtre d'intérêt) |
| `infinite` | Source inépuisable (eau), exception documentée à la conservation |

## 4. Chunks

| Élément | Règle |
| :-- | :-- |
| Stockage | `ushort[4096] ids` + `byte[4096] states` ; chunk **uniforme** (un seul type, état 0) représenté sans tableau |
| *Dirty flag* | Positionné à toute écriture ; sert au hachage, à la persistance, au flux de lecture |
| Hachage | FNV-1a 64 bits de `ids‖states` ; recalculé seulement si dirty |
| Résumé | Par chunk : masque de tags présents (blocs **exposés** uniquement), compteur de structures, version |
| Verrous | Aucun : les écritures sont séquentielles dans la phase d'application (`SIMULATION_LOOP.md` §2) |

Un bloc est **exposé** s'il a au moins un voisin (6-voisinage) non solide ou liquide. Les résumés de chunk et la perception ne remontent que des blocs exposés (anti-« rayons X », Q-07).

## 5. Objets, piles et conteneurs

> Un bloc occupe la grille ; un objet vit dans un inventaire, un conteneur ou au sol. Miner produit des objets ; poser un bloc en consomme un.

| Concept | Description |
| :-- | :-- |
| `ItemType` | Fiche d'objet (données) : tags, `maxStack`, `toolTier`, `durability`, `shelfLifeTicks`, `effects` (besoins), `weight` |
| `ItemStack` | `(itemTypeId, quantity, freshnessBucket)` ; plusieurs piles d'un même objet n'existent que si leurs fraîcheurs diffèrent |
| Fraîcheur | Durée de vie restante en ticks en virgule fixe, décrémentée **paresseusement** (à l'accès ou tous les `freshnessSweepTicks`) selon le taux de décroissance du conteneur |
| Durabilité | Points restants par outil ; un outil est une pile de quantité 1 avec sa durabilité |
| Inventaire d'agent | `inventorySlots` emplacements (défaut 12) + un emplacement « outil en main » |
| Conteneur | Associé à un bloc (`coffre`) ou à une structure (`grenier`) ; capacité et taux de péremption propres ; liste d'accès |
| Objet au sol | `GroundItem{id, position, stack}` ; fusion des piles compatibles sur la même case |

## 6. Agents physiques

| Champ | Description |
| :-- | :-- |
| `agentId` | Identifiant stable du run, fourni par SYNE à l'incarnation |
| `position` | Milli-blocs `(x, y, z)` ; z posé sur un support solide |
| `profile` | Facteurs immuables transmis à l'incarnation : `speedPermille` (issu du trait `speed`), `carrySlots`, `skills` initiales |
| `mastery[]` | Maîtrise par catégorie d'action (cultiver, forger, bâtir, miner…) en points entiers |
| `inventory` | Voir §5 |
| `activeAction` | Machine à états de la macro-action en cours (étape, cible, progression en ticks) |
| `status` | Indicateurs transmis à SYNE : `sheltered`, `onPath`, `nightExposure` (voir `API_CONTRACTS.md` §5) |

Règles : un agent a un corps de 1 × 1 × 2 blocs ; **pas de collision entre agents** (Q-08) ; collision avec les blocs solides ; **montée** de 1 bloc sans coût supplémentaire, **descente** jusqu'à 3 blocs, au-delà infranchissable ; l'eau (liquide) est infranchissable ; on la boit depuis une case adjacente.

## 7. Structures, chantiers

| Concept | Description |
| :-- | :-- |
| `Structure` | `(structureId, templateId, origin, rotation, integrity, accessList)` ; créée à l'achèvement d'un chantier |
| Intégrité | Rapport entre blocs du gabarit présents et attendus, vérifié paresseusement ; l'effet cesse sous un seuil |
| `Site` (chantier) | `(siteId, templateId, origin, rotation, requiredMaterials, depositedMaterials, workTicksDone, state)` ; états : `planned → supplied → inProgress → completed | abandoned` |
| `accessList` | Liste d'identifiants autorisés (agents ou « groupes » opaques) avec droits `use`, `take`, `put`, `build` |

## 8. Tables annexes

`Calendar` (tick → jour, saison, heure), `ScheduledEvents` (roue temporelle), `MatterLedger` (audit par `ruleId`), `Registries` (types de blocs, objets, recettes, gabarits, tags → index stables).

## 9. Invariants

1. Chaque position `(x,y,z)` appartient à exactement un chunk.
2. Un agent n'est jamais dans un bloc solide.
3. Aucun bloc n'est posé sur une case occupée par un agent.
4. Conservation : voir `SYSTEMS_SPEC.md` §1.
5. Identifiants de types stables pendant un run.

## Points restés ouverts dans ce document

Q-07, Q-08 (`adr/README.md` §3).

## Mises à jour

| Date | Changement | Motif |
| :-- | :-- | :-- |
| 1er novembre 2026 | Création | Refonte V2 |
