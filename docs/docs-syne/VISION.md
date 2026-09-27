# VISION.md

**Composant** : SYNE
**Statut** : [STABLE]
**Dernière mise à jour** : 17 septembre 2026
**Dépend de** : `../VISION.md` (racine), `../GLOSSARY.md`
**Source Monographie** : Partie 3 (SYNE : Le Moteur de Simulation), §2.3.1

---

## 1. Rôle de SYNE

SYNE (**Systems & Emergent Network Engine**) est le **moteur .NET autoritaire de simulation et de décision** : il possède la vérité de l'état du monde et calcule ses évolutions, interactions et décisions, indépendamment de toute représentation graphique. PRISM est le projet Unreal final de LIVEX ; il intègre le plugin PRISM-LDK (*LIVEX Development Kit*, module technique `PrismLdk`). L'affichage et le pilotage Unreal ne font pas autorité sur la simulation.

Il fonctionne en **mode headless** (sans interface) et expose des snapshots globaux et des événements d'observabilité, ainsi qu'un contrôle HTTP. Le flux WebSocket précède les snapshots d'un événement `world_initialized` décrivant le monde préparé. Il sauvegarde/restaure son état avec une garantie de **déterminisme bit-à-bit**.

## 2. Principes fondateurs

1. **Le moteur ne dicte pas aux entités ce qu'elles doivent faire** — il fournit des structures (état, besoins, perception, mémoire, croyances, capacités, système de décision, actions). (Monographie §3.7.1)
2. **Indépendance de la présentation** — aucune dépendance à un moteur graphique ; PRISM/Unreal (via PRISM-LDK / `PrismLdk`) et ECHOS consomment les contrats de SYNE sans devenir la source de vérité de la simulation. (Monographie §1.4.6, actualisée)
3. **Déterminisme** — reproductibilité exacte à seed + config + version moteur identiques. (Monographie §3.6.2)
4. **Observabilité** — chaque décision produit une trace (DecisionRecord) ; le flux externe expose l'état global par tick et des événements horodatés/attribués.

## 3. Ce que SYNE n'est pas

- Ce n'est **pas** un moteur physique réaliste (entités logiques, sans collisions physiques — ADR-010).
- Ce n'est **pas** une IA d'apprentissage : la cognition est explicite, déterministe et inspectable.
- Ce n'est **pas** un moteur de rendu : le monde simulé est un plan 2D logique.

## 4. Ambition V0.1

SYNE doit être un **instrument scientifique validable** :
- capacité de 50 → 1000 entités avec un budget de tick maîtrisé ;
- architecture BDI (boucle cognitive en 15 étapes) ;
- communication inter-entités par pulsations lumineuses ;
- persistance (JSON → SQLite) et reprise exacte ;
- la perception des entités reste locale/partielle, tandis que l'observabilité externe peut exposer des snapshots globaux du monde (`world_initialized`, puis `snapshot` par tick) ;
- ensembles de tests massifs (couverture ≥ 80 %, jalon 160+ tests).

## 5. Outil de développement du plugin

[`syne-mock`](../../syne-mock/) est un service séparé, à la racine du dépôt,
qui simule certains contrats et flux SYNE afin de développer et tester
PRISM-LDK (`PrismLdk`) sans lancer le moteur .NET. Il ne constitue ni l'implémentation
de SYNE ni une référence d'équivalence algorithmique : les comportements et
trajectoires du mock ne garantissent pas la parité avec le moteur autoritaire.

## 6. Références

- Architecture : `ARCHITECTURE.md`
- Modèle de données : `DATA_MODEL.md`
- Boucle de simulation : `SIMULATION_LOOP.md`
- Cognition : `COGNITIVE_ARCHITECTURE.md`
- Systèmes : `SYSTEMS_SPEC.md`

---

## Points restés ouverts dans ce document
- Aucun — vision du composant stabilisée ; les détails opérationnels jusqu'à `[OUVERT]` sont déportés vers les docs techniques du composant.