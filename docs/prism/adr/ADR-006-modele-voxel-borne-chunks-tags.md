# ADR-006 : Modèle voxel borné, chunks, bloc de 3 octets, tags

**Composant** : PRISM
**Statut** : [Proposed]
**Dernière mise à jour** : 1er novembre 2026
**Dépend de** : ADR-004
**Source Monographie** : —

---

## Contexte

Le monde doit être vaste face à la population, borné pour le déterminisme, économe en mémoire, et extensible sans toucher au code.

## Décision

- **Grille bornée** 256 × 256 × 64 blocs (paramètre), 1 bloc = 1 m, pas de génération infinie.
- **Chunks** 16 × 16 × 16 : 1 024 chunks de 4 096 blocs.
- **Bloc = 3 octets** : identifiant de type 16 bits + octet d'état. Stockage en tableaux séparés par chunk (`ushort[4096]`, `byte[4096]`) avec représentation uniforme (chunk entièrement d'air) sans tableau. ≈ 12,6 Mo pour le monde dense.
- **Types** : jusqu'à 65 536 ; identifiants **stables pendant un run**, résolus depuis des clés textuelles (`"stone"`) à la compilation du contenu.
- **Tags** : les agents et les recettes raisonnent sur des tags (`combustible`, `bois`, `comestible`…), jamais sur des identifiants.
- **Blocs ≠ objets** : un bloc occupe la grille ; un objet vit dans un inventaire, un conteneur ou au sol.

## Conséquences

### Positives
- Mémoire prévisible, hachage et sérialisation par chunk, mises à jour localisées.
- Ajout de contenu sans recompilation.

### Négatives
- Un octet d'état limite l'information par bloc ; les données riches vont dans des tables annexes (conteneurs, structures).
- Monde borné : un plus grand monde se paie en mémoire (linéaire).

### Risques
- 256 × 256 insuffisant pour 1 000 agents : la taille est un paramètre, la densité (1 agent pour 65 colonnes) est à vérifier au calibrage.

## Alternatives considérées

- **Quadtree/octree creux** : refusé, complexité sans gain à cette taille.
- **Blocs porteurs de données arbitraires** : refusé, rompt la compacité et la sérialisation.

## Validation / rejet

Validé au jalon G2 (tests de round-trip, empreinte stable, budget mémoire). Réouvert si la mémoire par monde dépasse le budget de `PERFORMANCE.md` §5.

---

## Mises à jour

| Date | Changement | Motif |
| :-- | :-- | :-- |
| 1er novembre 2026 | Création | Refonte V2 |
