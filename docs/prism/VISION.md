# VISION — PRISM (V2)

**Composant** : PRISM
**Statut** : [SPÉCIFICATION V2 — NON IMPLÉMENTÉ]
**Dernière mise à jour** : 1er novembre 2026
**Dépend de** : [`../presentation/VISION.md`](../presentation/VISION.md), [`adr/ADR-004-refonte-prism-monde-voxel-autoritaire.md`](adr/ADR-004-refonte-prism-monde-voxel-autoritaire.md)
**Source Monographie** : —

---

## 1. Le problème

Dans LIVEX V1, un agent vit dans un monde pauvre : deux besoins matériels (eau, nourriture) se satisfont en « allant chercher » une réserve globale. Il n'y a rien à fabriquer, stocker, transporter ni bâtir, donc **aucune raison structurelle de coopérer**. Dans le même temps, SYNE porte à la fois la cognition des agents *et* le monde (grille, obstacles, ressources, saisons, navigation, perception géométrique), ce qui alourdit le moteur et brouille ses responsabilités.

PRISM V2 répond à ce double problème : **élargir ce que les agents peuvent faire** et **alléger SYNE de la gestion du monde**.

## 2. Objectifs

| # | Objectif | Mesure de réussite |
| :-- | :-- | :-- |
| O1 | **Étendre le champ des possibles** : chaînes de production (extraction, outils, agriculture, stockage, construction) à la place de « aller chercher » | Cinq chaînes jouables de bout en bout par des agents (jalon G4) |
| O2 | **Créer des raisons de coopérer** sans les imposer par script | Effet mesurable des mécanismes de coopération par ablation (jalon G7) |
| O3 | **Séparer les responsabilités** : SYNE = cognition ; PRISM = matière, espace, temps physique | Aucune classe de géométrie ou de ressource dans `Simulation.Core` (jalon G5) |
| O4 | **Rendre le contenu extensible par la donnée** | Un matériau, une recette ou une culture s'ajoute sans recompiler (test de non-régression de contenu) |
| O5 | **Donner à PRISM une identité visuelle forte** : monde voxel lisible, cohérent avec l'idée d'un monde simulé | Visualiseur Silk.NET livré (phase P11) |
| O6 | **Préserver l'existant** : déterminisme par seed, runs headless, comparaison dans ECHOS, orchestration par le Launcher | Reprise bit-à-bit, empreintes dorées, manifestes conformes (jalons G3, G6) |

## 3. Principes de conception

1. **Autorité du monde.** PRISM est la seule source de vérité sur l'espace, la matière et le temps physique. SYNE est la seule source de vérité sur la cognition. Aucun des deux ne duplique l'état de l'autre.
2. **Information locale.** Un agent n'agit que sur ce qu'il a perçu ou mémorisé. PRISM ne répond jamais à un agent par une connaissance globale du monde (voir [`AGENT_INTERFACE.md`](AGENT_INTERFACE.md) §5).
3. **Conservation de la matière.** Rien n'apparaît ni ne disparaît sans règle déclarée. Un registre d'audit le vérifie.
4. **Données avant code.** Matériaux, objets, recettes, gabarits et paramètres sont des fichiers de données validés par schéma. Les agents et les recettes raisonnent sur des **tags**, jamais sur des identifiants.
5. **Déterminisme d'abord.** Arithmétique entière ou à virgule fixe pour tout ce qui influence l'état. Le virgule flottante est réservé au rendu.
6. **Headless d'abord.** La simulation ne dépend d'aucune bibliothèque graphique. Le rendu est un client qui ne peut jamais ralentir le tick.
7. **Pas de coopération scriptée.** Le monde fournit des coûts, des délais, des biens non rivaux et des risques ; il ne fournit aucune récompense directe pour « être en groupe ».

## 4. Périmètre

### Dans le périmètre V2

- Monde de colonnes 2.5D borné (défaut 256 × 256, altitude 0…63), régions horizontales 16 × 16 et génération par seed ; pas de cavernes ni de surplombs.
- Matériaux, objets, recettes, gabarits de structures, cultures : tout par données.
- Agents physiques (corps, inventaire, navigation 3D, conflits).
- Chaînes de production, stockage, péremption, outils à durabilité, paliers.
- Structures et chantiers par gabarits, propriété et accès, usure (optionnelle).
- Contrat SYNE↔PRISM versionné, flux de lecture pour ECHOS et le visualiseur.
- Hôte headless, persistance et reprise bit-à-bit, visualiseur Silk.NET.

### Hors périmètre V2 (décidé)

| Sujet | Raison | Piste ultérieure |
| :-- | :-- | :-- |
| Monde infini / génération à la volée | Risque pour le déterminisme, aucun apport scientifique | Augmenter les dimensions par paramètre |
| Écoulement de l'eau | Règle coûteuse, difficile à rendre déterministe | Extension après validation (ADR-010) |
| Construction libre bloc par bloc | Explose l'espace d'actions et la planification | Étendre le catalogue de gabarits |
| LLM dans la boucle de décision | Coût, non-déterminisme, hors objet | Génération hors boucle de recettes/noms, figée dans les données |
| Combat, faune, météo dynamique | Hors question de recherche | Évolutions futures documentées dans `ROADMAP.md` §9 |
| Modding tiers / API de plugins | Surface de support | Packs de contenu signés (post-V2) |
| Mode joueur-habitant | Dépend d'un monde stable | Après V2 |

## 5. Questions de recherche que le monde doit rendre posables

- Former des groupes, partager des stocks et bâtir des ouvrages collectifs devient-il avantageux, et dans quelles conditions ?
- Quels mécanismes (coûts fixes partagés, division du travail, lissage du risque, logistique, biens collectifs, partage d'information, travail parallèle) pèsent le plus ? (Ablations : [`TESTING.md`](TESTING.md) §6.)
- Que se passe-t-il si les recettes se découvrent par essai plutôt que d'être connues ? (Variante riche : [`AGENT_INTERFACE.md`](AGENT_INTERFACE.md) §7.)

> **Position scientifique** (héritée du README racine). PRISM fournit un dispositif d'expérimentation, pas une preuve d'émergence. L'hypothèse de l'auteur — les chaînes de production créent des situations où coopérer rapporte — tient *sous conditions* et peut échouer ; la documentation prévoit de la tester, y compris pour la réfuter.

## 6. Identité

Un monde de blocs **lisible** (on comprend d'un coup d'œil ce qui s'y passe), **modifiable** (par les données, par les agents, et par des interventions journalisées) et **cohérent** avec l'idée d'un monde simulé : palette sobre à portée scientifique, lumière et jour/nuit qui portent l'information, aucune animation décorative qui laisserait croire à de la vie là où il n'y a que de l'état (voir [`RENDERING_SPEC.md`](RENDERING_SPEC.md)).

## Points restés ouverts dans ce document

Aucun : les questions ouvertes de la V2 sont tenues dans [`adr/README.md`](adr/README.md) §3.

## Mises à jour

| Date | Changement | Motif |
| :-- | :-- | :-- |
| 1er novembre 2026 | Création | Refonte V2 |
