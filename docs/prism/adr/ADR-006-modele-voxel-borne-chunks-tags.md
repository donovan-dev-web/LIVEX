# ADR-006 : Monde borné, régions, blocs et tags

**Composant** : PRISM
**Statut** : [Accepted — géométrie précisée par ADR-011]
**Dernière mise à jour** : 2 novembre 2026
**Dépend de** : ADR-004
**Source Monographie** : —

---

## Contexte

Le monde doit être vaste face à la population, borné pour le déterminisme,
économe en mémoire et extensible sans toucher au code. Le volume voxel dense
envisagé initialement a été remplacé par le modèle 2.5D d'ADR-011 avant
l'implémentation.

## Décision

- Monde borné de 256 × 256 colonnes horizontales, altitude entière 0…63,
  1 unité = 1 mètre ; aucune génération infinie.
- Régions horizontales de 16 × 16 colonnes : 256 régions pour le monde de
  référence, sans partition verticale.
- Une colonne stocke en tableaux SoA son altitude de surface (`byte`), son
  matériau de surface (`ushort`), son état (`byte`) et son profil de strates
  (`ushort`), soit 6 octets par colonne et ≈ 384 Kio pour les métadonnées de
  surface.
- Les couches géologiques de base sont déterminées par le profil de contenu ;
  les blocs minés, posés ou altérés sont des surcharges clairsemées.
- Jusqu'à 65 536 types de blocs, identifiés de manière stable pendant un run
  et résolus depuis des clés textuelles au chargement du contenu.
- Les agents et recettes raisonnent sur des tags, jamais sur des identifiants
  internes de blocs.
- Les blocs occupent une colonne à une altitude ; les objets vivent dans un
  inventaire, un conteneur ou au sol.

## Conséquences

### Positives
- Mémoire de surface prévisible, hachage et sérialisation par région, mises à
  jour localisées.
- Ajout de contenu sans recompilation.

### Négatives
- Les couches de base sont dérivées des profils ; les modifications
  individuelles nécessitent une surcharge clairsemée.
- Monde borné : augmenter sa superficie augmente linéairement les métadonnées.

### Risques
- La densité d'agents et le coût des surcharges sont à vérifier au calibrage.
- L'ordre d'itération des modifications ne doit jamais influer sur les
  empreintes.

## Alternatives considérées

- **Volume dense 3D par chunks** : remplacé par ADR-011 ; la géométrie
  volumique n'est pas nécessaire au périmètre V2.
- **Quadtree/octree creux** : écarté, complexité sans gain pour la carte bornée.
- **Blocs porteurs de données arbitraires** : écarté, rompt la compacité et la
  sérialisation.

## Validation / rejet

Les limites, identifiants stables et tags sont acceptés. La géométrie 2.5D est
validée au jalon G2 (round-trip, empreinte stable, jouabilité et budget
mémoire), selon ADR-011.

---

## Mises à jour

| Date | Changement | Motif |
| :-- | :-- | :-- |
| 2 novembre 2026 | Géométrie dense remplacée par le modèle 2.5D d'ADR-011 | Optimisation décidée avant l'implémentation |
| 1er novembre 2026 | Création | Refonte V2 |
